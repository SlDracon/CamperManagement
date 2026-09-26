using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CamperManagement.Models;
using MySqlConnector;
namespace CamperManagement.Services;

public sealed class DatabaseService : IDatabaseService
{
    // The explicit constructor is used by all tests; it never falls back to the live server.
    private readonly string? _connectionString;
    private readonly TimeProvider _clock;
    public DatabaseService() { _clock = TimeProvider.System; }
    public DatabaseService(string connectionString, TimeProvider? clock = null)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Datenbankverbindung fehlt.");
        _connectionString = new MySqlConnectionStringBuilder(connectionString) { UseAffectedRows = false }.ConnectionString;
        _clock = clock ?? TimeProvider.System;
    }
    public async Task<MySqlConnection> OpenConnectionAsync()
    {
        var c = new MySqlConnection(_connectionString ?? DatabaseConfiguration.Validate(DatabaseConfiguration.Current.Load()));
        try
        {
            await c.OpenAsync();
            return c;
        }
        catch { await c.DisposeAsync(); throw; }
    }
    private static MySqlCommand Command(MySqlConnection c, string sql, MySqlTransaction? tx = null, params (string, object?)[] args)
    {
        var cmd = new MySqlCommand(sql, c, tx);
        foreach (var (key, value) in args)
            cmd.Parameters.AddWithValue(key, value ?? DBNull.Value);
        return cmd;
    }
    private async Task<List<T>> ReadAsync<T>(string sql, Func<MySqlDataReader, T> map, params (string, object?)[] args)
    {
        await using var c = await OpenConnectionAsync();
        await using var cmd = Command(c, sql, null, args);
        await using var r = await cmd.ExecuteReaderAsync();
        var result = new List<T>();
        while (await r.ReadAsync())
            result.Add(map(r));
        return result;
    }
    private static string Text(MySqlDataReader r, string col) => r[col] is DBNull ? "" : Convert.ToString(r[col], System.Globalization.CultureInfo.InvariantCulture) ?? "";
    private const string Active = "c.active=1 AND (c.deactivated IS NULL OR c.deactivated='0000-00-00 00:00:00')";
    // Resolve only the designated billing person, once, even with additional contacts.
    private const string PersonJoin = "JOIN camper_personen cp ON cp.id=(SELECT MIN(cp2.id) FROM camper_personen cp2 WHERE cp2.camper_id=c.id AND cp2.rechnungsadresse=1) JOIN personen pers ON pers.id=cp.personen_id";
    public Task<List<CamperDisplayModel>> GetActiveCampersAsync() => ReadAsync($"SELECT c.id,p.platznr,pers.*,c.Vertragskosten FROM camper c JOIN plaetze p ON p.id=c.platz_id {PersonJoin} WHERE {Active} ORDER BY p.platznr", r => new CamperDisplayModel
    {
        Id = r.GetInt32(0),
        Platznr = Text(r, "platznr"),
        Anrede = Text(r, "anrede"),
        Vorname = Text(r, "vorname"),
        Nachname = Text(r, "nachname"),
        Straße = Text(r, "strasse"),
        PLZ = Text(r, "plz"),
        Ort = Text(r, "ort"),
        Email = Text(r, "email"),
        Vertragskosten = r.GetDecimal("Vertragskosten")
    });
    public Task<List<RechnungDisplayModel>> GetRechnungenAsync() => ReadAsync("""
        SELECT r.*,p.platznr,s.camper_id,s.anrede,s.vorname,s.nachname,s.strasse,s.plz,s.ort
        FROM rechnungen r JOIN plaetze p ON p.id=r.platz_id
        LEFT JOIN rechnung_empfaenger s ON s.rechnung_id=r.id ORDER BY r.id DESC
        """, r => new RechnungDisplayModel { Id = r.GetInt32("id"), Platznr = Text(r, "platznr"), Alt = r.GetDecimal("alt"), Neu = r.GetDecimal("neu"), Verbrauch = r.GetDecimal("verbrauch"), Faktor = r.GetDecimal("faktor"), Betrag = r.GetDecimal("betrag"), Jahr = r.GetInt32("jahr"), Art = Text(r, "type"), Gedruckt = r.GetBoolean("printed") ? "Ja" : "Nein", CamperId = r["camper_id"] is DBNull ? null : r.GetInt32("camper_id"), RecipientResolved = r["camper_id"] is not DBNull, Anrede = Text(r, "anrede"), Vorname = Text(r, "vorname"), Nachname = r["camper_id"] is DBNull ? "Zuordnung prüfen" : Text(r, "nachname"), Straße = Text(r, "strasse"), PLZ = Text(r, "plz"), Ort = Text(r, "ort") });
    public Task<List<string>> GetPlatznummernAsync() => ReadAsync("SELECT DISTINCT platznr FROM plaetze ORDER BY platznr", r => r.GetString(0));
    public Task<List<int>> GetAvailableJahreAsync() => ReadAsync("SELECT DISTINCT jahr FROM rechnungen ORDER BY jahr DESC", r => r.GetInt32(0));
    public async Task<int> GetPlatzIdByPlatznummerAsync(string? platznummer)
    {
        if (string.IsNullOrWhiteSpace(platznummer))
            throw new ArgumentException("Bitte einen Platz auswählen.");
        var ids = await ReadAsync("SELECT id FROM plaetze WHERE platznr=@p", r => r.GetInt32(0), ("@p", platznummer));
        return ids.Count == 1 ? ids[0] : throw new InvalidOperationException("Platz wurde nicht eindeutig gefunden.");
    }
    private static async Task<int> LockPlatzAsync(MySqlConnection c, MySqlTransaction tx, string? platznummer)
    {
        await using var cmd = Command(c, "SELECT id FROM plaetze WHERE platznr=@p FOR UPDATE", tx, ("@p", platznummer));
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync())
            throw new InvalidOperationException("Platz wurde nicht gefunden.");
        var id = r.GetInt32(0);
        if (await r.ReadAsync())
            throw new InvalidOperationException("Platznummer ist nicht eindeutig.");
        return id;
    }
    public async Task DeactivateOldCamperAsync(string? platznummer)
    {
        await using var c = await OpenConnectionAsync();
        await using var tx = await c.BeginTransactionAsync();
        var id = await LockPlatzAsync(c, tx, platznummer);
        await using var cmd = Command(c, $"UPDATE camper c SET active=0,deactivated=NOW(),updated=NOW() WHERE platz_id=@id AND {Active}", tx, ("@id", id));
        await cmd.ExecuteNonQueryAsync();
        await tx.CommitAsync();
    }
    public async Task AddNewCamperAsync(CamperDisplayModel value)
    {
        BillingRules.ValidateCamper(value);
        await using var c = await OpenConnectionAsync();
        await using var tx = await c.BeginTransactionAsync();
        var platzId = await LockPlatzAsync(c, tx, value.Platznr);
        await using (var deactivate = Command(c, $"UPDATE camper c SET active=0,deactivated=NOW(),updated=NOW() WHERE platz_id=@id AND {Active}", tx, ("@id", platzId)))
            await deactivate.ExecuteNonQueryAsync();
        await using var person = Command(c, "INSERT INTO personen(anrede,vorname,nachname,strasse,plz,ort,email,created,updated) VALUES(@a,@v,@n,@s,@z,@o,@e,NOW(),NOW())", tx, PersonArgs(value));
        await person.ExecuteNonQueryAsync();
        await using var camper = Command(c, "INSERT INTO camper(platz_id,active,created,updated,Vertragskosten) VALUES(@p,1,NOW(),NOW(),@k)", tx, ("@p", platzId), ("@k", BillingRules.Round(value.Vertragskosten)));
        await camper.ExecuteNonQueryAsync();
        await using var link = Command(c, "INSERT INTO camper_personen(camper_id,personen_id,rechnungsadresse) VALUES(@c,@p,1)", tx, ("@c", camper.LastInsertedId), ("@p", person.LastInsertedId));
        await link.ExecuteNonQueryAsync();
        await tx.CommitAsync();
    }
    private static (string, object?)[] PersonArgs(CamperDisplayModel v) => new (string, object?)[] { ("@a", v.Anrede?.Trim() ?? ""), ("@v", v.Vorname?.Trim()), ("@n", v.Nachname?.Trim()), ("@s", v.Straße?.Trim()), ("@z", v.PLZ?.Trim()), ("@o", v.Ort?.Trim()), ("@e", v.Email?.Trim() ?? "") };
    public async Task UpdateCamperAsync(CamperDisplayModel value)
    {
        BillingRules.ValidateCamper(value);
        await using var c = await OpenConnectionAsync();
        await using var tx = await c.BeginTransactionAsync();
        var platz = await LockPlatzAsync(c, tx, value.Platznr);
        await using var cmd = Command(c, $"UPDATE camper c {PersonJoin} SET pers.anrede=@a,pers.vorname=@v,pers.nachname=@n,pers.strasse=@s,pers.plz=@z,pers.ort=@o,pers.email=@e,pers.updated=NOW(),c.Vertragskosten=@k,c.updated=NOW() WHERE c.id=@id AND c.platz_id=@p AND {Active}", tx, PersonArgs(value).Concat(new (string, object?)[] { ("@id", value.Id), ("@p", platz), ("@k", BillingRules.Round(value.Vertragskosten)) }).ToArray());
        if (await cmd.ExecuteNonQueryAsync() == 0)
            throw new InvalidOperationException("Der Camper wurde inzwischen geändert oder deaktiviert. Bitte neu laden.");
        await tx.CommitAsync();
    }
    public async Task<List<KostenEintrag>> GetRechnungenForJahrAsync(int jahr)
    {
        return await ReadAsync("""
            SELECT p.platznr,COALESCE(s.vorname,'') vorname,COALESCE(s.nachname,'Zuordnung prüfen') nachname,
              SUM(CASE WHEN r.type='Wasser' THEN r.betrag ELSE 0 END) wasser,
              SUM(CASE WHEN r.type='Strom' THEN r.betrag ELSE 0 END) strom,COALESCE(MAX(s.vertragskosten),0) kosten
            FROM rechnungen r JOIN plaetze p ON p.id=r.platz_id LEFT JOIN rechnung_empfaenger s ON s.rechnung_id=r.id
            WHERE r.jahr=@jahr GROUP BY p.id,p.platznr,s.camper_id,s.vorname,s.nachname ORDER BY p.platznr,s.nachname
            """, r => new KostenEintrag { PlatzNr = Text(r, "platznr"), Vorname = Text(r, "vorname"), Nachname = Text(r, "nachname"), WasserBetrag = r.GetDecimal("wasser"), StromBetrag = r.GetDecimal("strom"), Vertragskosten = r.GetDecimal("kosten") }, ("@jahr", jahr));
    }
    public Task MarkRechnungAsPrintedAsync(int id) => MarkRechnungenAsPrintedAsync(new[] { id });
    public async Task MarkRechnungenAsPrintedAsync(IReadOnlyCollection<int> ids)
    {
        if (ids.Count == 0)
            return;
        await using var c = await OpenConnectionAsync();
        await using var tx = await c.BeginTransactionAsync();
        foreach (var id in ids.Distinct().Order())
        {
            await using var cmd = Command(c, "UPDATE rechnungen SET printed=1 WHERE id=@id", tx, ("@id", id));
            if (await cmd.ExecuteNonQueryAsync() != 1)
                throw new InvalidOperationException("Eine Rechnung wurde nicht gefunden. Kein Druckstatus wurde geändert.");
        }
        await tx.CommitAsync();
    }
    public async Task AddRechnungAsync(Rechnung value)
    {
        BillingRules.ValidateInvoice(value, _clock, true);
        await using var c = await OpenConnectionAsync();
        await using var tx = await c.BeginTransactionAsync();
        await using (var place = Command(c, "SELECT id FROM plaetze WHERE id=@id FOR UPDATE", tx, ("@id", value.PlatzId)))
            if (await place.ExecuteScalarAsync() is null)
                throw new InvalidOperationException("Platz wurde nicht gefunden.");
        await using var cmd = Command(c, "INSERT INTO rechnungen(platz_id,alt,neu,verbrauch,faktor,betrag,jahr,type,created) VALUES(@p,@a,@n,@v,@f,@b,@j,@t,NOW())", tx, InvoiceArgs(value));
        await cmd.ExecuteNonQueryAsync();
        var id = (int)cmd.LastInsertedId;
        await using var recipient = Command(c, $"INSERT INTO rechnung_empfaenger(rechnung_id,camper_id,anrede,vorname,nachname,strasse,plz,ort,vertragskosten) SELECT @id,c.id,pers.anrede,pers.vorname,pers.nachname,pers.strasse,pers.plz,pers.ort,c.Vertragskosten FROM camper c {PersonJoin} WHERE c.platz_id=@p AND {Active}", tx, ("@id", id), ("@p", value.PlatzId));
        if (await recipient.ExecuteNonQueryAsync() != 1)
            throw new InvalidOperationException("Dem Platz muss genau ein aktiver Camper mit Rechnungsadresse zugeordnet sein.");
        await tx.CommitAsync();
        value.Id = id;
    }
    private static (string, object?)[] InvoiceArgs(Rechnung r) => new (string, object?)[] { ("@p", r.PlatzId), ("@a", r.Alt), ("@n", r.Neu), ("@v", r.Verbrauch), ("@f", r.Faktor), ("@b", BillingRules.Round(r.Betrag)), ("@j", r.Jahr), ("@t", r.Type), ("@id", r.Id) };
    public async Task UpdateRechnungAsync(Rechnung value)
    {
        BillingRules.ValidateInvoice(value, _clock, false);
        await using var c = await OpenConnectionAsync();
        await using var cmd = Command(c, "UPDATE rechnungen SET alt=@a,neu=@n,verbrauch=@v,faktor=@f,betrag=@b,type=@t,updated=NOW() WHERE id=@id AND platz_id=@p AND jahr=@j", null, InvoiceArgs(value));
        if (await cmd.ExecuteNonQueryAsync() != 1)
            throw new InvalidOperationException("Rechnung wurde nicht gefunden oder inzwischen verändert.");
    }
    public async Task<decimal> GetNeuFromLatestRechnungAsync(string? platznummer, string? type)
    {
        if (type is not ("Strom" or "Wasser"))
            throw new ArgumentException("Ungültige Rechnungsart.");
        var values = await ReadAsync("SELECT r.neu FROM rechnungen r JOIN plaetze p ON p.id=r.platz_id WHERE p.platznr=@p AND r.type=@t ORDER BY r.created DESC,r.id DESC LIMIT 1", r => r.GetDecimal(0), ("@p", platznummer), ("@t", type));
        return values.FirstOrDefault();
    }
    public Task<List<AbleseEintrag>> GetAbleseTabelleAsync() => ReadAsync($"""
        SELECT p.platznr,pers.vorname,pers.nachname,
        COALESCE((SELECT neu FROM rechnungen r WHERE r.platz_id=p.id AND r.type='Wasser' ORDER BY r.created DESC,r.id DESC LIMIT 1),0) wasser,
        COALESCE((SELECT neu FROM rechnungen r WHERE r.platz_id=p.id AND r.type='Strom' ORDER BY r.created DESC,r.id DESC LIMIT 1),0) strom
        FROM camper c JOIN plaetze p ON p.id=c.platz_id {PersonJoin} WHERE {Active} ORDER BY p.platznr
        """, r => new AbleseEintrag { PlatzNr = Text(r, "platznr"), Vorname = Text(r, "vorname"), Nachname = Text(r, "nachname"), WasserAlt = r.GetDecimal("wasser"), StromAlt = r.GetDecimal("strom") });
    public async Task<Standardfaktoren> GetStandardfaktorenAsync()
    {
        await using var c = await OpenConnectionAsync();
        await using var cmd = Command(c, "SELECT strom,wasser,version FROM standardfaktoren WHERE id=1");
        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            throw new InvalidOperationException("Die Standardfaktoren fehlen. Bitte die Datenbankmigration ausführen.");
        return new(reader.GetDecimal(0), reader.GetDecimal(1), reader.GetInt64(2));
    }
    public async Task SaveStandardfaktorenAsync(Standardfaktoren value)
    {
        await using var c = await OpenConnectionAsync();
        await using var cmd = Command(c, "UPDATE standardfaktoren SET strom=@s,wasser=@w,version=version+1 WHERE id=1 AND version=@v", null, ("@s", value.Strom), ("@w", value.Wasser), ("@v", value.Version));
        if (await cmd.ExecuteNonQueryAsync() != 1)
            throw new InvalidOperationException("Die Faktoren wurden auf einem anderen Gerät geändert. Bitte neu laden.");
    }
}

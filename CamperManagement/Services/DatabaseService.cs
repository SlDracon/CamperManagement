using System.Threading;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CamperManagement.Models;
using MySqlConnector;
namespace CamperManagement.Services;

public sealed partial class DatabaseService : IDatabaseService
{
    // The explicit constructor is used by all tests; it never falls back to the live server.
    private readonly Func<string> _connectionString;
    private readonly TimeProvider _clock;
    public DatabaseService(Func<string> connectionString, TimeProvider? clock = null) { _connectionString = connectionString; _clock = clock ?? TimeProvider.System; }
    public DatabaseService(string connectionString, TimeProvider? clock = null)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Datenbankverbindung fehlt.");
        var normalized = new MySqlConnectionStringBuilder(connectionString) { UseAffectedRows = false }.ConnectionString;
        _connectionString = () => normalized;
        _clock = clock ?? TimeProvider.System;
    }
    public async Task<MySqlConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var c = new MySqlConnection(_connectionString());
        try
        {
            await c.OpenAsync(cancellationToken);
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
    private async Task<List<T>> ReadAsync<T>(CancellationToken cancellationToken, string sql, Func<MySqlDataReader, T> map, params (string, object?)[] args)
    {
        await using var c = await OpenConnectionAsync(cancellationToken);
        await using var cmd = Command(c, sql, null, args);
        await using var r = await cmd.ExecuteReaderAsync(cancellationToken);
        var result = new List<T>();
        while (await r.ReadAsync(cancellationToken))
            result.Add(map(r));
        return result;
    }
    private static string Text(MySqlDataReader r, string col) => r[col] is DBNull ? "" : Convert.ToString(r[col], System.Globalization.CultureInfo.InvariantCulture) ?? "";
    private const string Active = "c.active=1 AND (c.deactivated IS NULL OR c.deactivated='0000-00-00 00:00:00')";
    // Explicit roles keep holder order stable when the billing address changes.
    // The fallback supports legacy records; additional contacts are never inferred as holders.
    private static string PersonJoin => JoinPersons(false);
    private static string JoinPersons(bool includeMissing) => $"""
        {(includeMissing ? "LEFT JOIN" : "JOIN")} camper_personen cp ON cp.id=(SELECT cp2.id FROM camper_personen cp2
          WHERE cp2.camper_id=c.id AND (cp2.vertragsnehmer_nr=1 OR (cp2.vertragsnehmer_nr IS NULL AND cp2.rechnungsadresse=1))
          ORDER BY cp2.vertragsnehmer_nr IS NULL,cp2.id LIMIT 1)
        {(includeMissing ? "LEFT JOIN" : "JOIN")} personen pers ON pers.id=cp.personen_id
        LEFT JOIN camper_personen cp_partner ON cp_partner.camper_id=c.id AND cp_partner.vertragsnehmer_nr=2
        LEFT JOIN personen partner ON partner.id=cp_partner.personen_id
        """;
    private const string CamperColumns = "c.id,p.platznr,pers.*,c.Vertragskosten,c.gemeinsame_adresse,partner.id zweite_person_id,partner.anrede zweite_anrede,partner.vorname zweiter_vorname,partner.nachname zweiter_nachname,partner.strasse zweite_strasse,partner.plz zweite_plz,partner.ort zweiter_ort,partner.email zweite_email,COALESCE(cp_partner.rechnungsadresse,0) zweite_rechnungsadresse";
    public Task<List<CamperDisplayModel>> GetActiveCampersAsync(CancellationToken cancellationToken = default) =>
        ReadAsync(cancellationToken, $"SELECT {CamperColumns} FROM camper c JOIN plaetze p ON p.id=c.platz_id {PersonJoin} WHERE {Active} ORDER BY p.platznr", MapCamper);
    private static CamperDisplayModel MapCamper(MySqlDataReader r) => new CamperDisplayModel
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
        Vertragskosten = r.GetDecimal("Vertragskosten"),
        HatZweitenVertragsnehmer = r["zweite_person_id"] is not DBNull,
        GemeinsameAdresse = r.GetBoolean("gemeinsame_adresse"),
        RechnungsadresseZweiterVertragsnehmer = r.GetBoolean("zweite_rechnungsadresse") && !r.GetBoolean("gemeinsame_adresse"),
        ZweiteAnrede = Text(r, "zweite_anrede"), ZweiterVorname = Text(r, "zweiter_vorname"), ZweiterNachname = Text(r, "zweiter_nachname"),
        ZweiteStraße = Text(r, r.GetBoolean("gemeinsame_adresse") ? "strasse" : "zweite_strasse"),
        ZweitePLZ = Text(r, r.GetBoolean("gemeinsame_adresse") ? "plz" : "zweite_plz"),
        ZweiterOrt = Text(r, r.GetBoolean("gemeinsame_adresse") ? "ort" : "zweiter_ort"), ZweiteEmail = Text(r, "zweite_email")
    };
    public Task<List<RechnungDisplayModel>> GetRechnungenAsync(CancellationToken cancellationToken = default) => ReadAsync(cancellationToken, """
        SELECT r.*,p.platznr,s.camper_id,s.anrede,s.vorname,s.nachname,s.strasse,s.plz,s.ort,s.zweite_anrede,s.zweiter_vorname,s.zweiter_nachname
        FROM rechnungen r JOIN plaetze p ON p.id=r.platz_id
        LEFT JOIN rechnung_empfaenger s ON s.rechnung_id=r.id ORDER BY r.id DESC
        """, r => new RechnungDisplayModel { Id = r.GetInt32("id"), Platznr = Text(r, "platznr"), Alt = r.GetDecimal("alt"), Neu = r.GetDecimal("neu"), Verbrauch = r.GetDecimal("verbrauch"), Faktor = r.GetDecimal("faktor"), Betrag = r.GetDecimal("betrag"), Jahr = r.GetInt32("jahr"), Art = Text(r, "type"), Gedruckt = r.GetBoolean("printed") ? "Ja" : "Nein", CamperId = r["camper_id"] is DBNull ? null : r.GetInt32("camper_id"), RecipientResolved = r["camper_id"] is not DBNull, Anrede = Text(r, "anrede"), Vorname = Text(r, "vorname"), Nachname = r["camper_id"] is DBNull ? "Zuordnung prüfen" : Text(r, "nachname"), Straße = Text(r, "strasse"), PLZ = Text(r, "plz"), Ort = Text(r, "ort"), ZweiteAnrede = Text(r, "zweite_anrede"), ZweiterVorname = Text(r, "zweiter_vorname"), ZweiterNachname = Text(r, "zweiter_nachname") });
    public Task<List<string>> GetPlatznummernAsync(CancellationToken cancellationToken = default) => ReadAsync(cancellationToken, "SELECT DISTINCT platznr FROM plaetze ORDER BY platznr", r => r.GetString(0));
    public Task<List<int>> GetAvailableJahreAsync(CancellationToken cancellationToken = default) => ReadAsync(cancellationToken, "SELECT DISTINCT jahr FROM rechnungen ORDER BY jahr DESC", r => r.GetInt32(0));
    public async Task<int> GetPlatzIdByPlatznummerAsync(string? platznummer, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(platznummer))
            throw new ArgumentException("Bitte einen Platz auswählen.");
        var ids = await ReadAsync(cancellationToken, "SELECT id FROM plaetze WHERE platznr=@p", r => r.GetInt32(0), ("@p", platznummer));
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
        await DeactivateWithHistoryAsync(c, tx, id);
        await tx.CommitAsync();
    }
    public async Task AddNewCamperAsync(CamperDisplayModel value)
    {
        BillingRules.ValidateCamper(value);
        await using var c = await OpenConnectionAsync();
        await using var tx = await c.BeginTransactionAsync();
        var platzId = await LockPlatzAsync(c, tx, value.Platznr);
        await DeactivateWithHistoryAsync(c, tx, platzId);
        var firstPerson = await InsertPersonAsync(c, tx, value);
        await using var camper = Command(c, "INSERT INTO camper(platz_id,active,created,updated,Vertragskosten,gemeinsame_adresse) VALUES(@p,1,NOW(),NOW(),@k,@g)", tx,
            ("@p", platzId), ("@k", BillingRules.Round(value.Vertragskosten)), ("@g", !value.HatZweitenVertragsnehmer || value.GemeinsameAdresse));
        await camper.ExecuteNonQueryAsync();
        var camperId = (int)camper.LastInsertedId;
        await using var link = Command(c, "INSERT INTO camper_personen(camper_id,personen_id,rechnungsadresse,vertragsnehmer_nr) VALUES(@c,@p,@b,1)", tx,
            ("@c", camperId), ("@p", firstPerson), ("@b", !value.NutztZweiteRechnungsadresse));
        await link.ExecuteNonQueryAsync();
        await SaveSecondPersonAsync(c, tx, camperId, null, value);
        await RecordCamperHistoryAsync(c, tx, "created", null, await ReadCamperStateAsync(c, tx, camperId));
        await tx.CommitAsync();
    }
    private static (string, object?)[] PersonArgs(CamperDisplayModel v) => new (string, object?)[] { ("@a", v.Anrede?.Trim() ?? ""), ("@v", v.Vorname?.Trim()), ("@n", v.Nachname?.Trim()), ("@s", v.Straße?.Trim()), ("@z", v.PLZ?.Trim()), ("@o", v.Ort?.Trim()), ("@e", v.Email?.Trim() ?? "") };
    private static async Task<int> InsertPersonAsync(MySqlConnection c, MySqlTransaction tx, CamperDisplayModel value)
    {
        await using var person = Command(c, "INSERT INTO personen(anrede,vorname,nachname,strasse,plz,ort,email,created,updated) VALUES(@a,@v,@n,@s,@z,@o,@e,NOW(),NOW())", tx, PersonArgs(value));
        await person.ExecuteNonQueryAsync();
        return (int)person.LastInsertedId;
    }
    private static async Task UpdatePersonAsync(MySqlConnection c, MySqlTransaction tx, int personId, CamperDisplayModel value)
    {
        await using var person = Command(c, "UPDATE personen SET anrede=@a,vorname=@v,nachname=@n,strasse=@s,plz=@z,ort=@o,email=@e,updated=NOW() WHERE id=@id", tx,
            PersonArgs(value).Concat(new (string, object?)[] { ("@id", personId) }).ToArray());
        await person.ExecuteNonQueryAsync();
    }
    private static async Task SaveSecondPersonAsync(MySqlConnection c, MySqlTransaction tx, int camperId, int? personId, CamperDisplayModel value)
    {
        if (!value.HatZweitenVertragsnehmer)
        {
            // Remove only the explicit second-holder link. Historical snapshots and contacts remain intact.
            await using var remove = Command(c, "DELETE FROM camper_personen WHERE camper_id=@c AND vertragsnehmer_nr=2", tx, ("@c", camperId));
            await remove.ExecuteNonQueryAsync();
            return;
        }
        var second = new CamperDisplayModel
        {
            Anrede = value.ZweiteAnrede, Vorname = value.ZweiterVorname, Nachname = value.ZweiterNachname, Email = value.ZweiteEmail,
            Straße = value.GemeinsameAdresse ? value.Straße : value.ZweiteStraße,
            PLZ = value.GemeinsameAdresse ? value.PLZ : value.ZweitePLZ,
            Ort = value.GemeinsameAdresse ? value.Ort : value.ZweiterOrt
        };
        if (personId.HasValue)
            await UpdatePersonAsync(c, tx, personId.Value, second);
        else
        {
            personId = await InsertPersonAsync(c, tx, second);
            await using var link = Command(c, "INSERT INTO camper_personen(camper_id,personen_id,rechnungsadresse,vertragsnehmer_nr) VALUES(@c,@p,0,2)", tx,
                ("@c", camperId), ("@p", personId.Value));
            await link.ExecuteNonQueryAsync();
        }
        await using var billing = Command(c, "UPDATE camper_personen SET rechnungsadresse=@b WHERE camper_id=@c AND vertragsnehmer_nr=2", tx,
            ("@b", value.NutztZweiteRechnungsadresse), ("@c", camperId));
        await billing.ExecuteNonQueryAsync();
    }
    public async Task UpdateCamperAsync(CamperDisplayModel value, decimal expectedContractCost)
    {
        BillingRules.ValidateCamper(value);
        await using var c = await OpenConnectionAsync();
        await using var tx = await c.BeginTransactionAsync();
        var platz = await LockPlatzAsync(c, tx, value.Platznr);
        int firstLink, firstPerson;
        int? secondPerson;
        await using (var current = Command(c, $"SELECT cp.id,pers.id,partner.id FROM camper c {PersonJoin} WHERE c.id=@id AND c.platz_id=@p AND {Active} FOR UPDATE", tx,
            ("@id", value.Id), ("@p", platz)))
        await using (var reader = await current.ExecuteReaderAsync())
        {
            if (!await reader.ReadAsync())
                throw new InvalidOperationException("Der Camper wurde inzwischen geändert oder deaktiviert. Bitte neu laden.");
            firstLink = reader.GetInt32(0); firstPerson = reader.GetInt32(1);
            secondPerson = reader.IsDBNull(2) ? null : reader.GetInt32(2);
        }
        var before = await ReadCamperStateAsync(c, tx, value.Id);
        EnsureContractCostUnchanged(before.Camper.Vertragskosten, expectedContractCost);
        await UpdatePersonAsync(c, tx, firstPerson, value);
        await using (var camper = Command(c, "UPDATE camper SET Vertragskosten=@k,gemeinsame_adresse=@g,updated=NOW() WHERE id=@id", tx,
            ("@id", value.Id), ("@k", BillingRules.Round(value.Vertragskosten)), ("@g", !value.HatZweitenVertragsnehmer || value.GemeinsameAdresse)))
            await camper.ExecuteNonQueryAsync();
        await using (var reset = Command(c, "UPDATE camper_personen SET rechnungsadresse=0 WHERE camper_id=@c", tx, ("@c", value.Id)))
            await reset.ExecuteNonQueryAsync();
        await using (var first = Command(c, "UPDATE camper_personen SET vertragsnehmer_nr=1,rechnungsadresse=@b WHERE id=@id", tx,
            ("@id", firstLink), ("@b", !value.NutztZweiteRechnungsadresse)))
            await first.ExecuteNonQueryAsync();
        await SaveSecondPersonAsync(c, tx, value.Id, secondPerson, value);
        await RecordCamperHistoryAsync(c, tx, "updated", before, await ReadCamperStateAsync(c, tx, value.Id));
        await tx.CommitAsync();
    }
    public async Task<List<KostenEintrag>> GetRechnungenForJahrAsync(int jahr, CancellationToken cancellationToken = default)
    {
        return await ReadAsync(cancellationToken, """
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
        await using var recipient = Command(c, $"""
            INSERT INTO rechnung_empfaenger(rechnung_id,camper_id,anrede,vorname,nachname,strasse,plz,ort,vertragskosten,zweite_anrede,zweiter_vorname,zweiter_nachname)
            SELECT @id,c.id,pers.anrede,pers.vorname,pers.nachname,
              CASE WHEN cp_partner.rechnungsadresse=1 AND c.gemeinsame_adresse=0 THEN partner.strasse ELSE pers.strasse END,
              CASE WHEN cp_partner.rechnungsadresse=1 AND c.gemeinsame_adresse=0 THEN partner.plz ELSE pers.plz END,
              CASE WHEN cp_partner.rechnungsadresse=1 AND c.gemeinsame_adresse=0 THEN partner.ort ELSE pers.ort END,
              c.Vertragskosten,COALESCE(partner.anrede,''),COALESCE(partner.vorname,''),COALESCE(partner.nachname,'')
            FROM camper c {PersonJoin} WHERE c.platz_id=@p AND {Active}
            """, tx, ("@id", id), ("@p", value.PlatzId));
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
    public async Task<decimal> GetNeuFromLatestRechnungAsync(string? platznummer, string? type, CancellationToken cancellationToken = default)
    {
        if (type is not ("Strom" or "Wasser"))
            throw new ArgumentException("Ungültige Rechnungsart.");
        var values = await ReadAsync(cancellationToken, "SELECT r.neu FROM rechnungen r JOIN plaetze p ON p.id=r.platz_id WHERE p.platznr=@p AND r.type=@t ORDER BY r.created DESC,r.id DESC LIMIT 1", r => r.GetDecimal(0), ("@p", platznummer), ("@t", type));
        return values.FirstOrDefault();
    }
    public Task<List<AbleseEintrag>> GetAbleseTabelleAsync(CancellationToken cancellationToken = default) => ReadAsync(cancellationToken, $"""
        SELECT p.platznr,pers.vorname,pers.nachname,
        COALESCE((SELECT neu FROM rechnungen r WHERE r.platz_id=p.id AND r.type='Wasser' ORDER BY r.created DESC,r.id DESC LIMIT 1),0) wasser,
        COALESCE((SELECT neu FROM rechnungen r WHERE r.platz_id=p.id AND r.type='Strom' ORDER BY r.created DESC,r.id DESC LIMIT 1),0) strom
        FROM camper c JOIN plaetze p ON p.id=c.platz_id {PersonJoin} WHERE {Active} ORDER BY p.platznr
        """, r => new AbleseEintrag { PlatzNr = Text(r, "platznr"), Vorname = Text(r, "vorname"), Nachname = Text(r, "nachname"), WasserAlt = r.GetDecimal("wasser"), StromAlt = r.GetDecimal("strom") });
    public async Task<Standardfaktoren> GetStandardfaktorenAsync(CancellationToken cancellationToken = default)
    {
        await using var c = await OpenConnectionAsync(cancellationToken);
        await using var cmd = Command(c, "SELECT strom,wasser,version FROM standardfaktoren WHERE id=1");
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
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

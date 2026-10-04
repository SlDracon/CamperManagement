using CamperManagement.Models;
using CamperManagement.Services;
using CamperManagement.Tests;
using MySqlConnector;
namespace CamperManagement.IntegrationTests;

public partial class DatabaseTests : IAsyncLifetime
{
    private readonly string _name = "camper_test_" + Guid.NewGuid().ToString("N");
    private string _server = null!; private DatabaseService _db = null!;
    public async Task InitializeAsync()
    {
        _server = Environment.GetEnvironmentVariable("CAMPER_TEST_DB") ?? throw new InvalidOperationException("CAMPER_TEST_DB muss auf eine isolierte lokale MariaDB-Testinstanz zeigen; siehe tests/README.md. Kein Produktions-Fallback.");
        var builder = new MySqlConnectionStringBuilder(_server);
        if (builder.Server is not ("127.0.0.1" or "localhost" or "mariadb") || builder.Database.Length != 0)
            throw new InvalidOperationException("Nur isolierte lokale Testinstanzen ohne vorgegebenen Datenbanknamen sind erlaubt.");
        await using (var c = new MySqlConnection(_server))
        {
            await c.OpenAsync();
            await new MySqlCommand($"CREATE DATABASE `{_name}`", c).ExecuteNonQueryAsync();
        }
        builder.Database = _name;
        _db = new(builder.ConnectionString, new FixedClock());
        using var stream = GetType().Assembly.GetManifestResourceStream("CamperManagement.IntegrationTests.Fixtures.schema.sql")!;
        await Sql(await new StreamReader(stream).ReadToEndAsync());
        await SchemaMigration.ApplyAsync(_db);
        await Sql("""
            INSERT INTO plaetze(id,platznr) VALUES(1,'1'),(2,'2'),(3,'10'),(4,'A1');
            INSERT INTO personen(id,anrede,vorname,nachname,strasse,plz,ort,email,created) VALUES(1,'','Test','Müller','Testweg 1','01234','Testort',NULL,'2024-01-01'),(2,'','Kontakt','Zweitperson','Weg 2','99999','Testort','extra@example.invalid','2024-01-01');
            INSERT INTO camper(id,platz_id,active,created,Vertragskosten) VALUES(1,1,1,'2024-01-01',100);
            INSERT INTO camper_personen(camper_id,personen_id,rechnungsadresse) VALUES(1,1,1),(1,2,0);
            """);
    }
    public async Task DisposeAsync()
    {
        if (_server == null)
            return;
        await using var c = new MySqlConnection(_server);
        await c.OpenAsync();
        await new MySqlCommand($"DROP DATABASE IF EXISTS `{_name}`", c).ExecuteNonQueryAsync();
    }
    private async Task Sql(string sql)
    {
        await using var c = await _db.OpenConnectionAsync();
        await new MySqlCommand(sql, c).ExecuteNonQueryAsync();
    }
    private async Task<long> Number(string sql)
    {
        await using var c = await _db.OpenConnectionAsync();
        return Convert.ToInt64(await new MySqlCommand(sql, c).ExecuteScalarAsync());
    }
    private static Rechnung Invoice(string type = "Wasser", int year = 2026) => new() { PlatzId = 1, Alt = 10, Neu = 12.5m, Verbrauch = 2.5m, Faktor = 8, Betrag = 20, Jahr = year, Type = type };
    [Fact]
    public async Task CancelledReadsReleaseConnectionsAndCanBeRetried()
    {
        var token = new CancellationToken(true);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _db.GetActiveCampersAsync(token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _db.GetStandardfaktorenAsync(token));
        Assert.Single(await _db.GetActiveCampersAsync());
        Assert.NotNull(await _db.GetStandardfaktorenAsync());
    }
    [Fact]
    public async Task D01_D02_D03_ActiveBillingPersonOnly()
    {
        var c = Assert.Single(await _db.GetActiveCampersAsync());
        Assert.Equal("Müller", c.Nachname);
        Assert.Equal("01234", c.PLZ);
        Assert.Equal("", c.Email);
        Assert.Equal(1, c.Id);
        await _db.DeactivateOldCamperAsync("1");
        Assert.Empty(await _db.GetActiveCampersAsync());
    }
    [Fact]
    public async Task D04_D05_Placements()
    {
        Assert.Equal(new[] { "1", "10", "2", "A1" }, await _db.GetPlatznummernAsync());
        Assert.Equal(4, await _db.GetPlatzIdByPlatznummerAsync("A1"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => _db.GetPlatzIdByPlatznummerAsync("missing"));
        await Assert.ThrowsAsync<ArgumentException>(() => _db.GetPlatzIdByPlatznummerAsync(null));
    }
    [Fact]
    public async Task D06_D08_YearsAndInvoiceOrder()
    {
        await _db.AddRechnungAsync(Invoice(year: 2025));
        await _db.AddRechnungAsync(Invoice());
        await _db.AddRechnungAsync(Invoice());
        Assert.Equal(new[] { 2026, 2025 }, await _db.GetAvailableJahreAsync());
        var rows = await _db.GetRechnungenAsync();
        Assert.Equal(3, rows.Count);
        Assert.Equal(new[] { 3, 2, 1 }, rows.Select(r => r.Id));
    }
    [Fact]
    public async Task D07_D16_OldRecipientSurvivesReplacement()
    {
        var r = Invoice();
        await _db.AddRechnungAsync(r);
        var next = Data.Camper();
        next.Vorname = "New";
        await _db.AddNewCamperAsync(next);
        Assert.Equal("Test", Assert.Single(await _db.GetRechnungenAsync()).Vorname);
        Assert.Equal("New", Assert.Single(await _db.GetActiveCampersAsync()).Vorname);
        Assert.Equal(2, await Number("SELECT COUNT(*) FROM camper WHERE platz_id=1"));
        Assert.Equal(2, await Number("SELECT COUNT(*) FROM camper_personen WHERE camper_id=1"));
    }
    [Fact]
    public async Task D09_D10_SumsDoNotMultiplyWithContacts()
    {
        await _db.AddRechnungAsync(Invoice());
        var s = Invoice("Strom");
        s.Betrag = 1.01m;
        await _db.AddRechnungAsync(s);
        var rows = await _db.GetRechnungenForJahrAsync(2026);
        var sum = Assert.Single(rows);
        Assert.Equal(21.01m, sum.Gesamtbetrag);
        Assert.Equal(100, sum.Vertragskosten);
        Assert.Empty(await _db.GetRechnungenForJahrAsync(2025));
    }
    [Fact]
    public async Task D11_D12_D13_LatestCreatedPerTypeAndTieBreak()
    {
        var a = Invoice();
        a.Neu = 50;
        await _db.AddRechnungAsync(a);
        var b = Invoice(year: 2025);
        b.Neu = 75;
        await _db.AddRechnungAsync(b);
        var s = Invoice("Strom");
        s.Neu = 8;
        await _db.AddRechnungAsync(s);
        await Sql($"UPDATE rechnungen SET created='2025-01-01',updated='2029-01-01' WHERE id={a.Id}");
        await Sql($"UPDATE rechnungen SET created='2025-02-01' WHERE id={b.Id}");
        Assert.Equal(75, await _db.GetNeuFromLatestRechnungAsync("1", "Wasser"));
        var row = Assert.Single(await _db.GetAbleseTabelleAsync());
        Assert.Equal(75, row.WasserAlt);
        Assert.Equal(8, row.StromAlt);
        Assert.Equal(0, await _db.GetNeuFromLatestRechnungAsync("2", "Wasser"));
    }
    [Fact]
    public async Task D14_EmptyRelationshipsDoNotCrash()
    {
        await Sql("DELETE FROM camper_personen");
        Assert.Empty(await _db.GetActiveCampersAsync());
        Assert.Empty(await _db.GetAbleseTabelleAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => _db.AddRechnungAsync(Invoice()));
        Assert.Empty(await _db.GetRechnungenAsync());
    }
    [Fact]
    public async Task D15_NewPlotGetsNewLink()
    {
        var camper = Data.Camper();
        camper.Platznr = "2";
        await _db.AddNewCamperAsync(camper);
        Assert.Equal(2, (await _db.GetActiveCampersAsync()).Count);
        Assert.Equal(1, await Number("SELECT COUNT(*) FROM camper_personen cp JOIN camper c ON c.id=cp.camper_id WHERE c.platz_id=2"));
    }
    [Theory]
    [InlineData("personen")]
    [InlineData("camper")]
    [InlineData("camper_personen")]
    public async Task D17_RollbackEachWrite(string table)
    {
        await Sql($"CREATE TRIGGER fail_write BEFORE INSERT ON {table} FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='injected failure'");
        await Assert.ThrowsAsync<MySqlException>(() => _db.AddNewCamperAsync(Data.Camper()));
        Assert.Equal(1, await Number("SELECT active FROM camper WHERE id=1"));
        Assert.Equal(1, await Number("SELECT COUNT(*) FROM camper"));
        Assert.Equal(2, await Number("SELECT COUNT(*) FROM personen"));
    }
    [Fact]
    public async Task D18_EditOnlyCurrentCamper()
    {
        var old = Assert.Single(await _db.GetActiveCampersAsync());
        await _db.AddRechnungAsync(Invoice());
        await _db.AddNewCamperAsync(Data.Camper());
        var current = Assert.Single(await _db.GetActiveCampersAsync());
        current.Vorname = "Changed";
        await _db.UpdateCamperAsync(current, current.Vertragskosten);
        Assert.Equal("Test", Assert.Single(await _db.GetRechnungenAsync()).Vorname);
        await Assert.ThrowsAsync<InvalidOperationException>(() => _db.UpdateCamperAsync(old, old.Vertragskosten));
        Assert.Equal("Changed", Assert.Single(await _db.GetActiveCampersAsync()).Vorname);
    }
    [Fact]
    public async Task D19_DeactivateIsIdempotent()
    {
        await _db.DeactivateOldCamperAsync("1");
        await _db.DeactivateOldCamperAsync("1");
        Assert.Empty(await _db.GetActiveCampersAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => _db.DeactivateOldCamperAsync("missing"));
    }
    [Fact]
    public async Task D20_PreciseDecimalAndInvalidPlace()
    {
        var r = Invoice();
        r.Neu = 123.456789m;
        r.Faktor = 1.005m;
        r.Betrag = 1.005m;
        await _db.AddRechnungAsync(r);
        var saved = Assert.Single(await _db.GetRechnungenAsync());
        Assert.Equal(123.456789m, saved.Neu);
        Assert.Equal(1.005m, saved.Faktor);
        Assert.Equal(1.01m, saved.Betrag);
        r.PlatzId = 999;
        await Assert.ThrowsAsync<InvalidOperationException>(() => _db.AddRechnungAsync(r));
        Assert.Single(await _db.GetRechnungenAsync());
    }
    [Fact]
    public async Task D21_UpdatePersistsArtAndValues()
    {
        var r = Invoice();
        await _db.AddRechnungAsync(r);
        r.Type = "Strom";
        r.Neu = 15;
        r.Alt = 11;
        r.Verbrauch = 4;
        r.Faktor = 0.6m;
        r.Betrag = 2.4m;
        await _db.UpdateRechnungAsync(r);
        var saved = Assert.Single(await _db.GetRechnungenAsync());
        Assert.Equal("Strom", saved.Art);
        Assert.Equal(0.6m, saved.Faktor);
        Assert.Equal(11, saved.Alt);
        Assert.Equal(2.4m, saved.Betrag);
    }
    [Fact]
    public async Task D22_D23_PrintedBatchRollsBackAndRepeats()
    {
        var r = Invoice();
        await _db.AddRechnungAsync(r);
        await Assert.ThrowsAsync<InvalidOperationException>(() => _db.MarkRechnungenAsPrintedAsync(new[] { r.Id, 999 }));
        Assert.Equal("Nein", Assert.Single(await _db.GetRechnungenAsync()).Gedruckt);
        await _db.MarkRechnungAsPrintedAsync(r.Id);
        await _db.MarkRechnungAsPrintedAsync(r.Id);
        Assert.Equal("Ja", Assert.Single(await _db.GetRechnungenAsync()).Gedruckt);
        r.Id = 999;
        await Assert.ThrowsAsync<InvalidOperationException>(() => _db.UpdateRechnungAsync(r));
    }
    [Fact]
    public async Task D24_ConcurrentReplacementSerializes()
    {
        await Task.WhenAll(_db.AddNewCamperAsync(Data.Camper()), _db.AddNewCamperAsync(Data.Camper()));
        Assert.Single(await _db.GetActiveCampersAsync());
        Assert.Equal(1, await Number("SELECT COUNT(*) FROM camper WHERE active=1"));
    }
    [Fact]
    public async Task D25_TextIsAlwaysParameterized()
    {
        var c = Assert.Single(await _db.GetActiveCampersAsync());
        c.Nachname = "O'Neil; DROP TABLE camper; --";
        await _db.UpdateCamperAsync(c, c.Vertragskosten);
        Assert.Equal(c.Nachname, Assert.Single(await _db.GetActiveCampersAsync()).Nachname);
    }
    [Fact]
    public async Task E02_E03_E04_CentralCurrentFactorsImmediatelyVisibleToAnotherInstance()
    {
        var a = await _db.GetStandardfaktorenAsync();
        await _db.SaveStandardfaktorenAsync(a with { Strom = 0.77m, Wasser = 9.25m });
        var builder = new MySqlConnectionStringBuilder(_server) { Database = _name };
        var other = new DatabaseService(builder.ConnectionString, new FixedClock());
        var b = await other.GetStandardfaktorenAsync();
        Assert.Equal(0.77m, b.Strom);
        Assert.Equal(9.25m, b.Wasser);
        await other.SaveStandardfaktorenAsync(b with { Strom = 1 });
        Assert.Equal(1, (await _db.GetStandardfaktorenAsync()).Strom);
        Assert.Equal(1, await Number("SELECT COUNT(*) FROM standardfaktoren"));
    }
    [Fact]
    public async Task E05_SettingsDoNotMutateOldInvoices()
    {
        var r = Invoice();
        await _db.AddRechnungAsync(r);
        var factors = await _db.GetStandardfaktorenAsync();
        await _db.SaveStandardfaktorenAsync(factors with
        {
            Wasser = 999
        });
        Assert.Equal(8, Assert.Single(await _db.GetRechnungenAsync()).Faktor);
    }
    [Fact]
    public async Task E07_E10_ConcurrentReadsAndOptimisticSave()
    {
        var values = await Task.WhenAll(_db.GetStandardfaktorenAsync(), _db.GetStandardfaktorenAsync());
        Assert.Equal(values[0], values[1]);
        await _db.SaveStandardfaktorenAsync(values[0] with
        {
            Wasser = 9
        });
        await Assert.ThrowsAsync<InvalidOperationException>(() => _db.SaveStandardfaktorenAsync(values[1] with { Strom = 1 }));
        Assert.Equal(0.5m, (await _db.GetStandardfaktorenAsync()).Strom);
    }
    [Fact]
    public async Task E09_FailedSettingsWriteKeepsBothFactors()
    {
        var v = await _db.GetStandardfaktorenAsync();
        await Sql("CREATE TRIGGER fail_settings BEFORE UPDATE ON standardfaktoren FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='injected'");
        await Assert.ThrowsAsync<MySqlException>(() => _db.SaveStandardfaktorenAsync(v with { Strom = 99, Wasser = 99 }));
        await Sql("DROP TRIGGER fail_settings");
        Assert.Equal(v, await _db.GetStandardfaktorenAsync());
    }
    [Fact]
    public async Task Migration_CurrentFactorsPreservesExistingTariffsAndDoesNotReseed()
    {
        await _db.AddRechnungAsync(Invoice());
        await Sql("""
            DELETE FROM camper_schema_version WHERE version=2;
            DROP TABLE standardfaktoren;
            INSERT INTO jahresfaktoren(jahr,strom,wasser,version) VALUES
              (YEAR(CURRENT_DATE)-1,0.4,7,1),(YEAR(CURRENT_DATE),0.63,9.7,3),(YEAR(CURRENT_DATE)+1,3,99,2);
            """);
        await SchemaMigration.ApplyAsync(_db);
        var factors = await _db.GetStandardfaktorenAsync();
        Assert.Equal(0.63m, factors.Strom);
        Assert.Equal(9.7m, factors.Wasser);
        Assert.Equal(8, Assert.Single(await _db.GetRechnungenAsync()).Faktor);
        await _db.SaveStandardfaktorenAsync(factors with { Strom = 0.81m });
        // Even retrying a partially recorded migration must never overwrite current prices.
        await Sql("DELETE FROM camper_schema_version WHERE version=2;");
        await SchemaMigration.ApplyAsync(_db);
        Assert.Equal(0.81m, (await _db.GetStandardfaktorenAsync()).Strom);
        Assert.Equal(3, await Number("SELECT COUNT(*) FROM jahresfaktoren"));
        Assert.Equal(5, await Number("SELECT MAX(version) FROM camper_schema_version"));
    }
    [Fact]
    public async Task Migration_IsIdempotentAndNeverGuesses()
    {
        await Sql("INSERT INTO rechnungen(platz_id,alt,neu,verbrauch,faktor,betrag,jahr,type,created) VALUES(1,0,1,1,8,8,2025,'Wasser','2025-01-01'); INSERT INTO camper(platz_id,active,created) VALUES(1,0,'2024-06-01'); INSERT INTO camper_personen(camper_id,personen_id,rechnungsadresse) VALUES(2,2,1); DELETE FROM camper_schema_version;");
        Assert.Equal(1, await SchemaMigration.ApplyAsync(_db));
        Assert.False(Assert.Single(await _db.GetRechnungenAsync()).RecipientResolved);
        Assert.Equal(1, await SchemaMigration.ApplyAsync(_db));
    }

    [Fact]
    public async Task D10_OnlyFullGroupBy()
    {
        await Sql("SET GLOBAL sql_mode='STRICT_TRANS_TABLES,ERROR_FOR_DIVISION_BY_ZERO,NO_ENGINE_SUBSTITUTION,ONLY_FULL_GROUP_BY'");
        await _db.AddRechnungAsync(Invoice());
        Assert.Single(await _db.GetRechnungenForJahrAsync(2026));
    }
    [Fact]
    public async Task D26_ConnectionFailureIsExplicit()
    {
        var db = new DatabaseService("Server=127.0.0.1;Port=1;User ID=none;Connection Timeout=1");
        await Assert.ThrowsAsync<MySqlException>(() => db.GetPlatznummernAsync());
    }
    [Fact]
    public async Task Migration_MapsUniqueRecipientAndPreservesFactor()
    {
        await Sql("INSERT INTO rechnungen(platz_id,alt,neu,verbrauch,faktor,betrag,jahr,type,created) VALUES(1,10,11,1,0.37,0.37,2025,'Strom','2025-01-01'); DELETE FROM camper_schema_version;");
        Assert.Equal(0, await SchemaMigration.ApplyAsync(_db));
        var row = Assert.Single(await _db.GetRechnungenAsync());
        Assert.True(row.RecipientResolved);
        Assert.Equal(0.37m, row.Faktor);
        Assert.Equal("01234", row.PLZ);
        Assert.Equal(0, await SchemaMigration.ApplyAsync(_db));
    }

    [Fact]
    public async Task Migration_EquivalentDuplicatesHaveOneSnapshot()
    {
        await Sql("INSERT INTO rechnungen(platz_id,alt,neu,verbrauch,faktor,betrag,jahr,type,created) VALUES(1,0,1,1,8,8,2025,'Wasser','2025-01-01'); INSERT INTO camper(platz_id,active,created,Vertragskosten) VALUES(1,0,'2024-06-01',100); INSERT INTO camper_personen(camper_id,personen_id,rechnungsadresse) VALUES(2,1,1); DELETE FROM camper_schema_version;");
        Assert.Equal(0, await SchemaMigration.ApplyAsync(_db));
        var invoice = Assert.Single(await _db.GetRechnungenAsync());
        Assert.True(invoice.RecipientResolved);
        Assert.Equal(2, invoice.CamperId);
        Assert.Equal("Test", invoice.Vorname);
    }
}

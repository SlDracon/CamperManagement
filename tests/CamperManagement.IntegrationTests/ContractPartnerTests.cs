using CamperManagement.Models;
using CamperManagement.Services;
using CamperManagement.Tests;
using MySqlConnector;
namespace CamperManagement.IntegrationTests;

public partial class DatabaseTests
{
    private static CamperDisplayModel Pair()
    {
        var value = Data.Camper();
        value.HatZweitenVertragsnehmer = true; value.GemeinsameAdresse = false;
        value.Anrede = "Frau"; value.ZweiteAnrede = "Herr";
        value.ZweiterVorname = "Alex"; value.ZweiterNachname = "Schneider";
        value.ZweiteStraße = "Nebenweg 2"; value.ZweitePLZ = "01235"; value.ZweiterOrt = "Zweitort";
        value.ZweiteEmail = "alex@example.invalid"; value.RechnungsadresseZweiterVertragsnehmer = true;
        return value;
    }
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Partners_CreateRoundTripAndInvoiceUsesBothNames(bool shared, bool billSecond)
    {
        var value = Pair(); value.GemeinsameAdresse = shared; value.RechnungsadresseZweiterVertragsnehmer = billSecond;
        await _db.AddNewCamperAsync(value);
        var actual = Assert.Single(await _db.GetActiveCampersAsync());
        Assert.Equal("Test", actual.Vorname); Assert.Equal("Alex", actual.ZweiterVorname);
        Assert.Equal("Schneider", actual.ZweiterNachname); Assert.Equal("alex@example.invalid", actual.ZweiteEmail);
        Assert.Equal(shared ? "Testweg 1" : "Nebenweg 2", actual.ZweiteStraße);
        Assert.Equal(!shared && billSecond, actual.NutztZweiteRechnungsadresse);
        Assert.Equal(1, await Number($"SELECT COUNT(*) FROM camper_personen WHERE camper_id={actual.Id} AND rechnungsadresse=1"));
        await _db.AddRechnungAsync(Invoice());
        var invoice = Assert.Single(await _db.GetRechnungenAsync());
        Assert.Equal("Test", invoice.Vorname); Assert.Equal("Alex", invoice.ZweiterVorname);
        Assert.Equal("Schneider", invoice.ZweiterNachname);
        Assert.Equal(!shared && billSecond ? "Nebenweg 2" : "Testweg 1", invoice.Straße);
        Assert.Equal(!shared && billSecond ? "01235" : "01234", invoice.PLZ);
        Assert.Equal(20, Assert.Single(await _db.GetRechnungenForJahrAsync(2026)).Gesamtbetrag);
        Assert.Single(await _db.GetAbleseTabelleAsync());
    }
    [Fact]
    public async Task Partners_EditingAndRemovalPreserveOldInvoiceAndContacts()
    {
        var value = Pair();
        await _db.UpdateCamperAsync(value, 100);
        await _db.AddRechnungAsync(Invoice());
        value = Assert.Single(await _db.GetActiveCampersAsync());
        value.Vorname = "Changed"; value.ZweiterNachname = "Updated"; value.ZweiteStraße = "New address 3";
        value.RechnungsadresseZweiterVertragsnehmer = false;
        await _db.UpdateCamperAsync(value, 0);
        var current = Assert.Single(await _db.GetActiveCampersAsync());
        Assert.Equal("Changed", current.Vorname); Assert.Equal("Updated", current.ZweiterNachname);
        Assert.False(current.NutztZweiteRechnungsadresse);
        await _db.AddRechnungAsync(Invoice());
        var invoices = await _db.GetRechnungenAsync();
        Assert.Equal("Test", invoices[1].Vorname); Assert.Equal("Schneider", invoices[1].ZweiterNachname);
        Assert.Equal("Nebenweg 2", invoices[1].Straße);
        Assert.Equal("Changed", invoices[0].Vorname); Assert.Equal("Testweg 1", invoices[0].Straße);
        current.HatZweitenVertragsnehmer = false;
        await _db.UpdateCamperAsync(current, current.Vertragskosten);
        Assert.False(Assert.Single(await _db.GetActiveCampersAsync()).HatZweitenVertragsnehmer);
        Assert.Equal(1, await Number("SELECT COUNT(*) FROM camper_personen WHERE camper_id=1 AND personen_id=2"));
        Assert.Equal(1, await Number("SELECT COUNT(*) FROM camper_personen WHERE camper_id=1 AND rechnungsadresse=1"));
        Assert.All(await _db.GetRechnungenAsync(), r => Assert.True(r.HatZweitenVertragsnehmer));
        await _db.AddRechnungAsync(Invoice());
        Assert.False((await _db.GetRechnungenAsync())[0].HatZweitenVertragsnehmer);
    }
    [Fact]
    public async Task Partners_SharedAddressTracksFirstHolderAfterEditing()
    {
        var value = Pair(); await _db.UpdateCamperAsync(value, 100);
        value.GemeinsameAdresse = true; value.Straße = "Gemeinsamer Weg 9";
        await _db.UpdateCamperAsync(value, 0);
        var actual = Assert.Single(await _db.GetActiveCampersAsync());
        Assert.Equal(actual.Straße, actual.ZweiteStraße); Assert.False(actual.NutztZweiteRechnungsadresse);
        await _db.AddRechnungAsync(Invoice());
        Assert.Equal("Gemeinsamer Weg 9", Assert.Single(await _db.GetRechnungenAsync()).Straße);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Partners_FailedSecondPersonRollsBackEntireWrite(bool adding)
    {
        await Sql("CREATE TRIGGER fail_second BEFORE INSERT ON personen FOR EACH ROW BEGIN IF NEW.vorname='Alex' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='test second holder failure'; END IF; END");
        var count = await Number("SELECT COUNT(*) FROM personen");
        var value = Pair(); value.Vorname = "Do not save";
        await Assert.ThrowsAsync<MySqlException>(() => adding ? _db.AddNewCamperAsync(value) : _db.UpdateCamperAsync(value, 100));
        var old = Assert.Single(await _db.GetActiveCampersAsync());
        Assert.Equal("Test", old.Vorname); Assert.False(old.HatZweitenVertragsnehmer);
        Assert.Equal(count, await Number("SELECT COUNT(*) FROM personen"));
        Assert.Equal(1, await Number("SELECT COUNT(*) FROM camper"));
        Assert.Equal(1, await Number("SELECT COUNT(*) FROM camper_personen WHERE camper_id=1 AND rechnungsadresse=1"));
    }
    [Fact]
    public async Task Partners_MigrationDoesNotInferExtraContactOrRewriteOldInvoices()
    {
        await _db.AddRechnungAsync(Invoice());
        await Sql("DELETE FROM camper_schema_version WHERE version=3");
        await SchemaMigration.ApplyAsync(_db);
        Assert.False(Assert.Single(await _db.GetActiveCampersAsync()).HatZweitenVertragsnehmer);
        Assert.Equal(1, await Number("SELECT COUNT(*) FROM camper_personen WHERE vertragsnehmer_nr=1"));
        Assert.Equal(0, await Number("SELECT COUNT(*) FROM camper_personen WHERE vertragsnehmer_nr=2"));
        await _db.UpdateCamperAsync(Pair(), 100);
        // Simulate retry after DDL succeeded but the migration version was not recorded.
        await Sql("DELETE FROM camper_schema_version WHERE version=3");
        await SchemaMigration.ApplyAsync(_db); await SchemaMigration.ApplyAsync(_db);
        var current = Assert.Single(await _db.GetActiveCampersAsync());
        Assert.Equal("Test", current.Vorname); Assert.Equal("Alex", current.ZweiterVorname);
        Assert.True(current.NutztZweiteRechnungsadresse);
        var historical = Assert.Single(await _db.GetRechnungenAsync());
        Assert.False(historical.HatZweitenVertragsnehmer); Assert.Equal("Testweg 1", historical.Straße);
    }
}

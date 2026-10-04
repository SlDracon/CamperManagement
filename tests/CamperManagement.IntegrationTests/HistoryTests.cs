using CamperManagement.Models;
using CamperManagement.Services;
using CamperManagement.Tests;
using MySqlConnector;
namespace CamperManagement.IntegrationTests;

public partial class DatabaseTests
{
    [Fact]
    public async Task History_ReplacementRecordsOldAndNewOccupationInOrder()
    {
        var next = Data.Camper(); next.Vorname = "Nachfolger";
        await _db.AddNewCamperAsync(next);
        var history = await _db.GetCamperHistoryAsync("1");
        Assert.Equal(2, history.Count);
        Assert.Equal("created", history[0].Kind); Assert.Equal("Nachfolger", history[0].After.Camper.Vorname);
        Assert.True(history[0].After.Active); Assert.Null(history[0].Before);
        Assert.Equal("deactivated", history[1].Kind); Assert.Equal("Test", history[1].After.Camper.Vorname);
        Assert.True(history[1].Before!.Active); Assert.False(history[1].After.Active); Assert.NotNull(history[1].After.Deactivated);
        Assert.NotEqual(history[0].After.Camper.Id, history[1].After.Camper.Id);
        Assert.True(history[0].Id > history[1].Id);
        Assert.Equal(DateTimeKind.Utc, history[0].RecordedAtUtc.Kind);
        Assert.Empty(await _db.GetCamperHistoryAsync("2"));
    }
    [Fact]
    public async Task History_ChangeContainsFullBeforeAfterIncludingPartnerAndAddress()
    {
        var pair = Pair(); await _db.UpdateCamperAsync(pair, 100);
        var first = Assert.Single(await _db.GetCamperHistoryAsync());
        Assert.False(first.Before!.Camper.HatZweitenVertragsnehmer); Assert.True(first.After.Camper.HatZweitenVertragsnehmer);
        Assert.Equal("Alex", first.After.Camper.ZweiterVorname); Assert.True(first.After.Camper.NutztZweiteRechnungsadresse);
        pair = Assert.Single(await _db.GetActiveCampersAsync());
        pair.Email = "updated@example.invalid"; pair.ZweiterNachname = "Neumann"; pair.ZweiteStraße = "Neuer Weg 5"; pair.Vertragskosten = 123.45m;
        await _db.UpdateCamperAsync(pair, 0);
        var latest = (await _db.GetCamperHistoryAsync())[0];
        Assert.Equal("updated", latest.Kind);
        Assert.Equal("Schneider", latest.Before!.Camper.ZweiterNachname); Assert.Equal("Neumann", latest.After.Camper.ZweiterNachname);
        Assert.Equal("Nebenweg 2", latest.Before.Camper.ZweiteStraße); Assert.Equal("Neuer Weg 5", latest.After.Camper.ZweiteStraße);
        Assert.Equal(123.45m, latest.After.Camper.Vertragskosten);
        Assert.Contains(latest.Changes, x => x.Field == "E-Mail" && x.After == "updated@example.invalid");
        // Later edits must not rewrite a historical snapshot.
        pair.HatZweitenVertragsnehmer = false; await _db.UpdateCamperAsync(pair, 123.45m);
        var all = await _db.GetCamperHistoryAsync();
        Assert.Equal("Neumann", all[0].Before!.Camper.ZweiterNachname); Assert.False(all[0].After.Camper.HatZweitenVertragsnehmer);
        Assert.Equal("Schneider", all[2].After.Camper.ZweiterNachname);
    }
    [Fact]
    public async Task History_UnchangedSaveAndRepeatedDeactivationDoNotAddEvents()
    {
        var original = Assert.Single(await _db.GetActiveCampersAsync());
        await _db.UpdateCamperAsync(original, original.Vertragskosten); await _db.UpdateCamperAsync(original, original.Vertragskosten);
        Assert.Empty(await _db.GetCamperHistoryAsync());
        await _db.DeactivateOldCamperAsync("1"); await _db.DeactivateOldCamperAsync("1");
        Assert.Equal("deactivated", Assert.Single(await _db.GetCamperHistoryAsync()).Kind);
    }
    [Theory]
    [InlineData("update")]
    [InlineData("replace")]
    [InlineData("deactivate")]
    public async Task History_WriteFailureRollsBackTheBusinessChange(string operation)
    {
        await Sql("CREATE TRIGGER fail_history BEFORE INSERT ON camper_historie FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='test history failure'");
        var changed = Data.Camper(); changed.Vorname = "Must roll back";
        await Assert.ThrowsAsync<MySqlException>(() => operation switch
        {
            "update" => _db.UpdateCamperAsync(changed, 100), "replace" => _db.AddNewCamperAsync(changed), _ => _db.DeactivateOldCamperAsync("1")
        });
        var original = Assert.Single(await _db.GetActiveCampersAsync());
        Assert.Equal("Test", original.Vorname); Assert.Empty(await _db.GetCamperHistoryAsync());
        Assert.Equal(1, await Number("SELECT COUNT(*) FROM camper"));
    }
    [Fact]
    public async Task History_FailedReplacementCannotLeaveDeactivationEvent()
    {
        await Sql("CREATE TRIGGER fail_new_person BEFORE INSERT ON personen FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='test failure after deactivation'");
        await Assert.ThrowsAsync<MySqlException>(() => _db.AddNewCamperAsync(Data.Camper()));
        Assert.Empty(await _db.GetCamperHistoryAsync()); Assert.Single(await _db.GetActiveCampersAsync());
    }
    [Fact]
    public async Task History_MigrationIncludesInactiveAndUnassignedCampersAndIsIdempotent()
    {
        await Sql("""
            INSERT INTO camper(id,platz_id,active,created,deactivated) VALUES(2,1,0,'2022-01-01','2023-02-01'),(3,2,0,'2021-01-01','2022-01-01');
            INSERT INTO camper_personen(camper_id,personen_id,rechnungsadresse) VALUES(2,2,1);
            DELETE FROM camper_schema_version WHERE version=4;
            """);
        await SchemaMigration.ApplyAsync(_db);
        var first = await _db.GetCamperHistoryAsync(); Assert.Equal(3, first.Count);
        Assert.All(first, e => { Assert.Equal("baseline", e.Kind); Assert.Null(e.Before); });
        var inactive = first.Single(e => e.After.Camper.Id == 2);
        Assert.False(inactive.After.Active); Assert.Equal(new DateTime(2022,1,1), inactive.After.Created);
        Assert.Equal(new DateTime(2023,2,1), inactive.After.Deactivated);
        Assert.Equal("Kontakt", inactive.After.Camper.Vorname);
        Assert.Contains("Name nicht hinterlegt", first.Single(e => e.After.Camper.Id == 3).Identity);
        await Sql("DELETE FROM camper_schema_version WHERE version=4;");
        await SchemaMigration.ApplyAsync(_db); await SchemaMigration.ApplyAsync(_db);
        Assert.Equal(first.Select(e => e.Id), (await _db.GetCamperHistoryAsync()).Select(e => e.Id));
        Assert.Equal(2, (await _db.GetCamperHistoryAsync("1")).Count);
    }
    [Fact]
    public async Task History_ReadCancellationReleasesConnection()
    {
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _db.GetCamperHistoryAsync(null, new CancellationToken(true)));
        Assert.Empty(await _db.GetCamperHistoryAsync());
    }
}

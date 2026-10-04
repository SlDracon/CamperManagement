using CamperManagement.Services;
using CamperManagement.Tests;
using MySqlConnector;

namespace CamperManagement.IntegrationTests;

public partial class DatabaseTests
{
    [Fact]
    public async Task ContractCosts_BookingsPreserveReasonAndSnapshotsAcrossLaterChanges()
    {
        await _db.AddRechnungAsync(Invoice());
        const string reason = "  Neue Gebühren für Müll 🏕\nO'Neil; DROP TABLE camper; --  ";
        await _db.IncreaseContractCostAsync(1, "1", 100, 25.555m, reason);
        await _db.AddRechnungAsync(Invoice());
        await _db.IncreaseContractCostAsync(1, "1", 125.56m, .005m, "Weitere Erhöhung");
        Assert.Equal(125.57m, Assert.Single(await _db.GetActiveCampersAsync()).Vertragskosten);
        var history = await _db.GetCamperHistoryAsync("1");
        Assert.Equal(2, history.Count); Assert.All(history, e => Assert.Equal("cost_increased", e.Kind));
        Assert.Equal(100, history[1].Before!.Camper.Vertragskosten); Assert.Equal(125.56m, history[1].After.Camper.Vertragskosten);
        Assert.Equal(reason.Trim(), history[1].Description); Assert.Equal("+25,56 €", history[1].IncreaseDisplay);
        Assert.Equal(125.56m, history[0].Before!.Camper.Vertragskosten); Assert.Equal(125.57m, history[0].After.Camper.Vertragskosten);
        Assert.Equal(1, await Number("SELECT COUNT(*) FROM rechnung_empfaenger WHERE vertragskosten=100"));
        Assert.Equal(1, await Number("SELECT COUNT(*) FROM rechnung_empfaenger WHERE vertragskosten=125.56"));
        await _db.AddNewCamperAsync(Data.Camper());
        Assert.Equal(0, Assert.Single(await _db.GetActiveCampersAsync()).Vertragskosten);
        Assert.Equal(reason.Trim(), (await _db.GetCamperHistoryAsync("1")).Single(e => e.Id == history[1].Id).Description);
    }
    [Theory]
    [InlineData("0", "Grund")]
    [InlineData("-1", "Grund")]
    [InlineData("0.004", "Grund")]
    [InlineData("10", " ")]
    [InlineData("9999999999999999.99", "Grund")]
    public async Task ContractCosts_InvalidBookingChangesNothing(string input, string reason)
    {
        BillingRules.TryDecimal(input, out var amount);
        await Assert.ThrowsAsync<ArgumentException>(() => _db.IncreaseContractCostAsync(1, "1", 100, amount, reason));
        Assert.Equal(100, Assert.Single(await _db.GetActiveCampersAsync()).Vertragskosten);
        Assert.Empty(await _db.GetCamperHistoryAsync());
    }
    [Fact]
    public async Task ContractCosts_HistoryFailureRollsBackPrice()
    {
        await Sql("CREATE TRIGGER fail_cost_history BEFORE INSERT ON camper_historie FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='injected history failure'");
        await Assert.ThrowsAsync<MySqlException>(() => _db.IncreaseContractCostAsync(1, "1", 100, 10, "Mehrkosten"));
        Assert.Equal(100, Assert.Single(await _db.GetActiveCampersAsync()).Vertragskosten); Assert.Empty(await _db.GetCamperHistoryAsync());
    }
    [Fact]
    public async Task ContractCosts_ConcurrentOrRetriedBookingCannotSilentlyChargeTwice()
    {
        var operations = new[] {
            Record.ExceptionAsync(() => _db.IncreaseContractCostAsync(1, "1", 100, 10, "Mehrkosten A")),
            Record.ExceptionAsync(() => _db.IncreaseContractCostAsync(1, "1", 100, 10, "Mehrkosten B"))
        };
        var results = await Task.WhenAll(operations);
        Assert.Single(results, e => e == null); Assert.Single(results, e => e is InvalidOperationException);
        await Assert.ThrowsAsync<InvalidOperationException>(() => _db.IncreaseContractCostAsync(1, "1", 100, 10, "Erneuter Versuch"));
        Assert.Equal(110, Assert.Single(await _db.GetActiveCampersAsync()).Vertragskosten); Assert.Single(await _db.GetCamperHistoryAsync());
    }
    [Fact]
    public async Task ContractCosts_StaleEditCannotOverwriteBookedIncreaseOrNames()
    {
        var stale = Assert.Single(await _db.GetActiveCampersAsync()); stale.Vorname = "Veraltet";
        await _db.IncreaseContractCostAsync(1, "1", 100, 10, "Mehrkosten");
        await Assert.ThrowsAsync<InvalidOperationException>(() => _db.UpdateCamperAsync(stale, 100));
        var current = Assert.Single(await _db.GetActiveCampersAsync());
        Assert.Equal(110, current.Vertragskosten); Assert.Equal("Test", current.Vorname);
        current.Email = "neu@example.invalid"; await _db.UpdateCamperAsync(current, 110);
        Assert.Equal(110, Assert.Single(await _db.GetActiveCampersAsync()).Vertragskosten);
        Assert.Equal(2, (await _db.GetCamperHistoryAsync()).Count);
    }
    [Fact]
    public async Task ContractCosts_UnknownOrWrongOccupationIsNotCharged()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => _db.IncreaseContractCostAsync(999, "1", 100, 10, "Mehrkosten"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => _db.IncreaseContractCostAsync(1, "2", 100, 10, "Mehrkosten"));
        Assert.Empty(await _db.GetCamperHistoryAsync());
        await _db.AddNewCamperAsync(Data.Camper());
        await Assert.ThrowsAsync<InvalidOperationException>(() => _db.IncreaseContractCostAsync(1, "1", 100, 10, "Mehrkosten"));
        Assert.Equal(0, Assert.Single(await _db.GetActiveCampersAsync()).Vertragskosten);
        Assert.Equal(2, (await _db.GetCamperHistoryAsync()).Count);
    }
    [Fact]
    public async Task ContractCosts_MigrationFromFourAndRetryPreserveHistoryAndInvoices()
    {
        var camper = Assert.Single(await _db.GetActiveCampersAsync()); camper.Email = "migration@example.invalid";
        await _db.UpdateCamperAsync(camper, 100); await _db.AddRechnungAsync(Invoice());
        var baseline = Assert.Single(await _db.GetCamperHistoryAsync());
        await Sql("ALTER TABLE camper_historie DROP COLUMN beschreibung; DELETE FROM camper_schema_version WHERE version=5;");
        Assert.Equal(0, await SchemaMigration.ApplyAsync(_db));
        var migrated = Assert.Single(await _db.GetCamperHistoryAsync());
        Assert.Equal(baseline.Id, migrated.Id); Assert.Equal("", migrated.Description);
        Assert.Equal(baseline.After.Camper.Email, migrated.After.Camper.Email);
        await _db.IncreaseContractCostAsync(1, "1", 100, 10, "Begründung bleibt erhalten");
        await Sql("DELETE FROM camper_schema_version WHERE version=5;");
        await SchemaMigration.ApplyAsync(_db); await SchemaMigration.ApplyAsync(_db);
        Assert.Equal(5, await Number("SELECT MAX(version) FROM camper_schema_version"));
        Assert.Equal(2, (await _db.GetCamperHistoryAsync()).Count);
        Assert.Equal("Begründung bleibt erhalten", (await _db.GetCamperHistoryAsync())[0].Description);
        Assert.Equal(1, await Number("SELECT COUNT(*) FROM rechnung_empfaenger WHERE vertragskosten=100"));
    }
}

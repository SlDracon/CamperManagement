using CamperManagement.Models;
using CamperManagement.Services;
using CamperManagement.Tests;
using CamperManagement.ViewModels;

namespace CamperManagement.UnitTests;

public class ContractCostTests
{
    [Theory]
    [InlineData("0.005", "100.01")]
    [InlineData("25.555", "125.56")]
    [InlineData("0.014", "100.01")]
    public void EachIncreaseIsRoundedCommerciallyBeforeAdding(string input, string total)
    {
        BillingRules.TryDecimal(input, out var amount); BillingRules.TryDecimal(total, out var expected);
        Assert.Equal(expected, ContractCostRules.NewTotal(100, amount));
    }
    [Fact]
    public void DatabasePrecisionLimitIsValidatedWithoutOverflow()
    {
        Assert.Equal(ContractCostRules.MaxCost, ContractCostRules.NewTotal(ContractCostRules.MaxCost - .01m, .01m));
        Assert.Throws<ArgumentException>(() => ContractCostRules.NewTotal(ContractCostRules.MaxCost, .01m));
        Assert.Throws<ArgumentException>(() => ContractCostRules.NewTotal(100, decimal.MaxValue));
        Assert.Throws<ArgumentException>(() => ContractCostRules.NewTotal(100, decimal.MinValue));
        Assert.Throws<ArgumentException>(() => ContractCostRules.Description(new string('x', 1001)));
        Assert.Equal(1000, ContractCostRules.Description(new string('x', 1000)).Length);
    }
    [Theory]
    [InlineData("", "Grund")]
    [InlineData("abc", "Grund")]
    [InlineData("0", "Grund")]
    [InlineData("-10", "Grund")]
    [InlineData("0,004", "Grund")]
    [InlineData("10", "  ")]
    public async Task InvalidBookingKeepsFormAndDoesNotWrite(string amount, string reason)
    {
        var (db, main, vm) = Setup(); await vm.InitializeAsync();
        vm.IncreaseText = amount; vm.Description = reason;
        await vm.BookCommand.ExecuteAsync(null);
        Assert.Equal(0, db.CostIncreases); Assert.NotEmpty(vm.StatusMessage!);
        Assert.Equal(amount, vm.IncreaseText); Assert.Equal(reason, vm.Description);
    }
    [Fact]
    public async Task BookingReloadsCurrentPriceAndReturnsToOverviewWithNewTotal()
    {
        var (db, main, vm) = Setup(); var tabs = main.CurrentView;
        // Selection can be stale: opening the form reads current values first.
        db.Campers[0].Vertragskosten = 120;
        main.NavigateToCommand.Execute(vm); await main.NavigationTask;
        Assert.Equal("120,00 €", vm.CurrentCostDisplay);
        vm.IncreaseText = "25,555"; vm.Description = "  Gestiegene Wartungskosten  ";
        Assert.Equal("145,56 €", vm.NewTotalDisplay);
        await vm.BookCommand.ExecuteAsync(null); await main.NavigationTask;
        Assert.Same(tabs, main.CurrentView); Assert.Equal(1, db.CostIncreases);
        Assert.Equal(120, db.ExpectedContractCost); Assert.Equal(25.56m, db.SavedIncrease);
        Assert.Equal("Gestiegene Wartungskosten", db.SavedDescription);
        var list = (CamperViewModel)((TabViewModel)main.CurrentView).CamperView;
        Assert.Equal(145.56m, Assert.Single(list.CamperList).Vertragskosten);
        await vm.BookCommand.ExecuteAsync(null); Assert.Equal(1, db.CostIncreases);
    }
    [Fact]
    public async Task PendingBookingBlocksSecondSubmitAndNavigation()
    {
        var (db, main, vm) = Setup(); var pending = new TaskCompletionSource(); db.BookCost = () => pending.Task;
        main.NavigateToCommand.Execute(vm); await main.NavigationTask;
        vm.IncreaseText = "10"; vm.Description = "Mehrkosten";
        var booking = vm.BookCommand.ExecuteAsync(null);
        Assert.True(vm.IsBusy); Assert.False(vm.CanEdit); Assert.False(vm.BookCommand.CanExecute(null));
        await vm.BookCommand.ExecuteAsync(null); main.NavigateBackCommand.Execute(null);
        Assert.Same(vm, main.CurrentView);
        pending.SetResult(); await booking; Assert.Equal(1, db.CostIncreases);
    }
    [Fact]
    public async Task StaleBookingKeepsInputAndCanBeReviewedAfterReload()
    {
        var (db, main, vm) = Setup(); await vm.InitializeAsync();
        vm.IncreaseText = "10"; vm.Description = "Mehrkosten";
        db.Campers[0].Vertragskosten = 125;
        await vm.BookCommand.ExecuteAsync(null);
        Assert.Equal(0, db.CostIncreases); Assert.Contains("neu laden", vm.StatusMessage);
        Assert.Equal("10", vm.IncreaseText); Assert.Equal("Mehrkosten", vm.Description);
        await vm.ReloadCommand.ExecuteAsync(null); Assert.Equal("135,00 €", vm.NewTotalDisplay);
        await vm.BookCommand.ExecuteAsync(null); Assert.Equal(135, db.Campers[0].Vertragskosten);
    }
    [Fact]
    public async Task FailedBookingAndMissingCamperCannotAppearSuccessful()
    {
        var (db, main, vm) = Setup(); main.NavigateToCommand.Execute(vm); await main.NavigationTask;
        vm.IncreaseText = "10"; vm.Description = "Mehrkosten"; db.SaveError = new IOException("injected");
        await vm.BookCommand.ExecuteAsync(null);
        Assert.Same(vm, main.CurrentView); Assert.Equal(0, db.CostIncreases); Assert.NotEmpty(vm.StatusMessage!);
        db.Campers.Clear(); await vm.ReloadCommand.ExecuteAsync(null);
        Assert.True(vm.LoadFailed); Assert.False(vm.CanEdit); await vm.BookCommand.ExecuteAsync(null); Assert.Equal(0, db.CostIncreases);
    }
    [Fact]
    public async Task CamperEditPassesOriginalPriceInsteadOfEditedValue()
    {
        var db = new FakeDatabase(); var camper = Data.Camper(); camper.Vertragskosten = 100;
        var vm = new EditCamperViewModel(new(db), db, camper) { VertragskostenText = "110" };
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Equal(100, db.ExpectedContractCost); Assert.Equal(110, db.SavedCamper!.Vertragskosten);
    }
    [Fact]
    public async Task ReasonAndIncreaseAreSearchableInHistory()
    {
        var db = new FakeDatabase(); var before = Data.Camper(); before.Vertragskosten = 100;
        var after = before.Snapshot(); after.Vertragskosten = 125.50m;
        var entry = new CamperHistoryEntry { Kind = "cost_increased", Description = "Neue Abfallgebühren", Before = new(before, true, null, null), After = new(after, true, null, null) };
        db.History.Add(entry); var vm = new CamperHistoryViewModel(db); await vm.InitializeAsync();
        vm.SearchText = "Abfallgebühren"; Assert.Single(vm.Entries);
        Assert.Equal("+25,50 €", entry.IncreaseDisplay); Assert.Equal("Vertragskosten erhöht", entry.EventName);
        Assert.Contains("Neue Abfallgebühren", entry.Summary);
        var change = Assert.Single(entry.Changes); Assert.Equal("100,00 €", change.Before); Assert.Equal("125,50 €", change.After);
    }
    [Fact]
    public async Task IncreaseActionRequiresSelectionAndHasHistoryNavigation()
    {
        var (db, main, _) = Setup(); var list = (CamperViewModel)((TabViewModel)main.CurrentView).CamperView;
        Assert.False(list.IncreaseCostCommand.CanExecute(null)); list.SelectedCamper = db.Campers[0];
        list.IncreaseCostCommand.Execute(null); await main.NavigationTask;
        var form = Assert.IsType<ContractCostIncreaseViewModel>(main.CurrentView);
        form.HistoryCommand.Execute(null); await main.NavigationTask;
        Assert.Equal("1", Assert.IsType<CamperHistoryViewModel>(main.CurrentView).SelectedPlace);
    }
    private static (FakeDatabase, MainViewModel, ContractCostIncreaseViewModel) Setup()
    {
        var camper = Data.Camper(); camper.Vertragskosten = 100;
        var db = new FakeDatabase(); db.Campers.Add(camper);
        var main = new MainViewModel(db);
        return (db, main, new(main, camper));
    }
}

using CamperManagement.Models;
using CamperManagement.Tests;
using CamperManagement.ViewModels;
namespace CamperManagement.UnitTests;

public class WorkflowTests
{
    private static (FakeDatabase db, MainViewModel main, AddRechnungViewModel form) Setup()
    {
        var db = new FakeDatabase();
        var main = new MainViewModel(db, clock: new FixedClock());
        return (db, main, new(main, db));
    }
    [Fact]
    public void V01_U03_ConstructorsDoNotConnect()
    {
        var (db, main, form) = Setup();
        Assert.Equal(0, db.Calls);
        Assert.Equal(2026, form.Jahr);
        Assert.Equal("Strom", form.SelectedArt);
        Assert.False(main.CanNavigateBack);
    }
    [Fact]
    public async Task V02_NoPlaceMeansNoWrite()
    {
        var (db, _, vm) = Setup();
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Equal(0, db.Adds);
        Assert.False(vm.SaveCommand.CanExecute(null));
    }
    [Theory]
    [InlineData("bad")]
    [InlineData("")]
    [InlineData("1.2.3")]
    public async Task V03_InvalidBindingCannotSaveStaleNumber(string input)
    {
        var (db, _, vm) = Setup();
        vm.SelectedPlatznummer = "1";
        await vm.SelectionTask;
        vm.NeuText = input;
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Equal(0, db.Adds);
    }
    [Fact]
    public async Task V04_V05_SaveAndAdvance()
    {
        var (db, _, vm) = Setup();
        await vm.InitializeAsync();
        vm.SelectedPlatznummer = "1";
        await vm.SelectionTask;
        vm.Neu = 20;
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Equal(5, db.SavedInvoice!.Betrag);
        Assert.Equal("2", vm.SelectedPlatznummer);
        await vm.SelectionTask;
        vm.Neu = 30;
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Equal("1", vm.SelectedPlatznummer);
        Assert.Equal(2, db.Adds);
    }
    [Fact]
    public async Task V06_EmptyPlaceList()
    {
        var (db, _, vm) = Setup();
        db.Places.Clear();
        await vm.InitializeAsync();
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Equal(0, db.Adds);
    }
    [Fact]
    public async Task V07_FailedSaveKeepsInputsAndPage()
    {
        var (db, main, vm) = Setup();
        main.NavigateToCommand.Execute(vm);
        await main.NavigationTask;
        vm.SelectedPlatznummer = "1";
        await vm.SelectionTask;
        vm.Neu = 22;
        db.SaveError = new IOException();
        await vm.SaveAndCloseCommand.ExecuteAsync(null);
        Assert.Same(vm, main.CurrentView);
        Assert.Equal(22, vm.Neu);
        Assert.Equal("1", vm.SelectedPlatznummer);
        Assert.NotNull(vm.StatusMessage);
        db.SaveError = null;
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Equal(1, db.Adds);
    }
    [Fact]
    public async Task V08_SaveAndCloseOnlyAfterSuccess()
    {
        var (db, main, vm) = Setup();
        var tabs = main.CurrentView;
        main.NavigateToCommand.Execute(vm);
        await main.NavigationTask;
        vm.SelectedPlatznummer = "1";
        await vm.SelectionTask;
        await vm.SaveAndCloseCommand.ExecuteAsync(null);
        Assert.Same(tabs, main.CurrentView);
        Assert.Equal(1, db.Adds);
    }
    [Fact]
    public async Task V09_V10_EditCopyAndArtPersistence()
    {
        var db = new FakeDatabase();
        var main = new MainViewModel(db);
        var invoice = Data.Invoice();
        var vm = new EditRechnungViewModel(main, db, invoice);
        await vm.InitializeAsync();
        Assert.Equal(8, vm.Faktor);
        vm.Neu = 150;
        Assert.Equal(112.5m, invoice.Neu);
        vm.SelectedArt = "Strom";
        await vm.SelectionTask;
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Equal("Strom", db.SavedInvoice!.Type);
        Assert.Equal(invoice.Id, db.SavedInvoice.Id);
        Assert.Equal("Wasser", invoice.Art);
    }
    [Theory]
    [InlineData("Platznr")]
    [InlineData("Vorname")]
    [InlineData("Nachname")]
    [InlineData("Straße")]
    [InlineData("PLZ")]
    [InlineData("Ort")]
    public async Task V14_RequiredCamperField(string field)
    {
        var db = new FakeDatabase();
        var c = Data.Camper();
        typeof(CamperDisplayModel).GetProperty(field)!.SetValue(c, "  ");
        var vm = new EditCamperViewModel(new(db), db, c);
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Equal(0, db.Updates);
        Assert.Contains("Pflichtfeld", vm.StatusMessage);
    }
    [Theory]
    [InlineData("", 0)]
    [InlineData("12,5", 12.5)]
    [InlineData("0", 0)]
    public async Task V11_V14_OptionalFieldsAndCosts(string input, double expected)
    {
        var db = new FakeDatabase();
        var vm = new AddCamperViewModel(new(db), db) { SelectedPlatznummer = "1", Vorname = "A", Nachname = "B", Straße = "Weg 1", Plz = "01234", Ort = "Ort", VertragskostenText = input };
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Equal(0, db.Deactivations);
        Assert.Equal(1, db.Adds);
        Assert.Equal((decimal)expected, db.SavedCamper!.Vertragskosten);
    }
    [Fact]
    public async Task V12_V13_FailedCamperWriteDoesNotDeactivate()
    {
        var db = new FakeDatabase { SaveError = new IOException() };
        var c = Data.Camper();
        var vm = new EditCamperViewModel(new(db), db, c) { Vorname = "Changed" };
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Equal("Test", c.Vorname);
        Assert.Equal(0, db.Deactivations);
        Assert.NotNull(vm.StatusMessage);
    }
    [Fact]
    public async Task V14_InvalidContractCostIsNotZero()
    {
        var db = new FakeDatabase();
        var vm = new EditCamperViewModel(new(db), db, Data.Camper()) { VertragskostenText = "abc" };
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Equal(0, db.Updates);
        Assert.NotNull(vm.StatusMessage);
    }
    [Fact]
    public async Task V15_ListReloadsAfterSave()
    {
        var db = new FakeDatabase();
        var main = new MainViewModel(db);
        var list = (CamperViewModel)((TabViewModel)main.CurrentView).CamperView;
        list.AddCamperCommand.Execute(null);
        await main.NavigationTask;
        var form = Assert.IsType<AddCamperViewModel>(main.CurrentView);
        db.Campers.Add(Data.Camper());
        form.SelectedPlatznummer = "1";
        form.Vorname = "A";
        form.Nachname = "B";
        form.Straße = "C";
        form.Plz = "D";
        form.Ort = "E";
        await form.SaveCommand.ExecuteAsync(null);
        Assert.Single(list.CamperList);
    }
    [Fact]
    public async Task V18_YearsSortedAndNoDataVisible()
    {
        var db = new FakeDatabase();
        var vm = new PrintSelectionViewModel(new(db), db);
        await vm.InitializeAsync();
        Assert.Equal(new[] { 2026, 2025 }, vm.Jahre);
        await vm.PrintCommand.ExecuteAsync(null);
        Assert.Contains("Keine Daten", vm.StatusMessage);
    }
    [Fact]
    public async Task A01_LateReadingCannotOverwriteCurrentSelection()
    {
        var (db, _, vm) = Setup();
        var a = new TaskCompletionSource<decimal>();
        db.Reading = (p, _) => p == "1" ? a.Task : Task.FromResult(99m);
        vm.SelectedPlatznummer = "1";
        var old = vm.SelectionTask;
        vm.SelectedPlatznummer = "2";
        await vm.SelectionTask;
        a.SetResult(1);
        await old;
        Assert.Equal(99, vm.Alt);
    }
    [Fact]
    public async Task A03_LatestReloadWins()
    {
        var db = new FakeDatabase();
        var first = new TaskCompletionSource<List<RechnungDisplayModel>>();
        db.InvoiceLoader = () => first.Task;
        var vm = new RechnungenViewModel(new(db));
        var old = vm.LoadDataAsync();
        db.InvoiceLoader = () => Task.FromResult(new List<RechnungDisplayModel> { Data.Invoice(2) });
        await vm.LoadDataAsync();
        first.SetResult(new() { Data.Invoice(1) });
        await old;
        Assert.Equal(2, Assert.Single(vm.RechnungenList).Id);
    }
    [Fact]
    public async Task A04_SharedSaveGuard()
    {
        var (db, _, vm) = Setup();
        vm.SelectedPlatznummer = "1";
        await vm.SelectionTask;
        var pending = new TaskCompletionSource();
        db.Insert = _ => pending.Task;
        var first = vm.SaveCommand.ExecuteAsync(null);
        var second = vm.SaveAndCloseCommand.ExecuteAsync(null);
        Assert.True(vm.IsBusy);
        pending.SetResult();
        await Task.WhenAll(first, second);
        Assert.Equal(1, db.Adds);
    }
    [Fact]
    public async Task A08_LeavingFormIgnoresLateResponse()
    {
        var (db, _, vm) = Setup();
        var pending = new TaskCompletionSource<decimal>();
        db.Reading = (_, _) => pending.Task;
        vm.SelectedPlatznummer = "1";
        var old = vm.SelectionTask;
        vm.Deactivate();
        pending.SetResult(888);
        await old;
        Assert.NotEqual(888, vm.Alt);
    }
    [Fact]
    public void U01_U02_NavigationPreservesTabInstance()
    {
        var main = new MainViewModel(new FakeDatabase());
        var tabs = main.CurrentView;
        main.NavigateToCommand.Execute(new object());
        var second = main.CurrentView;
        main.NavigateToCommand.Execute(new object());
        main.NavigateToCommand.Execute(null);
        Assert.Same(second, main.CurrentView);
        main.NavigateBackCommand.Execute(null);
        Assert.Same(tabs, main.CurrentView);
        main.NavigateBackCommand.Execute(null);
        Assert.Same(tabs, main.CurrentView);
        Assert.False(main.CanNavigateBack);
    }

    [Fact]
    public async Task A02_FailedLoadIsVisibleAndRetryable()
    {
        var db = new FakeDatabase { InvoiceLoader = () => throw new IOException() };
        var vm = new RechnungenViewModel(new(db));
        await vm.InitializeAsync();
        Assert.NotNull(vm.StatusMessage);
        db.InvoiceLoader = () => Task.FromResult(new List<RechnungDisplayModel> { Data.Invoice() });
        await vm.LoadDataAsync();
        Assert.Single(vm.RechnungenList);
    }
    [Fact]
    public async Task A02_FailedReadingCannotBeSaved()
    {
        var (db, _, vm) = Setup();
        db.Reading = (_, _) => throw new IOException();
        vm.SelectedPlatznummer = "1";
        await vm.SelectionTask;
        Assert.NotNull(vm.StatusMessage);
        Assert.False(vm.SaveCommand.CanExecute(null));
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Equal(0, db.Adds);
    }
    [Fact]
    public async Task E06_YearDoesNotSelectOrResetTariff()
    {
        var (db, _, vm) = Setup();
        db.Factors = new(0.6m, 9, 1);
        vm.SelectedPlatznummer = "1";
        await vm.SelectionTask;
        await vm.InitializeAsync();
        Assert.Equal(0.6m, vm.Faktor);
        vm.Faktor = 0.77m;
        vm.Jahr = 2025;
        await vm.SelectionTask;
        Assert.Equal(0.77m, vm.Faktor);
        vm.SelectedArt = "Wasser";
        await vm.SelectionTask;
        Assert.Equal(9, vm.Faktor);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task E06_NextInvoiceUsesLatestFactors(bool singlePlace)
    {
        var (db, _, form) = Setup();
        if (singlePlace) db.Places = new() { "1" };
        await form.InitializeAsync();
        form.SelectedPlatznummer = "1";
        await form.SelectionTask;
        form.Neu = 20;
        await db.SaveStandardfaktorenAsync(db.Factors with { Strom = 0.9m });
        await form.SaveCommand.ExecuteAsync(null);
        Assert.Equal(0.5m, db.SavedInvoice!.Faktor);
        Assert.Equal(0.9m, form.Faktor);
        form.Neu = 30;
        await form.SaveCommand.ExecuteAsync(null);
        Assert.Equal(0.9m, db.SavedInvoice!.Faktor);
    }
    [Fact]
    public async Task E06_BackFromSettingsRefreshesNewDraft()
    {
        var (db, main, form) = Setup();
        main.NavigateToCommand.Execute(form);
        await main.NavigationTask;
        form.SelectedPlatznummer = "1";
        await form.SelectionTask;
        main.SettingsCommand.Execute(null);
        await main.NavigationTask;
        var settings = Assert.IsType<SettingsViewModel>(main.CurrentView);
        settings.StromText = "0,8";
        await settings.SaveCommand.ExecuteAsync(null);
        main.NavigateBackCommand.Execute(null);
        await main.NavigationTask;
        Assert.Equal(0.8m, form.Faktor);
        Assert.True(form.SaveCommand.CanExecute(null));
    }
    [Fact]
    public async Task A08_BackReactivatesExistingForm()
    {
        var (db, main, vm) = Setup();
        main.NavigateToCommand.Execute(vm);
        await main.NavigationTask;
        vm.SelectedPlatznummer = "1";
        await vm.SelectionTask;
        main.SettingsCommand.Execute(null);
        await main.NavigationTask;
        main.NavigateBackCommand.Execute(null);
        await main.NavigationTask;
        Assert.Same(vm, main.CurrentView);
        vm.SelectedPlatznummer = "2";
        await vm.SelectionTask;
        Assert.True(vm.SaveCommand.CanExecute(null));
    }
}

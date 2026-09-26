using Avalonia.Platform.Storage;
using CamperManagement.Models;
using CamperManagement.Services;
using CamperManagement.Tests;
using CamperManagement.ViewModels;
namespace CamperManagement.UnitTests;

public class SearchTests
{
    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("  ", true)]
    [InlineData("mÜlLeR", true)]
    [InlineData("Müller Hamburg", true)]
    [InlineData("Müller Berlin", false)]
    [InlineData("\"Hans Müller\" Hamburg", true)]
    [InlineData("\"Hans Müller", true)]
    [InlineData("Hans\tHamburg\n", true)]
    [InlineData("\"\"", true)]
    [InlineData("O'Neil", true)]
    public void S01_S08_SearchTerms(string? query, bool expected) => Assert.Equal(expected, SearchQuery.Matches(query, "Hans Müller", "Hamburg", "O'Neil", null));
    [Fact]
    public async Task S07_S10_NullOptionalFieldsAndReload()
    {
        var db = new FakeDatabase();
        db.Campers.Add(Data.Camper());
        var vm = new CamperViewModel(new(db)) { CamperSearchQuery = "Müller" };
        await vm.LoadDataAsync();
        Assert.Single(vm.FilteredCamperList);
        db.Campers.Add(new()
        {
            Vorname = "Other"
        });
        await vm.LoadDataAsync();
        Assert.Single(vm.FilteredCamperList);
        vm.CamperSearchQuery = "";
        Assert.Equal(2, vm.FilteredCamperList.Count);
    }
    [Theory]
    [InlineData("12,5")]
    [InlineData("12.5")]
    [InlineData("2026")]
    [InlineData("Wasser")]
    [InlineData("Nein")]
    public async Task S09_InvoiceSearch(string query)
    {
        var db = new FakeDatabase();
        db.Invoices.Add(Data.Invoice());
        var vm = new RechnungenViewModel(new(db));
        await vm.LoadDataAsync();
        vm.RechnungSearchQuery = query;
        Assert.Single(vm.FilteredRechnungenList);
    }
}
public class SettingsTests
{
    [Fact]
    public async Task E01_E03_LoadAndSaveCurrentFactors()
    {
        var db = new FakeDatabase();
        var vm = new SettingsViewModel(db);
        await vm.InitializeAsync();
        vm.StromText = "0,75";
        vm.WasserText = "9,2";
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Equal(0.75m, db.Factors.Strom);
        Assert.Equal(9.2m, db.Factors.Wasser);
        Assert.NotNull(vm.StatusMessage);
    }
    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("1,2,3")]
    public async Task E08_InvalidInputNotSaved(string input)
    {
        var db = new FakeDatabase();
        var vm = new SettingsViewModel(db);
        await vm.InitializeAsync();
        vm.StromText = input;
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Equal(0.5m, db.Factors.Strom);
        Assert.NotNull(vm.StatusMessage);
    }
    [Fact]
    public async Task E09_FailureKeepsInput()
    {
        var db = new FakeDatabase();
        var vm = new SettingsViewModel(db);
        await vm.InitializeAsync();
        db.SaveError = new IOException();
        vm.StromText = "2";
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Equal("2", vm.StromText);
        Assert.Equal(0.5m, db.Factors.Strom);
        Assert.False(vm.IsBusy);
    }
    [Fact]
    public async Task E10_LatestLoadWins()
    {
        var db = new FakeDatabase();
        var wait = new TaskCompletionSource<Standardfaktoren>();
        var requests = 0;
        db.FactorLoader = () => ++requests == 1 ? wait.Task : Task.FromResult(new Standardfaktoren(2, 9, 2));
        var vm = new SettingsViewModel(db);
        var old = vm.InitializeAsync();
        await vm.InitializeAsync();
        wait.SetResult(new(1, 1, 1));
        await old;
        Assert.Equal("2", vm.StromText);
    }
    [Theory]
    [InlineData(2025, "Strom", "0.75")]
    [InlineData(2026, "Strom", "0.75")]
    [InlineData(2025, "Wasser", "9.2")]
    [InlineData(2026, "Wasser", "9.2")]
    public async Task E04_E06_SavedFactorsApplyImmediatelyRegardlessOfBillingYear(int year, string art, string expected)
    {
        var db = new FakeDatabase();
        var settings = new SettingsViewModel(db);
        await settings.InitializeAsync();
        settings.StromText = "0,75";
        settings.WasserText = "9,2";
        await settings.SaveCommand.ExecuteAsync(null);
        var main = new MainViewModel(db, clock: new FixedClock());
        var form = new AddRechnungViewModel(main, db) { Jahr = year, SelectedArt = art };
        await form.InitializeAsync();
        form.SelectedPlatznummer = "1";
        await form.SelectionTask;
        form.Neu = 20;
        var factor = decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(factor, form.Faktor);
        await form.SaveAndCloseCommand.ExecuteAsync(null);
        Assert.Equal(factor, db.SavedInvoice!.Faktor);
        Assert.Equal(year, db.SavedInvoice.Jahr);
        Assert.Equal(10 * factor, db.SavedInvoice.Betrag);
    }
    [Fact]
    public async Task E05_SettingsNeverReplaceStoredInvoiceFactorOnEditOrResume()
    {
        var db = new FakeDatabase { Factors = new(2, 99, 3) };
        var main = new MainViewModel(db, clock: new FixedClock());
        var invoice = Data.Invoice();
        var edit = new EditRechnungViewModel(main, db, invoice);
        await edit.InitializeAsync();
        edit.Deactivate();
        await edit.ResumeAsync();
        Assert.Equal(invoice.Faktor, edit.Faktor);
        Assert.Equal(invoice.Betrag, edit.Betrag);
        Assert.Equal(0, db.Calls);
    }

}
public class ExportWorkflowTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task V16_CancelDoesNotMark(bool group)
    {
        var db = new FakeDatabase();
        var pdf = new FakePdf();
        var vm = new RechnungenViewModel(new(db, pdf));
        vm.SelectedRechnungen.Add(Data.Invoice());
        await (group ? vm.CreateRechnungenCommand : vm.PrintRechnungCommand).ExecuteAsync(null);
        Assert.Empty(db.Printed);
        Assert.Equal(0, pdf.OpenCount);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task V16_A07_SuccessMarksOriginalImmutableSelection(bool group)
    {
        var db = new FakeDatabase();
        var pdf = new FakePdf();
        var vm = new RechnungenViewModel(new(db, pdf));
        var original = Data.Invoice();
        vm.SelectedRechnungen.Add(original);
        void Change(IReadOnlyList<RechnungDisplayModel> rows)
        {
            original.Betrag = 999;
            vm.SelectedRechnungen.Clear();
            vm.SelectedRechnungen.Add(Data.Invoice(9));
            Assert.Equal(100, rows[0].Betrag);
        }
        pdf.Export = rows => { Change(rows); return Task.FromResult<IStorageFile?>(Data.FileHandle()); };
        pdf.Group = rows => { Change(rows); return Task.FromResult(true); };
        await (group ? vm.CreateRechnungenCommand : vm.PrintRechnungCommand).ExecuteAsync(null);
        Assert.Equal(new[] { 1 }, db.Printed);
    }
    [Fact]
    public async Task V16_PartialGroupFailureMarksNothing()
    {
        var db = new FakeDatabase();
        var pdf = new FakePdf { Group = _ => throw new IOException("Second file failed") };
        var vm = new RechnungenViewModel(new(db, pdf));
        vm.SelectedRechnungen.Add(Data.Invoice());
        vm.SelectedRechnungen.Add(Data.Invoice(2));
        await vm.CreateRechnungenCommand.ExecuteAsync(null);
        Assert.Empty(db.Printed);
        Assert.NotNull(vm.StatusMessage);
    }
    [Fact]
    public async Task V17_StatusFailureKeepsPdfSuccessDistinct()
    {
        var db = new FakeDatabase { Mark = _ => throw new IOException() };
        var pdf = new FakePdf { Export = _ => Task.FromResult<IStorageFile?>(Data.FileHandle()) };
        var vm = new RechnungenViewModel(new(db, pdf));
        vm.SelectedRechnungen.Add(Data.Invoice());
        await vm.PrintRechnungCommand.ExecuteAsync(null);
        Assert.Contains("PDFs gespeichert", vm.StatusMessage);
        Assert.Empty(db.Printed);
    }
    [Fact]
    public async Task P17_ViewerFailureDoesNotUndoPrinted()
    {
        var db = new FakeDatabase();
        var pdf = new FakePdf { Opens = false, Export = _ => Task.FromResult<IStorageFile?>(Data.FileHandle()) };
        var vm = new RechnungenViewModel(new(db, pdf));
        vm.SelectedRechnungen.Add(Data.Invoice());
        await vm.PrintRechnungCommand.ExecuteAsync(null);
        Assert.Single(db.Printed);
        Assert.Contains("Viewer", vm.StatusMessage);
    }
    [Fact]
    public void U10_ReplacedSelectionUpdatesCommands()
    {
        var vm = new RechnungenViewModel(new(new FakeDatabase()));
        var old = vm.SelectedRechnungen;
        Assert.False(vm.PrintRechnungCommand.CanExecute(null));
        vm.SelectedRechnungen = new() { Data.Invoice() };
        Assert.True(vm.PrintRechnungCommand.CanExecute(null));
        vm.SelectedRechnungen.Clear();
        old.Add(Data.Invoice());
        Assert.False(vm.PrintRechnungCommand.CanExecute(null));
    }
    [Fact]
    public async Task A10_SharedExportGuard()
    {
        var db = new FakeDatabase();
        var pending = new TaskCompletionSource<IStorageFile?>();
        var pdf = new FakePdf { Export = _ => pending.Task };
        var vm = new RechnungenViewModel(new(db, pdf));
        vm.SelectedRechnungen.Add(Data.Invoice());
        var first = vm.PrintRechnungCommand.ExecuteAsync(null);
        Assert.False(vm.CreateRechnungenCommand.CanExecute(null));
        await vm.CreateRechnungenCommand.ExecuteAsync(null);
        pending.SetResult(Data.FileHandle());
        await first;
        Assert.Single(db.Printed);
    }

    [Fact]
    public async Task A09_LateProgressDoesNotEraseCompletion()
    {
        var old = SynchronizationContext.Current;
        var context = new QueuedContext();
        SynchronizationContext.SetSynchronizationContext(context);
        try
        {
            var db = new FakeDatabase();
            var pdf = new FakePdf { ExportWithProgress = (_, progress) => { progress.Report("old progress"); return Task.FromResult<IStorageFile?>(Data.FileHandle()); } };
            var vm = new RechnungenViewModel(new(db, pdf));
            vm.SelectedRechnungen.Add(Data.Invoice());
            await vm.PrintRechnungCommand.ExecuteAsync(null);
            var final = vm.StatusMessage;
            context.Flush();
            Assert.Equal(final, vm.StatusMessage);
            Assert.DoesNotContain("old progress", vm.StatusMessage);
        }
        finally { SynchronizationContext.SetSynchronizationContext(old); }
    }
    private sealed class QueuedContext : SynchronizationContext
    {
        private readonly Queue<Action> _queue = new(); public override void Post(SendOrPostCallback d, object? state) => _queue.Enqueue(() => d(state)); public void Flush()
        {
            while (_queue.TryDequeue(out var action))
                action();
        }
    }
}

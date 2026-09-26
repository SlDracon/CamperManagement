using System.Text.Json;
using CamperManagement.Models;
using CamperManagement.Services;
using CamperManagement.Tests;
using CamperManagement.ViewModels;

namespace CamperManagement.UnitTests;

public class QualityTests
{
    [Fact]
    public async Task ReloadCancelsOldQueryAndIgnoresAnUncooperativeResponse()
    {
        var first = new TaskCompletionSource<List<RechnungDisplayModel>>();
        CancellationToken oldToken = default;
        var calls = 0;
        var db = StubProxy.Create<IDatabaseService>((_, args) =>
        {
            if (++calls != 1) return Task.FromResult(new List<RechnungDisplayModel> { Data.Invoice(2) });
            oldToken = (CancellationToken)args![0]!;
            return first.Task;
        });
        var vm = new RechnungenViewModel(new(db));
        var loading = vm.LoadDataAsync();
        Assert.True(vm.IsLoading);
        Assert.False(vm.IsSaving);
        await vm.LoadDataAsync();
        Assert.True(oldToken.IsCancellationRequested);
        first.SetResult([Data.Invoice(1)]);
        await loading;
        Assert.Equal(2, Assert.Single(vm.RechnungenList).Id);
        Assert.False(vm.IsLoading);
        Assert.False(vm.CancelOperationCommand.CanExecute(null));
    }

    [Fact]
    public async Task NavigationCancelsOverviewLoadAndDoesNotPublishHiddenResults()
    {
        var pending = new TaskCompletionSource<List<CamperDisplayModel>>();
        CancellationToken token = default;
        var db = StubProxy.Create<IDatabaseService>((method, args) => method switch
        {
            "GetActiveCampersAsync" => Capture(),
            "GetRechnungenAsync" => Task.FromResult(new List<RechnungDisplayModel>()),
            "GetStandardfaktorenAsync" => Task.FromResult(new Standardfaktoren(0.5m, 8m, 1)),
            _ => throw new InvalidOperationException(method)
        });
        object Capture() { return pending.Task; }
        // Capture cancellation separately to make navigation's effect observable.
        var wrapped = StubProxy.Create<IDatabaseService>((method, args) =>
        {
            if (method == "GetActiveCampersAsync") token = (CancellationToken)args![0]!;
            return typeof(IDatabaseService).GetMethod(method)!.Invoke(db, args);
        });
        var main = new MainViewModel(wrapped);
        var tabs = (TabViewModel)main.CurrentView;
        var loading = main.InitializeAsync();
        main.SettingsCommand.Execute(null);
        await main.NavigationTask;
        Assert.IsType<SettingsViewModel>(main.CurrentView);
        Assert.True(token.IsCancellationRequested);
        pending.SetResult([Data.Camper()]);
        await loading;
        Assert.Empty(((CamperViewModel)tabs.CamperView).CamperList);
    }

    [Fact]
    public async Task CancelSelectionPreventsSaveAndAllowsReload()
    {
        var pending = new TaskCompletionSource<Standardfaktoren>();
        var db = new FakeDatabase { FactorLoader = () => pending.Task };
        var vm = new AddRechnungViewModel(new(db), db) { SelectedPlatznummer = "1" };
        var task = vm.SelectionTask;
        Assert.True(vm.IsLoading);
        vm.CancelOperationCommand.Execute(null);
        pending.SetResult(new(7, 9, 3));
        await task;
        Assert.False(vm.SaveCommand.CanExecute(null));
        Assert.False(vm.IsLoading);
        db.FactorLoader = null;
        Assert.True(vm.LoadFailed);
        await vm.RetryLoadCommand.ExecuteAsync(null);
        Assert.False(vm.LoadFailed);
        Assert.True(vm.SaveCommand.CanExecute(null));
    }

    [Fact]
    public async Task ExportCancellationNeverMarksOrOpensEvenIfProviderReturnsSuccess()
    {
        var db = new FakeDatabase();
        var pending = new TaskCompletionSource<Avalonia.Platform.Storage.IStorageFile?>();
        var pdf = new FakePdf { Export = _ => pending.Task };
        var vm = new RechnungenViewModel(new(db, pdf));
        vm.SelectedRechnungen.Add(Data.Invoice());
        var exporting = vm.PrintRechnungCommand.ExecuteAsync(null);
        Assert.True(vm.IsExporting);
        Assert.False(vm.IsSaving);
        vm.CancelOperationCommand.Execute(null);
        pending.SetResult(Data.FileHandle());
        await exporting;
        Assert.Empty(db.Printed);
        Assert.Equal(0, pdf.OpenCount);
        Assert.Contains("abgebrochen", vm.StatusMessage);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task SuccessfulExportFinishesStatusTransactionWithoutCancellation()
    {
        var commit = new TaskCompletionSource();
        var db = new FakeDatabase { Mark = _ => commit.Task };
        var pdf = new FakePdf { Export = _ => Task.FromResult<Avalonia.Platform.Storage.IStorageFile?>(Data.FileHandle()) };
        var vm = new RechnungenViewModel(new(db, pdf));
        vm.SelectedRechnungen.Add(Data.Invoice());
        var task = vm.PrintRechnungCommand.ExecuteAsync(null);
        Assert.True(vm.IsSaving);
        Assert.False(vm.IsExporting);
        Assert.False(vm.CancelOperationCommand.CanExecute(null));
        commit.SetResult();
        await task;
        Assert.Single(db.Printed);
    }

    [Fact]
    public async Task LoadFailureIsLoggedWithoutPublishingItsMessage()
    {
        var log = new CaptureLog();
        var db = new FakeDatabase { InvoiceLoader = () => throw new IOException("secret=private-data") };
        var vm = new RechnungenViewModel(new(db, log: log));
        await vm.LoadDataAsync();
        Assert.Equal(ErrorOperation.Load, Assert.Single(log.Operations));
        Assert.DoesNotContain("private-data", vm.StatusMessage);
        Assert.False(vm.IsLoading);
    }

    [Fact]
    public void LocalLogRotatesWithoutSecretsMessagesOrPaths()
    {
        var root = Directory.CreateTempSubdirectory("camper-log-test-").FullName;
        try
        {
            var log = new LocalErrorLog(root, 4096);
            Exception error;
            try { throw new IOException("Password=synthetic-secret; Kunde=Max Test; SQL=SELECT *", new Exception("inner-secret")); }
            catch (IOException caught) { error = caught; }
            for (var i = 0; i < 100; i++) log.Write(ErrorOperation.Export, error);
            var files = Directory.GetFiles(root);
            Assert.Equal(2, files.Length);
            foreach (var file in files)
            {
                Assert.InRange(new FileInfo(file).Length, 1, 4096);
                var text = File.ReadAllText(file);
                Assert.DoesNotContain("synthetic-secret", text);
                Assert.DoesNotContain("inner-secret", text);
                Assert.DoesNotContain("Max Test", text);
                Assert.DoesNotContain("SELECT", text);
                Assert.DoesNotContain(root, text);
                foreach (var line in File.ReadLines(file))
                {
                    using var json = JsonDocument.Parse(line);
                    Assert.Equal("Export", json.RootElement.GetProperty("operation").GetString());
                    Assert.Contains("IOException", json.RootElement.GetProperty("type").GetString());
                }
                if (!OperatingSystem.IsWindows()) Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(file));
            }
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void LogFailureNeverEscapes()
    {
        var path = Path.GetTempFileName();
        try { new LocalErrorLog(path).Write(ErrorOperation.Load, new Exception("private")); }
        finally { File.Delete(path); }
    }

    [Fact]
    public void DesignPreviewUsesOnlySyntheticRows()
    {
        var preview = DesignData.Rechnungen;
        Assert.Equal(2, preview.FilteredRechnungenList.Count);
        Assert.All(preview.FilteredRechnungenList, row => Assert.Null(row.Nachname));
        Assert.False(preview.IsBusy);
    }

    private sealed class CaptureLog : IErrorLog
    {
        public List<ErrorOperation> Operations { get; } = new();
        public void Write(ErrorOperation operation, Exception error) => Operations.Add(operation);
    }
}

using Avalonia.VisualTree;
using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.Xaml.Interactivity;
using Avalonia.Platform.Storage;
using CamperManagement.Behavior;
using CamperManagement.Models;
using CamperManagement.Services;
using CamperManagement.Tests;
using CamperManagement.ViewModels;
using CamperManagement.Views;
[assembly: AvaloniaTestApplication(typeof(CamperManagement.UiTests.TestAppBuilder))]
namespace CamperManagement.UiTests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
public class UiTests
{
    [AvaloniaFact]
    public void U03_MainViewDoesNotReplaceContext()
    {
        var db = new FakeDatabase();
        var main = new MainViewModel(db);
        var view = new MainView { DataContext = main };
        Assert.Same(main, view.DataContext);
        Assert.Equal(0, db.Calls);
    }
    [AvaloniaFact]
    public void U04_AllProductionTemplates()
    {
        var db = new FakeDatabase();
        var main = new MainViewModel(db);
        object[] models = [main, main.CurrentView, new CamperViewModel(main), new RechnungenViewModel(main), new AddCamperViewModel(main, db), new EditCamperViewModel(main, db, Data.Camper()), new AddRechnungViewModel(main, db), new EditRechnungViewModel(main, db, Data.Invoice()), new PrintSelectionViewModel(main, db), new SettingsViewModel(db)];
        foreach (var model in models)
        {
            var template = Application.Current!.DataTemplates.Single(t => t.Match(model));
            Assert.IsAssignableFrom<Control>(template.Build(model));
        }
        Assert.Equal(0, db.Calls);
    }
    [AvaloniaFact]
    public async Task U05_InvalidNumericBindingPreventsSave()
    {
        var db = new FakeDatabase();
        var vm = new AddRechnungViewModel(new(db), db);
        vm.SelectedPlatznummer = "1";
        await vm.SelectionTask;
        var view = new AddRechnungView { DataContext = vm };
        var window = new Window { Content = view };
        window.Show();
        try
        {
            var box = view.FindControl<TextBox>("NeuTextBox")!;
            box.Focus();
            box.SelectAll();
            window.KeyTextInput("abc");
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("abc", vm.NeuText);
            Assert.False(vm.SaveCommand.CanExecute(null));
            await vm.SaveCommand.ExecuteAsync(null);
            Assert.Equal(0, db.Adds);
        }
        finally { window.Close(); }
    }
    [AvaloniaFact]
    public void U06_FocusCallbackTracksDataContext()
    {
        var db = new FakeDatabase();
        var view = new AddRechnungView();
        var first = new AddRechnungViewModel(new(db), db);
        var second = new AddRechnungViewModel(new(db), db);
        view.DataContext = first;
        Assert.NotNull(first.SetFocusToNeuTextBox);
        view.DataContext = second;
        Assert.Null(first.SetFocusToNeuTextBox);
        Assert.NotNull(second.SetFocusToNeuTextBox);
        view.DataContext = null;
        Assert.Null(second.SetFocusToNeuTextBox);
    }
    [AvaloniaFact]
    public void U08_U09_SelectionBothWaysAndDetach()
    {
        var values = new ObservableCollection<string> { "one", "two" };
        var grid = new DataGrid { ItemsSource = values, SelectionMode = DataGridSelectionMode.Extended };
        var selected = new ObservableCollection<string>();
        var behavior = new DataGridSelectedItemsBehavior { SelectedItems = selected };
        Interaction.GetBehaviors(grid).Add(behavior);
        var window = new Window { Content = grid };
        window.Show();
        try
        {
            grid.SelectedItems.Add("one");
            Assert.Equal(new[] { "one" }, selected);
            selected.Add("two");
            Assert.Equal(2, grid.SelectedItems.Count);
            var replacement = new ObservableCollection<string> { "two" };
            behavior.SelectedItems = replacement;
            Assert.Equal("two", Assert.Single(grid.SelectedItems.Cast<string>()));
            selected.Clear();
            Assert.Single(grid.SelectedItems.Cast<object>());
            Interaction.GetBehaviors(grid).Remove(behavior);
            grid.SelectedItems.Clear();
            Assert.Single(replacement);
        }
        finally { window.Close(); }
    }
    [AvaloniaTheory]
    [InlineData("costs")]
    [InlineData("readings")]
    [InlineData("table")]
    [InlineData("invoice")]
    [InlineData("group")]
    public async Task A05_A06_RealDispatcherRemainsResponsiveDuringWrites(string kind)
    {
        using var storage = new StorageFixture();
        var uiThread = Environment.CurrentManagedThreadId;
        var ticks = 0;
        storage.Picking = () => Assert.Equal(uiThread, Environment.CurrentManagedThreadId);
        storage.WriteStream = path =>
        {
            Assert.Equal(uiThread, Environment.CurrentManagedThreadId);
            return new HeartbeatStream(File.Create(path), () =>
            {
                Assert.NotEqual(uiThread, Environment.CurrentManagedThreadId);
                using var heartbeat = new ManualResetEventSlim();
                Dispatcher.UIThread.Post(() => { ticks++; heartbeat.Set(); });
                Assert.True(heartbeat.Wait(TimeSpan.FromSeconds(5)), "UI dispatcher was blocked by PDF rendering");
            });
        };
        var file = storage.File(storage.PathFor("responsive.pdf"));
        var provider = storage.Provider(file);
        var rows = new[] { Data.Invoice() };
        var progress = new Progress<string?>(_ => Assert.Equal(uiThread, Environment.CurrentManagedThreadId));
        switch (kind)
        {
            case "costs":
                await PdfService.GenerateKostenPdfAsync(provider, 2026, [new KostenEintrag { PlatzNr = "1", Vorname = "A", Nachname = "B" }]);
                break;
            case "readings":
                await PdfService.GenerateAbleseTabellePdfAsync(provider, [new AbleseEintrag { PlatzNr = "1", Vorname = "A", Nachname = "B" }]);
                break;
            case "table":
                await PdfService.GenerateTabellePdfAsync(provider, rows);
                break;
            case "group":
                Assert.True(await PdfService.GenerateRechnungenByPlatzAsync(storage.FolderProvider(), rows, progress));
                break;
            default:
                await PdfService.GenerateAndMergeRechnungenAsync(provider, rows, progress);
                break;
        }
        Dispatcher.UIThread.RunJobs();
        Assert.True(ticks > 0);
    }

    [AvaloniaFact]
    public async Task U07_DoubleTapOnlyOpensSelectedInvoice()
    {
        var db = new FakeDatabase();
        var main = new MainViewModel(db);
        var vm = new RechnungenViewModel(main);
        var view = new RechnungenView { DataContext = vm };
        var window = new Window { Content = view };
        window.Show();
        try
        {
            var grid = view.GetVisualDescendants().OfType<DataGrid>().Single();
            grid.ItemsSource = new[] { Data.Invoice() };
            var tabs = main.CurrentView;
            grid.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Input.Gestures.DoubleTappedEvent));
            Assert.Same(tabs, main.CurrentView);
            grid.SelectedIndex = 0;
            grid.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Input.Gestures.DoubleTappedEvent));
            await main.NavigationTask;
            Assert.IsType<EditRechnungViewModel>(main.CurrentView);
        }
        finally { window.Close(); }
    }
    private sealed class HeartbeatStream(Stream inner, System.Action beat) : Stream
    {
        private bool _checked;
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (!_checked)
            {
                _checked = true;
                beat();
            }
            inner.Write(buffer, offset, count);
        }
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            if (!_checked)
            {
                _checked = true;
                beat();
            }
            inner.Write(buffer);
        }
        public override bool CanRead => false; public override bool CanSeek => inner.CanSeek; public override bool CanWrite => inner.CanWrite;
        public override long Length => inner.Length; public override long Position
        {
            get => inner.Position; set => inner.Position = value;
        }
        public override void Flush() => inner.Flush(); public override long Seek(long o, SeekOrigin origin) => inner.Seek(o, origin); public override void SetLength(long v) => inner.SetLength(v); public override int Read(byte[] b, int o, int c) => throw new NotSupportedException();
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                inner.Dispose();
            base.Dispose(disposing);
        }
    }
}

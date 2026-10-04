using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CamperManagement.Models;
using CamperManagement.Tests;
using CamperManagement.ViewModels;
using CamperManagement.Views;

namespace CamperManagement.UiTests;

public class ContractCostTests
{
    [AvaloniaTheory]
    [InlineData(390)]
    [InlineData(1000)]
    public async Task BookingFormBindsPreviewAndSubmitsDescription(int width)
    {
        var camper = Data.Camper(); camper.Vertragskosten = 100;
        var db = new FakeDatabase(); db.Campers.Add(camper); var main = new MainViewModel(db);
        var vm = new ContractCostIncreaseViewModel(main, camper); var view = new ContractCostIncreaseView { DataContext = vm };
        var window = new Window { Content = view, Width = width, Height = 720 }; window.Show();
        try
        {
            await vm.InitializeAsync(); Dispatcher.UIThread.RunJobs();
            Assert.Equal("100,00 €", view.FindControl<TextBlock>("CurrentCost")!.Text);
            view.FindControl<TextBox>("IncreaseAmount")!.Text = "12,345";
            view.FindControl<TextBox>("IncreaseDescription")!.Text = "Gestiegene Wartungskosten";
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("112,35 €", view.FindControl<TextBlock>("NewTotal")!.Text);
            var button = view.FindControl<Button>("BookIncrease")!; Assert.True(button.IsEffectivelyEnabled);
            await vm.BookCommand.ExecuteAsync(null); Dispatcher.UIThread.RunJobs();
            Assert.Equal(112.35m, camper.Vertragskosten); Assert.Equal("Gestiegene Wartungskosten", db.SavedDescription); Assert.False(button.IsEffectivelyEnabled);
        }
        finally { window.Close(); }
    }
    [AvaloniaFact]
    public async Task OverviewShowsFormattedFixedCostsAtEndAndNavigatesToBooking()
    {
        var db = new FakeDatabase(); var camper = Data.Camper(); camper.Vertragskosten = 123.45m; db.Campers.Add(camper);
        var main = new MainViewModel(db); var vm = (CamperViewModel)((TabViewModel)main.CurrentView).CamperView;
        var view = new CamperView { DataContext = vm }; var window = new Window { Content = view, Width = 1000, Height = 700 }; window.Show();
        try
        {
            await vm.InitializeAsync(); Dispatcher.UIThread.RunJobs();
            var grid = view.GetVisualDescendants().OfType<DataGrid>().Single();
            Assert.Equal("Fixkosten", grid.Columns.Last().Header);
            grid.ScrollIntoView(camper, grid.Columns.Last()); Dispatcher.UIThread.RunJobs();
            Assert.Contains(view.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "123,45 €");
            grid.SelectedItem = camper; Dispatcher.UIThread.RunJobs();
            var button = view.GetVisualDescendants().OfType<Button>().Single(b => Equals(b.Content, "Kosten erhöhen"));
            button.Command!.Execute(null); await main.NavigationTask;
            var form = Assert.IsType<ContractCostIncreaseViewModel>(main.CurrentView);
            var template = Application.Current!.DataTemplates.Single(t => t.Match(form));
            Assert.IsType<ContractCostIncreaseView>(template.Build(form));
        }
        finally { window.Close(); }
    }
    [AvaloniaFact]
    public async Task HistoryDisplaysFullBookingReasonAndAmount()
    {
        var db = new FakeDatabase(); var before = Data.Camper(); before.Vertragskosten = 100;
        var after = before.Snapshot(); after.Vertragskosten = 125;
        db.History.Add(new() { Kind = "cost_increased", Description = "Gebührenänderung\nMit zusätzlicher Leerung", Before = new(before, true, null, null), After = new(after, true, null, null) });
        var vm = new CamperHistoryViewModel(db); var view = new CamperHistoryView { DataContext = vm };
        var window = new Window { Content = view, Width = 390, Height = 720 }; window.Show();
        try
        {
            await vm.InitializeAsync(); Dispatcher.UIThread.RunJobs();
            Assert.Equal(db.History[0].Description, view.FindControl<TextBlock>("HistoryDescription")!.Text);
            Assert.Contains(view.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Erhöhung: +25,00 €");
        }
        finally { window.Close(); }
    }
}

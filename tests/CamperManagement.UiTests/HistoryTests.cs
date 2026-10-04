using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CamperManagement.Models;
using CamperManagement.Tests;
using CamperManagement.ViewModels;
using CamperManagement.Views;
namespace CamperManagement.UiTests;

public class HistoryTests
{
    [AvaloniaTheory]
    [InlineData(390)]
    [InlineData(1000)]
    public async Task HistoryShowsValuesAndFiltersWithoutLosingPlaceSelection(int width)
    {
        var db = new FakeDatabase(); var before=Data.Camper(); var after=before.Snapshot();after.Straße="Neuer Weg 2";
        db.History.Add(new() {Id=1,Kind="updated",RecordedAtUtc=DateTime.UtcNow,Before=new(before,true,new(2024,1,1),null),After=new(after,true,new(2024,1,1),null)});
        var vm=new CamperHistoryViewModel(db,"1");var view=new CamperHistoryView {DataContext=vm};
        var window=new Window {Content=view,Width=width,Height=720};window.Show();
        try
        {
            await vm.InitializeAsync();Dispatcher.UIThread.RunJobs();await vm.SelectionTask;
            Assert.Equal("1",vm.SelectedPlace);Assert.Single(vm.Entries);
            var list=view.FindControl<ListBox>("HistoryList")!;Assert.Same(vm.SelectedEntry,list.SelectedItem);
            var texts=view.GetVisualDescendants().OfType<TextBlock>().Select(t=>t.Text).ToArray();
            Assert.Contains("Vorher: Testweg 1",texts);Assert.Contains("Neuer Weg 2",texts);
            view.FindControl<TextBox>("HistorySearch")!.Text="does-not-exist";Dispatcher.UIThread.RunJobs();
            Assert.Empty(vm.Entries);Assert.True(vm.IsEmpty);
            view.FindControl<TextBox>("HistorySearch")!.Text="Testweg";Dispatcher.UIThread.RunJobs();Assert.Single(vm.Entries);
            view.FindControl<ComboBox>("HistoryPlace")!.SelectedItem="2";Dispatcher.UIThread.RunJobs();await vm.SelectionTask;Assert.Empty(vm.Entries);
        }
        finally {window.Close();}
    }
    [AvaloniaFact]
    public void HistoryHasAProductionTemplate()
    {
        var vm=new CamperHistoryViewModel(new FakeDatabase());
        var template=Avalonia.Application.Current!.DataTemplates.Single(t=>t.Match(vm));
        Assert.IsType<CamperHistoryView>(template.Build(vm));
    }
}

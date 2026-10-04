using CamperManagement.Models;
using CamperManagement.Tests;
using CamperManagement.ViewModels;
namespace CamperManagement.UnitTests;

public class HistoryTests
{
    private static CamperHistoryEntry Entry(long id = 1, string place = "1")
    {
        var before = Data.Camper(); before.Platznr = place;
        var after = before.Snapshot(); after.Straße = "Neuer Weg 2"; after.Vertragskosten = 125.50m;
        return new() { Id = id, Kind = "updated", RecordedAtUtc = DateTime.UtcNow, Before = new(before, true, new(2024,1,1), null), After = new(after, true, new(2024,1,1), null) };
    }
    [Fact]
    public void DifferencesOnlyShowChangedFieldsWithBothValues()
    {
        var e = Entry();
        Assert.Equal(2, e.Changes.Count);
        var address = Assert.Single(e.Changes, c => c.Field == "Straße");
        Assert.Equal("Testweg 1", address.Before); Assert.Equal("Neuer Weg 2", address.After);
        Assert.Contains(e.Changes, c => c.Field == "Vertragskosten" && c.After == "125,50 €");
        Assert.Contains("Müller", e.Identity); Assert.Contains("Testweg", e.SearchText);
    }
    [Theory]
    [InlineData("Testweg")]
    [InlineData("Neuer Weg")]
    [InlineData("Vertragskosten")]
    public async Task SearchIncludesPastAndCurrentValues(string search)
    {
        var db = new FakeDatabase(); db.History.Add(Entry());
        var vm = new CamperHistoryViewModel(db); await vm.InitializeAsync();
        vm.SearchText = search; Assert.Single(vm.Entries); Assert.True(vm.HasSelection);
        vm.SearchText = "does-not-exist"; Assert.Empty(vm.Entries); Assert.Null(vm.SelectedEntry); Assert.True(vm.IsEmpty);
        vm.SearchText = ""; Assert.Single(vm.Entries);
    }
    [Fact]
    public async Task PlaceFilterAndReloadRetainSelection()
    {
        var db = new FakeDatabase(); db.History.AddRange([Entry(), Entry(2,"2")]);
        var vm = new CamperHistoryViewModel(db); await vm.InitializeAsync(); Assert.Equal(2,vm.Entries.Count);
        vm.SelectedEntry = vm.Entries[1]; await vm.ReloadCommand.ExecuteAsync(null); Assert.Equal(2,vm.SelectedEntry!.Id);
        vm.SelectedPlace = "1"; await vm.SelectionTask; Assert.Single(vm.Entries); Assert.Equal("1",vm.SelectedEntry!.After.Camper.Platznr);
    }
    [Fact]
    public async Task OverlappingLoadsCannotReplaceLatestSelection()
    {
        var pending = new TaskCompletionSource<List<CamperHistoryEntry>>();
        var db = new FakeDatabase(); var vm = new CamperHistoryViewModel(db); await vm.InitializeAsync();
        CancellationToken oldToken = default;
        db.HistoryLoader = (place, token) => { if(place=="1") {oldToken=token;return pending.Task;} return Task.FromResult(new List<CamperHistoryEntry>{Entry(2,"2")}); };
        vm.SelectedPlace = "1"; var previous = vm.SelectionTask;
        vm.SelectedPlace = "2"; await vm.SelectionTask;
        pending.SetResult([Entry()]); await previous;
        Assert.True(oldToken.IsCancellationRequested); Assert.Equal("2", Assert.Single(vm.Entries).After.Camper.Platznr);
    }
    [Fact]
    public async Task FailedLoadCanBeRetriedWithoutShowingAnEmptySuccess()
    {
        var db = new FakeDatabase { HistoryLoader = (_,_) => throw new IOException("test") };
        var vm = new CamperHistoryViewModel(db); await vm.InitializeAsync();
        Assert.True(vm.LoadFailed); Assert.False(vm.IsEmpty);
        db.HistoryLoader = (_,_) => Task.FromResult(new List<CamperHistoryEntry>{Entry()});
        await vm.RetryLoadCommand.ExecuteAsync(null); Assert.False(vm.LoadFailed); Assert.Single(vm.Entries);
    }
    [Fact]
    public async Task DeactivationCancelsPendingHistoryRead()
    {
        var pending = new TaskCompletionSource<List<CamperHistoryEntry>>(); CancellationToken token = default;
        var db = new FakeDatabase { HistoryLoader = (_,t) => {token=t;return pending.Task;} };
        var vm = new CamperHistoryViewModel(db); var load=vm.InitializeAsync(); vm.Deactivate();
        Assert.True(token.IsCancellationRequested); pending.SetResult([Entry()]); await load; Assert.Empty(vm.Entries);
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task HistoryNavigationWorksWithAndWithoutCamperSelection(bool selected)
    {
        var db = new FakeDatabase(); var main = new MainViewModel(db); var tabs=(TabViewModel)main.CurrentView;
        var campers=(CamperViewModel)tabs.CamperView; if(selected) campers.SelectedCamper=Data.Camper();
        campers.HistoryCommand.Execute(null); await main.NavigationTask;
        var vm=Assert.IsType<CamperHistoryViewModel>(main.CurrentView);
        Assert.Equal(selected ? "1" : CamperHistoryViewModel.AllPlaces,vm.SelectedPlace);
        main.NavigateBackCommand.Execute(null); await main.NavigationTask; Assert.Same(tabs,main.CurrentView);
    }
}

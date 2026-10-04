using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CamperManagement.Models;
using CamperManagement.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CamperManagement.ViewModels;

public partial class CamperHistoryViewModel : ViewModelBase
{
    public const string AllPlaces = "Alle Plätze";
    private readonly IDatabaseService _db;
    private List<CamperHistoryEntry> _entries = new();
    private bool _initialized;
    [ObservableProperty] private ObservableCollection<string> places = new() { AllPlaces };
    [ObservableProperty] private string selectedPlace;
    [ObservableProperty] private string searchText = "";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ResultCount))]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private ObservableCollection<CamperHistoryEntry> entries = new();
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private CamperHistoryEntry? selectedEntry;
    public bool HasSelection => SelectedEntry != null;
    public bool IsEmpty => Entries.Count == 0 && !IsLoading && !LoadFailed;
    public string ResultCount => $"{Entries.Count} Historieneinträge";
    public IAsyncRelayCommand ReloadCommand { get; }
    public Task SelectionTask { get; private set; } = Task.CompletedTask;
    public CamperHistoryViewModel(IDatabaseService db, string? place = null, IErrorLog? log = null) : base(log)
    {
        _db = db;
        selectedPlace = place ?? AllPlaces;
        if (selectedPlace != AllPlaces) Places.Add(selectedPlace);
        ReloadCommand = new AsyncRelayCommand(InitializeAsync, () => !IsLoading);
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(IsLoading) or nameof(LoadFailed))
            {
                OnPropertyChanged(nameof(IsEmpty));
                ReloadCommand.NotifyCanExecuteChanged();
            }
        };
    }
    public override Task InitializeAsync() => RunLoadAsync(async token =>
    {
        var place = SelectedPlace;
        var places = await _db.GetPlatznummernAsync(token);
        var rows = await _db.GetCamperHistoryAsync(place == AllPlaces ? null : place, token);
        token.ThrowIfCancellationRequested();
        Places = new(new[] { AllPlaces }.Concat(places));
        _entries = rows;
        _initialized = true;
        Filter();
    });
    public override Task ResumeAsync() => InitializeAsync();
    partial void OnSelectedPlaceChanged(string value)
    {
        if (_initialized) SelectionTask = LoadEntriesAsync();
    }
    partial void OnSearchTextChanged(string value) => Filter();
    private Task LoadEntriesAsync() => RunLoadAsync(async token =>
    {
        var place = SelectedPlace;
        var rows = await _db.GetCamperHistoryAsync(place == AllPlaces ? null : place, token);
        token.ThrowIfCancellationRequested();
        _entries = rows;
        Filter();
    });
    private void Filter()
    {
        var terms = SearchQuery.Parse(SearchText);
        var selection = SelectedEntry?.Id;
        Entries = new(_entries.Where(e => terms.Count == 0 || SearchQuery.MatchesTerms(terms, e.SearchText)));
        SelectedEntry = Entries.FirstOrDefault(e => e.Id == selection) ?? Entries.FirstOrDefault();
    }
}

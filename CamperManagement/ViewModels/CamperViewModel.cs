using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CamperManagement.Models;
using CamperManagement.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
namespace CamperManagement.ViewModels;

public partial class CamperViewModel : ViewModelBase
{
    private readonly MainViewModel _main; private int _loadVersion;
    [ObservableProperty] private ObservableCollection<CamperDisplayModel> camperList = new();
    [ObservableProperty] private ObservableCollection<CamperDisplayModel> filteredCamperList = new();
    [ObservableProperty] private string camperSearchQuery = "";
    public IRelayCommand AddCamperCommand
    {
        get;
    }
    public IRelayCommand PrintCommand
    {
        get;
    }
    public IAsyncRelayCommand PrintAblesetabelleCommand
    {
        get;
    }
    public IRelayCommand<CamperDisplayModel> EditCamperCommand
    {
        get;
    }
    public IAsyncRelayCommand LoadDataCommand
    {
        get;
    }
    public CamperViewModel(MainViewModel main)
    {
        _main = main;
        AddCamperCommand = new RelayCommand(() => _main.NavigateToCommand.Execute(new AddCamperViewModel(_main, _main.Database) { OnSavedAsync = LoadDataAsync }));
        EditCamperCommand = new RelayCommand<CamperDisplayModel>(c => { if (c != null) _main.NavigateToCommand.Execute(new EditCamperViewModel(_main, _main.Database, c) { OnSavedAsync = LoadDataAsync }); });
        PrintCommand = new RelayCommand(() => _main.NavigateToCommand.Execute(new PrintSelectionViewModel(_main, _main.Database)));
        PrintAblesetabelleCommand = new AsyncRelayCommand(() => RunAsync(async () =>
        {
            var rows = await _main.Database.GetAbleseTabelleAsync();
            if (rows.Count == 0)
            {
                StatusMessage = "Keine Ablesedaten vorhanden.";
                return;
            }
            using var file = await _main.Pdf.ReadingsAsync(rows);
            if (file != null && !await _main.Pdf.OpenAsync(file))
                StatusMessage = "PDF gespeichert; der Viewer konnte nicht geöffnet werden.";
        }));
        LoadDataCommand = new AsyncRelayCommand(LoadDataAsync);
    }
    public override Task InitializeAsync() => LoadDataAsync();
    public async Task LoadDataAsync()
    {
        var version = ++_loadVersion;
        try
        {
            var rows = await _main.Database.GetActiveCampersAsync();
            if (version != _loadVersion)
                return;
            CamperList = new(rows);
            Filter();
            StatusMessage = null;
        }
        catch (System.Exception) { if (version == _loadVersion) StatusMessage = "Camper konnten nicht geladen werden. Bitte erneut laden."; }
    }
    partial void OnCamperSearchQueryChanged(string value) => Filter();
    private void Filter() => FilteredCamperList = new(CamperList.Where(c => SearchQuery.Matches(CamperSearchQuery, c.Platznr, c.Anrede, c.Vorname, c.Nachname, c.Straße, c.PLZ, c.Ort, c.Email)));
}

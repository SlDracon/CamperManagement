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
    [ObservableProperty] private CamperDisplayModel? selectedCamper;
    public IRelayCommand EditSelectedCommand { get; }
    public IRelayCommand HistoryCommand { get; }
    public IRelayCommand IncreaseCostCommand { get; }
    partial void OnSelectedCamperChanged(CamperDisplayModel? value)
    {
        EditSelectedCommand?.NotifyCanExecuteChanged();
        IncreaseCostCommand?.NotifyCanExecuteChanged();
    }
    private readonly MainViewModel _main;
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
    public CamperViewModel(MainViewModel main) : base(main.ErrorLog)
    {
        _main = main;
        IncreaseCostCommand = new RelayCommand(() => { if (SelectedCamper != null) _main.NavigateToCommand.Execute(new ContractCostIncreaseViewModel(_main, SelectedCamper)); }, () => SelectedCamper != null && !IsBusy && !IsLoading);
        HistoryCommand = new RelayCommand(() => _main.NavigateToCommand.Execute(new CamperHistoryViewModel(_main.Database, SelectedCamper?.Platznr, Log)), () => !IsBusy);
        EditSelectedCommand = new RelayCommand(() => EditCamperCommand!.Execute(SelectedCamper), () => SelectedCamper != null && !IsBusy && !IsLoading);
        PropertyChanged += (_, e) => { if (e.PropertyName is nameof(IsBusy) or nameof(IsLoading)) { EditSelectedCommand.NotifyCanExecuteChanged(); IncreaseCostCommand.NotifyCanExecuteChanged(); HistoryCommand.NotifyCanExecuteChanged(); LoadDataCommand?.NotifyCanExecuteChanged(); PrintAblesetabelleCommand?.NotifyCanExecuteChanged(); } };
        AddCamperCommand = new RelayCommand(() => _main.NavigateToCommand.Execute(new AddCamperViewModel(_main, _main.Database) { OnSavedAsync = LoadDataAsync }));
        EditCamperCommand = new RelayCommand<CamperDisplayModel>(c => { if (c != null) _main.NavigateToCommand.Execute(new EditCamperViewModel(_main, _main.Database, c) { OnSavedAsync = LoadDataAsync }); });
        PrintCommand = new RelayCommand(() => _main.NavigateToCommand.Execute(new PrintSelectionViewModel(_main, _main.Database)));
        PrintAblesetabelleCommand = new AsyncRelayCommand(() => RunExportAsync(async token =>
        {
            var rows = await _main.Database.GetAbleseTabelleAsync(token);
            if (rows.Count == 0)
            {
                StatusMessage = "Keine Ablesedaten vorhanden.";
                return;
            }
            using var file = await _main.Pdf.ReadingsAsync(rows, token);
            if (file != null && !await _main.Pdf.OpenAsync(file))
                StatusMessage = "PDF gespeichert; der Viewer konnte nicht geöffnet werden.";
        }), () => !IsBusy && !IsLoading);
        LoadDataCommand = new AsyncRelayCommand(LoadDataAsync, () => !IsBusy, AsyncRelayCommandOptions.AllowConcurrentExecutions);
    }
    public override Task ResumeAsync() => LoadDataAsync();
    public override Task InitializeAsync() => LoadDataAsync();
    public Task LoadDataAsync() => RunLoadAsync(async token =>
    {
        var rows = await _main.Database.GetActiveCampersAsync(token);
        token.ThrowIfCancellationRequested();
        CamperList = new(rows);
        SelectedCamper = null;
        Filter();
    });
    partial void OnCamperSearchQueryChanged(string value) => Filter();
    private void Filter()
    {
        var terms = SearchQuery.Parse(CamperSearchQuery);
        FilteredCamperList = new(CamperList.Where(c => SearchQuery.MatchesTerms(terms, c.Platznr, c.Anrede, c.Vorname, c.Nachname, c.ErsterName, c.Straße, c.PLZ, c.Ort, c.Email, c.ZweiterName, c.ZweiteStraße, c.ZweitePLZ, c.ZweiterOrt, c.ZweiteEmail)));
    }
}

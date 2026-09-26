using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CamperManagement.Models;
using CamperManagement.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
namespace CamperManagement.ViewModels;

public partial class RechnungenViewModel : ViewModelBase
{
    [ObservableProperty] private RechnungDisplayModel? selectedRechnung;
    public IRelayCommand EditSelectedCommand { get; }
    partial void OnSelectedRechnungChanged(RechnungDisplayModel? value) => EditSelectedCommand?.NotifyCanExecuteChanged();
    private readonly MainViewModel _main; private int _exportVersion;
    [ObservableProperty] private ObservableCollection<RechnungDisplayModel> rechnungenList = new();
    [ObservableProperty] private ObservableCollection<RechnungDisplayModel> filteredRechnungenList = new();
    [ObservableProperty] private ObservableCollection<RechnungDisplayModel> selectedRechnungen = new();
    [ObservableProperty] private string rechnungSearchQuery = "";
    public IAsyncRelayCommand LoadDataCommand
    {
        get;
    }
    public IRelayCommand OpenAddRechnungCommand
    {
        get;
    }
    public IRelayCommand<RechnungDisplayModel> EditRechnungCommand
    {
        get;
    }
    public IAsyncRelayCommand PrintRechnungCommand
    {
        get;
    }
    public IAsyncRelayCommand CreateRechnungenCommand
    {
        get;
    }
    public IAsyncRelayCommand PrintTabelleCommand
    {
        get;
    }
    public RechnungenViewModel(MainViewModel main) : base(main.ErrorLog)
    {
        _main = main;
        EditSelectedCommand = new RelayCommand(() => EditRechnungCommand!.Execute(SelectedRechnung), () => SelectedRechnung != null && !IsBusy && !IsLoading);
        PropertyChanged += (_, e) => { if (e.PropertyName is nameof(IsBusy) or nameof(IsLoading)) { EditSelectedCommand.NotifyCanExecuteChanged(); LoadDataCommand?.NotifyCanExecuteChanged(); } };
        LoadDataCommand = new AsyncRelayCommand(LoadDataAsync, () => !IsBusy, AsyncRelayCommandOptions.AllowConcurrentExecutions);
        OpenAddRechnungCommand = new RelayCommand(() => _main.NavigateToCommand.Execute(new AddRechnungViewModel(_main, _main.Database) { OnSavedAsync = LoadDataAsync }));
        EditRechnungCommand = new RelayCommand<RechnungDisplayModel>(r => { if (r != null) _main.NavigateToCommand.Execute(new EditRechnungViewModel(_main, _main.Database, r) { OnSavedAsync = LoadDataAsync }); });
        PrintRechnungCommand = new AsyncRelayCommand(() => ExportAsync(false), CanExport);
        CreateRechnungenCommand = new AsyncRelayCommand(() => ExportAsync(true), CanExport);
        PrintTabelleCommand = new AsyncRelayCommand(() => RunExportAsync(async token => { var rows = FilteredRechnungenList.Select(r => r.Snapshot()).ToList(); if (rows.Count == 0) return; using var file = await _main.Pdf.TableAsync(rows, token); if (file != null && !await _main.Pdf.OpenAsync(file)) StatusMessage = "PDF gespeichert; Viewer nicht verfügbar."; }), () => !IsBusy && !IsLoading);
        SelectedRechnungen.CollectionChanged += SelectionChanged;
        PropertyChanged += (_, e) => { if (e.PropertyName is nameof(IsBusy) or nameof(IsLoading)) NotifyExport(); };
    }
    partial void OnSelectedRechnungenChanging(ObservableCollection<RechnungDisplayModel> value)
    {
        if (selectedRechnungen != null)
            selectedRechnungen.CollectionChanged -= SelectionChanged;
    }
    partial void OnSelectedRechnungenChanged(ObservableCollection<RechnungDisplayModel> value)
    {
        if (value != null)
            value.CollectionChanged += SelectionChanged;
        NotifyExport();
    }
    private void SelectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => NotifyExport();
    private bool CanExport() => !IsBusy && !IsLoading && SelectedRechnungen?.Count > 0;
    private void NotifyExport()
    {
        PrintRechnungCommand?.NotifyCanExecuteChanged();
        CreateRechnungenCommand?.NotifyCanExecuteChanged();
        PrintTabelleCommand?.NotifyCanExecuteChanged();
    }
    public override Task ResumeAsync() => LoadDataAsync();
    public override Task InitializeAsync() => LoadDataAsync();
    public Task LoadDataAsync() => RunLoadAsync(async token =>
    {
        var rows = await _main.Database.GetRechnungenAsync(token);
        token.ThrowIfCancellationRequested();
        RechnungenList = new(rows);
        SelectedRechnungen.Clear();
        SelectedRechnung = null;
        Filter();
    });
    partial void OnRechnungSearchQueryChanged(string value) => Filter();
    private void Filter()
    {
        var terms = SearchQuery.Parse(RechnungSearchQuery);
        var de = CultureInfo.GetCultureInfo("de-DE");
        FilteredRechnungenList = new(RechnungenList.Where(r =>
        {
        return SearchQuery.MatchesTerms(terms, r.Id.ToString(), r.Platznr, r.Art, r.Gedruckt, r.Jahr.ToString(), r.Alt.ToString(de), r.Neu.ToString(de), r.Verbrauch.ToString(de), r.Faktor.ToString(de), r.Betrag.ToString(de), r.Alt.ToString(CultureInfo.InvariantCulture), r.Neu.ToString(CultureInfo.InvariantCulture), r.Verbrauch.ToString(CultureInfo.InvariantCulture), r.Faktor.ToString(CultureInfo.InvariantCulture), r.Betrag.ToString(CultureInfo.InvariantCulture));
        }));
    }
    private async Task ExportAsync(bool grouped)
    {
        if (!CanExport())
            return;
        var snapshot = SelectedRechnungen.Select(r => r.Snapshot()).ToList();
        await RunExportAsync(async token =>
        {
            if (snapshot.Any(r => !r.RecipientResolved))
                throw new InvalidOperationException("Bei mindestens einer Rechnung muss zuerst der historische Empfänger zugeordnet werden.");
            var version = ++_exportVersion;
            var progress = new Progress<string?>(text => { if (version == _exportVersion) StatusMessage = text; });
            IPdfFile? file = null;
            bool saved;
            try
            {
                saved = grouped ? await _main.Pdf.ByPlatzAsync(snapshot, progress, token) : (file = await _main.Pdf.InvoicesAsync(snapshot, progress, token)) != null;
                token.ThrowIfCancellationRequested();
                ++_exportVersion;
                if (!saved)
                {
                    StatusMessage = "Export abgebrochen. Druckstatus unverändert.";
                    return;
                }
                try
                {
                    CompleteExportCancellation();
                    await _main.Database.MarkRechnungenAsPrintedAsync(snapshot.Select(r => r.Id).ToArray());
                }
                catch (Exception ex) { Log.Write(ErrorOperation.Save, ex); StatusMessage = "PDFs gespeichert, aber der Druckstatus konnte nicht gespeichert werden. Bitte erneut versuchen."; return; }
                await LoadDataAsync();
                ShowTemporaryStatus("Alle ausgewählten Rechnungen wurden gespeichert und als gedruckt markiert.");
                if (file != null && !await _main.Pdf.OpenAsync(file))
                    StatusMessage = "PDF gespeichert und als gedruckt markiert; Viewer nicht verfügbar.";
            }
            finally { ++_exportVersion; file?.Dispose(); }
        });
    }
}

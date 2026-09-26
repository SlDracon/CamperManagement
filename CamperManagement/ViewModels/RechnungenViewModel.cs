using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using CamperManagement.Models;
using CamperManagement.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
namespace CamperManagement.ViewModels;

public partial class RechnungenViewModel : ViewModelBase
{
    private readonly MainViewModel _main; private int _loadVersion, _exportVersion;
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
    public RechnungenViewModel(MainViewModel main)
    {
        _main = main;
        LoadDataCommand = new AsyncRelayCommand(LoadDataAsync);
        OpenAddRechnungCommand = new RelayCommand(() => _main.NavigateToCommand.Execute(new AddRechnungViewModel(_main, _main.Database) { OnSavedAsync = LoadDataAsync }));
        EditRechnungCommand = new RelayCommand<RechnungDisplayModel>(r => { if (r != null) _main.NavigateToCommand.Execute(new EditRechnungViewModel(_main, _main.Database, r) { OnSavedAsync = LoadDataAsync }); });
        PrintRechnungCommand = new AsyncRelayCommand(() => ExportAsync(false), CanExport);
        CreateRechnungenCommand = new AsyncRelayCommand(() => ExportAsync(true), CanExport);
        PrintTabelleCommand = new AsyncRelayCommand(() => RunAsync(async () => { var rows = FilteredRechnungenList.Select(r => r.Snapshot()).ToList(); if (rows.Count == 0) return; using var file = await _main.Pdf.TableAsync(rows); if (file != null && !await _main.Pdf.OpenAsync(file)) StatusMessage = "PDF gespeichert; Viewer nicht verfügbar."; }), () => !IsBusy);
        SelectedRechnungen.CollectionChanged += SelectionChanged;
        PropertyChanged += (_, e) => { if (e.PropertyName == nameof(IsBusy)) NotifyExport(); };
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
    private bool CanExport() => !IsBusy && SelectedRechnungen?.Count > 0;
    private void NotifyExport()
    {
        PrintRechnungCommand?.NotifyCanExecuteChanged();
        CreateRechnungenCommand?.NotifyCanExecuteChanged();
        PrintTabelleCommand?.NotifyCanExecuteChanged();
    }
    public override Task InitializeAsync() => LoadDataAsync();
    public async Task LoadDataAsync()
    {
        var version = ++_loadVersion;
        try
        {
            var rows = await _main.Database.GetRechnungenAsync();
            if (version != _loadVersion)
                return;
            RechnungenList = new(rows);
            SelectedRechnungen.Clear();
            Filter();
            StatusMessage = null;
        }
        catch (Exception) { if (version == _loadVersion) StatusMessage = "Rechnungen konnten nicht geladen werden. Bitte erneut laden."; }
    }
    partial void OnRechnungSearchQueryChanged(string value) => Filter();
    private void Filter() => FilteredRechnungenList = new(RechnungenList.Where(r =>
    {
        var de = CultureInfo.GetCultureInfo("de-DE");
        return SearchQuery.Matches(RechnungSearchQuery, r.Id.ToString(), r.Platznr, r.Art, r.Gedruckt, r.Jahr.ToString(), r.Alt.ToString(de), r.Neu.ToString(de), r.Verbrauch.ToString(de), r.Faktor.ToString(de), r.Betrag.ToString(de), r.Alt.ToString(CultureInfo.InvariantCulture), r.Neu.ToString(CultureInfo.InvariantCulture), r.Verbrauch.ToString(CultureInfo.InvariantCulture), r.Faktor.ToString(CultureInfo.InvariantCulture), r.Betrag.ToString(CultureInfo.InvariantCulture));
    }));
    private async Task ExportAsync(bool grouped)
    {
        if (!CanExport())
            return;
        var snapshot = SelectedRechnungen.Select(r => r.Snapshot()).ToList();
        await RunAsync(async () =>
        {
            if (snapshot.Any(r => !r.RecipientResolved))
                throw new InvalidOperationException("Bei mindestens einer Rechnung muss zuerst der historische Empfänger zugeordnet werden.");
            var version = ++_exportVersion;
            var progress = new Progress<string?>(text => { if (version == _exportVersion) StatusMessage = text; });
            IStorageFile? file = null;
            bool saved;
            try
            {
                saved = grouped ? await _main.Pdf.ByPlatzAsync(snapshot, progress) : (file = await _main.Pdf.InvoicesAsync(snapshot, progress)) != null;
                ++_exportVersion;
                if (!saved)
                {
                    StatusMessage = "Export abgebrochen. Druckstatus unverändert.";
                    return;
                }
                try
                {
                    await _main.Database.MarkRechnungenAsPrintedAsync(snapshot.Select(r => r.Id).ToArray());
                }
                catch (Exception) { StatusMessage = "PDFs gespeichert, aber der Druckstatus konnte nicht gespeichert werden. Bitte erneut versuchen."; return; }
                await LoadDataAsync();
                StatusMessage = "Alle ausgewählten Rechnungen wurden gespeichert und als gedruckt markiert.";
                if (file != null && !await _main.Pdf.OpenAsync(file))
                    StatusMessage = "PDF gespeichert und als gedruckt markiert; Viewer nicht verfügbar.";
            }
            finally { ++_exportVersion; file?.Dispose(); }
        });
    }
}

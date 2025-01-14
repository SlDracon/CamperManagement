using CamperManagement.Models;
using CamperManagement.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.Generic;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using CamperManagement.Views;
using Avalonia.Controls;

namespace CamperManagement.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly DatabaseService _dbService;

    [ObservableProperty]
    private ObservableCollection<CamperDisplayModel> camperList = new();

    [ObservableProperty]
    private ObservableCollection<CamperDisplayModel> filteredCamperList = new();

    [ObservableProperty]
    private string camperSearchQuery = string.Empty;

    public MainViewModel()
    {
        _dbService = new DatabaseService();

        AddCamperCommand = new RelayCommand(OpenAddCamperDialog);
        PrintCommand = new RelayCommand(OpenPrintDialog);
        PrintAblesetabelleCommand = new AsyncRelayCommand(PrintAblesetabelleAsync);

        // Initialisiere die gefilterte Liste
        FilteredCamperList = new ObservableCollection<CamperDisplayModel>();

        // Lade die Daten
        _ = LoadDataAsync();
    }

    public IRelayCommand AddCamperCommand { get; }
    public IRelayCommand PrintCommand { get; }
    public IRelayCommand PrintAblesetabelleCommand { get; }

    public async Task LoadDataAsync()
    {
        CamperList.Clear();
        var campers = await _dbService.GetActiveCampersAsync();
        foreach (var camper in campers)
        {
            CamperList.Add(camper);
        }

        // Initiale Filterung
        FilterCamperList();
    }

    private async void OpenAddCamperDialog()
    {
        var viewModel = new AddCamperViewModel(new DatabaseService());
        var addCamperWindow = new AddCamperWindow(viewModel);

        await addCamperWindow.ShowDialog(Avalonia.Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop ? desktop.MainWindow : null);

        // Aktualisiere die Camperliste nach dem Speichern
        await LoadDataAsync();
    }

    partial void OnCamperSearchQueryChanged(string value)
    {
        FilterCamperList();
    }

    private void FilterCamperList()
    {
        if (string.IsNullOrWhiteSpace(CamperSearchQuery))
        {
            FilteredCamperList = new ObservableCollection<CamperDisplayModel>(CamperList);
        }
        else
        {
            var searchTerms = ParseSearchQuery(CamperSearchQuery);

            var filtered = CamperList.Where(camper =>
                searchTerms.All(term =>
                    camper.Platznr.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    camper.Anrede.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    camper.Vorname.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    camper.Nachname.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    camper.Straße.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    camper.PLZ.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    camper.Ort.Contains(term, StringComparison.OrdinalIgnoreCase)
                )
            );

            FilteredCamperList = new ObservableCollection<CamperDisplayModel>(filtered);
        }
    }

    private IEnumerable<string> ParseSearchQuery(string query)
    {
        var terms = new List<string>();
        var inQuotes = false;
        var currentTerm = string.Empty;

        foreach (var c in query)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
                if (!inQuotes && !string.IsNullOrEmpty(currentTerm))
                {
                    terms.Add(currentTerm);
                    currentTerm = string.Empty;
                }
            }
            else if (char.IsWhiteSpace(c) && !inQuotes)
            {
                if (!string.IsNullOrEmpty(currentTerm))
                {
                    terms.Add(currentTerm);
                    currentTerm = string.Empty;
                }
            }
            else
            {
                currentTerm += c;
            }
        }

        if (!string.IsNullOrEmpty(currentTerm))
        {
            terms.Add(currentTerm);
        }

        return terms;
    }

    private async void OpenPrintDialog()
    {
        var printSelectionViewModel = new PrintSelectionViewModel(new DatabaseService());
        var printWindow = new PrintSelectionWindow(printSelectionViewModel);

        await printWindow.ShowDialog(Avalonia.Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop ? desktop.MainWindow : null);
    }

    private async Task PrintAblesetabelleAsync()
    {
        var ableseEintraege = await _dbService.GetAbleseTabelleAsync();

        if (ableseEintraege.Count == 0)
        {
            return;
        }

        var pdfPath = await PdfService.GenerateAbleseTabellePdfAsync(ableseEintraege);
        PdfService.OpenPdf(pdfPath);
    }
}

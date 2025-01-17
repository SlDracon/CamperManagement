using CamperManagement.Models;
using CamperManagement.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.Generic;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using CamperManagement.Views;

namespace CamperManagement.ViewModels;

public partial class RechnungenViewModel : ObservableObject
{
    private readonly DatabaseService _dbService;

    [ObservableProperty]
    private ObservableCollection<RechnungDisplayModel> rechnungenList = new();

    [ObservableProperty]
    private ObservableCollection<RechnungDisplayModel> filteredRechnungenList = new();

    [ObservableProperty]
    private string rechnungSearchQuery = string.Empty;

    [ObservableProperty]
    private ObservableCollection<RechnungDisplayModel> selectedRechnungen = new();

    [ObservableProperty]
    private string statusMessage;

    public RechnungenViewModel()
    {
        _dbService = new DatabaseService();
        LoadDataCommand = new AsyncRelayCommand(LoadDataAsync);
        EditRechnungCommand = new RelayCommand<RechnungDisplayModel>(OpenEditRechnungDialog);
        OpenAddRechnungCommand = new RelayCommand(OpenAddRechnungDialog);
        PrintRechnungCommand = new AsyncRelayCommand(
            PrintRechnungAsync,
            () => SelectedRechnungen.Any()
        );

        // Beobachte Änderungen an der Auswahl
        SelectedRechnungen.CollectionChanged += (_, _) =>
        {
            PrintRechnungCommand.NotifyCanExecuteChanged();
        };
        PrintTabelleCommand = new AsyncRelayCommand(PrintTabelleAsync);

        // Initialisiere die gefilterte Liste
        FilteredRechnungenList = new ObservableCollection<RechnungDisplayModel>();

        // Lade die Daten
        _ = LoadDataAsync();
    }

    public IAsyncRelayCommand LoadDataCommand { get; }
    public IRelayCommand OpenAddRechnungCommand { get; }
    public IRelayCommand PrintRechnungCommand { get; }
    public IRelayCommand PrintTabelleCommand { get; }
    public IRelayCommand<RechnungDisplayModel> EditRechnungCommand { get; }

    private async Task LoadDataAsync()
    {
        RechnungenList.Clear();
        var rechnungen = await _dbService.GetRechnungenAsync();
        foreach (var rechnung in rechnungen)
        {
            RechnungenList.Add(rechnung);
        }

        // Initiale Filterung
        FilterRechnungenList();
    }

    partial void OnRechnungSearchQueryChanged(string value)
    {
        FilterRechnungenList();
    }

    private void FilterRechnungenList()
    {
        if (string.IsNullOrWhiteSpace(RechnungSearchQuery))
        {
            FilteredRechnungenList = new ObservableCollection<RechnungDisplayModel>(RechnungenList);
        }
        else
        {
            var searchTerms = ParseSearchQuery(RechnungSearchQuery);

            var filtered = RechnungenList.Where(rechnung =>
                searchTerms.All(term =>
                    rechnung.Id.ToString().Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    rechnung.Platznr.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    rechnung.Alt.ToString().Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    rechnung.Neu.ToString().Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    rechnung.Verbrauch.ToString().Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    rechnung.Faktor.ToString().Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    rechnung.Betrag.ToString().Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    rechnung.Jahr.ToString().Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    rechnung.Art.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    rechnung.Gedruckt.Contains(term, StringComparison.OrdinalIgnoreCase)
                )
            );

            FilteredRechnungenList = new ObservableCollection<RechnungDisplayModel>(filtered);
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

    private void OpenAddRechnungDialog()
    {
        var viewModel = new AddRechnungViewModel(new DatabaseService())
        {
            // Füge die neue Rechnung direkt zur Liste hinzu
            OnRechnungAdded = () =>
            {
                _ = LoadDataAsync();
            }
        };
        var addRechnungWindow = new AddRechnungWindow(viewModel);

        addRechnungWindow.ShowDialog(Avalonia.Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop ? desktop.MainWindow : null);
    }

    private async Task PrintTabelleAsync()
    {
        // Hole die Daten aus der gefilterten Liste
        var filteredRechnungen = FilteredRechnungenList;

        if (filteredRechnungen.Count == 0)
        {
            // Keine Daten verfügbar
            return;
        }

        // Erstelle die PDF
        var pdfPath = await PdfService.GenerateTabellePdfAsync(filteredRechnungen);

        // Öffne die PDF
        PdfService.OpenPdf(pdfPath);
    }
    
    private async Task PrintRechnungAsync()
    {
        if (SelectedRechnungen == null || !SelectedRechnungen.Any())
        {
            return;
        }

        // Pfade zu den Vorlagen und dem Ziel
        var exePath = AppDomain.CurrentDomain.BaseDirectory;
        var wasserTemplatePath = Path.Combine(exePath, "rechnung_wasser.docx");
        var stromTemplatePath = Path.Combine(exePath, "rechnung_strom.docx");
        var vorlagePath = Path.Combine(exePath, "rechnung_vorlage.docx");
        var outputPdfPath = Path.Combine(Path.GetTempPath(), $"Rechnungen_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");

        try
        {
            StatusMessage = "Rechnungen werden generiert...";

            // Starte den Prozess in einem separaten Thread
            await Task.Run(async () =>
            {
                var tempWordFiles = await WordService.GenerateWordFilesForRechnungenAsync(
                    SelectedRechnungen,
                    wasserTemplatePath,
                    stromTemplatePath,
                    message => StatusMessage = message);

                StatusMessage = "Rechnungen werden zusammengeführt und als PDF gespeichert...";
                WordService.MergeWordDocuments(tempWordFiles, vorlagePath, outputPdfPath);
            });

            // Markiere die Rechnungen als gedruckt in der Datenbank
            foreach (var rechnung in SelectedRechnungen)
            {
                await _dbService.MarkRechnungAsPrintedAsync(rechnung.Id);
            }

            StatusMessage = "PDF erfolgreich erstellt.";
            PdfService.OpenPdf(outputPdfPath);
            StatusMessage = "";

            // Aktualisiere die Rechnungsliste
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = "Fehler beim Drucken der Rechnungen";
        }
    }


    private void OpenEditRechnungDialog(RechnungDisplayModel rechnung)
    {
        if (rechnung == null) return;

        var viewModel = new EditRechnungViewModel(new DatabaseService(), rechnung)
        {
            // Füge die neue Rechnung direkt zur Liste hinzu
            OnRechnungChanged = () =>
            {
                _ = LoadDataAsync();
            }
        };

        var editRechnungWindow = new EditRechnungWindow(viewModel);

        viewModel.CloseAction = () => editRechnungWindow.Close();
        editRechnungWindow.ShowDialog(Avalonia.Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop ? desktop.MainWindow : null);
    }
}

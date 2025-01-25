using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CamperManagement.Models;
using CamperManagement.Services;
using CamperManagement.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using iText.Kernel.Pdf;
using Microsoft.Extensions.Logging;

namespace CamperManagement.ViewModels;

public partial class RechnungenViewModel : ObservableObject
{
    private readonly MainViewModel _mainViewModel;
    private readonly DatabaseService _dbService;
    private TopLevel? _toplevel;

    [ObservableProperty] private ObservableCollection<RechnungDisplayModel> filteredRechnungenList = new();

    [ObservableProperty] private ObservableCollection<RechnungDisplayModel> rechnungenList = new();

    [ObservableProperty] private string rechnungSearchQuery = string.Empty;

    [ObservableProperty] private ObservableCollection<RechnungDisplayModel> selectedRechnungen = new();

    [ObservableProperty] private string? statusMessage;

    public RechnungenViewModel(MainViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;
        _dbService = new DatabaseService();
        LoadDataCommand = new AsyncRelayCommand(LoadDataAsync);
        EditRechnungCommand = new RelayCommand<RechnungDisplayModel>(OpenEditRechnungDialog!);
        OpenAddRechnungCommand = new RelayCommand(OpenAddRechnungDialog);
        PrintRechnungCommand = new AsyncRelayCommand(
            PrintRechnungAsync,
            () => SelectedRechnungen.Any()
        );

        // Beobachte Änderungen an der Auswahl
        SelectedRechnungen.CollectionChanged += (_, _) => { PrintRechnungCommand.NotifyCanExecuteChanged(); };
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

    public async Task LoadDataAsync()
    {
        RechnungenList.Clear();
        var rechnungen = await _dbService.GetRechnungenAsync();
        foreach (var rechnung in rechnungen) RechnungenList.Add(rechnung);

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
                    rechnung is { Platznr: not null, Art: not null, Gedruckt: not null } && (rechnung.Id.ToString().Contains(term, StringComparison.OrdinalIgnoreCase) ||
                        rechnung.Platznr.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                        rechnung.Alt.ToString(CultureInfo.CurrentCulture).Contains(term, StringComparison.OrdinalIgnoreCase) ||
                        rechnung.Neu.ToString(CultureInfo.CurrentCulture).Contains(term, StringComparison.OrdinalIgnoreCase) ||
                        rechnung.Verbrauch.ToString(CultureInfo.CurrentCulture).Contains(term, StringComparison.OrdinalIgnoreCase) ||
                        rechnung.Faktor.ToString(CultureInfo.CurrentCulture).Contains(term, StringComparison.OrdinalIgnoreCase) ||
                        rechnung.Betrag.ToString(CultureInfo.CurrentCulture).Contains(term, StringComparison.OrdinalIgnoreCase) ||
                        rechnung.Jahr.ToString().Contains(term, StringComparison.OrdinalIgnoreCase) ||
                        rechnung.Art.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                        rechnung.Gedruckt.Contains(term, StringComparison.OrdinalIgnoreCase))
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
            if (c == '"')
            {
                inQuotes = !inQuotes;
                if (inQuotes || string.IsNullOrEmpty(currentTerm)) continue;
                terms.Add(currentTerm);
                currentTerm = string.Empty;
            }
            else if (char.IsWhiteSpace(c) && !inQuotes)
            {
                if (string.IsNullOrEmpty(currentTerm)) continue;
                terms.Add(currentTerm);
                currentTerm = string.Empty;
            }
            else
            {
                currentTerm += c;
            }

        if (!string.IsNullOrEmpty(currentTerm)) terms.Add(currentTerm);

        return terms;
    }

    private void OpenAddRechnungDialog()
    {
        _mainViewModel.NavigateToCommand.Execute(new AddRechnungViewModel(_mainViewModel, new DatabaseService())
        {
            // Füge die neue Rechnung direkt zur Liste hinzu
            OnRechnungAdded = () => { _ = LoadDataAsync(); }
        });
    }

    private async Task PrintTabelleAsync()
    {
        // Hole die Daten aus der gefilterten Liste
        var filteredRechnungen = FilteredRechnungenList;

        if (filteredRechnungen.Count == 0)
            // Keine Daten verfügbar
            return;


        switch (Avalonia.Application.Current?.ApplicationLifetime)
        {
            case IClassicDesktopStyleApplicationLifetime desktop:
                // Desktop: Hauptfenster verwenden
                _toplevel = desktop.MainWindow;
                break;
            case ISingleViewApplicationLifetime singleViewPlatform:
            {
                // Android: Hauptansicht verwenden
                var mainView = singleViewPlatform.MainView;

                if (mainView != null)
                {
                    // TopLevel aus der Hauptansicht extrahieren
                    _toplevel = TopLevel.GetTopLevel(mainView);
                }
                else
                {
                    Console.WriteLine("MainView konnte nicht gefunden werden.");
                }

                break;
            }
            default:
                Console.WriteLine("Unbekannte Plattform oder ApplicationLifetime.");
                break;
        }

        // Erstelle die PDF
        var pdfPath = await PdfService.GenerateTabellePdfAsync(_toplevel, filteredRechnungen);

        // Öffne die PDF
        if (_toplevel != null) await PdfService.OpenPdfAsync(pdfPath, _toplevel);
    }

    private async Task PrintRechnungAsync()
    {
        if (SelectedRechnungen == null || !SelectedRechnungen.Any())
            return;

        try
        {
            StatusMessage = "Rechnungen werden generiert...";

            switch (Avalonia.Application.Current?.ApplicationLifetime)
            {
                case IClassicDesktopStyleApplicationLifetime desktop:
                    // Desktop: Hauptfenster verwenden
                    _toplevel = desktop.MainWindow;
                    break;
                case ISingleViewApplicationLifetime singleViewPlatform:
                {
                    // Android: Hauptansicht verwenden
                    var mainView = singleViewPlatform.MainView;

                    if (mainView != null)
                    {
                        // TopLevel aus der Hauptansicht extrahieren
                        _toplevel = TopLevel.GetTopLevel(mainView);
                    }
                    else
                    {
                        Console.WriteLine("MainView konnte nicht gefunden werden.");
                    }

                    break;
                }
                default:
                    Console.WriteLine("Unbekannte Plattform oder ApplicationLifetime.");
                    break;
            }

            // Starte den Prozess in einem separaten Thread
            var pdfPath = await Task.Run(async () =>
            {
                // Übergib die Rechnungen an den PdfService
                return await PdfService.GenerateAndMergeRechnungenAsync(
                    _toplevel,
                    SelectedRechnungen,
                    status => StatusMessage = status // Aktualisiere Statusnachrichten
                );
            });

            // Markiere die Rechnungen als gedruckt in der Datenbank
            foreach (var rechnung in SelectedRechnungen)
            {
                await _dbService.MarkRechnungAsPrintedAsync(rechnung.Id);
            }

            StatusMessage = "PDF erfolgreich erstellt.";
            await PdfService.OpenPdfAsync(pdfPath, _toplevel);

            StatusMessage = "";

            // Aktualisiere die Rechnungsliste
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = "Fehler beim Drucken der Rechnungen";
            Console.WriteLine(ex);
        }
    }

    private void OpenEditRechnungDialog(RechnungDisplayModel rechnung)
    {
        if (rechnung == null) return;

        _mainViewModel.NavigateToCommand.Execute(new EditRechnungViewModel(_mainViewModel, new DatabaseService(), rechnung)
        {
            // Füge die neue Rechnung direkt zur Liste hinzu
            OnRechnungChanged = () => { _ = LoadDataAsync(); }
        });
    }
}
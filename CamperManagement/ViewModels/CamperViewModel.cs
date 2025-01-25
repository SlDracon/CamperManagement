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

namespace CamperManagement.ViewModels
{
    public partial class CamperViewModel : ViewModelBase
    {
        private readonly MainViewModel _mainViewModel;
        private readonly DatabaseService _dbService;
        private TopLevel? _toplevel;

        [ObservableProperty]
        private ObservableCollection<CamperDisplayModel> camperList = new();

        [ObservableProperty]
        private ObservableCollection<CamperDisplayModel> filteredCamperList = new();

        [ObservableProperty]
        private string camperSearchQuery = string.Empty;

        public CamperViewModel(MainViewModel mainViewModel)
        {
            _mainViewModel = mainViewModel;
            _dbService = new DatabaseService();

            AddCamperCommand = new AsyncRelayCommand(OpenAddCamperDialog);
            PrintCommand = new RelayCommand(OpenPrintDialog);
            PrintAblesetabelleCommand = new AsyncRelayCommand(PrintAblesetabelleAsync);
            EditCamperCommand = new RelayCommand<CamperDisplayModel>(OpenEditCamperDialog!);

            // Initialisiere die gefilterte Liste
            FilteredCamperList = new ObservableCollection<CamperDisplayModel>();

            // Lade die Daten
            _ = LoadDataAsync();
        }

        public IAsyncRelayCommand AddCamperCommand { get; }
        public IRelayCommand PrintCommand { get; }
        public IRelayCommand PrintAblesetabelleCommand { get; }
        public IRelayCommand<CamperDisplayModel> EditCamperCommand { get; }

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

        private async Task OpenAddCamperDialog()
        {
            _mainViewModel.NavigateToCommand.Execute(new AddCamperViewModel(_mainViewModel, new DatabaseService()));

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
                        camper is { Platznr: not null, Anrede: not null, Vorname: not null, Nachname: not null, Straße: not null, PLZ: not null, Ort: not null } && (camper.Platznr.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                            camper.Anrede.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                            camper.Vorname.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                            camper.Nachname.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                            camper.Straße.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                            camper.PLZ.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                            camper.Ort.Contains(term, StringComparison.OrdinalIgnoreCase))
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

        private void OpenPrintDialog()
        {
            _mainViewModel.NavigateToCommand.Execute(new PrintSelectionViewModel(_mainViewModel, new DatabaseService()));
        }

        private async Task PrintAblesetabelleAsync()
        {
            var ableseEintraege = await _dbService.GetAbleseTabelleAsync();

            if (ableseEintraege.Count == 0)
            {
                return;
            }

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

            var pdfPath = await PdfService.GenerateAbleseTabellePdfAsync(_toplevel, ableseEintraege);
            await PdfService.OpenPdfAsync(pdfPath, _toplevel);
        }

        private void OpenEditCamperDialog(CamperDisplayModel camper)
        {
            if (camper == null) return;

            _mainViewModel.NavigateToCommand.Execute(new EditCamperViewModel(_mainViewModel, new DatabaseService(), camper)
            {
                OnCamperChanged = () => { _ = LoadDataAsync(); }
            });
        }
    }
}

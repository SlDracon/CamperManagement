using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CamperManagement.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace CamperManagement.ViewModels
{
    public partial class PrintSelectionViewModel : ObservableObject
    {
        private readonly MainViewModel _mainViewModel;
        public ObservableCollection<int> Jahre { get; } = new();
        private readonly TopLevel? _toplevel;

        [ObservableProperty]
        private int selectedJahr;

        public IAsyncRelayCommand PrintCommand { get; }
        public IAsyncRelayCommand CancelCommand { get; }

        public PrintSelectionViewModel(MainViewModel mainViewModel, DatabaseService dbService)
        {
            _mainViewModel = mainViewModel;
            _ = LoadJahreAsync(dbService);

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

            PrintCommand = new AsyncRelayCommand(async () =>
            {
                // Rechnungsdaten laden
                var kostenEintraege = await dbService.GetRechnungenForJahrAsync(SelectedJahr);

                if (kostenEintraege == null || kostenEintraege.Count == 0)
                {
                    await ShowErrorMessageAsync("Keine Daten für das ausgewählte Jahr gefunden.");
                    return;
                }

                // PDF generieren
                var pdfPath = await PdfService.GenerateKostenPdfAsync(_toplevel, SelectedJahr, kostenEintraege);

                // Öffne die PDF im Standard-PDF-Viewer
                if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    // Desktop: Hauptfenster verwenden
                    var mainWindow = desktop.MainWindow;

                    if (mainWindow != null)
                    {
                        await PdfService.OpenPdfAsync(pdfPath, mainWindow);
                    }
                    else
                    {
                        Console.WriteLine("MainWindow konnte nicht gefunden werden.");
                    }
                }
                else if (Avalonia.Application.Current?.ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
                {
                    // Android: Hauptansicht verwenden
                    var mainView = singleViewPlatform.MainView;

                    if (mainView != null)
                    {
                        // TopLevel aus der Hauptansicht extrahieren
                        var topLevel = TopLevel.GetTopLevel(mainView);

                        if (topLevel != null)
                        {
                            await PdfService.OpenPdfAsync(pdfPath, topLevel);
                        }
                        else
                        {
                            Console.WriteLine("TopLevel konnte nicht abgerufen werden.");
                        }
                    }
                    else
                    {
                        Console.WriteLine("MainView konnte nicht gefunden werden.");
                    }
                }
                else
                {
                    Console.WriteLine("Unbekannte Plattform oder ApplicationLifetime.");
                }


                // Fenster nach Abschluss schließen (falls benötigt)
                await Cancel();
            });
            CancelCommand = new AsyncRelayCommand(Cancel);
        }

        private Task ShowErrorMessageAsync(string message)
        {
            // Hier kannst du eine Benutzerbenachrichtigung anzeigen
            Console.WriteLine(message); // Debug-Ausgabe
            return Task.CompletedTask;
        }

        private async Task LoadJahreAsync(DatabaseService dbService)
        {
            var jahre = await dbService.GetAvailableJahreAsync();
            Jahre.Clear();
            foreach (var jahr in jahre)
            {
                Jahre.Add(jahr);
            }

            // Neuestes Jahr standardmäßig auswählen
            if (Jahre.Count > 0)
            {
                SelectedJahr = Jahre[0];
            }
        }

        private Task Cancel()
        {
            // Rufe NavigateBackCommand aus MainViewModel auf
            if (_mainViewModel.CanNavigateBack)
            {
                _mainViewModel.NavigateBackCommand.Execute(null);
            }

            return Task.CompletedTask;
        }
    }
}

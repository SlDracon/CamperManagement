using Avalonia.Controls;
using CamperManagement.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CamperManagement.ViewModels
{
    public partial class PrintSelectionViewModel : ObservableObject
    {
        public ObservableCollection<int> Jahre { get; } = new();

        [ObservableProperty]
        private int selectedJahr;

        public IAsyncRelayCommand PrintCommand { get; }

        public PrintSelectionViewModel(DatabaseService dbService)
        {
            LoadJahreAsync(dbService);

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
                var pdfPath = await PdfService.GenerateKostenPdfAsync(SelectedJahr, kostenEintraege);

                // Öffne die PDF im Standard-PDF-Viewer
                PdfService.OpenPdf(pdfPath);

                // Fenster nach Abschluss schließen (falls benötigt)
                CloseAction?.Invoke();
            });
        }

        private async Task ShowErrorMessageAsync(string message)
        {
            // Hier kannst du eine Benutzerbenachrichtigung anzeigen
            Console.WriteLine(message); // Debug-Ausgabe
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

        public Action? CloseAction { get; set; }
    }
}

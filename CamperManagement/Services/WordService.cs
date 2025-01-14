using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CamperManagement.Models;
using System.Threading.Tasks;
using Microsoft.Office.Interop.Word;
using Document = Microsoft.Office.Interop.Word.Document;

namespace CamperManagement.Services
{
    public static class WordService
    {
        public static async Task<List<string>> GenerateWordFilesForRechnungenAsync(
            IEnumerable<RechnungDisplayModel> rechnungen,
            string wasserTemplatePath,
            string stromTemplatePath,
            Action<string> updateStatusMessage)
        {
            var tempFiles = new List<string>();
            int total = rechnungen.Count();
            int current = 0;

            foreach (var rechnung in rechnungen)
            {
                current++;
                updateStatusMessage?.Invoke($"Rechnungen werden generiert ({current}/{total})...");

                var templatePath = rechnung.Art == "Wasser" ? wasserTemplatePath : stromTemplatePath;
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var randomPart = Guid.NewGuid().ToString("N").Substring(0, 8); // 8 Zeichen zufälliger Teil
                var tempFilePath = Path.Combine(Path.GetTempPath(), $"Rechnung_{rechnung.Id}_{rechnung.Art}_{timestamp}_{randomPart}.docx");

                // Erstelle die Word-Datei
                var wordApp = new Application();
                var doc = wordApp.Documents.Open(templatePath);

                // Platzhalter ersetzen
                ReplaceWordBookmarks(doc, rechnung);

                // Speichern und schließen
                doc.SaveAs2(tempFilePath);
                doc.Close();
                tempFiles.Add(tempFilePath);
            }

            return tempFiles;
        }

        public static void ReplaceWordBookmarks(Document doc, RechnungDisplayModel rechnung)
        {
            // Berechne Von und Bis basierend auf dem Rechnungsjahr
            var vonDatum = new DateTime(rechnung.Jahr, 4, 1).ToShortDateString(); // 01.04. des Rechnungsjahres
            var bisDatum = new DateTime(rechnung.Jahr, 9, 30).ToShortDateString(); // 30.09. des Rechnungsjahres

            // Alle möglichen Felder mit ihren zugehörigen Werten aus dem Model
            var bookmarkValues = new Dictionary<string, string>
            {
                { "PlatzNr", rechnung.Platznr },
                { "Vorname", rechnung.Vorname },
                { "Nachname", rechnung.Nachname },
                { "Anrede", rechnung.Anrede },
                { "Plz", rechnung.PLZ },
                { "Ort", rechnung.Ort },
                { "Strasse", rechnung.Straße },
                { "Alt", $"{rechnung.Alt:0.00}" },
                { "Neu", $"{rechnung.Neu:0.00}" },
                { "Verbrauch", $"{rechnung.Verbrauch:0.00}" },
                { "Betrag", $"{rechnung.Betrag:0.00}" },
                { "Faktor", $"{rechnung.Faktor:0.00}" },
                { "Art", rechnung.Art },
                { "Datum", DateTime.Now.ToShortDateString() },
                { "Jahr", rechnung.Jahr.ToString() },
                { "Von", vonDatum },
                { "Bis", bisDatum }
            };

            // Iteriere durch alle Bookmarks im Dokument und ersetze deren Inhalte
            foreach (Bookmark bookmark in doc.Bookmarks)
            {
                if (bookmarkValues.TryGetValue(bookmark.Name, out string value))
                {
                    bookmark.Range.Text = value;
                }
            }
        }

        public static void MergeWordDocuments(IEnumerable<string> files, string vorlagePath, string outputPdfPath)
        {
            // Word Interop initialisieren
            var wordApp = new Application();
            Document mergedDoc = null;

            try
            {
                // Öffne die Vorlage
                mergedDoc = wordApp.Documents.Open(vorlagePath);

                foreach (var file in files)
                {
                    // Füge jedes Dokument ein
                    var tempDoc = wordApp.Documents.Open(file);
                    tempDoc.Content.Copy(); // Kopiere den gesamten Inhalt
                    var range = mergedDoc.Content;
                    range.Collapse(WdCollapseDirection.wdCollapseEnd); // Füge am Ende ein
                    range.Paste(); // Inhalt einfügen
                    range.Collapse(WdCollapseDirection.wdCollapseEnd); // Füge am Ende ein
                    range.Paste(); // Inhalt doppelt einfügen

                    tempDoc.Close(false); // Schließe das temporäre Dokument
                }

                // Speichere direkt als PDF
                mergedDoc.ExportAsFixedFormat(outputPdfPath, WdExportFormat.wdExportFormatPDF);
            }
            finally
            {
                // Bereinige Ressourcen
                mergedDoc?.Close(false);
                wordApp.Quit();
            }
        }

        public static void ConvertWordToPdf(string wordPath, string pdfPath)
        {
            var wordApp = new Application();
            Document doc = null;

            try
            {
                doc = wordApp.Documents.Open(wordPath);
                doc.ExportAsFixedFormat(pdfPath, WdExportFormat.wdExportFormatPDF);
            }
            finally
            {
                doc?.Close(false);
                wordApp.Quit();
            }
        }
    }
}

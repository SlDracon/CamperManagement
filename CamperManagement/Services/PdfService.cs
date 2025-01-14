using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CamperManagement.Models;
using iText.IO.Font.Constants;
using iText.Kernel.Font;
using System.Diagnostics;
using System;
using System.Linq;
using iText.Kernel.Geom;
using Path = System.IO.Path;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas;

namespace CamperManagement.Services
{
    public static class PdfService
    {
        public static async Task<string> GenerateKostenPdfAsync(int jahr, List<KostenEintrag> eintraege)
        {
            // Erzeuge einen eindeutigen Dateinamen mit Zeitstempel
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var tempPath = Path.Combine(Path.GetTempPath(), $"Kosten_{jahr}_{timestamp}.pdf");

            using var writer = new PdfWriter(tempPath);
            using var pdf = new PdfDocument(writer);
            var document = new Document(pdf);

            // Schriftarten
            var boldFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
            var regularFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);

            // Titel
            var title = new Paragraph($"Kosten für das Jahr {jahr}")
                .SetFont(boldFont)
                .SetFontSize(18)
                .SetTextAlignment(TextAlignment.CENTER);
            document.Add(title);

            // Leerer Abstand
            document.Add(new Paragraph(" "));

            // Tabelle erstellen
            var table = new Table(new float[] { 2, 3, 3, 2, 2, 2 })
                .SetWidth(UnitValue.CreatePercentValue(100));

            // Kopfzeile hinzufügen
            table.AddHeaderCell(new Cell().Add(new Paragraph("PlatzNr").SetFont(boldFont)));
            table.AddHeaderCell(new Cell().Add(new Paragraph("Vorname").SetFont(boldFont)));
            table.AddHeaderCell(new Cell().Add(new Paragraph("Nachname").SetFont(boldFont)));
            table.AddHeaderCell(new Cell().Add(new Paragraph("Wasser-Betrag").SetFont(boldFont)));
            table.AddHeaderCell(new Cell().Add(new Paragraph("Strom-Betrag").SetFont(boldFont)));
            table.AddHeaderCell(new Cell().Add(new Paragraph("Gesamtbetrag").SetFont(boldFont)));

            // Zeilen hinzufügen
            foreach (var eintrag in eintraege)
            {
                table.AddCell(new Cell().Add(new Paragraph(eintrag.PlatzNr).SetFont(regularFont)));
                table.AddCell(new Cell().Add(new Paragraph(eintrag.Vorname).SetFont(regularFont)));
                table.AddCell(new Cell().Add(new Paragraph(eintrag.Nachname).SetFont(regularFont)));
                table.AddCell(new Cell().Add(new Paragraph($"{eintrag.WasserBetrag:0.00} €").SetFont(regularFont)));
                table.AddCell(new Cell().Add(new Paragraph($"{eintrag.StromBetrag:0.00} €").SetFont(regularFont)));
                table.AddCell(new Cell().Add(new Paragraph($"{eintrag.Gesamtbetrag:0.00} €").SetFont(regularFont)));
            }

            document.Add(table);
            document.Close();

            return tempPath;
        }

        public static async Task<string> GenerateTabellePdfAsync(IEnumerable<RechnungDisplayModel> rechnungen)
        {
            // Generiere einen eindeutigen Dateinamen
            var tempPath = Path.Combine(Path.GetTempPath(), $"Tabelle_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");

            using var writer = new PdfWriter(tempPath);
            using var pdf = new PdfDocument(writer);
            var document = new Document(pdf);
            var boldFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);

            // Titel
            document.Add(new Paragraph("Rechnungen")
                .SetTextAlignment(TextAlignment.CENTER)
                .SetFontSize(18)
                .SetFont(boldFont));

            document.Add(new Paragraph(" ")); // Leere Zeile

            // Tabelle
            var table = new Table(new float[] { 2, 3, 3, 2, 2, 2, 2 }).UseAllAvailableWidth();
            table.AddHeaderCell("PlatzNr");
            table.AddHeaderCell("Vorname");
            table.AddHeaderCell("Nachname");
            table.AddHeaderCell("Verbrauch");
            table.AddHeaderCell("Betrag");
            table.AddHeaderCell("Jahr");
            table.AddHeaderCell("Art");

            // Zeilen hinzufügen
            foreach (var rechnung in rechnungen)
            {
                table.AddCell(rechnung.Platznr);
                table.AddCell(rechnung.Vorname);
                table.AddCell(rechnung.Nachname);
                table.AddCell($"{rechnung.Verbrauch:0.00}");
                table.AddCell($"{rechnung.Betrag:0.00} €");
                table.AddCell(rechnung.Jahr.ToString());
                table.AddCell(rechnung.Art);
            }

            document.Add(table);
            document.Close();

            return tempPath;
        }

        public static async Task<string> GenerateAbleseTabellePdfAsync(IEnumerable<AbleseEintrag> ableseEintraege)
        {
            var tempPath = Path.Combine(Path.GetTempPath(), $"AbleseTabelle_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");

            using var writer = new PdfWriter(tempPath);
            using var pdf = new PdfDocument(writer);
            var document = new Document(pdf);
            var boldFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);

            // Titel hinzufügen
            document.Add(new Paragraph("Tabelle zum Ablesen")
                .SetTextAlignment(TextAlignment.CENTER)
                .SetFontSize(18)
                .SetFont(boldFont));

            document.Add(new Paragraph(" ")); // Leerzeile

            // Tabelle erstellen
            var table = new Table(new float[] { 2, 3, 3, 2, 2, 2, 2 }).UseAllAvailableWidth();
            table.AddHeaderCell("PlatzNr");
            table.AddHeaderCell("Vorname");
            table.AddHeaderCell("Nachname");
            table.AddHeaderCell("Wasser_Alt");
            table.AddHeaderCell("Wasser_Neu");
            table.AddHeaderCell("Strom_Alt");
            table.AddHeaderCell("Strom_Neu");

            foreach (var eintrag in ableseEintraege)
            {
                table.AddCell(eintrag.PlatzNr);
                table.AddCell(eintrag.Vorname);
                table.AddCell(eintrag.Nachname);
                table.AddCell($"{eintrag.WasserAlt:0.00}");
                table.AddCell(" "); // Leeres Feld für Wasser_Neu
                table.AddCell($"{eintrag.StromAlt:0.00}");
                table.AddCell(" "); // Leeres Feld für Strom_Neu
            }
            document.Add(table);
            document.Close();

            return tempPath;
        }

        public static string MergeRechnungenToPdf(IEnumerable<string> pdfPaths)
        {
            var outputPath = Path.Combine(Path.GetTempPath(), $"Rechnungen_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");

            using var writer = new PdfWriter(outputPath);
            using var pdf = new PdfDocument(writer);

            foreach (var path in pdfPaths)
            {
                using var reader = new PdfReader(path);
                using var srcPdf = new PdfDocument(reader);

                srcPdf.CopyPagesTo(1, srcPdf.GetNumberOfPages(), pdf);
            }

            return outputPath;
        }

        public static void OpenPdf(string filePath)
        {
            // Öffne die PDF mit dem Standardprogramm
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo(filePath)
                {
                    UseShellExecute = true
                }
            };
            process.Start();
        }
    }
}

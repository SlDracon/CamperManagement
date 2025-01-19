using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using System.Collections.Generic;
using System.Threading.Tasks;
using CamperManagement.Models;
using iText.IO.Font.Constants;
using iText.Kernel.Font;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using iText.Kernel.Geom;
using iText.Layout.Borders;
using Path = System.IO.Path;

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
            var table = new Table(new float[] { 2, 3, 3, 2, 2, 2, 1 })
                .SetWidth(UnitValue.CreatePercentValue(100));

            // Kopfzeile hinzufügen
            table.AddHeaderCell(new Cell().Add(new Paragraph("PlatzNr").SetFont(boldFont).SetTextAlignment(TextAlignment.CENTER)));
            table.AddHeaderCell(new Cell().Add(new Paragraph("Vorname").SetFont(boldFont).SetTextAlignment(TextAlignment.CENTER)));
            table.AddHeaderCell(new Cell().Add(new Paragraph("Nachname").SetFont(boldFont).SetTextAlignment(TextAlignment.CENTER)));
            table.AddHeaderCell(new Cell().Add(new Paragraph("Wasser-betrag").SetFont(boldFont).SetTextAlignment(TextAlignment.CENTER)));
            table.AddHeaderCell(new Cell().Add(new Paragraph("Strom-betrag").SetFont(boldFont).SetTextAlignment(TextAlignment.CENTER)));
            table.AddHeaderCell(new Cell().Add(new Paragraph("Gesamt-betrag").SetFont(boldFont).SetTextAlignment(TextAlignment.CENTER)));
            table.AddHeaderCell(new Cell().Add(new Paragraph("Vertrags-kosten").SetFont(boldFont).SetTextAlignment(TextAlignment.CENTER)));

            // Zeilen hinzufügen
            foreach (var eintrag in eintraege)
            {
                table.AddCell(new Cell().Add(new Paragraph(eintrag.PlatzNr).SetFont(regularFont)));
                table.AddCell(new Cell().Add(new Paragraph(eintrag.Vorname).SetFont(regularFont)));
                table.AddCell(new Cell().Add(new Paragraph(eintrag.Nachname).SetFont(regularFont)));
                table.AddCell(new Cell().Add(new Paragraph($"{eintrag.WasserBetrag:0.00} €").SetFont(regularFont).SetTextAlignment(TextAlignment.RIGHT)));
                table.AddCell(new Cell().Add(new Paragraph($"{eintrag.StromBetrag:0.00} €").SetFont(regularFont).SetTextAlignment(TextAlignment.RIGHT)));
                table.AddCell(new Cell().Add(new Paragraph($"{eintrag.Gesamtbetrag:0.00} €").SetFont(regularFont).SetTextAlignment(TextAlignment.RIGHT)));
                table.AddCell(new Cell().Add(new Paragraph($"{eintrag.Vertragskosten:0.00} €").SetFont(regularFont).SetTextAlignment(TextAlignment.RIGHT)));
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

        public static async Task GenerateAndMergeRechnungenAsync(
            IEnumerable<RechnungDisplayModel> rechnungen,
            string outputPath,
            Action<string> updateStatus)
        {
            var tempPdfPaths = new List<string>();

            try
            {
                int total = rechnungen.Count();
                int current = 0;

                foreach (var rechnung in rechnungen)
                {
                    current++;
                    updateStatus($"Rechnung {current}/{total} wird generiert...");

                    // Generiere die PDF für die Rechnung
                    string tempPdfPath = GenerateRechnungPdf(rechnung);
                    tempPdfPaths.Add(tempPdfPath);
                }

                updateStatus("Rechnungen werden zusammengeführt...");

                // Merge die temporären PDFs
                using var writer = new PdfWriter(outputPath);
                using var mergedPdf = new PdfDocument(writer);

                foreach (var path in tempPdfPaths)
                {
                    using var reader = new PdfReader(path);
                    using var srcPdf = new PdfDocument(reader);
                    srcPdf.CopyPagesTo(1, srcPdf.GetNumberOfPages(), mergedPdf);
                }

                updateStatus("Zusammenführen abgeschlossen.");
            }
            finally
            {
                // Lösche temporäre Dateien
                foreach (var tempPath in tempPdfPaths)
                {
                    if (File.Exists(tempPath))
                        File.Delete(tempPath);
                }
            }
        }

        public static string GenerateRechnungPdf(RechnungDisplayModel rechnung)
        {
            // Erzeuge einen eindeutigen Dateinamen
            string tempPath = Path.Combine(
                Path.GetTempPath(),
                $"Rechnung_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N").Substring(0, 8)}.pdf"
            );

            using (var writer = new PdfWriter(tempPath))
            using (var pdf = new PdfDocument(writer))
            {
                // A5-Seitengröße definieren
                pdf.SetDefaultPageSize(PageSize.A5);
                var document = new Document(pdf);

                // Schriftarten definieren
                var boldFont = PdfFontFactory.CreateFont(StandardFonts.TIMES_BOLD);
                var regularFont = PdfFontFactory.CreateFont(StandardFonts.TIMES_ROMAN);

                // Kopfzeile
                document.Add(new Paragraph("Strandbetriebe August Heim")
                    .SetFont(boldFont)
                    .SetFontSize(14)
                    .SetTextAlignment(TextAlignment.CENTER));

                document.Add(new Paragraph("Inhaber Andreas Heim")
                    .SetFont(regularFont)
                    .SetFontSize(12)
                    .SetTextAlignment(TextAlignment.CENTER));

                document.Add(new Paragraph(" "));

                // Tabelle für oberen Bereich erstellen
                var table = new Table(UnitValue.CreatePercentArray(new float[] { 1, 1 })).UseAllAvailableWidth();

                // Linke Zellen für Adresse
                var addressTable = new Table(UnitValue.CreatePercentArray(1)).UseAllAvailableWidth();
                addressTable.AddCell(new Cell().Add(new Paragraph(rechnung.Anrede).SetFont(regularFont).SetFontSize(11)).SetBorder(Border.NO_BORDER));
                addressTable.AddCell(new Cell().Add(new Paragraph($"{rechnung.Vorname} {rechnung.Nachname}").SetFont(regularFont).SetFontSize(11)).SetBorder(Border.NO_BORDER));
                addressTable.AddCell(new Cell().Add(new Paragraph(rechnung.Straße).SetFont(regularFont).SetFontSize(11)).SetBorder(Border.NO_BORDER));
                addressTable.AddCell(new Cell().Add(new Paragraph($"{rechnung.PLZ} {rechnung.Ort}").SetFont(regularFont).SetFontSize(11)).SetBorder(Border.NO_BORDER));
                table.AddCell(new Cell().Add(addressTable).SetBorder(Border.NO_BORDER));

                // Rechte Zellen für Details
                var detailsTable = new Table(UnitValue.CreatePercentArray(1)).UseAllAvailableWidth();
                detailsTable.AddCell(new Cell().Add(new Paragraph("Am Strande 25").SetFont(regularFont).SetFontSize(11)).SetBorder(Border.NO_BORDER));
                detailsTable.AddCell(new Cell().Add(new Paragraph("23730 Neustadt").SetFont(regularFont).SetFontSize(11)).SetBorder(Border.NO_BORDER));
                detailsTable.AddCell(new Cell().Add(new Paragraph("Tel.: 04561/2017").SetFont(regularFont).SetFontSize(11)).SetBorder(Border.NO_BORDER));
                detailsTable.AddCell(new Cell().Add(new Paragraph("St.-Nr.: 2504700595").SetFont(regularFont).SetFontSize(11)).SetBorder(Border.NO_BORDER));
                detailsTable.AddCell(new Cell().Add(new Paragraph($"Neustadt, den {DateTime.Now:dd.MM.yyyy}").SetFont(regularFont).SetFontSize(11)).SetBorder(Border.NO_BORDER));
                table.AddCell(new Cell().Add(detailsTable).SetTextAlignment(TextAlignment.RIGHT).SetBorder(Border.NO_BORDER));

                document.Add(table);

                document.Add(new Paragraph(" "));
                if (rechnung.Art == "Strom")
                {
                    // Überschrift für Rechnungsdetails
                    document.Add(new Paragraph($"Stromverbrauch in der Zeit vom 01.04.{rechnung.Jahr} bis 30.09.{rechnung.Jahr}").SetMarginTop(30)
                        .SetFont(boldFont)
                        .SetFontSize(12)
                        .SetTextAlignment(TextAlignment.LEFT).SetMarginBottom(10));
                }
                else
                {
                    // Überschrift für Rechnungsdetails
                    document.Add(new Paragraph($"Wasserverbrauch in der Zeit vom 01.04.{rechnung.Jahr} bis 30.09.{rechnung.Jahr}").SetMarginTop(30)
                        .SetFont(boldFont)
                        .SetFontSize(12)
                        .SetTextAlignment(TextAlignment.LEFT).SetMarginBottom(10));
                }
                
                // Rechnungsdetails Tabelle erstellen
                var detailsContentTable = new Table(UnitValue.CreatePercentArray(new float[] { 2, 6 })).UseAllAvailableWidth();
                if (rechnung.Art == "Strom")
                {
                    detailsContentTable.AddCell(new Cell().Add(new Paragraph("Strom alt:").SetFont(regularFont).SetFontSize(11)).SetBorder(Border.NO_BORDER));
                    detailsContentTable.AddCell(new Cell().Add(new Paragraph($"{rechnung.Alt:0.00} kWh").SetFont(regularFont).SetFontSize(11)).SetBorder(Border.NO_BORDER));
                    detailsContentTable.AddCell(new Cell().Add(new Paragraph("Strom neu:").SetFont(regularFont).SetFontSize(11)).SetBorder(Border.NO_BORDER));
                    detailsContentTable.AddCell(new Cell().Add(new Paragraph($"{rechnung.Neu:0.00} kWh").SetFont(regularFont).SetFontSize(11)).SetBorder(Border.NO_BORDER));
                    detailsContentTable.AddCell(new Cell(1, 2).Add(new Paragraph("______________________________________________________________").SetFont(regularFont).SetFontSize(11)).SetBorder(Border.NO_BORDER));
                    detailsContentTable.AddCell(new Cell().Add(new Paragraph("Verbrauch:").SetFont(regularFont).SetFontSize(11)).SetBorder(Border.NO_BORDER));
                    detailsContentTable.AddCell(new Cell().Add(new Paragraph($"{rechnung.Verbrauch:0.00} kWh x {rechnung.Faktor:0.00} €").SetFont(regularFont).SetFontSize(11)).SetBorder(Border.NO_BORDER));
                }
                else
                {
                    detailsContentTable.AddCell(new Cell().Add(new Paragraph("Wasser alt:").SetFont(regularFont).SetFontSize(11)).SetBorder(Border.NO_BORDER));
                    detailsContentTable.AddCell(new Cell().Add(new Paragraph($"{rechnung.Alt:0.00} cbm").SetFont(regularFont).SetFontSize(11)).SetBorder(Border.NO_BORDER));
                    detailsContentTable.AddCell(new Cell().Add(new Paragraph("Wasser neu:").SetFont(regularFont).SetFontSize(11)).SetBorder(Border.NO_BORDER));
                    detailsContentTable.AddCell(new Cell().Add(new Paragraph($"{rechnung.Neu:0.00} cbm").SetFont(regularFont).SetFontSize(11)).SetBorder(Border.NO_BORDER));
                    detailsContentTable.AddCell(new Cell(1, 2).Add(new Paragraph("______________________________________________________________").SetFont(regularFont).SetFontSize(11)).SetBorder(Border.NO_BORDER));
                    detailsContentTable.AddCell(new Cell().Add(new Paragraph("Verbrauch:").SetFont(regularFont).SetFontSize(11)).SetBorder(Border.NO_BORDER));
                    detailsContentTable.AddCell(new Cell().Add(new Paragraph($"{rechnung.Verbrauch:0.00} cbm x {rechnung.Faktor:0.00} €").SetFont(regularFont).SetFontSize(11)).SetBorder(Border.NO_BORDER));
                }
                detailsContentTable.AddCell(new Cell().Add(new Paragraph("Summe:").SetFont(boldFont).SetFontSize(11)).SetBorder(Border.NO_BORDER));
                detailsContentTable.AddCell(new Cell().Add(new Paragraph($"{rechnung.Betrag:0.00} €").SetFont(boldFont).SetFontSize(11)).SetBorder(Border.NO_BORDER));

                document.Add(detailsContentTable);

                document.Add(new Paragraph(" "));

                // Zahlungsdetails
                document.Add(new Paragraph("– Rechnungsbetrag wird eingezogen –").SetMarginTop(50)
                    .SetFont(regularFont)
                    .SetFontSize(11)
                    .SetTextAlignment(TextAlignment.CENTER).SetMarginBottom(5));

                document.Add(new Paragraph("Kto.-Verb.: VR OH Nord eG, IBAN: DE19 2139 0008  0000 0012 01")
                    .SetFont(regularFont)
                    .SetFontSize(10)
                    .SetTextAlignment(TextAlignment.CENTER).SetMarginBottom(2));
                document.Add(new Paragraph("BIC: GENODEF1NSH")
                    .SetFont(regularFont)
                    .SetFontSize(10)
                    .SetTextAlignment(TextAlignment.CENTER));

                document.Close();
            }

            return tempPath;
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

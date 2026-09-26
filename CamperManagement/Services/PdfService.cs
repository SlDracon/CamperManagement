using System.Threading;
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
using Border = iText.Layout.Borders.Border;
using Avalonia.Platform.Storage;
using iText.Kernel.Colors;

namespace CamperManagement.Services
{
    public static class PdfService
    {
        private static readonly CultureInfo GermanCulture = CultureInfo.GetCultureInfo("de-DE");
        public static async Task<IStorageFile?> GenerateKostenPdfAsync(IStorageProvider? storageProvider, int jahr, IEnumerable<KostenEintrag> eintraege, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (storageProvider == null)
            {
                return null;
            }

            var snapshot = eintraege.Select(r => r.Snapshot()).ToList();

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var filename = $"Kosten_{jahr}_{timestamp}.pdf";
            // Speichern-Dialog anzeigen
            var options = new FilePickerSaveOptions
            {
                SuggestedFileName = filename,
                ShowOverwritePrompt = true,
                FileTypeChoices =
                [
                    new FilePickerFileType("PDF Files") { Patterns = ["*.pdf"] }
                ]
            };

            var result = await storageProvider.SaveFilePickerAsync(options);

            // Prüfe ob der Benutzer den Dialog abgebrochen hat
            if (result == null)
            {
                return null;
            }

            // Schreibe den Inhalt in die Datei
            return await WriteFileAsync(result, cancellationToken, stream => WriteKostenPdf(stream, jahr, snapshot));
        }

        private static void WriteKostenPdf(Stream stream, int jahr, IReadOnlyList<KostenEintrag> eintraege)
        {
            using var writer = new PdfWriter(stream);
            using var pdf = new PdfDocument(writer);
            var document = new Document(pdf, PageSize.A4);
            document.SetMargins(36, 36, 36, 36);  // 36 Punkte = ca. 1,27 cm

            // Schriftarten
            var boldFont = CreateFont("NotoSans-Bold");
            var regularFont = CreateFont("NotoSans-Regular");

            // Titel mit mehr Abstand
            var title = new Paragraph($"Kostenübersicht {jahr}")
                .SetFont(boldFont)
                .SetFontSize(20)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetMarginBottom(20);
            document.Add(title);

            // Tabelle mit verbessertem Layout
            var columnWidths = new float[] { 1, 2, 2, 1.5f, 1.5f, 1.5f, 1.5f };
            var table = new Table(UnitValue.CreatePercentArray(columnWidths))
                .UseAllAvailableWidth()
                .SetBorder(new SolidBorder(0.5f));

            // Kopfzeilen mit verbessertem Styling
            string[] headers = { "PlatzNr", "Vorname", "Nachname", "Wasser", "Strom", "Gesamt", "Vertrag" };
            foreach (var header in headers)
            {
                table.AddHeaderCell(
                    new Cell()
                        .SetBackgroundColor(new DeviceRgb(240, 240, 240))
                        .SetPadding(5)
                        .Add(new Paragraph(header)
                            .SetFont(boldFont)
                            .SetTextAlignment(TextAlignment.CENTER)));
            }

            // Zeilen mit verbessertem Styling
            foreach (var eintrag in eintraege)
            {
                table.AddCell(new Cell().SetPadding(5).Add(new Paragraph(eintrag.PlatzNr).SetFont(regularFont)));
                table.AddCell(new Cell().SetPadding(5).Add(new Paragraph(eintrag.Vorname).SetFont(regularFont)));
                table.AddCell(new Cell().SetPadding(5).Add(new Paragraph(eintrag.Nachname).SetFont(regularFont)));
                table.AddCell(new Cell().SetPadding(5).Add(new Paragraph($"{eintrag.WasserBetrag.ToString("N2", GermanCulture)} €")
                    .SetFont(regularFont).SetTextAlignment(TextAlignment.RIGHT)));
                table.AddCell(new Cell().SetPadding(5).Add(new Paragraph($"{eintrag.StromBetrag.ToString("N2", GermanCulture)} €")
                    .SetFont(regularFont).SetTextAlignment(TextAlignment.RIGHT)));
                table.AddCell(new Cell().SetPadding(5).Add(new Paragraph($"{eintrag.Gesamtbetrag.ToString("N2", GermanCulture)} €")
                    .SetFont(regularFont).SetTextAlignment(TextAlignment.RIGHT)));
                table.AddCell(new Cell().SetPadding(5).Add(new Paragraph($"{eintrag.Vertragskosten.ToString("N2", GermanCulture)} €")
                    .SetFont(regularFont).SetTextAlignment(TextAlignment.RIGHT)));
            }

            // Summenzeile hinzufügen
            var summen = new[]
            {
                eintraege.Sum(e => e.WasserBetrag),
                eintraege.Sum(e => e.StromBetrag),
                eintraege.Sum(e => e.Gesamtbetrag),
                eintraege.Sum(e => e.Vertragskosten)
            };

            table.AddCell(new Cell(1, 3).SetPadding(5)
                .Add(new Paragraph("Gesamtsumme:").SetFont(boldFont)));
            foreach (var summe in summen)
            {
                table.AddCell(new Cell().SetPadding(5)
                    .SetBackgroundColor(new DeviceRgb(240, 240, 240))
                    .Add(new Paragraph($"{summe.ToString("N2", GermanCulture)} €")
                        .SetFont(boldFont)
                        .SetTextAlignment(TextAlignment.RIGHT)));
            }

            document.Add(table);

            // Fußzeile hinzufügen
            document.Add(new Paragraph($"\nErstellt am: {DateTime.Now:dd.MM.yyyy HH:mm}")
                .SetFont(regularFont)
                .SetFontSize(8)
                .SetTextAlignment(TextAlignment.RIGHT));

            document.Close();

        }

        public static async Task<IStorageFile?> GenerateTabellePdfAsync(IStorageProvider? storageProvider, IEnumerable<RechnungDisplayModel> rechnungen, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (storageProvider == null)
            {
                return null;
            }

            var snapshot = rechnungen.Select(r => r.Snapshot()).ToList();

            var filename = $"Tabelle_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
            // Speichern-Dialog anzeigen
            var options = new FilePickerSaveOptions
            {
                SuggestedFileName = filename,
                ShowOverwritePrompt = true,
                FileTypeChoices =
                [
                    new FilePickerFileType("PDF Files") { Patterns = ["*.pdf"] }
                ]
            };

            var result = await storageProvider.SaveFilePickerAsync(options);

            // Prüfe ob der Benutzer den Dialog abgebrochen hat
            if (result == null)
            {
                return null;
            }

            // Schreibe den Inhalt in die Datei
            return await WriteFileAsync(result, cancellationToken, stream => WriteTabellePdf(stream, snapshot));
        }

        private static void WriteTabellePdf(Stream stream, IReadOnlyList<RechnungDisplayModel> rechnungen)
        {
            using var writer = new PdfWriter(stream);
            using var pdf = new PdfDocument(writer);
            var document = new Document(pdf).SetFont(CreateFont("NotoSans-Regular"));
            var boldFont = CreateFont("NotoSans-Bold");

            // Titel
            document.Add(new Paragraph("Rechnungen")
                .SetTextAlignment(TextAlignment.CENTER)
                .SetFontSize(18)
                .SetFont(boldFont));

            document.Add(new Paragraph(" ")); // Leere Zeile

            // Tabelle
            var table = new Table([2, 3, 3, 2, 2, 2, 2]).UseAllAvailableWidth();
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
                table.AddCell(rechnung.VerbrauchDisplay);
                table.AddCell($"{rechnung.Betrag.ToString("0.00", GermanCulture)} €");
                table.AddCell(rechnung.Jahr.ToString());
                table.AddCell(rechnung.Art);
            }

            document.Add(table);
            document.Close();

        }

        public static async Task<IStorageFile?> GenerateAbleseTabellePdfAsync(IStorageProvider? storageProvider, IEnumerable<AbleseEintrag> ableseEintraege, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (storageProvider == null)
            {
                return null;
            }

            var snapshot = ableseEintraege.Select(r => r.Snapshot()).ToList();

            var filename = $"AbleseTabelle_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
            // Speichern-Dialog anzeigen
            var options = new FilePickerSaveOptions
            {
                SuggestedFileName = filename,
                ShowOverwritePrompt = true,
                FileTypeChoices =
                [
                    new FilePickerFileType("PDF Files") { Patterns = ["*.pdf"] }
                ]
            };

            var result = await storageProvider.SaveFilePickerAsync(options);

            // Prüfe ob der Benutzer den Dialog abgebrochen hat
            if (result == null)
            {
                return null;
            }

            // Schreibe den Inhalt in die Datei
            return await WriteFileAsync(result, cancellationToken, stream => WriteAbleseTabellePdf(stream, snapshot));
        }

        private static void WriteAbleseTabellePdf(Stream stream, IReadOnlyList<AbleseEintrag> ableseEintraege)
        {
            using var writer = new PdfWriter(stream);
            using var pdf = new PdfDocument(writer);
            var document = new Document(pdf).SetFont(CreateFont("NotoSans-Regular"));
            var boldFont = CreateFont("NotoSans-Bold");

            // Titel hinzufügen
            document.Add(new Paragraph("Tabelle zum Ablesen")
                .SetTextAlignment(TextAlignment.CENTER)
                .SetFontSize(18)
                .SetFont(boldFont));

            document.Add(new Paragraph(" ")); // Leerzeile

            // Tabelle erstellen
            var table = new Table([2, 3, 3, 2, 2, 2, 2]).UseAllAvailableWidth();
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
                table.AddCell(eintrag.WasserAltDisplay);
                table.AddCell(" "); // Leeres Feld für Wasser_Neu
                table.AddCell(eintrag.StromAltDisplay);
                table.AddCell(" "); // Leeres Feld für Strom_Neu
            }
            document.Add(table);
            document.Close();

        }

        public static async Task<IStorageFile?> GenerateAndMergeRechnungenAsync(
            IStorageProvider? storageProvider,
            IEnumerable<RechnungDisplayModel> rechnungen,
            IProgress<string?> updateStatus, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (storageProvider == null)
            {
                updateStatus.Report("Speicherziel nicht verfügbar.");
                return null;
            }

            var selectedRechnungen = rechnungen.Select(r => r.Snapshot()).ToList();
            ValidateInvoices(selectedRechnungen);

            var filename = $"Rechnungen_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
            // Speichern-Dialog anzeigen
            var options = new FilePickerSaveOptions
            {
                SuggestedFileName = filename,
                ShowOverwritePrompt = true,
                FileTypeChoices =
                [
                    new FilePickerFileType("PDF Files") { Patterns = ["*.pdf"] }
                ]
            };

            var result = await storageProvider.SaveFilePickerAsync(options);

            // Prüfe ob der Benutzer den Dialog abgebrochen hat
            if (result == null)
            {
                return null;
            }

            // Schreibe den Inhalt in die Datei
            return await WriteFileAsync(result, cancellationToken, stream =>
            {
                var tempPdfPaths = new List<string>();
                try
                {
                    var current = 0;
                    foreach (var rechnung in selectedRechnungen)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        updateStatus.Report($"Rechnung {++current}/{selectedRechnungen.Count} wird generiert...");
                        tempPdfPaths.Add(GenerateRechnungPdf(rechnung));
                    }

                    updateStatus.Report("Rechnungen werden zusammengeführt...");
                    using var writer = new PdfWriter(stream);
                    using var mergedPdf = new PdfDocument(writer);
                    foreach (var path in tempPdfPaths)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        using var reader = new PdfReader(path);
                        using var srcPdf = new PdfDocument(reader);
                        srcPdf.CopyPagesTo(1, srcPdf.GetNumberOfPages(), mergedPdf);
                    }
                }
                finally
                {
                    foreach (var tempPath in tempPdfPaths)
                    {
                        File.Delete(tempPath);
                    }
                }
            });

        }

        public static async Task<bool> GenerateRechnungenByPlatzAsync(
            IStorageProvider? storageProvider,
            IEnumerable<RechnungDisplayModel> rechnungen,
            IProgress<string?> updateStatus, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (storageProvider == null)
            {
                updateStatus.Report("Speicherziel nicht verfügbar.");
                return false;
            }

            var snapshot = rechnungen.Select(r => r.Snapshot()).ToList();
            ValidateInvoices(snapshot);
            try
            {
                updateStatus.Report("Zielordner auswählen...");
                var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = "Zielordner für Rechnungen"
                });

                using var targetFolder = folders?.FirstOrDefault();
                cancellationToken.ThrowIfCancellationRequested();
                if (targetFolder == null)
                {
                    return false;
                }

                var groupedRechnungen = await Task.Run(() => snapshot
                    .GroupBy(r => (Platz: string.IsNullOrWhiteSpace(r.Platznr) ? "Unbekannt" : r.Platznr!.Trim(), r.CamperId))
                    .ToList());

                if (groupedRechnungen.Count == 0)
                {
                    updateStatus.Report("Keine Rechnungen ausgewählt.");
                    return false;
                }

                foreach (var group in groupedRechnungen)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var platzNr = group.Key.Platz;
                    updateStatus.Report($"Platz {platzNr}: PDFs werden vorbereitet...");

                    var baseFileName = $"Rechnungen_{SanitizeFileName(platzNr)}";
                    var destinationName = baseFileName + ".pdf";
                    var duplicateIndex = 1;
                    while (true)
                    {
                        using var existingFile = await targetFolder.GetFileAsync(destinationName);
                        if (existingFile == null)
                            break;
                        destinationName = $"{baseFileName}_{duplicateIndex++}.pdf";
                    }

                    using var destination = await targetFolder.CreateFileAsync(destinationName)
                        ?? throw new IOException("Die PDF-Datei konnte nicht angelegt werden.");
                    await WriteFileAsync(destination, cancellationToken, output => WritePlatzPdf(output, platzNr, group, updateStatus, cancellationToken));
                    updateStatus.Report($"Platz {platzNr}: PDF erstellt.");
                }

                updateStatus.Report("Alle PDFs wurden erstellt.");
                return true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                updateStatus.Report("Fehler beim Erstellen der PDFs.");
                throw new IOException("Die ausgewählten Rechnungen konnten nicht vollständig gespeichert werden.", ex);
            }
        }

        private static void WritePlatzPdf(Stream output, string platzNr,
            IEnumerable<RechnungDisplayModel> rechnungen, IProgress<string?> updateStatus, CancellationToken cancellationToken)
        {
            var tempPaths = new List<string>();
            try
            {
                var orderedGroup = rechnungen.OrderBy(r => r.Art).ThenBy(r => r.Jahr).ThenBy(r => r.Id).ToList();
                var current = 0;
                foreach (var rechnung in orderedGroup)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    updateStatus.Report($"Platz {platzNr}: Rechnung {++current}/{orderedGroup.Count} wird generiert...");
                    tempPaths.Add(GenerateRechnungPdf(rechnung));
                }
                tempPaths.Add(GeneratePlatzSummaryPdf(platzNr, orderedGroup));

                using var writer = new PdfWriter(output);
                using var mergedPdf = new PdfDocument(writer);
                foreach (var path in tempPaths)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    using var reader = new PdfReader(path);
                    using var srcPdf = new PdfDocument(reader);
                    srcPdf.CopyPagesTo(1, srcPdf.GetNumberOfPages(), mergedPdf);
                }
            }
            finally
            {
                foreach (var tempPath in tempPaths)
                {
                    try
                    {
                        File.Delete(tempPath);
                    }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
            }
        }

        public static string GenerateRechnungPdf(RechnungDisplayModel rechnung)
        {
            // Erzeuge einen eindeutigen Dateinamen
            var tempPath = Path.Combine(
                Path.GetTempPath(),
                $"Rechnung_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N").Substring(0, 8)}.pdf"
            );

            try
            {
                WriteRechnungPdf(tempPath, rechnung);
                return tempPath;
            }
            catch
            {
                File.Delete(tempPath);
                throw;
            }
        }

        private static void WriteRechnungPdf(string tempPath, RechnungDisplayModel rechnung)
        {
            using var writer = new PdfWriter(tempPath);
            using var pdf = new PdfDocument(writer);
            // A5-Seitengröße definieren
            pdf.SetDefaultPageSize(PageSize.A5);
            var document = new Document(pdf).SetFont(CreateFont("NotoSans-Regular"));

            // Schriftarten definieren
            var boldFont = CreateFont("NotoSerif-Bold");
            var regularFont = CreateFont("NotoSerif-Regular");

            // Kopfzeile
            document.Add(new Paragraph("Strandbetriebe August Heim")
                .SetFont(boldFont)
                .SetFontSize(14)
                .SetTextAlignment(TextAlignment.CENTER));

            document.Add(new Paragraph("Inhaber Andreas Heim").SetMarginTop(-5)
                .SetFont(regularFont)
                .SetFontSize(12)
                .SetTextAlignment(TextAlignment.CENTER));

            document.Add(new Paragraph(" "));

            // Tabelle für oberen Bereich erstellen
            var table = new Table(UnitValue.CreatePercentArray([4.5f, 3.5f])).UseAllAvailableWidth();

            // Linke Zellen für Adresse
            var addressTable = new Table(UnitValue.CreatePercentArray(1)).UseAllAvailableWidth();
            addressTable.AddCell(new Cell().Add(new Paragraph("Strandbetriebe August Heim * Am Strande 25 * 23730 Neustadt").SetFont(regularFont).SetFontSize(7)).SetBorder(Border.NO_BORDER));
            addressTable.AddCell(new Cell().Add(new Paragraph(rechnung.Anrede).SetFont(regularFont).SetFontSize(11)).SetBorder(Border.NO_BORDER));
            addressTable.AddCell(new Cell().Add(new Paragraph($"{rechnung.Vorname} {rechnung.Nachname}").SetFont(regularFont).SetFontSize(11)).SetBorder(Border.NO_BORDER));
            addressTable.AddCell(new Cell().Add(new Paragraph(rechnung.Straße).SetFont(regularFont).SetFontSize(11)).SetBorder(Border.NO_BORDER));
            addressTable.AddCell(new Cell().Add(new Paragraph($"{rechnung.PLZ} {rechnung.Ort}").SetFont(regularFont).SetFontSize(11)).SetBorder(Border.NO_BORDER));
            table.AddCell(new Cell().Add(addressTable).SetBorder(Border.NO_BORDER));

            // Rechte Zellen für Details
            var detailsTable = new Table(UnitValue.CreatePercentArray(1)).UseAllAvailableWidth();
            detailsTable.AddCell(new Cell().Add(new Paragraph("Am Strande 25").SetFont(regularFont).SetFontSize(10)).SetTextAlignment(TextAlignment.LEFT).SetBorder(Border.NO_BORDER));
            detailsTable.AddCell(new Cell().Add(new Paragraph("23730 Neustadt").SetFont(regularFont).SetFontSize(10)).SetTextAlignment(TextAlignment.LEFT).SetBorder(Border.NO_BORDER));
            detailsTable.AddCell(new Cell().Add(new Paragraph("Tel.: 04561/2017").SetFont(regularFont).SetFontSize(10)).SetTextAlignment(TextAlignment.LEFT).SetBorder(Border.NO_BORDER));
            detailsTable.AddCell(new Cell().Add(new Paragraph("St.-Nr.: 2504700595").SetFont(regularFont).SetFontSize(10)).SetTextAlignment(TextAlignment.LEFT).SetBorder(Border.NO_BORDER));
            detailsTable.AddCell(new Cell().Add(new Paragraph($"Neustadt, den {DateTime.Now:dd.MM.yyyy}").SetFont(regularFont).SetFontSize(10).SetMarginTop(12)).SetTextAlignment(TextAlignment.LEFT).SetBorder(Border.NO_BORDER));
            table.AddCell(new Cell().Add(detailsTable).SetBorder(Border.NO_BORDER));

            document.Add(table);

            document.Add(new Paragraph(" "));
            if (rechnung.Art == "Strom")
            {
                // Überschrift für Rechnungsdetails
                document.Add(new Paragraph($"Stromverbrauch in der Zeit vom 01.04.{rechnung.Jahr} bis 30.09.{rechnung.Jahr}").SetMarginTop(15)
                    .SetFont(boldFont)
                    .SetFontSize(12)
                    .SetTextAlignment(TextAlignment.LEFT).SetMarginBottom(10));
            }
            else
            {
                // Überschrift für Rechnungsdetails
                document.Add(new Paragraph($"Wasserverbrauch in der Zeit vom 01.04.{rechnung.Jahr} bis 30.09.{rechnung.Jahr}").SetMarginTop(15)
                    .SetFont(boldFont)
                    .SetFontSize(12)
                    .SetTextAlignment(TextAlignment.LEFT).SetMarginBottom(10));
            }

            // Rechnungsdetails Tabelle erstellen
            var detailsContentTable = new Table(UnitValue.CreatePercentArray(new float[] { 2, 6 })).UseAllAvailableWidth();
            if (rechnung.Art == "Strom")
            {
                detailsContentTable.AddCell(new Cell().Add(new Paragraph("Strom alt:").SetFont(regularFont).SetFontSize(11)).SetBorder(Border.NO_BORDER));
                detailsContentTable.AddCell(new Cell().Add(new Paragraph($"{rechnung.Alt.ToString("0.00", GermanCulture)} kWh").SetFont(regularFont).SetFontSize(11)).SetBorder(Border.NO_BORDER));
                detailsContentTable.AddCell(new Cell().Add(new Paragraph("Strom neu:").SetFont(regularFont).SetFontSize(11)).SetBorder(Border.NO_BORDER));
                detailsContentTable.AddCell(new Cell().Add(new Paragraph($"{rechnung.Neu.ToString("0.00", GermanCulture)} kWh").SetFont(regularFont).SetFontSize(11)).SetBorder(Border.NO_BORDER));
                detailsContentTable.AddCell(new Cell(1, 2).Add(new Paragraph("______________________________________________________________").SetFont(regularFont).SetFontSize(11)).SetBorder(Border.NO_BORDER));
                detailsContentTable.AddCell(new Cell().Add(new Paragraph("Verbrauch:").SetFont(regularFont).SetFontSize(11)).SetBorder(Border.NO_BORDER));
                detailsContentTable.AddCell(new Cell().Add(new Paragraph($"{rechnung.Verbrauch.ToString("0.00", GermanCulture)} kWh x {rechnung.Faktor.ToString("0.00", GermanCulture)} €").SetFont(regularFont).SetFontSize(11)).SetBorder(Border.NO_BORDER));
            }
            else
            {
                detailsContentTable.AddCell(new Cell().Add(new Paragraph("Wasser alt:").SetFont(regularFont).SetFontSize(11)).SetBorder(Border.NO_BORDER));
                detailsContentTable.AddCell(new Cell().Add(new Paragraph(rechnung.AltDisplay + " cbm").SetFont(regularFont).SetFontSize(11)).SetBorder(Border.NO_BORDER));
                detailsContentTable.AddCell(new Cell().Add(new Paragraph("Wasser neu:").SetFont(regularFont).SetFontSize(11)).SetBorder(Border.NO_BORDER));
                detailsContentTable.AddCell(new Cell().Add(new Paragraph(rechnung.NeuDisplay + " cbm").SetFont(regularFont).SetFontSize(11)).SetBorder(Border.NO_BORDER));
                detailsContentTable.AddCell(new Cell(1, 2).Add(new Paragraph("______________________________________________________________").SetFont(regularFont).SetFontSize(11)).SetBorder(Border.NO_BORDER));
                detailsContentTable.AddCell(new Cell().Add(new Paragraph("Verbrauch:").SetFont(regularFont).SetFontSize(11)).SetBorder(Border.NO_BORDER));
                detailsContentTable.AddCell(new Cell().Add(new Paragraph($"{rechnung.VerbrauchDisplay} cbm x {rechnung.Faktor.ToString("0.00", GermanCulture)} €").SetFont(regularFont).SetFontSize(11)).SetBorder(Border.NO_BORDER));
            }
            detailsContentTable.AddCell(new Cell().Add(new Paragraph("Summe:").SetFont(boldFont).SetFontSize(11)).SetBorder(Border.NO_BORDER));
            detailsContentTable.AddCell(new Cell().Add(new Paragraph($"{rechnung.Betrag.ToString("0.00", GermanCulture)} €").SetFont(boldFont).SetFontSize(11)).SetBorder(Border.NO_BORDER));

            document.Add(detailsContentTable);

            document.Add(new Paragraph(" "));

            // Zahlungsdetails
            document.Add(new Paragraph("– Rechnungsbetrag wird eingezogen –").SetMarginTop(15)
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

        private static string GeneratePlatzSummaryPdf(string platzNr, List<RechnungDisplayModel> rechnungen)
        {
            var tempPath = Path.Combine(
                Path.GetTempPath(),
                $"Rechnung_{SanitizeFileName(platzNr)}_Summary_{Guid.NewGuid():N}.pdf"
            );

            try
            {
                WritePlatzSummaryPdf(tempPath, platzNr, rechnungen);
                return tempPath;
            }
            catch
            {
                File.Delete(tempPath);
                throw;
            }
        }

        private static void WritePlatzSummaryPdf(string tempPath, string platzNr, List<RechnungDisplayModel> rechnungen)
        {
            using var writer = new PdfWriter(tempPath);
            using var pdf = new PdfDocument(writer);
            pdf.SetDefaultPageSize(PageSize.A5);
            var document = new Document(pdf).SetFont(CreateFont("NotoSans-Regular"));

            var boldFont = CreateFont("NotoSerif-Bold");
            var regularFont = CreateFont("NotoSerif-Regular");

            document.Add(new Paragraph($"Zusammenfassung Platz {platzNr}")
                .SetFont(boldFont)
                .SetFontSize(14)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetMarginBottom(20));

            var table = new Table(UnitValue.CreatePercentArray([3, 3, 2])).UseAllAvailableWidth();
            table.AddHeaderCell(new Cell().Add(new Paragraph("Art").SetFont(boldFont)).SetBackgroundColor(new DeviceRgb(240, 240, 240)));
            table.AddHeaderCell(new Cell().Add(new Paragraph("Jahr").SetFont(boldFont)).SetBackgroundColor(new DeviceRgb(240, 240, 240)));
            table.AddHeaderCell(new Cell().Add(new Paragraph("Betrag").SetFont(boldFont)).SetBackgroundColor(new DeviceRgb(240, 240, 240)));

            foreach (var rechnung in rechnungen)
            {
                table.AddCell(new Paragraph(rechnung.Art ?? "-").SetFont(regularFont));
                table.AddCell(new Paragraph(rechnung.Jahr.ToString()).SetFont(regularFont));
                table.AddCell(new Paragraph($"{rechnung.Betrag.ToString("0.00", GermanCulture)} €").SetFont(regularFont).SetTextAlignment(TextAlignment.RIGHT));
            }

            var gesamtbetrag = rechnungen.Sum(r => r.Betrag);

            table.AddCell(new Cell(1, 2)
                .Add(new Paragraph("Gesamtsumme:").SetFont(boldFont))
                .SetBackgroundColor(new DeviceRgb(240, 240, 240)));
            table.AddCell(new Cell()
                .Add(new Paragraph($"{gesamtbetrag.ToString("0.00", GermanCulture)} €").SetFont(boldFont).SetTextAlignment(TextAlignment.RIGHT))
                .SetBackgroundColor(new DeviceRgb(240, 240, 240)));

            document.Add(table);

            document.Add(new Paragraph(" "));
            document.Add(new Paragraph("– Rechnungsbeträge werden eingezogen –")
                .SetFont(regularFont)
                .SetFontSize(10)
                .SetTextAlignment(TextAlignment.CENTER));

            document.Close();

        }

        private static string SanitizeFileName(string input)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(input.Length);
            foreach (var ch in input)
            {
                builder.Append(invalidChars.Contains(ch) || "<>:\"/\\|?*".Contains(ch) || char.IsControl(ch) ? '_' : ch);
            }

            var sanitized = builder.ToString().Trim().TrimEnd('.');
            if (sanitized.Length > 100)
                sanitized = sanitized[..100];
            return string.IsNullOrWhiteSpace(sanitized) ? "Unbekannt" : sanitized;
        }

        private static async Task<IStorageFile> WriteFileAsync(IStorageFile file, CancellationToken cancellationToken, Action<Stream> write)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                await using (var stream = await file.OpenWriteAsync())
                    await Task.Run(() => { cancellationToken.ThrowIfCancellationRequested(); write(new CancellationWriteStream(stream, cancellationToken)); }, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                return file;
            }
            catch
            {
                // Preserve the original failure if releasing a provider handle also fails.
                try
                {
                    file.Dispose();
                }
                catch { }
                cancellationToken.ThrowIfCancellationRequested();
                throw;
            }
        }
        private static PdfFont CreateFont(string name)
        {
            using var stream = typeof(PdfService).Assembly.GetManifestResourceStream($"CamperManagement.Assets.Fonts.{name}.ttf")
                ?? throw new InvalidOperationException("PDF-Schrift fehlt.");
            using var bytes = new MemoryStream();
            stream.CopyTo(bytes);
            return PdfFontFactory.CreateFont(bytes.ToArray(), iText.IO.Font.PdfEncodings.IDENTITY_H, PdfFontFactory.EmbeddingStrategy.FORCE_EMBEDDED);
        }
        private static void ValidateInvoices(IEnumerable<RechnungDisplayModel> rows)
        {
            if (!rows.Any())
                throw new InvalidOperationException("Keine Rechnungen ausgewählt.");
            if (rows.Any(r => !r.RecipientResolved))
                throw new InvalidOperationException("Historischer Rechnungsempfänger muss zuerst zugeordnet werden.");
        }
        public static async Task<bool> OpenPdfAsync(IStorageFile? file, ILauncher? launcher, IErrorLog? log = null)
        {
            if (file == null || launcher == null)
                return false;
            try
            {
                return await launcher.LaunchFileAsync(file);
            }
            catch (Exception ex) { (log ?? NullErrorLog.Instance).Write(ErrorOperation.OpenPdf, ex); return false; }
        }
    }
}

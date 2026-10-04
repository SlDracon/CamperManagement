using CamperManagement.Models;
using CamperManagement.Services;
using CamperManagement.Tests;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
namespace CamperManagement.PdfTests;

public class ContractPartnerTests
{
    private static RechnungDisplayModel Pair()
    {
        var invoice = Data.Invoice();
        invoice.Anrede = "Frau"; invoice.Vorname = "Anna"; invoice.Nachname = "Müller";
        invoice.ZweiteAnrede = "Herr"; invoice.ZweiterVorname = "Alex"; invoice.ZweiterNachname = "Schneider";
        invoice.Straße = "Nebenweg 2"; invoice.PLZ = "01235"; invoice.Ort = "Zweitort";
        return invoice;
    }
    private static string Text(string path)
    {
        using var pdf = new PdfDocument(new PdfReader(path));
        return string.Join("\n", Enumerable.Range(1, pdf.GetNumberOfPages()).Select(i => PdfTextExtractor.GetTextFromPage(pdf.GetPage(i))));
    }
    [Theory]
    [InlineData("Strom")]
    [InlineData("Wasser")]
    public async Task InvoiceHasBothFullNamesAndChosenAddress(string type)
    {
        using var storage = new StorageFixture();
        var invoice = Pair(); invoice.Art = type;
        var path = storage.PathFor("pair.pdf");
        await PdfTests.Export("invoice", storage.Provider(storage.File(path)), [invoice]);
        var text = Text(path);
        Assert.Contains("Anna Müller", text); Assert.Contains("Alex Schneider", text);
        Assert.Contains("Nebenweg 2", text); Assert.Contains("01235 Zweitort", text);
        Assert.DoesNotContain("Testweg", text); Assert.DoesNotContain("Eheleute", text);
        using var pdf = new PdfDocument(new PdfReader(path));
        Assert.Equal(1, pdf.GetNumberOfPages());
        var artifacts = Environment.GetEnvironmentVariable("CAMPER_TEST_ARTIFACTS");
        if (artifacts != null) { Directory.CreateDirectory(artifacts); File.Copy(path, Path.Combine(artifacts, "contract-pair-" + type + ".pdf"), true); }
    }
    [Fact]
    public async Task GroupSummaryAndTableListBothNames()
    {
        using var storage = new StorageFixture();
        var pair = Pair();
        await PdfService.GenerateRechnungenByPlatzAsync(storage.FolderProvider(), [pair], new SilentProgress());
        using var pdf = new PdfDocument(new PdfReader(storage.PathFor("Rechnungen_1.pdf")));
        Assert.Equal(2, pdf.GetNumberOfPages());
        var summary = PdfTextExtractor.GetTextFromPage(pdf.GetPage(2));
        Assert.Contains("Anna Müller", summary); Assert.Contains("Alex Schneider", summary);
        Assert.Contains("Nebenweg 2", summary);
        var path = storage.PathFor("table.pdf");
        await PdfTests.Export("table", storage.Provider(storage.File(path)), [pair]);
        Assert.Contains("Schneider", Text(path));
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GroupSeparatesChangedPartnerOrAddressForSameCamper(bool changeName)
    {
        using var storage = new StorageFixture();
        var first = Pair(); var second = Pair(); second.Id = 2;
        if (changeName) second.ZweiterNachname = "Neumann";
        else second.Straße = "Andere Straße 3";
        await PdfService.GenerateRechnungenByPlatzAsync(storage.FolderProvider(), [first, second], new SilentProgress());
        Assert.Equal(2, Directory.GetFiles(storage.Root).Length);
        var changed = changeName ? "Neumann" : "Andere Straße 3";
        Assert.DoesNotContain(changed, Text(storage.PathFor("Rechnungen_1.pdf")));
        Assert.Contains(changed, Text(storage.PathFor("Rechnungen_1_1.pdf")));
    }
    [Fact]
    public async Task PickerChangesCannotChangeSecondRecipientSnapshot()
    {
        using var storage = new StorageFixture(); var pair = Pair();
        storage.Picking = () => { pair.ZweiterNachname = "MUTATED"; pair.Straße = "MUTATED"; };
        var path = storage.PathFor("snapshot.pdf");
        await PdfTests.Export("invoice", storage.Provider(storage.File(path)), [pair]);
        Assert.Contains("Alex Schneider", Text(path)); Assert.DoesNotContain("MUTATED", Text(path));
    }
    [Fact]
    public async Task LongerNamesRemainCompleteOnInvoice()
    {
        using var storage = new StorageFixture(); var pair = Pair();
        pair.Vorname = "Anna Katharina"; pair.Nachname = "Müller-Lüdenscheidt";
        pair.ZweiterVorname = "Alexander Johannes"; pair.ZweiterNachname = "Schneider-Weiß";
        var path = storage.PathFor("long-names.pdf");
        await PdfTests.Export("invoice", storage.Provider(storage.File(path)), [pair]);
        var text = Text(path);
        Assert.Contains("Müller-", text); Assert.Contains("Lüdenscheidt", text); Assert.Contains("Schneider-Weiß", text);
        Assert.Contains("GENODEF1NSH", text);
        using var pdf = new PdfDocument(new PdfReader(path)); Assert.Equal(1, pdf.GetNumberOfPages());
        var artifacts = Environment.GetEnvironmentVariable("CAMPER_TEST_ARTIFACTS");
        if (artifacts != null) { Directory.CreateDirectory(artifacts); File.Copy(path, Path.Combine(artifacts, "contract-pair-long-names.pdf"), true); }
    }
}

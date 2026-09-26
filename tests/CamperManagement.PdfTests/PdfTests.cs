using Avalonia.Platform.Storage;
using CamperManagement.Models;
using CamperManagement.Services;
using CamperManagement.Tests;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
namespace CamperManagement.PdfTests;

public class PdfTests
{
    public static Task<IStorageFile?> Export(string kind, IStorageProvider? provider, IEnumerable<RechnungDisplayModel>? invoices = null) => kind switch
    {
        "costs" => PdfService.GenerateKostenPdfAsync(provider, 2026, [new KostenEintrag { PlatzNr = "1", Vorname = "Test", Nachname = "Müller", WasserBetrag = 100, StromBetrag = 1.01m, Vertragskosten = 500 }]),
        "readings" => PdfService.GenerateAbleseTabellePdfAsync(provider, [new AbleseEintrag { PlatzNr = "1", Vorname = "Test", Nachname = "Müller", WasserAlt = 1.234m, StromAlt = 2.34m }]),
        "table" => PdfService.GenerateTabellePdfAsync(provider, invoices ?? [Data.Invoice()]),
        _ => PdfService.GenerateAndMergeRechnungenAsync(provider, invoices ?? [Data.Invoice()], new SilentProgress())
    };
    private static string Text(string path)
    {
        using var pdf = new PdfDocument(new PdfReader(path));
        return string.Join("\n", Enumerable.Range(1, pdf.GetNumberOfPages()).Select(i => PdfTextExtractor.GetTextFromPage(pdf.GetPage(i))));
    }
    [Theory]
    [InlineData("costs")]
    [InlineData("readings")]
    [InlineData("table")]
    [InlineData("invoice")]
    public async Task P01_P04_ExactStorageHandleAndUnicodeFilename(string kind)
    {
        using var f = new StorageFixture();
        var path = f.PathFor("Ä Ö % # umbenannt.pdf");
        var file = f.File(path);
        Assert.Same(file, await Export(kind, f.Provider(file)));
        Assert.Contains("Müller", Text(path));
    }
    [Theory]
    [InlineData("costs")]
    [InlineData("readings")]
    [InlineData("table")]
    [InlineData("invoice")]
    public async Task P02_P03_CancelOrNoStorage(string kind)
    {
        using var f = new StorageFixture();
        Assert.Null(await Export(kind, f.Provider(null)));
        Assert.Null(await Export(kind, null));
        Assert.Empty(Directory.GetFiles(f.Root));
    }
    [Theory]
    [InlineData("costs")]
    [InlineData("readings")]
    [InlineData("table")]
    [InlineData("invoice")]
    public async Task P05_OpenWriteFailurePropagates(string kind)
    {
        using var f = new StorageFixture { WriteStream = _ => throw new IOException("denied") };
        await Assert.ThrowsAsync<IOException>(() => Export(kind, f.Provider(f.File(f.PathFor("x.pdf")))));
    }
    [Fact]
    public async Task P06_CostsShowSeparateContractAndCorrectSum()
    {
        using var f = new StorageFixture();
        var path = f.PathFor("costs.pdf");
        await Export("costs", f.Provider(f.File(path)));
        var text = Text(path);
        Assert.Contains("101,01", text);
        Assert.Contains("500", text);
        Assert.DoesNotContain("601,01", text);
    }
    [Fact]
    public async Task P07_TableOnlyRequestedRows()
    {
        using var f = new StorageFixture();
        var path = f.PathFor("table.pdf");
        var r = Data.Invoice();
        r.Platznr = "DistinctPlot";
        await Export("table", f.Provider(f.File(path)), [r]);
        var text = Text(path);
        Assert.Contains("DistinctPlot", text);
        Assert.Contains("Wasser", text);
    }
    [Fact]
    public async Task P08_ReadingsFormat()
    {
        using var f = new StorageFixture();
        var path = f.PathFor("read.pdf");
        await Export("readings", f.Provider(f.File(path)));
        Assert.Contains("1,234", Text(path));
        Assert.Contains("2,34", Text(path));
    }
    [Theory]
    [InlineData("Wasser")]
    [InlineData("Strom")]
    public async Task P09_InvoiceIdentityAmountAndYear(string type)
    {
        using var f = new StorageFixture();
        var path = f.PathFor("invoice.pdf");
        var r = Data.Invoice();
        r.Art = type;
        await Export("invoice", f.Provider(f.File(path)), [r]);
        var text = Text(path);
        Assert.Contains("Müller", text);
        Assert.Contains("Testweg", text);
        Assert.Contains("2026", text);
        Assert.Contains("100", text);
    }
    [Fact]
    public async Task P10_A07_DeepSnapshotBeforePicker()
    {
        using var f = new StorageFixture();
        var path = f.PathFor("merged.pdf");
        var row = Data.Invoice();
        var rows = new List<RechnungDisplayModel> { row, Data.Invoice(2) };
        f.Picking = () => { row.Nachname = "MUTATED"; rows.Clear(); };
        await Export("invoice", f.Provider(f.File(path)), rows);
        Assert.DoesNotContain("MUTATED", Text(path));
        using var pdf = new PdfDocument(new PdfReader(path));
        Assert.Equal(2, pdf.GetNumberOfPages());
    }
    [Fact]
    public async Task P11_P12_GroupPreservesExistingAndAddsSummary()
    {
        using var f = new StorageFixture();
        await File.WriteAllTextAsync(f.PathFor("Rechnungen_1.pdf"), "keep");
        Assert.True(await PdfService.GenerateRechnungenByPlatzAsync(f.FolderProvider(), [Data.Invoice(), Data.Invoice(2)], new SilentProgress()));
        Assert.Equal("keep", await File.ReadAllTextAsync(f.PathFor("Rechnungen_1.pdf")));
        using var pdf = new PdfDocument(new PdfReader(f.PathFor("Rechnungen_1_1.pdf")));
        Assert.Equal(3, pdf.GetNumberOfPages());
        Assert.Contains("200", Text(f.PathFor("Rechnungen_1_1.pdf")));
    }
    [Fact]
    public async Task P02_GroupCancellation()
    {
        using var f = new StorageFixture();
        Assert.False(await PdfService.GenerateRechnungenByPlatzAsync(f.FolderProvider(true), [Data.Invoice()], new SilentProgress()));
        Assert.Empty(Directory.GetFiles(f.Root));
    }
    [Theory]
    [InlineData("../12")]
    [InlineData("A\\B")]
    [InlineData("Ä / Ö : #")]
    [InlineData("")]
    public async Task P13_SafeNames(string place)
    {
        using var f = new StorageFixture();
        var row = Data.Invoice();
        row.Platznr = place;
        Assert.True(await PdfService.GenerateRechnungenByPlatzAsync(f.FolderProvider(), [row], new SilentProgress()));
        Assert.Single(Directory.GetFiles(f.Root));
    }
    [Fact]
    public async Task P14_P15_TemporaryCleanup()
    {
        using var f = new StorageFixture();
        var old = Environment.GetEnvironmentVariable("TMPDIR");
        Environment.SetEnvironmentVariable("TMPDIR", f.Root);
        try
        {
            await Export("invoice", f.Provider(f.File(f.PathFor("ok.pdf"))));
            Assert.Empty(Directory.GetFiles(f.Root, "Rechnung_*.pdf"));
            f.WriteStream = _ => new BrokenStream();
            await Assert.ThrowsAnyAsync<Exception>(() => Export("invoice", f.Provider(f.File(f.PathFor("fail.pdf")))));
            Assert.Empty(Directory.GetFiles(f.Root, "Rechnung_*.pdf"));
        }
        finally { Environment.SetEnvironmentVariable("TMPDIR", old); }
    }
    [Fact]
    public async Task P16_EmptyInvoiceExportRejected()
    {
        using var f = new StorageFixture();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Export("invoice", f.Provider(null), []));
    }
    [Fact]
    public async Task P16_LargeTableHasAllRows()
    {
        using var f = new StorageFixture();
        var rows = Enumerable.Range(1, 120).Select(i => { var r = Data.Invoice(i); r.Platznr = $"Unique{i:000}"; return r; }).ToArray();
        var path = f.PathFor("large.pdf");
        await Export("table", f.Provider(f.File(path)), rows);
        var text = Text(path);
        Assert.Contains("Unique001", text);
        Assert.Contains("Unique120", text);
        using var pdf = new PdfDocument(new PdfReader(path));
        Assert.True(pdf.GetNumberOfPages() > 1);
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task P17_LauncherUsesExactContentHandle(bool success)
    {
        var file = StubProxy.Create<IStorageFile>((m, _) => m == "get_Path" ? new Uri("content://test/document") : throw new InvalidOperationException(m));
        var calls = 0;
        var launcher = StubProxy.Create<ILauncher>((m, args) => { Assert.Equal("LaunchFileAsync", m); Assert.Same(file, args![0]); calls++; return Task.FromResult(success); });
        Assert.Equal(success, await PdfService.OpenPdfAsync(file, launcher));
        Assert.False(await PdfService.OpenPdfAsync(null, launcher));
        Assert.Equal(1, calls);
    }
    [Fact]
    public async Task P17_AbsentAndFailingViewer()
    {
        Assert.False(await PdfService.OpenPdfAsync(Data.FileHandle(), null));
        var launcher = StubProxy.Create<ILauncher>((_, _) => throw new IOException());
        Assert.False(await PdfService.OpenPdfAsync(Data.FileHandle(), launcher));
    }

    [Theory]
    [InlineData("write")]
    [InlineData("flush")]
    [InlineData("close")]
    public async Task P05_WriteFlushCloseErrorsPropagate(string phase)
    {
        using var f = new StorageFixture { WriteStream = _ => new FailingStream(phase) };
        await Assert.ThrowsAnyAsync<Exception>(() => Export("invoice", f.Provider(f.File(f.PathFor("x.pdf")))));
    }
    [Fact]
    public async Task P11_DifferentHistoricalRecipientsGetSeparateDocuments()
    {
        using var f = new StorageFixture();
        var a = Data.Invoice();
        var b = Data.Invoice(2);
        b.CamperId = 2;
        b.Nachname = "Different";
        await PdfService.GenerateRechnungenByPlatzAsync(f.FolderProvider(), [a, b], new SilentProgress());
        Assert.Equal(2, Directory.GetFiles(f.Root).Length);
        Assert.DoesNotContain("Different", Text(f.PathFor("Rechnungen_1.pdf")));
        Assert.Contains("Different", Text(f.PathFor("Rechnungen_1_1.pdf")));
    }
    [Fact]
    public async Task P18_LayoutSamples()
    {
        using var f = new StorageFixture();
        foreach (var kind in new[] { "invoice", "costs", "readings", "table" })
        {
            var path = f.PathFor(kind + ".pdf");
            await Export(kind, f.Provider(f.File(path)));
            using (var pdf = new PdfDocument(new PdfReader(path)))
            {
                var size = pdf.GetPage(1).GetPageSize();
                Assert.Equal(kind == "invoice" ? 420f : 595f, size.GetWidth(), 0);
                Assert.Equal(kind == "invoice" ? 595f : 842f, size.GetHeight(), 0);
                Assert.NotEmpty(Text(path));
            }
            var artifacts = Environment.GetEnvironmentVariable("CAMPER_TEST_ARTIFACTS");
            if (artifacts != null)
            {
                Directory.CreateDirectory(artifacts);
                File.Copy(path, Path.Combine(artifacts, kind + ".pdf"), true);
            }
        }
    }

    [Theory]
    [InlineData("costs")]
    [InlineData("readings")]
    [InlineData("table")]
    [InlineData("invoice")]
    public async Task P05_FailedExportReleasesSelectedHandle(string kind)
    {
        var disposed = false;
        var file = StubProxy.Create<IStorageFile>((m, _) => { if (m == "Dispose") { disposed = true; return null; } if (m == "OpenWriteAsync") throw new IOException("denied"); throw new InvalidOperationException(m); });
        using var storage = new StorageFixture();
        await Assert.ThrowsAsync<IOException>(() => Export(kind, storage.Provider(file)));
        Assert.True(disposed);
    }
    [Theory]
    [InlineData(1000)]
    [InlineData(10000)]
    public async Task P16_PerformanceSampleWithoutMachineSpecificTimeLimit(int count)
    {
        using var f = new StorageFixture();
        var rows = Enumerable.Range(1, count).Select(i => { var row = Data.Invoice(i); row.Platznr = $"Plot{i:00000}"; return row; }).ToArray();
        var path = f.PathFor("large.pdf");
        await Export("table", f.Provider(f.File(path)), rows);
        var text = Text(path);
        Assert.Contains("Plot00001", text);
        Assert.Contains($"Plot{count:00000}", text);
    }
    private sealed class FailingStream(string phase) : MemoryStream
    {
        public override void Write(byte[] b, int o, int c)
        {
            if (phase == "write")
                throw new IOException("disk full");
            base.Write(b, o, c);
        }
        public override void Flush()
        {
            if (phase == "flush")
                throw new IOException("flush failed");
            base.Flush();
        }
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (phase == "close")
                throw new IOException("close failed");
        }
    }
    private sealed class BrokenStream : MemoryStream
    {
        public override void Write(byte[] buffer, int offset, int count) => throw new IOException("disk full"); public override void Write(ReadOnlySpan<byte> buffer) => throw new IOException("disk full");
    }
}

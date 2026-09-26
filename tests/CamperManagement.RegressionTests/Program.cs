using System.Reflection;
using Avalonia.Platform.Storage;
using CamperManagement.Models;
using CamperManagement.Services;
using iText.Kernel.Pdf;

// No UI, printer, or live database is used. All files live in an isolated temporary directory.
var root = Directory.CreateTempSubdirectory("CamperManagement-PDF-").FullName;
var oldTemp = Environment.GetEnvironmentVariable("TMPDIR");
Environment.SetEnvironmentVariable("TMPDIR", root);
try
{
    var invoice = new RechnungDisplayModel
    {
        Id = 1, Platznr = "12", Vorname = "Test", Nachname = "Müller", Anrede = "Herr",
        Straße = "Testweg 1", PLZ = "12345", Ort = "Testort", Art = "Wasser",
        Jahr = 2026, Alt = 1, Neu = 3, Verbrauch = 2, Faktor = 4, Betrag = 8
    };
    var exports = new (string Name, Func<IStorageProvider, Task<IStorageFile?>> Run)[]
    {
        ("Kosten", p => PdfService.GenerateKostenPdfAsync(p, 2026,
            [new KostenEintrag { PlatzNr = "12", Vorname = "Test", Nachname = "Müller" }])),
        ("Tabelle", p => PdfService.GenerateTabellePdfAsync(p, [invoice])),
        ("Ablesetabelle", p => PdfService.GenerateAbleseTabellePdfAsync(p,
            [new AbleseEintrag { PlatzNr = "12", Vorname = "Test", Nachname = "Müller" }])),
        ("Rechnungen", p => PdfService.GenerateAndMergeRechnungenAsync(p, [invoice, invoice], new SilentProgress()))
    };
    foreach (var (name, export) in exports)
    {
        var path = Path.Combine(root, $"Vom Benutzer umbenannt – Ä Ö % # {name}.pdf");
        var selectedFile = StorageFile(path);
        var callerThread = Environment.CurrentManagedThreadId;
        var provider = Stub<IStorageProvider>((method, args) => method switch
        {
            "SaveFilePickerAsync" => Choose(),
            _ => throw new InvalidOperationException("Unexpected storage call: " + method)
        });
        Task<IStorageFile?> Choose()
        {
            Check(Environment.CurrentManagedThreadId == callerThread, "Save dialog moved off the calling thread");
            return Task.FromResult<IStorageFile?>(selectedFile);
        }
        var result = await export(provider);
        Check(ReferenceEquals(result, selectedFile), "The selected file must be returned unchanged");
        using (var pdf = new PdfDocument(new PdfReader(path)))
            Check(pdf.GetNumberOfPages() >= (name == "Rechnungen" ? 2 : 1), "Missing PDF pages");
        Console.WriteLine($"PASS {name}: selected name, Unicode path, readable PDF, dialog thread");

        var cancelled = Stub<IStorageProvider>((method, _) => method == "SaveFilePickerAsync"
            ? Task.FromResult<IStorageFile?>(null) : throw new InvalidOperationException(method));
        Check(await export(cancelled) == null, "Cancellation must not return a successful export");
        Console.WriteLine($"PASS {name}: cancellation");
    }

    // Changing the live selection while the picker is open must not change the exported invoices.
    var selection = new List<RechnungDisplayModel> { invoice, invoice };
    var snapshotPath = Path.Combine(root, "snapshot.pdf");
    var snapshotFile = StorageFile(snapshotPath);
    var snapshotProvider = Stub<IStorageProvider>((method, _) =>
    {
        Check(method == "SaveFilePickerAsync", "Unexpected call");
        selection.Clear();
        return Task.FromResult<IStorageFile?>(snapshotFile);
    });
    await PdfService.GenerateAndMergeRechnungenAsync(snapshotProvider, selection, new SilentProgress());
    using (var pdf = new PdfDocument(new PdfReader(snapshotPath)))
        Check(pdf.GetNumberOfPages() >= 2, "Export must use its initial selection");
    Check(!Directory.EnumerateFiles(root, "Rechnung_*.pdf").Any(), "Temporary PDFs leaked after successful export");
    Console.WriteLine("PASS selection snapshot and temporary file cleanup");

    // A failure after creating the temporary PDF must remove both complete and incomplete temp files.
    var failureProvider = Stub<IStorageProvider>((method, _) => method == "SaveFilePickerAsync"
        ? Task.FromResult<IStorageFile?>(StorageFile(Path.Combine(root, "failed.pdf")))
        : throw new InvalidOperationException(method));
    var threw = false;
    try
    {
        await PdfService.GenerateAndMergeRechnungenAsync(failureProvider, [invoice, null!], new SilentProgress());
    }
    catch (NullReferenceException) { threw = true; }
    Check(threw, "Rendering failure must propagate instead of reporting success");
    Check(!Directory.EnumerateFiles(root, "Rechnung_*.pdf").Any(), "Temporary PDFs leaked after failed export");
    Console.WriteLine("PASS rendering failure and temporary file cleanup");

    // Group exports must use storage APIs and preserve an existing file with the same name.
    var existingPath = Path.Combine(root, "Rechnungen_12.pdf");
    await File.WriteAllTextAsync(existingPath, "existing file must survive");
    var groupFolder = Stub<IStorageFolder>((method, args) => method switch
    {
        "GetFileAsync" => Task.FromResult<IStorageFile?>(File.Exists(Path.Combine(root, (string)args![0]!))
            ? StorageFile(Path.Combine(root, (string)args[0]!)) : null),
        "CreateFileAsync" => Task.FromResult<IStorageFile?>(StorageFile(Path.Combine(root, (string)args![0]!))),
        "Dispose" => null,
        _ => throw new InvalidOperationException("A storage folder need not expose a local path")
    });
    var groupProvider = Stub<IStorageProvider>((method, _) => method == "OpenFolderPickerAsync"
        ? Task.FromResult<IReadOnlyList<IStorageFolder>>([groupFolder])
        : throw new InvalidOperationException(method));
    Check(await PdfService.GenerateRechnungenByPlatzAsync(groupProvider, [invoice, invoice], new SilentProgress()),
        "Group export failed");
    Check(await File.ReadAllTextAsync(existingPath) == "existing file must survive", "Existing file overwritten");
    using (var pdf = new PdfDocument(new PdfReader(Path.Combine(root, "Rechnungen_12_1.pdf"))))
        Check(pdf.GetNumberOfPages() >= 3, "Group must contain invoices and summary");
    Check(!Directory.EnumerateFiles(root, "Rechnung_*.pdf").Any(), "Group export leaked temporary files");
    var cancelledFolderProvider = Stub<IStorageProvider>((method, _) => method == "OpenFolderPickerAsync"
        ? Task.FromResult<IReadOnlyList<IStorageFolder>>([])
        : throw new InvalidOperationException(method));
    Check(!await PdfService.GenerateRechnungenByPlatzAsync(cancelledFolderProvider, [invoice], new SilentProgress()),
        "Cancelled folder export must not report success");
    Console.WriteLine("PASS group export, duplicate filename, summary, cleanup and folder cancellation");

    // Opening must pass the exact storage object, even when it has a non-file URI.
    var contentFile = Stub<IStorageFile>((method, _) => method switch
    {
        "get_Path" => new Uri("content://test/documents/renamed%20invoice.pdf"),
        "Dispose" => null,
        _ => throw new InvalidOperationException("Must not interpret a storage handle as a filesystem path")
    });
    var launches = 0;
    var launcher = Stub<ILauncher>((method, args) =>
    {
        Check(method == "LaunchFileAsync", "Expected file launcher");
        Check(ReferenceEquals(args![0], contentFile), "Wrong file opened");
        launches++;
        return Task.FromResult(true);
    });
    await PdfService.OpenPdfAsync(contentFile, launcher);
    await PdfService.OpenPdfAsync(null, launcher);
    Check(launches == 1, "Cancelled export must not launch a file");
    Console.WriteLine("PASS exact file passed to launcher; no launch after cancellation");
    // Run with a single-thread UI context: storage calls stay on it while slow PDF writes do not.
    UiTestContext.Run(async ui =>
    {
        IStorageFile ResponsiveFile(string path) => Stub<IStorageFile>((method, _) =>
        {
            ui.AssertCurrent();
            return method switch
            {
                "OpenWriteAsync" => Task.FromResult<Stream>(new ResponsiveWriteStream(File.Create(path), ui)),
                "Dispose" => null,
                _ => throw new InvalidOperationException(method)
            };
        });
        foreach (var (name, export) in exports)
        {
            var path = Path.Combine(root, $"responsive-{name}.pdf");
            var file = ResponsiveFile(path);
            var provider = Stub<IStorageProvider>((method, _) =>
            {
                ui.AssertCurrent();
                return method == "SaveFilePickerAsync" ? Task.FromResult<IStorageFile?>(file)
                    : throw new InvalidOperationException(method);
            });
            Check(await export(provider) != null, "Export failed");
            Console.WriteLine($"PASS {name}: UI processes callbacks during slow PDF writes");
        }
        var statusCallbacks = 0;
        var progress = new Progress<string?>(_ => { ui.AssertCurrent(); statusCallbacks++; });
        var folder = Stub<IStorageFolder>((method, _) =>
        {
            ui.AssertCurrent();
            return method switch
            {
                "GetFileAsync" => Task.FromResult<IStorageFile?>(null),
                "CreateFileAsync" => Task.FromResult<IStorageFile?>(ResponsiveFile(Path.Combine(root, "responsive-group.pdf"))),
                "Dispose" => null,
                _ => throw new InvalidOperationException(method)
            };
        });
        var folders = Stub<IStorageProvider>((method, _) =>
        {
            ui.AssertCurrent();
            return method == "OpenFolderPickerAsync" ? Task.FromResult<IReadOnlyList<IStorageFolder>>([folder])
                : throw new InvalidOperationException(method);
        });
        Check(await PdfService.GenerateRechnungenByPlatzAsync(folders, [invoice, invoice], progress), "Group export failed");
        Check(statusCallbacks > 0, "Expected progress on the UI context");
        Console.WriteLine("PASS grouped export: responsive UI, storage and progress stay on UI context");
    });
    Console.WriteLine("All PDF regression checks passed.");
}
finally
{
    Environment.SetEnvironmentVariable("TMPDIR", oldTemp);
    Directory.Delete(root, recursive: true);
}

static IStorageFile StorageFile(string path) => Stub<IStorageFile>((method, _) => method switch
{
    "OpenWriteAsync" => Task.FromResult<Stream>(File.Create(path)),
    "get_Path" => new Uri(path),
    "get_Name" => Path.GetFileName(path),
    "Dispose" => null,
    _ => throw new InvalidOperationException("Unexpected file call: " + method)
});

static T Stub<T>(Func<string, object?[]?, object?> handler) where T : class
{
    var instance = DispatchProxy.Create<T, StorageStub>();
    ((StorageStub)(object)instance).Handler = handler;
    return instance;
}

static void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

public class StorageStub : DispatchProxy
{
    public Func<string, object?[]?, object?> Handler { get; set; } = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!.Name, args);
}

sealed class SilentProgress : IProgress<string?>
{
    public void Report(string? value) { }
}

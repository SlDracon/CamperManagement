using System.Threading;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CamperManagement.Models;
namespace CamperManagement.Services;

public interface IPdfFile : IDisposable { }

public interface IPdfExporter
{
    Task<IPdfFile?> InvoicesAsync(IReadOnlyList<RechnungDisplayModel> rows, IProgress<string?> progress, CancellationToken cancellationToken = default);
    Task<bool> ByPlatzAsync(IReadOnlyList<RechnungDisplayModel> rows, IProgress<string?> progress, CancellationToken cancellationToken = default);
    Task<IPdfFile?> TableAsync(IReadOnlyList<RechnungDisplayModel> rows, CancellationToken cancellationToken = default);
    Task<IPdfFile?> ReadingsAsync(IReadOnlyList<AbleseEintrag> rows, CancellationToken cancellationToken = default);
    Task<IPdfFile?> CostsAsync(int year, IReadOnlyList<KostenEintrag> rows, CancellationToken cancellationToken = default);
    Task<bool> OpenAsync(IPdfFile? file);
}
public sealed class PdfExporter : IPdfExporter
{
    public static IPdfExporter Unavailable { get; } = new PdfExporter(() => null);
    private sealed class StoragePdfFile(IStorageFile file) : IPdfFile
    {
        public IStorageFile File { get; } = file;
        public void Dispose() => File.Dispose();
    }
    private static async Task<IPdfFile?> WrapAsync(Task<IStorageFile?> pending)
    {
        var file = await pending;
        return file == null ? null : new StoragePdfFile(file);
    }
    private readonly Func<TopLevel?> _topLevel;
    private readonly IErrorLog _log;
    public PdfExporter(Func<TopLevel?> topLevel, IErrorLog? log = null) { _topLevel = topLevel; _log = log ?? NullErrorLog.Instance; }
    private IStorageProvider Storage => _topLevel()?.StorageProvider ?? throw new InvalidOperationException("Speicherziel nicht verfügbar.");
    public Task<IPdfFile?> InvoicesAsync(IReadOnlyList<RechnungDisplayModel> rows, IProgress<string?> progress, CancellationToken cancellationToken = default) => WrapAsync(PdfService.GenerateAndMergeRechnungenAsync(Storage, rows, progress, cancellationToken));
    public Task<bool> ByPlatzAsync(IReadOnlyList<RechnungDisplayModel> rows, IProgress<string?> progress, CancellationToken cancellationToken = default) => PdfService.GenerateRechnungenByPlatzAsync(Storage, rows, progress, cancellationToken);
    public Task<IPdfFile?> TableAsync(IReadOnlyList<RechnungDisplayModel> rows, CancellationToken cancellationToken = default) => WrapAsync(PdfService.GenerateTabellePdfAsync(Storage, rows, cancellationToken));
    public Task<IPdfFile?> ReadingsAsync(IReadOnlyList<AbleseEintrag> rows, CancellationToken cancellationToken = default) => WrapAsync(PdfService.GenerateAbleseTabellePdfAsync(Storage, rows, cancellationToken));
    public Task<IPdfFile?> CostsAsync(int year, IReadOnlyList<KostenEintrag> rows, CancellationToken cancellationToken = default) => WrapAsync(PdfService.GenerateKostenPdfAsync(Storage, year, rows, cancellationToken));
    public Task<bool> OpenAsync(IPdfFile? file) => PdfService.OpenPdfAsync((file as StoragePdfFile)?.File, _topLevel()?.Launcher, _log);
}

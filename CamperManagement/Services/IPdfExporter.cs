using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CamperManagement.Models;
namespace CamperManagement.Services;

public interface IPdfExporter
{
    Task<IStorageFile?> InvoicesAsync(IReadOnlyList<RechnungDisplayModel> rows, IProgress<string?> progress);
    Task<bool> ByPlatzAsync(IReadOnlyList<RechnungDisplayModel> rows, IProgress<string?> progress);
    Task<IStorageFile?> TableAsync(IReadOnlyList<RechnungDisplayModel> rows);
    Task<IStorageFile?> ReadingsAsync(IReadOnlyList<AbleseEintrag> rows);
    Task<IStorageFile?> CostsAsync(int year, IReadOnlyList<KostenEintrag> rows);
    Task<bool> OpenAsync(IStorageFile? file);
}
public sealed class PdfExporter : IPdfExporter
{
    private static TopLevel? Window => Application.Current?.ApplicationLifetime switch
    {
        IClassicDesktopStyleApplicationLifetime d => d.MainWindow,
        ISingleViewApplicationLifetime s when s.MainView != null => TopLevel.GetTopLevel(s.MainView),
        _ => null
    };
    private static IStorageProvider Storage => Window?.StorageProvider ?? throw new InvalidOperationException("Speicherziel nicht verfügbar.");
    public Task<IStorageFile?> InvoicesAsync(IReadOnlyList<RechnungDisplayModel> rows, IProgress<string?> progress) => PdfService.GenerateAndMergeRechnungenAsync(Storage, rows, progress);
    public Task<bool> ByPlatzAsync(IReadOnlyList<RechnungDisplayModel> rows, IProgress<string?> progress) => PdfService.GenerateRechnungenByPlatzAsync(Storage, rows, progress);
    public Task<IStorageFile?> TableAsync(IReadOnlyList<RechnungDisplayModel> rows) => PdfService.GenerateTabellePdfAsync(Storage, rows);
    public Task<IStorageFile?> ReadingsAsync(IReadOnlyList<AbleseEintrag> rows) => PdfService.GenerateAbleseTabellePdfAsync(Storage, rows);
    public Task<IStorageFile?> CostsAsync(int year, IReadOnlyList<KostenEintrag> rows) => PdfService.GenerateKostenPdfAsync(Storage, year, rows);
    public Task<bool> OpenAsync(IStorageFile? file) => PdfService.OpenPdfAsync(file, Window?.Launcher);
}

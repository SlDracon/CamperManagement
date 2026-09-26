using CamperManagement.Services;
using CamperManagement.Tests;
namespace CamperManagement.PdfTests;
public class CancellationTests
{
    [Fact]
    public async Task CancelledExportDoesNotOpenPickerOrCreateFile()
    {
        using var storage = new StorageFixture();
        var picker = false;
        storage.Picking = () => picker = true;
        var token = new CancellationToken(true);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => PdfService.GenerateAndMergeRechnungenAsync(storage.Provider(null), [Data.Invoice()], new SilentProgress(), token));
        Assert.False(picker);
        Assert.Empty(Directory.GetFiles(storage.Root));
    }
    [Fact]
    public async Task CancellationAfterPickerDoesNotOverwriteExistingFile()
    {
        using var storage = new StorageFixture();
        using var source = new CancellationTokenSource();
        var path = storage.PathFor("existing.pdf");
        File.WriteAllText(path, "existing content");
        storage.Picking = () => source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => PdfService.GenerateTabellePdfAsync(storage.Provider(storage.File(path)), [Data.Invoice()], source.Token));
        Assert.Equal("existing content", File.ReadAllText(path));
    }
    [Fact]
    public async Task CancellationBetweenInvoicesStopsMerge()
    {
        using var storage = new StorageFixture();
        using var source = new CancellationTokenSource();
        var count = 0;
        var progress = new InlineProgress(_ => { if (++count == 1) source.Cancel(); });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => PdfService.GenerateAndMergeRechnungenAsync(storage.Provider(storage.File(storage.PathFor("cancel.pdf"))), [Data.Invoice(), Data.Invoice(2)], progress, source.Token));
        Assert.Equal(1, count);
    }
    private sealed class InlineProgress(Action<string?> report) : IProgress<string?>
    { public void Report(string? value) => report(value); }
}

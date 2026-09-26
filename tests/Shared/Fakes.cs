using System.Threading;
using System.Reflection;
using Avalonia.Platform.Storage;
using CamperManagement.Models;
using CamperManagement.Services;
namespace CamperManagement.Tests;

public class StubProxy : DispatchProxy
{
    public Func<string, object?[]?, object?> Handler { get; set; } = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!.Name, args);
    public static T Create<T>(Func<string, object?[]?, object?> handler) where T : class
    {
        var obj = Create<T, StubProxy>();
        ((StubProxy)(object)obj).Handler = handler;
        return obj;
    }
}
public sealed class FixedClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Now;
    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
}
public class FakeDatabase : IDatabaseService
{
    public int Calls, Adds, Updates, Deactivations;
    public Exception? SaveError;
    public Rechnung? SavedInvoice;
    public CamperDisplayModel? SavedCamper;
    public List<int> Printed = new();
    public List<RechnungDisplayModel> Invoices = new();
    public List<CamperDisplayModel> Campers = new();
    public List<string> Places = new() { "1", "2" };
    public Standardfaktoren Factors = new(0.5m, 8m, 1);
    public Func<string?, string?, Task<decimal>> Reading = (_, _) => Task.FromResult(10m);
    public Func<Task<List<RechnungDisplayModel>>>? InvoiceLoader;
    public Func<Task<Standardfaktoren>>? FactorLoader;
    public Func<Rechnung, Task>? Insert;
    public Func<IReadOnlyCollection<int>, Task>? Mark;
    private void Save()
    {
        if (SaveError != null)
            throw SaveError;
    }
    public Task<List<CamperDisplayModel>> GetActiveCampersAsync(CancellationToken cancellationToken = default)
    {
        Calls++;
        return Task.FromResult(Campers.ToList());
    }
    public Task<List<RechnungDisplayModel>> GetRechnungenAsync(CancellationToken cancellationToken = default)
    {
        Calls++;
        return InvoiceLoader?.Invoke() ?? Task.FromResult(Invoices.ToList());
    }
    public Task<List<string>> GetPlatznummernAsync(CancellationToken cancellationToken = default)
    {
        Calls++;
        return Task.FromResult(Places.ToList());
    }
    public Task DeactivateOldCamperAsync(string? p)
    {
        Deactivations++;
        return Task.CompletedTask;
    }
    public Task AddNewCamperAsync(CamperDisplayModel c)
    {
        Save();
        Adds++;
        SavedCamper = c.Snapshot();
        return Task.CompletedTask;
    }
    public Task UpdateCamperAsync(CamperDisplayModel c)
    {
        Save();
        Updates++;
        SavedCamper = c.Snapshot();
        return Task.CompletedTask;
    }
    public Task<List<int>> GetAvailableJahreAsync(CancellationToken cancellationToken = default) => Task.FromResult(new List<int> { 2025, 2026 });
    public Task<List<KostenEintrag>> GetRechnungenForJahrAsync(int y, CancellationToken cancellationToken = default) => Task.FromResult(new List<KostenEintrag>());
    public Task MarkRechnungAsPrintedAsync(int id) => MarkRechnungenAsPrintedAsync(new[] { id });
    public async Task MarkRechnungenAsPrintedAsync(IReadOnlyCollection<int> ids)
    {
        if (Mark != null)
            await Mark(ids);
        Save();
        Printed.AddRange(ids);
    }
    public async Task AddRechnungAsync(Rechnung r)
    {
        if (Insert != null)
            await Insert(r);
        Save();
        Adds++;
        SavedInvoice = r;
    }
    public Task<int> GetPlatzIdByPlatznummerAsync(string? p, CancellationToken cancellationToken = default) => Task.FromResult(Places.Contains(p ?? "") ? Places.IndexOf(p!) + 1 : throw new ArgumentException("Unbekannter Platz"));
    public Task<decimal> GetNeuFromLatestRechnungAsync(string? p, string? t, CancellationToken cancellationToken = default) => Reading(p, t);
    public Task<List<AbleseEintrag>> GetAbleseTabelleAsync(CancellationToken cancellationToken = default) => Task.FromResult(new List<AbleseEintrag>());
    public Task UpdateRechnungAsync(Rechnung r)
    {
        Save();
        Updates++;
        SavedInvoice = r;
        return Task.CompletedTask;
    }
    public Task<Standardfaktoren> GetStandardfaktorenAsync(CancellationToken cancellationToken = default)
    {
        Calls++;
        return FactorLoader?.Invoke() ?? Task.FromResult(Factors);
    }
    public Task SaveStandardfaktorenAsync(Standardfaktoren f)
    {
        Save();
        if (Factors.Version != f.Version)
            throw new InvalidOperationException("Faktoren wurden inzwischen geändert.");
        Factors = f with { Version = f.Version + 1 };
        return Task.CompletedTask;
    }

}
public class FakePdf : IPdfExporter
{
    private sealed class FakeFile(IStorageFile file) : IPdfFile { public void Dispose() => file.Dispose(); }
    private static async Task<IPdfFile?> Wrap(Task<IStorageFile?> task) { var file = await task; return file == null ? null : new FakeFile(file); }
    public Func<IReadOnlyList<RechnungDisplayModel>, Task<IStorageFile?>> Export = _ => Task.FromResult<IStorageFile?>(null);
    public Func<IReadOnlyList<RechnungDisplayModel>, Task<bool>> Group = _ => Task.FromResult(false);
    public Func<IReadOnlyList<RechnungDisplayModel>, IProgress<string?>, Task<IStorageFile?>>? ExportWithProgress;
    public bool Opens = true; public int OpenCount;
    public Task<IPdfFile?> InvoicesAsync(IReadOnlyList<RechnungDisplayModel> r, IProgress<string?> p, CancellationToken cancellationToken = default) => Wrap(ExportWithProgress?.Invoke(r, p) ?? Export(r));
    public Task<bool> ByPlatzAsync(IReadOnlyList<RechnungDisplayModel> r, IProgress<string?> p, CancellationToken cancellationToken = default) => Group(r);
    public Task<IPdfFile?> TableAsync(IReadOnlyList<RechnungDisplayModel> r, CancellationToken cancellationToken = default) => Wrap(Export(r));
    public Task<IPdfFile?> ReadingsAsync(IReadOnlyList<AbleseEintrag> r, CancellationToken cancellationToken = default) => Task.FromResult<IPdfFile?>(null);
    public Task<IPdfFile?> CostsAsync(int y, IReadOnlyList<KostenEintrag> r, CancellationToken cancellationToken = default) => Task.FromResult<IPdfFile?>(null);
    public Task<bool> OpenAsync(IPdfFile? file)
    {
        OpenCount++;
        return Task.FromResult(Opens);
    }
}
public static class Data
{
    public static RechnungDisplayModel Invoice(int id = 1) => new() { Id = id, Platznr = "1", CamperId = 1, Art = "Wasser", Jahr = 2026, Alt = 100, Neu = 112.5m, Verbrauch = 12.5m, Faktor = 8, Betrag = 100, Vorname = "Test", Nachname = "Müller", Anrede = "Herr", Straße = "Testweg 1", PLZ = "01234", Ort = "Testort", Gedruckt = "Nein" };
    public static CamperDisplayModel Camper() => new() { Id = 1, Platznr = "1", Vorname = "Test", Nachname = "Müller", Straße = "Testweg 1", PLZ = "01234", Ort = "Testort" };
    public static IStorageFile FileHandle() => StubProxy.Create<IStorageFile>((m, _) => m == "Dispose" ? null : throw new InvalidOperationException(m));
}

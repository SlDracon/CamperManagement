using Avalonia.Platform.Storage;
namespace CamperManagement.Tests;

public sealed class StorageFixture : IDisposable
{
    public string Root { get; } = Directory.CreateTempSubdirectory("camper_pdf_test_").FullName;
    public string PathFor(string name) => Path.Combine(Root, name);
    public Func<string, Stream>? WriteStream;
    public Action? Picking;
    public IStorageFile File(string path) => StubProxy.Create<IStorageFile>((m, _) => m switch
    {
        "OpenWriteAsync" => Task.FromResult(WriteStream?.Invoke(path) ?? System.IO.File.Create(path)),
        "get_Path" => new Uri(path),
        "get_Name" => Path.GetFileName(path),
        "Dispose" => null,
        _ => throw new InvalidOperationException(m)
    });
    public IStorageProvider Provider(IStorageFile? file) => StubProxy.Create<IStorageProvider>((m, _) => { if (m != "SaveFilePickerAsync") throw new InvalidOperationException(m); Picking?.Invoke(); return Task.FromResult(file); });
    public IStorageProvider FolderProvider(bool cancel = false) => StubProxy.Create<IStorageProvider>((m, _) => m == "OpenFolderPickerAsync" ? Task.FromResult<IReadOnlyList<IStorageFolder>>(cancel ? [] : [Folder()]) : throw new InvalidOperationException(m));
    private IStorageFolder Folder() => StubProxy.Create<IStorageFolder>((m, args) => m switch
    {
        "GetFileAsync" => Task.FromResult<IStorageFile?>(System.IO.File.Exists(PathFor((string)args![0]!)) ? File(PathFor((string)args![0]!)) : null),
        "CreateFileAsync" => Task.FromResult<IStorageFile?>(File(PathFor((string)args![0]!))),
        "Dispose" => null,
        _ => throw new InvalidOperationException(m)
    });
    public void Dispose()
    {
        Directory.Delete(Root, true);
    }
}
public sealed class SilentProgress : IProgress<string?>
{
    public void Report(string? text)
    {
    }
}

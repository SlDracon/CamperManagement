using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace CamperManagement.Services;

public enum ErrorOperation { Save, Load, Export, OpenPdf, Cleanup, Connection }
public interface IErrorLog { void Write(ErrorOperation operation, Exception error); }
public sealed class NullErrorLog : IErrorLog
{
    public static readonly IErrorLog Instance = new NullErrorLog();
    public void Write(ErrorOperation operation, Exception error) { }
}

/// <summary>Two bounded files. No exception messages, values, SQL, paths or user data.</summary>
public sealed class LocalErrorLog : IErrorLog
{
    private readonly string _directory;
    private readonly int _maxBytes;
    private readonly object _gate = new();
    public LocalErrorLog(string directory, int maxBytes = 262144)
    {
        _directory = directory;
        _maxBytes = Math.Max(4096, maxBytes);
    }
    public void Write(ErrorOperation operation, Exception error)
    {
        try
        {
            var line = JsonSerializer.Serialize(new
            {
                time = DateTimeOffset.UtcNow,
                operation = operation.ToString(),
                type = error.GetType().FullName,
                error.HResult,
                cause = error.InnerException?.GetType().FullName,
                // Method identities only: a regular StackTrace can expose local paths.
                frames = new StackTrace(error, false).GetFrames()?.Take(12)
                    .Select(f => f.GetMethod()).Select(m => $"{m?.DeclaringType?.FullName}.{m?.Name}")
            }) + Environment.NewLine;
            lock (_gate)
            {
                Directory.CreateDirectory(_directory);
                if (!OperatingSystem.IsWindows())
                    File.SetUnixFileMode(_directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                var path = Path.Combine(_directory, "errors.jsonl");
                if (File.Exists(path) && new FileInfo(path).Length + System.Text.Encoding.UTF8.GetByteCount(line) > _maxBytes)
                    File.Move(path, Path.Combine(_directory, "errors.previous.jsonl"), true);
                var options = new FileStreamOptions { Mode = FileMode.Append, Access = FileAccess.Write, Share = FileShare.Read };
                if (!OperatingSystem.IsWindows())
                    options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
                using var stream = new FileStream(path, options);
                using var writer = new StreamWriter(stream);
                writer.Write(line);
            }
        }
        catch (Exception) { /* Logging must never interrupt saving or mask the original failure. */ }
    }
}

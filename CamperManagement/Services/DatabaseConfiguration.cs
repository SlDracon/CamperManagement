using System;
using System.IO;
using System.Text.Json;
using MySqlConnector;

namespace CamperManagement.Services;

public sealed class DatabaseConfiguration
{
    public static DatabaseConfiguration Current { get; } = new();
    private readonly string _path;
    private readonly Func<string?> _environment;
    public bool IsEnvironmentOverride => _environment() != null;
    public DatabaseConfiguration(string? path = null, Func<string?>? environment = null)
    {
        _path = path ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CamperManagement", "database.json");
        _environment = environment ?? (() => Environment.GetEnvironmentVariable("CAMPER_DB_CONNECTION"));
    }
    public string? Load()
    {
        var configured = _environment();
        if (configured != null)
            return configured;
        try
        {
            return File.Exists(_path) ? JsonSerializer.Deserialize<Settings>(File.ReadAllText(_path))?.ConnectionString : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null; // The setup screen lets the user repair an unreadable configuration.
        }
    }
    public bool IsConfigured
    {
        get
        {
            try { Validate(Load()); return true; }
            catch (ArgumentException) { return false; }
        }
    }
    public static string Validate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Bitte die Datenbankverbindung unter Einstellungen einrichten.");
        MySqlConnectionStringBuilder builder;
        try { builder = new MySqlConnectionStringBuilder(value); }
        catch (ArgumentException) { throw new ArgumentException("Die Datenbankkonfiguration ist ungültig."); }
        if (string.IsNullOrWhiteSpace(builder.Server) || string.IsNullOrWhiteSpace(builder.Database) || string.IsNullOrWhiteSpace(builder.UserID) || builder.Port is 0 or > 65535)
            throw new ArgumentException("Bitte Server, Port, Datenbank und Benutzer vollständig angeben.");
        builder.UseAffectedRows = false;
        return builder.ConnectionString;
    }
    public void Save(string connectionString)
    {
        if (IsEnvironmentOverride)
            throw new InvalidOperationException("CAMPER_DB_CONNECTION ist gesetzt. Bitte diese Umgebungsvariable ändern oder entfernen und die Anwendung neu starten.");
        var normalized = Validate(connectionString);
        var directory = Path.GetDirectoryName(_path)!;
        Directory.CreateDirectory(directory);
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var temporary = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
            if (!OperatingSystem.IsWindows())
                options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            using (var stream = new FileStream(temporary, options))
                JsonSerializer.Serialize(stream, new Settings(normalized));
            File.Move(temporary, _path, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    private sealed record Settings(string ConnectionString);
}

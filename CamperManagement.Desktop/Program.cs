using System;
using System.Linq;
using Avalonia;
using MySqlConnector;

namespace CamperManagement.Desktop;

sealed class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Contains("--smoke-test"))
        {
            var connection = Environment.GetEnvironmentVariable("CAMPER_DB_CONNECTION");
            if (string.IsNullOrWhiteSpace(connection)) return 2;
            var options = new MySqlConnectionStringBuilder(connection);
            if (options.Server is not ("localhost" or "127.0.0.1") ||
                !options.Database.StartsWith("camper_test_", StringComparison.Ordinal)) return 2;
            App.IsSmokeTest = true;
        }
        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .WithInterFont()
        .LogToTrace();
}

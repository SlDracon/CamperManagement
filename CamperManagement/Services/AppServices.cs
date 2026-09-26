using System;
using System.IO;
using Avalonia.Controls;
using CamperManagement.ViewModels;
namespace CamperManagement.Services;

/// <summary>Single composition root. Views and view models never construct live database services.</summary>
public static class AppServices
{
    public static MainViewModel Create(Func<TopLevel?> topLevel)
    {
        var configuration = new DatabaseConfiguration();
        var log = new LocalErrorLog(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CamperManagement", "logs"));
        var database = new DatabaseService(() => DatabaseConfiguration.Validate(configuration.Load()));
        return new MainViewModel(database, new PdfExporter(topLevel, log), TimeProvider.System, log, configuration,
            async value => { await new DatabaseService(value).GetStandardfaktorenAsync(); });
    }
}

using System;
using System.Reflection;
using CamperManagement.Models;
using CamperManagement.Services;
namespace CamperManagement.ViewModels;

public static class DesignData
{
    public static RechnungenViewModel Rechnungen
    {
        get
        {
            var db = DispatchProxy.Create<IDatabaseService, NoDatabase>();
            return new RechnungenViewModel(new MainViewModel(db))
            {
                FilteredRechnungenList = new()
                {
                    new RechnungDisplayModel { Id = 1, Platznr = "101", Art = "Strom", Jahr = 2026, Alt = 100, Neu = 130, Verbrauch = 30, Faktor = 0.5m, Betrag = 15, Gedruckt = "Nein" },
                    new RechnungDisplayModel { Id = 2, Platznr = "102", Art = "Wasser", Jahr = 2026, Alt = 80, Neu = 85, Verbrauch = 5, Faktor = 8m, Betrag = 40, Gedruckt = "Ja" }
                }
            };
        }
    }
    public class NoDatabase : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => throw new InvalidOperationException("Design-Vorschau greift nicht auf Datenbanken zu.");
    }
}

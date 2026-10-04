using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace CamperManagement.Models;

public sealed record CamperHistoryState(CamperDisplayModel Camper, bool Active, DateTime? Created, DateTime? Deactivated);
public sealed record CamperHistoryChange(string Field, string? Before, string After);

public sealed class CamperHistoryEntry
{
    private IReadOnlyList<CamperHistoryChange>? _changes, _stateFields;
    private string? _searchText;
    public long Id { get; init; }
    public DateTime RecordedAtUtc { get; init; }
    public string Kind { get; init; } = "";
    public string Description { get; init; } = "";
    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);
    public string IncreaseDisplay => Kind == "cost_increased" && Before != null ? "+" + (After.Camper.Vertragskosten - Before.Camper.Vertragskosten).ToString("N2", CultureInfo.GetCultureInfo("de-DE")) + " €" : "";
    public CamperHistoryState? Before { get; init; }
    public required CamperHistoryState After { get; init; }
    public string EventName => Kind switch { "baseline" => "Bestandsaufnahme", "created" => "Camper angelegt", "updated" => "Camper geändert", "cost_increased" => "Vertragskosten erhöht", "deactivated" => "Belegung beendet", _ => "Änderung" };
    public string TimeDisplay => RecordedAtUtc.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss", CultureInfo.GetCultureInfo("de-DE"));
    public string Title => $"{TimeDisplay} · {EventName}";
    public string Names => string.Join(" und ", new[] { $"{After.Camper.Vorname} {After.Camper.Nachname}".Trim(), After.Camper.ZweiterName }.Where(s => !string.IsNullOrWhiteSpace(s)));
    public string Identity => $"Platz {After.Camper.Platznr} · {(Names.Length == 0 ? "Name nicht hinterlegt" : Names)} · Belegung #{After.Camper.Id}";
    public string Occupation => $"Belegt seit {Date(After.Created)} · {(After.Active ? "aktiv" : "beendet am " + Date(After.Deactivated))}";
    public string DetailHeading => Before == null ? "Gespeicherter Stand" : "Änderungen (vorher → nachher)";
    public bool HasPreviousState => Before != null;
    public IReadOnlyList<CamperHistoryChange> Changes
    {
        get
        {
            if (_changes != null) return _changes;
            var previous = Before == null ? null : Fields(Before);
            return _changes = Fields(After).Where(item => previous == null || previous[item.Key] != item.Value)
                .Select(item => new CamperHistoryChange(item.Key, previous == null ? null : previous[item.Key], item.Value)).ToArray();
        }
    }
    public IReadOnlyList<CamperHistoryChange> StateFields => _stateFields ??= Fields(After).Select(f => new CamperHistoryChange(f.Key, "", f.Value)).ToArray();
    public string Summary => Kind == "cost_increased" ? $"{IncreaseDisplay} · {Description}" : Before == null ? Occupation : string.Join(", ", Changes.Select(c => c.Field));
    public string SearchText => _searchText ??= string.Join(" ", new[] { Identity, EventName, TimeDisplay, Occupation, Description, IncreaseDisplay }
        .Concat(Fields(After).Values).Concat(Before == null ? Array.Empty<string>() : Fields(Before).Values)
        .Concat(Changes.Select(c => c.Field)));
    private static string Date(DateTime? value) => value?.ToString("dd.MM.yyyy", CultureInfo.GetCultureInfo("de-DE")) ?? "unbekannt";
    private static string Text(string? value) => string.IsNullOrWhiteSpace(value) ? "–" : value.Trim();
    private static Dictionary<string, string> Fields(CamperHistoryState state)
    {
        var c = state.Camper;
        var second = c.HatZweitenVertragsnehmer;
        return new()
        {
            ["Platznummer"] = Text(c.Platznr),
            ["Status"] = state.Active ? "Aktiv" : "Beendet",
            ["Belegt seit"] = Date(state.Created),
            ["Beendet am"] = state.Active ? "–" : Date(state.Deactivated),
            ["Anrede"] = Text(c.Anrede), ["Vorname"] = Text(c.Vorname), ["Nachname"] = Text(c.Nachname),
            ["Straße"] = Text(c.Straße), ["PLZ"] = Text(c.PLZ), ["Ort"] = Text(c.Ort), ["E-Mail"] = Text(c.Email),
            ["Zweiter Vertragsnehmer"] = second ? "Ja" : "Nein",
            ["Zweite Anrede"] = Text(second ? c.ZweiteAnrede : null),
            ["Zweiter Vorname"] = Text(second ? c.ZweiterVorname : null), ["Zweiter Nachname"] = Text(second ? c.ZweiterNachname : null),
            ["Zweite E-Mail"] = Text(second ? c.ZweiteEmail : null),
            ["Gemeinsame Adresse"] = second ? (c.GemeinsameAdresse ? "Ja" : "Nein") : "–",
            ["Zweite Straße"] = Text(second ? c.ZweiteStraße : null), ["Zweite PLZ"] = Text(second ? c.ZweitePLZ : null), ["Zweiter Ort"] = Text(second ? c.ZweiterOrt : null),
            ["Rechnungsadresse"] = c.NutztZweiteRechnungsadresse ? "Zweiter Vertragsnehmer" : "Erster Vertragsnehmer",
            ["Vertragskosten"] = c.Vertragskosten.ToString("N2", CultureInfo.GetCultureInfo("de-DE")) + " €"
        };
    }
}

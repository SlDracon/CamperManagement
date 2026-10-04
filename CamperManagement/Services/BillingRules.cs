using System;
using System.Globalization;
using CamperManagement.Models;
namespace CamperManagement.Services;

public static class BillingRules
{
    public static decimal Round(decimal amount) => Math.Round(amount, 2, MidpointRounding.AwayFromZero);
    public static decimal Amount(decimal consumption, decimal factor) => Round(checked(consumption * factor));
    public static bool IsAllowedYear(int year, TimeProvider clock) => year == clock.GetLocalNow().Year || year == clock.GetLocalNow().Year - 1;
    public static void ValidateInvoice(Rechnung value, TimeProvider clock, bool isNew)
    {
        if (value.PlatzId <= 0)
            throw new ArgumentException("Bitte einen gültigen Platz auswählen.");
        if (value.Type is not ("Wasser" or "Strom"))
            throw new ArgumentException("Bitte Strom oder Wasser auswählen.");
        if (isNew && !IsAllowedYear(value.Jahr, clock))
            throw new ArgumentException("Erlaubt sind das aktuelle Jahr und das Vorjahr.");
    }
    public static void ValidateCamper(CamperDisplayModel value)
    {
        foreach (var (name, text) in new[] { ("Platznummer", value.Platznr), ("Vorname", value.Vorname), ("Nachname", value.Nachname), ("Straße", value.Straße), ("PLZ", value.PLZ), ("Ort", value.Ort) })
            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentException($"{name} ist ein Pflichtfeld.");
        ValidateContractPartners(value);
    }
    public static void ValidateContractPartners(CamperDisplayModel value)
    {
        if (!value.HatZweitenVertragsnehmer)
            return;
        foreach (var (name, text) in new[] { ("Vorname des zweiten Vertragsnehmers", value.ZweiterVorname), ("Nachname des zweiten Vertragsnehmers", value.ZweiterNachname) })
            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentException($"{name} ist ein Pflichtfeld.");
        if (!value.GemeinsameAdresse)
            foreach (var (name, text) in new[] { ("Straße des zweiten Vertragsnehmers", value.ZweiteStraße), ("PLZ des zweiten Vertragsnehmers", value.ZweitePLZ), ("Ort des zweiten Vertragsnehmers", value.ZweiterOrt) })
                if (string.IsNullOrWhiteSpace(text))
                    throw new ArgumentException($"{name} ist ein Pflichtfeld.");
    }
    public static bool TryDecimal(string? text, out decimal value) => decimal.TryParse(text?.Trim().Replace(',', '.'), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value);
}

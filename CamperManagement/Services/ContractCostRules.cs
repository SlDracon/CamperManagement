using System;

namespace CamperManagement.Services;

public static class ContractCostRules
{
    public const int MaxDescriptionLength = 1000;
    // camper.Vertragskosten is DECIMAL(18,2).
    public const decimal MaxCost = 9999999999999999.99m;
    public static decimal NewTotal(decimal current, decimal increase)
    {
        var rounded = BillingRules.Round(increase);
        if (rounded <= 0)
            throw new ArgumentException("Die Erhöhung muss mindestens 0,01 € betragen.");
        if (rounded > MaxCost || current > MaxCost - rounded || current < -MaxCost)
            throw new ArgumentException("Der neue Endpreis ist zu groß.");
        return BillingRules.Round(current + rounded);
    }
    public static string Description(string? description)
    {
        var text = description?.Trim() ?? "";
        if (text.Length == 0)
            throw new ArgumentException("Bitte eine Begründung oder Beschreibung eingeben.");
        if (text.Length > MaxDescriptionLength)
            throw new ArgumentException($"Die Beschreibung darf höchstens {MaxDescriptionLength} Zeichen enthalten.");
        return text;
    }
}

namespace CamperManagement.Models;

public sealed record Standardfaktoren(decimal Strom, decimal Wasser, long Version = 0)
{
    public decimal ForArt(string? art) => art switch
    {
        "Strom" => Strom,
        "Wasser" => Wasser,
        _ => throw new System.ArgumentException("Bitte Strom oder Wasser auswählen.")
    };
}

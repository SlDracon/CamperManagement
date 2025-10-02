using System;
using System.Globalization;

namespace CamperManagement.Models
{
    public class RechnungDisplayModel
    {
        public int Id { get; set; }
        public string? Platznr { get; set; }
        public decimal Alt { get; set; }
        public decimal Neu { get; set; }
        public decimal Verbrauch { get; set; }
        public decimal Faktor { get; set; }
        public decimal Betrag { get; set; }
        public int Jahr { get; set; }
        public string? Art { get; set; }
        public string? Gedruckt { get; set; }
        public string? Vorname { get; set; }
        public string? Nachname { get; set; }
        public string? Anrede { get; set; }
        public string? Straße { get; set; }
        public string? PLZ { get; set; }
        public string? Ort { get; set; }

        public string AltDisplay => FormatValue(Alt);
        public string NeuDisplay => FormatValue(Neu);
        public string VerbrauchDisplay => FormatValue(Verbrauch);

        private static readonly CultureInfo GermanCulture = CultureInfo.GetCultureInfo("de-DE");

        private string FormatValue(decimal value)
        {
            var format = string.Equals(Art, "Wasser", StringComparison.OrdinalIgnoreCase) ? "0.000" : "0.00";
            return value.ToString(format, GermanCulture);
        }
    }
}

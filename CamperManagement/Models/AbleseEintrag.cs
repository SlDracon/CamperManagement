using System;
using System.Globalization;

namespace CamperManagement.Models
{
    public class AbleseEintrag
    {
        public required string PlatzNr { get; set; }
        public required string Vorname { get; set; }
        public required string Nachname { get; set; }
        public decimal WasserAlt { get; set; }
        public decimal StromAlt { get; set; }

        private static readonly CultureInfo GermanCulture = CultureInfo.GetCultureInfo("de-DE");

        public string WasserAltDisplay => WasserAlt.ToString("0.000", GermanCulture);
        public string StromAltDisplay => StromAlt.ToString("0.00", GermanCulture);
    }
}

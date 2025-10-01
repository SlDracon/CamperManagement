using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CamperManagement.Models
{
    public class KostenEintrag
    {
        public required string PlatzNr { get; set; }
        public required string Vorname { get; set; }
        public required string Nachname { get; set; }
        public decimal WasserBetrag { get; set; }
        public decimal StromBetrag { get; set; }
        public decimal Gesamtbetrag => WasserBetrag + StromBetrag;
        public decimal Vertragskosten { get; set; }
    }
}

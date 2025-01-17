using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CamperManagement.Models
{
    public class CamperDisplayModel
    {
        public string Platznr { get; set; }
        public string Anrede { get; set; }
        public string Vorname { get; set; }
        public string Nachname { get; set; }
        public string Straße { get; set; }
        public string PLZ { get; set; }
        public string Ort { get; set; }
        public decimal Vertragskosten { get; set; }
    }
}

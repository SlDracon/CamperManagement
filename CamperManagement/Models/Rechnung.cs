using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CamperManagement.Models
{
    public class Rechnung
    {
        public int Id { get; set; }
        public int PlatzId { get; set; }
        public decimal Alt { get; set; }
        public decimal Neu { get; set; }
        public decimal Verbrauch { get; set; }
        public decimal Faktor { get; set; }
        public decimal Betrag { get; set; }
        public int Jahr { get; set; }
        public string Type { get; set; }
        public DateTime Created { get; set; }
        public DateTime Updated { get; set; }
        public bool Printed { get; set; }
    }
}

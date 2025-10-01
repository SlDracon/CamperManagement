using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CamperManagement.Models
{
    public class AbleseEintrag
    {
        public required string PlatzNr { get; set; }
        public required string Vorname { get; set; }
        public required string Nachname { get; set; }
        public decimal WasserAlt { get; set; }
        public decimal StromAlt { get; set; }
    }
}

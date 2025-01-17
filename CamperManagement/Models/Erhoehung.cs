using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CamperManagement.Models
{
    public class Erhoehung
    {
        public decimal Betrag { get; set; }
        public DateTime WirksamAb { get; set; }
        public string Kommentar { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CamperManagement.Models
{
    public class Vertragspreis
    {
        public int CamperId { get; set; }
        public decimal Basispreis { get; set; }
        public List<Erhoehung> Erhoehungen { get; set; } = new();
    }
}

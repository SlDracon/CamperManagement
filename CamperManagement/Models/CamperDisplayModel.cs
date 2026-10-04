using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CamperManagement.Models
{
    public class CamperDisplayModel
    {
        public CamperDisplayModel Snapshot() => (CamperDisplayModel)MemberwiseClone();
        public bool HatZweitenVertragsnehmer { get; set; }
        public string? ZweiteAnrede { get; set; }
        public string? ZweiterVorname { get; set; }
        public string? ZweiterNachname { get; set; }
        public string? ZweiteStraße { get; set; }
        public string? ZweitePLZ { get; set; }
        public string? ZweiterOrt { get; set; }
        public string? ZweiteEmail { get; set; }
        public bool GemeinsameAdresse { get; set; } = true;
        public bool RechnungsadresseZweiterVertragsnehmer { get; set; }
        public bool NutztZweiteRechnungsadresse => HatZweitenVertragsnehmer && !GemeinsameAdresse && RechnungsadresseZweiterVertragsnehmer;
        public string ErsterName => $"{Vorname} {Nachname}".Trim();
        public string ZweiterName => HatZweitenVertragsnehmer ? $"{ZweiterVorname} {ZweiterNachname}".Trim() : "";
        public string? Rechnungsstraße => NutztZweiteRechnungsadresse ? ZweiteStraße : Straße;
        public string? RechnungsPLZ => NutztZweiteRechnungsadresse ? ZweitePLZ : PLZ;
        public string? Rechnungsort => NutztZweiteRechnungsadresse ? ZweiterOrt : Ort;
        public int Id
        {
            get; set;
        }

        public string? Platznr
        {
            get; set;
        }
        public string? Anrede
        {
            get; set;
        }
        public string? Vorname
        {
            get; set;
        }
        public string? Nachname
        {
            get; set;
        }
        public string? Straße
        {
            get; set;
        }
        public string? PLZ
        {
            get; set;
        }
        public string? Ort
        {
            get; set;
        }
        public string? Email
        {
            get; set;
        }
        public string EndpreisDisplay => Vertragskosten.ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("de-DE")) + " €";
        public decimal Vertragskosten
        {
            get; set;
        }
    }
}

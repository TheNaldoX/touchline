using System;
using System.Collections.Generic;
using System.Linq;

namespace Touchline.Core
{
    // Shared labels and read-time aliases for searches and tactical fit.
    // Imported positions and saved identifiers are never rewritten.
    public static class FootballPositions
    {
        public static string Canonical(string role)=>role=="CDM"?"DM":role=="CAM"?"AM":role=="GB"?"GK":role;
        public static bool Matches(PlayerData player,string role)
        {
            if(player==null)return false;
            if(role=="Tous"||role=="Tous postes")return true;
            var target=Canonical(role);
            return Canonical(player.position)==target||(player.positions?.Any(p=>Canonical(p)==target)??false);
        }
        public static string Short(string role)
        {
            switch(role)
            {
                case "GK": return "GB"; case "CB": return "DC";
                case "LB": return "DG"; case "RB": return "DD";
                case "LWB": return "PG"; case "RWB": return "PD";
                case "CDM": case "DM": return "MDC"; case "CM": return "MC"; case "CAM": case "AM": return "MOC";
                case "LM": return "MG"; case "RM": return "MD";
                case "LW": return "AG"; case "RW": return "AD";
                case "ST": return "BT"; case "CF": return "AC"; case "SS": return "SA";
                default: return string.IsNullOrWhiteSpace(role)?"—":role;
            }
        }
        public static string Label(string role)
        {
            switch(role)
            {
                case "Tous": case "Tous postes": return "Tous les postes";
                case "GB": case "GK": return "Gardien";
                case "DEF": return "Défenseur"; case "MIL": return "Milieu"; case "ATT": return "Attaquant";
                case "CB": return "Défenseur central";
                case "LB": return "Défenseur gauche"; case "RB": return "Défenseur droit";
                case "LWB": return "Piston gauche"; case "RWB": return "Piston droit";
                case "CDM": case "DM": return "Milieu défensif"; case "CM": return "Milieu central"; case "CAM": case "AM": return "Milieu offensif";
                case "LM": return "Milieu gauche"; case "RM": return "Milieu droit";
                case "LW": return "Ailier gauche"; case "RW": return "Ailier droit";
                case "ST": return "Buteur"; case "CF": return "Avant-centre"; case "SS": return "Second attaquant";
                default: return string.IsNullOrWhiteSpace(role)?"Poste non renseigné":role;
            }
        }
        public static string List(IEnumerable<string> roles)=>string.Join(" / ",(roles??Array.Empty<string>()).Select(Label).Distinct());
        public static string CompactList(IEnumerable<string> roles)=>string.Join(" / ",(roles??Array.Empty<string>()).Select(Short).Distinct());
    }
}

using System.Collections.Generic;
using System;
using System.Linq;
namespace Touchline
{
    public static class FrenchFootballPositions
    {
        public static string Short(string role)=>Core.FootballPositions.Short(role);
        public static string Label(string role)=>Core.FootballPositions.Label(role);
        public static string List(IEnumerable<string> roles)=>Core.FootballPositions.List(roles);
        public static string CompactList(IEnumerable<string> roles)=>Core.FootballPositions.CompactList(roles);
        // The imported `position` is often only ATT/MIL/DEF. Keep the ordered,
        // more precise repertoire for presentation without rewriting simulation ids.
        static readonly HashSet<string> SpecificRoles=new HashSet<string>(new[]{"GK","CB","LB","RB","LWB","RWB","DM","CM","AM","LM","RM","LW","RW","ST","CF","SS"},StringComparer.Ordinal);
        static bool Specific(string role)=>SpecificRoles.Contains(Core.FootballPositions.Canonical(role));
        static string[] PlayerRoles(Core.PlayerData player)
        {
            if(player==null)return Array.Empty<string>();
            var precise=new[]{player.position}.Concat(player.positions??Array.Empty<string>()).Where(Specific).Select(Core.FootballPositions.Canonical).Distinct().ToArray();
            if(precise.Length>0)return precise;
            var fallback=!string.IsNullOrWhiteSpace(player.position)?player.position:(player.positions??Array.Empty<string>()).FirstOrDefault(p=>!string.IsNullOrWhiteSpace(p));
            return string.IsNullOrWhiteSpace(fallback)?Array.Empty<string>():new[]{fallback};
        }
        public static string PlayerLabel(Core.PlayerData player)=>Label(PlayerRoles(player).FirstOrDefault());
        public static string PlayerList(Core.PlayerData player){var roles=PlayerRoles(player);return roles.Length==0?Label(null):List(roles);}
    }
}

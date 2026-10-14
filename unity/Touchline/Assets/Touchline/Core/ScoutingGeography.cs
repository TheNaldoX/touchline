using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Touchline.Core
{
    // Recruitment territories are independent of playable championships.
    public static class ScoutingGeography
    {
        public static string Country(Database db,ClubData club)
        {
            if(club==null)return null;
            string leagueCountry=db.leagues?.FirstOrDefault(l=>l.id==club.league)?.country;
            if(!string.IsNullOrWhiteSpace(leagueCountry))return leagueCountry;
            if(string.IsNullOrWhiteSpace(club.country)||club.country==club.name
                ||!Uri.TryCreate(club.countrySource,UriKind.Absolute,out var source)
                ||(source.Scheme!="https"&&source.Scheme!="http")
                ||!DateTime.TryParseExact(club.countryAsOf,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out _))return null;
            return club.country;
        }
        public static IEnumerable<string> Countries(Database db)=>(db.leagues??Array.Empty<LeagueData>()).Select(l=>l.country)
            .Concat((db.clubs??Array.Empty<ClubData>()).Select(c=>Country(db,c)))
            .Where(c=>!string.IsNullOrWhiteSpace(c)).Distinct().OrderBy(c=>c,StringComparer.Ordinal);
        public static IEnumerable<string> ClubIds(Database db,string country="Tous",string league="Tous")
        {
            bool allCountries=string.IsNullOrEmpty(country)||country=="Tous";
            bool allLeagues=string.IsNullOrEmpty(league)||league=="Tous";
            var leagues=new HashSet<string>((db.leagues??Array.Empty<LeagueData>()).Where(l=>l.name==league).Select(l=>l.id));
            return (db.clubs??Array.Empty<ClubData>()).Where(c=>(allCountries||Country(db,c)==country)&&(allLeagues||leagues.Contains(c.league))).Select(c=>c.id);
        }
    }
}

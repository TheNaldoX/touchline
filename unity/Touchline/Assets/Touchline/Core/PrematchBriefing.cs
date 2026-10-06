using System;
using System.Linq;
using System.Collections.Generic;

namespace Touchline.Core
{
    // A read-only view of this career, not of the real-world league table.
    public sealed class PrematchBriefing
    {
        public Fixture fixture;
        public string venue, context;
        public List<Standing> table;
        public bool HasResults => table.Any(r=>r.played>0);
        public static int Side(MatchState match,string club)
        {
            if(match.home==club)return 0;
            if(match.away==club)return 1;
            throw new ArgumentException("Ce club ne participe pas à ce match.");
        }
        public static Actor[] Starters(MatchState match,string club)=>match.actors.Where(a=>a.side==Side(match,club)).OrderBy(a=>a.slot).ToArray();
        public static string Form(Career career,Fixture fixture,string club)
        {
            var played=career.world.history.Concat(career.world.fixtures)
                .Where(f=>f.played&&f.id!=fixture.id&&f.day<=fixture.day&&(f.home==club||f.away==club))
                .GroupBy(f=>f.id).Select(g=>g.Last()).OrderBy(f=>f.day).ThenBy(f=>f.id,StringComparer.Ordinal).TakeLast(5);
            var results=played.Select(f=>{int gf=f.home==club?f.hg:f.ag,ga=f.home==club?f.ag:f.hg;return gf>ga?"V":gf==ga?"N":"D";}).ToArray();
            return results.Length==0?"Aucun résultat dans cette carrière":string.Join("  ",results);
        }
        public static PrematchBriefing Create(Career career,Database db,Fixture fixture)
        {
            var result=new PrematchBriefing{fixture=fixture,table=new List<Standing>()};
            result.venue=!string.IsNullOrWhiteSpace(fixture.venue)?fixture.venue:fixture.neutral?"Terrain neutre · stade non renseigné":db.clubs.FirstOrDefault(c=>c.id==fixture.home)?.stadium;
            if(string.IsNullOrWhiteSpace(result.venue))result.venue="Stade non renseigné";
            if(fixture.league=="friendly")result.context="Préparation · retrouver du rythme et travailler les automatismes. Aucun point de championnat en jeu.";
            else if(fixture.knockout){
                var previous=career.world.fixtures.Where(f=>f.played&&f.id!=fixture.id&&f.day<=fixture.day&&f.league==fixture.league&&!string.IsNullOrEmpty(fixture.tie)&&f.tie==fixture.tie&&((f.home==fixture.home&&f.away==fixture.away)||(f.home==fixture.away&&f.away==fixture.home))).ToArray();
                result.context=previous.Length==0?(fixture.leg==1?"Match aller · première manche de la qualification.":fixture.leg==2?"Match retour · résultat de l’aller non disponible.":"Élimination directe · une qualification se joue aujourd’hui."):
                    "Match retour · cumul avant cette rencontre : "+previous.Sum(f=>f.home==fixture.home?f.hg:f.ag)+" – "+previous.Sum(f=>f.home==fixture.home?f.ag:f.hg)+" (domicile – extérieur de ce soir).";
            }else{
                result.table=career.Table(fixture.league);
                result.context=result.HasResults?"Classement de votre carrière avant le coup d’envoi · trois points à prendre.":"Début de compétition · le classement n’est pas encore établi.";
            }
            return result;
        }
    }
}

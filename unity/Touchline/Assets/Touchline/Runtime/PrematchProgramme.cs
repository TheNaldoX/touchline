using System;
using System.Collections.Generic;
using System.Linq;
using Touchline.Core;

namespace Touchline
{
    // Read-only programme: actual career results and the exact match tactics.
    // No player ability, potential or scouting knowledge enters this model.
    public static class PrematchProgramme
    {
        public static Fixture[] RecentResults(Career career,Fixture upcoming,string club,int count=3)
        {
            if(career?.world==null||upcoming==null||string.IsNullOrEmpty(club)||count<=0)return Array.Empty<Fixture>();
            return (career.world.history??new List<Fixture>()).Concat(career.world.fixtures??new List<Fixture>())
                .Where(f=>f!=null&&f.played&&f.id!=upcoming.id&&f.day<=upcoming.day&&(f.home==club||f.away==club))
                .GroupBy(f=>f.id).Select(g=>g.Last()).OrderByDescending(f=>f.day).ThenByDescending(f=>f.id,StringComparer.Ordinal)
                .Take(count).ToArray();
        }
        public static Fixture LastMeeting(Career career,Fixture upcoming)
        {
            if(upcoming==null)return null;
            return RecentResults(career,upcoming,upcoming.home,int.MaxValue)
                .FirstOrDefault(f=>f.home==upcoming.home&&f.away==upcoming.away||f.away==upcoming.home&&f.home==upcoming.away);
        }
        public static Slot Position(MatchState match,string club,int index,bool withBall)
        {
            if(match==null)throw new ArgumentNullException(nameof(match));
            var tactic=PrematchBriefing.Side(match,club)==0?match.homeTactic:match.awayTactic;
            var positions=withBall?tactic?.withBall:tactic?.withoutBall;
            if(positions==null||index<0||index>=positions.Length||positions[index]==null)return null;
            var slot=positions[index];return new Slot(slot.role,slot.x,slot.y){duty=slot.duty};
        }
        public static string Instructions(MatchState match,string club,bool withBall)
        {
            var tactic=PrematchBriefing.Side(match,club)==0?match.homeTactic:match.awayTactic;
            if(tactic==null)return "Consignes non renseignées";
            return withBall?"Rythme "+Level(tactic.tempo,new[]{"très patient","patient","normal","rapide","très rapide"})+" · Passes "+Level(tactic.directness,new[]{"très courtes","courtes","mixtes","directes","très directes"})+(tactic.workIntoBox?" · Construire dans la surface":"")
                :"Ligne "+Level(tactic.line,new[]{"très basse","basse","normale","haute","très haute"})+" · Pressing "+Level(tactic.pressing,new[]{"rare","mesuré","normal","fréquent","très intense"});
        }
        static string Level(float value,string[] labels)=>labels[Math.Max(0,Math.Min(4,(int)Math.Round(value*4)))];
        public static string Duty(Slot slot)=>slot==null?"Non renseignée":slot.duty=="attack"?"Attaque":slot.duty=="defend"?"Défense":"Soutien";
        public static string PreferredFoot(PlayerData player)=>player?.preferredFoot=="Left"?"Gauche":player?.preferredFoot=="Right"?"Droit":player?.preferredFoot=="Both"?"Les deux":"Non renseigné";
    }
}

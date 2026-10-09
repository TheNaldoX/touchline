using System;
using System.Linq;

namespace Touchline.Core
{
    public static class CareerSaveRestore
    {
        public static bool TryRestore(Database original,Career state,out Database restored)
        {
            restored=null;
            if(state==null||state.schema!=1||state.lineup?.Length!=11||state.lineup.Distinct().Count()!=11
                ||!original.clubs.Any(c=>c.id==state.club))return false;
            // RestoreWorld replaces the player array but mutates club records.
            // Share read-only competition catalogues and copy mutable records.
            // A rejected primary must never contaminate the backup attempt.
            var candidate=new Database{
                schema=original.schema,importedAt=original.importedAt,provenance=original.provenance,
                players=original.players.Select(p=>p.Copy()).ToArray(),clubs=original.clubs.Select(c=>c.Copy()).ToArray(),
                leagues=original.leagues,fixtures=original.fixtures,pyramidRules=original.pyramidRules,
                freeAgents=original.freeAgents,freeAgentCatalogVersion=original.freeAgentCatalogVersion
            };
            try{state.ExpandCompactSave(original);state.RestoreWorld(candidate);state.SynchronizePlayerAges(candidate);}catch(ArgumentException){return false;}
            // An unemployed manager retains a historical XI; retirement or a transfer
            // must not make the career unreadable. Active matches keep strict ownership.
            bool historical=state.world?.managerStatus=="unemployed"&&string.IsNullOrEmpty(state.world.activeFixture)
                &&(state.match==null||string.IsNullOrEmpty(state.match.home)&&string.IsNullOrEmpty(state.match.away)&&(state.match.actors==null||state.match.actors.Length==0));
            if(!state.lineup.All(id=>candidate.Find(id)!=null&&(historical||candidate.Find(id).team==state.club)))return false;
            restored=candidate;return true;
        }
    }
}

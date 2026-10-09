using UnityEngine;
using System;
using System.Linq;
using Touchline.Core;

namespace Touchline
{
    // New careers use imported players only. The discontinued fictional extension
    // remains available solely to decode careers already saved with it.
    public static class TouchlineCatalogue
    {
        public static Database Load()
        {
            var db=JsonUtility.FromJson<Database>(Resources.Load<TextAsset>("Data/database").text);
            return db;
        }

        public static Database ForSavedCareer(Database original,Career state)
        {
            var world=state?.world;
            bool legacy=world!=null&&((world.rosterChanges?.Any(p=>GeneratedWorld.IsGenerated(p?.id))??false)
                ||(world.compactFullPlayers?.Any(p=>GeneratedWorld.IsGenerated(p?.id))??false)
                ||(world.compactRoster?.Any(r=>r!=null&&r.StartsWith("="+GeneratedWorld.IdPrefix,StringComparison.Ordinal))??false)
                ||(world.compactStrings?.Any(GeneratedWorld.IsGenerated)??false));
            if(!legacy||original.leagues.Any(GeneratedWorld.IsGeneratedLeague))return original;
            // AppendFrozen replaces arrays: keep the launch catalogue untouched.
            var compatible=new Database{schema=original.schema,importedAt=original.importedAt,provenance=original.provenance,
                players=original.players,clubs=original.clubs,leagues=original.leagues,fixtures=original.fixtures,
                pyramidRules=original.pyramidRules,freeAgents=original.freeAgents,freeAgentCatalogVersion=original.freeAgentCatalogVersion};
            GeneratedWorld.AppendFrozen(compatible,JsonUtility.FromJson<Database>(Resources.Load<TextAsset>("Data/generated-world-v1").text));
            return compatible;
        }
    }
}

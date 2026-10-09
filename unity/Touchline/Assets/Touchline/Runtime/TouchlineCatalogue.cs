using UnityEngine;
using Touchline.Core;

namespace Touchline
{
    // Single entry point for the shipped catalogue: the imported database plus
    // the fictional generated leagues (deterministic, see Core/GeneratedWorld).
    // Every runtime load goes through here so saves and their baselines match.
    public static class TouchlineCatalogue
    {
        public static Database Load()
        {
            var db=JsonUtility.FromJson<Database>(Resources.Load<TextAsset>("Data/database").text);
            GeneratedWorld.Expand(db);
            return db;
        }
    }
}

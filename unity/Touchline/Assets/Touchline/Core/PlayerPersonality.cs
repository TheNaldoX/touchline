using System;

namespace Touchline.Core
{
    public struct TraitAssessment
    {
        public int low, high; public bool known;
        public override string ToString() => !known ? "?" : low == high ? low.ToString() : low + "–" + high;
    }
    // Hidden personality used by recruitment. The imported database has no
    // personality data: these 1–20 values are a stable simulation profile
    // derived from the player id (never saved, never presented as real).
    public partial class Career
    {
        public const string Ambition = "ambition", Adaptability = "adaptability";
        public static readonly string[] PersonalityTraits = { Ambition, Adaptability };
        const int TraitKnowledgeShown = 65;      // % of knowledge before a range is shown
        const int TraitWidthPartial = 4, TraitWidthComplete = 2; // ± points (1–20) at 65 % and 90 % of knowledge

        /// <summary>True hidden value (1–20). Average of two stable draws: most players sit around 10.</summary>
        public static int PersonalityTrait(string playerId, string trait)
        {
            if (string.IsNullOrEmpty(playerId)) return 10;
            uint a = MixedIdentity("trait/" + trait + "/" + playerId) % 20, b = MixedIdentity("trait2/" + trait + "/" + playerId) % 20;
            return (int)Mathx.Clamp(1 + (a + b) / 2f + .5f, 1, 20);
        }
        // FNV-1a alone mixes its low bits poorly (ids differing by one digit share residues):
        // a 32-bit finalizer spreads them before taking small moduli.
        static uint MixedIdentity(string key)
        {
            uint x = StableIdentity(key); x ^= x >> 16; x = unchecked(x * 0x7feb352d); x ^= x >> 15; x = unchecked(x * 0x846ca68b); x ^= x >> 16; return x;
        }
        static float MixedBias(string key) => MixedIdentity(key) % 2001 / 1000f - 1;
        public static string TraitLabel(string trait) => trait == Ambition ? "Ambition" : trait == Adaptability ? "Adaptabilité" : trait;

        public TraitAssessment AssessedTrait(Database db, string id, string trait)
        {
            var p = db?.Find(id); if (p == null) return default;
            int truth = PersonalityTrait(id, trait);
            if (revealAttributes || p.team == club) return new TraitAssessment { known = true, low = truth, high = truth };
            int knowledge = Knowledge(id); if (knowledge < TraitKnowledgeShown) return default;
            var report = ReportFor(id); int judging = report?.judging > 0 ? report.judging : Staff("scout").judging;
            int width = knowledge >= 90 ? (judging >= 15 ? TraitWidthComplete - 1 : TraitWidthComplete) : TraitWidthPartial;
            if ((report?.depth ?? 0) >= 2) width = Math.Max(1, width - 1);
            // Stable scout error, smaller than the width: the truth always stays inside the range.
            int center = truth + (int)Math.Round(MixedBias(id + "/trait-" + trait + "/" + (report?.started ?? 0)) * (width - 1));
            int low = Math.Max(1, Math.Min(truth, center - width)), high = Math.Min(20, Math.Max(truth, center + width));
            return new TraitAssessment { known = true, low = low, high = high };
        }
    }
}

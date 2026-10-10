using System;
using System.Collections.Generic;
using System.Linq;

namespace Touchline.Core
{
    public static class ClubRatingScale
    {
        const float RatingPointsPerStar=7; // Internal ability points; three stars match the current club reference.
        public static float Relative(float rating,float reference)=>Mathx.Clamp(3+(rating-reference)/RatingPointsPerStar,1,5);
        public static string Stars(float rating,float reference)
        {
            int count=(int)Math.Round(Relative(rating,reference));
            return new string('★',count)+new string('☆',5-count);
        }
        public static string Range(ScoutRange range,float reference)
        {
            if(!range.known)return "À observer";
            string low=Stars(range.low,reference),high=Stars(range.high,reference);
            return low==high?low:low+" à "+high;
        }
    }
    public struct ScoutRange
    {
        public int low, high; public bool known;
        public int Middle => (low + high) / 2;
        public override string ToString() => !known ? "?" : low == high ? low.ToString() : low + "–" + high;
    }
    // Read-only view of stored observations; hidden live values are reserved for
    // the managed squad and the explicit reveal-attributes option.
    public sealed class ScoutReportCardData
    {
        public string player, grade = "?", gradeReason, fit, familiarityLabel, territory, rival, comparable, interest;
        public TraitAssessment ambition, adaptability;
        public List<string> interestReasons = new List<string>();
        public int knowledge, reportAge, depth, judging, observedDay = -1;
        public bool stale, generated, exact, pending;
        public float familiarity = 1;
        public ScoutRange ability, potential;
        public List<string> strengths = new List<string>(), weaknesses = new List<string>();
    }
    public partial class Career
    {
        const float CardBaseWidth = 2f;             // rating points (1–99): narrowest range for a perfect scout
        const float CardWidthPerJudging = .3f;      // extra points per missing judging point (20 − judging)
        const float CardUnfamiliarWidth = .8f;      // +80 % width in a territory the network does not know
        const float CardDepthNarrowing = .25f;      // −25 % width per repeated observation (max four)
        const float CardCenterBias = .6f;           // scout error, as a share of the width: the truth stays inside
        const float ForeignFamiliarity = .35f;      // 0–1 familiarity in a new foreign territory
        const float FamiliarityPerReport = .12f;    // gained per completed report in that territory
        const int GradeA = 6, GradeB = 2, GradeC = -2, GradeD = -6; // points above the reference starter

        /// <summary>0–1: how well the scouting network knows the territory of this player's club.</summary>
        // Derived caches only (never saved): rebuilt when the day, club or report list changes.
        [NonSerialized] Dictionary<string, int> familiarityCounts; [NonSerialized] string familiarityKey;
        [NonSerialized] Dictionary<string, string> territoryIndex; [NonSerialized] ClubData[] territorySource; [NonSerialized] LeagueData[] territoryLeagues;
        string PlayerTerritory(Database db, PlayerData p)
        {
            if (p == null || p.team == "free" || db?.clubs == null) return null;
            if (territoryIndex == null || !ReferenceEquals(territorySource, db.clubs) || !ReferenceEquals(territoryLeagues, db.leagues))
            {
                territorySource = db.clubs; territoryLeagues = db.leagues; territoryIndex = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var c in db.clubs) if (c?.id != null) territoryIndex[c.id] = ScoutingGeography.Country(db, c);
            }
            return territoryIndex.TryGetValue(p.team, out var country) ? country : null;
        }
        public float ScoutingFamiliarity(Database db, PlayerData p)
        {
            if (p == null || db == null || world?.reports == null) return 1;
            string territory = PlayerTerritory(db, p), home = PlayerTerritory(db, new PlayerData { team = club });
            if (territory == null || territory == home) return 1;
            string key = club + "/" + life?.day + "/" + world.reports.Count + "/" + world.reports.Count(r => r.confidence >= 90);
            if (familiarityCounts == null || familiarityKey != key)
            {
                familiarityKey = key; familiarityCounts = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var r in world.reports)
                {
                    if (r.confidence < 90 || !string.IsNullOrEmpty(r.club) && r.club != club) continue;
                    var t = PlayerTerritory(db, db.Find(r.player)); if (t == null) continue;
                    familiarityCounts[t] = (familiarityCounts.TryGetValue(t, out var n) ? n : 0) + 1;
                }
            }
            int known = familiarityCounts.TryGetValue(territory, out var count) ? count : 0;
            // The player's own completed report does not count as knowledge of his territory.
            var own = ReportFor(p.id); if (own != null && own.confidence >= 90 && known > 0) known--;
            float bonus = Staff("scout").judging >= 15 ? .1f : 0;
            // A scout of the department who knows this territory brings his own knowledge.
            return Mathx.Clamp(Math.Max(ForeignFamiliarity + FamiliarityPerReport * known + bonus, DepartmentFamiliarity(territory)), 0, 1);
        }
        public static string FamiliarityLabel(float value) => value >= .85f ? "excellente" : value >= .6f ? "bonne" : value >= .4f ? "partielle" : "faible";

        float CardWidth(int knowledge, int judging, float familiarity, int depth)
        {
            float width = CardBaseWidth + (20 - Math.Max(1, Math.Min(20, judging))) * CardWidthPerJudging;
            width *= knowledge >= 90 ? 1 : 1 + (90 - knowledge) / 50f;
            width *= 1 + (1 - familiarity) * CardUnfamiliarWidth;
            width /= 1 + CardDepthNarrowing * Math.Min(4, depth);
            return Math.Max(1, width);
        }
        static ScoutRange Around(float truth, float bias, float width, float floor)
        {
            float center = truth + bias * width * CardCenterBias;
            int low = (int)Math.Floor(Mathx.Clamp(center - width, Math.Max(1, floor), 99)), high = (int)Math.Ceiling(Mathx.Clamp(center + width, 1, 99));
            low = Math.Min(low, (int)Math.Floor(truth)); high = Math.Max(high, (int)Math.Ceiling(truth));
            return new ScoutRange { low = Math.Max(1, low), high = Math.Min(99, Math.Max(low, high)), known = true };
        }

        public ScoutReportCardData ScoutReportCard(Database db, string id, RecruitmentOverviewData overview = null)
        {
            var p = db?.Find(id); var card = new ScoutReportCardData { player = id };
            if (p == null || world == null || life == null) return card;
            card.generated = GeneratedWorld.IsGenerated(p.id) || GeneratedWorld.IsGenerated(p.team);
            card.territory = PlayerTerritory(db, p);
            var report = ReportFor(id); card.knowledge = Knowledge(id);
            card.exact = revealAttributes || p.team == club;
            card.judging = report?.judging > 0 ? report.judging : Staff("scout").judging;
            card.depth = report?.depth ?? 0; card.pending = report != null && report.confidence < 90;
            if (report != null) { card.reportAge = RecruitmentReportAge(report, life.day); card.stale = RecruitmentReportNeedsRefresh(report); card.observedDay = report.lastObserved > 0 ? report.lastObserved : report.due; }
            card.familiarity = card.exact ? 1 : ScoutingFamiliarity(db, p); card.familiarityLabel = FamiliarityLabel(card.familiarity);
            float level = p.rating + p.development;
            if (card.exact) { card.ability = new ScoutRange { low = (int)Math.Round(level), high = (int)Math.Round(level), known = true }; card.potential = new ScoutRange { low = (int)Math.Round(p.potential), high = (int)Math.Round(p.potential), known = true }; }
            else if (card.knowledge >= 40 && report != null && report.estimate > 0)
            {
                float width = CardWidth(card.knowledge, card.judging, card.familiarity, card.depth);
                // A report describes its observation date, not the hidden live player state.
                card.ability = Around(report.estimate, 0, Math.Max(width,report.uncertainty), 1);
                // Potential is harder to read, especially for young players.
                // The scout's judging of potential (report.potentialUncertainty) may differ from his judging of ability.
                float potentialFactor = report != null && report.uncertainty > 0 && report.potentialUncertainty > PotentialUncertaintyOffset ? Mathx.Clamp((report.potentialUncertainty - PotentialUncertaintyOffset) / report.uncertainty, .5f, 2) : 1;
                float potentialWidth = width * 1.5f * potentialFactor + (p.age < 21 ? 4 : p.age < 24 ? 2 : 0);
                card.potential = card.knowledge >= 65 && report.potential > 0 ? Around(report.potential, 0, Math.Max(potentialWidth,report.potentialUncertainty), card.ability.low) : default;
            }
            if (card.knowledge >= 40) DescribeProfile(db, p, card);
            Grade(db, p, card, overview);
            card.rival = RecruitmentRivalStatus(db, id);
            card.ambition = AssessedTrait(db, id, Ambition); card.adaptability = AssessedTrait(db, id, Adaptability);
            if (card.ability.known && !card.exact) card.comparable = ComparableSquadPlayer(db, p, card.ability.Middle);
            if (p.team != club && card.knowledge >= 40) { var interest = PlayerTransferInterest(db, id); card.interest = interest.label; card.interestReasons = interest.reasons; }
            return card;
        }

        /// <summary>"Comparable à …": the squad player of the same line whose level is closest to the estimate.</summary>
        string ComparableSquadPlayer(Database db, PlayerData p, float level)
        {
            string line = Group(p);
            var match = db.Squad(club).Where(x => x.id != p.id && Group(x) == line).OrderBy(x => Math.Abs(x.rating + x.development - level)).ThenBy(x => x.id, StringComparer.Ordinal).FirstOrDefault();
            if (match == null) return null;
            float gap = level - (match.rating + match.development);
            return "Comparable à " + match.name + (gap >= 2 ? " (un peu au-dessus)" : gap <= -2 ? " (un peu en dessous)" : " (niveau proche)");
        }
        static readonly Dictionary<string, string> AttributeLabels = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["gkDiving"] = "plongeon", ["gkHandling"] = "prise de balle", ["gkKicking"] = "jeu au pied", ["gkPositioning"] = "placement", ["gkReflexes"] = "réflexes",
            ["reactions"] = "réactivité", ["composure"] = "sang-froid", ["jumping"] = "détente", ["standingTackle"] = "tacle debout", ["slidingTackle"] = "tacle glissé",
            ["defensiveAwareness"] = "lecture défensive", ["interceptions"] = "interceptions", ["headingAccuracy"] = "jeu de tête", ["strength"] = "puissance",
            ["sprintSpeed"] = "vitesse", ["acceleration"] = "accélération", ["shortPassing"] = "passes courtes", ["longPassing"] = "passes longues", ["vision"] = "vision",
            ["ballControl"] = "contrôle", ["dribbling"] = "dribble", ["stamina"] = "endurance", ["longShots"] = "frappe de loin", ["agility"] = "agilité",
            ["finishing"] = "finition", ["positioning"] = "appels", ["shotPower"] = "puissance de frappe", ["crossing"] = "centres", ["aggression"] = "agressivité",
        };
        static readonly Dictionary<string, string[]> RelevantAttributes = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["GB"] = new[] { "gkDiving", "gkHandling", "gkKicking", "gkPositioning", "gkReflexes", "reactions", "composure", "jumping" },
            ["DEF"] = new[] { "standingTackle", "slidingTackle", "defensiveAwareness", "interceptions", "headingAccuracy", "strength", "jumping", "sprintSpeed", "shortPassing", "composure", "stamina", "aggression" },
            ["MIL"] = new[] { "shortPassing", "longPassing", "vision", "ballControl", "dribbling", "stamina", "interceptions", "standingTackle", "composure", "longShots", "agility", "reactions" },
            ["ATT"] = new[] { "finishing", "positioning", "sprintSpeed", "acceleration", "dribbling", "ballControl", "headingAccuracy", "shotPower", "composure", "crossing", "agility", "strength" },
        };
        static string Group(PlayerData p) => p.Goalkeeper ? "GB" : p.position == "DEF" || p.position == "MIL" || p.position == "ATT" ? p.position : "MIL";

        void DescribeProfile(Database db, PlayerData p, ScoutReportCardData card)
        {
            var known = RelevantAttributes[Group(p)].Where(k => p.attributes?.Any(a => a.key == k) == true)
                .Select(k => (key: k, value: AssessedAttribute(db, p.id, k))).Where(x => x.value.known).ToArray();
            if (known.Length >= 3)
            {
                float mean = (float)known.Average(x => (x.value.low + x.value.high) / 2f);
                card.strengths = known.Where(x => x.value.low >= mean + 1).OrderByDescending(x => x.value.low + x.value.high).ThenBy(x => x.key, StringComparer.Ordinal).Take(3).Select(x => AttributeLabels[x.key]).ToList();
                card.weaknesses = known.Where(x => x.value.high <= mean - 1).OrderBy(x => x.value.low + x.value.high).ThenBy(x => x.key, StringComparer.Ordinal).Take(3).Select(x => AttributeLabels[x.key]).ToList();
            }
            card.fit = TacticalFitHint(db, p, known.ToDictionary(x => x.key, x => x.value));
        }

        string TacticalFitHint(Database db, PlayerData p, Dictionary<string, AttributeAssessment> known)
        {
            var roles = (tactic?.withoutBall ?? Array.Empty<Slot>()).Select(s => FootballPositions.Canonical(s.role)).Distinct().ToArray();
            var natural = roles.Where(r => FootballPositions.Matches(p, r)).ToArray();
            string place = natural.Length > 0 ? "Poste naturel dans votre " + tactic.formation + " : " + FootballPositions.List(natural) + "."
                : "Aucun poste naturel dans votre " + (tactic?.formation ?? "système") + " : reconversion à prévoir.";
            float Mid(string key) => known.TryGetValue(key, out var a) ? (a.low + a.high) / 2f : -1;
            string style = null;
            if (tactic != null && tactic.pressing > .6f && Mid("stamina") >= 0 && Mid("stamina") < 11) style = "Pressing intense : endurance à surveiller.";
            else if (tactic != null && tactic.directness > .6f && Mid("sprintSpeed") >= 14) style = "Sa vitesse sert votre jeu direct.";
            else if (tactic != null && tactic.directness < .4f && Mid("shortPassing") >= 14) style = "Adapté à la conservation en passes courtes.";
            else if (tactic != null && tactic.line > .6f && p.position == "DEF" && Mid("sprintSpeed") >= 0 && Mid("sprintSpeed") < 11) style = "Ligne haute : manque de vitesse pour couvrir la profondeur.";
            return style == null ? place : place + " " + style;
        }

        void Grade(Database db, PlayerData p, ScoutReportCardData card, RecruitmentOverviewData overview)
        {
            if (!card.ability.known) { card.grade = "?"; card.gradeReason = card.pending ? "Observation en cours." : "Observation nécessaire pour noter ce profil."; return; }
            if (p.team == club) { card.grade = "—"; card.gradeReason = "Joueur de votre effectif."; return; }
            overview ??= RecruitmentOverview(db);
            var need = overview.needs.Where(n => FootballPositions.Matches(p, n.role)).OrderByDescending(n => n.priority).FirstOrDefault();
            float Level(string id) { var x = db.Find(id); return x == null ? 0 : x.rating + x.development; }
            float reference;
            if (need != null && need.players.Count > 0) reference = need.players.Take(Math.Max(1, need.starters)).Select(Level).Min();
            else reference = db.Squad(club).OrderByDescending(x => x.rating + x.development).Take(11).Select(x => x.rating + x.development).DefaultIfEmpty(65).Average();
            float width = (card.ability.high - card.ability.low) / 2f, mid = (card.ability.low + card.ability.high) / 2f;
            float score = mid - width * .5f - reference;
            if (need != null) score += need.priority >= 3 ? 4 : need.priority == 2 ? 3 : need.priority == 1 ? 1.5f : 0; else score -= 3;
            if (p.age <= 23 && card.potential.known) score += Mathx.Clamp(((card.potential.low + card.potential.high) / 2f - mid) * .25f, 0, 4);
            if (p.age >= 32) score -= p.age - 31;
            bool affordable = (p.team == "free" || p.value <= TransferBudget) && MonthlySalary(p.wage) <= RecruitmentWageRoom(db);
            if (!affordable) score -= 2;
            card.grade = score >= GradeA ? "A" : score >= GradeB ? "B" : score >= GradeC ? "C" : score >= GradeD ? "D" : "E";
            string role = need != null ? FootballPositions.Label(need.role) : "votre onze";
            card.gradeReason = (card.grade == "A" ? "Titulaire probable · " : card.grade == "B" ? "Renfort crédible · " : card.grade == "C" ? "Rotation · " : card.grade == "D" ? "En retrait · " : "Pas au niveau · ")
                + "comparé à " + role + (need != null && need.priority >= 2 ? " (besoin prioritaire)" : need == null ? " (hors postes du système)" : "")
                + (affordable ? "" : " · hors budget actuel") + (width >= 6 ? " · incertitude élevée" : "");
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

namespace Touchline.Core
{
    // Standing assignment of one scout of the department (FM-like "scouting assignment").
    [Serializable] public class ScoutAssignment
    {
        public string staff, focus = "needs", country = "Tous", role = "Tous";
        public int maxAge = 35, started, next, reports;
    }
    // How well one scout knows a territory (0–100). Grows with completed reports there.
    [Serializable] public class ScoutRegionKnowledge { public string staff, country; public int level; }
    // Derived view of a scout of the department; nothing here is saved.
    public sealed class ScoutProfile
    {
        public StaffMember member; public string key, home;
        public int ability, potential, adaptability, active, capacity;
        public bool chief; public ScoutAssignment assignment;
        public List<ScoutRegionKnowledge> regions = new List<ScoutRegionKnowledge>();
    }
    public partial class Career
    {
        public List<ScoutAssignment> scoutAssignments = new List<ScoutAssignment>();
        public List<ScoutRegionKnowledge> scoutRegions = new List<ScoutRegionKnowledge>();
        public List<string> scoutRecommendations = new List<string>();

        public static readonly string[] ScoutFocuses = { "needs", "territory", "youth", "free", "shortlist" };
        static readonly string[] ScoutRoles = { "Tous", "GB", "DEF", "MIL", "ATT", "GK", "CB", "LB", "RB", "DM", "CM", "AM", "LW", "RW", "ST" };
        const string AssignmentTag = "assign-";      // ScoutReport.mission prefix for department reports
        const int ScoutCycleDays = 14;                // days between two new profiles for one average scout
        const int ScoutUnknownRegionDays = 6;         // extra days while the scout knows the territory < 50/100
        const int ScoutCycleMin = 7, ScoutCycleMax = 24;
        const int ScoutConcurrentReports = 2;         // simultaneous observations per assigned scout
        const int HomeRegionKnowledge = 80;           // 0–100: a scout knows the market where he already works
        const int RegionKnowledgeUnknownThreshold = 50;
        const float RegionGainPerReport = 6;          // 0–100 points per completed report, ×(0,5 + adaptability/20)
        const float AbroadObservationFactor = 1.5f;   // travel: an observation abroad costs 50 % more
        const int AssignmentRetryDays = 7;            // wait when no profile or no cash is available
        const int YouthFocusMaxAge = 21;
        const int MaxScoutRecommendations = 12;
        const float ScoutPickErrorBase = 1.5f, ScoutPickErrorPerPoint = .45f; // rating points of a scout's misjudgement when choosing whom to observe
        const int MaxScoutSlots = 5;

        /// <summary>Scouts the club may employ at the same time: 1 to 5 depending on revenue (chief scout included).</summary>
        public int ScoutSlots => (int)Mathx.Clamp(1 + (float)Math.Floor(Math.Log10(Math.Max(1, life?.revenue ?? 0) / 5000000.0) * 2), 1, MaxScoutSlots);
        static string ScoutKey(StaffMember s) => string.IsNullOrEmpty(s?.id) ? "scout:" + s?.name : s.id;
        static int TraitAround(string key, string trait, int baseValue) => (int)Mathx.Clamp(baseValue + (int)(MixedIdentity(key + "/" + trait) % 7) - 3, 1, 20);
        /// <summary>Judging of potential (1–20): close to the judging of ability, stable for one person.</summary>
        public static int ScoutPotentialJudging(StaffMember s) => s == null || s.wage <= 0 ? Math.Max(1, s?.judging ?? 1) : TraitAround(ScoutKey(s), "potential", Math.Max(1, s.judging));
        public static int ScoutAdaptability(StaffMember s) => s == null ? 10 : TraitAround(ScoutKey(s), "adaptability", Math.Max(1, s.people));

        IEnumerable<StaffMember> DepartmentScouts()
        {
            EnsureStaff(); return life.staff.members.Where(m => m.role == "scout" && m.wage > 0);
        }
        string HomeTerritory(Database db) => db?.clubs == null ? null : ScoutingGeography.Country(db, db.clubs.FirstOrDefault(c => c.id == club));
        void EnsureDepartment(Database db)
        {
            scoutAssignments ??= new List<ScoutAssignment>(); scoutRegions ??= new List<ScoutRegionKnowledge>(); scoutRecommendations ??= new List<string>();
            string home = HomeTerritory(db);
            foreach (var s in DepartmentScouts())
            {
                string key = ScoutKey(s);
                if (home != null && !scoutRegions.Any(r => r.staff == key && r.country == home)) scoutRegions.Add(new ScoutRegionKnowledge { staff = key, country = home, level = HomeRegionKnowledge });
                // A scout hired from another club already knows that club's market.
                if (s.id != null && s.id.StartsWith("staff-gen-", StringComparison.Ordinal) && s.id.EndsWith("-scout", StringComparison.Ordinal))
                {
                    string origin = s.id.Substring("staff-gen-".Length, s.id.Length - "staff-gen-".Length - "-scout".Length);
                    string country = ScoutingGeography.Country(db, db.clubs.FirstOrDefault(c => c.id == origin));
                    if (country != null && !scoutRegions.Any(r => r.staff == key && r.country == country)) scoutRegions.Add(new ScoutRegionKnowledge { staff = key, country = country, level = HomeRegionKnowledge });
                }
            }
        }
        public int RegionKnowledge(string staffKey, string country) => scoutRegions?.FirstOrDefault(r => r.staff == staffKey && r.country == country)?.level ?? 0;
        /// <summary>0–1: best knowledge of this territory among the scouts currently employed.</summary>
        float DepartmentFamiliarity(string territory)
        {
            if (territory == null || scoutRegions == null || life?.staff?.members == null) return 0;
            var keys = new HashSet<string>(life.staff.members.Where(m => m.role == "scout" && m.wage > 0).Select(ScoutKey));
            int best = 0; foreach (var r in scoutRegions) if (r.country == territory && keys.Contains(r.staff)) best = Math.Max(best, r.level);
            return best / 100f;
        }

        public List<ScoutProfile> ScoutingDepartment(Database db)
        {
            EnsureDepartment(db); var chief = Staff("scout"); var result = new List<ScoutProfile>();
            foreach (var s in DepartmentScouts())
            {
                string key = ScoutKey(s);
                result.Add(new ScoutProfile { member = s, key = key, chief = ReferenceEquals(s, chief), ability = Math.Max(1, s.judging), potential = ScoutPotentialJudging(s), adaptability = ScoutAdaptability(s),
                    home = HomeTerritory(db), assignment = scoutAssignments.FirstOrDefault(a => a.staff == key), capacity = ScoutConcurrentReports,
                    active = world?.reports?.Count(r => r.mission == AssignmentTag + key && r.confidence < 90) ?? 0,
                    regions = scoutRegions.Where(r => r.staff == key).OrderByDescending(r => r.level).ThenBy(r => r.country, StringComparer.Ordinal).ToList() });
            }
            return result;
        }

        public ScoutAssignment AssignScout(Database db, string staffId, string focus, string country = "Tous", string role = "Tous", int maxAge = 35)
        {
            OffPitch(); EnsureScouting(); EnsureDepartment(db);
            var s = DepartmentScouts().FirstOrDefault(m => ScoutKey(m) == staffId) ?? throw new InvalidOperationException("Ce recruteur ne fait pas partie de votre cellule.");
            if (!ScoutFocuses.Contains(focus)) throw new ArgumentException("Affectation inconnue.");
            if (string.IsNullOrWhiteSpace(country)) country = "Tous"; if (string.IsNullOrWhiteSpace(role)) role = "Tous";
            if (country != "Tous" && !ScoutingGeography.Countries(db).Contains(country)) throw new ArgumentException("Ce territoire n’est pas couvert par la base de recrutement.");
            if (!ScoutRoles.Contains(role)) throw new ArgumentException("Poste inconnu.");
            if (maxAge < 16 || maxAge > 45) throw new ArgumentException("Âge maximal invalide.");
            string key = ScoutKey(s); scoutAssignments.RemoveAll(a => a.staff == key);
            var assignment = new ScoutAssignment { staff = key, focus = focus, country = country, role = role, maxAge = focus == "youth" ? Math.Min(maxAge, YouthFocusMaxAge) : maxAge, started = life.day, next = life.day + 1 };
            scoutAssignments.Add(assignment);
            ScoutMail(s.name, "Nouvelle affectation", AssignmentLabel(assignment) + ". Premier profil attendu sous " + ScoutCycle(db, s, assignment) + " jours ; chaque observation est facturée à son lancement" + (country != "Tous" && country != HomeTerritory(db) ? " (déplacement à l’étranger : +50 %)." : "."));
            return assignment;
        }
        public void ClearScoutAssignment(string staffId)
        {
            OffPitch(); scoutAssignments ??= new List<ScoutAssignment>();
            if (scoutAssignments.RemoveAll(a => a.staff == staffId) == 0) throw new InvalidOperationException("Aucune affectation en cours.");
        }
        public static string AssignmentLabel(ScoutAssignment a)
        {
            if (a == null) return "Sans affectation";
            string where = a.country == "Tous" ? "tous territoires" : a.country, role = a.role == "Tous" ? "tous postes" : FootballPositions.Label(a.role);
            return a.focus switch
            {
                "territory" => "Exploration · " + where + " · " + role + " · ≤ " + a.maxAge + " ans",
                "youth" => "Jeunes talents · " + where + " · " + role + " · ≤ " + a.maxAge + " ans",
                "free" => "Joueurs libres · " + role + " · ≤ " + a.maxAge + " ans",
                "shortlist" => "Suivi de ma sélection",
                _ => "Besoins de l’effectif · " + where + (a.role == "Tous" ? "" : " · " + role) + " · ≤ " + a.maxAge + " ans",
            };
        }
        int ScoutCycle(Database db, StaffMember s, ScoutAssignment a)
        {
            int days = ScoutCycleDays - (Math.Max(1, s.judging) - 10) / 3;
            if (a.country != "Tous" && RegionKnowledge(ScoutKey(s), a.country) < RegionKnowledgeUnknownThreshold) days += ScoutUnknownRegionDays;
            return (int)Mathx.Clamp(days, ScoutCycleMin, ScoutCycleMax);
        }

        /// <summary>The level this scout believes a player has: truth plus a stable error that shrinks with his judging.</summary>
        float ScoutPerceivedLevel(StaffMember s, PlayerData p, bool potential)
        {
            int judging = potential ? ScoutPotentialJudging(s) : Math.Max(1, s.judging);
            float error = ScoutPickErrorBase + (20 - judging) * ScoutPickErrorPerPoint;
            float truth = potential ? Math.Max(p.potential, p.rating + p.development) : p.rating + p.development;
            return truth + MixedBias(ScoutKey(s) + "/" + p.id + (potential ? "/pick-potential/" : "/pick-ability/") + world.year) * error;
        }
        public IEnumerable<PlayerData> AssignmentCandidates(Database db, StaffMember s, ScoutAssignment a)
        {
            var reports = world.reports.Where(r => r.club == club || string.IsNullOrEmpty(r.club)).GroupBy(r => r.player).ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.started).First());
            bool NeedsReport(string id) => !reports.TryGetValue(id, out var r) || r.confidence >= 90 && ReportKnowledge(r) < 90;
            string role = a.role;
            if (a.focus == "needs" && role == "Tous") role = RecruitmentOverview(db).needs.Where(n => n.priority >= 1).Select(n => n.searchRole).FirstOrDefault() ?? "Tous";
            var territory = a.country == "Tous" ? null : new HashSet<string>(ScoutingGeography.ClubIds(db, a.country));
            long fees = TransferBudget, wages = Math.Max(RecruitmentWageRoom(db), MonthlySalary(Payroll(db) / Math.Max(1, db.Squad(club).Count)));
            var followed = new HashSet<string>(shortlist ?? new List<string>());
            var pool = db.players.Where(p => Scoutable(p) && p.age <= a.maxAge && p.age >= 16 && FootballPositions.Matches(p, role) && NeedsReport(p.id)
                && (territory == null || territory.Contains(p.team))
                && (a.focus != "free" || p.team == "free") && (a.focus != "shortlist" || followed.Contains(p.id))
                // The department does not waste trips on players the club can't afford (needs: budget; others: twice the budget).
                && (a.focus == "shortlist" || (p.team == "free" || p.value <= (a.focus == "needs" ? fees : fees * 2)) && MonthlySalary(p.wage) <= (a.focus == "needs" ? wages * 3 / 2 : wages * 3)));
            bool potential = a.focus == "youth";
            return pool.OrderByDescending(p => ScoutPerceivedLevel(s, p, potential)).ThenBy(p => p.id, StringComparer.Ordinal);
        }

        bool IsAssignmentReport(ScoutReport r) => r?.mission != null && r.mission.StartsWith(AssignmentTag, StringComparison.Ordinal);
        StaffMember ReportScout(ScoutReport r)
        {
            if (!IsAssignmentReport(r)) return null; string key = r.mission.Substring(AssignmentTag.Length);
            return DepartmentScouts().FirstOrDefault(m => ScoutKey(m) == key);
        }

        void ScoutingDepartmentDay(Database db)
        {
            if (world.managerStatus != "employed" || scoutAssignments == null || scoutAssignments.Count == 0) return;
            EnsureDepartment(db); var scouts = DepartmentScouts().GroupBy(ScoutKey).ToDictionary(g => g.Key, g => g.First());
            scoutAssignments.RemoveAll(a => !scouts.ContainsKey(a.staff));
            foreach (var a in scoutAssignments.ToArray())
            {
                if (life.day < a.next) continue; var s = scouts[a.staff];
                if (world.reports.Count(r => r.mission == AssignmentTag + a.staff && r.confidence < 90) >= ScoutConcurrentReports) { a.next = life.day + 1; continue; }
                var p = AssignmentCandidates(db, s, a).FirstOrDefault();
                if (p == null) { a.next = life.day + AssignmentRetryDays; continue; }
                string territory = PlayerTerritory(db, p); bool abroad = territory != null && territory != HomeTerritory(db);
                long cost = (long)(ObservationCost * (abroad ? AbroadObservationFactor : 1));
                // Keep a cash reserve: assignments never push the club into an overdraft.
                if (cost > life.cash - life.revenue / 40) { a.next = life.day + AssignmentRetryDays; continue; }
                Charge(cost, "Cellule de recrutement • observation de " + p.name);
                StartObservation(db, p, null, s, AssignmentTag + a.staff);
                a.reports++; a.next = life.day + ScoutCycle(db, s, a);
            }
        }

        void OnScoutReportCompleted(Database db, PlayerData p, ScoutReport r)
        {
            var scout = ReportScout(r); if (scout == null) return;
            string territory = PlayerTerritory(db, p);
            if (territory != null)
            {
                var k = scoutRegions.FirstOrDefault(x => x.staff == ScoutKey(scout) && x.country == territory);
                if (k == null) scoutRegions.Add(k = new ScoutRegionKnowledge { staff = ScoutKey(scout), country = territory });
                k.level = (int)Math.Min(100, k.level + RegionGainPerReport * (.5f + ScoutAdaptability(scout) / 20f));
            }
            var card = ScoutReportCard(db, p.id);
            // The chief scout only recommends a profile who would at least listen to the club.
            if ((card.grade == "A" || card.grade == "B") && !scoutRecommendations.Contains(p.id) && !PlayerTransferInterest(db, p.id).refuses)
            {
                scoutRecommendations.Add(p.id); if (scoutRecommendations.Count > MaxScoutRecommendations) scoutRecommendations.RemoveAt(0);
                Mail(Staff("scout").name, "Recommandation de la cellule", p.name + " (" + p.age + " ans, " + ClubLabel(db, p.team) + ") : note " + card.grade + ". " + card.gradeReason + ". Observé par " + scout.name + ".", p.id, "scout");
            }
        }
        /// <summary>Recommended players still worth a look (scoutable, report known), most recent first.</summary>
        public List<string> DepartmentRecommendations(Database db)
            => (scoutRecommendations ?? new List<string>()).AsEnumerable().Reverse().Where(id => Scoutable(db.Find(id)) && Knowledge(id) >= 40 && !PlayerTransferInterest(db,id).refuses).ToList();
    }
}

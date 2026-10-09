using System;
using System.Collections.Generic;
using System.Linq;

namespace Touchline.Core
{
    // Derived views only: opening recruitment never creates contracts or changes a save.
    public sealed class RecruitmentOverviewData
    {
        public List<RecruitmentNeed> needs = new List<RecruitmentNeed>();
        public long transferBudget, monthlyWageRoom;
        public int observations, reportsReady, reportsStale, actionableOffers;
    }
    public sealed class RecruitmentNeed
    {
        public string role, searchRole, reason;
        public int starters, target, assigned, available, expiring, priority;
        public List<string> players = new List<string>();
    }
    public sealed class RecruitmentRecommendation
    {
        public string player, reason;
        public int knowledge, reportAge;
        public bool stale, affordable, levelKnown;
        public float assessedLevel;
        public long estimatedFee, currentMonthlyWage;
    }
    public partial class Career
    {
        const int RecruitmentContractHorizon = 180; // days: prepare renewals or replacements six months ahead.

        bool RecruitmentAvailable(PlayerData p)
        {
            var person = life.players.FirstOrDefault(x => x.id == p.id);
            var injury = Injury(p.id);
            return p.unavailableDays <= 0 && (person == null || person.restUntil <= life.day && person.banUntil <= life.day)
                && (injury == null || injury.reliefUntil >= life.day);
        }
        long RecruitmentWageRoom(Database db) => MonthlySalary(Math.Max(0, WageBudget - Payroll(db) - ReservedWages));
        static int RecruitmentReportAge(ScoutReport r, int day) => Math.Max(0, day - (r.lastObserved > 0 ? r.lastObserved : r.due));

        public bool RecruitmentReportNeedsRefresh(ScoutReport report) => report != null && report.confidence >= 90 && Knowledge(report.player) < 90;

        public bool RecruitmentOfferNeedsDecision(Database db, TransferOffer offer)
        {
            if (offer == null || !string.IsNullOrEmpty(offer.destination) && offer.destination != club) return false;
            var player = db.Find(offer.player);
            if (player == null || PlayingCareerEnded(player)) return false;
            if (offer.status == "sale") return player.team == club && offer.due >= life.day;
            // A previous counter-offer is history once a newer negotiation exists.
            var latest = world.offers.LastOrDefault(o => o.player == offer.player && o.status != "sale" && o.status != "sold"
                && (string.IsNullOrEmpty(o.destination) || o.destination == club));
            if (!ReferenceEquals(latest, offer) || player.team != offer.seller) return false;
            return offer.status == "counter" || offer.status == "accepted" && offer.due + 7 >= life.day;
        }

        public RecruitmentOverviewData RecruitmentOverview(Database db)
        {
            var result = new RecruitmentOverviewData();
            if (db?.players == null || life == null || world == null) return result;
            result.transferBudget = TransferBudget;
            result.monthlyWageRoom = RecruitmentWageRoom(db);
            var reports = world.reports.Where(r => string.IsNullOrEmpty(r.club) || r.club == club)
                .GroupBy(r => r.player).Select(g => g.OrderByDescending(r => r.started).First())
                .Where(r => Scoutable(db.Find(r.player))).ToArray();
            result.observations = reports.Count(r => r.confidence < 90);
            result.reportsReady = reports.Count(r => r.confidence >= 90 && !RecruitmentReportNeedsRefresh(r));
            result.reportsStale = reports.Count(RecruitmentReportNeedsRefresh);
            result.actionableOffers = world.offers.Count(o => RecruitmentOfferNeedsDecision(db, o));

            var roles = (tactic?.withoutBall ?? Array.Empty<Slot>()).Select(s => FootballPositions.Canonical(s.role)).ToArray();
            // Two layers: starting places first, then one rotation place per starter. Every
            // player gets one assignment, even when qualified at several positions.
            var places = roles.Concat(roles).ToArray();
            var squad = db.Squad(club).Where(p => !string.IsNullOrEmpty(p.id)).GroupBy(p => p.id).Select(g => g.First())
                .OrderByDescending(RecruitmentAvailable).ThenBy(p => roles.Distinct().Count(r => FootballPositions.Matches(p, r)))
                .ThenBy(p => p.id, StringComparer.Ordinal).ToArray();
            var assigned = Enumerable.Repeat(-1, places.Length).ToArray();
            bool Place(int player, bool[] visited)
            {
                // Fill a vacant starting position before shuffling an already covered one.
                for (int s = 0; s < places.Length; s++)
                    if (!visited[s] && assigned[s] < 0 && FootballPositions.Matches(squad[player], places[s]))
                    { visited[s] = true; assigned[s] = player; return true; }
                for (int s = 0; s < places.Length; s++)
                {
                    if (visited[s] || !FootballPositions.Matches(squad[player], places[s])) continue;
                    visited[s] = true;
                    // A missing player must not displace available cover to another role.
                    if (assigned[s] < 0 || (RecruitmentAvailable(squad[player]) || !RecruitmentAvailable(squad[assigned[s]])) && Place(assigned[s], visited)) { assigned[s] = player; return true; }
                }
                return false;
            }
            for (int p = 0; p < squad.Length; p++) Place(p, new bool[places.Length]);
            foreach (var group in roles.GroupBy(r => r))
            {
                var need = new RecruitmentNeed { role = group.Key, starters = group.Count(), target = group.Count() * 2 };
                need.searchRole = need.role == "LM" ? "LW" : need.role == "RM" ? "RW" : need.role == "LWB" ? "LB"
                    : need.role == "RWB" ? "RB" : need.role == "CF" || need.role == "SS" ? "ST" : need.role;
                for (int s = 0; s < places.Length; s++) if (places[s] == need.role && assigned[s] >= 0) need.players.Add(squad[assigned[s]].id);
                need.assigned = need.players.Count;
                need.available = need.players.Count(id => RecruitmentAvailable(db.Find(id)));
                need.expiring = need.players.Count(id => {
                    var contract = world.contracts.FirstOrDefault(c => c.player == id && c.club == club);
                    if (contract == null) return false;
                    int end = !string.IsNullOrEmpty(contract.parent) ? contract.loanUntil : contract.until;
                    return end <= life.day + RecruitmentContractHorizon;
                });
                need.priority = need.available < need.starters ? 3 : need.assigned < need.target ? 2
                    : need.expiring > 0 || need.available < need.target ? 1 : 0;
                need.reason = need.available < need.starters ? need.assigned >= need.starters
                    ? "Absences : adapter le onze et prévoir une couverture interne avant de recruter."
                    : "Pas assez de spécialistes pour le onze : recrutement ou formation à étudier."
                    : need.assigned < need.target ? "Rotation courte : étudier un renfort ou une promotion."
                    : need.expiring > 0 ? "Contrat ou prêt à échéance sous six mois : anticiper."
                    : need.available < need.target ? "Absence temporaire : privilégier une couverture interne."
                    : "Deux options par place dans la tactique actuelle.";
                result.needs.Add(need);
            }
            result.needs = result.needs.OrderByDescending(n => n.priority).ThenBy(n => n.role, StringComparer.Ordinal).ToList();
            return result;
        }

        public List<RecruitmentRecommendation> RecruitmentRecommendations(Database db, string role = "Tous", int limit = 6)
        {
            var result = new List<RecruitmentRecommendation>();
            if (db?.players == null || world == null || life == null || limit <= 0) return result;
            long transfer = TransferBudget, wage = RecruitmentWageRoom(db);
            var reports = world.reports.Where(r => string.IsNullOrEmpty(r.club) || r.club == club)
                .GroupBy(r => r.player).ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.started).First());
            foreach (var p in db.players.Where(p => Scoutable(p) && FootballPositions.Matches(p, role)))
            {
                reports.TryGetValue(p.id, out var report);
                int knowledge = revealAttributes ? 100 : report == null ? 0 : ReportKnowledge(report);
                // Do not use AssessedLevel's legacy fallback to live hidden ratings for an empty estimate.
                if (!revealAttributes && (knowledge < 40 || report.estimate <= 0)) continue;
                int age = report == null ? 0 : RecruitmentReportAge(report, life.day);
                var item = new RecruitmentRecommendation { player = p.id, knowledge = knowledge, reportAge = age,
                    stale = RecruitmentReportNeedsRefresh(report), levelKnown = true,
                    assessedLevel = revealAttributes ? p.rating + p.development : report.estimate,
                    estimatedFee = p.team == "free" ? 0 : Math.Max(0, p.value), currentMonthlyWage = MonthlySalary(Math.Max(0, p.wage)) };
                item.affordable = item.estimatedFee <= transfer && item.currentMonthlyWage <= wage;
                item.reason = item.stale ? "Rapport ancien : renouveler l’observation avant décision."
                    : !item.affordable ? "Valeur ou salaire actuel hors enveloppe : prêt ou négociation à étudier."
                    : knowledge < 90 ? "Premières impressions : poursuivre l’observation."
                    : "Profil à comparer ; indemnité, salaire demandé et primes à confirmer avec l’agent.";
                result.Add(item);
            }
            return result.OrderBy(r => r.stale).ThenByDescending(r => r.affordable).ThenByDescending(r => r.knowledge)
                .ThenByDescending(r => r.assessedLevel).ThenBy(r => r.player, StringComparer.Ordinal).Take(limit).ToList();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

namespace Touchline.Core
{
    [Serializable] public class RivalBid { public string player, club, seller, status = "open"; public int day, decision; public long fee, wage; }
    // Competition for the players this club follows. AI clubs judge targets
    // through their own noisy, seeded scouting (never the exact hidden level),
    // pay with their own accounts and stop as soon as our agreement is signed.
    public partial class Career
    {
        public List<RivalBid> rivalBids = new List<RivalBid>();
        public List<string> recruitmentAlerts = new List<string>();
        const int RivalDecisionDays = 10;          // days between a rival bid and the selling club's decision
        const int MaxRivalBids = 60, MaxRecruitmentAlerts = 300;
        const float RivalBidChanceStarter = .2f;   // weekly chance when the rival sees him as a starter
        const float RivalBidChanceRotation = .08f; // weekly chance for a rotation profile
        const float RivalFeePremium = 1.05f, RivalWageRise = 1.1f;
        const float AiTransferShare = .12f;        // share of revenue an AI club may spend on one target
        const float AiScoutErrorMax = 6, AiScoutErrorMin = 2; // rating points of AI misjudgement (small → big club)
        const int AlertContractDays = 180;         // six months: pre-contract window
        const int DeadlineDays = 7;                // last days of a window: daily rival activity
        const float DeadlineBidFactor = 2;         // rival bids twice as likely in those days

        /// <summary>Last day of the open transfer window (31 January or 1 September), or -1 when closed.</summary>
        public int WindowCloseDay => !WindowOpen ? -1 : DayOf(Date.Month == 1 ? new DateTime(Date.Year, 1, 31) : new DateTime(Date.Year, 9, 1));
        public bool DeadlinePeriod => WindowOpen && WindowCloseDay - life.day < DeadlineDays;

        [NonSerialized] Dictionary<string, float> aiStrengths; [NonSerialized] int aiStrengthDay = -1; [NonSerialized] PlayerData[] aiStrengthSource;
        float AiStrength(Database db, string id)
        {
            if (aiStrengths == null || aiStrengthDay / 7 != life.day / 7 || !ReferenceEquals(aiStrengthSource, db.players))
            {
                aiStrengthDay = life.day; aiStrengthSource = db.players;
                aiStrengths = db.players.Where(p => p.team != null && p.team != "free" && p.team != "retired" && !p.team.StartsWith("academy-", StringComparison.Ordinal))
                    .GroupBy(p => p.team).ToDictionary(g => g.Key, g => g.Select(p => p.rating + p.development).OrderByDescending(v => v).Take(11).DefaultIfEmpty(50).Average(), StringComparer.Ordinal);
            }
            return aiStrengths.TryGetValue(id, out var v) ? v : 0;
        }
        /// <summary>What an AI club believes about a player: exact level plus a stable error shrinking with its means.</summary>
        public float AiPerceivedLevel(ClubData team, PlayerData p)
        {
            float quality = Mathx.Clamp(((float)Math.Log10(Math.Max(1, team.annualRevenue)) - 6) / 3, 0, 1);
            float error = AiScoutErrorMax - (AiScoutErrorMax - AiScoutErrorMin) * quality;
            return p.rating + p.development + AssessmentBias(team.id + "/" + p.id, "ai-scout", world?.year ?? 0) * error;
        }

        /// <summary>The AI club most interested in this player this season (or null), and whether it sees him as a starter.</summary>
        public ClubData RecruitmentRival(Database db, PlayerData p, out bool starter)
        {
            starter = false; if (p == null || world == null || db?.clubs == null || p.team == "free" || p.team == club) return null;
            ClubData best = null; uint bestKey = uint.MaxValue;
            foreach (var team in db.clubs)
            {
                if (team.id == club || team.id == p.team || team.annualRevenue * AiTransferShare < p.value * RivalFeePremium) continue;
                float strength = AiStrength(db, team.id); if (strength <= 0) continue;
                float seen = AiPerceivedLevel(team, p);
                if (seen < strength - 2 || seen > strength + 8) continue; // a step down or out of reach
                uint key = StableIdentity("rival/" + team.id + "/" + p.id + "/" + world.year);
                if (best == null || key < bestKey) { best = team; bestKey = key; starter = seen >= strength + 2; }
            }
            return best;
        }

        public string RecruitmentRivalStatus(Database db, string id)
        {
            var bid = rivalBids?.LastOrDefault(b => b.player == id);
            if (bid != null && bid.status == "open") return "Offre de " + ClubLabel(db, bid.club) + " · décision du vendeur vers le " + Epoch.AddDays(bid.decision).ToString("dd/MM");
            if (bid != null && bid.status == "signed" && life.day - bid.decision < 60) return "Recruté par " + ClubLabel(db, bid.club);
            if (!shortlist.Contains(id)) return null;
            var p = db.Find(id); var rival = RecruitmentRival(db, p, out bool starter);
            return rival == null ? "Aucun intérêt concurrent connu" : "Intérêt de " + ClubLabel(db, rival.id) + (starter ? " (titulaire visé)" : " (rotation)");
        }
        static string ClubLabel(Database db, string id) => db.clubs.FirstOrDefault(c => c.id == id)?.name ?? id;

        bool RecruitmentAlert(string key)
        {
            recruitmentAlerts ??= new List<string>(); if (recruitmentAlerts.Contains(key)) return false;
            recruitmentAlerts.Add(key); if (recruitmentAlerts.Count > MaxRecruitmentAlerts) recruitmentAlerts.RemoveRange(0, recruitmentAlerts.Count - MaxRecruitmentAlerts);
            return true;
        }
        bool OurAgreement(string id) => world.offers.Any(o => o.player == id && OfferForManagedClub(o) && (o.status == "accepted" || o.status == "scheduled"));

        void RecruitmentRivalsDay(Database db)
        {
            rivalBids ??= new List<RivalBid>(); shortlist ??= new List<string>();
            foreach (var bid in rivalBids.Where(b => b.status == "open" && b.decision <= life.day).ToArray()) ResolveRivalBid(db, bid);
            if (world.managerStatus != "employed") return;
            foreach (var id in shortlist.ToArray())
            {
                var p = db.Find(id); if (!Scoutable(p) || p.team == "free") continue;
                var contract = world.contracts.FirstOrDefault(c => c.player == id && c.parent == null);
                if (contract != null && contract.until > life.day && contract.until - life.day <= AlertContractDays && RecruitmentAlert("contract:" + id + ":" + contract.until))
                    Mail("Cellule recrutement", "Fin de contrat en vue", p.name + " arrive en fin de contrat le " + Epoch.AddDays(contract.until).ToString("dd/MM/yyyy") + ". Un précontrat est envisageable, sans indemnité, si le joueur accepte votre projet.", id, "scout");
            }
            bool deadline = DeadlinePeriod;
            if (life.day % 7 != 0 && !deadline) return;
            var tracked = shortlist.Concat(world.offers.Where(o => OfferForManagedClub(o) && (o.status == "pending" || o.status == "counter") && !o.renewal).Select(o => o.player)).Distinct().ToArray();
            foreach (var id in tracked)
            {
                var p = db.Find(id); if (!Scoutable(p) || p.team == "free" || OurAgreement(id) || HasActiveLoan(id)) continue;
                var rival = RecruitmentRival(db, p, out bool starter); if (rival == null) continue;
                if (shortlist.Contains(id) && RecruitmentAlert("interest:" + id + ":" + world.year))
                    Mail("Cellule recrutement", "Concurrence sur une cible", ClubLabel(db, rival.id) + " suit aussi " + p.name + (starter ? " et le voit comme titulaire." : " pour sa rotation.") + " Une offre rivale reste possible pendant les fenêtres de transfert.", id, "scout");
                if (!WindowOpen || rivalBids.Any(b => b.player == id && b.status == "open")) continue;
                float chance = (starter ? RivalBidChanceStarter : RivalBidChanceRotation) * (deadline ? DeadlineBidFactor : 1);
                // Stable draw: does not consume the career's random stream.
                if (StableIdentity("rival-bid/" + id + "/" + rival.id + "/" + life.day) % 1000 >= chance * 1000) continue;
                var bid = new RivalBid { player = id, club = rival.id, seller = p.team, day = life.day, decision = Math.Min(life.day + RivalDecisionDays, WindowCloseDay), fee = (long)(p.value * RivalFeePremium), wage = (long)(p.wage * RivalWageRise) };
                rivalBids.Add(bid); foreach (var old in rivalBids.Where(b => b.status != "open").Take(Math.Max(0, rivalBids.Count - MaxRivalBids)).ToArray()) rivalBids.Remove(old);
                Mail("Cellule recrutement", "Offre concurrente", ClubLabel(db, rival.id) + " a proposé environ " + bid.fee.ToString("N0") + " € pour " + p.name + ". Le club vendeur décidera vers le " + Epoch.AddDays(bid.decision).ToString("dd/MM") + " ; seul un accord signé de votre part l’arrête.", id, "transfer");
            }
        }

        void ResolveRivalBid(Database db, RivalBid bid)
        {
            var p = db.Find(bid.player); var team = db.clubs.FirstOrDefault(c => c.id == bid.club);
            // An offer prepared before a manager move is no authorization to buy
            // or sell on the human manager's behalf at their new club.
            if (bid.club == club || bid.seller == club) { bid.status = "lapsed"; return; }
            if (p == null || team == null || p.team != bid.seller || OurAgreement(bid.player) || HasActiveLoan(bid.player) || world.offers.Any(o => o.player == bid.player && o.status == "sale")) { bid.status = "lapsed"; return; }
            if (!WindowOpen || AiStrength(db, bid.seller) <= 0 || db.Squad(bid.seller).Count <= 22) { bid.status = "refused"; NotifyRivalOutcome(db, bid, p, false); return; }
            long squadWages = db.Squad(team.id).Sum(x => x.wage);
            try { ValidateNpcPayment(db, team.id, bid.fee); } catch (InvalidOperationException) { bid.status = "refused"; NotifyRivalOutcome(db, bid, p, false); return; }
            if (squadWages + bid.wage > AiGrossWageCeiling(team)) { bid.status = "refused"; NotifyRivalOutcome(db, bid, p, false); return; }
            SettleSignedPrincipal(db, team.id, bid.seller, bid.fee, "Transfert IA · " + p.name);
            p.team = team.id; p.wage = bid.wage; p.salarySource = "Salaire négocié dans la simulation IA";
            var contract = Contract(db, p.id); contract.club = team.id; contract.wage = bid.wage; contract.until = life.day + 365 * 3; contract.joined = life.day; contract.estimated = true; contract.parent = null; contract.aiRelease = false; contract.nextWage = 0; contract.wageChangeDay = 0;
            AfterFinancialTermsChange(db, team.id, bid.seller); SavePlayer(p);
            world.aiTransfers.Add(new AiTransferRecord { player = p.id, buyer = team.id, seller = bid.seller, year = world.year, fee = bid.fee, wage = bid.wage });
            foreach (var o in world.offers.Where(o => o.player == p.id && OfferForManagedClub(o) && (o.status == "pending" || o.status == "counter"))) o.status = "expired";
            bid.status = "signed"; NotifyRivalOutcome(db, bid, p, true);
        }
        void NotifyRivalOutcome(Database db, RivalBid bid, PlayerData p, bool signed)
        {
            if (!shortlist.Contains(p.id) && !world.offers.Any(o => o.player == p.id && OfferForManagedClub(o))) return;
            Mail("Cellule recrutement", signed ? "Cible recrutée par un rival" : "Offre rivale repoussée",
                signed ? p.name + " s’engage avec " + ClubLabel(db, bid.club) + ". Vos discussions ouvertes sont closes." : "Le vendeur a écarté l’offre de " + ClubLabel(db, bid.club) + " pour " + p.name + ". La concurrence peut revenir.", p.id, "transfer");
        }
    }
}

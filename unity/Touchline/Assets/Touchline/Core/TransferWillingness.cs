using System;
using System.Collections.Generic;
using System.Linq;

namespace Touchline.Core
{
    // What the player thinks of a move to the managed club, before any fee is discussed.
    public sealed class TransferInterest
    {
        public int level = 3;                 // 3 très intéressé, 2 intéressé, 1 hésitant, 0 refuse
        public bool refuses; public float deficit;
        public long requiredWeeklyWage;       // minimum weekly wage for a permanent move
        public string label = "Très intéressé";
        public List<string> reasons = new List<string>();
    }
    public partial class Career
    {
        const float BaseWageRise = 1.12f;           // same rise as the agent's ordinary demand (ManagementDay)
        const float TransferFeePremium = 1.05f;     // same asking fee as ManagementDay / PlayerAgent
        const float RevenueWeight = 3;              // pull points (rating scale) per ×10 of club revenue
        const float FreeAgentPull = 6;              // a free agent needs a club
        const float AmbitionTolerance = .4f;        // points of step down accepted per missing ambition point (20 − ambition)
        const int AdaptabilityComfort = 10;         // below this 1–20 value a move abroad worries the player
        const float ForeignReluctance = .5f;        // deficit points per missing adaptability point abroad
        const float StarterRoleAppeal = 2, BenchRoleReluctance = 2; // deficit points for the promised status
        const float InterestedDeficit = 3, RefusalDeficit = 8;      // thresholds (rating points of deficit)
        const float WagePremiumPerPoint = .05f, MaxWagePremium = .6f; // +5 % of wage per deficit point, +60 % at most

        long ClubRevenue(Database db, string id) => id == club ? Math.Max(1, life.revenue) : Math.Max(1, db.clubs.FirstOrDefault(c => c.id == id)?.annualRevenue ?? 1);

        public TransferInterest PlayerTransferInterest(Database db, string id, string role = null)
        {
            var result = new TransferInterest(); var p = db?.Find(id);
            if (p == null || world == null || life == null || p.team == club) return result;
            bool free = p.team == "free";
            float level = p.rating + p.development, destination = Strength(db, club), current = free ? destination : Strength(db, p.team);
            float pull = destination - current + (free ? FreeAgentPull : RevenueWeight * (float)Math.Log10((double)ClubRevenue(db, club) / ClubRevenue(db, p.team)));
            int ambition = PersonalityTrait(id, Ambition), adaptability = PersonalityTrait(id, Adaptability);
            float deficit = -pull - (20 - ambition) * AmbitionTolerance;
            if (!free && destination < current - 1) result.reasons.Add("Votre équipe est jugée moins forte que son club actuel.");
            if (free) result.reasons.Add("Libre de contrat : il cherche un projet.");
            string from = PlayerTerritory(db, p), home = HomeTerritory(db);
            if (from != null && home != null && from != home && adaptability < AdaptabilityComfort)
            {
                deficit += (AdaptabilityComfort - adaptability) * ForeignReluctance;
                result.reasons.Add("Un départ à l’étranger l’inquiète.");
            }
            if (role != null)
            {
                string normalized = PlayingTimeRoles.Normalize(role);
                if ((normalized == "key" || normalized == "starter") && level >= destination - 2) { deficit -= StarterRoleAppeal; result.reasons.Add("Le statut de titulaire proposé le motive."); }
                else if (normalized != "key" && normalized != "starter" && level >= destination) { deficit += BenchRoleReluctance; result.reasons.Add("Il attend plus de temps de jeu que le statut proposé."); }
            }
            result.deficit = deficit;
            result.requiredWeeklyWage = (long)(p.wage * (BaseWageRise + Mathx.Clamp(deficit * WagePremiumPerPoint, 0, MaxWagePremium)));
            result.level = deficit <= 0 ? 3 : deficit <= InterestedDeficit ? 2 : deficit <= RefusalDeficit ? 1 : 0;
            result.refuses = result.level == 0;
            result.label = result.level == 3 ? "Très intéressé" : result.level == 2 ? "Intéressé" : result.level == 1 ? "Hésitant · salaire plus élevé exigé" : "Pas intéressé";
            if (result.level == 3 && result.reasons.Count == 0) result.reasons.Add("Votre projet représente une progression pour lui.");
            return result;
        }

        void TransferWillingnessDay(Database db)
        {
            if (world?.offers == null) return;
            foreach (var o in world.offers.Where(o => o.status == "pending" && o.due <= life.day && !o.renewal && !o.loan && OfferForManagedClub(o)).ToArray())
            {
                var p = db.Find(o.player); if (p == null || p.team != o.seller) continue;
                var interest = PlayerTransferInterest(db, p.id, o.role);
                if (interest.refuses)
                {
                    o.status = "declined";
                    Mail("Agent de " + p.name, "Projet refusé", "Mon client ne souhaite pas rejoindre votre club pour le moment. " + string.Join(" ", interest.reasons) + " Il pourra réévaluer votre projet plus tard.", p.id, "transfer", TransferMessageReference(o));
                }
                else if (o.wage < interest.requiredWeeklyWage)
                {
                    // Counter-offer handled here, before the ordinary agent answer of ManagementDay.
                    o.status = "counter"; o.wage = interest.requiredWeeklyWage;
                    o.fee = o.precontract || p.team == "free" ? 0 : Math.Max(o.fee, (long)(p.value * TransferFeePremium));
                    Mail("Agent de " + p.name, "Négociation à reprendre", "Mon client est " + interest.label.ToLowerInvariant() + ". " + string.Join(" ", interest.reasons) + " Il demande " + MonthlySalary(o.wage).ToString("N0") + " € par mois" + (o.fee > 0 ? " et une indemnité d’au moins " + o.fee.ToString("N0") + " € pour son club." : "."), p.id, "transfer", TransferMessageReference(o));
                }
            }
        }
    }
}

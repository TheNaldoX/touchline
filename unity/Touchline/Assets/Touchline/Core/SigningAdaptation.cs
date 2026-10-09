using System;
using System.Collections.Generic;
using System.Linq;

namespace Touchline.Core
{
    // One arrival at the managed club: what recruitment believed, and the settling period.
    [Serializable] public class SigningRecord
    {
        public string player, from, fromCountry, scout;
        public int day, settleDays, knowledge;
        public float estimate, actual, penalty;   // estimate/actual: level (1–99) at signing; penalty: share of match attributes
        public long fee; public bool loan, settled;
    }
    public sealed class SigningReview
    {
        public string player, verdict; public SigningRecord record;
        public float progress, current; public int seasonsAgo;
    }
    public partial class Career
    {
        public List<SigningRecord> signings = new List<SigningRecord>();
        const int SettleDomesticDays = 45, SettleAbroadDays = 120, SettleLanguageDays = 30; // days to settle
        const int SettleDaysPerAdaptability = 4, SettleMinDays = 21, SettleMaxDays = 200;   // −4 days per adaptability point above 10
        const float SettlingPenaltyDomestic = .02f, SettlingPenaltyAbroad = .05f;           // share of match attributes lost on arrival
        const int SigningDetectionDays = 3;          // a contract signed in the last days is recorded once
        const int MaxSigningRecords = 80;
        const float ReviewTolerance = 3;             // rating points: within ± this, the report was right

        void SigningsDay(Database db)
        {
            signings ??= new List<SigningRecord>(); if (world?.contracts == null) return;
            string home = HomeTerritory(db);
            foreach (var c in world.contracts.Where(c => c.club == club && c.joined > 0 && c.joined >= life.day - SigningDetectionDays && c.joined <= life.day))
            {
                if (signings.Any(s => s.player == c.player && s.day == c.joined)) continue;
                var p = db.Find(c.player); if (p == null || p.team != club || !life.players.Any(x => x.id == p.id)) continue;
                if (world.youth.Any(y => y.player == p.id)) continue;                 // academy promotion, not a signing
                var offer = world.offers.LastOrDefault(o => o.player == p.id && OfferForManagedClub(o) && (o.status == "signed" || o.status == "scheduled") && !o.renewal);
                if (offer == null) continue;   // promotion, loan bookkeeping or renewal: not a new arrival
                string from = offer.seller; var fromClub = db.clubs.FirstOrDefault(x => x.id == from);
                string fromCountry = fromClub == null ? null : ScoutingGeography.Country(db, fromClub);
                bool abroad = fromCountry != null && home != null && fromCountry != home;
                bool foreignNational = LanguageBarrier(p.nationality, home);
                int adaptability = PersonalityTrait(p.id, Adaptability);
                int days = (abroad ? SettleAbroadDays : SettleDomesticDays) + (foreignNational ? SettleLanguageDays : 0) - (adaptability - 10) * SettleDaysPerAdaptability;
                var report = world.reports.Where(r => r.player == p.id && (string.IsNullOrEmpty(r.club) || r.club == club) && r.confidence >= 90).OrderByDescending(r => r.started).FirstOrDefault();
                signings.Add(new SigningRecord { player = p.id, from = from, fromCountry = fromCountry, day = c.joined, settleDays = (int)Mathx.Clamp(days, SettleMinDays, SettleMaxDays),
                    penalty = abroad || foreignNational ? SettlingPenaltyAbroad : SettlingPenaltyDomestic, knowledge = report == null ? 0 : ReportKnowledge(report),
                    estimate = report?.estimate ?? 0, scout = report?.scout, actual = p.rating + p.development, fee = offer.fee, loan = offer.loan });
                if (signings.Count > MaxSigningRecords) signings.RemoveRange(0, signings.Count - MaxSigningRecords);
            }
            foreach (var s in signings.Where(s => !s.settled && life.day >= s.day + s.settleDays))
            {
                s.settled = true; var p = db.Find(s.player);
                if (p != null && p.team == club) Mail("Adjoint", "Intégration terminée", p.name + " a trouvé ses repères au club. Son rendement n’est plus freiné par l’adaptation.", p.id);
            }
        }
        // Main football languages, approximate: territories (French names) and nationalities (English names of the database).
        static readonly Dictionary<string, string> TerritoryLanguages = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["France"] = "fr", ["Belgique"] = "fr nl", ["Suisse"] = "fr de it", ["Espagne"] = "es", ["Argentine"] = "es", ["Chili"] = "es", ["Colombie"] = "es", ["Mexique"] = "es",
            ["Pérou"] = "es", ["Uruguay"] = "es", ["Équateur"] = "es", ["Portugal"] = "pt", ["Brésil"] = "pt", ["Angleterre"] = "en", ["Écosse"] = "en", ["États-Unis / Canada"] = "en",
            ["Australie / Nouvelle-Zélande"] = "en", ["Afrique du Sud"] = "en", ["Allemagne"] = "de", ["Autriche"] = "de", ["Italie"] = "it", ["Pays-Bas"] = "nl", ["Turquie"] = "tr", ["Japon"] = "ja",
        };
        static readonly Dictionary<string, string> NationalityLanguages = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["France"] = "fr", ["Senegal"] = "fr", ["Ivory Coast"] = "fr", ["Cote D'Ivoire"] = "fr", ["Cameroon"] = "fr", ["Mali"] = "fr", ["Guinea"] = "fr", ["Burkina Faso"] = "fr", ["Gabon"] = "fr", ["DR Congo"] = "fr", ["Congo DR"] = "fr", ["Haiti"] = "fr",
            ["Spain"] = "es", ["Argentina"] = "es", ["Chile"] = "es", ["Colombia"] = "es", ["Mexico"] = "es", ["Peru"] = "es", ["Uruguay"] = "es", ["Ecuador"] = "es", ["Venezuela"] = "es", ["Paraguay"] = "es", ["Bolivia"] = "es", ["Costa Rica"] = "es", ["Honduras"] = "es",
            ["Portugal"] = "pt", ["Brazil"] = "pt", ["Angola"] = "pt", ["Cape Verde"] = "pt", ["Cape Verde Islands"] = "pt", ["Guinea-Bissau"] = "pt", ["Mozambique"] = "pt",
            ["England"] = "en", ["Scotland"] = "en", ["Wales"] = "en", ["Northern Ireland"] = "en", ["Republic of Ireland"] = "en", ["Ireland"] = "en", ["United States"] = "en", ["USA"] = "en", ["Canada"] = "en", ["Australia"] = "en", ["New Zealand"] = "en", ["South Africa"] = "en", ["Jamaica"] = "en", ["Ghana"] = "en", ["Nigeria"] = "en",
            ["Germany"] = "de", ["Austria"] = "de", ["Switzerland"] = "de", ["Italy"] = "it", ["Netherlands"] = "nl", ["Belgium"] = "nl", ["Turkey"] = "tr", ["Japan"] = "ja",
        };
        /// <summary>True when both languages are known and the player's is not spoken in the club's territory.</summary>
        static bool LanguageBarrier(string nationality, string territory)
            => !string.IsNullOrEmpty(nationality) && territory != null && NationalityLanguages.TryGetValue(nationality, out var spoken)
               && TerritoryLanguages.TryGetValue(territory, out var local) && !local.Split(' ').Contains(spoken);
        SigningRecord LatestSigning(string id)
        {
            if (signings == null) return null; SigningRecord found = null;
            foreach (var s in signings) if (s.player == id && (found == null || s.day > found.day)) found = s;
            return found;
        }
        /// <summary>0–1 progress of the settling period of a recent signing (1 when settled or unknown).</summary>
        public float SettlingProgress(string id)
        {
            var s = LatestSigning(id); if (s == null || life == null || s.settleDays <= 0) return 1;
            return Mathx.Clamp((life.day - s.day) / (float)s.settleDays, 0, 1);
        }
        /// <summary>Match attribute modifier (≤ 0): a new signing is not yet at his level.</summary>
        public float SettlingModifier(string id)
        {
            var s = LatestSigning(id); if (s == null || s.settled || life == null) return 0;
            return -s.penalty * (1 - SettlingProgress(id));
        }

        /// <summary>Recent signings: what the report said, what the player shows now, and settling.</summary>
        public List<SigningReview> RecruitmentReview(Database db, int seasons = 2)
        {
            var result = new List<SigningReview>(); if (signings == null || db == null) return result;
            foreach (var s in signings.Where(s => life.day - s.day <= 365 * seasons).OrderByDescending(s => s.day))
            {
                var p = db.Find(s.player); if (p == null) continue;
                var item = new SigningReview { player = s.player, record = s, progress = SettlingProgress(s.player), current = p.rating + p.development, seasonsAgo = (life.day - s.day) / 365 };
                if (s.knowledge < 40 || s.estimate <= 0) item.verdict = "Recruté sans rapport complet";
                else item.verdict = s.actual > s.estimate + ReviewTolerance ? "Meilleur que le rapport" : s.actual < s.estimate - ReviewTolerance ? "En deçà du rapport" : "Conforme au rapport";
                result.Add(item);
            }
            return result;
        }
    }
}

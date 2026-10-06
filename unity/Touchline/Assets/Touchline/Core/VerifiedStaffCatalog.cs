using System;
using System.Linq;

namespace Touchline.Core
{
    // Identity and employment are sourced; attributes and remuneration remain game estimates.
    // Import only while a career date is covered by this dated snapshot. Existing simulations win.
    public partial class Career
    {
        static readonly DateTime VerifiedStaffObservedAt = new DateTime(2026, 10, 5);
        void EnsureVerifiedStaffCatalog(Database db)
        {
            if (Date > VerifiedStaffObservedAt) return;
            string bayern = "https://fcbayern.com/en/news/2025/12/fc-bayern-extend-contracts-of-coaching-team";
            string city = "https://www.mancity.com/news/mens/coaching-team-enzo-maresca-confirmed-63919119";
            string barcelona = "https://www.fcbarcelona.com/en/football/first-team/players";
            var bayernPublished = new DateTime(2025, 12, 12);
            var bayernEnd = new DateTime(2029, 6, 30);
            var cityPublished = new DateTime(2026, 7, 8);
            AddVerifiedStaff(db, "rene-maric", "René Marić", "132", "assistant", bayernPublished, bayernEnd, bayern, "Entraîneur adjoint", true);
            AddVerifiedStaff(db, "aaron-danks", "Aaron Danks", "132", "assistant", bayernPublished, bayernEnd, bayern, "Entraîneur adjoint", true);
            AddVerifiedStaff(db, "floribert-ngalula", "Floribert Ngalula", "132", "assistant", bayernPublished, bayernEnd, bayern, "Entraîneur adjoint", true);
            AddVerifiedStaff(db, "roberto-vitiello", "Roberto Vitiello", "382", "assistant", cityPublished, null, city, "Entraîneur adjoint", true);
            AddVerifiedStaff(db, "willy-caballero", "Willy Caballero", "382", "assistant", cityPublished, null, city, "Entraîneur de l’équipe première, regroupé dans le poste d’adjoint du jeu", true);
            AddVerifiedStaff(db, "danny-walker", "Danny Walker", "382", "assistant", cityPublished, null, city, "Entraîneur de l’équipe première, regroupé dans le poste d’adjoint du jeu", true);
            AddVerifiedStaff(db, "denis-silva", "Denis Silva", "382", "assistant", cityPublished, null, city, "Entraîneur de l’équipe première, regroupé dans le poste d’adjoint du jeu", true);
            AddVerifiedStaff(db, "marcos-alvarez", "Marcos Alvarez", "382", "fitness", cityPublished, null, city, "Préparateur physique", true);
            AddVerifiedStaff(db, "marcus-sorg", "Marcus Sorg", "83", "assistant", VerifiedStaffObservedAt, null, barcelona, "Entraîneur adjoint", false);
            AddVerifiedStaff(db, "toni-tapalovic", "Toni Tapalovic", "83", "assistant", VerifiedStaffObservedAt, null, barcelona, "Entraîneur adjoint", false);
            AddVerifiedStaff(db, "heiko-westermann", "Heiko Westermann", "83", "assistant", VerifiedStaffObservedAt, null, barcelona, "Entraîneur adjoint", false);
            AddVerifiedStaff(db, "pepe-conde", "Pepe Conde", "83", "fitness", VerifiedStaffObservedAt, null, barcelona, "Préparateur physique sur le terrain", false);
            AddVerifiedStaff(db, "benjamin-kugel", "Benjamin Kugel", "83", "fitness", VerifiedStaffObservedAt, null, barcelona, "Préparateur physique en salle et musculation", false);
        }
        void AddVerifiedStaff(Database db, string key, string name, string teamId, string role,
            DateTime firstEvidence, DateTime? knownEnd, string source, string actualRole, bool datedAnnouncement)
        {
            if (Date < firstEvidence) return;
            string id = "real-" + key;
            // Includes staff now free, transferred, or already under negotiation: never resurrect a contract.
            if (staffMarket.Any(s => s.id == id || string.Equals(s.name, name, StringComparison.OrdinalIgnoreCase))) return;
            var team = db.clubs.FirstOrDefault(c => c.id == teamId && c.playable);
            if (team == null) return;
            DateTime simulatedEnd = knownEnd ?? VerifiedStaffObservedAt.AddDays(730);
            string evidence = datedAnnouncement ? "Annonce officielle du " + firstEvidence.ToString("dd/MM/yyyy") : "Présence constatée au 05/10/2026, date de nomination non vérifiée";
            string contract = knownEnd.HasValue ? "Échéance publiée : " + knownEnd.Value.ToString("dd/MM/yyyy") + "." : "Échéance contractuelle non publique : durée simulée, non vérifiée.";
            staffMarket.Add(new StaffMember {
                id = id, name = name, club = team.id, role = role, fictional = false,
                tactics = 16, coaching = 16, judging = 14, people = 16,
                wage = Math.Max(1200, team.annualRevenue / 70000),
                until = (int)(simulatedEnd - Epoch).TotalDays, source = source,
                biography = actualRole + " · " + team.name + ". " + evidence + ". Source consultée le 05/10/2026. " + contract + " Notes et salaire estimés pour la simulation ; aucune évaluation individuelle ni rémunération vérifiée."
            });
        }
    }
}

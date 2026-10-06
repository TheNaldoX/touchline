using System;
using System.Collections.Generic;
using System.Linq;

namespace Touchline.Core
{
    [Serializable] public class EmploymentCosts
    {
        public long playersGross,playerContributions,staffGross,staffContributions,otherPersonnel;
        public long Total=>playersGross+playerContributions+staffGross+staffContributions+otherPersonnel;
    }
    public partial class Career
    {
        // Ratios observed in DNCG 2024/25, not legal contribution rates for an individual contract.
        // Other countries use explicitly unverified simulation assumptions until sourced separately.
        public static float EmployerRatio(string league)=>league=="fra.2"?63499f/155867:league?.StartsWith("fra.")==true?398238f/1334537:.20f;
        public static float ProfessionalPersonnelShare(string league)=>league=="fra.2"?.59f:league?.StartsWith("fra.")==true?.74f:.80f;
        string FinanceLeague=>world?.divisions.FirstOrDefault(d=>d.clubs.Contains(club))?.id??life?.financeLeague??"unknown";
        public static long GrossWageCeiling(string league,long revenue,long annualStaffGross=0)
        {
            double contribution=1+EmployerRatio(league),share=ProfessionalPersonnelShare(league),loadedLimit=revenue*.55;
            return Math.Max(0,(long)Math.Min(loadedLimit*share/contribution/52,(loadedLimit-annualStaffGross*contribution)/contribution/52));
        }
        public static EmploymentCosts EmploymentProjection(string league,long annualPlayersGross,long annualStaffGross=0)
        {
            double rate=EmployerRatio(league),share=ProfessionalPersonnelShare(league);
            var result=new EmploymentCosts{playersGross=annualPlayersGross,staffGross=annualStaffGross,playerContributions=(long)(annualPlayersGross*rate),staffContributions=(long)(annualStaffGross*rate)};
            result.otherPersonnel=Math.Max(0,(long)((result.playersGross+result.playerContributions)*(1/share-1))-result.staffGross-result.staffContributions);
            return result;
        }
        public EmploymentCosts AnnualEmploymentCosts(Database db)
            =>EmploymentProjection(db.clubs.First(t=>t.id==club).league,Payroll(db)*52,(life.staff?.members.Sum(s=>s.wage)??0)*52);
        public static long AnnualOperatingCosts(ClubData team,int facilityLevels=0)
        {
            // PSG consolidated 2024/25: 229.173m other charges / 837.011m non-transfer revenue.
            // The other-club 40% structure forecast remains an estimate, not a transplanted tax rule.
            double ratio=team.id=="160"?229173d/837011:.40;
            return (long)(team.annualRevenue*ratio)+facilityLevels*team.annualRevenue*52/20000;
        }
        public string OperatingCostSource(Database db)
        {
            var team=db.clubs.First(t=>t.id==club);
            string personnel=team.league?.StartsWith("fra.")==true?"Personnel : ratios sectoriels DNCG 2024/25, consultés le 14/09/2026. Ce sont des projections, pas des taux légaux individuels.":"Personnel hors France : hypothèses de jeu non vérifiées (cotisations 20 %, joueurs 80 % du personnel chargé).";
            return personnel+(team.id=="160"?" Structure PSG : ratio des autres charges consolidées DNCG 2024/25 ; périmètre incluant plusieurs filiales et sections.":" Exploitation : prévision de jeu à 40 % des recettes, faute de ventilation propre au club vérifiée.")+" Les indemnités payées et leurs échéanciers sont comptés séparément. L’amortissement comptable ne constitue pas un second paiement.";
        }
    }
}

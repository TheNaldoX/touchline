using System;
using System.Collections.Generic;
using System.Linq;

namespace Touchline.Core
{
    [Serializable] public class BoardObjective
    {
        public string club,division;
        public int season,targetRank,createdDay,lastReviewDay=-1,lastRank,played;
        public long revenueAtAgreement,weeklyWageCeiling;
        public float lastSportingChange;
    }
    public partial class Career
    {
        // Seasonal expectations are agreed once, rather than changing whenever
        // the manager sells a player or the squad's form/development changes.
        public BoardObjective EnsureBoardObjective(Database db)
        {
            if(world==null||life==null||world.managerStatus!="employed")return null;
            world.boardObjectives??=new List<BoardObjective>();
            var division=world.divisions.FirstOrDefault(d=>d.clubs.Contains(club));
            if(division==null)return null;
            var current=world.boardObjectives.LastOrDefault(o=>o.club==club&&o.season==world.year&&o.division==division.id);
            if(current!=null)return current;
            var economic=division.clubs.OrderByDescending(id=>db.clubs.FirstOrDefault(c=>c.id==id)?.annualRevenue??0).ThenBy(id=>id,StringComparer.Ordinal).ToList();
            var sporting=division.clubs.OrderByDescending(id=>Strength(db,id)).ThenBy(id=>id,StringComparer.Ordinal).ToList();
            // 60% means / 40% initial squad rank. This is a simulated board
            // policy, not a sourced real-world objective or a promise of results.
            const double EconomicWeight=.6;
            int target=(int)Math.Round((economic.IndexOf(club)+1)*EconomicWeight+(sporting.IndexOf(club)+1)*(1-EconomicWeight),MidpointRounding.AwayFromZero);
            var table=Table(division.id);var standing=table.FirstOrDefault(t=>t.club==club);
            if(standing!=null&&standing.played>=5){
                int remaining=world.fixtures.Count(f=>f.league==division.id&&!f.played&&!f.knockout&&(f.home==club||f.away==club));
                // An arrival late in the season inherits points already lost.
                // Scale the recovery expected to the league matches still available.
                double opportunity=remaining/(double)Math.Max(1,standing.played+remaining);
                target=(int)Math.Round(target*opportunity+(table.IndexOf(standing)+1)*(1-opportunity),MidpointRounding.AwayFromZero);
            }
            current=new BoardObjective{club=club,division=division.id,season=world.year,createdDay=life.day,targetRank=Math.Max(1,target),revenueAtAgreement=life.revenue,weeklyWageCeiling=WageBudget};
            world.boardObjectives.Add(current);
            // Keep twenty seasons of agreements, including former clubs.
            world.boardObjectives.RemoveAll(o=>o.season<world.year-20);
            Mail("Présidence","Objectifs de la saison",world.year+"–"+(world.year+1)+" : viser la "+current.targetRank+"e place ou mieux. Cette référence simulée tient compte des recettes, de l'effectif et des rencontres restantes à la prise de fonction. Elle reste fixe pour la saison. Plafond salarial indicatif à l'accord : "+MonthlySalary(current.weeklyWageCeiling).ToString("N0")+" € / mois ; le plafond disponible reste consultable dans Finances.",null,"jobs");
            return current;
        }

        public void ReviewBoardObjective(Database db)
        {
            var objective=EnsureBoardObjective(db);
            if(objective==null||objective.lastReviewDay>=0&&life.day-objective.lastReviewDay<30)return;
            var table=Table(objective.division);var own=table.FirstOrDefault(t=>t.club==club);
            objective.lastReviewDay=life.day;objective.played=own?.played??0;objective.lastRank=own==null?0:table.IndexOf(own)+1;
            objective.lastSportingChange=0;
            // Early tables are noisy. Limit one monthly sporting review to five
            // trust points; weekly results and financial reviews remain separate.
            const int MinimumPlayed=5;
            const float TrustPerPlace=.7f,MaximumMonthlyChange=5;
            if(own!=null&&own.played>=MinimumPlayed){
                float before=life.boardTrust;
                float change=Mathx.Clamp((objective.targetRank-objective.lastRank)*TrustPerPlace,-MaximumMonthlyChange,MaximumMonthlyChange);
                life.boardTrust=Mathx.Clamp(before+change,0,100);
                objective.lastSportingChange=life.boardTrust-before;
            }
            string standing=objective.played<MinimumPlayed?"Pas de jugement sportif : moins de cinq matchs de championnat disputés.":"Classement : "+objective.lastRank+"e, objectif : "+objective.targetRank+"e ou mieux. Effet sportif sur la confiance : "+objective.lastSportingChange.ToString("+0.0;-0.0;0")+" points.";
            Mail("Présidence","Bilan mensuel des objectifs",standing+" Les résultats des matchs et la trésorerie sont évalués séparément. "+(life.boardTrust<35?"Votre poste est fragilisé ; une confiance inférieure à 25 au bilan du conseil peut conduire à votre départ.":"Les objectifs restent ceux convenus pour cette saison."),null,"jobs");
        }
    }
}

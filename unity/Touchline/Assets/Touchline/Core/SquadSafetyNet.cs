using System;
using System.Collections.Generic;
using System.Linq;

namespace Touchline.Core
{
    // A managed club whose contracts all lapse could not field eleven players:
    // the fixture day then blocked both "Continuer" (a match is due) and the
    // match itself ("Effectif insuffisant"), with no way out. Before such a
    // match the sporting director now completes the group from the club's own
    // academy, as a real club would register youth players, and says so.
    public partial class Career
    {
        public const int MinimumMatchSquad=16,SquadWarningThreshold=18;

        // Returns the ids promoted for this match (empty when nothing was needed).
        public List<string> EnsureMatchSquad(Database db)
        {
            var promoted=new List<string>();
            if(world==null||world.managerStatus!="employed")return promoted;
            var squad=db.Squad(club);int keepers=squad.Count(p=>p.Goalkeeper);
            if(squad.Count>=MinimumMatchSquad&&keepers>=1)return promoted;
            var academy=db.players.Where(p=>p.team=="academy-"+club&&p.team!="retired").OrderByDescending(p=>p.rating+p.development).ToList();
            void Promote(PlayerData p)
            {
                long wage=Math.Max(250,p.wage);
                BeforeFinancialTermsChange(db,club);
                p.team=club;p.wage=wage;
                var path=world.youth.FirstOrDefault(y=>y.player==p.id);if(path!=null){path.group="senior";path.mentor=null;}
                var contract=Contract(db,p.id);contract.club=club;contract.wage=wage;contract.joined=life.day;
                if(contract.until<=life.day)contract.until=Math.Max(world.seasonEnd+1,life.day+180);
                SavePlayer(p);
                if(!life.players.Any(x=>x.id==p.id))life.players.Add(new PlayerLife{id=p.id,fitness=p.fitness,morale=p.morale});
                AfterFinancialTermsChange(db,club);
                academy.Remove(p);promoted.Add(p.id);
            }
            while(keepers<Math.Min(2,keepers+academy.Count(p=>p.Goalkeeper))&&squad.Count+promoted.Count<MinimumMatchSquad+2){var gk=academy.FirstOrDefault(p=>p.Goalkeeper);if(gk==null)break;Promote(gk);keepers++;}
            while(squad.Count+promoted.Count<MinimumMatchSquad){var next=academy.FirstOrDefault(p=>!p.Goalkeeper)??academy.FirstOrDefault();if(next==null)break;Promote(next);}
            if(promoted.Count>0)
                Mail("Direction sportive","Effectif complété avec le centre de formation",
                    "Il ne restait que "+squad.Count+" joueur(s) sous contrat"+(squad.Count(p=>p.Goalkeeper)==0?" et aucun gardien":"")+". Pour disputer la rencontre, "+promoted.Count+" jeune(s) du centre rejoignent le groupe professionnel : "
                    +string.Join(", ",promoted.Select(id=>db.Find(id).name))+". Prolongez ou recrutez rapidement : ces joueurs ne remplacent pas un effectif complet.",null,"transfer");
            return promoted;
        }

        // Daily: warn before the squad becomes too thin, at most once a fortnight.
        void WarnThinSquad(Database db)
        {
            if(world==null||world.managerStatus!="employed"||life.day%14!=0)return;
            int count=db.Squad(club).Count;
            int expiring=world.contracts.Count(c=>c.club==club&&!c.IsLoan&&c.until>life.day&&c.until-life.day<=60);
            if(count-expiring>=SquadWarningThreshold)return;
            Mail("Direction sportive","Effectif bientôt trop court",
                count+" joueurs sous contrat"+(expiring>0?", dont "+expiring+" en fin de contrat d’ici deux mois":"")+". Sous "+MinimumMatchSquad+" joueurs, le centre de formation devra compléter le groupe. Prolongez, recrutez ou intégrez des jeunes.",null,"transfer");
        }
    }
}

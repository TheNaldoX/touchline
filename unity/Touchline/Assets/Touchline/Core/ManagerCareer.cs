using System;
using System.Collections.Generic;
using System.Linq;

namespace Touchline.Core
{
    [Serializable] public class JobApproach { public string club,reason,style,status="open";public int offered,until;public long weeklySalary;public float fit; }
    [Serializable] public class ManagedClub { public string club;public ClubLife life;public int capacity,ticket;public long debt,spent;public List<CommercialDeal> sponsors=new List<CommercialDeal>(); }
    public partial class Career
    {
        public List<JobApproach> approaches=new List<JobApproach>();
        public List<ManagedClub> previousClubs=new List<ManagedClub>();
        public string Philosophy=>tactic.pressing>.7f&&tactic.line>.6f?"pressing haut":tactic.directness<.4f&&tactic.tempo<.65f?"possession":tactic.counterAttack&&tactic.line<.5f?"transitions":"équilibre";
        public float Overperformance(Database db)
        {
            var d=world.divisions.FirstOrDefault(x=>x.clubs.Contains(club));if(d==null)return 0;var table=Table(d.id);var own=table.FirstOrDefault(t=>t.club==club);if(own==null||own.played<10)return 0;
            int expected=d.clubs.OrderByDescending(id=>db.clubs.First(c=>c.id==id).annualRevenue).ThenBy(id=>id,StringComparer.Ordinal).ToList().IndexOf(club)+1;
            int actual=table.IndexOf(own)+1;return Mathx.Clamp((expected-actual)/(float)Math.Max(3,d.clubs.Count/3),-1,1);
        }
        void ReviewApproaches(Database db)
        {
            approaches??=new List<JobApproach>();foreach(var o in approaches.Where(o=>o.status=="open"&&o.until<life.day))o.status="expired";
            if(world.managerStatus!="employed"||life.day%30!=0)return;float excess=Overperformance(db);life.reputation=Mathx.Clamp(life.reputation+excess*.8f,0,100);
            if(excess<.3f||life.players.Count==0||life.players.Average(p=>p.trust)<45||approaches.Any(o=>o.status=="open")||approaches.Any(o=>life.day-o.offered<90))return;
            float ceiling=1+excess+(life.reputation-60)/60f;
            var candidates=db.clubs.Where(c=>c.id!=club&&c.playable&&c.annualRevenue>=life.revenue*.65&&c.annualRevenue<=life.revenue*Math.Max(1,ceiling)&&db.Squad(c.id).Count>=18).OrderBy(c=>c.annualRevenue).ToArray();
            if(candidates.Length==0)return;var target=candidates[(int)(Roll()*candidates.Length)];string style=target.annualRevenue>200000000?"possession":target.annualRevenue<40000000?"transitions":"équilibre";
            float fit=Philosophy==style?90:Philosophy=="équilibre"?72:55;if(fit<60&&excess<.7f)return;
            var offer=new JobApproach{club=target.id,offered=life.day,until=life.day+14,style=style,fit=fit,weeklySalary=Math.Max(1000,target.annualRevenue/10000),reason="Résultats supérieurs aux attentes budgétaires, réputation "+life.reputation.ToString("0")+", gestion du groupe "+life.players.Average(p=>p.trust).ToString("0")+" / 100. Le club recherche un jeu de "+style+"."};approaches.Add(offer);
            Mail("Agent • carrière","Approche de "+target.name,offer.reason+" Vous disposez de quatorze jours pour répondre dans Carrière.",null,"jobs",ApproachMessageReference(offer));
        }
        public void AnswerApproach(Database db,string target,bool accept)
        {
            OffPitch(requireEmployment:false);var offer=approaches.LastOrDefault(o=>o.club==target&&o.status=="open"&&o.until>=life.day);if(offer==null)throw new InvalidOperationException("Cette approche n’est plus ouverte.");offer.status=accept?"accepted":"declined";
            if(accept)MoveManager(db,target);
        }
        void MoveManager(Database db,string target)
        {
            // Old saves did not name the proposing club. Bind unsigned discussions
            // before changing manager context; signed commitments keep their destination.
            foreach(var offer in world.offers.Where(o=>string.IsNullOrEmpty(o.destination)&&(o.status=="pending"||o.status=="accepted"||o.status=="counter")))offer.destination=club;
            CloseClubScouting();
            var leavingAccount=world.aiAccounts?.FirstOrDefault(a=>a.club==club);if(leavingAccount!=null){
                leavingAccount.cash=life.cash;leavingAccount.operatingDebt=world.debt;leavingAccount.projectedFromDay=life.day;
                foreach(var facility in life.facilities){if(facility.kind=="training")leavingAccount.training=Math.Max(leavingAccount.training,facility.level);if(facility.kind=="academy")leavingAccount.academy=Math.Max(leavingAccount.academy,facility.level);}
                CaptureAiOperatingSnapshot(db.clubs.First(t=>t.id==club),leavingAccount,Payroll(db));
            }
            var targetAccount=world.aiAccounts?.FirstOrDefault(a=>a.club==target);if(targetAccount!=null)ProjectAiOperatingPeriod(db.clubs.First(t=>t.id==target),targetAccount,db.Squad(target),life.day);
            int day=life.day;float reputation=life.reputation;previousClubs??=new List<ManagedClub>();previousClubs.RemoveAll(x=>x.club==club);
            previousClubs.Add(new ManagedClub{club=club,life=life,capacity=world.capacity,ticket=world.ticket,debt=world.debt,spent=world.transferSpent,sponsors=world.sponsors});
            club=target;var previous=previousClubs.FirstOrDefault(x=>x.club==club);life=previous?.life;if(life!=null)life.day=day;match=null;lineup=Select(db,club,tactic);EnsureLife(db);var arrivingAccount=world.aiAccounts?.FirstOrDefault(a=>a.club==club);if(arrivingAccount!=null){life.cash=arrivingAccount.cash;foreach(var f in life.facilities){if(f.kind=="training")f.level=Math.Max(f.level,arrivingAccount.training);if(f.kind=="academy")f.level=Math.Max(f.level,arrivingAccount.academy);}}life.revenue=db.clubs.First(c=>c.id==club).annualRevenue;life.day=day;life.reputation=reputation;life.boardTrust=65;
            if(life.staff!=null)life.staff.marketBound=false;EnsureStaffMarket(db);
            world.managerStatus="employed";world.reviewDay=day+30;world.activeFixture=null;world.sponsors=previous?.sponsors??new List<CommercialDeal>();world.capacity=previous?.capacity??(int)Mathx.Clamp((float)Math.Sqrt(life.revenue)*3,4000,85000);world.ticket=previous?.ticket??(int)Mathx.Clamp((float)Math.Sqrt(life.revenue)/400,8,90);world.debt=arrivingAccount?.operatingDebt??previous?.debt??0;world.transferSpent=previous?.spent??0;world.lastTicketDay=day-7;
            foreach(var p in db.Squad(club))Contract(db,p.id);if(world.sponsors.Count==0)GenerateSponsors();if(!world.youth.Any(y=>db.Find(y.player)?.team=="academy-"+club)){world.lastIntake=-1;CreateIntake(db);}WorldDay(db);
            Mail("Présidence","Bienvenue dans votre nouveau club","Le calendrier et le monde poursuivent leur cours. Votre réputation vous accompagne ; les finances appartiennent au club.");
        }
    }
}

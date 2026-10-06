using System;
using System.Collections.Generic;
using System.Linq;

namespace Touchline.Core
{
    [Serializable] public class TrainingCamp { public int year,start,end;public string club,kind,status="planned";public long cost; }
    [Serializable] public class FriendlyInvitation { public string club,opponent,status="pending";public int day,answerDay;public bool home;public long guarantee; }
    public partial class Career
    {
        public static readonly DateTime Epoch=new DateTime(2026,6,15);
        public string calendarEpoch="2026-06-15";
        public List<TrainingCamp> camps=new List<TrainingCamp>();
        public List<FriendlyInvitation> friendlies=new List<FriendlyInvitation>();
        public bool Preseason=>Date.Month==6||Date.Month==7||Date.Month==8&&Date.Day<20;
        public int PreparationYear=>Preseason?Date.Year:world.year;
        public static int CampYear(TrainingCamp camp)=>Epoch.AddDays(camp.start).Year;
        public void ScheduleCamp(string kind,int start)
        {
            OffPitch();if(!Preseason||Epoch.AddDays(start+7).Month>8||Epoch.AddDays(start).Year!=Date.Year||start<life.day+2||start>life.day+35||!new[]{"local","altitude","tour"}.Contains(kind))throw new InvalidOperationException("Choisissez un stage de préparation dans les cinq prochaines semaines.");
            if(camps.Any(c=>(c.club==null||c.club==club)&&CampYear(c)==PreparationYear&&c.status!="cancelled"))throw new InvalidOperationException("Un stage est déjà organisé pour cette saison.");
            if(world.fixtures.Any(f=>!f.played&&f.league!="friendly"&&(f.home==club||f.away==club)&&f.day>=start&&f.day<=start+7))throw new InvalidOperationException("Une compétition officielle se déroule pendant ces dates.");
            long cost=Math.Max(3000,life.revenue/(kind=="local"?20000:kind=="altitude"?4000:1800));Charge(cost,"Stage de préparation • "+kind);camps.Add(new TrainingCamp{club=club,year=Epoch.AddDays(start).Year,start=start,end=start+7,kind=kind,cost=cost});Mail("Intendance","Stage confirmé","Sept jours de préparation sont réservés. La charge sera adaptée au travail choisi et aux rencontres amicales.",null,"calendar");
        }
        public List<ClubData> FriendlyRecommendations(Database db)
        {
            float strength=Strength(db,club);return db.clubs.Where(c=>c.id!=club&&c.playable&&!c.reserve&&db.Squad(c.id).Count>=18).OrderBy(c=>Math.Abs(Strength(db,c.id)-strength+6)).ThenBy(c=>c.id,StringComparer.Ordinal).Take(12).ToList();
        }
        public void InviteFriendly(Database db,string opponent,int day,bool home)
        {
            OffPitch();if(!Preseason||Epoch.AddDays(day).Year!=Date.Year||Epoch.AddDays(day).Month>8||day<life.day+3||day>life.day+45||opponent==club||db.Squad(opponent).Count<11)throw new InvalidOperationException("Proposez une rencontre de préparation entre trois et quarante-cinq jours à l’avance.");
            if(friendlies.Any(f=>(f.club==null||f.club==club)&&f.status=="pending"&&(f.opponent==opponent||Math.Abs(f.day-day)<3)))throw new InvalidOperationException("Une proposition est déjà en cours.");
            if(world.fixtures.Any(f=>!f.played&&(f.home==club||f.away==club)&&Math.Abs(f.day-day)<3))throw new InvalidOperationException("Conservez au moins trois jours entre deux rencontres.");
            long fee=home?Math.Max(1000,db.clubs.First(c=>c.id==opponent).annualRevenue/10000):0;friendlies.Add(new FriendlyInvitation{club=club,opponent=opponent,day=day,home=home,guarantee=fee,answerDay=life.day+2});Mail("Secrétariat sportif","Invitation envoyée",db.clubs.First(c=>c.id==opponent).name+" étudie votre proposition. La garantie sera payée uniquement si la rencontre est confirmée.",null,"calendar");
        }
        public void CancelFriendly(string id)
        {
            OffPitch();var f=world.fixtures.FirstOrDefault(x=>x.id==id&&x.league=="friendly"&&!x.played);if(f==null||f.day-life.day<3)throw new InvalidOperationException("Annulation impossible à moins de trois jours du match.");world.fixtures.Remove(f);Mail("Intendance","Amical annulé","La garantie déjà versée reste acquise à l’adversaire.",null,"calendar");life.nextFixture=NextFixture()?.day??int.MaxValue;
        }
        void PreseasonDay(Database db)
        {
            camps??=new List<TrainingCamp>();friendlies??=new List<FriendlyInvitation>();
            foreach(var camp in camps)camp.year=CampYear(camp);
            foreach(var invitation in friendlies.Where(f=>(f.club==null||f.club==club)&&f.status=="pending"&&f.answerDay<=life.day)){
                bool conflict=world.fixtures.Any(f=>!f.played&&(f.home==invitation.opponent||f.away==invitation.opponent||f.home==club||f.away==club)&&Math.Abs(f.day-invitation.day)<3);bool affordable=life.cash>=invitation.guarantee;
                invitation.status=!conflict&&affordable?"accepted":"declined";
                if(invitation.status=="accepted"){Charge(invitation.guarantee,"Garantie de match amical");AddFixture("friendly",invitation.home?club:invitation.opponent,invitation.home?invitation.opponent:club,invitation.day);}
                Mail("Secrétariat sportif",invitation.status=="accepted"?"Amical confirmé":"Invitation refusée",db.clubs.First(c=>c.id==invitation.opponent).name+" : "+(conflict?"calendrier incompatible.":!affordable?"garantie non financée.":"rendez-vous ajouté à votre calendrier."),null,"calendar");
            }
            foreach(var camp in camps.Where(c=>(c.club==null||c.club==club)&&c.status=="planned"&&c.start<=life.day)){camp.status="active";Mail("Intendance","Départ en stage","Le groupe commence sa semaine de préparation.",null,"calendar");}
            foreach(var camp in camps.Where(c=>(c.club==null||c.club==club)&&c.status=="active")){
                if(life.day>=camp.end){camp.status="complete";Mail("Adjoint","Bilan du stage","Le travail collectif est terminé. Les gains physiques et de cohésion dépendent de la disponibilité des joueurs.",null,"calendar");continue;}
                foreach(var p in life.players.Where(p=>Injury(p.id)==null)){p.fitness=Math.Min(100,p.fitness+(camp.kind=="altitude"?.4f:.2f));p.trust=Math.Min(100,p.trust+.15f);p.morale=Math.Min(100,p.morale+.25f);}
            }
        }
        // Summer bookings belong to their actual dates, not the season that was still closing.
        void RestoreSummerPreparation(List<Fixture> fixtures)
        {
            foreach(var fixture in fixtures){
                if(!world.fixtures.Any(f=>f.id==fixture.id))world.fixtures.Add(fixture);
            }
            ResolveCalendar();
            camps??=new List<TrainingCamp>();
            foreach(var camp in camps){
                camp.year=CampYear(camp);
                if((camp.club==null||camp.club==club)&&camp.status=="planned"&&world.fixtures.Any(f=>!f.played&&f.league!="friendly"&&(f.home==club||f.away==club)&&f.day>=camp.start&&f.day<=camp.end)){
                    camp.status="cancelled";
                    Account(camp.cost,"Remboursement du stage • nouveau calendrier officiel");
                    Mail("Intendance","Stage remboursé : conflit de calendrier","Le nouveau calendrier officiel chevauche votre stage. La réservation est annulée et son coût remboursé ; choisissez une nouvelle période depuis Préparation.",null,"calendar");
                }
            }
        }
        void SyncFriendlyDate(Fixture fixture,int oldDay)
        {
            if(fixture.league!="friendly")return;
            foreach(var invite in friendlies??new List<FriendlyInvitation>()){
                string owner=invite.club??club;
                if(invite.status=="accepted"&&invite.day==oldDay&&fixture.home==(invite.home?owner:invite.opponent)&&fixture.away==(invite.home?invite.opponent:owner))invite.day=fixture.day;
            }
            if(fixture.home==club||fixture.away==club)Mail("Secrétariat sportif","Amical reprogrammé", "Le calendrier officiel impose un déplacement de l’amical du "+Epoch.AddDays(oldDay).ToString("dd/MM/yyyy")+" au "+Epoch.AddDays(fixture.day).ToString("dd/MM/yyyy")+". La garantie déjà versée est conservée, sans nouveau débit.",null,"calendar");
        }
        public void MigrateSummerEpoch()
        {
            const int shift=67;calendarEpoch="2026-06-15";if(life==null)return;
            void ShiftLife(ClubLife l){l.day+=shift;if(l.nextFixture!=int.MaxValue)l.nextFixture+=shift;l.managerBanUntil+=shift;l.schemeCooldown+=shift;if(l.leadershipUntil>0)l.leadershipUntil+=shift;
                foreach(var p in l.players){p.lastTalk+=shift;if(p.promiseUntil>=0)p.promiseUntil+=shift;if(p.boostUntil>=0)p.boostUntil+=shift;if(p.restUntil>=0)p.restUntil+=shift;p.banUntil+=shift;p.rehabUntil+=shift;}
                foreach(var m in l.messages)m.day+=shift;foreach(var m in l.medical){m.opened+=shift;if(m.closed>=0)m.closed+=shift;if(m.reliefUntil>=0)m.reliefUntil+=shift;}
                foreach(var p in l.projects){p.requested+=shift;p.due+=shift;p.started+=shift;}foreach(var e in l.ledger)e.day+=shift;foreach(var i in l.investigations){i.opened+=shift;i.due+=shift;}}
            ShiftLife(life);foreach(var old in previousClubs??new List<ManagedClub>())ShiftLife(old.life);
            if(world==null||world.divisions==null||world.divisions.Count==0)return;
            world.seasonEnd+=shift;world.reviewDay+=shift;if(world.jobDay>=0)world.jobDay+=shift;world.lastTicketDay+=shift;
            foreach(var f in world.fixtures.Concat(world.history))f.day+=shift;
            foreach(var c in world.contracts){c.until+=shift;if(c.parent!=null)c.loanUntil+=shift;if(c.retirement>=0)c.retirement+=shift;c.joined+=shift;}
            foreach(var r in world.reports){r.started+=shift;r.due+=shift;if(r.lastObserved>0)r.lastObserved+=shift;}foreach(var m in world.scoutMissions??new List<ScoutMission>()){m.started+=shift;m.until+=shift;m.nextSearch+=shift;}if(integrityReviewUntil>=0)integrityReviewUntil+=shift;foreach(var o in world.offers)o.due+=shift;foreach(var s in world.sponsors){s.until+=shift;s.counterDay+=shift;}foreach(var p in world.press)p.day+=shift;
            foreach(var y in world.youth)if(y.loanReviewedThrough>=0)y.loanReviewedThrough+=shift;
            foreach(var a in approaches??new List<JobApproach>()){a.offered+=shift;a.until+=shift;}
        }
    }
}

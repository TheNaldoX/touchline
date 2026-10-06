using System;
using System.Collections.Generic;
using System.Linq;

namespace Touchline.Core
{
    [Serializable] public class StaffMember
    {
        public string id,role,name,club,source,biography; public int tactics,coaching,judging,people,until; public long wage; public bool fictional=true;
    }
    [Serializable] public class DelegationPlan
    {
        public bool training,lineup,youth,scouting,press,initialized,marketBound;
        public int lastDay=-1; public long scoutingWeeklyLimit=2000; public List<StaffMember> members=new List<StaffMember>();
    }
    public class TacticalAdvice
    {
        public string key,title,evidence,tradeoff; public float value; public int confidence;
    }
    public partial class Career
    {
        public void EnsureStaff()
        {
            life.staff??=new DelegationPlan();life.staff.members??=new List<StaffMember>();
            if(life.staff.initialized)return;life.staff.initialized=true;
            int level=(int)Mathx.Clamp(6+(float)Math.Log10(Math.Max(1000000,life.revenue)/1000000.0)*4,6,17);
            foreach(var role in new[]{"assistant","fitness","scout","youth"})if(!life.staff.members.Any(s=>s.role==role))life.staff.members.Add(new StaffMember{role=role,name=role=="assistant"?"Alex Morel":role=="fitness"?"Camille Laurent":role=="scout"?"Sacha Perrin":"Elias Martin",tactics=Math.Min(20,level+(role=="assistant"?2:0)),coaching=Math.Min(20,level+(role=="fitness"||role=="youth"?2:0)),judging=Math.Min(20,level+(role=="scout"?2:0)),people=level,wage=Math.Max(150,life.revenue/100000)});
        }
        public StaffMember Staff(string role){EnsureStaff();return life.staff.members.FirstOrDefault(s=>s.role==role)??new StaffMember{role=role,name="Poste vacant",tactics=1,coaching=1,judging=1,people=1};}
        public void SetDelegation(string responsibility,bool enabled)
        {
            OffPitch();EnsureStaff();switch(responsibility){case "training":life.staff.training=enabled;break;case "lineup":life.staff.lineup=enabled;break;case "youth":life.staff.youth=enabled;break;case "scouting":life.staff.scouting=enabled;break;case "press":life.staff.press=enabled;break;default:throw new ArgumentException("Responsabilité inconnue.");}
            Mail("Secrétariat",enabled?"Responsabilité déléguée":"Responsabilité reprise",responsibility+" : changement applicable au prochain jour de travail. Les signatures et les décisions médicales restent soumises à votre décision.",null,"staff");
        }
        public void TrainStaff(string role)
        {
            OffPitch();var s=Staff(role);
            if(s.wage<=0||s.tactics>=20&&s.coaching>=20&&s.judging>=20&&s.people>=20)throw new InvalidOperationException("Poste vacant ou qualification maximale atteinte.");
            if(string.IsNullOrEmpty(s.id)||s.club!=club||s.until<=life.day)throw new InvalidOperationException("Le bénéficiaire doit avoir une identité vérifiable et un contrat valide dans votre staff.");
            if(StaffTrainingInProgress)throw new InvalidOperationException("Une formation du staff est déjà en cours. Attendez son suivi avant un nouveau cycle.");
            long cost=StaffTrainingCost;Charge(cost,"Formation du staff");life.staffTrainingUntil=life.day+45;life.staffTrainingRole=role;life.staffTrainingStaffId=s.id;
            Mail("Secrétariat","Formation du staff engagée",s.name+" suit un programme de 45 jours. Fin prévue le "+Epoch.AddDays(life.staffTrainingUntil).ToString("dd/MM/yyyy")+". Coût engagé : "+cost.ToString("N0")+" €. La formation est nominative. Un départ avant son terme met fin au programme ; les frais engagés restent à la charge du club.",null,"staff",StaffTrainingReference());
        }
        public List<TacticalAdvice> TacticalReport(Database db)
        {
            var staff=Staff("assistant");var result=new List<TacticalAdvice>();if(staff.wage<=0)return result;bool live=match!=null&&!match.finished;
            var selection=(live?match.actors.Where(a=>a.side==0&&!a.sentOff).Select(a=>(id:a.id,slot:a.slot)):(lineup??Select(db,club,tactic)).Select((id,slot)=>(id,slot))).ToArray();
            var players=selection.Select(x=>db.Find(x.id)).Where(p=>p!=null).ToArray();if(players.Length==0)return result;
            Action<string,string,string,string,float> add=(key,title,evidence,tradeoff,value)=>result.Add(new TacticalAdvice{key=key,title=title,evidence=evidence,tradeoff=tradeoff,value=value,confidence=Math.Min(95,45+staff.tactics*2)});
            float condition=live?match.actors.Where(a=>a.side==0&&!a.sentOff).Average(a=>a.fitness):(float)players.Average(p=>p.fitness);float passing=(float)players.Where(p=>!p.Goalkeeper).DefaultIfEmpty(players[0]).Average(p=>p.Attribute("shortPassing"));
            var backs=selection.Where(x=>tactic.withoutBall[x.slot].role=="CB").Select(x=>db.Find(x.id)).Where(p=>p!=null&&!p.Goalkeeper).ToArray();float speed=backs.Length==0?65:(float)backs.Average(p=>p.Attribute("sprintSpeed"));
            if(condition<83&&tactic.pressing>.55f)add("pressing","Réduire la pression", "Condition du onze : "+condition.ToString("0")+" %. Le pressing actuel augmente le coût des courses.","Moins de récupérations hautes, davantage d’énergie en fin de match.",.45f);
            if(tactic.line>.65f&&speed<68)add("line","Protéger la profondeur","Vitesse moyenne des défenseurs centraux : "+(speed/5).ToString("0.0")+" / 20. Notre ligne laisse de l’espace dans leur dos.","Le bloc récupérera le ballon plus loin du but adverse.",.5f);
            if(tactic.tempo>.7f&&passing<70)add("tempo","Laisser du temps au porteur","Passe du onze : "+(passing/5).ToString("0.0")+" / 20. Le tempo élevé exige des contrôles et transmissions rapides.","Circulation plus sûre, mais l’adversaire a davantage de temps pour se replacer.",.55f);
            if(tactic.width>.8f&&tactic.directness<.3f)add("width","Rapprocher les soutiens","Une équipe très large avec un jeu très court laisse peu de partenaires à distance de passe.","Moins d’étirement adverse ; les latéraux doivent fournir la largeur.",.65f);
            int cover=tactic.withBall.Skip(1).Count(s=>s.duty=="defend"&&s.y<65);
            if(cover<3)add("cover","Conserver une couverture","Seulement "+cover+" joueurs de champ en mission défensive derrière l’attaque.","Un milieu couvrira les pertes de balle au lieu de multiplier les appels.",0);
            if(staff.tactics>=12&&tactic.counterPress&&condition<78)add("counterPress","Se regrouper à la perte","Le contre-pressing ajoute des efforts à un onze déjà fatigué.","L’adversaire peut sortir plus facilement de sa première relance.",0);
            if(staff.tactics>=12&&live&&match.Minute>=20){
                var observed=match.metrics[0];
                float heading=(float)players.Where(p=>!p.Goalkeeper).DefaultIfEmpty(players[0]).Average(p=>p.Attribute("headingAccuracy"));
                float finishing=(float)players.Where(p=>!p.Goalkeeper).DefaultIfEmpty(players[0]).Average(p=>p.Attribute("finishing"));
                if(tactic.crossing!="low"&&observed.aerialCrosses>=6&&finishing>heading+5)
                    add("lowCrosses","Chercher les partenaires dans les pieds",observed.aerialCrosses+" centres aériens tentés. Notre finition au pied dépasse notre jeu de tête.","Les passes rasantes sont plus rapides, mais peuvent être coupées par le premier défenseur.",0);
                if(tactic.workIntoBox&&match.shots[0]<=2&&observed.possessionSeconds>300)
                    add("workIntoBox","Autoriser davantage de tentatives","Après "+match.Minute+" minutes, nous n’avons tenté que "+match.shots[0]+" tirs malgré "+(int)observed.possessionSeconds+" secondes de possession.","Plus de tentatives, mais davantage de frappes à faible probabilité de but.",0);
            }
            return result.Take(staff.tactics<10?2:staff.tactics<15?4:6).ToList();
        }
        public void ApplyStaffAdvice(Database db,string key)
        {
            RequireEmployment();
            if(life.managerBanUntil>life.day)throw new InvalidOperationException("Vos décisions tactiques sont suspendues jusqu’à votre retour.");var a=TacticalReport(db).FirstOrDefault(x=>x.key==key);if(a==null)throw new InvalidOperationException("Le conseil ne correspond plus à la situation actuelle.");
            switch(key){case "pressing":tactic.pressing=a.value;break;case "line":tactic.line=a.value;break;case "tempo":tactic.tempo=a.value;break;case "width":tactic.width=a.value;break;case "counterPress":tactic.counterPress=false;break;case "cover":var slot=tactic.withBall.Skip(1).Where(s=>s.role=="DM"||s.role=="CM").OrderBy(s=>s.y).FirstOrDefault()??tactic.withBall.Skip(1).OrderBy(s=>s.y).First();slot.duty="defend";slot.y=Math.Min(slot.y,55);break;}
            if(key=="lowCrosses")tactic.crossing="low";
            if(key=="workIntoBox")tactic.workIntoBox=false;
            BindMatchTactic();Mail(Staff("assistant").name,"Consigne ajustée",a.title+". "+a.tradeoff,null,"staff");
        }
        void StaffDay(Database db)
        {
            EnsureStaffMarket(db);StaffMarketDay(db);var d=life.staff;if(d.lastDay==life.day||world==null)return;d.lastDay=life.day;
            if(life.day%7==0)Account(-d.members.Sum(s=>s.wage),"Salaires du staff");
            if(world.managerStatus!="employed")return;
            ReviewStaffTraining();
            bool suspended=life.managerBanUntil>life.day;if(suspended)return;
            var next=NextFixture();int distance=next==null?999:next.day-life.day;var done=new List<string>();
            if(d.training&&Staff("fitness").wage>0){float mean=life.players.Count==0?100:(float)life.players.Average(p=>p.fitness);life.training=distance<=2||mean<82?"rest":"balanced";world.trainingFocus=Staff("fitness").coaching>=12&&TacticalReport(db).Count>0?"tactical":"balanced";if(life.day%7==0)done.Add("Charge "+life.training+" · prochain match dans "+distance+" jours");}
            if(d.lineup&&Staff("assistant").wage>0&&distance<=1&&db.Squad(club).Count(p=>p.unavailableDays==0)>=11){lineup=Select(db,club,tactic);PrepareLineup(db);done.Add("Onze proposé selon les postes, la forme et les indisponibilités");}
            if(d.youth&&Staff("youth").wage>0&&life.day%7==0){foreach(var y in world.youth.Where(y=>db.Find(y.player)?.team=="academy-"+club)){var p=db.Find(y.player);y.focus=p.Attribute("stamina")<p.rating-5?"physical":"technical";y.mentor=db.Squad(club).Where(m=>CanMentorYouth(db,p.id,m.id)&&m.Fit(p.positions?[0]??"CM")>=.86f).OrderByDescending(m=>m.morale).FirstOrDefault()?.id;}done.Add("Plans de formation et mentors réévalués");}
            long scoutCost=Math.Max(500,life.revenue/100000);
            if(d.scouting&&Staff("scout").wage>0&&life.day%7==0&&scoutCost<=d.scoutingWeeklyLimit&&scoutCost<=Math.Max(0,life.cash-life.revenue/40)&&world.reports.Count(r=>r.due>life.day)<3){var p=shortlist.Select(db.Find).Where(p=>p!=null&&p.team!=club&&Knowledge(p.id)<90&&!world.reports.Any(r=>r.player==p.id&&r.due>life.day)).FirstOrDefault();if(p!=null){Scout(db,p.id);done.Add("Observation de "+p.name+" · "+scoutCost+" €");}}
            if(d.press&&Staff("assistant").wage>0&&distance==1&&!world.press.Any(p=>p.fixture==next.id&&p.phase=="before")){Press("before","calm");done.Add("Conférence d’avant-match assurée par l’adjoint");}
            if(done.Count>0)Mail("Staff · délégation","Compte rendu de vos délégations",string.Join(". ",done)+". Vous pouvez reprendre chaque responsabilité dans Staff et délégation.",null,"staff");
        }
    }
}

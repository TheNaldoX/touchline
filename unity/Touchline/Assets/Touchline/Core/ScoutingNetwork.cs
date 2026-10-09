using System;
using System.Collections.Generic;
using System.Linq;

namespace Touchline.Core
{
    [Serializable] public class ScoutMission
    {
        public string id,club,role="Tous",nationality="Tous",country="Tous",priority="ready",status="active";
        public int minAge=18,maxAge=30,started,until,nextSearch,found;
        public long maxFee,maxMonthlyWage,budget,remaining,observationCost;
        public List<string> players=new List<string>();
    }
    public struct AttributeAssessment
    {
        public int low,high;public bool known;
        public override string ToString()=>!known?"—":low==high?low.ToString():low+"–"+high;
    }
    public partial class Career
    {
        public long ObservationCost=>Math.Max(500,life.revenue/100000);
        public int ActiveObservations=>world?.reports.Count(r=>(string.IsNullOrEmpty(r.club)||r.club==club)&&r.confidence<90)??0;
        public ScoutReport ReportFor(string id)=>world?.reports?.Where(r=>r.player==id&&(string.IsNullOrEmpty(r.club)||r.club==club)).OrderByDescending(r=>r.started).FirstOrDefault();
        int ReportKnowledge(ScoutReport r){int age=Math.Max(0,life.day-(r.lastObserved>0?r.lastObserved:r.due));return r.confidence<40?r.confidence:Math.Max(40,r.confidence-Math.Max(0,age-120)/7);}
        void EnsureScouting()
        {
            if(world==null)throw new InvalidOperationException("Une carrière est nécessaire.");
            world.reports??=new List<ScoutReport>();world.scoutMissions??=new List<ScoutMission>();
            foreach(var r in world.reports)if(string.IsNullOrEmpty(r.club))r.club=club;
        }
        bool Scoutable(PlayerData p)=>p!=null&&p.team!=club&&p.team!="retired"&&!string.IsNullOrEmpty(p.team)&&!p.team.StartsWith("academy-",StringComparison.Ordinal);
        void RequireScout(){if(Staff("scout").wage<=0)throw new InvalidOperationException("Le poste de recruteur est vacant. Embauchez un responsable dans Staff et délégation.");}
        static float AssessmentBias(string id,string key,int day)=>((StableIdentity(id+"/"+key+"/"+day)%2001)/1000f-1);
        void StartObservation(Database db,PlayerData p,ScoutMission mission)
        {
            var scout=Staff("scout");int judging=Math.Max(1,Math.Min(20,scout.judging));
            world.reports.RemoveAll(r=>r.player==p.id&&(string.IsNullOrEmpty(r.club)||r.club==club));
            float uncertainty=1.5f+(20-judging)*.22f;
            world.reports.Add(new ScoutReport{player=p.id,club=club,scout=scout.name,mission=mission?.id,judging=judging,started=life.day,due=life.day+Math.Max(5,19-judging/2)+(int)(Roll()*3),estimate=Mathx.Clamp(p.rating+p.development+AssessmentBias(p.id,"ability",life.day)*uncertainty,1,99),potential=Mathx.Clamp(p.potential+AssessmentBias(p.id,"potential",life.day)*(uncertainty+3),1,99),uncertainty=uncertainty,potentialUncertainty=uncertainty+3});
        }
        public ScoutMission CreateScoutMission(Database db,string role,string nationality,int minAge,int maxAge,long maxFee,long maxMonthlyWage,string priority,long budget,string country="Tous")
        {
            OffPitch();EnsureScouting();RequireScout();
            if(!new[]{"Tous","GB","DEF","MIL","ATT","GK","CB","LB","RB","DM","CM","AM","LW","RW","ST"}.Contains(role)||!new[]{"ready","prospect","free","value"}.Contains(priority)||minAge<16||maxAge>45||minAge>maxAge||maxFee<0||maxMonthlyWage<=0||budget<ObservationCost||budget>ObservationCost*4)throw new ArgumentException("Critères invalides. Une mission dure 28 jours et finance une à quatre observations.");
            if(world.scoutMissions.Count(m=>m.club==club&&(m.status=="active"||m.status=="finishing"))>=2)throw new InvalidOperationException("Deux recherches sont déjà en cours. Terminez ou arrêtez une mission.");
            if(string.IsNullOrWhiteSpace(nationality))nationality="Tous";
            if(string.IsNullOrWhiteSpace(country))country="Tous";
            if(country!="Tous"&&!ScoutingGeography.Countries(db).Contains(country))throw new ArgumentException("Ce territoire n’est pas couvert par la base de recrutement.");
            var mission=new ScoutMission{id="scout-"+club+"-"+(++world.serial),club=club,role=role,nationality=nationality,country=country,minAge=minAge,maxAge=maxAge,maxFee=maxFee,maxMonthlyWage=maxMonthlyWage,priority=priority,budget=budget,remaining=budget,observationCost=ObservationCost,started=life.day,until=life.day+28,nextSearch=life.day+1};
            Charge(budget,"Enveloppe d’observation • "+FootballPositions.Label(role)+" / "+nationality);world.scoutMissions.Add(mission);
            ScoutMail(Staff("scout").name,"Mission de recrutement ouverte","Recherche : "+FootballPositions.Label(role)+" · territoire "+country+" · nationalité "+nationality+" · "+minAge+"–"+maxAge+" ans. L’enveloppe de "+budget.ToString("N0")+" € est réservée. Le reliquat sera rendu à la clôture ; trois observations simultanées au maximum.");
            return mission;
        }
        // Named helper keeps mission messages actionable without exposing hidden simulation ratings.
        void ScoutMail(string scout,string subject,string text)=>Mail(scout,subject,text,null,"scout");
        public void StopScoutMission(string id)
        {
            OffPitch();EnsureScouting();var m=world.scoutMissions.FirstOrDefault(m=>m.id==id&&m.club==club&&(m.status=="active"||m.status=="finishing"));if(m==null)throw new InvalidOperationException("Mission déjà terminée.");
            m.status="stopped";RefundScoutMission(m);ScoutMail(Staff("scout").name,"Recherche arrêtée","Les observations déjà engagées continuent. L’enveloppe inutilisée a été restituée.");
        }
        void RefundScoutMission(ScoutMission m){if(m.remaining>0){Account(m.remaining,"Reliquat mission d’observation");m.remaining=0;}}
        void CloseClubScouting()
        {
            if(world==null)return;EnsureScouting();
            foreach(var m in world.scoutMissions.Where(m=>m.club==club&&(m.status=="active"||m.status=="finishing")).ToArray()){m.status="stopped";RefundScoutMission(m);}
            world.reports.RemoveAll(r=>r.club==club&&r.confidence<90);
        }
        public IEnumerable<PlayerData> ScoutCandidates(Database db,ScoutMission m)
        {
            var reports=world.reports.Where(r=>r.club==club||string.IsNullOrEmpty(r.club)).GroupBy(r=>r.player).ToDictionary(g=>g.Key,g=>g.OrderByDescending(r=>r.started).First());var selected=new HashSet<string>(m.players??new List<string>());var followed=new HashSet<string>(shortlist);
            var territoryClubs=new HashSet<string>(ScoutingGeography.ClubIds(db,m.country));bool allTerritories=string.IsNullOrEmpty(m.country)||m.country=="Tous";
            bool NeedsReport(string id)=>!reports.TryGetValue(id,out var r)||r.confidence>=90&&ReportKnowledge(r)<90;
            float Priority(PlayerData p)
            {
                if(m.priority=="prospect")return 45-p.age+(followed.Contains(p.id)?10:0);
                if(m.priority=="value"||m.priority=="free")return (p.team=="free"?20:0)+(followed.Contains(p.id)?10:0)-(float)Math.Log10(Math.Max(1,p.value));
                return (followed.Contains(p.id)?20:0)-Math.Abs(p.age-25)+(reports.TryGetValue(p.id,out var report)?report.estimate*.05f:0);
            }
            return db.players.Where(p=>Scoutable(p)&&(allTerritories||territoryClubs.Contains(p.team))&&p.age>=m.minAge&&p.age<=m.maxAge&&FootballPositions.Matches(p,m.role)&&(m.nationality=="Tous"||p.nationality==m.nationality)&&(p.team=="free"||p.value<=m.maxFee)&&MonthlySalary(p.wage)<=m.maxMonthlyWage&&(m.priority!="free"||p.team=="free")&&!selected.Contains(p.id)&&NeedsReport(p.id)).OrderByDescending(Priority).ThenBy(p=>p.id,StringComparer.Ordinal);
        }
        void ScoutingDay(Database db)
        {
            EnsureScouting();var scout=Staff("scout");bool vacant=scout.wage<=0;
            foreach(var r in world.reports.Where(r=>r.club==club&&r.confidence<90).ToArray())
            {
                var p=db.Find(r.player);if(!Scoutable(p)){world.reports.Remove(r);continue;}
                if(vacant){r.started++;r.due++;continue;}
                r.confidence=Math.Min(90,Math.Max(0,(life.day-r.started)*90/Math.Max(1,r.due-r.started)));
                if(r.confidence<90)continue;
                if(r.judging<=0){r.judging=Math.Max(1,scout.judging);r.scout=scout.name;r.uncertainty=1.5f+(20-r.judging)*.22f;r.potentialUncertainty=r.uncertainty+3;r.estimate=Mathx.Clamp(p.rating+p.development+AssessmentBias(p.id,"ability",r.started)*r.uncertainty,1,99);r.potential=Mathx.Clamp(p.potential+AssessmentBias(p.id,"potential",r.started)*r.potentialUncertainty,1,99);}
                r.lastObserved=life.day;r.advice=ScoutingAdvice(db,p,r);
                Mail(r.scout??scout.name,"Rapport disponible",p.name+" : "+r.advice+" Les étoiles sont relatives à votre effectif ; le potentiel reste incertain.",p.id,"scout");
            }
            foreach(var m in world.scoutMissions.Where(m=>m.club==club&&(m.status=="active"||m.status=="finishing")).ToArray())
            {
                long price=m.observationCost>0?m.observationCost:ObservationCost;
                if(vacant){m.until++;m.nextSearch++;continue;}
                if(m.status=="active"&&(life.day>=m.until||m.remaining<price)){m.status="finishing";RefundScoutMission(m);}
                if(m.status=="active"&&life.day>=m.nextSearch&&ActiveObservations<3)
                {
                    var p=ScoutCandidates(db,m).FirstOrDefault();
                    if(p==null){m.status="finishing";RefundScoutMission(m);}else{m.remaining-=price;m.players.Add(p.id);m.found++;m.nextSearch=life.day+7;StartObservation(db,p,m);Mail(scout.name,"Profil repéré pour votre mission",p.name+" · "+p.age+" ans · "+FootballPositions.List(p.positions??new[]{p.position})+". Il répond à vos critères de marché. L’observation détaillée commence ; sa qualité sportive reste à confirmer.",p.id,"scout");}
                }
                if(m.status=="finishing"&&!world.reports.Any(r=>r.club==club&&r.mission==m.id&&r.confidence<90)){m.status="complete";ScoutMail(scout.name,"Mission de recrutement terminée",m.found+" profil(s) observé(s) dans cette recherche. Consultez Rapports, comparez les profils puis vérifiez les attentes avec leurs agents.");}
            }
        }
        string ScoutingAdvice(Database db,PlayerData p,ScoutReport r)
        {
            float baseline=db.Squad(club).OrderByDescending(x=>x.rating+x.development).Take(11).Select(x=>x.rating+x.development).DefaultIfEmpty(65).Average();
            string level=r.estimate>baseline+5?"Peut renforcer immédiatement votre onze.":r.estimate>=baseline-5?"Peut entrer dans la rotation actuelle.":"Niveau estimé en retrait du onze actuel.";
            string development=p.age<23?" Prévoir du temps de jeu régulier et un plan individuel.":p.age>31?" Contrat court conseillé ; surveiller la charge physique.":" Vérifier l’adaptation au rôle et au collectif.";
            string finance=MonthlySalary(p.wage)>MonthlySalary(Math.Max(1,WageBudget-Payroll(db)))?" Le salaire actuel dépasse votre marge salariale.":" Vérifier indemnité, salaire mensuel et primes avec l’agent.";
            return level+development+finance;
        }
        public float AssessedLevel(Database db,string id,bool potential=false)
        {
            var p=db.Find(id);if(p==null)return 0;if(revealAttributes)return potential?p.potential:p.rating+p.development;
            var report=ReportFor(id);if(p.team!=club){if(report==null)return 0;float estimate=potential?report.potential:report.estimate;return estimate>0?estimate:Mathx.Clamp((potential?p.potential:p.rating+p.development)+AssessmentBias(id,potential?"potential":"ability",report.started)*(potential?6:3),1,99);}
            if(!potential)return p.rating+p.development;
            int judging=Staff("scout").judging;return Mathx.Clamp(p.potential+AssessmentBias(p.id,"own-potential",world?.year??2026)*(3+(20-judging)*.2f),1,99);
        }
        public AttributeAssessment AssessedAttribute(Database db,string id,string key)
        {
            var p=db.Find(id);if(p==null||p.attributes?.Any(a=>a.key==key)!=true)return default;
            int knowledge=Knowledge(id);if(knowledge<40)return default;int raw=(int)Mathx.Clamp((float)Math.Round(p.Attribute(key)/5),1,20);
            if(revealAttributes||p.team==club)return new AttributeAssessment{known=true,low=raw,high=raw};
            var report=ReportFor(id);int judging=report?.judging>0?report.judging:10;
            int uncertainty=knowledge>=90?(judging>=16?1:2):knowledge>=65?2:3;
            int offset=(int)Math.Round(AssessmentBias(id,key,report?.started??0)*Math.Max(.4f,(20-judging)*.12f));
            int center=(int)Mathx.Clamp(raw+offset,1,20);return new AttributeAssessment{known=true,low=Math.Max(1,center-uncertainty),high=Math.Min(20,center+uncertainty)};
        }
    }
}

using System;
using System.Linq;
using System.Collections.Generic;

namespace Touchline.Core
{
    public partial class Career
    {
        public IEnumerable<YouthPath> AcademyPaths(Database db)=>world.youth.Where(y=>{
            var p=db.Find(y.player);return p!=null&&(p.team=="academy-"+club||p.team==club||world.contracts.Any(c=>c.player==p.id&&c.parent==club&&c.club==p.team&&c.loanUntil>life.day));
        });
        YouthPath RequireAcademyPath(Database db,string id)
        {
            var path=AcademyPaths(db).FirstOrDefault(y=>y.player==id);
            if(path==null)throw new InvalidOperationException("Ce joueur n’est plus sous la responsabilité de votre club.");
            return path;
        }
        public string YouthPromotionIssue(Database db,string id)
        {
            var path=AcademyPaths(db).FirstOrDefault(y=>y.player==id);var p=db.Find(id);
            if(path==null)return "Ce joueur n’est plus sous la responsabilité de votre club.";
            if(path.group!="academy"||p.team!="academy-"+club)return "Seul un jeune de votre centre peut être intégré. Un prêt doit se terminer normalement.";
            return Payroll(db)+ReservedWages+Math.Max(250,p.wage)>Math.Max(WageBudget,Payroll(db))?"Le plafond salarial ne permet pas cette intégration. Libérez de la masse salariale avant de le promouvoir.":null;
        }
        public static string AcademyGroup(PlayerData p,YouthPath path)=>path.group=="loan"?"En prêt":path.group=="senior"?"Professionnels":p.age<19?"U19":"Réserve";
        public bool CanMentorYouth(Database db,string youth,string mentor)
        {
            var p=db.Find(youth);var m=db.Find(mentor);
            return p!=null&&m!=null&&m.id!=p.id&&m.team==club&&m.age>=26&&m.Goalkeeper==p.Goalkeeper&&
                world.youth.Count(y=>y.player!=youth&&y.mentor==mentor&&db.Find(y.player)?.team=="academy-"+club)<3;
        }
        public void SetAcademyLoad(Database db,string id,string load)
        {
            OffPitch();var path=RequireAcademyPath(db,id);
            if(db.Find(id).team!="academy-"+club)throw new InvalidOperationException("La charge du joueur dépend de son groupe professionnel ou de son club emprunteur.");
            if(!new[]{"rest","light","standard","intensive"}.Contains(load))throw new ArgumentException("Charge inconnue.");
            path.trainingLoad=load;
        }
        public float AcademyEstimate(Database db,string id,bool potential=false)
        {
            var p=db.Find(id);if(p==null||!AcademyPaths(db).Any(y=>y.player==id))return 0;
            if(revealAttributes)return potential?p.potential:p.rating+p.development;
            var coach=Staff("youth");if(coach.wage<=0)return 0;
            float uncertainty=potential?4+(20-coach.judging)*.35f:1+(20-coach.judging)*.2f;
            return Mathx.Clamp((potential?p.potential:p.rating+p.development)+AssessmentBias(id,potential?"academy-potential":"academy-level",world.year)*uncertainty,1,99);
        }
        public string AcademyAdvice(Database db,string id)
        {
            var path=RequireAcademyPath(db,id);var p=db.Find(id);var coach=Staff("youth");
            if(path.group=="loan")return "Comparer ses apparitions et sa concurrence au club emprunteur avant de préparer la saison prochaine. Les minutes de prêt restent estimées.";
            if(p.unavailableDays>0||Injury(id)!=null)return "Priorité à la récupération : aucune progression d’entraînement tant que le joueur est indisponible.";
            if(p.fitness<65)return "Fatigue importante : la prochaine séance sera remplacée par de la récupération. Allégez son programme.";
            if(path.group=="academy"&&path.trainingLoad=="intensive"&&p.fitness<80)return "La charge soutenue entame sa condition. Prévoir une période allégée avant d’accumuler trop de fatigue.";
            if(coach.wage<=0||coach.judging<9)return "L’encadrement ne permet pas une recommandation individuelle précise. Renforcez le staff et observez son évolution.";
            if(path.group=="senior")return path.seniorMinutes<180&&p.age>=19?"Une intégration ne suffit pas : chercher des apparitions réelles ou un prêt avec une concurrence accessible.":"Conserver un rôle adapté et surveiller ses apparitions ; la progression dépend désormais du groupe professionnel.";
            float baseline=db.Squad(club).OrderByDescending(x=>x.rating+x.development).Take(11).Select(x=>x.rating+x.development).DefaultIfEmpty(65).Average();
            if(p.age>=18&&AcademyEstimate(db,id)>=baseline-9)return "Une intégration progressive peut être envisagée. Prévoir sa place dans la rotation et vérifier la marge salariale avant de le promouvoir.";
            return p.age>=19?"Il a besoin d’un projet senior adapté : évaluer un prêt après intégration, sans lui promettre un rôle inaccessible.":"Poursuivre la formation avec une charge régulière. Le potentiel reste une estimation, pas une promesse de réussite.";
        }
        void AcademyDevelopmentDay(Database db)
        {
            var paths=AcademyPaths(db).ToArray();var coach=Staff("youth");
            foreach(var y in paths){
                var p=db.Find(y.player);if(y.group!="academy"||p.team!="academy-"+club||p.age>=22||y.lastAcademyDay==life.day)continue;
                y.lastAcademyDay=life.day;
                if(y.mentor!=null&&!CanMentorYouth(db,p.id,y.mentor))y.mentor=null;
                var weekday=Date.DayOfWeek;bool session=weekday==DayOfWeek.Monday||weekday==DayOfWeek.Tuesday||weekday==DayOfWeek.Thursday||weekday==DayOfWeek.Friday;
                string load=y.trainingLoad??"standard";
                if(p.unavailableDays>0||Injury(p.id)!=null){SavePlayer(p);continue;}
                if(!session||load=="rest"||p.fitness<65){p.fitness=Math.Min(100,p.fitness+6);if(p.morale<75)p.morale=Math.Min(75,p.morale+.2f);SavePlayer(p);continue;}
                y.trainingSessions++;
                float staff=coach.wage>0?.6f+Mathx.Clamp(coach.coaching,1,20)/25f:.45f;
                float intensity=load=="light"?.75f:load=="intensive"?1.18f:1;
                bool mentor=y.mentor!=null&&db.Find(y.mentor).unavailableDays==0&&db.Find(y.mentor).morale>=45;
                float gain=(.008f+Level("academy")*.001f)*staff*intensity*(.5f+p.morale/200)*Mathx.Clamp(p.fitness/90,.6f,1)*(mentor?1.08f:1);
                y.progress=p.rating>=p.potential?0:Math.Min(1,y.progress+gain);
                p.fitness=Mathx.Clamp(p.fitness-(load=="intensive"?8:load=="light"?.25f:1),20,100);
                if(load=="intensive"&&p.fitness<75)p.morale=Math.Max(20,p.morale-.4f);
                if(y.progress>=1&&p.rating<p.potential){
                    float step=Math.Min(1,p.potential-p.rating);y.progress=0;p.rating+=step;
                    foreach(var a in p.attributes??Array.Empty<AttributeValue>()){
                        bool physical=a.key=="sprintSpeed"||a.key=="acceleration"||a.key=="stamina"||a.key=="strength";
                        bool tactical=a.key=="vision"||a.key=="interceptions"||a.key=="defensiveAwareness";
                        float growth=y.focus=="balanced"?1:(y.focus=="physical"&&physical||y.focus=="tactical"&&tactical||y.focus=="technical"&&!physical&&!tactical)?1.3f:.8f;
                        // Never lower a pre-existing strong attribute when applying a gain.
                        a.value=Math.Min(99,a.value+step*growth);
                    }
                }
                SavePlayer(p);
            }
            if(life.day>0&&life.day%28==0&&paths.Any(y=>y.lastReview<life.day)){
                foreach(var y in paths)y.lastReview=life.day;
                var priorities=paths.OrderByDescending(y=>db.Find(y.player).age).Take(3).Select(y=>db.Find(y.player).name+" : "+AcademyAdvice(db,y.player));
                Mail(coach.name,"Bilan du centre de formation",paths.Length+" jeunes suivis.\n"+string.Join("\n",priorities)+"\nConsultez Formation pour ajuster les plans, l’encadrement et les parcours.",null,"academy");
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Touchline.Core
{
    public sealed class MatchObservation { public string key,text;public int priority; }
    public sealed class DangerMan { public string id,name,reason;public float threat; }
    public sealed class OpponentReport { public string style,plan;public List<DangerMan> danger=new List<DangerMan>();public List<string> advice=new List<string>(); }

    // Lecture du match par l'adjoint : phrases courtes déduites des mesures
    // réellement jouées (côté 0 = club dirigé). Aucun tirage aléatoire.
    public static class MatchAssistant
    {
        static readonly CultureInfo French=CultureInfo.GetCultureInfo("fr-FR");
        // Seuils d'observation (comptes cumulés depuis le coup d'envoi).
        public const int PressuredLossesAlert=6,ThroughBallsAlert=3,CrossesAlert=7,HighRecoveriesPraise=5;
        public const float HighLineThreshold=.6f,HighPressThreshold=.6f,PassCompletionAlert=72f,TiredAlert=72f,NervesAlert=-.2f;
        static float Completion(MatchState m,int side)=>m.passes[side]>0?100f*m.completedPasses[side]/m.passes[side]:100;
        public static float AverageComposure(MatchState m,int side)
        {
            var mind=m.mindset!=null&&m.mindset.Length==2?m.mindset[side]:null;if(mind==null)return 0;
            float shout=m.clock<mind.shoutUntil?mind.shoutComposure:0;return mind.Average(mind.composure)+shout;
        }
        public static float AverageDrive(MatchState m,int side)
        {
            var mind=m.mindset!=null&&m.mindset.Length==2?m.mindset[side]:null;if(mind==null)return 0;
            float shout=m.clock<mind.shoutUntil?mind.shoutDrive:0;return mind.Average(mind.drive)+shout;
        }
        static bool ExposedFullBacks(Tactic t)=>Enumerable.Range(1,10).Any(i=>(t.withoutBall[i].role=="LB"||t.withoutBall[i].role=="RB")&&(t.withBall[i].duty=="attack"||t.withoutBall[i].instruction==PlayerInstructions.GetForward));
        public static List<MatchObservation> Observe(MatchState m)
        {
            var list=new List<MatchObservation>();var us=m.metrics[0];var them=m.metrics[1];var ours=m.homeTactic;var theirs=m.awayTactic;
            void Add(string key,int priority,string text)=>list.Add(new MatchObservation{key=key,priority=priority,text=text});
            if(us.pressuredLosses>=PressuredLossesAlert&&theirs.pressing>=HighPressThreshold)
                Add("press-exposed",5,ExposedFullBacks(ours)?"Ils pressent haut et nos latéraux montés sont exposés : "+us.pressuredLosses+" ballons perdus sous pression. Jouez plus direct ou gardez un latéral.":"Ils pressent haut : "+us.pressuredLosses+" ballons perdus sous pression. Des passes plus directes contourneraient ce pressing.");
            if(them.throughBalls>=ThroughBallsAlert&&ours.line>=HighLineThreshold)
                Add("line-exposed",5,"Ils attaquent l’espace dans le dos de notre ligne haute : "+them.throughBalls+" passes en profondeur. Essayez de baisser la ligne d’un cran ; surveillez les appels adverses et nos récupérations hautes pour juger le compromis.");
            if(them.crosses>=CrossesAlert)
                Add("crosses",4,"Leurs centres se multiplient ("+them.crosses+"). Essayez de garder un latéral en couverture pour limiter les livraisons ; surveillez ensuite les centres concédés, sans dégarnir l’axe.");
            float understanding=m.mindset!=null&&m.mindset.Length==2&&m.mindset[0]!=null?m.mindset[0].understanding:0;
            if(m.passes[0]>=40&&Completion(m,0)<PassCompletionAlert)
                Add("passing",4,(understanding< -.15f?"Le groupe manque d’automatismes : ":"Passes réussies : ")+Completion(m,0).ToString("0",French)+" %. "+(understanding< -.15f?"Essayez un système plus familier.":ours.tempo>.65f?"Le rythme peut y contribuer : essayez de le réduire d’un cran, sans présumer que c’est la seule cause.":"Essayez un soutien proche du porteur pour offrir une sortie de passe.")+" Surveillez la précision et les pertes sous pression après ce changement.");
            if(AverageComposure(m,0)<NervesAlert)
                Add("nerves",3,"Le groupe est tendu : décisions hâtives. « Concentration ! » ou une causerie rassurante peut l’apaiser.");
            float fitness=m.actors.Where(a=>a.side==0&&!a.sentOff&&a.slot>0).Select(a=>a.fitness).DefaultIfEmpty(100).Average();
            if(fitness<TiredAlert&&(ours.pressing>=HighPressThreshold||AverageDrive(m,0)>.2f))
                Add("tired",4,"Condition moyenne "+fitness.ToString("0",French)+" % avec un engagement élevé. Essayez un remplacement ou un pressing moins intense ; surveillez ensuite la condition et les récupérations, car réduire l’effort peut leur laisser plus de temps.");
            if(us.highRecoveries>=HighRecoveriesPraise)
                Add("high-recoveries",2,"Nous récupérons haut : "+us.highRecoveries+" ballons gagnés dans leur moitié. Gardez ce réglage si les occasions suivent ; surveillez la fatigue et les passes dans notre dos.");
            if(m.score[0]<m.score[1]&&m.Minute>=60&&ours.mentality<.5f)
                Add("chasing",3,"Nous sommes menés et le bloc reste prudent : une mentalité plus offensive ouvrirait davantage d’occasions.");
            return list.OrderByDescending(o=>o.priority).ToList();
        }
        static string Level(float v,string low,string mid,string high)=>v<.38f?low:v>.62f?high:mid;
        public static string Style(Tactic t)=>t.formation+" · "+Level(t.line,"bloc bas","bloc médian","ligne haute")+" · "+Level(t.pressing,"pressing mesuré","pressing modéré","pressing intense")+" · "+Level(t.directness,"jeu court","jeu mixte","jeu direct");
        static float Attr(PlayerData p,string key)=>p.Attribute(key);
        public static OpponentReport Opponent(Database db,MatchState m)
        {
            var report=new OpponentReport{style=Style(m.awayTactic)};var t=m.awayTactic;
            var starters=m.actors.Where(a=>a.side==1&&a.slot>0).Select(a=>(slot:a.slot,p:db.Find(a.id))).Where(x=>x.p!=null).ToList();
            foreach(var x in starters){
                var p=x.p;float finishing=Attr(p,"finishing"),pace=Attr(p,"sprintSpeed"),vision=Attr(p,"vision"),heading=Attr(p,"headingAccuracy");
                string role=t.withoutBall[x.slot].role;bool forward=role=="ST"||role=="LW"||role=="RW"||role=="AM";
                float threat=forward?finishing*.5f+pace*.3f+vision*.2f:vision*.5f+Attr(p,"longPassing")*.3f+finishing*.2f;
                string reason=finishing>=78&&forward?"finisseur redoutable":pace>=85?"très rapide dans la profondeur":vision>=80?"créateur, voit la passe avant les autres":heading>=80?"dangereux dans les airs":"pièce maîtresse de leur animation";
                report.danger.Add(new DangerMan{id=p.id,name=p.name,reason=reason,threat=threat});
            }
            report.danger=report.danger.OrderByDescending(d=>d.threat).ThenBy(d=>d.id,StringComparer.Ordinal).Take(2).ToList();
            report.plan=t.counterAttack?"Ils attendent pour partir en contre.":t.pressing>.62f?"Ils viennent presser haut dès la relance.":t.line<.38f?"Ils défendent bas et compact.":"Ils cherchent un match équilibré.";
            if(t.line>.55f)report.advice.Add("Leur ligne est haute : nos appels en profondeur peuvent faire mal.");
            if(t.pressing>.62f)report.advice.Add("Leur pressing est agressif : un jeu plus direct évite les pertes à la relance.");
            if(t.line<.38f)report.advice.Add("Bloc bas : de la largeur et de la patience pour l’étirer.");
            if(t.counterAttack)report.advice.Add("Contre-attaque : gardez une couverture derrière les latéraux.");
            if(report.danger.Count>0&&report.danger[0].reason.Contains("rapide"))report.advice.Add("Ne laissez pas d’espace dans le dos à "+report.danger[0].name+" : une ligne trop haute serait risquée.");
            if(report.advice.Count==0)report.advice.Add("Pas de point faible évident : la qualité d’exécution fera la différence.");
            return report;
        }
        // Bilan de fin de match : les consignes et l'état du groupe tels qu'ils se sont vus.
        public static List<string> FullTimeReview(MatchState m)
        {
            var lines=new List<string>();var us=m.metrics[0];var t=m.homeTactic;
            lines.Add("Passes réussies : "+Completion(m,0).ToString("0",French)+" % · récupérations hautes : "+us.highRecoveries+" · tirs : "+m.shots[0]+".");
            var mind=m.mindset!=null&&m.mindset.Length==2?m.mindset[0]:null;
            if(mind!=null&&mind.familiarity>=0)lines.Add(mind.understanding< -.15f?"Automatismes insuffisants ("+mind.familiarity.ToString("0",French)+" % de familiarité, cohésion "+mind.cohesion.ToString("0",French)+" %) : cela s’est vu dans les passes et le placement.":mind.understanding>.15f?"Le groupe maîtrise son système : placement et passes plus sûrs.":"Automatismes corrects, sans avantage net.");
            if(t.line>=HighLineThreshold)lines.Add(m.metrics[1].throughBalls>=ThroughBallsAlert?"La ligne haute a été attaquée dans son dos ("+m.metrics[1].throughBalls+" passes en profondeur adverses).":"La ligne haute a tenu : peu de ballons dans notre dos.");
            if(t.pressing>=HighPressThreshold)lines.Add("Le pressing intense a coûté de l’énergie : condition finale "+m.actors.Where(a=>a.side==0&&a.slot>0).Average(a=>a.fitness).ToString("0",French)+" %.");
            if(mind!=null&&mind.talks.Count>0)lines.Add("Causeries : "+string.Join(", ",mind.talks.Select(TeamTalks.Describe))+".");
            return lines;
        }
    }
}

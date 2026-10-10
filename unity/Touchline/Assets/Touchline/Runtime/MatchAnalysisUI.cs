using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using Touchline.Core;

namespace Touchline
{
    public sealed partial class TouchlineApp
    {
        string matchAnalysisTab="Statistiques";bool allMatchEvents;
        void MatchAnalysis()
        {
            arena.Paused=true;var m=Career.match;var panel=Modal("Lecture du match");panel.AddToClassList("match-analysis");panel.name="match-analysis";
            var order=FixtureDisplayOrder(m);var clubs=Row(panel,"analysis-clubs");Text(clubs,ClubName(order.HomeClub));Text(clubs,order.Score(m)+" · "+m.Minute+"′","analysis-score");Text(clubs,ClubName(order.AwayClub));
            var tabs=Row(panel,"profile-tabs");var body=Scroll(panel);body.name="match-analysis-body";
            string talkMoment=Career.TalkMoment();bool talkOpen=talkMoment!=null&&m.mindset!=null&&m.mindset.Length==2;
            // À la pause et au coup de sifflet final, le vestiaire s'ouvre d'abord tant que le groupe n'a pas été réuni.
            if(talkOpen&&m.mindset[0]?.talks?.Any(t=>t.StartsWith(talkMoment+"|group|",StringComparison.Ordinal))!=true){matchAnalysisTab="Vestiaire";lastTalkReactions=null;lastTalkError=null;}
            if(!talkOpen&&matchAnalysisTab=="Vestiaire")matchAnalysisTab="Statistiques";
            void Show(string tab){matchAnalysisTab=tab;foreach(var b in tabs.Query<Button>().ToList())b.EnableInClassList("active",b.text==tab);body.Clear();body.scrollOffset=Vector2.zero;if(tab=="Temps forts")MatchTimeline(body,m);else if(tab=="Adjoint")MatchCoach(body,m);else if(tab=="Vestiaire")TeamTalkCard(body,()=>Show("Vestiaire"));else MatchStatistics(body,m);AnimateEntry(body);}
            foreach(var tab in talkOpen?new[]{"Statistiques","Temps forts","Adjoint","Vestiaire"}:new[]{"Statistiques","Temps forts","Adjoint"})Button(tabs,tab,()=>Show(tab));Show(matchAnalysisTab);
            var actions=Row(panel,"analysis-actions");Button(actions,"Modifier les consignes",()=>Navigate("Tactique")).SetEnabled(Career.life.managerBanUntil<=Career.life.day);
            Button(actions,m.finished?"Retour au club":m.halfTime?"Deuxième mi-temps":"Reprendre le match",()=>{CloseModal();if(m.finished){Navigate("Club");return;}if(m.halfTime)arena.Simulation.ResumeHalf();arena.Paused=false;}).AddToClassList("primary");
        }
        void MatchStatistics(VisualElement parent,MatchState m)
        {
            var order=FixtureDisplayOrder(m);var a=m.metrics[0];var b=m.metrics[1];float total=a.possessionSeconds+b.possessionSeconds;int homeShare=total>0?Mathf.RoundToInt(a.possessionSeconds/total*100):0;
            void Stat(string label,string home,string away){var row=Row(parent,"match-stat");Text(row,label,"match-stat-label");Text(row,order.HomeSide==0?home:away,"match-stat-value");Text(row,order.HomeSide==0?away:home,"match-stat-value");}
            Stat("Équipes",ClubName(m.home),ClubName(m.away));
            Stat("Possession",total>0?homeShare+" %":"—",total>0?(100-homeShare)+" %":"—");Stat("Tirs / cadrés",$"{m.shots[0]} / {a.shotsOnTarget}",$"{m.shots[1]} / {b.shotsOnTarget}");Stat("Qualité des occasions (xG)",$"{a.xg:0.00}",$"{b.xg:0.00}");
            Stat("Passes réussies",$"{m.completedPasses[0]} / {m.passes[0]}",$"{m.completedPasses[1]} / {m.passes[1]}");Stat("Longueur des passes",m.passes[0]>0?$"{a.AveragePassLength(m.passes[0]):0.0} m":"—",m.passes[1]>0?$"{b.AveragePassLength(m.passes[1]):0.0} m":"—");
            Stat("Largeur en possession",a.shapeSamples>0?$"{a.AverageWidth:0.0} m":"—",b.shapeSamples>0?$"{b.AverageWidth:0.0} m":"—");Stat("Ligne depuis son propre but",a.defensiveSamples>0?$"{a.AverageLine+52.5f:0.0} m":"—",b.defensiveSamples>0?$"{b.AverageLine+52.5f:0.0} m":"—");Stat("Récupérations hautes",a.highRecoveries.ToString(),b.highRecoveries.ToString());
            Stat("Centres / passes en profondeur",$"{a.crosses} / {a.throughBalls}",$"{b.crosses} / {b.throughBalls}");Stat("Corners / fautes",$"{a.corners} / {a.fouls}",$"{b.corners} / {b.fouls}");Stat("Contrôles échappés",a.miscontrols.ToString(),b.miscontrols.ToString());
            Stat("Passes offensives · gauche / droite",$"{a.leftAttackPasses} / {a.rightAttackPasses}",$"{b.leftAttackPasses} / {b.rightAttackPasses}");
            Stat("Centres rasants / aériens",$"{a.lowCrosses} / {a.aerialCrosses}",$"{b.lowCrosses} / {b.aerialCrosses}");
            Stat("Arrêts / sorties captées",$"{a.saves} / {a.keeperClaims}",$"{b.saves} / {b.keeperClaims}");Stat("Hors-jeu",a.offsides.ToString(),b.offsides.ToString());Stat("Remplacements",m.substitutions[0].ToString(),m.substitutions[1].ToString());
            Text(parent,"Données issues des actions jouées. Les xG sont une estimation interne. Le début de match offre encore peu de recul.","footnote");
        }
        static readonly string[] MajorEventKinds={"kickoff","goal","shot","save","woodwork","yellow","red","injury","substitution","penalty","interval","fulltime","abandoned","assistant","shout","opponent-talk"};
        static bool SignificantMoment(string kind)=>Array.IndexOf(MajorEventKinds,kind)>=0;
        static string MomentLabel(string kind)=>kind switch{"kickoff"=>"ENGAGEMENT","goal"=>"BUT","shot"=>"TIR","save"=>"ARRÊT","woodwork"=>"MONTANT","yellow"=>"AVERTISSEMENT","red"=>"EXCLUSION","injury"=>"BLESSURE","substitution"=>"REMPLACEMENT","penalty"=>"PENALTY","interval"=>"MI-TEMPS","fulltime"=>"FIN DU MATCH","abandoned"=>"ARRÊT DU MATCH","assistant"=>"ADJOINT","shout"=>"DEPUIS LA TOUCHE","opponent-talk"=>"VESTIAIRE ADVERSE",_=>"ACTION"};
        void MatchTimeline(VisualElement parent,MatchState m)
        {
            var filter=new Toggle("Inclure les passes et les duels"){value=allMatchEvents};parent.Add(filter);var entries=new VisualElement();parent.Add(entries);
            void Populate(){entries.Clear();var moments=m.events.Where(e=>allMatchEvents||SignificantMoment(e.kind)).Reverse().Take(100).ToArray();foreach(var e in moments){var card=Card(entries,"match-moment");card.AddToClassList(FixtureDisplayOrder(m).MomentClass(e.side));Text(card,m.MinuteAt(e.time)+"′ · "+MomentLabel(e.kind),"eyebrow");Text(card,e.text);if(e.kind=="shot")Text(card,"Qualité estimée : "+e.xg.ToString("0.00",French)+" xG","muted");var player=e.player==null?null:Database.Find(e.player);if(player!=null){var link=Button(card,player.name+" · fiche",()=>PlayerProfile(player.id,()=>{matchAnalysisTab="Temps forts";MatchAnalysis();}));link.AddToClassList("moment-profile");}}
                if(moments.Length==0)Text(entries,"Aucun moment important enregistré pour le moment.","empty-state");else Text(entries,"Du plus récent au plus ancien · 100 événements maximum dans cette vue.","footnote");}
            filter.RegisterValueChangedCallback(e=>{allMatchEvents=e.newValue;Populate();});Populate();
        }
        void MatchCoach(VisualElement parent,MatchState m)
        {
            var chances=Card(parent);chances.name="match-chance-review";Text(chances,"OCCASIONS ET FINITION","section-title");
            var chanceLines=MatchAssistant.ChanceReview(m);foreach(var line in chanceLines.Take(chanceLines.Count-1))Text(chances,line);
            var explanation=new Foldout{text="Comprendre les xG",value=false};chances.Add(explanation);Text(explanation,chanceLines.Last(),"muted");
            AssistantObservations(parent,m);
            if(Career.world!=null)Career.EnsureStaffMarket(Database);var staff=Career.Staff("assistant");Text(parent,staff.name+" · Tactique "+staff.tactics+" / 20","section-title");
            if(staff.wage<=0){Text(parent,"Le poste d’adjoint est vacant. Recrutez un adjoint pour disposer de conseils.","notice");return;}
            Text(parent,staff.fictional?"Adjoint fictif · compétences de simulation":"Identité réelle · compétences estimées pour la simulation","footnote");
            Text(parent,"Les conseils tiennent compte des joueurs actuellement sur le terrain. L’adjoint signale aussi le compromis associé à chaque changement.","muted");
            var advice=Career.TacticalReport(Database);foreach(var item in advice){var card=Card(parent);Text(card,item.title,"section-title");Text(card,item.evidence);Text(card,item.tradeoff,"muted");Text(card,"Confiance de l’analyse : "+item.confidence+" / 100","footnote");
                var apply=Button(card,"Appliquer ce conseil",()=>{try{Career.ApplyStaffAdvice(Database,item.key);Save();MatchAnalysis();}catch(Exception e){Message(e.Message);}});apply.SetEnabled(Career.life.managerBanUntil<=Career.life.day);}
            if(advice.Count==0)Text(parent,"Aucune contradiction majeure détectée dans les consignes. Cela ne prédit pas le résultat.");
            if(m.clock<80)Text(parent,"Les premières minutes offrent encore peu d’actions à analyser.","notice");
            else if(staff.tactics>=12){var a=m.metrics[0];var card=Card(parent);Text(card,"Ce que nous observons","section-title");Text(card,$"Largeur avec ballon : {a.AverageWidth:0.0} m · Longueur des passes : {a.AveragePassLength(m.passes[0]):0.0} m · Récupérations hautes : {a.highRecoveries}.");Text(card,"Ces moyennes couvrent tout le match joué jusqu’ici. Un changement récent demande du temps avant d’être visible dans les chiffres.","muted");}
            if(staff.tactics>=10&&m.awayPlan!="balanced")Text(parent,m.awayPlan=="chase"?"L’adversaire prend davantage de risques. Surveillez les espaces derrière ses joueurs qui se projettent.":"L’adversaire protège son avance. Cherchez des soutiens entre les lignes en conservant une couverture.","notice");
        }
    }
}

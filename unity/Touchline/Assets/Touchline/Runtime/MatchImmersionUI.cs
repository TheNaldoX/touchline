using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using Touchline.Core;

namespace Touchline
{
    // Causeries, briefing adverse, automatismes, cris depuis la touche et lecture
    // de l'adjoint. Toute la logique est dans Core (TeamTalks, MatchAssistant,
    // TacticalPreparation, MatchMindset) ; ici seulement l'affichage.
    public sealed partial class TouchlineApp
    {
        const float ImmersionTouchTarget=50; // unités UI ≈ 48 dp sur les deux formats du Fold
        string talkRecipient;List<TalkReaction> lastTalkReactions;string lastTalkError;
        static string Percent(float value)=>value<0?"—":Mathf.RoundToInt(value)+" %";
        static string UnderstandingWord(float u)=>u>.15f?"rodés":u< -.35f?"très fragiles":u< -.1f?"fragiles":"corrects";
        void PreparationCard(VisualElement parent)
        {
            var m=Career.match;var mind=m?.mindset!=null&&m.mindset.Length==2?m.mindset[0]:null;if(mind==null||mind.familiarity<0)return;
            var card=Card(parent,"immersion-card");card.name="immersion-preparation";Text(card,"NOTRE PRÉPARATION","eyebrow");
            Text(card,"Automatismes "+UnderstandingWord(mind.understanding),"section-title");
            Text(card,"Familiarité tactique "+Percent(mind.familiarity)+" · cohésion du onze "+Percent(mind.cohesion)+" · thème : "+Core.Career.TrainingFocusLabel(Career.TrainingFocus)+".");
            Text(card,mind.understanding< -.1f?"Sur le terrain : passes moins précises, ligne défensive moins alignée, pressing moins coordonné.":"Sur le terrain : placement et passes conformes à ce qui a été travaillé.","muted");
        }
        void OpponentBriefingCard(VisualElement parent)
        {
            if(Career.match==null)return;var report=MatchAssistant.Opponent(Database,Career.match);
            var card=Card(parent,"immersion-card");card.name="immersion-opponent";Text(card,"RAPPORT SUR L’ADVERSAIRE","eyebrow");
            Text(card,report.style,"section-title");Text(card,report.plan);
            foreach(var d in report.danger)Text(card,"Danger : "+d.name+" · "+d.reason+".","immersion-danger");
            foreach(var line in report.advice.Take(2))Text(card,"Adjoint : "+line,"muted");
        }
        void TeamTalkCard(VisualElement parent,Action refresh)
        {
            string moment=Career.TalkMoment();if(moment==null||Career.world!=null&&Career.world.managerStatus!="employed")return;
            var card=Card(parent,"immersion-card");card.name="team-talk";
            Text(card,"CAUSERIE · "+TeamTalks.MomentLabel(moment).ToUpper(French),"eyebrow");
            var mind=Career.match.mindset!=null&&Career.match.mindset.Length==2?Career.match.mindset[0]:null;
            bool groupDone=mind!=null&&mind.talks.Any(t=>t.StartsWith(moment+"|group|",StringComparison.Ordinal));
            var situation=Career.CurrentTalkSituation(Database,moment);
            Text(card,(moment==TeamTalks.FullTime?"Score final ":moment==TeamTalks.HalfTime?"Score ":"")+(moment==TeamTalks.PreMatch?"":Career.match.score[0]+" – "+Career.match.score[1]+" · ")+"enjeu "+(situation.importance>=.7f?"élevé":situation.importance<=.2f?"faible":"normal")+" · "+(situation.gap>3?"favori":situation.gap< -3?"outsider":"rapport équilibré")+".","muted");
            var audience=Career.match.actors.Where(a=>a.side==0&&!a.sentOff).Select(a=>a.id).ToList();
            if(moment==TeamTalks.FullTime)audience=Career.match.used.Where(id=>Career.life.players.Any(p=>p.id==id)).ToList();
            var names=new List<string>{"Tout le groupe"};names.AddRange(audience.Select(id=>Database.Find(id)?.name??id));
            int selected=talkRecipient==null?0:Math.Max(0,audience.IndexOf(talkRecipient)+1);
            var recipient=new DropdownField("Destinataire",names,selected){name="team-talk-recipient"};card.Add(recipient);recipient.style.minHeight=ImmersionTouchTarget;
            recipient.RegisterValueChangedCallback(_=>{talkRecipient=recipient.index<=0?null:audience[recipient.index-1];});
            var grid=Row(card,"team-talk-tones");grid.style.flexWrap=Wrap.Wrap;
            foreach(var tone in TeamTalks.Tones){
                string chosen=tone;var b=Button(grid,TeamTalks.Label(tone),()=>{
                    try{lastTalkReactions=Career.GiveTeamTalk(Database,chosen,talkRecipient);lastTalkError=null;Save();}
                    catch(Exception e){lastTalkError=e.Message;}
                    refresh();});
                b.name="team-talk-"+tone;b.tooltip=TeamTalks.Hint(tone);b.style.minHeight=ImmersionTouchTarget;b.style.flexGrow=1;b.style.flexBasis=Length.Percent(30);
                b.SetEnabled(talkRecipient!=null||!groupDone);
            }
            Text(card,talkRecipient==null?"Un message au groupe par moment ; trois échanges individuels au plus. La réaction dépend du score, de l’enjeu, du caractère et de la confiance de chacun.":"Un échange individuel porte davantage, en bien comme en mal.","footnote");
            if(lastTalkError!=null)Text(card,lastTalkError,"status-warning");
            if(lastTalkReactions!=null&&lastTalkReactions.Count>0){
                var summary=lastTalkReactions.GroupBy(r=>r.label).OrderByDescending(g=>g.Count()).Select(g=>g.Count()+" "+g.Key.ToLower(French));
                Text(card,"Réactions : "+string.Join(" · ",summary)+".","section-title");
                foreach(var r in lastTalkReactions.Where(r=>r.label!="Indifférent").OrderByDescending(r=>Math.Abs(r.composure)+Math.Abs(r.drive)+Math.Abs(r.morale)/10).Take(4))
                    Text(card,r.name+" · "+r.label+(moment==TeamTalks.FullTime?" · moral "+r.morale.ToString("+0;-0;0"):" · sang-froid "+Signed(r.composure)+" · engagement "+Signed(r.drive)),"muted");
            }
            if(mind!=null&&mind.talks.Count>0)Text(card,"Déjà dit : "+string.Join(" ; ",mind.talks.Where(t=>t.StartsWith(moment+"|",StringComparison.Ordinal)).Select(TeamTalks.Describe)),"footnote");
        }
        static string Signed(float v)=>v>.04f?"↑":v< -.04f?"↓":"=";
        void TouchlineShoutsCard(VisualElement parent)
        {
            var sim=arena?.Simulation;var m=sim?.State;if(m==null||m.mindset==null||m.mindset.Length!=2)return;
            var card=Card(parent,"instruction-card");card.name="touchline-shouts";Text(card,"Depuis la touche","section-title");
            var row=Row(card,"instruction-options");row.style.flexWrap=Wrap.Wrap;
            foreach(var kind in MatchSimulation.ShoutKinds){
                string chosen=kind;var b=Button(row,MatchSimulation.ShoutLabel(kind),()=>{try{sim.Shout(chosen);Save();CloseModal();}catch(Exception e){Message(e.Message);}});
                b.name="shout-"+kind;b.tooltip=MatchSimulation.ShoutTradeoff(kind);b.style.minHeight=ImmersionTouchTarget;b.style.fontSize=13;b.style.flexGrow=1;b.style.flexBasis=Length.Percent(45);b.SetEnabled(sim.CanShout());
            }
            var mind=m.mindset[0];
            Text(card,sim.CanShout()?"Effet temporaire (environ "+MatchSimulation.ShoutMinutes.ToString("0")+" minutes de match). Calmez le jeu et Allez de l’avant modifient aussi rythme ou mentalité.":"Dernière consigne : « "+MatchSimulation.ShoutLabel(mind.lastShoutKind)+" ». Vos joueurs l’appliquent encore.","muted");
        }
        void AssistantObservations(VisualElement parent,MatchState m)
        {
            if(m.mindset==null||m.mindset.Length!=2)return;
            var card=Card(parent,"immersion-card");card.name="assistant-observations";Text(card,m.finished?"BILAN TACTIQUE":"CE QUE JE VOIS DEPUIS LE BANC","eyebrow");
            var lines=m.finished?MatchAssistant.FullTimeReview(m):MatchAssistant.Observe(m).Select(o=>o.text).Take(3).ToList();
            if(lines.Count==0)Text(card,"Rien d’alarmant pour l’instant : le plan est respecté.","muted");
            foreach(var line in lines)Text(card,line);
            Text(card,"Sang-froid moyen "+Signed(MatchAssistant.AverageComposure(m,0))+" · engagement "+Signed(MatchAssistant.AverageDrive(m,0))+" · automatismes "+UnderstandingWord(m.mindset[0].understanding)+".","footnote");
        }
        string LatestAssistantLine(MatchState m)
        {
            for(int i=m.events.Count-1;i>=0;i--)if(m.events[i].kind=="assistant")return "Adjoint · "+m.events[i].text;
            return null;
        }
    }
}

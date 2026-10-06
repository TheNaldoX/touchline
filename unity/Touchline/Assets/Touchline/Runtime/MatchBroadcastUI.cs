using System;
using UnityEngine;
using UnityEngine.UIElements;
using Touchline.Core;

namespace Touchline
{
    public sealed partial class TouchlineApp
    {
        VisualElement quietPanel;Image broadcastImage;Label broadcastStatus,quietSituation,matchSignalTitle,matchSignalDetail;VisualElement matchSignal;
        Button broadcastMode,broadcastSpeed,goalReplaySkip,goalReplaySpeed,goalReplayOptions;bool? quietShown;int feedbackSeen;float feedbackUntil;bool feedbackGoal;
        Button[] goalReplayControls;
        readonly Label[] quietHome=new Label[6],quietAway=new Label[6],quietShapes=new Label[2],quietInsights=new Label[3],quietMoments=new Label[3];
        readonly Button[] quietPlayers=new Button[22];
        public static string BroadcastClock(float seconds,float secondsPerMinute=8){int total=Mathf.Clamp(Mathf.FloorToInt(seconds*60/Mathf.Max(.001f,secondsPerMinute)+.001f),0,5400);return (total/60).ToString("00")+":"+(total%60).ToString("00");}
        void BuildBroadcastUI(Image surface)
        {
            broadcastImage=surface;quietShown=null;
            var strip=new VisualElement{name="match-broadcast-strip"};strip.AddToClassList("broadcast-strip");strip.style.flexWrap=Wrap.Wrap;content.Insert(content.IndexOf(surface),strip);
            broadcastStatus=Text(strip,"","broadcast-status");
            broadcastMode=Button(strip,"",MatchViewingOptions);broadcastMode.name="match-broadcast-mode";broadcastMode.tooltip="Choisir les moments clés, temps forts, temps forts étendus ou le match complet";
            broadcastSpeed=Button(strip,"×"+arena.Speed,()=>{int index=Array.IndexOf(MatchArena.LiveSpeeds,arena.Speed);arena.SetLiveSpeed(MatchArena.LiveSpeeds[(index+1)%MatchArena.LiveSpeeds.Length]);});broadcastSpeed.name="match-speed-cycle";broadcastSpeed.tooltip="Vitesse des temps forts : ×1, ×2, ×3, ×5, ×10";
            goalReplaySkip=Button(strip,"Passer le replay",()=>{arena.SkipGoalReplay();RefreshBroadcastVisibility();});goalReplaySkip.name="match-replay-skip";goalReplaySkip.tooltip="Revenir au direct sans modifier le score ni avancer la rencontre";goalReplaySkip.style.minHeight=45;goalReplaySkip.style.display=DisplayStyle.None;goalReplaySkip.SetEnabled(false);
            goalReplaySpeed=Button(strip,"Replay ×1",()=>{arena.SetGoalReplaySpeed(arena.GoalReplaySpeed<1?1:.5f);RefreshBroadcastVisibility();});goalReplaySpeed.name="match-replay-speed";goalReplaySpeed.tooltip="Vitesse du replay uniquement : ×0,5 ou ×1";
            goalReplayOptions=Button(strip,"Options",MatchViewingOptions);goalReplayOptions.name="match-replay-options";goalReplayOptions.tooltip="Activer ou désactiver les replays automatiques";
            goalReplayControls=new[]{goalReplaySkip,goalReplaySpeed,goalReplayOptions};foreach(var replayControl in goalReplayControls){replayControl.style.minHeight=45;replayControl.style.minWidth=44;replayControl.style.flexShrink=0;replayControl.style.display=DisplayStyle.None;replayControl.SetEnabled(false);}
            matchSignal=new VisualElement{name="match-action-signal",pickingMode=PickingMode.Ignore};matchSignal.AddToClassList("match-action-signal");surface.Add(matchSignal);
            matchSignalTitle=Text(matchSignal,"","match-signal-title");matchSignalTitle.pickingMode=PickingMode.Ignore;matchSignalDetail=Text(matchSignal,"","match-signal-detail");matchSignalDetail.pickingMode=PickingMode.Ignore;
            matchSignal.style.display=DisplayStyle.None;feedbackSeen=arena.Simulation.State.events.Count;feedbackUntil=0;feedbackGoal=false;
            surface.schedule.Execute(RefreshMatchFeedback).Every(40);
            var dashboard=new ScrollView(ScrollViewMode.Vertical){name="match-interlude"};dashboard.AddToClassList("match-interlude");dashboard.horizontalScrollerVisibility=ScrollerVisibility.Hidden;content.Insert(content.IndexOf(surface)+1,dashboard);quietPanel=dashboard;
            quietSituation=Text(dashboard,"","interlude-situation");quietSituation.name="match-quiet-situation";
            var layout=Row(dashboard,"interlude-layout");var overview=Row(layout,"interlude-overview");var stats=Card(overview,"interlude-stats");
            Text(stats,"LA RENCONTRE EN CHIFFRES","eyebrow");
            string[] names={"Possession","Tirs / cadrés","Occasions · xG","Passes réussies","Corners","Récupérations hautes"};
            for(int i=0;i<names.Length;i++){var row=Row(stats,"interlude-stat");quietHome[i]=Text(row,"—","interlude-value");Text(row,names[i],"interlude-stat-name");quietAway[i]=Text(row,"—","interlude-value");}
            var insights=Card(overview,"interlude-insights");Text(insights,"DEPUIS LE BANC","eyebrow");
            for(int i=0;i<3;i++)quietInsights[i]=Text(insights,"","interlude-insight");
            Button(insights,"Consulter l’adjoint",()=>{matchAnalysisTab="Adjoint";MatchAnalysis();}).name="interlude-coach";
            var teams=Row(layout,"interlude-teams");
            var order=FixtureDisplayOrder(Career.match);
            for(int column=0;column<2;column++){
                int side=order.SideAt(column);
                var team=Card(teams,"interlude-team");team.name="interlude-team-"+side;
                var heading=Row(team,"interlude-team-heading");var club=side==0?Career.match.home:Career.match.away;
                var logo=Resources.Load<Texture2D>("Logos/club-"+club);if(logo!=null){var crest=new Image{image=logo,scaleMode=ScaleMode.ScaleToFit};crest.AddToClassList("interlude-crest");heading.Add(crest);}
                Text(heading,ClubName(club),"section-title");quietShapes[side]=Text(team,"","muted");
                for(int slot=0;slot<11;slot++){int index=side*11+slot;var b=Button(team,"",()=>{arena.Paused=true;PlayerProfile(Career.match.actors[index].id);});b.name="interlude-player-"+index;b.AddToClassList("interlude-player");quietPlayers[index]=b;}
            }
            var moments=Card(dashboard,"interlude-moments");Text(moments,"DERNIERS ÉVÉNEMENTS","eyebrow");for(int i=0;i<3;i++)quietMoments[i]=Text(moments,"","interlude-insight");
            RefreshBroadcastVisibility();RefreshBroadcastData();
        }
        void SetBroadcastMode(MatchViewingMode mode)
        {
            arena.Broadcast.SetMode(mode);PlayerPrefs.SetInt("match-view-mode",(int)arena.Broadcast.Mode);PlayerPrefs.SetInt("match-highlights",arena.Broadcast.Enabled?1:0);RefreshBroadcastVisibility();
        }
        void MatchViewingOptions()
        {
            bool replaying=arena.GoalReplayActive;if(!replaying)arena.Paused=true;var panel=Modal("Visionnage du match");panel.name="match-viewing-options";var body=Scroll(panel);
            Text(body,replaying?"Réglages du replay. La vitesse et la pause du direct sont conservées.":"Match en pause. Toutes les actions restent simulées, quel que soit le mode choisi.","muted");
            var replayCard=Card(body);
            Button replayToggle=null;
            replayToggle=Button(replayCard,"",()=>{arena.SetAutomaticGoalReplays(!arena.AutomaticGoalReplays);replayToggle.text=arena.AutomaticGoalReplays?"Replays automatiques : activés":"Replays automatiques : désactivés";replayToggle.EnableInClassList("primary",arena.AutomaticGoalReplays);RefreshBroadcastVisibility();});
            replayToggle.name="match-auto-goal-replays";replayToggle.style.minHeight=45;replayToggle.style.whiteSpace=WhiteSpace.Normal;
            replayToggle.text=arena.AutomaticGoalReplays?"Replays automatiques : activés":"Replays automatiques : désactivés";replayToggle.EnableInClassList("primary",arena.AutomaticGoalReplays);
            replayToggle.tooltip="Mémorisé pour les prochains matchs. Désactiver pendant un replay revient immédiatement au direct.";
            Text(replayCard,"Un replay après chaque but. Désactiver revient au direct si un replay est en cours.","muted");
            foreach(var mode in new[]{MatchViewingMode.KeyMoments,MatchViewingMode.Highlights,MatchViewingMode.Extended,MatchViewingMode.Full}){
                var card=Card(body);var choice=Button(card,MatchBroadcast.ModeName(mode),()=>{SetBroadcastMode(mode);CloseModal();});choice.name="match-view-"+mode;choice.EnableInClassList("primary",arena.Broadcast.Mode==mode);
                Text(card,MatchBroadcast.ModeDescription(mode),"muted");
            }
            Text(body,replaying?"Le bouton Passer le replay permet de revenir immédiatement au direct.":"Entre les séquences : statistiques, compositions et chrono accéléré. Après votre choix, utilisez Reprendre pour continuer.","footnote");
        }
        void UpdateMatchClock(MatchState m)
        {
            bool narrow=root.ClassListContains("narrow");
            clock.text=FixtureDisplayOrder(m).Score(m)+(narrow?"\n":"     ")+BroadcastClock(m.clock,m.SecondsPerMinute)+(narrow?"":m.halfTime?" · MI-TEMPS":m.finished?" · TERMINÉ":arena.Paused?" · PAUSE":"");
        }
        void RefreshBroadcastVisibility()
        {
            if(arena==null||quietPanel==null||broadcastImage?.panel==null)return;
            bool replaying=arena.GoalReplayActive;bool quiet=arena.QuietPresentation&&!replaying;
            if(quietShown!=quiet){quietShown=quiet;broadcastImage.style.display=quiet?DisplayStyle.None:DisplayStyle.Flex;quietPanel.style.display=quiet?DisplayStyle.Flex:DisplayStyle.None;if(quiet){RefreshBroadcastData();AnimateEntry(quietPanel);}}
            broadcastMode.text="Vue : "+(arena.Broadcast.Mode==MatchViewingMode.Extended?"Étendus":MatchBroadcast.ModeName(arena.Broadcast.Mode));
            foreach(var control in goalReplayControls)if(control!=null){control.style.display=replaying?DisplayStyle.Flex:DisplayStyle.None;control.SetEnabled(replaying);}
            if(goalReplaySpeed!=null)goalReplaySpeed.text=arena.GoalReplaySpeed<1?"Replay ×0,5":"Replay ×1";
            broadcastMode.style.display=replaying?DisplayStyle.None:DisplayStyle.Flex;broadcastSpeed.style.display=replaying?DisplayStyle.None:DisplayStyle.Flex;
            var state=arena.Simulation.State;
            string text=replaying?"REPLAY DU BUT · Séquence enregistrée":state.finished?"RENCONTRE TERMINÉE":state.halfTime?"MI-TEMPS · Ajustez vos consignes":quiet?(arena.Paused?"EN PAUSE · TABLEAU DE BORD":"SUIVI DU MATCH · CHRONO ×"+arena.Broadcast.QuietSpeed+(arena.Broadcast.BudgetLimited?" · charge adaptée":"")):arena.Paused?"EN PAUSE · "+arena.Broadcast.Reason:arena.Broadcast.Enabled?"EN DIRECT · "+arena.Broadcast.Reason+" · ×"+arena.Speed:"MATCH INTÉGRAL · ×"+arena.Speed;
            if(broadcastStatus.text!=text)broadcastStatus.text=text;
            if(broadcastSpeed!=null)broadcastSpeed.text="×"+arena.Speed;
            if(quietSituation!=null)quietSituation.text=QuietSituationText();
            broadcastStatus.EnableInClassList("broadcast-live",!quiet&&!arena.Paused&&!replaying);
        }
        void RefreshMatchFeedback()
        {
            if(arena==null||matchSignal?.panel==null||page!="Match")return;
            if(arena.GoalReplayActive){matchSignal.style.display=DisplayStyle.None;return;}
            var m=arena.Simulation.State;float now=Time.unscaledTime;
            if(feedbackSeen>m.events.Count)feedbackSeen=m.events.Count;
            while(feedbackSeen<m.events.Count){var e=m.events[feedbackSeen++];string title=MatchBroadcast.EventHeadline(e.kind);if(title==null)continue;
                bool goal=e.kind=="goal";if(feedbackGoal&&now<feedbackUntil&&!goal)continue;
                feedbackGoal=goal;feedbackUntil=now+(goal?5:2.1f);matchSignalTitle.text=title+(goal?"  "+FixtureDisplayOrder(m).Score(m):"");
                var player=string.IsNullOrEmpty(e.player)?null:Database.Find(e.player);string name=player==null?"":arena.PlayerSurname(player.id);
                matchSignalDetail.text=BroadcastClock(e.time,m.SecondsPerMinute)+" · "+(string.IsNullOrEmpty(name)?e.text:name+" · "+ClubName(e.side==0?m.home:m.away));matchSignal.EnableInClassList("match-signal-goal",goal);
            }
            bool shown=now<feedbackUntil&&!arena.QuietPresentation;matchSignal.style.display=shown?DisplayStyle.Flex:DisplayStyle.None;
            if(quietSituation!=null&&arena.QuietPresentation)quietSituation.text=QuietSituationText();
        }
        string QuietSituationText()
        {
            var m=arena.Simulation.State;if(m.finished||m.halfTime)return arena.Broadcast.QuietSituation;
            int side=m.restart>0?m.restartSide:MatchSimulation.PossessionSide(m);if(side<0)side=m.ball.side;
            return ClubName(side==0?m.home:m.away)+" · "+arena.Broadcast.QuietSituation;
        }
        void RefreshBroadcastData()
        {
            if(arena==null||quietHome[0]==null)return;var m=arena.Simulation.State;var a=m.metrics[0];var b=m.metrics[1];var order=FixtureDisplayOrder(m);float total=a.possessionSeconds+b.possessionSeconds;int share=total>0?Mathf.RoundToInt(m.metrics[order.HomeSide].possessionSeconds/total*100):50;
            quietHome[0].text=total>0?share+" %":"—";quietAway[0].text=total>0?(100-share)+" %":"—";
            for(int side=0;side<2;side++){
                var values=side==order.HomeSide?quietHome:quietAway;var stats=m.metrics[side];values[1].text=m.shots[side]+" / "+stats.shotsOnTarget;values[2].text=stats.xg.ToString("0.00",French);values[3].text=m.passes[side]>0?Mathf.RoundToInt(100f*m.completedPasses[side]/m.passes[side])+" %":"—";values[4].text=stats.corners.ToString();values[5].text=stats.highRecoveries.ToString();
                quietShapes[side].text=arena.Simulation.Tactic(side).formation+" · "+(11-CountSentOff(m,side))+" joueurs sur le terrain";
                for(int slot=0;slot<11;slot++){
                    int index=side*11+slot;var actor=m.actors[index];var p=Database.Find(actor.id);var role=arena.Simulation.Tactic(side).withoutBall[slot].role;
                    quietPlayers[index].text=FrenchFootballPositions.Short(role)+"  "+MatchPlayerLabels.Surname(p.name,p.nationality)+"\n"+(actor.sentOff?"EXCLU":Mathf.RoundToInt(actor.fitness)+" % de condition")+(actor.injured?" · BLESSÉ":actor.yellows>0?" · JAUNE":"");quietPlayers[index].tooltip=p.name;quietPlayers[index].EnableInClassList("interlude-alert",actor.injured||actor.sentOff);quietPlayers[index].EnableInClassList("interlude-tired",actor.fitness<65&&!actor.sentOff);
                }
            }
            Actor tired=null;foreach(var actor in m.actors)if(actor.side==0&&!actor.sentOff&&(tired==null||actor.fitness<tired.fitness))tired=actor;
            quietInsights[0].text=tired==null?"":arena.PlayerSurname(tired.id)+" · condition la plus basse : "+Mathf.RoundToInt(tired.fitness)+" %.";
            quietInsights[1].text=m.clock<10*m.SecondsPerMinute?"Premières minutes : encore peu de recul pour juger les tendances.":"Notre largeur : "+a.AverageWidth.ToString("0.0",French)+" m · passes moyennes : "+a.AveragePassLength(m.passes[0]).ToString("0.0",French)+" m.";
            quietInsights[2].text=m.pendingSubstitutions.Count>0?m.pendingSubstitutions.Count+" changement(s) en attente du prochain arrêt.":"Adversaire : "+m.awayTactic.formation+" · "+(m.awayPlan=="chase"?"prend davantage de risques.":m.awayPlan=="protect"?"protège son avance.":"organisation observée en direct.");
            int at=0;for(int i=m.events.Count-1;i>=0&&at<3;i--){var e=m.events[i];if(!SignificantMoment(e.kind))continue;quietMoments[at++].text=BroadcastClock(e.time,m.SecondsPerMinute)+" · "+e.text;}
            while(at<3)quietMoments[at++].text="";
        }
        static int CountSentOff(MatchState m,int side){int count=0;foreach(var actor in m.actors)if(actor.side==side&&actor.sentOff)count++;return count;}
        static string ShortPlayerName(string name)=>MatchPlayerLabels.Surname(name);
    }
}

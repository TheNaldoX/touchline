using System;
using System.Collections.Generic;
using Touchline.Core;

namespace Touchline.Analysis
{
    // Observes one completed physics tick at a time. Never changes state or draws randomness.
    public sealed class ThroughPassObserver
    {
        public static readonly string[] Labels={"Contrôles", "Hors-jeu", "Interceptions contrôlées", "Déviations adverses", "Contrôles manqués", "Sorties", "Ballons non maîtrisés", "Interruptions", "Vols inachevés"};
        public readonly int[,] Counts=new int[2,9];
        public readonly int[] Attempts=new int[2];
        public static readonly string[] FollowLabels={"Tir", "Nouvelle passe", "Possession adverse", "Ballon contesté", "Arrêt de jeu", "Sans action après 5s", "Fin de période"};
        public readonly int[,] FollowCounts=new int[2,7];
        public readonly int[] FarReceipts=new int[2],CoveredReceipts=new int[2];
        const float FollowSeconds=5,FarDistance=25; // seconds and metres from opposing goal
        sealed class Reception { public int side;public string player;public float time; }
        readonly List<Reception> receptions=new List<Reception>();
        int seenEvents,side,completedAtStart;
        bool pending;
        string passer;

        public void Sample(MatchState state)
        {
            for(int i=receptions.Count-1;i>=0;i--)if(Follow(state,receptions[i]))receptions.RemoveAt(i);
            if(pending){
                int outcome=-1;
                if(state.completedPasses[side]>completedAtStart)outcome=0;
                for(int i=seenEvents;i<state.events.Count&&outcome<0;i++){
                    var e=state.events[i];
                    if(e.kind=="offside"&&e.side==side)outcome=1;
                    else if((e.kind=="interception"||e.kind=="claim"||e.kind=="clearance")&&e.side!=side)
                        outcome=state.ball.owner!=null?2:3;
                    else if(e.kind=="miscontrol"&&e.side==side)outcome=4;
                }
                if(outcome<0&&state.ball.owner!=null){
                    foreach(var actor in state.actors)if(actor.id==state.ball.owner){outcome=actor.side==side?0:2;break;}
                }
                if(outcome<0&&(state.ball.kind!="through"||state.ball.from!=passer))
                    outcome=state.phase=="throw-in"||state.phase=="goal-kick"||state.phase=="corner"?5:state.restart>0?7:6;
                if(outcome<0&&(state.halfTime||state.finished))outcome=8;
                if(outcome>=0){
                    Counts[side,outcome]++;pending=false;
                    if(outcome==0){
                        var receipt=new Reception{side=side,player=state.ball.owner??state.ball.from,time=state.clock};
                        foreach(var actor in state.actors)if(actor.id==receipt.player){
                            int direction=(side==0?1:-1)*(state.period==2?-1:1);
                            if(Point.Distance(actor.position,new Point(52.5f*direction,0))>FarDistance)FarReceipts[side]++;
                            int cover=0;foreach(var defender in state.actors)if(!defender.sentOff&&defender.side!=side&&defender.slot>0&&(defender.position.x-actor.position.x)*direction>0)cover++;
                            if(cover>=2)CoveredReceipts[side]++;
                            break;
                        }
                        if(!Follow(state,receipt))receptions.Add(receipt);
                    }
                }
            }
            for(int i=seenEvents;i<state.events.Count;i++)if(state.events[i].kind=="through"){
                if(pending)throw new InvalidOperationException("Unresolved through pass before the next delivery.");
                var e=state.events[i];side=e.side;passer=e.player;completedAtStart=state.completedPasses[side];Attempts[side]++;pending=true;
            }
            seenEvents=state.events.Count;
        }
        bool Follow(MatchState state,Reception receipt)
        {
            int result=-1;
            for(int i=seenEvents;i<state.events.Count&&result<0;i++){
                var e=state.events[i];if(e.side!=receipt.side||e.player!=receipt.player)continue;
                if(e.kind=="shot")result=0;
                else if(e.kind=="pass"||e.kind=="through"||e.kind=="cross"||e.kind=="cutback"||e.kind=="switch"||e.kind=="clearance")result=1;
            }
            if(result<0&&(state.halfTime||state.finished))result=6;
            if(result<0&&state.restart>0)result=4;
            if(result<0&&state.ball.owner==null&&(state.ball.kind=="loose"||state.ball.kind=="none"))result=3;
            if(result<0&&state.ball.side!=receipt.side)result=2;
            if(result<0&&state.clock-receipt.time>=FollowSeconds)result=5;
            if(result<0)return false;
            FollowCounts[receipt.side,result]++;return true;
        }
    }
}

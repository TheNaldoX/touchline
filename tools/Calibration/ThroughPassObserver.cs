using System;
using Touchline.Core;

namespace Touchline.Analysis
{
    // Observes one completed physics tick at a time. Never changes state or draws randomness.
    public sealed class ThroughPassObserver
    {
        public static readonly string[] Labels={"Contrôles", "Hors-jeu", "Interceptions contrôlées", "Déviations adverses", "Contrôles manqués", "Sorties", "Ballons non maîtrisés", "Interruptions", "Vols inachevés"};
        public readonly int[,] Counts=new int[2,9];
        public readonly int[] Attempts=new int[2];
        int seenEvents,side,completedAtStart;
        bool pending;
        string passer;

        public void Sample(MatchState state)
        {
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
                if(outcome>=0){Counts[side,outcome]++;pending=false;}
            }
            for(int i=seenEvents;i<state.events.Count;i++)if(state.events[i].kind=="through"){
                if(pending)throw new InvalidOperationException("Unresolved through pass before the next delivery.");
                var e=state.events[i];side=e.side;passer=e.player;completedAtStart=state.completedPasses[side];Attempts[side]++;pending=true;
            }
            seenEvents=state.events.Count;
        }
    }
}

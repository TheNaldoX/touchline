using System;

namespace Touchline.Core
{
    public sealed partial class MatchSimulation
    {
        public const string AerialContest="aerial-contest";
        public const float AerialContestDuration=.64f,AerialContactWindow=.04f;
        readonly float[] aerialFractions=new float[22],aerialScores=new float[22];

        Actor SelectAerialDuel(out float contact,out Actor contestant)
        {
            float exit=ExitFraction(State.ball.previous,State.ball.position),earliest=exit;contact=exit;contestant=null;
            var actors=State.actors;
            for(int i=0;i<actors.Length;i++){
                float fraction=HeaderContactFraction(actors[i]);aerialFractions[i]=fraction;aerialScores[i]=float.NegativeInfinity;
                if(fraction<=1&&fraction<earliest)earliest=fraction;
            }
            if(earliest>=exit||earliest>1)return null;
            // Anchor the comparison window to the earliest physical reach.
            // Never move this deadline with a later, higher-scoring candidate.
            Actor winner=null;float best=float.NegativeInfinity;
            for(int i=0;i<actors.Length;i++){
                float f=aerialFractions[i];if(f>1||f>=exit||f>earliest+AerialContactWindow)continue;
                var p=actors[i];float score=Skill(p,"headingAccuracy")*.6f+Skill(p,"jumping")*.4f+Random()*12;aerialScores[i]=score;
                if(winner==null||score>best){winner=p;best=score;contact=f;}
            }
            float challengerScore=float.NegativeInfinity;
            for(int i=0;i<actors.Length;i++){
                var p=actors[i];if(p==winner||p.side==winner.side||aerialScores[i]<=challengerScore||Point.Distance(p.position,winner.position)>.95f)continue;
                contestant=p;challengerScore=aerialScores[i];
            }
            return winner;
        }

        void DistributeHeader(Actor player,Actor contestant)
        {
            var b=State.ball;var origin=b.position;float height=b.height,facing=player.angle;
            Actor receiver=null;float best=float.NegativeInfinity;int direction=Direction(player.side);
            foreach(var mate in State.actors){
                if(mate==player||mate.sentOff||mate.side!=player.side||GroundedAction(mate)||mate.controlTime>.3f||mate.position.x*direction>OffsideLine(player.side)-.2f)continue;
                float distance=Point.Distance(origin,mate.position);if(distance<2||distance>12)continue;
                float safety=Safety(player,mate.position);float space=Space(mate.position,1-player.side);if(safety<.45f||space<1.8f)continue;
                float score=safety*8+Math.Min(space,6)-distance*.25f+ShotQuality(mate)*8;
                if(score>best){best=score;receiver=mate;}
            }
            // A deliberate clearance aims away from either goal mouth. A
            // knock-down is a real pass to an available player, not an
            // uncounted attempt sent blindly through the goal line.
            var end=receiver!=null?receiver.position:new Point(Mathx.Clamp(origin.x+direction*10,-47,47),origin.z>=0?36:-36);
            float distanceToTarget=Point.Distance(origin,end);
            Flight(player,receiver,receiver!=null?"pass":"clearance",end,.11f,Math.Max(.35f,distanceToTarget/13),receiver!=null?.25f:1.6f);
            b.start=origin;b.startHeight=height;b.releaseDelay=.12f;b.elapsed=-.12f;
            player.action="header";player.angle=facing;player.actionTarget=origin;player.actionHeight=height;player.actionContactTime=.12f;
            BeginAerialContest(contestant,player,origin,height);
            if(receiver!=null){State.passes[player.side]++;State.metrics[player.side].passDistance+=distanceToTarget;State.metrics[player.side].forwardPassDistance+=(end.x-origin.x)*direction;}
            Emit("header",player.side,player.id,Data(player).name+(receiver!=null?" remet de la tête à "+Data(receiver).name+".":" dégage de la tête vers la touche."),receiver?.id);
        }

        void BeginAerialContest(Actor player,Actor winner,Point ballPoint,float height)
        {
            if(player==null)return;
            player.action=AerialContest;player.actionKind=AerialContest;player.actionSequence++;
            player.actionTime=AerialContestDuration;player.actionContactTime=winner.actionContactTime;
            player.actionTarget=ballPoint;player.actionHeight=height;
            player.controlTime=Math.Max(player.controlTime,AerialContestDuration);player.duelCooldown=Math.Max(player.duelCooldown,AerialContestDuration);
            player.velocity*=.15f;player.tackleOpponent=null;
        }

        bool AdvanceAerialAction(Actor player)
        {
            if((player.action!=AerialContest&&player.action!="header")||player.actionTime<=0)return false;
            // Plant, then jump in the space already occupied. No root warp
            // toward the ball and no second ball contact for the losing actor.
            player.velocity=new Point();player.actionTime=Math.Max(0,player.actionTime-Step);
            if(player.actionTime<=0){player.action="idle";player.actionKind="";}
            return true;
        }
    }
}

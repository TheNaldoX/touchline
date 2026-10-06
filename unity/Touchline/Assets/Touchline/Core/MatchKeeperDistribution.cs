using System;

namespace Touchline.Core
{
    public sealed partial class MatchSimulation
    {
        public const float KeeperDistributionDuration=1.08f,KeeperDistributionContact=.48f;
        public const string KeeperDistributionTurn="keeper-distribution-turn";
        public static bool HandDistribution(string kind)=>kind=="keeper-roll"||kind=="keeper-throw";
        public static Point KeeperDistributionLook(Actor keeper,Point fallback)
        {
            if(keeper.slot!=0)return fallback;
            if(keeper.action=="keeper-hold"&&keeper.actionKind==KeeperDistributionTurn)
                return keeper.actionTarget-keeper.position;
            if(!HandDistribution(keeper.action))return fallback;
            // The release point is .52 m ahead and .20 m to the right.
            // Recover that stable heading instead of chasing our own wind-up ball.
            var offset=keeper.actionTarget-keeper.position;
            if(Math.Abs(offset.Length-(float)Math.Sqrt(.52f*.52f+.20f*.20f))>.03f)return fallback;
            return new Point(offset.x*.52f+offset.z*.20f,offset.z*.52f-offset.x*.20f).Normalized;
        }
        bool BeginHandDistribution(Actor keeper)
        {
            if(!InOwnArea(keeper,keeper.position))return false;
            Actor receiver=null;Point target=new Point();string kind=null;float best=float.MinValue;
            var tactic=Tactic(keeper.side);int direction=Direction(keeper.side);
            foreach(var mate in State.actors){
                if(mate==keeper||mate.side!=keeper.side||mate.sentOff||mate.slot==0)continue;
                float distance=Point.Distance(keeper.position,mate.position),progress=(mate.position.x-keeper.position.x)*direction;
                if(distance<6||distance>38||progress<3||mate.position.x*direction>OffsideLine(keeper.side)-.3f)continue;
                string candidate=distance>20||tactic.counterAttack&&progress>16?"keeper-throw":"keeper-roll";
                var end=mate.position+mate.velocity*(distance/(candidate=="keeper-roll"?12:18)*.3f);
                end.x=Mathx.Clamp(end.x,-50,50);end.z=Mathx.Clamp(end.z,-32,32);
                float space=Space(end,1-keeper.side,true),safety=HandDistributionSafety(keeper,end,candidate=="keeper-throw");
                if(space<4||safety<.72f)continue;
                float score=safety*10+Math.Min(space,10)*.4f+progress*(tactic.counterAttack?.24f:.10f)-Math.Abs(distance-(10+tactic.directness*22))*.18f;
                if(score<=best)continue;best=score;receiver=mate;target=end;kind=candidate;
            }
            if(receiver==null)return false;
            float desired=(float)Math.Atan2(target.x-keeper.position.x,target.z-keeper.position.z);
            float turn=desired-keeper.angle;
            while(turn>Math.PI)turn-=(float)Math.PI*2;
            while(turn< -Math.PI)turn+=(float)Math.PI*2;
            if(Math.Abs(turn)>.12f){
                // Turn with the ball secured at the chest before starting the
                // arm action. Re-evaluate the recipient after the turn; a lost
                // option falls back to the existing foot distribution.
                if(keeper.actionKind==KeeperDistributionTurn)return false;
                keeper.action="keeper-hold";keeper.actionKind=KeeperDistributionTurn;
                keeper.actionTarget=target;keeper.actionTime=Mathx.Clamp(Math.Abs(turn)/5+.20f,.25f,.85f);
                keeper.actionSequence++;return true;
            }
            float d=Point.Distance(keeper.position,target),accuracy=Skill(keeper,"gkHandling")*.7f+Skill(keeper,"vision")*.3f;
            float error=(1-accuracy/105)*(Random()-.5f)*d*.045f;
            target+=new Point(error,-error);float speed=kind=="keeper-roll"?12:18;
            Flight(keeper,receiver,kind,target,BallRadius,Math.Max(.4f,d/speed),kind=="keeper-roll"?.035f:2.1f);
            var ball=State.ball;var forward=(target-keeper.position).Normalized;var right=new Point(forward.z,-forward.x);
            ball.fixedStart=true;ball.directThrow=true;ball.keeperDistribution=true;ball.start=keeper.position+forward*.52f-right*.20f;
            float height=Data(keeper).heightCm;float scale=height>=145&&height<=215?height/182:1;
            ball.startHeight=kind=="keeper-roll"?.24f:1.78f*scale;ball.releaseDelay=KeeperDistributionContact;ball.elapsed=-ball.releaseDelay;
            // This transition occurs during MoveActor, before UpdateBall consumes
            // this tick. Start both timelines at the same elapsed substep.
            keeper.action=kind;keeper.actionKind=kind;keeper.actionTime=KeeperDistributionDuration-Step;keeper.actionContactTime=ball.releaseDelay;keeper.actionTarget=ball.start;keeper.actionHeight=ball.startHeight;
            State.passes[keeper.side]++;var metrics=State.metrics[keeper.side];metrics.passDistance+=d;metrics.forwardPassDistance+=(target.x-keeper.position.x)*direction;if(d>27)metrics.longPasses++;
            Emit(kind,keeper.side,keeper.id,Data(keeper).name+(kind=="keeper-roll"?" relance à ras de terre vers ":" relance à la main vers ")+Data(receiver).name+".",receiver.id);
            return true;
        }
        float HandDistributionSafety(Actor keeper,Point end,bool high)
        {
            float d=Point.Distance(keeper.position,end),risk=0,speed=high?18:12;
            foreach(var opponent in State.actors){if(opponent.sentOff||opponent.side==keeper.side)continue;
                float u=Projection(keeper.position,end,opponent.position);if(u<.03f)continue;
                float height=high?1.78f+(BallRadius-1.78f)*u+(float)Math.Sin(Math.PI*u)*2.1f:BallRadius;
                if(height>2.3f)continue;
                float reaction=.30f+(100-Skill(opponent,"interceptions"))*.004f;
                float reach=.75f+Math.Max(0,d*u/speed-reaction)*(3+Skill(opponent,"acceleration")*.026f);
                float gap=Point.Distance(opponent.position,Point.Lerp(keeper.position,end,u));risk=Math.Max(risk,Mathx.Clamp((reach+.7f-gap)/(reach+.7f),0,1));
            }
            return 1-risk;
        }
        public static Point KeeperDistributionPreparation(BallState ball,float progress,out float height)
        {
            var forward=(ball.end-ball.start).Normalized;var right=new Point(forward.z,-forward.x);
            var wind=ball.start-forward*.66f-right*.05f;
            float windHeight=ball.kind=="keeper-roll"?.48f:ball.startHeight+.08f;
            if(progress<.42f){float t=progress/.42f;t=t*t*(3-2*t);height=ball.setupHeight+(windHeight-ball.setupHeight)*t;return Point.Lerp(ball.setupStart,wind,t);}
            float release=(progress-.42f)/.58f;release=release*release*(3-2*release);height=windHeight+(ball.startHeight-windHeight)*release;return Point.Lerp(wind,ball.start,release);
        }
    }
}

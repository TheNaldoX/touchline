using System;

namespace Touchline.Core
{
    public sealed partial class MatchSimulation
    {
        public const float KeeperHoldDuration=.35f,KeeperPlaceDuration=.65f,KeeperRiseDuration=.45f;
        bool FinishKeeperAction(Actor player)
        {
            var b=State.ball;if(player.slot!=0||b.owner!=player.id||!b.held)return false;
            var forward=new Point((float)Math.Sin(player.angle),(float)Math.Cos(player.angle));
            if(player.action=="claim"||player.action=="dive"){
                player.action="keeper-hold";player.actionKind=null;player.actionTime=KeeperHoldDuration;return true;
            }
            if(player.action=="keeper-hold"){
                if(BeginHandDistribution(player))return true;
                player.action="place-ball";player.actionKind=null;player.actionTime=KeeperPlaceDuration;player.actionTarget=player.position+forward*.42f;player.actionHeight=BallRadius;b.controlOrigin=b.position;b.setupHeight=b.height;return true;
            }
            if(player.action=="place-ball"){
                b.held=false;b.position=player.actionTarget;b.height=BallRadius;b.controlOrigin=b.position;b.controlElapsed=0;b.controlDuration=.12f;
                player.action="keeper-rise";player.actionTime=KeeperRiseDuration;player.controlTime=KeeperRiseDuration;State.decision=.2f;return true;
            }
            return false;
        }
        bool UpdateHeldBall(Actor player)
        {
            var b=State.ball;if(!b.held||player.slot!=0)return false;
            if(UpdateRecordedKeeperClaimBall(player,b)){
                // Preserve the accepted contact height during the gather.
            }else if(player.action=="place-ball"){
                float progress=Mathx.Clamp(1-player.actionTime/KeeperPlaceDuration,0,1);progress=progress*progress*(3-2*progress);
                b.position=Point.Lerp(b.controlOrigin,player.actionTarget,progress);b.height=b.setupHeight+(BallRadius-b.setupHeight)*progress;
            }else{b.position=player.position+new Point((float)Math.Sin(player.angle),(float)Math.Cos(player.angle))*.36f;b.height=1.1f;}
            b.velocity=new Point();b.verticalVelocity=0;return true;
        }
    }
}

using System;

namespace Touchline.Core
{
    public sealed partial class MatchSimulation
    {
        public const string RecordedKeeperClaim="keeper-claim-contact";
        public const float KeeperClaimDuration=.8f,KeeperClaimPresentationDuration=.92f,KeeperClaimGatherDuration=.55f;
        public static bool HasRecordedKeeperClaim(Actor keeper)=>keeper.action=="claim"&&keeper.actionKind==RecordedKeeperClaim&&keeper.actionSequence>0&&keeper.actionContactTime>0&&keeper.actionContactTime<=.121f&&keeper.actionHeight>=BallRadius;
        public static float KeeperClaimSecondsAfterContact(Actor keeper,float remaining)=>KeeperClaimPresentationDuration-remaining-keeper.actionContactTime;
        void BeginRecordedKeeperClaim(Actor keeper,Point impact,float height,float fraction)
        {
            keeper.action="claim";keeper.actionKind=RecordedKeeperClaim;keeper.actionTime=KeeperClaimDuration;
            keeper.actionTarget=impact;keeper.actionHeight=height;keeper.actionSequence++;
            // Preserve the existing 0.8 s action lifetime. The marker places
            // contact inside the already simulated tick, so interpolation
            // reaches the real impact at alpha=fraction rather than alpha=1.
            keeper.actionContactTime=KeeperClaimPresentationDuration-KeeperClaimDuration-Step*(1-Mathx.Clamp(fraction,0,1));
        }
        bool UpdateRecordedKeeperClaimBall(Actor keeper,BallState ball)
        {
            if(!HasRecordedKeeperClaim(keeper))return false;
            float progress=Mathx.Clamp(KeeperClaimSecondsAfterContact(keeper,keeper.actionTime)/KeeperClaimGatherDuration,0,1);
            progress=progress*progress*(3-2*progress);
            var hold=keeper.position+new Point((float)Math.Sin(keeper.angle),(float)Math.Cos(keeper.angle))*.36f;
            ball.position=Point.Lerp(keeper.actionTarget,hold,progress);ball.height=keeper.actionHeight+(1.1f-keeper.actionHeight)*progress;
            return true;
        }
    }
}

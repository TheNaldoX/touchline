using System;

namespace Touchline.Core
{
    public sealed partial class MatchSimulation
    {
        public static Point KeeperParryVelocity(Point incoming,Point keeper,Point impact,int direction,float handling,float variation)
        {
            float lateral=impact.z-keeper.z;int side=Math.Sign(Math.Abs(impact.z)>.2f?impact.z:lateral==0?1:lateral);
            float speed=Math.Min(incoming.Length,Mathx.Clamp(incoming.Length*.4f,3,14));
            if(Math.Abs(lateral)>.65f&&Math.Abs(impact.z)>1.35f&&incoming.x*direction<0){
                // A stretched hand glances the ball beyond the near post;
                // it cannot reverse every fast shot back into the pitch.
                float targetWidth=Math.Max(Math.Abs(impact.z)+.4f,4.0f+handling*.007f+(variation-.5f)*.5f);
                var target=new Point(-direction*52.8f,side*targetWidth);
                return (target-impact).Normalized*speed;
            }
            // A central block can absorb the shot and rebound into play.
            var rebound=new Point(direction*(2.5f+variation*2),side*(2+variation*3));
            return rebound.Normalized*speed;
        }
    }
}

using UnityEngine;
using Touchline.Core;
namespace Touchline
{
    public static class BallImpactPresentation
    {
        public static bool Current(BallImpactContact impact,MatchState state)=>impact.valid&&impact.clock==state.clock&&state.restart<=0;
        public static Vector3 Incoming(Point start,float startHeight,Point impact,float height,float fraction,float alpha,BallReleaseContact release,float clock)
        {
            var contact=new Vector3(impact.x,height,impact.z);
            if(alpha>=fraction)return contact;
            // A first-step collision ends the launch record, but its incoming
            // path remains valid up to the physical impact. Do not stretch the
            // preparation over the whole tick or show a pre-release rebound.
            if(release.valid&&release.clock==clock&&fraction>=release.fraction){
                var atImpact=BallReleasePresentation.PathPosition(release,fraction);
                if(Vector3.Distance(atImpact,contact)<.002f)return BallReleasePresentation.PathPosition(release,alpha);
            }
            return Vector3.Lerp(new Vector3(start.x,startHeight,start.z),contact,fraction>0?alpha/fraction:1);
        }
        public static Vector3 Position(BallImpactContact impact,MatchState state,float alpha,BallReleaseContact release=default)
        {
            alpha=Mathf.Clamp01(alpha);
            if(!Current(impact,state))return BallReleasePresentation.Position(release,state,alpha);
            return Incoming(impact.start,impact.startHeight,impact.impact,impact.height,impact.fraction,alpha,release,state.clock);
        }
    }
}

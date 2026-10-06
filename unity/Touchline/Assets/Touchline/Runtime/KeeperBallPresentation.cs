using UnityEngine;
using Touchline.Core;

namespace Touchline
{
    public static class KeeperBallPresentation
    {
        public static bool Current(KeeperPresentationContact contact,MatchState state)=>contact.valid&&contact.clock==state.clock&&state.restart<=0;
        public static Vector3 Position(KeeperPresentationContact contact,MatchState state,float alpha,Vector3? hands=null,BallReleaseContact release=default,BallImpactContact block=default)
        {
            alpha=Mathf.Clamp01(alpha);var b=state.ball;
            var ordinary=BallImpactPresentation.Position(block,state,alpha,release);
            if(!Current(contact,state))return hands??ordinary;
            var impact=new Vector3(contact.impact.x,contact.height,contact.impact.z);
            if(alpha<contact.fraction){
                return BallImpactPresentation.Incoming(contact.start,contact.startHeight,contact.impact,contact.height,contact.fraction,alpha,release,state.clock);
            }
            // The physics step ends at impact for a parry. Do not invent an
            // outgoing path; the following step supplies the actual rebound.
            if(!contact.caught||!hands.HasValue)return impact;
            // Join the grip continuously after contact, never before it.
            float settle=contact.fraction<1?Mathf.Clamp01((alpha-contact.fraction)/(1-contact.fraction)):0;
            return Vector3.Lerp(impact,hands.Value,settle);
        }
    }
}

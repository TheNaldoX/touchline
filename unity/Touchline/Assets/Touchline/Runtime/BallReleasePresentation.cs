using UnityEngine;
using Touchline.Core;

namespace Touchline
{
    public static class BallReleasePresentation
    {
        static Vector3 Position(Point p,float height)=>new Vector3(p.x,height,p.z);
        public static bool Current(BallReleaseContact contact,MatchState state)
        {
            var b=state.ball;
            // A block, interception or restart in this step remains authoritative.
            // Never render a flight that the simulation has already stopped.
            return contact.valid&&contact.clock==state.clock&&state.restart<=0&&b.owner==null&&b.kind==contact.kind&&b.from==contact.source&&Point.Distance(b.position,contact.end)<.00001f&&Mathf.Abs(b.height-contact.endHeight)<.00001f;
        }
        public static Vector3 Position(BallReleaseContact contact,MatchState state,float alpha)
        {
            alpha=Mathf.Clamp01(alpha);var b=state.ball;
            if(!Current(contact,state))return Vector3.Lerp(Position(b.previous,b.previousHeight),Position(b.position,b.height),alpha);
            return PathPosition(contact,alpha);
        }
        // The immutable incoming path remains usable until a physical impact,
        // even when that impact changes the state's ball kind in the same step.
        public static Vector3 PathPosition(BallReleaseContact contact,float alpha)
        {
            alpha=Mathf.Clamp01(alpha);
            if(alpha<=0)return Position(contact.previous,contact.previousHeight);
            if(alpha>=1)return Position(contact.end,contact.endHeight);
            if(alpha<contact.fraction)return Vector3.Lerp(Position(contact.previous,contact.previousHeight),Position(contact.release,contact.releaseHeight),alpha/contact.fraction);
            float u=Mathf.Clamp01((alpha-contact.fraction)*MatchSimulation.Step/Mathf.Max(.1f,contact.duration));
            var point=Point.Lerp(contact.release,contact.flightEnd,u);
            float height=contact.releaseHeight+(contact.flightEndHeight-contact.releaseHeight)*u+Mathf.Sin(Mathf.PI*u)*contact.loft;
            return Position(point,height);
        }
    }
}

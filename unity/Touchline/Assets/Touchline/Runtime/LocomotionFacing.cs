using UnityEngine;

namespace Touchline
{
    // Angular presentation only. Never moves a player or predicts a contact.
    // A bounded turn builds and releases angular momentum rather than rotating
    // most of a reversal in its first rendered frame.
    public sealed class LocomotionFacing
    {
        const float Acceleration=1800f,Substep=1f/240;
        bool initialized;
        public float Angle { get; private set; }
        public float Velocity { get; private set; }
        public static float MaximumRate(float speed)=>Mathf.Lerp(360,270,Mathf.Clamp01(speed/7));
        // Feasibility check for a presentation deadline only. Restore every
        // integrator field; this neither advances facing nor inspects game RNG.
        public bool CanReachHeading(float target,float speed,float seconds,float tolerance=3)
        {
            float angle=Angle,velocity=Velocity;bool ready=initialized;
            try{Sample(target,speed,Mathf.Max(0,seconds));return Mathf.Abs(Mathf.DeltaAngle(Angle,target))<=tolerance;}
            finally{Angle=angle;Velocity=velocity;initialized=ready;}
        }
        public float Sample(float target,float speed,float elapsed,bool reset=false)
        {
            if(float.IsNaN(target)||float.IsInfinity(target)||float.IsNaN(speed)||float.IsInfinity(speed)||float.IsNaN(elapsed)||float.IsInfinity(elapsed)||elapsed<0)return Angle;
            if(reset||!initialized){initialized=true;Angle=Mathf.Repeat(target,360);Velocity=0;return Angle;}
            float remaining=Mathf.Min(elapsed,1f),limit=MaximumRate(Mathf.Max(0,speed));
            while(remaining>.000001f){
                float step=Mathf.Min(Substep,remaining);remaining-=step;
                float error=Mathf.DeltaAngle(Angle,target);
                if(Mathf.Abs(error)<.02f&&Mathf.Abs(Velocity)<3){Angle=Mathf.Repeat(target,360);Velocity=0;continue;}
                float desired=Mathf.Sign(error)*Mathf.Min(limit,Mathf.Sqrt(2*Acceleration*Mathf.Abs(error)));
                float next=Mathf.MoveTowards(Velocity,desired,Acceleration*step);
                float distance=(Velocity+next)*.5f*step;
                if(Mathf.Sign(distance)==Mathf.Sign(error)&&Mathf.Abs(distance)>=Mathf.Abs(error)){Angle=Mathf.Repeat(target,360);Velocity=0;}
                else{Angle=Mathf.Repeat(Angle+distance,360);Velocity=next;}
            }
            return Angle;
        }
    }
}

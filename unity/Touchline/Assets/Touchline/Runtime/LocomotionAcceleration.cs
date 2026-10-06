using UnityEngine;

namespace Touchline
{
    // Presentation only: never modifies the authoritative actor or ball.
    public sealed class LocomotionAcceleration
    {
        bool initialized,clocked;float previousClock,previousSpeed,target,value;
        public float Value=>value;
        public float Target=>target;
        public float Sample(float speed,float elapsed,bool hasClock,float clock,bool reset=false)
        {
            if(float.IsNaN(speed)||float.IsInfinity(speed)||float.IsNaN(elapsed)||float.IsInfinity(elapsed)||elapsed<0||hasClock&&(float.IsNaN(clock)||float.IsInfinity(clock)))return value;
            speed=Mathf.Max(0,speed);
            // Teleports, substitutions, restored/replayed state and returns
            // from hidden highlights must not inherit another motion's lean.
            if(reset||!initialized||clocked!=hasClock||hasClock&&(clock<previousClock||clock-previousClock>1.01f)){
                initialized=true;clocked=hasClock;previousClock=clock;previousSpeed=speed;target=value=0;return value;
            }
            if(hasClock){
                float interval=clock-previousClock;
                if(interval>.00001f){target=Mathf.Clamp((speed-previousSpeed)/interval,-6,6);previousSpeed=speed;previousClock=clock;}
            }else if(elapsed>0){
                // Standalone pose previews provide per-frame actor samples.
                target=Mathf.Clamp((speed-previousSpeed)/elapsed,-6,6);previousSpeed=speed;
            }
            value=Mathf.Lerp(value,target,1-Mathf.Exp(-elapsed*9));return value;
        }
    }
}

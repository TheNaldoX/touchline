using System;

namespace Touchline.Core
{
    public sealed partial class MatchSimulation
    {
        float HeaderMaximumHeight(Actor player)
        {
            var data=Data(player);float stature=data.heightCm>=145&&data.heightCm<=215?data.heightCm*.01f:1.8f;
            return stature+.12f+Skill(player,"jumping")*.0023f;
        }
        // Limit a header to the head/torso reach represented by the animation.
        // Clip the swept ball segment to the player's vertical reach first so
        // a fast descending cross cannot skip the contact between two ticks.
        float HeaderContactFraction(Actor player)
        {
            if(player.sentOff||GroundedAction(player)||player.controlTime>0||player.actionTime>0&&
                (player.action=="header"||player.action=="dive"||player.action=="hurt"||player.action=="tackle"||player.action=="kick"||player.action=="claim"))return float.PositiveInfinity;
            var data=Data(player);float stature=data.heightCm>=145&&data.heightCm<=215?data.heightCm*.01f:1.8f;
            float low=stature-.18f,high=HeaderMaximumHeight(player);
            var b=State.ball;float dy=b.height-b.previousHeight,first=0,last=1;
            if(Math.Abs(dy)<.00001f){if(b.height<low||b.height>high)return float.PositiveInfinity;}
            else{
                float a=(low-b.previousHeight)/dy,c=(high-b.previousHeight)/dy;
                first=Math.Max(0,Math.Min(a,c));last=Math.Min(1,Math.Max(a,c));
                if(first>last)return float.PositiveInfinity;
            }
            // Reach follows the actual facing of the planted player. A ball
            // behind his head cannot be reached by turning toward its exit.
            var center=player.position+new Point((float)Math.Sin(player.angle),(float)Math.Cos(player.angle))*.25f;
            float fraction=ContactFraction(Point.Lerp(b.previous,b.position,first),Point.Lerp(b.previous,b.position,last),center,center,.25f);
            return fraction>1?float.PositiveInfinity:first+(last-first)*fraction;
        }
    }
}

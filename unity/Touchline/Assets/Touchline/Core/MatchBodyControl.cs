using System;

namespace Touchline.Core
{
    public sealed partial class MatchSimulation
    {
        public const string ChestControl="control-chest",ThighControl="control-thigh";
        public const float BodyControlContactTime=.08f;
        public static bool IsBodyControl(Actor actor)=>actor.action=="control"&&(actor.actionKind==ChestControl||actor.actionKind==ThighControl);
        public static float BodyControlDuration(string kind)=>kind==ChestControl?.60f:.48f;
        float ControlStature(Actor actor){int h=Data(actor).heightCm;return h>=145&&h<=215?h*.01f:1.8f;}

        // An aerial first touch needs a body within reach, unlike the wider
        // interception envelope used by an outstretched foot at ground level.
        float BodyReceptionFraction(Actor actor)
        {
            var b=State.ball;float stature=ControlStature(actor),low=.65f,high=stature*.84f;
            float first=0,last=1,dy=b.height-b.previousHeight;
            if(Math.Abs(dy)<.00001f){if(b.height<low||b.height>high)return float.PositiveInfinity;}
            else{float a=(low-b.previousHeight)/dy,c=(high-b.previousHeight)/dy;first=Math.Max(0,Math.Min(a,c));last=Math.Min(1,Math.Max(a,c));if(first>last)return float.PositiveInfinity;}
            var forward=new Point((float)Math.Sin(actor.angle),(float)Math.Cos(actor.angle));
            var center=actor.position+forward*.30f;
            float f=ContactFraction(Point.Lerp(b.previous,b.position,first),Point.Lerp(b.previous,b.position,last),center,center,.26f);
            return f>1?float.PositiveInfinity:first+(last-first)*f;
        }

        void PrepareBodyControl(Actor actor)
        {
            var b=State.ball;
            actor.actionKind="control-foot";actor.actionTarget=b.position;actor.actionHeight=b.height;actor.actionSequence++;
            if(actor.slot==0||b.height<=.65f||b.height>ControlStature(actor)*.84f)return;
            actor.actionKind=b.height>=ControlStature(actor)*.63f?ChestControl:ThighControl;
            float duration=BodyControlDuration(actor.actionKind);
            actor.actionTime=duration;actor.actionContactTime=BodyControlContactTime;actor.controlTime=Math.Max(actor.controlTime,duration);
            b.setupHeight=b.height;b.controlDuration=duration;
            // A cushioning touch cannot remain at sprint speed while the ball
            // drops from the torso to a controllable position at the feet.
            actor.velocity*=.25f;
        }

        public static float BodyControlProgress(Actor actor,BallState ball)
        {
            if(!IsBodyControl(actor))return Mathx.Clamp(ball.controlElapsed/Math.Max(.01f,ball.controlDuration),0,1);
            float t=Mathx.Clamp((ball.controlElapsed-BodyControlContactTime)/Math.Max(.01f,ball.controlDuration-BodyControlContactTime),0,1);
            return t*t*(3-2*t);
        }
        public static float ControlledBallHeight(Actor actor,BallState ball)=>IsBodyControl(actor)?ball.setupHeight+(BallRadius-ball.setupHeight)*BodyControlProgress(actor,ball):BallRadius;
    }
}

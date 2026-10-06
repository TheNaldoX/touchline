using System;
using Touchline.Core;

namespace Touchline
{
    public struct KeeperReadinessSample
    {
        public bool active;
        public float weight;
        public bool Same(KeeperReadinessSample other)=>active==other.active&&weight==other.weight;
    }

    // Presentation only. No trajectory result, contact reach or goalkeeper AI
    // is predicted here; specialised claims/dives keep their existing poses.
    public static class KeeperReadiness
    {
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        static bool Finite(Point value)=>Finite(value.x)&&Finite(value.z);
        static float Clamp01(float value)=>Math.Max(0,Math.Min(1,value));
        static float Smooth(float value){value=Clamp01(value);return value*value*(3-2*value);}
        public static KeeperReadinessSample Evaluate(MatchState state,Actor actor,float alpha,bool claimActive=false)
        {
            if(state==null||actor==null||state.ball==null||actor.slot!=0||actor.sentOff||actor.injured||actor.controlTime>0||claimActive||
                actor.action!="idle"&&actor.action!="run"||state.restart>0||state.phase!="play"||state.finished||state.halfTime)return default;
            var b=state.ball;
            if(b.owner==actor.id||b.held&&b.side==actor.side||!Finite(alpha)||!Finite(actor.previous)||!Finite(actor.position)||!Finite(b.previous)||!Finite(b.position))return default;
            alpha=Clamp01(alpha);
            var keeper=Point.Lerp(actor.previous,actor.position,alpha);
            var ball=Point.Lerp(b.previous,b.position,alpha);
            float distance=Point.Distance(ball,keeper);
            // Conservative artistic grading, not measured FIFA timings: stay
            // alert in our possession; crouch more as an opponent approaches.
            float proximity=1-Smooth((distance-6)/26);
            bool opponent=MatchSimulation.PossessionSide(state)!=actor.side;
            float weight=.25f+(opponent?.5f*proximity:0);
            if(opponent&&b.kind=="shot"&&Finite(b.start)&&Finite(b.end)&&Finite(b.elapsed)&&Finite(b.duration)&&b.duration>0){
                var travel=b.end-b.start;var toward=keeper-ball;
                // A shot aimed away from this keeper adds no strike readiness.
                if(Point.Dot(travel,toward)>0&&Point.Distance(b.end,keeper)<12)weight+=.25f*proximity;
            }
            return new KeeperReadinessSample{active=true,weight=Clamp01(weight)};
        }
    }

    // Render receives simulation delta, including x2/x10. An exponential
    // response handles the whole supplied interval without a frame-rate cap.
    public sealed class KeeperReadinessPresentation
    {
        bool initialized,clocked;float previousClock,value=1;
        public float Value=>value;
        public float Sample(KeeperReadinessSample target,float elapsed,bool hasClock,float clock,bool reset=false)
        {
            if(!target.active){
                // Specialised actions retain the authored (unmodulated) layer.
                // Remember that displayed weight so ordinary re-entry can
                // blend from it instead of snapping to a new threat target.
                value=1;
                if(!hasClock||!float.IsNaN(clock)&&!float.IsInfinity(clock)){
                    initialized=true;clocked=hasClock;previousClock=clock;
                }else initialized=false;
                return value;
            }
            if(float.IsNaN(target.weight)||float.IsInfinity(target.weight)||float.IsNaN(elapsed)||float.IsInfinity(elapsed)||elapsed<0||
                hasClock&&(float.IsNaN(clock)||float.IsInfinity(clock)))return value;
            float desired=Math.Max(0,Math.Min(1,target.weight));
            if(reset||!initialized||clocked!=hasClock||hasClock&&(clock<previousClock||clock-previousClock>1.01f)){
                initialized=true;clocked=hasClock;previousClock=clock;value=desired;return value;
            }
            if(hasClock)previousClock=clock;
            if(elapsed>0)value=desired+(value-desired)*(float)Math.Exp(-elapsed*(desired>value?12:7));
            return value;
        }
    }
}

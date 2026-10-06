using UnityEngine;
using Touchline.Core;

namespace Touchline
{
    public struct KeeperClaimAnticipationSample
    {
        public bool active;public Point target;public float height,remaining,weight;
        public bool Same(KeeperClaimAnticipationSample other)=>active==other.active&&target.x==other.target.x&&target.z==other.target.z&&height==other.height&&remaining==other.remaining&&weight==other.weight;
    }
    // A visual readiness estimate from the current public trajectory, never
    // an interception result. It cannot change the ball, AI or save radius.
    public static class KeeperClaimAnticipation
    {
        public static KeeperClaimAnticipationSample Evaluate(MatchState state,Actor actor,float alpha)
        {
            var b=state.ball;
            if(actor.slot!=0||actor.sentOff||actor.controlTime>0||state.restart>0||b==null||b.owner!=null||b.side==actor.side||actor.action!="idle"&&actor.action!="run")return default;
            if(b.kind!="pass"&&b.kind!="cross"&&b.kind!="through"&&b.kind!="cutback"&&b.kind!="clearance")return default;
            if(b.duration<=0||b.elapsed<0)return default;
            float elapsed=b.elapsed-(1-Mathf.Clamp01(alpha))*MatchSimulation.Step;
            if(elapsed<0||elapsed>b.duration)return default;
            var ball=Point.Lerp(b.start,b.end,Mathf.Clamp01(elapsed/b.duration));
            var keeper=Point.Lerp(actor.previous,actor.position,Mathf.Clamp01(alpha));
            var velocity=(b.end-b.start)/b.duration;var relative=ball-keeper;var closing=velocity-actor.velocity;
            float c=Point.Dot(relative,relative)-1.05f*1.05f,time=0;
            if(c>0){
                float a=Point.Dot(closing,closing),linear=2*Point.Dot(relative,closing),discriminant=linear*linear-4*a*c;
                if(a<.001f||linear>=0||discriminant<0)return default;
                time=(-linear-Mathf.Sqrt(discriminant))/(2*a);
            }
            if(time<0||time>.35f||elapsed+time>b.duration)return default;
            var point=ball+velocity*time;float height=Height(b,elapsed+time);
            int direction=(actor.side==0?1:-1)*(state.period==2?-1:1);
            if(point.x*direction>=-36||Mathf.Abs(point.z)>=20.16f||height<.14f||height>2.3f)return default;
            var segment=point-ball;float length=Point.Dot(segment,segment);
            if(state.actors!=null&&length>.01f)foreach(var other in state.actors){
                if(other==actor||other.sentOff||other.id==b.from)continue;
                float u=Point.Dot(other.position-ball,segment)/length;
                if(u<=.03f||u>=.97f)continue;
                // Do not prepare a claim through an intervening player. This
                // conservative test does not predict who will win that duel.
                if(Height(b,elapsed+time*u)<=2.3f&&Point.Distance(other.position,ball+segment*u)<.85f)return default;
            }
            return new KeeperClaimAnticipationSample{active=true,target=point,height=height,remaining=time,weight=Mathf.SmoothStep(0,1,1-time/.35f)};
        }
        static float Height(BallState b,float elapsed)
        {
            float t=Mathf.Clamp01(elapsed/b.duration);return b.startHeight+(b.endHeight-b.startHeight)*t+Mathf.Sin(Mathf.PI*t)*b.loft;
        }
    }
}

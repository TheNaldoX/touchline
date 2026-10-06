using System;
using UnityEngine;
using Touchline.Core;
namespace Touchline
{
    // An observable incoming flight, not a prediction of possession or success.
    public struct BodyReceptionAnticipationSample
    {
        public bool active; public float eta,height; public Point point; public string kind,source;
        public Point flightStart;
        public bool Same(BodyReceptionAnticipationSample other)=>active==other.active&&eta==other.eta&&height==other.height&&point.x==other.point.x&&point.z==other.point.z&&kind==other.kind&&source==other.source&&flightStart.x==other.flightStart.x&&flightStart.z==other.flightStart.z;
        public static BodyReceptionAnticipationSample From(MatchState match,Actor actor,float alpha,float stature=1.8f)
        {
            var b=match.ball;
            if(MatchSimulation.IsBodyControl(actor)&&b.owner==actor.id)
                return new BodyReceptionAnticipationSample{kind=actor.actionKind,source=b.from,flightStart=b.start,point=actor.actionTarget,height=actor.actionHeight};
            if(actor.slot==0||actor.sentOff||actor.injured||match.restart>0||match.phase!="play"||
               (actor.action!="run"&&actor.action!="idle")||!string.IsNullOrEmpty(b.owner)||b.held||
               b.to!=actor.id||b.side!=actor.side||b.elapsed<0||b.duration<=0||b.goalAttempt||
               !(b.kind=="pass"||b.kind=="through"||b.kind=="switch"||b.kind=="cross"||b.kind=="cutback"||b.kind=="keeper-throw"))return default;
            // Never replace a contested duel with an anticipatory cushion.
            if(match.actors!=null)foreach(var other in match.actors)if(other!=actor&&!other.sentOff&&other.side!=actor.side&&Point.Distance(other.position,actor.position)<1.35f)return default;
            stature=stature>=1.45f&&stature<=2.15f?stature:1.8f;
            float elapsed=Mathf.Max(0,b.elapsed-(1-Mathf.Clamp01(alpha))*MatchSimulation.Step);
            var current=Point.Lerp(actor.previous,actor.position,Mathf.Clamp01(alpha));
            var forward=new Point(Mathf.Sin(actor.angle),Mathf.Cos(actor.angle));
            // Sweep the observed flight rather than checking isolated points.
            // At20m/s a25ms interval spans50cm, wider than a grazing body's
            // contact chord. Point probes could alternate ready/not-ready while
            // exactly the same incoming ball remained on a collision course.
            float maxEta=Mathf.Min(.35f,Mathf.Max(0,b.duration-elapsed));
            if(maxEta<=0)return default;
            Point previous=Point.Lerp(b.start,b.end,Mathf.Clamp01(elapsed/Mathf.Max(.1f,b.duration)));
            float previousHeight=FlightHeight(b,elapsed),previousEta=0;
            for(int i=1;i<=14;i++){
                float eta=Mathf.Min(i*.025f,maxEta),u=Mathf.Clamp01((elapsed+eta)/Mathf.Max(.1f,b.duration));
                var next=Point.Lerp(b.start,b.end,u);float height=FlightHeight(b,elapsed+eta);
                float first=0,last=1,dy=height-previousHeight,high=stature*.84f;
                if(Mathf.Abs(dy)<.000001f){if(previousHeight<=.65f||previousHeight>high){if(eta>=maxEta)break;previous=next;previousHeight=height;previousEta=eta;continue;}}
                else{float a=(.65f-previousHeight)/dy,c=(high-previousHeight)/dy;first=Mathf.Max(0,Mathf.Min(a,c));last=Mathf.Min(1,Mathf.Max(a,c));}
                if(first<=last){
                    float span=eta-previousEta;
                    var playerFrom=current+actor.velocity*(previousEta+span*first)+forward*.30f;
                    var playerTo=current+actor.velocity*(previousEta+span*last)+forward*.30f;
                    float fraction=MatchSimulation.ContactFraction(Point.Lerp(previous,next,first),Point.Lerp(previous,next,last),playerFrom,playerTo,.26f);
                    if(fraction<=1){
                        float segment=first+(last-first)*fraction,arrival=previousEta+span*segment;
                        float y=FlightHeight(b,elapsed+arrival);
                        if(y>.65f&&y<=high)return new BodyReceptionAnticipationSample{active=true,eta=arrival,height=y,point=Point.Lerp(previous,next,segment),kind=y>=stature*.63f?MatchSimulation.ChestControl:MatchSimulation.ThighControl,source=b.from,flightStart=b.start};
                    }
                }
                previous=next;previousHeight=height;previousEta=eta;if(eta>=maxEta)break;
            }
            return default;
        }
        static float FlightHeight(BallState ball,float elapsed){float u=Mathf.Clamp01(elapsed/Mathf.Max(.1f,ball.duration));return ball.startHeight+(ball.endHeight-ball.startHeight)*u+Mathf.Sin(Mathf.PI*u)*ball.loft;}
    }
}

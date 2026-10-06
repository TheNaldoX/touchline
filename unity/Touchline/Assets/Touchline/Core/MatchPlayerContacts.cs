using System;

namespace Touchline.Core
{
    public sealed partial class MatchSimulation
    {
        // Upright bodies occupy space even when their movement targets diverge.
        // Swept contacts also catch two runners exchanging places in one tick.
        public const float BodySeparation=.56f;
        public void ResolvePlayerContacts()
        {
            var actors=State.actors;
            for(int iteration=0;iteration<4;iteration++)
                for(int i=0;i<actors.Length;i++)for(int j=i+1;j<actors.Length;j++){
                    var a=actors[i];var b=actors[j];if(a.sentOff||b.sentOff)continue;
                    var delta=a.position-b.position;float distance=delta.Length;
                    Point normal=distance>.0001f?delta/distance:new Point((i+j)%2==0?1:-1,0);
                    float correction=BodySeparation-distance;
                    if(iteration==0){
                        var start=a.previous-b.previous;var travel=delta-start;float speed2=Point.Dot(travel,travel);
                        float c=Point.Dot(start,start)-BodySeparation*BodySeparation;
                        float approach=Point.Dot(start,travel);float discriminant=approach*approach-speed2*c;
                        if(c>0&&speed2>.000001f&&approach<0&&discriminant>=0){
                            float time=(-approach-(float)Math.Sqrt(discriminant))/speed2;
                            if(time>=0&&time<=1){normal=(start+travel*time).Normalized;correction=BodySeparation-Point.Dot(delta,normal);}
                        }
                    }
                    if(correction<=0)continue;
                    if(iteration==0)ConsiderContactFoul(a,b,Point.Dot(a.velocity-b.velocity,normal));
                    float wa=ContactMobility(a),wb=ContactMobility(b),sum=wa+wb;
                    if(sum<=0)continue;
                    wa/=sum;wb/=sum;
                    a.position=ContactBounds(a.position+normal*(correction*wa));
                    b.position=ContactBounds(b.position-normal*(correction*wb));
                    float closing=Point.Dot(a.velocity-b.velocity,normal);
                    if(closing<0){a.velocity-=normal*(closing*wa);b.velocity+=normal*(closing*wb);}
                }
        }
        float ContactMobility(Actor p)
        {
            if(GroundedAction(p)||p.action==AerialContest)return 0;
            // Do not slide a planted strike, dive or prepared tackle away from
            // its contact point. The moving player must yield to that body.
            if(p.actionTime>0&&(p.action=="kick"||p.action=="header"||p.action=="dive"||p.action=="claim"||p.action=="tackle"||p.action=="throw"||p.action=="keeper-hold"||p.action=="keeper-rise"||p.action=="place-ball"||HandDistribution(p.action)))return 0;
            return 1/(.8f+Skill(p,"strength")*.004f);
        }
        static Point ContactBounds(Point p)=>new Point(Mathx.Clamp(p.x,-52.5f,52.5f),Mathx.Clamp(p.z,-33.8f,33.8f));
    }
}

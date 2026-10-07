using System;

namespace Touchline.Core
{
    public sealed partial class MatchSimulation
    {
        // Move has already advanced the pose to the end of this tick. Sweep
        // against reach at contact time, not that future end-of-tick pose.
        // Arm extension (m) a full dive adds to the .55 m standing reach: about
        // 1.2 m for an average keeper (gkDiving 80), i.e. a 1.75 m total reach.
        const float KeeperDiveExtensionBase=.40f,KeeperDiveExtensionPerPoint=.010f;
        float KeeperShotContactFraction(Actor keeper,BallState shot)
        {
            float reaction=.14f+(100-Skill(keeper,"gkReflexes"))*.002f;
            float reactionFraction=(reaction-(shot.elapsed-Step))/Step;
            float first=0;
            if(reactionFraction>0){
                float end=Math.Min(1,reactionFraction);
                float contact=KeeperReachInterval(keeper,shot,0,end,.42f,.42f);
                if(contact<=1)return contact;
                if(end>=1)return 2;
                first=end;
            }
            if(keeper.action!="dive"){
                float reach=.55f+Skill(keeper,"gkDiving")*.0035f;
                return KeeperReachInterval(keeper,shot,first,1,reach,reach);
            }
            float poseStart=1.2f-keeper.actionTime-Step;
            float startFraction=-poseStart/Step,fullFraction=(.28f-poseStart)/Step;
            float extension=KeeperDiveExtensionBase+Skill(keeper,"gkDiving")*KeeperDiveExtensionPerPoint;
            while(first<1){
                float last=1;
                if(startFraction>first)last=Math.Min(last,startFraction);
                if(fullFraction>first)last=Math.Min(last,fullFraction);
                float a=.55f+extension*Mathx.Clamp((poseStart+Step*first)/.28f,0,1);
                float b=.55f+extension*Mathx.Clamp((poseStart+Step*last)/.28f,0,1);
                float contact=KeeperReachInterval(keeper,shot,first,last,a,b);
                if(contact<=1)return contact;
                first=last;
            }
            return 2;
        }

        static float KeeperReachInterval(Actor keeper,BallState shot,float first,float last,float radiusFrom,float radiusTo)
        {
            var from=Point.Lerp(shot.previous,shot.position,first)-Point.Lerp(keeper.previous,keeper.position,first);
            var to=Point.Lerp(shot.previous,shot.position,last)-Point.Lerp(keeper.previous,keeper.position,last);
            var movement=to-from;
            // Relative distance squared minus linearly expanding reach squared.
            // Double precision keeps nearly tangent/linear cases stable.
            double growth=radiusTo-radiusFrom;
            double a=Point.Dot(movement,movement)-growth*growth;
            double b=2*(Point.Dot(from,movement)-radiusFrom*growth);
            double c=Point.Dot(from,from)-(double)radiusFrom*radiusFrom;
            if(c<=0)return first;
            double t=2;
            if(Math.Abs(a)<1e-10){if(b<0)t=-c/b;}
            else{
                double discriminant=b*b-4*a*c;
                if(discriminant>=0){
                    double root=Math.Sqrt(discriminant);
                    double q=-.5*(b+(b>=0?root:-root));
                    double u=q/a,v=Math.Abs(q)>1e-15?c/q:2;
                    if(u>=0&&u<=1)t=u;
                    if(v>=0&&v<=1)t=Math.Min(t,v);
                }
            }
            return t>=0&&t<=1?first+(last-first)*(float)t:2;
        }
    }
}

using System;

namespace Touchline.Core
{
    public sealed partial class MatchSimulation
    {
        Point AnticipateKeeperShot(Actor keeper,Point formationTarget)
        {
            var ball=State.ball;
            float reaction=.14f+(100-Skill(keeper,"gkReflexes"))*.002f;
            float dx=ball.end.x-ball.start.x;
            if(ball.kind!="shot"||ball.side==keeper.side||ball.elapsed<=reaction||Math.Abs(dx)<.0001f)return formationTarget;
            // Intercept where the keeper physically stands. Using the shape
            // target (which retreats with the ball) delayed launch until after
            // the shot had already crossed the keeper's real depth.
            float fraction=Mathx.Clamp((keeper.position.x-ball.start.x)/dx,0,1);
            var intercept=new Point(keeper.position.x,Mathx.Clamp(ball.start.z+(ball.end.z-ball.start.z)*fraction,-4,4));
            float remaining=fraction*ball.duration-ball.elapsed,lateral=intercept.z-keeper.position.z;
            if(remaining>0&&remaining<=.35f&&Math.Abs(lateral)>.45f&&keeper.action!="dive"){
                keeper.diveSide=lateral*-(float)Math.Sin(keeper.angle)<0?-1:1;
                keeper.action="dive";keeper.actionTime=1.2f;keeper.actionContactTime=Math.Max(.04f,remaining);
                keeper.actionKind="save-attempt";keeper.actionTarget=intercept;
                keeper.actionHeight=ball.startHeight+(ball.endHeight-ball.startHeight)*fraction+(float)Math.Sin(Math.PI*fraction)*ball.loft;
                keeper.actionSequence++;
                // Existing normalized captures translate the pelvis roughly
                // .52-.84m in .30s. This moves the authoritative root; the
                // rendering then needs less local reach, not longer arms.
                float speed=Math.Min(2.7f,Math.Max(0,(Math.Abs(lateral)-1.05f)/Math.Max(.04f,remaining)));
                keeper.velocity=new Point(0,Math.Sign(lateral)*speed);keeper.keeperLaunchRemaining=remaining;
            }
            formationTarget.z=intercept.z;return formationTarget;
        }

        bool AdvanceKeeperLaunch(Actor keeper)
        {
            if(keeper.slot!=0||keeper.action!="dive"||keeper.keeperLaunchRemaining<=0)return false;
            float travel=Math.Min(Step,keeper.keeperLaunchRemaining);
            keeper.position=ContactBounds(keeper.position+keeper.velocity*travel);
            keeper.keeperLaunchRemaining=Math.Max(0,keeper.keeperLaunchRemaining-Step);
            keeper.actionTime=Math.Max(0,keeper.actionTime-Step);
            if(keeper.keeperLaunchRemaining<=0)keeper.velocity=new Point();
            return true;
        }
    }
}

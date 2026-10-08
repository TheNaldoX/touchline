using System;

namespace Touchline.Core
{
    public sealed partial class MatchSimulation
    {
        public static Point ThrowPreparation(BallState ball,float progress,out float height)
        {
            // Pickup, draw behind the head, then release over the head. This
            // is also the rendered ball path, rather than a detached hand prop.
            progress=Mathx.Clamp(progress,0,1);
            var behind=ball.start-(ball.end-ball.start).Normalized*.55f;
            float peak=ball.startHeight+.14f;
            if(progress<.55f){float t=progress/.55f;t=t*t*(3-2*t);height=ball.setupHeight+(peak-ball.setupHeight)*t;return Point.Lerp(ball.setupStart,ball.start,t);}
            if(progress<.75f){float t=(progress-.55f)/.2f;t=t*t*(3-2*t);height=peak;return Point.Lerp(ball.start,behind,t);}
            float release=(progress-.75f)/.25f;release=release*release*(3-2*release);height=peak+(ball.startHeight-peak)*release;return Point.Lerp(behind,ball.start,release);
        }
        public const float BallRadius=.11f;
        const float GoalExit=52.5f+BallRadius,TouchExit=34+BallRadius;

        // First collision along the swept ball/player segment. Using relative
        // motion avoids selecting a defender who arrives after the ball passed.
        public static float ContactFraction(Point ballFrom,Point ballTo,Point playerFrom,Point playerTo,float radius)
        {
            var offset=ballFrom-playerFrom;var movement=(ballTo-ballFrom)-(playerTo-playerFrom);
            float c=Point.Dot(offset,offset)-radius*radius;if(c<=0)return 0;
            float a=Point.Dot(movement,movement);if(a<.000001f)return 2;
            float b=2*Point.Dot(offset,movement),d=b*b-4*a*c;if(d<0)return 2;
            float t=(-b-(float)Math.Sqrt(d))/(2*a);return t>=0&&t<=1?t:2;
        }
        static float ExitFraction(Point from,Point to)
        {
            float f=2;if(Math.Abs(to.x)>GoalExit&&to.x!=from.x)f=Math.Min(f,(Math.Sign(to.x)*GoalExit-from.x)/(to.x-from.x));if(Math.Abs(to.z)>TouchExit&&to.z!=from.z)f=Math.Min(f,(Math.Sign(to.z)*TouchExit-from.z)/(to.z-from.z));return Math.Max(0,f);
        }
        // Deceleration (m/s²) of a ball rolling on cut grass (rolling resistance ~0,1 g).
        public const float GroundRollDeceleration=.95f;
        void RollBall()
        {
            var b=State.ball;b.position+=b.velocity*Step;
            if(b.height>BallRadius+.002f||b.verticalVelocity>0){b.verticalVelocity-=9.81f*Step;b.height+=b.verticalVelocity*Step;if(b.height<BallRadius){b.height=BallRadius;b.verticalVelocity=-b.verticalVelocity*.38f;if(b.verticalVelocity<.45f)b.verticalVelocity=0;b.velocity=b.velocity*.83f;}}
            else{b.height=BallRadius;b.verticalVelocity=0;}
            float drag=b.height>BallRadius+.01f?.04f:GroundRollDeceleration;float speed=b.velocity.Length;b.velocity=speed>drag*Step?b.velocity.Normalized*(speed-drag*Step):new Point();
        }
        // A contested touch rarely sends the ball exactly where it was going:
        // turn a direction by up to +/- maxRadians, uniformly.
        Point Deflect(Point direction,float maxRadians)
        {
            float a=(Random()*2-1)*maxRadians,c=(float)Math.Cos(a),s=(float)Math.Sin(a);
            return new Point(direction.x*c-direction.z*s,direction.x*s+direction.z*c);
        }
        public const float PokeSpread=.7f,HeavyTouchSpread=.6f; // radians (~40 deg, ~35 deg)
        void LooseBall(Point position,Point velocity,float height,float vertical,int side,string player=null)
        {
            State.ball.held=false;State.ball.keeperDistribution=false;
            var b=State.ball;b.owner=null;b.kind="loose";b.to=null;b.offside=false;b.passEligible=false;b.directThrow=false;b.position=position;b.height=Math.Max(BallRadius,height);b.velocity=velocity;b.verticalVelocity=vertical;b.side=side;b.lastTouch=side;b.lastTouchId=player;b.elapsed=0;
        }
        bool GoalFrame()
        {
            var b=State.ball;bool firstFlight=CurrentFirstReleasedFlight(out var release);var from=firstFlight?release.release:b.previous;if(Math.Abs(b.position.x)<52.5f||Math.Abs(from.x)>=52.5f)return false;
            int sign=Math.Sign(b.position.x);float f=(sign*52.5f-from.x)/(b.position.x-from.x);float global=firstFlight?ReleaseSweepFraction(release.fraction,f):f;float z=from.z+(b.position.z-from.z)*f,y=firstFlight?ReleaseSweepHeight(release,global):b.previousHeight+(b.height-b.previousHeight)*f;
            if(Math.Abs(Math.Abs(z)-3.66f)<.17f&&y<2.58f||Math.Abs(y-2.44f)<.15f&&Math.Abs(z)<3.8f){
                bool direct=b.directThrow,keeper=b.keeperDistribution;var v=firstFlight?b.velocity:(b.position-b.previous)/Step;float vertical=firstFlight?ReleasedVerticalVelocity(release,global):b.verticalVelocity;LooseBall(new Point(sign*52.3f,z),new Point(-v.x*.48f,v.z*.7f+(z<0?-1:1)*1.5f),y,Math.Max(.3f,Math.Abs(vertical)*.35f),b.lastTouch,b.lastTouchId);b.directThrow=direct;b.keeperDistribution=keeper;Emit("woodwork",b.side,b.from,"Le ballon revient dans le jeu après avoir heurté le montant !");return true;}
            return false;
        }
        // Interception deflections: ball speed (m/s) above which a stretched
        // touch can deflect, reach (m) still controlled cleanly, extra reach (m)
        // at which a deflection is certain for a poor reader, deflection cone
        // and the share of speed the ball keeps.
        const float InterceptDeflectSpeed=8f,InterceptCleanReach=.6f,InterceptStretchRange=.9f,InterceptDeflectSpread=.8f,InterceptDeflectKeep=.55f;
        bool BallLeavesPitch()
        {
            var b=State.ball;bool firstFlight=CurrentFirstReleasedFlight(out var release);var from=firstFlight?release.release:b.previous;float fx=2,fz=2;if(Math.Abs(b.position.x)>GoalExit)fx=b.position.x==from.x?0:Mathx.Clamp((Math.Sign(b.position.x)*GoalExit-from.x)/(b.position.x-from.x),0,1);if(Math.Abs(b.position.z)>TouchExit)fz=b.position.z==from.z?0:Mathx.Clamp((Math.Sign(b.position.z)*TouchExit-from.z)/(b.position.z-from.z),0,1);
            if(fx>1&&fz>1)return false;
            if(fz<fx){int side=1-b.lastTouch;State.metrics[side].throwIns++;
                float global=firstFlight?ReleaseSweepFraction(release.fraction,fz):fz;
                var impact=Point.Lerp(from,b.position,fz);float height=firstFlight?ReleaseSweepHeight(release,global):b.previousHeight+(b.height-b.previousHeight)*fz;
                var spot=new Point(Mathx.Clamp(impact.x,-51.5f,51.5f),Math.Sign(b.position.z)*34);
                RecordExitContact(global,impact,height,"throw-in",side,spot);Restart("throw-in",side,spot,2);return true;}
            int sign=Math.Sign(b.position.x),attack=Direction(0)==sign?0:1;float z=from.z+(b.position.z-from.z)*fx,y=firstFlight?ReleaseSweepHeight(release,ReleaseSweepFraction(release.fraction,fx)):b.previousHeight+(b.height-b.previousHeight)*fx;
            // IFAB Law 10: a keeper cannot throw directly into the opponents'
            // goal. Unlike a throw-in, an own goal from open play still counts.
            if((!b.directThrow||b.keeperDistribution&&attack!=b.side)&&Math.Abs(z)<3.66f&&y<2.44f){
                bool own=b.lastTouch!=attack&&!(b.goalAttempt&&b.shotOnTarget&&b.shotSide==attack);
                // A touch that fails to prevent the same uninterrupted shot
                // entering the goal is a deflection, not an additional save.
                if(b.saveCredited&&b.goalAttempt&&b.shotSide==attack)State.metrics[b.saveSide].saves=Math.Max(0,State.metrics[b.saveSide].saves-1);
                State.score[attack]++;RecordOnTarget();if(own)State.metrics[b.lastTouch].ownGoals++;var player=own?b.lastTouchId:b.goalAttempt?b.from:b.lastTouchId;
                Emit("goal",attack,player,own?"BUT ! Le ballon est dévié dans son propre but.":"BUT ! Le ballon a entièrement franchi la ligne.");
                RecordGoalContact(firstFlight?ReleaseSweepFraction(release.fraction,fx):fx,new Point(sign*GoalExit,z),y,attack);
                Restart("goal",1-attack,new Point(sign*53,z),3);return true;
            }
            bool corner=b.lastTouch!=attack;string restartKind=corner?"corner":"goal-kick";int restartSide=corner?attack:1-attack;
            var restartSpot=corner?new Point(sign*52.5f,Math.Sign(z+.01f)*33.8f):new Point(sign*47,0);
            RecordExitContact(firstFlight?ReleaseSweepFraction(release.fraction,fx):fx,new Point(sign*GoalExit,z),y,restartKind,restartSide,restartSpot);
            if(corner){State.metrics[attack].corners++;Restart("corner",attack,restartSpot,3);}else Restart("goal-kick",1-attack,restartSpot,3);return true;
        }
        public float FirstTouchError(Actor player,float speed,float pressure)
        {
            // The same reception difficulty penalises poor technique more, without
            // manufacturing a turnover: the released ball remains contestable.
            return Mathx.Clamp((100-Skill(player,"ballControl"))*.006f+Math.Max(0,speed-12)*.022f+Math.Max(0,3-pressure)*.1f,.05f,1.1f);
        }
        void RecordOnTarget(){var b=State.ball;if(b.goalAttempt&&b.shotOnTarget&&!b.onTargetCounted){State.metrics[b.shotSide].shotsOnTarget++;b.onTargetCounted=true;}}
    }
}


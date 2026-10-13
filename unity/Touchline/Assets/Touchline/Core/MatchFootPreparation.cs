using System;

namespace Touchline.Core
{
    public sealed partial class MatchSimulation
    {
        public const string FootDeliveryPreparation="prepare-kick";
        const string PreparedFootDelivery="foot-delivery-ready";
        public static bool PreparingFootDelivery(Actor actor)=>actor!=null&&actor.action==FootDeliveryPreparation&&actor.actionTime>0;

        void ReconsiderPreparedBackpass(Actor player)
        {
            // Re-read a newly opened finish while pivoting to recycle outside the box.
            // Only pass -> shot is allowed: no alternating pivots or instantaneous yaw.
            if(player.slot==0||(player.actionKind!="pass"&&player.actionKind!="switch"))return;
            int dir=Direction(player.side);var goal=new Point(dir*52.5f,0);
            // Metres: recycle beyond the final 22.5 m, retreat >=3 m, finish within18 m/10 m wide.
            // .08 is the existing internal geometric shot-quality scale, not displayed xG.
            if(player.actionTarget.x*dir>=30||(player.position.x-player.actionTarget.x)*dir<3)return;
            if(Point.Distance(player.position,goal)>18||Math.Abs(player.position.z)>10||ShotQuality(player)<.08f||!ShootingLaneClear(player,0))return;
            float depth=Math.Max(.5f,52.5f-player.position.x*dir),z=player.position.z;
            // Same minimum goal aperture as the normal close-range shooting decision (radians).
            if(Math.Atan2(3.66f-z,depth)-Math.Atan2(-3.66f-z,depth)<=.26f)return;
            if(BeginFootDeliveryPreparation(player,goal,"shot"))return;
            // Already facing the finish: keep one physical step before the usual decision.
            player.actionKind="shot";player.actionTarget=goal;player.actionTime=Step;player.actionContactTime=Step;
        }

        static float FootDeliveryTurn(Actor player,Point destination)
        {
            float angle=(float)Math.Atan2(destination.x-player.position.x,destination.z-player.position.z);
            float turn=angle-player.angle;
            while(turn>Math.PI)turn-=(float)Math.PI*2;
            while(turn< -Math.PI)turn+=(float)Math.PI*2;
            return Math.Abs(turn);
        }

        bool BeginFootDeliveryPreparation(Actor player,Point destination,string kind)
        {
            // Only open-play decisions use this phase. A turn happens while
            // the ball is still owned and contestable, before shot/pass rolls.
            // Restarts, headers and hand deliveries keep their contact paths.
            if(State.ball.owner!=player.id||State.ball.held||State.ball.height>.65f)return false;
            float turn=FootDeliveryTurn(player,destination);
            // A side-foot delivery can use an open body; a large reversal
            // needs a real braking/pivot interval, not an instantaneous yaw.
            if(turn<=Math.PI/3)return false;
            float braking=3+Skill(player,"acceleration")*.055f;
            player.action=FootDeliveryPreparation;player.actionKind=kind;
            player.actionTarget=destination;player.actionHeight=State.ball.height;
            player.actionTime=Math.Max(Math.Abs(turn)/5,player.velocity.Length/braking)+Step;
            // In prepare-kick only, this stores the initial phase duration for
            // presentation. Flight replaces it with the actual contact delay.
            player.actionContactTime=player.actionTime;player.actionSequence++;
            // Re-evaluate the football decision after preparation. The old
            // recipient or shooting lane may no longer be available then.
            State.decision=.1f;return true;
        }
    }
}

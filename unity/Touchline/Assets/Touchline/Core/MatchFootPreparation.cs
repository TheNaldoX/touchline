using System;

namespace Touchline.Core
{
    public sealed partial class MatchSimulation
    {
        public const string FootDeliveryPreparation="prepare-kick";
        const string PreparedFootDelivery="foot-delivery-ready";
        public static bool PreparingFootDelivery(Actor actor)=>actor!=null&&actor.action==FootDeliveryPreparation&&actor.actionTime>0;

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

using System;
namespace Touchline.Core
{
    public sealed partial class MatchSimulation
    {
        // A supporting teammate must also run around a stationary keeper's
        // gather/throwing body. The ordinary .56m upright contact circle is too
        // small for the keeper bending forward to roll the ball.
        const float KeeperBodyClearance=1.2f;
        Actor KeeperBodyInHands()
        {
            var ball=State.ball;var owner=Owner;
            if(ball.held&&owner!=null&&owner.slot==0)return owner;
            foreach(var keeper in State.actors)if(keeper.slot==0&&!keeper.sentOff&&HandDistribution(keeper.action)&&keeper.actionTime>0)return keeper;
            return null;
        }
        Point KeeperBodySafeTarget(Actor player,Actor keeper,Point target)
        {
            var relative=player.position-keeper.position;
            if(relative.Length>6)return target;
            var travel=target-player.position;
            float fraction=Projection(player.position,target,keeper.position);
            var closest=Point.Lerp(player.position,target,fraction)-keeper.position;
            if(closest.Length>=KeeperBodyClearance+.2f)return target;
            var side=closest.Normalized;
            if(side.Length<.1f){
                side=new Point(-travel.z,travel.x).Normalized*(player.slot%2==0?1:-1);
                if(side.Length<.1f)side=relative.Length>.01f?relative.Normalized:new Point(1,0);
            }
            // Use a waypoint outside the whole gesture, then release back to
            // the formation route once that segment no longer crosses it.
            return keeper.position+side*(KeeperBodyClearance+.35f);
        }
        Point KeeperBodySafeVelocity(Actor player,Actor keeper,Point desired,float maximum)
        {
            var toKeeper=keeper.position-player.position;float distance=toKeeper.Length;
            if(distance>6||distance<.001f)return desired;
            var normal=toKeeper/distance;
            float actualClosing=Math.Max(0,Point.Dot(player.velocity-keeper.velocity,normal));
            float gap=Math.Max(0,distance-KeeperBodyClearance-actualClosing*(Step+.05f));
            float braking=3+Skill(player,"acceleration")*.055f;
            float safe=(float)Math.Sqrt(2*braking*gap);
            float closing=Point.Dot(desired-keeper.velocity,normal);
            if(closing>safe)desired-=normal*(closing-safe);
            if(desired.Length>maximum)desired=desired.Normalized*maximum;
            return desired;
        }
        Actor CommittedKickerBody()
        {
            var b=State.ball;
            if(b.owner!=null||b.elapsed>=.35f||b.elapsed< -b.releaseDelay)return null;
            if(b.kind!="pass"&&b.kind!="through"&&b.kind!="cross"&&b.kind!="cutback"&&b.kind!="switch"&&b.kind!="shot")return null;
            var p=Find(b.from);
            return p!=null&&!p.sentOff&&p.action=="kick"&&p.actionTime>0?p:null;
        }
        static bool TrackingCarrier(Actor player)=>player.intent=="press"||player.intent=="delay"||player.intent=="cover"||player.intent=="mark-carrier";
        Point ShieldSafePressTarget(Actor defender,Actor carrier,Point target)
        {
            var relative=defender.position-carrier.position;
            if(relative.Length>4.5f)return target;
            float projection=Projection(defender.position,target,carrier.position);
            if(projection<=0||projection>=1||Point.Distance(carrier.position,Point.Lerp(defender.position,target,projection))>=.7f)return target;
            var forward=new Point((float)Math.Sin(carrier.angle),(float)Math.Cos(carrier.angle));
            var lateral=new Point(forward.z,-forward.x);
            float flank=Point.Dot(relative,lateral);float side=Math.Abs(flank)>.08f?Math.Sign(flank):defender.slot%2==0?1:-1;
            var channel=State.ball.position+lateral*(side*.75f);
            float blocked=Projection(defender.position,channel,carrier.position);
            if(blocked>0&&blocked<1&&Point.Distance(carrier.position,Point.Lerp(defender.position,channel,blocked))<.7f)
                channel=carrier.position-forward*.55f+lateral*(side*.95f);
            return channel;
        }
        Point ControlledPressVelocity(Actor defender,Actor carrier,Point desired,float maximum)
        {
            float distance=Point.Distance(defender.position,carrier.position);
            if(distance>4.5f)return desired;
            // Limit actual relative closure toward the body, retaining the
            // tangential motion needed to get beside the ball. An incoming
            // carrier can require the defender to backpedal rather than stop.
            var normal=(carrier.position-defender.position).Normalized;
            float closing=Point.Dot(desired-carrier.velocity,normal);
            float actualClosing=Math.Max(0,Point.Dot(defender.velocity-carrier.velocity,normal));
            // A decision is based on the preceding movement sample. Reserve
            // that reaction travel before using the remaining braking gap.
            float gap=Math.Max(0,distance-BodySeparation-.25f-actualClosing*(Step+.05f));
            float braking=3+Skill(defender,"acceleration")*.055f;
            float safe=(float)Math.Sqrt(2*braking*gap);
            if(closing>safe)desired-=normal*(closing-safe);
            if(desired.Length>maximum)desired=desired.Normalized*maximum;
            return desired;
        }
    }
}

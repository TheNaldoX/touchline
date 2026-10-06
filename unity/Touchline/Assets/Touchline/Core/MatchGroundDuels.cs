using System;

namespace Touchline.Core
{
    public sealed partial class MatchSimulation
    {
        public const float ContactFallDuration=2.2f;
        public const float SlidingDuelDuration=1.7f,SlidingDuelContact=.30f;
        public const string SlidingDuel="sliding-duel";
        public static bool GroundedAction(Actor actor)=>(actor.action=="fall"||actor.action=="slide")&&actor.actionTime>0;

        bool BeginSlidingDuel(Actor defender,Actor owner)
        {
            var b=State.ball;var delta=b.position-defender.position;float distance=delta.Length;
            if(defender.side==owner.side||b.held||defender.slot==0||defender.sentOff||defender.controlTime>0||defender.duelCooldown>0||owner.controlTime>0||GroundedAction(defender)||defender.action=="tackle"||defender.action=="hurt")return false;
            if(distance<1.3f||distance>1.9f||b.height>.35f||defender.velocity.Length<3||owner.position.x*Direction(owner.side)<20||defender.fitness<35||Tactic(defender.side).pressing<.35f)return false;
            float bodyProjection=Projection(defender.position,b.position,owner.position);
            if(bodyProjection>.1f&&bodyProjection<.95f&&Point.Distance(owner.position,Point.Lerp(defender.position,b.position,bodyProjection))<.42f)return false;
            var anticipated=b.position+owner.velocity*SlidingDuelContact;
            var reach=anticipated-defender.position;
            // A slide needs a reachable future ball, not its stale location.
            // The three decelerating travel steps cover about 1.15 metres;
            // leave another .75 metres for the extended boot.
            if(reach.Length>1.9f)return false;
            float futureBody=Projection(defender.position,anticipated,owner.position+owner.velocity*SlidingDuelContact);
            if(futureBody>.1f&&futureBody<.95f&&Point.Distance(owner.position+owner.velocity*SlidingDuelContact,Point.Lerp(defender.position,anticipated,futureBody))<.42f)return false;
            var direction=reach.Normalized;var forward=new Point((float)Math.Sin(defender.angle),(float)Math.Cos(defender.angle));
            if(Point.Dot(forward,direction)<.75f||Point.Dot(defender.velocity-owner.velocity,direction)<1.5f||Skill(defender,"slidingTackle")<50)return false;
            // Commit once to the reachable lane. The player cannot steer a
            // slide around the carrier after leaving his feet.
            defender.action="slide";defender.actionKind=SlidingDuel;defender.actionSequence++;defender.actionTime=SlidingDuelDuration;defender.actionContactTime=SlidingDuelContact;
            defender.actionTarget=anticipated;defender.actionHeight=BallRadius;defender.tackleOpponent=owner.id;
            defender.velocity=direction*4.8f;defender.angle=(float)Math.Atan2(direction.x,direction.z);
            var lateral=owner.position-defender.position;defender.diveSide=lateral.x*(float)Math.Cos(defender.angle)-lateral.z*(float)Math.Sin(defender.angle)>=0?1:-1;
            defender.controlTime=SlidingDuelDuration+.1f;defender.duelCooldown=SlidingDuelDuration+.45f;
            return true;
        }

        bool ResolveSlidingDuels(Actor owner)
        {
            var b=State.ball;
            foreach(var defender in State.actors){
                if(defender.action!="slide"||defender.actionKind!=SlidingDuel||defender.tackleOpponent==null||defender.actionTime>SlidingDuelDuration-SlidingDuelContact+.0001f)continue;
                bool same=defender.tackleOpponent==owner.id;defender.tackleOpponent=null;
                if(!same||defender.sentOff||owner.controlTime>0||b.height>.4f)continue;
                float distance=Point.Distance(defender.position,b.position);if(distance>1.05f)continue;
                var toward=(b.position-defender.position).Normalized;
                if(Point.Dot(toward,new Point((float)Math.Sin(defender.angle),(float)Math.Cos(defender.angle)))<.55f)continue;
                float projection=Projection(defender.position,b.position,owner.position);
                bool bodyFirst=projection>.12f&&projection<.9f&&Point.Distance(owner.position,Point.Lerp(defender.position,b.position,projection))<.24f;
                float foulRoll=Random();
                bool foul=bodyFirst||StandingTripContact(defender,owner)&&foulRoll<.04f+(100-Skill(defender,"slidingTackle"))*.0006f;
                var committedTarget=defender.actionTarget;defender.actionTarget=b.position;defender.actionHeight=BallRadius;
                if(foul){
                    defender.actionTarget=bodyFirst?Point.Lerp(defender.position,b.position,projection):committedTarget;
                    State.metrics[defender.side].fouls++;Emit("foul",defender.side,defender.id,Data(defender).name+" arrive en retard sur son tacle glissé.");
                    BeginContactFall(owner,defender,true);
                    if(State.professionalRules&&Random()<.48f)Caution(defender.id);
                    bool penalty=owner.position.x*Direction(owner.side)>36&&Math.Abs(owner.position.z)<20.16f;
                    Restart(penalty?"penalty":"free-kick",owner.side,penalty?new Point(Direction(owner.side)*41.5f,0):owner.position,3);return true;
                }
                float chance=Mathx.Clamp(.43f+(Skill(defender,"slidingTackle")-Skill(owner,"dribbling"))*.006f,.12f,.78f);
                if(Random()>=chance)continue;
                State.metrics[owner.side].pressuredLosses++;LooseBall(b.position,toward*(3+Random()*2),BallRadius,.35f,defender.side,defender.id);
                BeginContactFall(owner,defender,false);owner.controlTime=Math.Max(owner.controlTime,.35f);
                Emit("tackle",defender.side,defender.id,Data(defender).name+" coupe la course du ballon d’un tacle glissé.");return true;
            }
            return false;
        }

        // Called only after a resolved tackle or foul. It does not decide who
        // wins the duel, and cannot invent a collision with a distant player.
        bool BeginContactFall(Actor player,Actor challenger,bool foul)
        {
            if(player.sentOff||player.slot==0||GroundedAction(player))return false;
            var contactPoint=challenger.position;
            if(Point.Distance(player.position,challenger.position)>.95f){
                // An extended tackling boot can genuinely catch the runner's
                // stride while the two torso roots are farther apart. Require
                // that actual committed foot contact before playing a trip.
                if(!foul||(challenger.action!="tackle"&&challenger.action!="slide")||
                    Point.Distance(challenger.position,challenger.actionTarget)>1.25f)return false;
                float along=Projection(player.previous,player.position,challenger.actionTarget);
                if(Point.Distance(challenger.actionTarget,Point.Lerp(player.previous,player.position,along))>=.35f)return false;
                contactPoint=challenger.actionTarget;
            }
            float relative=(player.velocity-challenger.velocity).Length;
            if(relative<(foul?1.2f:3.2f))return false;
            var contact=contactPoint-player.position;
            float lateral=contact.x*(float)Math.Cos(player.angle)-contact.z*(float)Math.Sin(player.angle);
            player.action="fall";player.actionKind=foul?"fouled-fall":"tackled-fall";player.actionTime=ContactFallDuration;
            player.actionSequence++;player.actionTarget=contactPoint;player.actionHeight=0;player.diveSide=lateral>=0?1:-1;
            player.actionContactTime=.36f;player.tackleOpponent=null;player.velocity=new Point();
            player.controlTime=Math.Max(player.controlTime,ContactFallDuration+.1f);player.duelCooldown=Math.Max(player.duelCooldown,ContactFallDuration+.25f);
            return true;
        }

        bool AdvanceGroundAction(Actor player)
        {
            if(!GroundedAction(player))return false;
            if(player.action=="slide"){
                if(player.actionTime>SlidingDuelDuration-SlidingDuelContact){player.position=ContactBounds(player.position+player.velocity*Step);player.velocity*=.78f;}
                else player.velocity=new Point();
                if(Owner==null||Owner.id!=player.tackleOpponent)player.tackleOpponent=null;
            }else player.velocity=new Point();
            player.actionTime=Math.Max(0,player.actionTime-Step);
            if(player.actionTime<=0){player.action="idle";player.actionKind="";player.tackleOpponent=null;}
            return true;
        }
    }
}

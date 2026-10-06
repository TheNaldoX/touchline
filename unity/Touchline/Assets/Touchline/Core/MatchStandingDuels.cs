using System;

namespace Touchline.Core
{
    public sealed partial class MatchSimulation
    {
        public const float TacklePreparation=.2f,TackleRecovery=.55f;
        public const string StandingDuel="standing-duel";
        void CancelInvalidStandingDuels()
        {
            var owner=Owner;
            foreach(var p in State.actors){
                if(string.IsNullOrEmpty(p.tackleOpponent)||p.action=="slide")continue;
                if(State.restart>0||p.sentOff||p.action!="tackle"||p.actionKind!=StandingDuel||owner==null||owner.id!=p.tackleOpponent||State.ball.held){
                    p.tackleOpponent=null;
                    // Keep the committed gesture, but withdraw from the reach.
                    if(p.action=="tackle"&&p.actionKind==StandingDuel){p.tackleWithdrawFrom=p.actionTime;p.actionTime=Math.Min(p.actionTime,TackleRecovery*.35f);}
                }
            }
        }
        bool BeginStandingDuel(Actor defender,Actor owner)
        {
            var b=State.ball;var delta=b.position-defender.position;
            if(delta.Length>1.3f||b.height>.45f||GroundedAction(defender)||defender.action=="tackle"||defender.action=="hurt")return false;
            float exposed=Point.Distance(owner.position,b.position);
            bool urgent=owner.position.x*Direction(owner.side)>44&&Math.Abs(owner.position.z)<10;
            float restraint=defender.yellows>0?.49f:owner.position.x*Direction(owner.side)>30?.45f:.30f;
            if(!urgent&&exposed<restraint&&delta.Length>.65f)return false;
            var forward=new Point((float)Math.Sin(defender.angle),(float)Math.Cos(defender.angle));
            // During the preparation the carrier keeps running while the
            // defender brakes. Judge the ball at contact, not the place it
            // occupied before the tackling foot left the ground.
            var futureBall=b.position+owner.velocity*TacklePreparation;
            var futureBody=owner.position+owner.velocity*TacklePreparation;
            float braking=3+Skill(defender,"acceleration")*.055f;
            var futureDefender=defender.position;var velocity=defender.velocity;
            for(float time=0;time<TacklePreparation-.001f;time+=Step){
                float speed=velocity.Length;velocity=speed>braking*Step?velocity.Normalized*(speed-braking*Step):new Point();
                futureDefender+=velocity*Step;
            }
            // The carrier cannot run straight through a planted defender.
            // Predict the same body separation enforced by movement, keeping
            // the exposed ball in front of that actual reachable body point.
            float bodyContact=ContactFraction(owner.position,futureBody,defender.position,futureDefender,BodySeparation);
            if(bodyContact<=1){
                futureBody=Point.Lerp(owner.position,futureBody,bodyContact)+(futureDefender-defender.position)*(1-bodyContact);
                futureBall=futureBody+(b.position-owner.position);
            }
            var futureReach=futureBall-futureDefender;
            if(futureReach.Length>1.05f||Point.Dot(forward,futureReach.Normalized)<.15f)return false;
            float shield=Projection(futureDefender,futureBall,futureBody);
            if(shield>.1f&&shield<.95f&&Point.Distance(futureBody,Point.Lerp(futureDefender,futureBall,shield))<.30f)return false;
            defender.tackleOpponent=owner.id;defender.action="tackle";defender.actionKind=StandingDuel;
            defender.tackleWithdrawFrom=0;
            defender.actionContactTime=TacklePreparation;defender.actionTime=TacklePreparation+TackleRecovery;defender.actionSequence++;
            defender.actionTarget=futureBall;defender.actionHeight=BallRadius;
            defender.duelCooldown=.9f+(100-Skill(defender,"standingTackle"))*.006f;
            defender.controlTime=Math.Max(defender.controlTime,TacklePreparation+.15f);
            return true;
        }
        bool ResolveStandingDuels(Actor owner)
        {
            var m=State;var b=m.ball;
            foreach(var defender in m.actors){
                if(defender.action!="tackle"||defender.actionKind!=StandingDuel||string.IsNullOrEmpty(defender.tackleOpponent)||defender.actionTime>TackleRecovery+.0001f)continue;
                bool sameOwner=defender.tackleOpponent==owner.id;defender.tackleOpponent=null;
                if(!sameOwner||defender.sentOff||owner.controlTime>0||b.height>.45f)continue;
                var committedTarget=defender.actionTarget;bool lateContact=StandingTripContact(defender,owner);
                bool ballReachable=Point.Distance(defender.position,b.position)<=1.05f;
                // A carrier can escape the ball contest and still be tripped
                // by a boot left in his stride. Evaluate that real contact
                // before abandoning the poke, including midfield runs.
                if(!ballReachable&&!lateContact)continue;
                // The boot cannot poke straight through the carrier's torso.
                float projection=Projection(defender.position,b.position,owner.position);
                bool protectedBall=projection>.15f&&projection<.9f&&Point.Distance(owner.position,Point.Lerp(defender.position,b.position,projection))<.22f;
                if(protectedBall&&!lateContact)continue;
                // A missed poke is not automatically a trip. The committed
                // boot must meet the moving carrier's foot corridor; a clean
                // reach to an exposed ball cannot generate a random penalty.
                defender.actionTarget=b.position;defender.actionHeight=BallRadius;
                float tackle=Mathx.Clamp(.44f+(Skill(defender,"standingTackle")-Skill(owner,"dribbling"))*.006f,.12f,.8f);
                float foulRoll=Random();
                if(lateContact&&foulRoll<.035f+(100-Skill(defender,"standingTackle"))*.0004f){
                    defender.actionTarget=committedTarget;
                    m.metrics[defender.side].fouls++;Emit("foul",defender.side,defender.id,Data(defender).name+" intervient en retard.");
                    if(m.professionalRules&&Random()<.32f)Caution(defender.id);
                    bool penalty=owner.position.x*Direction(owner.side)>36&&Math.Abs(owner.position.z)<20.16f;
                    BeginContactFall(owner,defender,true);
                    Restart(penalty?"penalty":"free-kick",owner.side,penalty?new Point(Direction(owner.side)*41.5f,0):owner.position,3);return true;
                }
                if(!ballReachable||protectedBall||Random()>=tackle)continue;
                m.metrics[owner.side].pressuredLosses++;var direction=(b.position-defender.position).Normalized;
                LooseBall(b.position,direction*(2+Random()*2),BallRadius,.4f,defender.side,defender.id);
                BeginContactFall(owner,defender,false);owner.controlTime=Math.Max(owner.controlTime,.35f);Emit("tackle",defender.side,defender.id,Data(defender).name+" déloge le ballon.");return true;
            }
            return false;
        }
        bool StandingTripContact(Actor defender,Actor carrier)
        {
            if((carrier.velocity-defender.velocity).Length<1.2f)return false;
            var boot=defender.actionTarget;
            if(Point.Distance(defender.position,boot)>1.25f)return false;
            // The ball has to have escaped that committed contact point.
            // A foot touching the ball beside the carrier is still a legal poke.
            if(Point.Distance(boot,State.ball.position)<=.28f)return false;
            float path=Projection(carrier.previous,carrier.position,boot);
            return Point.Distance(boot,Point.Lerp(carrier.previous,carrier.position,path))<.35f;
        }
    }
}

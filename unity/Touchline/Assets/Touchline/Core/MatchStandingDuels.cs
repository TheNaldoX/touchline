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
        // A defender in reach does not lunge at every opportunity: he jockeys
        // and commits when the ball is exposed, his instructions ask for it or
        // his temperament pushes him. Expressed as a per-second rate so the
        // decision does not depend on the simulation step.
        public const float ChallengeRatePerSecond=1.2f;
        // A lost poke from a defender in the carrier's running corridor, or
        // arriving from behind him, often catches his stride (lateral offset
        // under .75 m). A boot that meets the exposed ball beside the runner
        // does not. Inside his own area the defender holds back far more,
        // but a late challenge there is a penalty.
        const float MistimedBaseChance=.55f; // part des pokes perdus qui accrochent le porteur (joueurs égaux, à l'arrêt)
        const float AreaMistimedShare=.10f; // part des fautes en retard encore commises dans sa propre surface
        const float BookedCarefulness=.5f; // part des fautes « en retard » encore commises une fois averti
        bool MistimedChallenge(Actor defender,Actor owner)
        {
            var m=State;float speed=owner.velocity.Length;
            if(!m.professionalRules||speed<1.5f)return false;
            var run=owner.velocity*(1/speed);var toDefender=defender.position-owner.position;
            float along=Point.Dot(toDefender,run),lateral=Math.Abs(toDefender.x*run.z-toDefender.z*run.x);
            if(along< -1f||along>1.2f||lateral>.75f)return false;
            bool area=owner.position.x*Direction(owner.side)>36&&Math.Abs(owner.position.z)<20.16f;
            float chance=Mathx.Clamp(MistimedBaseChance+(along<0?.15f:0)+(Skill(owner,"dribbling")-Skill(defender,"standingTackle"))*.004f
                +(Skill(defender,"aggression")-60)*.003f+owner.velocity.Length*.025f,.10f,.65f);
            if(defender.yellows>0)chance*=BookedCarefulness; // un joueur averti retient son geste
            if(area)chance*=AreaMistimedShare; // dans sa surface, le défenseur retient beaucoup plus son geste
            if(Random()>=chance)return false;
            m.metrics[defender.side].fouls++;Emit("foul",defender.side,defender.id,Data(defender).name+" accroche son adversaire.");
            // Most such fouls are careless, not reckless.
            if(Random()<.16f+Math.Max(0,Skill(defender,"aggression")-70)*.006f)Caution(defender.id);
            BeginContactFall(owner,defender,true);
            if(area)Restart("penalty",owner.side,new Point(Direction(owner.side)*41.5f,0),3);else Restart("free-kick",owner.side,owner.position,3);return true;
        }
        // Called by the match loop before BeginStandingDuel, which stays a pure
        // reachability check. Only rolls when a poke is physically possible.
        bool CommitsToChallenge(Actor defender,Actor owner)
        {
            var b=State.ball;
            if(Point.Distance(b.position,defender.position)>1.3f||b.height>.45f||GroundedAction(defender)||defender.action=="tackle"||defender.action=="hurt")return false;
            bool urgent=owner.position.x*Direction(owner.side)>44&&Math.Abs(owner.position.z)<10;
            if(urgent)return true;
            float exposed=Point.Distance(owner.position,b.position);
            var t=Tactic(defender.side);
            float rate=ChallengeRatePerSecond
                *(.55f+t.pressing*.9f)                                   // consignes : 0,55× (attentiste) à 1,45× (pressing max)
                *(.75f+Skill(defender,"aggression")*.005f)               // tempérament : 0,75× à 1,25×
                *(1+Math.Max(0,exposed-.45f)*2.5f);                      // ballon mal protégé : opportunité
            bool ownBox=owner.position.x*Direction(owner.side)>36&&Math.Abs(owner.position.z)<20.16f;
            if(ownBox)rate*=.55f; // prudence dans sa propre surface
            if(defender.yellows>0)rate*=.6f;
            return Random()<1-(float)Math.Exp(-rate*Step);
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
                // A boot that meets the carrier's stride is usually whistled.
                // Inside the area the original, rarer rate is kept so spot
                // kicks stay exceptional.
                bool inArea=owner.position.x*Direction(owner.side)>36&&Math.Abs(owner.position.z)<20.16f;
                float tripFoul=inArea?.035f+(100-Skill(defender,"standingTackle"))*.0004f:.30f+(100-Skill(defender,"standingTackle"))*.003f;
                if(lateContact&&foulRoll<tripFoul){
                    defender.actionTarget=committedTarget;
                    m.metrics[defender.side].fouls++;Emit("foul",defender.side,defender.id,Data(defender).name+" intervient en retard.");
                    if(m.professionalRules&&Random()<.32f)Caution(defender.id);
                    bool penalty=owner.position.x*Direction(owner.side)>36&&Math.Abs(owner.position.z)<20.16f;
                    BeginContactFall(owner,defender,true);
                    Restart(penalty?"penalty":"free-kick",owner.side,penalty?new Point(Direction(owner.side)*41.5f,0):owner.position,3);return true;
                }
                if(!ballReachable||protectedBall||Random()>=tackle){
                    if(MistimedChallenge(defender,owner))return true;
                    continue;
                }
                m.metrics[owner.side].pressuredLosses++;var direction=Deflect((b.position-defender.position).Normalized,PokeSpread);
                // A poke dislodges the ball a few metres: 3-7 m/s before rolling friction.
                LooseBall(b.position,direction*(3+Random()*4),BallRadius,.4f,defender.side,defender.id);
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

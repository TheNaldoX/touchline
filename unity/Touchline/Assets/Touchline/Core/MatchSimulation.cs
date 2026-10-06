using System;
using System.Collections.Generic;
using System.Linq;

namespace Touchline.Core
{
    // Metres and seconds are shared by simulation and rendering. New career
    // matches simulate two full halves; legacy saves retain their old clock.
    public sealed partial class MatchSimulation
    {
        public const float Step=.1f;
        public readonly MatchState State;
        readonly Dictionary<string,PlayerData> roster;
        readonly Point[] targets=new Point[22];
        readonly int[] firstPress=new int[2],coverPress=new int[2];
        public MatchSimulation(Database db,MatchState state)
        {
            State=state;roster=db.players.ToDictionary(p=>p.id);
            // JsonUtility restores null string references as empty strings.
            // Canonicalise identifiers before any physical or restart decision.
            if(string.IsNullOrEmpty(State.ball.owner))State.ball.owner=null;
            if(string.IsNullOrEmpty(State.ball.to))State.ball.to=null;
            if(string.IsNullOrEmpty(State.ball.from))State.ball.from=null;
            if(string.IsNullOrEmpty(State.ball.lastTouchId))State.ball.lastTouchId=null;
            if(string.IsNullOrEmpty(State.restartTaker))State.restartTaker=null;
            foreach(var actor in State.actors)if(string.IsNullOrEmpty(actor.tackleOpponent))actor.tackleOpponent=null;
            foreach(var e in State.events){if(string.IsNullOrEmpty(e.player))e.player=null;if(string.IsNullOrEmpty(e.receiver))e.receiver=null;}
            State.pendingSubstitutions??=new List<PendingSubstitution>();
            var owner=Owner;if(owner!=null&&owner.slot==0&&(owner.action=="claim"||owner.action=="dive"))State.ball.held=true;
            if(State.metrics==null||State.metrics.Length!=2)State.metrics=new[]{new TeamMetrics(),new TeamMetrics()};
            for(int i=0;i<2;i++)if(State.metrics[i]==null)State.metrics[i]=new TeamMetrics();
            if(State.engineVersion<3){State.awayReviewAt=Math.Max(60*State.SecondsPerMinute,State.clock+1);State.awayBaseLine=State.awayTactic.line;State.awayBaseTempo=State.awayTactic.tempo;State.awayBaseRisk=State.awayTactic.mentality;}
            if(State.engineVersion<4){State.awayTacticalReviewAt=State.clock+10*State.SecondsPerMinute;State.awayBaseDefensiveWidth=State.awayTactic.defensiveWidth;State.awayBasePress=State.awayTactic.pressing;}
            State.engineVersion=4;
        }
        public static MatchSimulation Create(Database db,Career career,string opponent,uint seed=731,int periodSeconds=360)
        {
            if(periodSeconds!=360&&periodSeconds!=2700)throw new ArgumentOutOfRangeException(nameof(periodSeconds));
            if(opponent==career.club)throw new ArgumentException("Choisissez un autre club.");
            var own=career.lineup??Career.Select(db,career.club,career.tactic);
            if(own.Length!=11||own.Distinct().Count()!=11||own.Any(id=>db.Find(id)?.team!=career.club))throw new ArgumentException("Le onze doit contenir onze joueurs distincts du club.");
            var awayTactic=OpponentTactic(db,opponent,own);var other=Career.Select(db,opponent,awayTactic);
            var m=new MatchState{home=career.club,away=opponent,seed=seed,homeTactic=career.tactic,awayTactic=awayTactic,actors=new Actor[22],engineVersion=4,awayBaseLine=awayTactic.line,awayBaseTempo=awayTactic.tempo,awayBaseRisk=awayTactic.mentality};
            m.periodSeconds=periodSeconds;m.nextMedicalCheck=m.SecondsPerMinute;m.awayReviewAt=60*m.SecondsPerMinute;
            m.awayTacticalReviewAt=15*m.SecondsPerMinute;m.awayBaseDefensiveWidth=awayTactic.defensiveWidth;m.awayBasePress=awayTactic.pressing;
            for(int i=0;i<22;i++){var id=(i<11?own:other)[i%11];m.actors[i]=new Actor{id=id,side=i/11,slot=i%11,fitness=db.Find(id).fitness};m.used.Add(id);}
            var sim=new MatchSimulation(db,m);sim.PlaceKickoffPlayers(0);sim.Restart("kickoff",0,new Point(),2);return sim;
        }
        static Tactic OpponentTactic(Database db,string club,string[] opposition)
        {
            var best=new Tactic();float score=float.MinValue;
            foreach(var shape in new[]{"4-3-3","4-4-2","4-2-3-1","3-4-2-1"}){var candidate=new Tactic();candidate.SetFormation(shape);var ids=Career.Select(db,club,candidate);float fit=0;for(int i=0;i<11;i++)fit+=db.Find(ids[i]).rating*db.Find(ids[i]).Fit(candidate.withoutBall[i].role);if(fit>score){score=fit;best=candidate;}}
            var lineup=Career.Select(db,club,best).Select(db.Find).ToArray();float difference=(float)(lineup.Average(p=>p.rating)-opposition.Select(db.Find).Average(p=>p.rating));
            best.line=Mathx.Clamp(.45f+difference*.015f,.2f,.72f);best.pressing=Mathx.Clamp(.45f+difference*.01f+((float)lineup.Average(p=>p.Attribute("stamina"))-70)*.008f,.25f,.8f);best.directness=Mathx.Clamp(.5f-difference*.012f,.3f,.75f);best.counterAttack=difference< -3;best.counterPress=best.pressing>.65f;return best;
        }
        public int Direction(int side)=>(side==0?1:-1)*(State.period==2?-1:1);
        public Tactic Tactic(int side)=>side==0?State.homeTactic:State.awayTactic;
        PlayerData Data(Actor p)=>roster[p.id];
        float Skill(Actor p,string key)
        {
            var data=Data(p);var role=Tactic(p.side).withoutBall[p.slot].role;
            return Mathx.Clamp(MatchAttribute(p,data,key)*(1+data.performanceModifier)*(.86f+data.Fit(role)*.14f)*(.93f+data.morale*.0007f)*(.87f+p.fitness*.0013f),5,99);
        }
        float Random(){State.seed=unchecked(State.seed*1664525+1013904223);return (State.seed>>8)/16777216f;}
        Actor Owner=>Find(State.ball.owner);
        Actor Find(string id){if(id!=null)foreach(var p in State.actors)if(p.id==id)return p;return null;}
        void Emit(string kind,int side,string id,string text,string receiver=null){State.events.Add(new MatchEvent{kind=kind,side=side,player=id,receiver=receiver,text=text,time=State.clock,position=State.ball.position,xg=kind=="shot"?State.shotXg:0});}
        public void Advance(double seconds)
        {
            if(double.IsNaN(seconds)||double.IsInfinity(seconds)||seconds<0)throw new ArgumentOutOfRangeException(nameof(seconds));
            if(State.finished||State.halfTime)return;State.remainder+=seconds;
            while(State.remainder+1e-9>=.1&&!State.finished&&!State.halfTime){State.remainder=Math.Max(0,State.remainder-.1);Tick();}
            if(State.finished||State.halfTime)State.remainder=0;
        }
        void TickCore()
        {
            KeeperContact=default;
            ReleaseContact=default;
            ImpactContact=default;
            var m=State;m.clock=(float)(Math.Round(m.clock*10)+1)/10;
            foreach(var p in m.actors){p.previous=p.position;p.duelCooldown=Math.Max(0,p.duelCooldown-Step);p.controlTime=Math.Max(0,p.controlTime-Step);}
            m.ball.previous=m.ball.position;m.ball.previousHeight=m.ball.height;
            if(m.restart>0)ApplyPendingSubstitutions();
            if(m.restart>0){m.restart=Math.Max(0,m.restart-Step);MoveRestart();if(m.restart<=0){if(m.phase=="goal")Restart("kickoff",m.restartSide,new Point(),.3f);else if(m.phase=="kickoff")Kickoff(m.restartSide);else RestartKick();}}
            else{Move();CancelInvalidStandingDuels();if(m.restart<=0)UpdateBall();RecordMetrics();var owner=Owner;if(m.restart<=0&&owner!=null){m.decision-=Step;m.carryTime+=Step;if(m.decision<=0&&owner.controlTime<=0&&owner.action!="dive"&&owner.action!="claim"){m.decision=DecisionInterval(owner);Decide(owner);}}}
            if(m.period==1&&m.clock>=m.HalfDuration){m.clock=m.HalfDuration;m.halfTime=true;Emit("interval",0,null,"Mi-temps. Ajustez vos consignes.");ApplyPendingSubstitutions();}
            if(m.clock>=m.HalfDuration*2){m.clock=m.HalfDuration*2;m.finished=true;m.pendingSubstitutions.Clear();Emit("fulltime",0,null,"Fin de la rencontre.");}
            if(!m.finished&&!m.halfTime){ReviewOpponent();MedicalDuringMatch();}
            MaintainGoalContact();
            MaintainExitContact();
        }
        bool ProtectingLead(int side)=>State.Minute>=70&&State.score[side]>State.score[1-side]&&Tactic(side).mentality<.5f;
        float DecisionInterval(Actor p)=>1.9f-Tactic(p.side).tempo*1.05f+(100-Skill(p,"vision"))*.003f+(ProtectingLead(p.side)?.4f:0);
        public void ResumeHalf(){if(!State.halfTime)return;State.halfTime=false;State.period=2;PlaceKickoffPlayers(1);Restart("kickoff",1,new Point(),3);}
        public void PlayToEnd(){Advance(State.HalfDuration*2);ResumeHalf();Advance(State.HalfDuration*2);}
        void Restart(string kind,int side,Point position,float duration)
        {
            if(State.HalfDuration==2700)duration=kind=="goal"?45:kind=="goal-kick"?28:kind=="throw-in"?18:kind=="free-kick"?27:kind=="corner"?35:kind=="penalty"?45:duration;
            foreach(var p in State.actors)if(!string.IsNullOrEmpty(p.tackleOpponent)){p.tackleOpponent=null;if(p.action=="tackle"){p.action="idle";p.actionTime=0;}}
            foreach(var actor in State.actors)if(PreparingFootDelivery(actor)){actor.action="idle";actor.actionTime=0;}
            State.phase=kind;State.restartSide=side;State.restart=duration;State.restartTaker=null;State.restartWall=new List<string>();State.indirectRestart=false;State.ball=new BallState{position=position,previous=position,side=side,lastTouch=side};State.carryTime=0;State.turnoverAt=-100;
            if(kind=="kickoff")Emit("setup",side,null,"Les équipes se replacent pour l’engagement.");
            if(kind!="goal"&&kind!="kickoff")Emit(kind,side,null,kind=="corner"?"Corner.":kind=="throw-in"?"Remise en touche.":kind=="goal-kick"?"Sortie de but.":kind=="free-kick"?"Coup franc.":"Les équipes se replacent.");
        }
        void Control(Actor p)
        {
            State.ball.offsidePlayersMask=OffsideSnapshotKnown;
            State.ball.held=false;State.ball.keeperDistribution=false;State.ball.goalAttempt=false;State.ball.saveCredited=false;
            var m=State;var changed=m.possessionSide>=0&&m.possessionSide!=p.side;
            if(changed){m.turnoverAt=m.clock;m.metrics[p.side].recoveries++;if(p.position.x*Direction(p.side)>12)m.metrics[p.side].highRecoveries++;if(Tactic(p.side).counterAttack)m.metrics[p.side].counters++;m.metrics[1-p.side].possessionChain=0;}
            m.possessionSide=p.side;m.ball.controlOrigin=m.ball.position;m.ball.controlElapsed=0;m.ball.controlDuration=.16f+(100-Skill(p,"ballControl"))*.003f;m.ball.owner=p.id;m.ball.side=p.side;m.ball.lastTouch=p.side;m.ball.lastTouchId=p.id;m.ball.kind="none";m.ball.to=null;m.ball.directThrow=false;m.ball.goalAttempt=false;m.ball.passEligible=false;m.ball.velocity=new Point();m.carryTime=0;
            p.controlTime=.12f+(100-Skill(p,"ballControl"))*.003f;m.decision=DecisionInterval(p)*.55f;p.action="control";p.actionTime=.3f;p.carryTarget=ChooseCarry(p);PrepareBodyControl(p);
        }
        float OffsideLine(int side)
        {
            float first=-100,second=-100;int dir=Direction(side);foreach(var p in State.actors)if(!p.sentOff&&p.side!=side){var x=p.position.x*dir;if(x>first){second=first;first=x;}else if(x>second)second=x;}return Math.Max(0,Math.Max(State.ball.position.x*dir,second));
        }
        float Space(Point point,int oppositionSide,bool outfield=false){float d=100;foreach(var p in State.actors)if(!p.sentOff&&p.side==oppositionSide&&(!outfield||p.slot>0))d=Math.Min(d,Point.Distance(point,p.position));return d;}
        void RecordMetrics()
        {
            if(State.restart>0)return;var owner=Owner;int possession=PossessionSide(State);
            for(int side=0;side<2;side++){var stats=State.metrics[side];float left=100,right=-100,line=0;int defenders=0;foreach(var p in State.actors)if(!p.sentOff&&p.side==side&&p.slot>0){left=Math.Min(left,p.position.z);right=Math.Max(right,p.position.z);if(Tactic(side).withoutBall[p.slot].y<36){line+=p.position.x*Direction(side);defenders++;}stats.distanceRun+=p.velocity.Length*Step;if(p.intent=="press")stats.pressingSeconds+=Step;}
                if(side==possession){stats.widthSum+=right-left;stats.shapeSamples++;stats.possessionSeconds+=Step;stats.possessionChain+=Step;stats.chainSeconds=Math.Max(stats.chainSeconds,stats.possessionChain);if(State.clock-State.turnoverAt<5){stats.transitionSeconds+=Step;stats.transitionProgress+=(State.ball.position.x-State.ball.previous.x)*Direction(side);}}
                else if(defenders>0){stats.lineSum+=line/defenders;stats.defensiveSamples++;}}
        }
        public void Substitute(int side,int slot,string incoming)
        {
            ValidateSubstitution(side,slot,incoming);var p=roster[incoming];var windows=side==0?State.homeWindows:State.awayWindows;
            var actor=State.actors[side*11+slot];
            // The incoming player does not inherit the outgoing player's
            // pending turn or one-decision orientation preference.
            if(actor.action==FootDeliveryPreparation||actor.actionKind==PreparedFootDelivery){actor.action=actor.velocity.Length>.4f?"run":"idle";actor.actionTime=0;actor.actionKind=null;actor.actionContactTime=0;}
            var old=actor.id;float outgoingFitness=actor.fitness;if(!State.halfTime&&!windows.Contains(State.clock))windows.Add(State.clock);State.substitutions[side]++;State.used.Add(incoming);actor.id=incoming;actor.fitness=p.fitness;actor.injured=false;actor.yellows=0;
            foreach(var duel in State.actors)if(duel==actor||duel.tackleOpponent==old){duel.tackleOpponent=null;if(duel.action=="tackle"&&duel.actionKind==StandingDuel){duel.action="idle";duel.actionTime=0;}}
            if(State.ball.owner==old)State.ball.owner=incoming;if(State.ball.to==old)State.ball.to=incoming;Emit("substitution",side,incoming,p.name+" remplace "+roster[old].name+".",old);
            var substitution=State.events[State.events.Count-1];substitution.hasOutgoingFitness=outgoingFitness>=0&&outgoingFitness<=100;substitution.observedOutgoingFitness=substitution.hasOutgoingFitness?outgoingFitness:0;
        }
    }
}


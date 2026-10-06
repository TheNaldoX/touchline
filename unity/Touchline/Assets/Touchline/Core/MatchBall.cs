using System;

namespace Touchline.Core
{
    public sealed partial class MatchSimulation
    {
        void UpdateBall()
        {
            var b=State.ball;var owner=Owner;
            if(owner!=null){UpdateOwnedBall(owner);return;}
            if(b.kind=="none"||b.kind=="loose"){
                RollBall();if(ResolveReception())return;if(GoalFrame())return;BallLeavesPitch();return;
            }
            if(b.elapsed<0&&!b.penalty&&!b.fixedStart){var kicker=Find(b.from);if(kicker!=null&&kicker.action!="header"){b.start=kicker.position+(b.end-kicker.position).Normalized*.42f;kicker.actionTarget=b.start;kicker.actionHeight=b.startHeight;}}
            bool releasing=b.elapsed<0;b.elapsed+=Step;
            if(b.elapsed<0){float set=Mathx.Clamp(1+b.elapsed/Math.Max(.01f,b.releaseDelay),0,1);b.position=Point.Lerp(b.setupStart,b.start,set);b.height=b.setupHeight+(b.startHeight-b.setupHeight)*set;if(b.kind=="throw")b.position=ThrowPreparation(b,set,out b.height);else if(HandDistribution(b.kind))b.position=KeeperDistributionPreparation(b,set,out b.height);return;}
            if(releasing)CaptureOffsidePlayers(b,Mathx.Clamp(1-b.elapsed/Step,0,1));
            float u=Mathx.Clamp(b.elapsed/Math.Max(.1f,b.duration),0,1);b.position=Point.Lerp(b.start,b.end,u);b.height=b.startHeight+(b.endHeight-b.startHeight)*u+(float)Math.Sin(Math.PI*u)*b.loft;
            if(releasing)RecordBallRelease(Mathx.Clamp(1-b.elapsed/Step,0,1));
            if(releasing){
                float flightDuration=Math.Max(.1f,b.duration);
                b.velocity=(b.end-b.start)/flightDuration;
                b.verticalVelocity=(b.endHeight-b.startHeight)/flightDuration+(float)Math.Cos(Math.PI*u)*(float)Math.PI*b.loft/flightDuration;
            }else{b.velocity=(b.position-b.previous)/Step;b.verticalVelocity=(b.height-b.previousHeight)/Step;}
            if(b.kind=="shot"){if(ResolveShotContact())return;}
            else if(b.elapsed>.10f){if(ResolveReception())return;if(ResolveAerial(u))return;}
            if(GoalFrame()||BallLeavesPitch())return;
            if(u>=1){b.kind="loose";b.to=null;if(b.height<=BallRadius+.01f){b.height=BallRadius;b.verticalVelocity=0;}}
        }
        void UpdateOwnedBall(Actor owner)
        {
            if(UpdateHeldBall(owner))return;
            var m=State;var b=m.ball;var forward=new Point((float)Math.Sin(owner.angle),(float)Math.Cos(owner.angle));
            float touch=.30f+.26f*(.5f+.5f*(float)Math.Sin(owner.stride*2.4f));var target=owner.action=="keeper-rise"?owner.actionTarget:owner.position+forward*touch;
            b.controlElapsed+=Step;b.position=Point.Lerp(b.controlOrigin,target,BodyControlProgress(owner,b));b.height=ControlledBallHeight(owner,b);
            if(owner.slot==0&&(owner.action=="dive"||owner.action=="claim")){b.height=.7f;return;}
            if(ResolveSlidingDuels(owner)||ResolveStandingDuels(owner))return;
            foreach(var defender in m.actors){if(defender.sentOff||GroundedAction(defender)||defender.side==owner.side||defender.duelCooldown>0||owner.controlTime>0)continue;
                if(defender.slot==0){if(InOwnArea(defender,b.position)&&Point.Distance(defender.position,b.position)<1.15f&&b.height<.8f){var claimImpact=b.position;float claimHeight=b.height;Control(defender);b.held=true;BeginRecordedKeeperClaim(defender,claimImpact,claimHeight,1);defender.duelCooldown=1.2f;m.metrics[defender.side].keeperClaims++;Emit("claim",defender.side,defender.id,Data(defender).name+" se couche dans les pieds de l’attaquant.");return;}continue;}
                if(!BeginSlidingDuel(defender,owner))BeginStandingDuel(defender,owner);
            }
            if(!GoalFrame())BallLeavesPitch();
        }
        bool ResolveShotContact()
        {
            var b=State.ball;Actor hit=null;
            bool firstFlight=CurrentFirstReleasedFlight(out var release);
            float begin=firstFlight?release.fraction:0;
            var from=firstFlight?release.release:b.previous;
            float exit=ExitFraction(from,b.position),first=firstFlight?ReleaseSweepFraction(begin,exit):exit;
            foreach(var p in State.actors){if(p.sentOff||GroundedAction(p)||p.id==b.from)continue;bool keeper=p.slot==0&&p.side!=b.side&&InOwnArea(p,p.position);
                float f;
                if(firstFlight){
                    // The first released portion is at most Step=.1s long.
                    // Minimum keeper reaction is >.14s, so the existing kernel
                    // uses only .42m physical reach throughout this portion.
                    var playerFrom=Point.Lerp(p.previous,p.position,begin);
                    f=ReleaseSweepFraction(begin,ContactFraction(from,b.position,playerFrom,p.position,keeper?.42f:.56f));
                }else f=keeper?KeeperShotContactFraction(p,b):ContactFraction(b.previous,b.position,p.previous,p.position,.56f);
                float y=firstFlight?ReleaseSweepHeight(release,Math.Min(1,f)):b.previousHeight+(b.height-b.previousHeight)*Math.Min(1,f);
                if(f<first&&y<(keeper?2.5f:1.85f)){first=f;hit=p;}}
            if(hit==null)return false;
            var impact=firstFlight?ReleaseSweepPosition(release,first):Point.Lerp(b.previous,b.position,first);
            float height=firstFlight?ReleaseSweepHeight(release,first):b.previousHeight+(b.height-b.previousHeight)*first;
            if(OffsideReception(hit))return true;
            if(hit.slot==0&&hit.side!=b.side&&InOwnArea(hit,impact)){
                bool anticipated=hit.action=="dive";
                float remaining=anticipated?hit.actionTime:1.2f-(.08f+Step*(1-first));if(!anticipated)hit.actionSequence++;
                hit.actionContactTime=Math.Max(.02f,1.2f-remaining-Step+first*Step);
                hit.actionTarget=impact;hit.actionHeight=height;hit.diveSide=(impact.z-hit.position.z)*-(float)Math.Sin(hit.angle)<0?-1:1;
                bool tip=Point.Distance(hit.position,impact)>1.05f||Random()>.46f+Skill(hit,"gkHandling")*.004f;
                RecordKeeperContact(hit,first,impact,height,!tip);
                // A keeper may intercept a shot already heading wide. That is
                // still animated, but it is not a statistical save on target.
                if(!b.goalAttempt||b.shotOnTarget){State.metrics[hit.side].saves++;b.saveCredited=b.goalAttempt;b.saveSide=hit.side;}RecordOnTarget();
                if(tip){var incoming=b.velocity;var outward=KeeperParryVelocity(incoming,hit.position,impact,Direction(hit.side),Skill(hit,"gkHandling"),Random());hit.controlTime=Math.Max(hit.controlTime,.45f);LooseBall(impact,outward,height,1.5f,hit.side,hit.id);Emit("save",hit.side,hit.id,Data(hit).name+" repousse la frappe !");}
                else{b.position=impact;b.height=height;Control(hit);b.held=true;Emit("save",hit.side,hit.id,Data(hit).name+" capte le ballon.");}
                hit.action="dive";hit.actionKind=tip?"save-parry":"save-catch";hit.actionTime=remaining;return true;
            }
            RecordBallImpact(first,impact,height);
            var contactCenter=firstFlight?Point.Lerp(hit.previous,hit.position,first):hit.position;var bounce=(impact-contactCenter).Normalized;if(bounce.Length<.1f)bounce=b.velocity.Normalized*-1;var blockVelocity=b.velocity;float inward=Point.Dot(blockVelocity,bounce);var deflected=(blockVelocity-bounce*(Math.Min(0,inward)*1.35f))*.58f;LooseBall(impact,deflected,height,.9f,hit.side,hit.id);hit.actionTarget=impact;hit.actionHeight=height;hit.actionSequence++;hit.action="block";hit.actionTime=.45f;Emit("block",hit.side,hit.id,Data(hit).name+" contre la frappe.");return true;
        }
        bool ResolveReception()
        {
            var b=State.ball;Actor hit=null;float first=ExitFraction(b.previous,b.position);
            // A steep descending cross may pass the head and then torso in
            // the same physical tick. The resolver's call order cannot award
            // a later chest contact before an earlier reachable header.
            if(b.kind=="cross"&&b.elapsed/Math.Max(.1f,b.duration)>=.74f)
                foreach(var player in State.actors)first=Math.Min(first,HeaderContactFraction(player));
            foreach(var p in State.actors){if(p.sentOff||GroundedAction(p)||p.controlTime>0||p.id==b.from&&b.kind!="loose"&&b.elapsed<.45f)continue;
                bool claim=p.slot==0&&b.side!=p.side&&InOwnArea(p,b.position);float radius=claim?1.05f:p.id==b.to?1.05f:.72f;float f=ContactFraction(b.previous,b.position,p.previous,p.position,radius);float y=b.previousHeight+(b.height-b.previousHeight)*Math.Min(1,f);
                if(y>(claim?2.3f:.65f))f=float.PositiveInfinity;
                bool bodyAvailable=p.slot>0&&!(p.actionTime>0&&(p.action=="header"||p.action=="dive"||p.action=="hurt"||p.action=="tackle"||p.action=="kick"||p.action=="claim"));
                if(bodyAvailable)f=Math.Min(f,BodyReceptionFraction(p));
                if(f<first){first=f;hit=p;}}
            if(hit==null)return false;
            if(OffsideReception(hit))return true;
            bool intercept=hit.side!=b.side;bool pass=b.passEligible||b.kind!="loose"&&b.kind!="none"&&b.kind!="clearance"&&b.kind!="deflection";float speed=b.velocity.Length;var impact=Point.Lerp(b.previous,b.position,first);float impactHeight=b.previousHeight+(b.height-b.previousHeight)*first;
            if(hit.slot==0&&intercept&&InOwnArea(hit,impact)){RecordKeeperContact(hit,first,impact,impactHeight,true);b.position=impact;b.height=impactHeight;Control(hit);b.held=true;BeginRecordedKeeperClaim(hit,impact,impactHeight,first);State.metrics[hit.side].keeperClaims++;Emit("claim",hit.side,hit.id,Data(hit).name+" sort pour capter le ballon.");return true;}
            if(intercept&&hit.slot>0&&hit.position.x*Direction(hit.side)<-28&&speed>12&&Space(hit.position,1-hit.side)<2.5f){
                bool byline=Math.Abs(impact.x)>44&&Math.Abs(impact.z)>5;
                var exit=(byline?new Point(-Direction(hit.side)*4,impact.z>=0?6:-6):new Point(Direction(hit.side)*4,impact.z>=0?20:-20)).Normalized;
                // When a forward flank is free, clear upfield rather than
                // always hammering every interception across the touchline.
                // Keep emergency deflections near the byline and blocked lanes.
                if(!byline){
                    // A first-time clearance can use either open channel.
                    // Do not repeatedly send it down the already blocked flank.
                    float safest=.45f;Point outlet=new Point();
                    for(int channel=-2;channel<=2;channel++){
                        var candidate=new Point(Direction(hit.side)*16,channel*6);
                        var destination=impact+candidate;
                        if(Math.Abs(destination.z)>32.5f)continue;
                        float safety=Safety(hit,destination);
                        if(safety>safest){safest=safety;outlet=candidate;}
                    }
                    if(outlet.Length>0)exit=outlet.Normalized;
                }
                // This is a first-contact block, not a fully wound-up kick.
                // Dissipate incoming energy so it cannot launch another
                // full-power delivery while the defender barely moves.
                var clearance=exit*Math.Min(12,speed*.58f);float vertical=.7f;
                var facing=new Point((float)Math.Sin(hit.angle),(float)Math.Cos(hit.angle));
                bool preparedBoot=Point.Dot(facing,b.velocity.Normalized*-1)>.35f&&Point.Dot(facing,exit)>.35f;
                if(impactHeight>=.65f||!preparedBoot){
                    // The rendered high block turns the torso, not a kicking
                    // foot. It cannot choose an accurate upfield delivery from
                    // a fast chest/thigh impact. Reflect and absorb the actual
                    // incoming momentum, leaving the rebound contestable.
                    var normal=(impact-hit.position).Normalized;
                    if(normal.Length<.1f)normal=b.velocity.Normalized*-1;
                    float inward=Point.Dot(b.velocity,normal);
                    clearance=(b.velocity-normal*(Math.Min(0,inward)*1.25f))*(impactHeight>=.65f?.45f:.58f);
                    vertical=impactHeight>=.65f?-.7f:.35f;
                }
                if(preparedBoot&&impactHeight<.65f&&!b.goalAttempt)b.offsidePlayersMask=OffsideSnapshotKnown;
                LooseBall(impact,clearance,impactHeight,vertical,hit.side,hit.id);
                hit.controlTime=.45f;hit.action="block";hit.actionTime=.45f;hit.actionKind="clearance";hit.actionTarget=impact;hit.actionHeight=impactHeight;hit.actionSequence++;
                Emit("clearance",hit.side,hit.id,Data(hit).name+" écarte le centre sous la pression.");return true;
            }
            float difficulty=FirstTouchError(hit,speed,Space(hit.position,1-hit.side));
            if(speed>7&&Random()<difficulty*.36f){var direction=b.velocity.Normalized;LooseBall(impact,direction*(2.3f+difficulty*2.5f),impactHeight,.45f,hit.side,hit.id);hit.controlTime=.4f;hit.actionTarget=impact;hit.actionHeight=impactHeight;hit.actionSequence++;hit.action="miscontrol";hit.actionTime=.4f;State.metrics[hit.side].miscontrols++;Emit("miscontrol",hit.side,hit.id,Data(hit).name+" laisse échapper son contrôle.");return true;}
            if(!intercept&&pass)State.completedPasses[hit.side]++;b.position=impact;b.height=impactHeight;Control(hit);if(intercept)Emit("interception",hit.side,hit.id,Data(hit).name+" coupe la trajectoire.");return true;
        }
        bool ResolveAerial(float progress)
        {
            var b=State.ball;if(b.kind!="cross"||progress<.74f)return false;
            var aerial=SelectAerialDuel(out float first,out var contestant);
            if(aerial==null)return false;if(OffsideReception(aerial))return true;bool attack=aerial.side==b.side;if(attack)State.completedPasses[b.side]++;b.owner=aerial.id;b.side=aerial.side;b.lastTouch=aerial.side;b.lastTouchId=aerial.id;
            b.position=Point.Lerp(b.previous,b.position,first);b.height=b.previousHeight+(b.height-b.previousHeight)*first;
            // A short plant before take-off keeps the prepared head contact
            // reachable rather than coasting a full sprint step beyond it.
            aerial.velocity=new Point();
            if(attack&&ShotQuality(aerial,true)>(Tactic(aerial.side).workIntoBox?.12f:.07f)){var contact=b.position;float height=b.height;Shoot(aerial,true);BeginAerialContest(contestant,aerial,contact,height);return true;}
            DistributeHeader(aerial,contestant);return true;
        }
        bool InOwnArea(Actor keeper,Point point)=>point.x*Direction(keeper.side)< -36&&Math.Abs(point.z)<20.16f;
        bool OffsideReception(Actor p)
        {
            if(!InOffsideSnapshot(p))return false;State.metrics[p.side].offsides++;Emit("offside",p.side,p.id,"Hors-jeu de "+Data(p).name);Restart("free-kick",1-p.side,p.position,2);State.indirectRestart=true;return true;
        }
    }
}




using System;

namespace Touchline.Core
{
    public sealed partial class MatchSimulation
    {
        public Point MovementTarget(int index)=>targets[index];
        void Move()
        {
            if(State.ball.fixedStart&&State.ball.elapsed<0&&!HandDistribution(State.ball.kind)){
                if(State.restartTaker==null)MoveKickoffRestart();
                else foreach(var p in State.actors){if(p.sentOff)continue;var q=p.position;if(State.ball.penalty&&p.slot==0&&p.side!=State.restartSide)q=new Point(-Direction(p.side)*52.5f,0);MoveActor(p,q,4.8f);}
                ResolvePlayerContacts();return;
            }
            var m=State;var b=m.ball;var owner=Owner;var protectedBody=owner??CommittedKickerBody();var protectedKeeper=KeeperBodyInHands();int possession=PossessionSide(m);var transition=m.clock-m.turnoverAt<5;bool free=owner==null&&(b.kind=="none"||b.kind=="loose");
            for(int side=0;side<2;side++){firstPress[side]=coverPress[side]=-1;float first=10000,second=10000;
                var focus=DefensiveFocus(side);
                for(int i=0;i<22;i++){var p=m.actors[i];if(p.sentOff||p.side!=side||p.slot==0)continue;var d=Point.Distance(p.position,focus);if(Tactic(side).withoutBall[p.slot].y<36)d+=3;d+=(100-p.fitness)*.025f;
                    if(Tactic(side).withoutBall[p.slot].y<36&&b.position.x*Direction(side)<-30&&Math.Abs(b.position.z)<18)d-=2.65f;
                    if(d<first){second=first;coverPress[side]=firstPress[side];first=d;firstPress[side]=i;}else if(d<second){second=d;coverPress[side]=i;}}}
            for(int i=0;i<22;i++){
                var p=m.actors[i];
                // A run in behind lasts a fixed time whatever happens to the ball.
                if(p.runBehind>0)p.runBehind=Math.Max(0,p.runBehind-Step);
                if(PreparingFootDelivery(p)&&(b.owner!=p.id||p.sentOff)){p.action=p.velocity.Length>.4f?"run":"idle";p.actionTime=0;}
                if(p.sentOff){targets[i]=p.position;continue;}var dir=Direction(p.side);var has=possession==p.side;var t=Tactic(p.side);var slot=(has?t.withBall:t.withoutBall)[p.slot];var bx=b.position.x*dir;
                var q=t.Position(p.slot,has,bx);p.intent=has?"support":"shape";
                q.z+=b.position.z*dir*(has?.12f:.34f);
                if(!has&&p.slot>0&&slot.y<60){float danger=Mathx.Clamp((-bx-20)/22,0,1)*(1-Mathx.Clamp(Math.Abs(b.position.z)/24,0,1));q.z*=1-.45f*danger;}
                if(has&&p.slot>0){q.x+=slot.duty=="attack"?5:slot.duty=="defend"?-5:0;InstructionWithBall(p,t,ref q);
                    if(transition&&t.counterAttack&&slot.duty!="defend"){q.x+=8;p.intent="counter";}
                    if(slot.duty=="defend")q.x=Math.Min(q.x,bx-7);
                    if(bx>22&&slot.duty=="attack"){q.x=Math.Min(48,Math.Max(q.x,bx+7));p.intent="run-behind";}
                    if(owner!=null&&p!=owner&&slot.duty=="support"&&Point.Distance(p.position,owner.position)<18&&Safety(owner,p.position)<.45f){float lateral=p.position.z>=owner.position.z?1:-1;q.z+=lateral*3.5f*dir;q.x=Math.Min(q.x,bx+5);p.intent="show-for-pass";}
                    DeliverySupport(p,owner,ref q);
                    if(owner!=null&&p!=owner&&!RunBehind(p,owner,slot.duty,ref q))q.x=Math.Min(q.x,OffsideLine(p.side)-.9f);
                }
                if(!has&&p.slot>0){InstructionWithoutBall(p,t,ref q);if(slot.y<38)q.x-=LineStagger(p);}
                q=q*dir;
                if(p.slot==0){p.intent="keeper";q=KeeperTarget(p);}
                else if(p==owner){if(Point.Distance(p.carryTarget,p.position)<1)m.decision=Math.Min(m.decision,.1f);q=p.carryTarget;p.intent="carry";if(p.controlTime>0)q=p.position;}
                // Until contact, keep the visible supporting run instead of chasing the random endpoint.
                else if(b.to==p.id&&b.kind!="none"&&b.elapsed>=0){q=ReceptionTarget(p);p.intent="receive";}
                else if(!has){
                    var focus=DefensiveFocus(p.side);float focusX=focus.x*dir;
                    if(slot.y<38&&bx< -24)q.x=dir*Math.Max(-49.2f,Math.Min(q.x*dir,bx-3.2f));
                    bool recovering=transition&&!t.counterPress;
                    // Un porteur dans son propre tiers défensif est pressé par le
                    // joueur le plus proche dès que l'équipe presse un minimum.
                    bool highPress=focusX>HighPressZone&&t.pressing>=HighPressMinPressing;
                    // The engagement zone belongs to pressing intensity. Line height
                    // already positions the block; raising it must not also order a press.
                    bool trigger=focusX< -28+t.pressing*55||Space(focus,p.side,true)<4||free||highPress;
                    bool closeDelay=recovering&&Point.Distance(p.position,focus)<7;
                    // Ballon perdu haut : le plus proche presse aussitôt au lieu de se replier.
                    bool press=trigger&&(!recovering||closeDelay||highPress)||transition&&t.counterPress;
                    float radius=(5+t.pressing*18+(transition&&t.counterPress?7:0))*(1+Drive(p)*DrivePressReach);
                    if(firstPress[p.side]==i&&press&&Point.Distance(p.position,focus)<radius){q=focus-new Point(dir*(closeDelay?1.4f:.65f),0);p.intent=closeDelay?"delay":"press";}
                    else if(coverPress[p.side]==i&&press&&t.pressing>.35f&&Point.Distance(p.position,focus)<(radius+3)*(1+Understanding(p.side)*UnderstandingCoverReach)){q=focus+(new Point(-dir*52.5f,0)-focus).Normalized*(4.8f-t.pressing*2);p.intent="cover";}
                    else{
                        // Track a runner only inside this player's zone; retain cover.
                        bool tight=Instruction(t,p.slot)==TightMarking;Actor threat=null;float best=tight?TightMarkingReach:DefaultMarkingReach;foreach(var o in m.actors)if(!o.sentOff&&o.side!=p.side&&o.slot>0){float d=Point.Distance(o.position,q);if(d<best&&o.position.x*dir<p.position.x*dir+12){best=d;threat=o;}}
                        if(slot.y<60)threat=CloseGoalSideMark(p,threat,m.actors,dir);
                        if(threat!=null&&slot.y<60){q=Point.Lerp(q,threat.position-new Point(dir*2,0),(tight?TightMarkingPull:.30f)+Skill(p,"defensiveAwareness")*.003f);p.intent=threat==owner?"mark-carrier":"mark";}
                        if(focusX< -24&&slot.y<60){float behind=slot.y<38?4.2f:1.2f;q.x=dir*Math.Max(-49,Math.Min(q.x*dir,focusX-behind));}
                        if(recovering){q.x-=dir*5;p.intent="recover";}
                    }
                }
                // Against a cross the two nearest defenders attack the flight
                // itself, like the intended receiver does, instead of standing
                // on the attacker's body while the ball drops over them.
                if(!has&&p.slot>0&&b.kind=="cross"&&b.elapsed>=0&&string.IsNullOrEmpty(b.owner)&&b.side!=p.side&&(firstPress[p.side]==i||coverPress[p.side]==i)&&Point.Distance(p.position,b.end)<14){q=ReceptionTarget(p);p.intent="attack-cross";}
                if(free&&firstPress[p.side]==i){q=b.position+b.velocity*Mathx.Clamp(Point.Distance(p.position,b.position)/9,0,.65f);p.intent="loose-ball";}
                if(protectedBody!=null&&p.side!=protectedBody.side&&p.slot>0)q=ShieldSafePressTarget(p,protectedBody,q);
                if(protectedKeeper!=null&&p!=protectedKeeper)q=KeeperBodySafeTarget(p,protectedKeeper,q);
                if(p.action=="tackle"&&p.actionKind==StandingDuel&&p.actionTime>0){q=p.position;p.intent="duel";}
                if(PreparingFootDelivery(p)){q=p.position;p.intent="prepare-delivery";}
                if(p.actionTime>0&&(p.action=="kick"||p.action=="header"||p.action=="dive"||p.action=="claim"||p.action=="throw"||p.action=="keeper-hold"||p.action=="place-ball"||p.action=="keeper-rise"||HandDistribution(p.action)))q=p.position;
                if(ContactMobility(p)>0)foreach(var other in m.actors){if(other==p||other.sentOff)continue;var delta=p.position-other.position;float d=delta.Length;if(d<1.35f&&d>.02f)q+=delta/d*((1.35f-d)*1.5f);}
                // Formation margins must not stop a player reaching a ball
                // that is still in play beside the touchline or goal line.
                bool pursuing=p.intent=="loose-ball"||p.intent=="receive"||p.intent=="sweep"||TrackingCarrier(p);
                float limit=pursuing||p.slot==0&&b.penalty&&b.elapsed<0?52.5f:51.5f;
                float lateralLimit=pursuing?33.8f:32.5f;
                // Legal receptions can place a body beyond the formation's
                // interior margin. A planted kick/tackle must not slide back
                // inside solely because its stationary waypoint is clamped.
                if(p.intent=="shape"){float understanding=Understanding(p.side);if(understanding<0)q=Point.Lerp(q,p.position,-understanding*ShapeLagShare);}
                targets[i]=ContactMobility(p)==0?p.position:new Point(Mathx.Clamp(q.x,-limit,limit),Mathx.Clamp(q.z,-lateralLimit,lateralLimit));
            }
            for(int i=0;i<22;i++){
                var p=m.actors[i];if(p.sentOff)continue;float maxSpeed=(4.1f+Skill(p,"sprintSpeed")*.043f)*(.74f+p.fitness*.0026f);
                if(p.injured)maxSpeed*=.65f;if(p==owner)maxSpeed*=.80f;if(p.intent=="shape"||p.intent=="support")maxSpeed*=.76f*(1+Drive(p)*DriveShapeSpeed);
                if(p.intent=="press")maxSpeed*=(.83f+Tactic(p.side).pressing*.17f)*(1+Drive(p)*DrivePressSpeed);
                MoveActor(p,targets[i],maxSpeed);
                p.fitness=Mathx.Clamp(p.fitness-(Step*State.LegacyTimeScale)*(.012f+p.velocity.Length*p.velocity.Length*.0006f)*(1.2f-Skill(p,"stamina")*.004f)*FatigueFactor(p),15,100);
            }
            ResolvePlayerContacts();
        }
        // Meet a travelling pass at a reachable point instead of blindly running
        // to its original endpoint. Heights exclude balls still out of reach.
        public Point ReceptionTarget(Actor player)
        {
            var b=State.ball;
            if(b.duration<=0||b.kind=="none"||b.kind=="loose")return b.end;
            float remaining=Math.Max(0,b.duration-b.elapsed);
            float topSpeed=(4.1f+Skill(player,"sprintSpeed")*.043f)*(.74f+player.fitness*.0026f)*(player.injured?.65f:1);
            float acceleration=3+Skill(player,"acceleration")*.055f;
            float receptionHeight=b.kind=="cross"?(player.slot==0?2.3f:HeaderMaximumHeight(player)):1.05f;
            for(float time=.12f;time<remaining;time+=.12f){
                float elapsed=b.elapsed+time;if(elapsed<0)continue;
                float u=Mathx.Clamp(elapsed/b.duration,0,1);
                float height=b.startHeight+(b.endHeight-b.startHeight)*u+(float)Math.Sin(Math.PI*u)*b.loft;
                if(height>receptionHeight)continue;
                var point=Point.Lerp(b.start,b.end,u);var delta=point-player.position;
                float initial=Math.Max(0,Point.Dot(player.velocity,delta.Normalized));
                float ramp=Mathx.Clamp((topSpeed-initial)/acceleration,0,time);
                float reach=initial*ramp+.5f*acceleration*ramp*ramp+topSpeed*(time-ramp);
                if(delta.Length<=reach+.55f)return point;
            }
            return b.end;
        }
        Point KeeperTarget(Actor p)
        {
            var b=State.ball;var dir=Direction(p.side);var depth=Math.Max(1,b.position.x*dir+52.5f);var offset=Mathx.Clamp(depth*.11f,1,4.5f+Tactic(p.side).line*1.5f);
            var q=new Point(dir*(-52.5f+offset),Mathx.Clamp(b.position.z*offset/depth,-3.2f,3.2f));
            if(b.penalty&&b.kind=="shot"&&b.elapsed<0)return new Point(-dir*52.5f,0);
            if(b.to==p.id&&b.side==p.side&&b.elapsed>=0)return ReceptionTarget(p);
            var owner=Owner;
            if(owner!=null&&owner.side!=p.side&&depth<17&&Math.Abs(b.position.z)<10){
                float cover=Space(b.position,p.side,true);
                if(cover>4){
                    float advance=Mathx.Clamp(depth*.43f,1.5f,7);
                    // Stand on the angular bisector of the two goalposts.
                    // The same forward advance then leaves equal shot angles
                    // on either side, including an attacker approaching wide.
                    var nearPost=new Point(-depth,3.66f-b.position.z).Normalized;
                    var farPost=new Point(-depth,-3.66f-b.position.z).Normalized;
                    var bisector=nearPost+farPost;
                    float lateral=b.position.z+(advance-depth)*bisector.z/bisector.x;
                    q=new Point(dir*(-52.5f+advance),Mathx.Clamp(lateral,-5,5));p.intent="close-angle";
                }
            }
            if(owner==null&&(b.kind=="loose"||b.kind=="none"||b.kind=="through")&&InOwnArea(p,b.position)&&b.height<1.6f){
                var intercept=b.position+b.velocity*.3f;float keeperTime=Point.Distance(p.position,intercept)/6;
                float attackerTime=Space(intercept,1-p.side)/7.5f;
                if(InOwnArea(p,intercept)&&keeperTime+.15f<attackerTime){q=intercept;p.intent="sweep";}
            }
            return AnticipateKeeperShot(p,q);
        }
        void MoveActor(Actor p,Point target,float maxSpeed)
        {
            if(AdvanceKeeperLaunch(p))return;
            if(AdvanceAerialAction(p))return;
            if(AdvanceGroundAction(p))return;
            var delta=target-p.position;float speed=Math.Min(delta.Length*2,maxSpeed);var desired=delta.Normalized*speed;var acceleration=3+Skill(p,"acceleration")*.055f;
            var carrier=Owner??CommittedKickerBody();
            if(carrier!=null&&p.side!=carrier.side&&p.slot>0&&TrackingCarrier(p)&&(p.action=="run"||p.action=="idle")&&Point.Distance(p.position,carrier.position)<6){
                // The pressure waypoint moves with the carrier. Feed forward
                // that motion, then correct the remaining distance; otherwise
                // pursuing players permanently trail a moving shoulder point.
                desired+=carrier.velocity;if(desired.Length>maxSpeed)desired=desired.Normalized*maxSpeed;
            }
            if(carrier!=null&&p.side!=carrier.side&&p.slot>0&&(p.action=="run"||p.action=="idle"))desired=ControlledPressVelocity(p,carrier,desired,maxSpeed);
            if(p.velocity.Length>1&&Point.Dot(p.velocity.Normalized,desired.Normalized)<.2f)acceleration*=1.25f;
            var protectedKeeper=KeeperBodyInHands();
            if(protectedKeeper!=null&&p!=protectedKeeper&&(p.action=="run"||p.action=="idle"))desired=KeeperBodySafeVelocity(p,protectedKeeper,desired,maxSpeed);
            var change=desired-p.velocity;var maxChange=acceleration*Step;if(change.Length>maxChange)change=change.Normalized*maxChange;
            p.velocity+=change;p.position+=p.velocity*Step;p.position.x=Mathx.Clamp(p.position.x,-52.5f,52.5f);p.position.z=Mathx.Clamp(p.position.z,-33.8f,33.8f);p.stride+=p.velocity.Length*Step;
            var look=p.action=="tackle"&&p.actionKind==StandingDuel?p.actionTarget-p.position:p.slot==0?State.ball.position-p.position:p.velocity;
            look=KeeperDistributionLook(p,look);
            if(PreparingFootDelivery(p))look=p.actionTarget-p.position;
            // Slow jockeying and retreat keep the body facing the ball, as
            // their rendered defensive stance already does. Facing velocity
            // here would make a backpedalling defender unable to attempt a
            // legal poke visibly in front of him.
            if(carrier!=null&&p.side!=carrier.side&&p.slot>0&&(p.action=="run"||p.action=="idle")&&p.velocity.Length<3.2f&&Point.Distance(p.position,State.ball.position)<6)look=State.ball.position-p.position;
            if(look.Length>.25f&&p.action!="kick"&&p.action!="dive"&&p.action!="header"&&p.action!="throw"){float angle=(float)Math.Atan2(look.x,look.z);float turn=angle-p.angle;while(turn>Math.PI)turn-=(float)Math.PI*2;while(turn< -Math.PI)turn+=(float)Math.PI*2;p.angle+=Mathx.Clamp(turn,-Step*5,Step*5);}
            bool footPreparation=PreparingFootDelivery(p);
            p.actionTime=Math.Max(0,p.actionTime-Step);if(p.actionTime==0&&!FinishKeeperAction(p)){p.action=p.velocity.Length>.4f?"run":"idle";if(footPreparation)p.actionKind=PreparedFootDelivery;}
        }
    }
}


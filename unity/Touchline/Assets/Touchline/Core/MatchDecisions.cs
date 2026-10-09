using System;  
using System.Collections.Generic;  
  
namespace Touchline.Core  
{  
 public sealed partial class MatchSimulation  
 {  
 public static float Projection(Point a,Point b,Point p){var delta=b-a;return Mathx.Clamp(Point.Dot(p-a,delta)/Math.Max(.00001f,Point.Dot(delta,delta)),0,1);}  
 public static float LaneSafety(Point from,Point to,IEnumerable<Actor> opponents)  
 {  
 float d=Point.Distance(from,to),risk=0;foreach(var p in opponents){var u=Projection(from,to,p.position);if(u<.04f)continue;var gap=Point.Distance(p.position,Point.Lerp(from,to,u));var reach=.7f+Math.Max(0,d*u/21-.22f)*4.8f;risk=Math.Max(risk,Mathx.Clamp((reach+.8f-gap)/(reach+.8f),0,1));}return 1-risk;  
 }  
 float Safety(Actor from,Point end,bool aerial=false)  
 {  
 float risk=0,d=Point.Distance(from.position,end);foreach(var p in State.actors)if(!p.sentOff&&p.side!=from.side){float f=Projection(from.position,end,p.position);if(f<.04f||aerial&&f>.12f&&f<.78f)continue;  
 // A player still on the ground at arrival cannot intercept. If  
 // he recovers in time, only the remaining interval allows travel.  
 float arrival=.18f+d*f/20;  
 if(GroundedAction(p)&&p.actionTime>arrival)continue;  
 float reaction=.30f+(100-Skill(p,"interceptions"))*.004f;  
 if(GroundedAction(p))reaction=Math.Max(reaction,p.actionTime-.18f);  
 float reach=.65f+Math.Max(0,d*f/20-reaction)*(3+Skill(p,"acceleration")*.026f);float gap=Point.Distance(p.position,Point.Lerp(from.position,end,f));risk=Math.Max(risk,Mathx.Clamp((reach+.65f-gap)/(reach+.65f),0,1));}return 1-risk;  
 }  
 public float ShotQuality(Actor p,bool header=false)  
 {  
 int dir=Direction(p.side);float depth=Math.Max(.5f,52.5f-p.position.x*dir),z=p.position.z;  
 float opening=(float)(Math.Atan2(3.66f-z,depth)-Math.Atan2(-3.66f-z,depth));float d=Point.Distance(p.position,new Point(dir*52.5f,0));  
 float pressure=Mathx.Clamp(Space(p.position,1-p.side,true)/5,.35f,1);float quality=(float)(1/(1+Math.Exp(-(-.4f-d*.15f+opening*1.4f))))*pressure;  
 return Mathx.Clamp(quality*(header?.65f:1),.01f,.75f);  
 }  
 float ShotPlacement(Actor player,float finishing)=>Mathx.Clamp((finishing-35)/55,0,1)*Mathx.Clamp(Space(player.position,1-player.side,true)/4,.4f,1);  
 float AvailableShotWidth(Actor player)=>.2f+(1.4f+1.5f*ShotPlacement(player,Skill(player,"finishing")))*.85f;  
 bool ShootingLaneClear(Actor player,float goalZ)  
 {  
 int dir=Direction(player.side);var goal=new Point(dir*52.5f,goalZ);  
 foreach(var opponent in State.actors){  
 if(opponent.sentOff||opponent.side==player.side||opponent.slot==0||opponent.position.x*dir<=player.position.x*dir)continue;  
 var intercept=Point.Lerp(player.position,goal,Projection(player.position,goal,opponent.position));  
 float arrival=.18f+Point.Distance(player.position,intercept)/(23+Skill(player,"shotPower")*.10f);  
 if(GroundedAction(opponent)&&opponent.actionTime>arrival)continue;  
 float gap=Point.Distance(opponent.position,intercept);  
 if(gap<1.2f)return false;  
 }  
 return true;  
 }  
 public string Decide(Actor p)  
 {  
 if(State.ball.owner==p.id&&(State.ball.held||p.action=="keeper-rise"))return "hold";  
 if(State.ball.owner==p.id&&PreparingFootDelivery(p)){State.decision=Step;return "prepare";}  
 bool prepared=p.actionKind==PreparedFootDelivery;if(prepared)p.actionKind=null;  
 var m=State;int dir=Direction(p.side);var t=Tactic(p.side);float pressure=Space(p.position,1-p.side,true);float distance=Point.Distance(p.position,new Point(dir*52.5f,0));  
 float depth=Math.Max(.5f,52.5f-p.position.x*dir);float opening=(float)(Math.Atan2(3.66f-p.position.z,depth)-Math.Atan2(-3.66f-p.position.z,depth));  
 float availableWidth=AvailableShotWidth(p);  
 bool clear=ShootingLaneClear(p,0)||ShootingLaneClear(p,availableWidth)||ShootingLaneClear(p,-availableWidth);  
 float quality=ShotQuality(p);bool counter=t.counterAttack&&m.clock-m.turnoverAt<5;  
   
Actor best=null;float bestScore=-100;string kind="pass";  
 float line=OffsideLine(p.side),preferred=8+t.directness*26;  
 // The passer reads the defensive line from distance and in motion: he
 // may see a team-mate who has just strayed beyond it as onside (up to
 // about 1.5 m for a poor reader, under 0.8 m for the best).
 // Only rolled when such a team-mate exists, so most decisions keep
 // their usual draw sequence.
 // Le passeur juge la position d'un coéquipier en mouvement avec un temps de retard
 // (0,3 s pour une vision de 100, 0,9 s pour 0) : un appel lancé peut déjà être hors-jeu.
 float readLag=PassReadLagMin+(100-Skill(p,"vision"))*PassReadLagPerPoint; float lineRead=line+.1f;foreach(var mate in m.actors)if(!mate.sentOff&&mate.side==p.side&&mate!=p&&mate.slot>0){float beyond=mate.position.x*dir-line;if(beyond>.1f&&beyond<2f){lineRead+=Random()*Mathx.Clamp(2.0f-Skill(p,"vision")*.013f,.1f,1.8f);break;}}
 foreach(var mate in m.actors){if(mate.sentOff||GroundedAction(mate)||mate==p||mate.side!=p.side||(mate.position.x-mate.velocity.x*readLag)*dir>lineRead)continue;float d=Point.Distance(p.position,mate.position);if(d<3||d>22+t.directness*36)continue;  
 float forward=(mate.position.x-p.position.x)*dir;float space=Space(mate.position,1-p.side);  
 bool wing=Math.Abs(p.position.z)>19&&p.position.x*dir>27&&mate.position.x*dir>36&&Math.Abs(mate.position.z)<13;  
 bool cutback=wing&&p.position.x*dir>42&&forward< -2;  
 bool runsBehind=mate.velocity.x*dir>1.5f&&(mate.position.x+mate.velocity.x*d/20)*dir>line+1;  
 string candidate=cutback?"cutback":wing&&forward>=-2?"cross":Math.Abs(mate.position.z-p.position.z)>29?"switch":forward>8&&mate.slot>0&&runsBehind&&(space>4||Space(mate.position+new Point(dir*ThroughSpaceAhead,0),1-p.side)>4)?"through":"pass";  
 bool aerial=candidate=="cross"?!LowCross(p,mate):candidate=="switch"||d>30;var projected=PassTarget(p,mate,candidate);if(prepared&&FootDeliveryTurn(p,projected)>Math.PI/3)continue;float safety=candidate=="cross"?CrossOutlook(p,mate,!aerial):Math.Min(Safety(p,mate.position,aerial),Safety(p,projected,aerial));if(safety<(candidate=="cross"?.12f:candidate=="through"?ThroughMinSafety:.24f+(1-t.directness)*.16f))continue;  
 float score=10*safety+forward*(.10f+t.directness*.28f+(counter?.22f:0))+Math.Min(space,9)*.4f-Math.Abs(d-preferred)*.30f+(pressure<4?4:0)+Math.Min(m.carryTime,5)*.85f;  
 score+=Math.Max(0,ShotQuality(mate)-quality)*28*safety;  
 if(ProtectingLead(p.side)){score+=safety*2;if(forward>12&&safety<.7f)score-=4;}  
 if(candidate=="through")score+=ThroughBonus*(.5f+t.directness);if(candidate=="cross")score+=1.5f+Math.Max(0,ShotQuality(mate,true)-.08f)*14;if(candidate=="cutback")score+=Math.Max(0,ShotQuality(mate)-quality)*20;  
 if(forward> -5&&mate.slot>0){  
 float flank=mate.position.z*dir;  
 if(t.attackFocus=="left")score+=flank>10?3*safety:flank< -10?-1.5f:0;  
 else if(t.attackFocus=="right")score+=flank< -10?3*safety:flank>10?-1.5f:0;  
 else if(t.attackFocus=="centre")score+=Math.Abs(flank)<13?2.5f*safety:-1;  
 }  
 if(t.workIntoBox){  
 // Patient attacks seek a usable finish or recycle. Do not  
 // keep the generic crossing bonus for a tightly marked,  
 // low-quality header simply because the ball is wide.  
 if(candidate=="cross") score += (ShotQuality(mate,true)-.12f)*20-7; // PATCH 0.42: -7 au lieu de -3
 if(candidate=="cutback") score += 5*safety; // PATCH 0.42: Bonus augmenté à 5
 if(candidate=="through" || candidate=="pass") score += 4*safety; // PATCH 0.42: Favorise activement la possession au sol 
 }  
 if(mate.slot==0&&p.position.x*dir>0)score-=7;  
 if(forward< -4&&pressure>4&&space<pressure)score-=5;  
 // A weak passer under pressure recognises fewer ambitious options.  
 score-=aerial?(100-Skill(p,"vision"))*.02f:0;score+=(Random()-.5f)*(1.8f-Skill(p,"vision")*.01f);  
 // Servir un partenaire marqué de près hors de la zone de finition, quand
 // on pourrait jouer ailleurs, revient souvent à lui faire perdre le ballon.
 if((candidate=="pass"||candidate=="switch")&&mate.position.x*dir<MarkedReceiverZone)score-=MarkedReceiverCost(space);
 score+=PassPreference(p,candidate,d);if(score>bestScore){bestScore=score;best=mate;kind=candidate;}  
 }  
 float passChance=best==null?0:ShotQuality(best,kind=="cross"&&!LowCross(p,best))*Safety(p,PassTarget(p,best,kind),kind=="cross"?!LowCross(p,best):kind=="switch");  
 // Chance (xG) a distant shot must beat to be preferred to keeping the ball.
 float patientValue=LongShotPatience-t.mentality*LongShotMentalityShift+(counter?-.008f:0);  
 // A chasing defender behind the striker does not close the goal  
 // window. Take a clear central finish unless a genuinely better  
 // passing chance is available. Patient play still allows this shot.  
 bool closeChance=distance<20&&opening>.26f&&(clear||pressure>2.3f||distance<8)&&(!t.workIntoBox||quality>.10f||clear&&distance<18&&opening>.4f||distance<10);  
 bool closeFinish=distance<8&&opening>.6f&&pressure>.65f;  
 if((!prepared||FootDeliveryTurn(p,new Point(dir*52.5f,0))<=Math.PI/3)&&p.slot>0&&closeChance&&quality>passChance*(closeFinish?.72f:.82f)&&(clear||pressure>3.1f||closeFinish)){if(BeginFootDeliveryPreparation(p,new Point(dir*52.5f,0),"shot"))return "prepare";Shoot(p);return "shot";}  
 if((!prepared||FootDeliveryTurn(p,new Point(dir*52.5f,0))<=Math.PI/3)&&p.slot>0&&opening>.19f&&distance<27&&!t.workIntoBox&&(clear||pressure>LongShotClearSpace)&&pressure>LongShotMinSpace&&m.carryTime>LongShotSetTime&&quality*DistantShotPreference(p,distance)>Math.Max(patientValue,passChance*.95f)){if(BeginFootDeliveryPreparation(p,new Point(dir*52.5f,0),"shot"))return "prepare";Shoot(p);return "shot";}  
 // Engagement : un joueur qui a orienté son corps pour frapper (préparation
 // vers le centre du but, voir ci-dessus) frappe tant que l'angle reste
 // ouvert, au lieu de repartir en conduite ; le contre fait partie du jeu.
 bool preparedShot=prepared&&Point.Distance(p.actionTarget,new Point(dir*52.5f,0))<.01f;
 if(preparedShot&&p.slot>0&&FootDeliveryTurn(p,new Point(dir*52.5f,0))<=Math.PI/3&&opening>PreparedShotMinOpening&&distance<PreparedShotMaxDistance&&passChance<quality*PreparedShotPassMargin){Shoot(p);return "shot";}
 var carry=ChooseCarry(p);p.carryTarget=carry;float progress=(carry.x-p.position.x)*dir;  
 float carryScore=(p.position.x*dir>12?18:7)+Math.Min(Space(carry,1-p.side),8)*.5f+progress*.35f+(Skill(p,"dribbling")-65)*.055f-(pressure<3?5:0)-Math.Min(m.carryTime,6)*1.2f;  
  
 carryScore+=CarryPreference(p);  
 // Conduire dans plusieurs adversaires coûte le ballon.
 carryScore-=CarryCrowdCost(p,carry);
 // Engagement : un joueur qui vient d'orienter son corps pour donner le
 // ballon joue la passe si elle reste disponible, au lieu de repartir en
 // conduite (arrêt, pivot, puis départ dans une autre direction).
 if(prepared&&best!=null)carryScore-=PreparedDeliveryCommitment;
 // In the crossing zone a wide player delivers rather than dribbling to
 // the byline every time; good crossers more readily.
 if(p.slot>0&&Math.Abs(p.position.z)>17&&p.position.x*dir>25&&kind=="cross")carryScore-=3+(Skill(p,"crossing")-60)*.06f;
 // Safety first: a pressed defender in his own third, or a full-back
 // pinned on his own touchline, does not gamble on a risky pass.
 bool pinned=p.slot>0&&pressure<ClearancePressure&&(p.position.x*dir< -26||p.position.x*dir<0&&Math.Abs(p.position.z)>20);
 if(pinned&&best!=null&&Safety(p,PassTarget(p,best,kind))<PinnedPassSafety)return Clear(p,dir);
 if(best!=null&&(p.slot==0||bestScore>carryScore)){if(BeginFootDeliveryPreparation(p,PassTarget(p,best,kind),kind))return "prepare";Pass(p,best,kind);return kind;}  
 if(p.slot>0&&p.position.x*dir<-26&&pressure<ClearancePressure&&(best==null||Safety(p,PassTarget(p,best,kind))<.6f))return Clear(p,dir);
 if(p.slot==0&&(m.carryTime>2||pressure<3)){Flight(p,null,"clearance",new Point(dir*8,Math.Sign(p.position.z+.01f)*27),.11f,2.6f,8);Emit("clearance",p.side,p.id,Data(p).name+" allonge pour sortir du pressing.");return "clearance";}  
 return "carry";  
 }  
 // A pressed defender clears: long upfield when that channel is open,
 // otherwise over the nearest touchline.
 string Clear(Actor p,int dir)
 {
 float flank=p.position.z>=0?1:-1;var upfield=new Point(Mathx.Clamp(p.position.x+dir*28,-48,48),flank*26);
 bool open=Safety(p,upfield,true)>OpenClearanceSafety;var target=open?upfield:new Point(Mathx.Clamp(p.position.x+dir*16,-48,48),flank*36);
 Flight(p,null,"clearance",target,.11f,2.0f,3.5f);
 Emit("clearance",p.side,p.id,Data(p).name+(open?" allonge pour écarter le danger.":" dégage en touche."));
 return "clearance";
 }
 Point ChooseCarry(Actor p)  
 {  
 var dir=Direction(p.side);var tactic=Tactic(p.side);var slot=tactic.withBall[p.slot];Point best=p.position;float bestScore=-100;  
 bool wideRole=slot.role=="LW"||slot.role=="RW"||slot.role=="LM"||slot.role=="RM"||slot.role=="LB"||slot.role=="RB";  
 float assignedFlank=tactic.Position(p.slot,true,p.position.x*dir).z*dir;  
 // A winger or overlapping fullback carries in his assigned lane.  
 // He may still cut inside around a closing defender, but should not  
 // abandon width solely because every dribbler prefers field centre.  
 float laneWeight=wideRole?(tactic.attackFocus=="centre"?.035f:.12f):.035f;  
 if(wideRole&&tactic.workIntoBox&&p.position.x*dir>35)laneWeight*=.5f;  
 for(int i=-5;i<=5;i++){float angle=i*.62f;var end=p.position+new Point(dir*(float)Math.Cos(angle)*6,(float)Math.Sin(angle)*6);end.x=Mathx.Clamp(end.x,-50.5f,50.5f);end.z=Mathx.Clamp(end.z,-32,32);  
 float progress=(end.x-p.position.x)*dir;float space=Space(end,1-p.side);float lane=CarryLaneSafety(p,end);float score=lane*7+Math.Min(space,9)*.6f+progress*(ProtectingLead(p.side)?.12f:.7f)-Math.Abs(end.z-(wideRole?assignedFlank:0))*laneWeight;  
 if(Point.Dot(p.velocity.Normalized,(end-p.position).Normalized)<0)score-=2;score-=CarryCrowdCost(p,end);if(score>bestScore){bestScore=score;best=end;}}  
 return best;  
 }  
 float CarryLaneSafety(Actor carrier,Point end)  
 {  
 float distance=Point.Distance(carrier.position,end),risk=0;  
 float speed=Math.Max(2.5f,carrier.velocity.Length);  
 foreach(var opponent in State.actors){  
 if(opponent.sentOff||opponent.side==carrier.side||GroundedAction(opponent))continue;  
 float along=Projection(carrier.position,end,opponent.position);if(along<.04f)continue;  
 var point=Point.Lerp(carrier.position,end,along);var toPoint=point-opponent.position;  
 float time=distance*along/speed;  
 float reaction=.30f+(100-Skill(opponent,"interceptions"))*.004f;  
 float available=Math.Max(0,time-reaction);  
 float initial=Point.Dot(opponent.velocity,toPoint.Normalized);  
 float acceleration=3+Skill(opponent,"acceleration")*.055f;  
 float top=(4.1f+Skill(opponent,"sprintSpeed")*.043f)*(.74f+opponent.fitness*.0026f);  
 float travel=Math.Min(top*time,Math.Max(0,initial*time+.5f*acceleration*available*available));  
 float reach=.65f+travel;  
 risk=Math.Max(risk,Mathx.Clamp((reach+.65f-toPoint.Length)/(reach+.65f),0,1));  
 }  
 return 1-risk;  
 }  
 void Pass(Actor from,Actor to,string kind)  
 {  
 bool low=kind=="cross"&&LowCross(from,to);var tactic=Tactic(from.side);  
 float d=Point.Distance(from.position,to.position);float duration=Math.Max(.3f,d/DeliverySpeed(from,to,kind));  
 float skill=Skill(from,kind=="cross"?"crossing":d>27?"longPassing":"shortPassing");float pressure=Math.Max(0,3-Space(from.position,1-from.side));  
 // A long aerial ball is much harder to land on a team-mate than a pass
 // along the ground: its error is scaled by LongBallError.
 // Tempo already changes decision cadence and ball speed (hence reception
 // difficulty). Do not also worsen an otherwise identical prepared pass.
 float error=(1-skill/105)*(Random()-.5f)*(Math.Min(14,d*.30f)+pressure*2+BasePassExecutionUncertainty)*(kind=="switch"||d>30&&kind!="cross"?LongBallError:1);
 var end=PassTarget(from,to,kind)+new Point(error,error*(Random()<.5f?-1:1));
 // A lofted long ball is judged on its length: weight it wrongly and it
 // sails long (more often than short) and can carry over the touchline.
 if(kind=="switch"||d>30&&kind!="cross"){var along=(end-from.position).Normalized;end+=along*((1-skill/105)*(Random()-LongBallOverhitBias)*d*LongBallLengthError);}
 end.x=Mathx.Clamp(end.x,-54,54);end.z=Mathx.Clamp(end.z,-35,35);  
 Flight(from,to,kind,end,kind=="cross"&&!low?1.6f:.11f,duration,kind=="cross"?(low?.12f:tactic.crossing=="floated"?5:3.5f):kind=="throw"?1.5f:d>30?4:.08f);State.passes[from.side]++;  
 var metrics=State.metrics[from.side];metrics.passDistance+=d;metrics.forwardPassDistance+=(end.x-from.position.x)*Direction(from.side);if(d>27)metrics.longPasses++;if(kind=="cross"||kind=="cutback")metrics.crosses++;if(kind=="through")metrics.throughBalls++;  
 if(kind=="cross"){if(low)metrics.lowCrosses++;else metrics.aerialCrosses++;}  
 if(end.x*Direction(from.side)>20){if(end.z*Direction(from.side)>10)metrics.leftAttackPasses++;else if(end.z*Direction(from.side)<-10)metrics.rightAttackPasses++;}  
 Emit(kind,from.side,from.id,Data(from).name+(kind=="through"?" lance ":kind=="cutback"?" trouve en retrait ":kind=="cross"?(low?" centre à ras de terre vers ":" centre vers "):kind=="switch"?" renverse vers ":" sert ")+Data(to).name+".",to.id);  
 }  
 // A cross is a contest, not a pass through a free lane: estimate the
 // share of such deliveries the target wins, from his aerial (or first
 // touch for a driven low ball) against the best defender and the keeper
 // near the landing point, and the crosser's delivery.
 float CrossOutlook(Actor from,Actor mate,bool low)
 {
 float threat=0;var land=mate.position;
 foreach(var o in State.actors){
 if(o.sentOff||o.side==from.side||GroundedAction(o))continue;float d=Point.Distance(o.position,land);
 if(o.slot==0){if(!low&&d<7)threat=Math.Max(threat,Skill(o,"gkPositioning")*.85f*(1-d/7));continue;}
 if(d<3.5f)threat=Math.Max(threat,(low?Skill(o,"interceptions"):(Skill(o,"headingAccuracy")+Skill(o,"jumping"))*.5f)*(1-d/3.5f));
 }
 float attack=low?Skill(mate,"ballControl"):(Skill(mate,"headingAccuracy")+Skill(mate,"jumping"))*.5f;
 return Mathx.Clamp(.30f+(attack-threat)*.006f+(Skill(from,"crossing")-65)*.004f,.05f,.85f);
 }
 bool LowCross(Actor from,Actor to)  
 {  
 var tactic=Tactic(from.side);if(tactic.crossing=="low")return true;if(tactic.crossing=="floated")return false;  
 return Point.Distance(from.position,to.position)<22&&Safety(from,to.position)>.65f;  
 }  
 float DeliverySpeed(Actor from,Actor to,string kind)=>kind=="cross"?(LowCross(from,to)?22:Tactic(from.side).crossing=="floated"?16:18):17+Tactic(from.side).tempo*5;  
 Point PassTarget(Actor from,Actor to,string kind)  
 {  
 float distance=Point.Distance(from.position,to.position),duration=Math.Max(.3f,distance/DeliverySpeed(from,to,kind));var end=to.position+to.velocity*(duration*.4f);if(kind=="through")end+=new Point(Direction(from.side)*Math.Min(5,distance*.1f),0);return new Point(Mathx.Clamp(end.x,-50.8f,50.8f),Mathx.Clamp(end.z,-32.5f,32.5f));  
 }  
 void Flight(Actor from,Actor to,string kind,Point end,float endHeight,float duration,float loft)  
 {  
 State.ball.fixedStart=false;State.ball.held=false;State.ball.keeperDistribution=false;  
 var b=State.ball;b.offsidePlayersMask=b.restartExemption?OffsideSnapshotKnown:OffsideSnapshotPending;b.offside=!b.restartExemption&&to!=null&&to.position.x*Direction(from.side)>OffsideLine(from.side)+.1f;b.restartExemption=false;b.penalty=false;b.goalAttempt=false;b.saveCredited=false;b.onTargetCounted=false;b.kind=kind;b.from=from.id;b.to=to?.id;b.side=from.side;b.lastTouch=from.side;b.lastTouchId=from.id;b.directThrow=kind=="throw";b.passEligible=kind!="shot"&&kind!="clearance";b.owner=null;b.setupStart=b.position;b.setupHeight=b.height;b.start=from.position+(end-from.position).Normalized*.42f;b.startHeight=b.height;b.end=end;b.endHeight=endHeight;b.duration=duration;b.releaseDelay=kind=="throw"?.5f:.18f;b.elapsed=-b.releaseDelay;b.loft=loft;  
 from.action=kind=="throw"?"throw":"kick";from.actionTime=kind=="throw"?1.1f:.64f;if(kind=="throw"){var height=Data(from).heightCm;b.startHeight=height>=145&&height<=215?height*.01f:1.8f;}from.angle=(float)Math.Atan2(end.x-from.position.x,end.z-from.position.z);from.actionTarget=b.start;from.actionHeight=b.startHeight;from.actionContactTime=b.releaseDelay;from.actionKind=kind;from.actionSequence++;State.carryTime=0;  
 }  
 public const float ShotSpreadExponent=1.6f;
 // Distance (m) of the nearest opponent under which a defender without a
 // safe pass clears instead of carrying out of his own third.
 public const float ClearancePressure=3f;
 // Below this lane safety a pinned defender clears rather than passes.
 public const float PinnedPassSafety=.7f;
 // A clearance stays in play only through a clearly open long channel.
 public const float OpenClearanceSafety=.75f;
 public const float LongBallError=2.2f;
 // Metres before skill/random scaling; preserves execution noise at default tempo.
 public const float BasePassExecutionUncertainty=1f;
 // Points de score retirés à la conduite d'un joueur qui vient d'orienter
 // son corps pour une passe encore jouable.
 public const float PreparedDeliveryCommitment=4f;
 // Engagement dans la frappe préparée : angle d'ouverture minimal (rad),
 // distance maximale au but (m), et facteur par lequel l'occasion offerte
 // à un coéquipier doit dépasser la sienne pour qu'il renonce à frapper.
 public const float PreparedShotMinOpening=.19f,PreparedShotMaxDistance=27f,PreparedShotPassMargin=2f;
 // Shooting from distance (18–27 m): space (m) to the nearest outfield
 // opponent needed when the lane is not clear / at all, and time (s) on
 // the ball to set the body. A shot under a closing defender is often
 // blocked, which is part of the game.
 public const float LongShotPatience=.033f,LongShotMentalityShift=.028f;
 public const float LongShotClearSpace=2.6f,LongShotMinSpace=1.7f,LongShotSetTime=.45f;
 // Length error of a long aerial ball, as a fraction of its distance for a
 // 0-rated passer; the bias (0–1) below 0.5 makes overhitting more common.
 public const float LongBallLengthError=.8f,LongBallOverhitBias=.35f;
 void Shoot(Actor p,bool header=false,bool penalty=false,bool freeKick=false)  
 {  
 float approachFacing=p.angle;  
 int dir=Direction(p.side);float d=Point.Distance(p.position,new Point(dir*52.5f,0));float quality=penalty?.76f:ShotQuality(p,header);State.shotXg=quality;State.metrics[p.side].xg+=quality;  
 float finishing=penalty?Skill(p,"penalties"):freeKick?FreeKickSkill(p):header?Skill(p,"headingAccuracy"):OpenPlayShotSkill(p,d);  
 float accuracy=penalty?.83f+finishing*.0012f:Mathx.Clamp(.30f+finishing/240-d/110,.15f,.74f);bool onTarget=Random()<accuracy;  
 var keeper=State.actors[(1-p.side)*11];float side=keeper.position.z>0?-1:1;  
 // Finishing and room to set the body govern how precisely a player  
 // can place the shot away from the keeper, including headers.  
 float placement=ShotPlacement(p,finishing);  
 // Even accurate shots rarely find the very corner: bias the spread
 // towards the keeper (exponent > 1), so goals track expected goals.
 float z=onTarget?side*(.2f+(float)Math.Pow(Random(),ShotSpreadExponent)*(1.4f+1.5f*placement)):(Random()<.5f?-1:1)*(3.8f+Random()*4);float y=onTarget?.2f+Random()*1.9f:.8f+Random()*3;  
 // The goal has width. An accurate, set-foot shot can use the  
 // other post instead of repeatedly hitting a defender on its axis.  
 // Keep the original accuracy roll, keeper preference and finishing  
 // limits; this changes the chosen reachable lane, not hit success.  
 if(onTarget&&!header&&!penalty&&!freeKick&&!ShootingLaneClear(p,z)){  
 float wide=.2f+(1.4f+1.5f*placement)*.85f;  
 if(ShootingLaneClear(p,-z))z=-z;  
 else if(ShootingLaneClear(p,side*wide))z=side*wide;  
 else if(ShootingLaneClear(p,-side*wide))z=-side*wide;  
 }  
 if(penalty&&onTarget)z=(Random()<.5f?-1:1)*(1.5f+Random()*1.8f);  
 var start=header||penalty?State.ball.position:p.position+(new Point(dir*52.5f,z)-p.position).Normalized*.42f;float extend=(53.4f*dir-start.x)/(52.5f*dir-start.x);var end=new Point(dir*53.4f,start.z+(z-start.z)*extend);  
 Flight(p,null,"shot",end,State.ball.height+(y-State.ball.height)*extend,Math.Max(.28f,d/(23+Skill(p,"shotPower")*.10f)),.10f);p.action=header?"header":"kick";if(penalty){State.ball.penalty=true;State.ball.start=start;}if(header){State.ball.start=start;State.ball.releaseDelay=.12f;State.ball.elapsed=-.12f;}  
 if(header){p.angle=approachFacing;p.actionTarget=start;p.actionHeight=State.ball.startHeight;p.actionContactTime=State.ball.releaseDelay;}  
 bool lob=!header&&!penalty&&!freeKick&&d<24&&Math.Abs(keeper.position.x)<45&&Skill(p,"finishing")>65&&Random()<.45f;if(lob){State.ball.loft=2.5f;State.ball.duration*=1.3f;}  
 if(freeKick){State.ball.loft=1.5f+finishing*.006f;State.ball.duration*=1.12f;}  
 State.ball.goalAttempt=true;State.ball.shotOnTarget=onTarget;State.ball.shotSide=p.side;State.shots[p.side]++;Emit("shot",p.side,p.id,Data(p).name+(header?" reprend de la tête !":lob?" tente de lober le gardien !":d>23?" tente sa chance de loin !":" frappe !"));  
 }  
 }  
}

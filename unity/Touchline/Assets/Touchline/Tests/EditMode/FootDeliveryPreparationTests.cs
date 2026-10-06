using System;using System.Linq;using System.Reflection;using NUnit.Framework;using Touchline.Core;
namespace Touchline.Tests {
 public sealed class FootDeliveryPreparationTests {
  MatchSimulation Prepare(int side,int period,float speed,out Actor owner,out Actor keeper){
   var db=new Database{players=Enumerable.Range(0,40).Select(i=>new PlayerData{id="foot-prep"+i,name="P"+i,team=i<20?"a":"b",position=i%20==0?"GB":i%20<5?"DEF":i%20<9?"MIL":"ATT",rating=80}).ToArray()};var c=new Career{club="a"};c.lineup=Career.Select(db,"a",c.tactic);var sim=MatchSimulation.Create(db,c,"b",73,2700);var m=sim.State;m.restart=0;m.phase="play";m.period=period;m.clock=period==1?100:2800;m.nextMedicalCheck=99999;
   foreach(var a in m.actors)a.sentOff=true;owner=m.actors[side*11+9];keeper=m.actors[(1-side)*11];owner.sentOff=keeper.sentOff=false;int dir=sim.Direction(side);owner.position=new Point(dir*38,0);keeper.position=new Point(dir*51,0);owner.angle=-dir*(float)Math.PI/2;owner.velocity=new Point(-dir*speed,0);owner.carryTarget=owner.position;
   m.ball=new BallState{owner=owner.id,side=side,position=owner.position,controlOrigin=owner.position,kind="none",height=.11f};m.possessionSide=side;m.decision=0;return sim;
  }
  static object Invoke(MatchSimulation sim,string method,params object[] args)=>typeof(MatchSimulation).GetMethod(method,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(sim,args);
  static float Delta(float a,float b){float d=a-b;while(d>Math.PI)d-=(float)Math.PI*2;while(d< -Math.PI)d+=(float)Math.PI*2;return Math.Abs(d);}
  [TestCase(0,1)] [TestCase(1,1)] [TestCase(0,2)] [TestCase(1,2)]
  public void BackFacingShotRetainsContestableBallBeforeTurning(int side,int period){
   var sim=Prepare(side,period,4,out var p,out var keeper);uint seed=sim.State.seed;float facing=p.angle;
   Assert.AreEqual("prepare",sim.Decide(p));Assert.IsTrue(MatchSimulation.PreparingFootDelivery(p));Assert.AreEqual(p.id,sim.State.ball.owner);Assert.AreEqual(0,sim.State.shots[side]);Assert.AreEqual(seed,sim.State.seed);Assert.AreEqual(facing,p.angle);Assert.AreEqual(p.actionTime,p.actionContactTime);
   float previousSpeed=p.velocity.Length;bool shot=false;for(int n=0;n<22;n++){
    float before=p.angle;var old=p.position;sim.Advance(.1);if(sim.State.shots[side]>0){shot=true;break;}
    Assert.LessOrEqual(Delta(p.angle,before),.5001f);Assert.LessOrEqual(p.velocity.Length,previousSpeed+.001f);Assert.LessOrEqual(Point.Distance(old,p.position),.401f);previousSpeed=p.velocity.Length;Assert.AreEqual(p.id,sim.State.ball.owner);
   }
   Assert.IsTrue(shot,"A clear finish must eventually be executed after preparation.");Assert.AreEqual(1,sim.State.shots[side]);Assert.Less(Delta(p.angle,sim.Direction(side)*(float)Math.PI/2),.35f);
  }
  [Test] public void AlreadyFacingGoalShootsWithoutArtificialWaiting(){var sim=Prepare(0,1,0,out var p,out var k);p.angle=(float)Math.PI/2;Assert.AreEqual("shot",sim.Decide(p));Assert.AreEqual(1,sim.State.shots[0]);Assert.IsFalse(MatchSimulation.PreparingFootDelivery(p));}
  [Test] public void DefenderCanCommitAndWinBallDuringPreparation(){
   bool won=false;for(uint seed=1;seed<=16;seed++){
    var sim=Prepare(0,1,0,out var p,out var k);sim.State.seed=seed;sim.Decide(p);p.controlTime=0;
    var d=sim.State.actors[12];d.sentOff=false;d.position=p.position+new Point(-.95f,0);d.angle=(float)Math.PI/2;d.controlTime=d.duelCooldown=0;sim.State.ball.position=p.position+new Point(-.4f,0);
    Assert.IsTrue((bool)Invoke(sim,"BeginStandingDuel",d,p));d.actionTime=MatchSimulation.TackleRecovery;
    if((bool)Invoke(sim,"ResolveStandingDuels",p)){won=true;Assert.AreNotEqual(p.id,sim.State.ball.owner);break;}
   }Assert.IsTrue(won,"Preparation must not create an invulnerable owner.");
  }
  [Test] public void LostPossessionCancelsStalePreparation(){var sim=Prepare(0,1,0,out var p,out var k);sim.Decide(p);sim.State.ball.owner=k.id;Invoke(sim,"Move");Assert.IsFalse(MatchSimulation.PreparingFootDelivery(p));}
  [Test] public void PassPreparationDoesNotCountOrConsumeAccuracyRandomness(){var sim=Prepare(0,1,0,out var p,out var k);uint seed=sim.State.seed;bool prepared=(bool)Invoke(sim,"BeginFootDeliveryPreparation",p,new Point(50,0),"pass");Assert.IsTrue(prepared);Assert.AreEqual(seed,sim.State.seed);Assert.AreEqual(0,sim.State.passes[0]);Assert.AreEqual("pass",p.actionKind);Assert.AreEqual(p.id,sim.State.ball.owner);}
  [Test] public void HeldBallKeepsKeeperPath(){var sim=Prepare(0,1,0,out var p,out var k);sim.State.ball.held=true;Assert.IsFalse((bool)Invoke(sim,"BeginFootDeliveryPreparation",p,new Point(50,0),"pass"));}
  [Test] public void FoulRestartImmediatelyCancelsPendingTurn(){var sim=Prepare(0,1,0,out var p,out var k);sim.Decide(p);Assert.IsTrue(MatchSimulation.PreparingFootDelivery(p));Invoke(sim,"Restart","free-kick",1,p.position,3f);Assert.IsFalse(MatchSimulation.PreparingFootDelivery(p));Assert.AreEqual("idle",p.action);Assert.IsNull(sim.State.ball.owner);}
  [Test] public void SentOffActorCannotKeepPreparingAfterPossessionChanges(){var sim=Prepare(0,1,0,out var p,out var k);sim.Decide(p);p.sentOff=true;Invoke(sim,"Move");Assert.IsFalse(MatchSimulation.PreparingFootDelivery(p));}
  [Test] public void ExistingTackleKeepsItsTargetWhenCarrierStartsPreparing(){var sim=Prepare(0,1,0,out var p,out var k);p.controlTime=0;var d=sim.State.actors[12];d.sentOff=false;d.position=p.position+new Point(-.95f,0);d.angle=(float)Math.PI/2;d.controlTime=d.duelCooldown=0;sim.State.ball.position=p.position+new Point(-.4f,0);Assert.IsTrue((bool)Invoke(sim,"BeginStandingDuel",d,p));Assert.IsTrue((bool)Invoke(sim,"BeginFootDeliveryPreparation",p,new Point(52.5f,0),"shot"));Invoke(sim,"CancelInvalidStandingDuels");Assert.AreEqual(p.id,d.tackleOpponent);Assert.AreEqual("tackle",d.action);Assert.Greater(d.actionTime,MatchSimulation.TackleRecovery);}

  [TestCase(false)] [TestCase(true)] public void IncomingPlayerDoesNotInheritOutgoingPreparation(bool ready){
   var sim=Prepare(0,1,0,out var player,out var keeper);sim.Decide(player);
   if(ready){player.action="idle";player.actionTime=0;player.actionKind="foot-delivery-ready";}
   string incoming=Enumerable.Range(1,19).Select(i=>"foot-prep"+i).First(id=>!sim.State.used.Contains(id));
   sim.Substitute(0,player.slot,incoming);Assert.AreEqual(incoming,player.id);Assert.AreEqual(incoming,sim.State.ball.owner);Assert.AreEqual("idle",player.action);Assert.AreEqual(0,player.actionTime);Assert.IsNull(player.actionKind);Assert.AreEqual(0,player.actionContactTime);
  }
  [Test] public void ReplacingAnotherPlayerDoesNotCancelCarrierPreparation(){
   var sim=Prepare(0,1,0,out var player,out var keeper);sim.Decide(player);sim.State.actors[8].sentOff=false;
   string incoming=Enumerable.Range(1,19).Select(i=>"foot-prep"+i).First(id=>!sim.State.used.Contains(id));
   sim.Substitute(0,8,incoming);Assert.IsTrue(MatchSimulation.PreparingFootDelivery(player));Assert.AreEqual(player.id,sim.State.ball.owner);
  }
 }
}

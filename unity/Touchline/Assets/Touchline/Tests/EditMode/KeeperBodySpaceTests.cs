using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Touchline.Core;

namespace Touchline.Tests
{
 public sealed class KeeperBodySpaceTests
 {
  static object Invoke(MatchSimulation sim,string name,params object[] args)=>typeof(MatchSimulation).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(sim,args);
  MatchSimulation Prepare(int side,int period,bool teammate,out Actor keeper,out Actor runner)
  {
   var db=new Database{players=Enumerable.Range(0,40).Select(i=>new PlayerData{id="keeper-space"+i,name="K"+i,team=i<20?"a":"b",position=i%20==0?"GB":i%20<5?"DEF":i%20<9?"MIL":"ATT",rating=80}).ToArray()};var c=new Career{club="a"};c.lineup=Career.Select(db,"a",c.tactic);var sim=MatchSimulation.Create(db,c,"b",73,2700);
   sim.State.period=period;sim.State.restart=0;sim.State.phase="play";foreach(var a in sim.State.actors)a.sentOff=true;
   keeper=sim.State.actors[side*11];runner=sim.State.actors[(teammate?side:1-side)*11+3];keeper.sentOff=runner.sentOff=false;int dir=sim.Direction(side);
   keeper.position=keeper.previous=new Point(-45,0)*dir;keeper.action="keeper-hold";keeper.actionTime=100;runner.position=runner.previous=keeper.position+new Point(4,0)*dir;runner.velocity=new Point(-5,0)*dir;runner.action="run";runner.intent=teammate?"support":"recover";
   sim.State.ball=new BallState{held=true,owner=keeper.id,side=side,position=keeper.position,kind="none"};sim.State.possessionSide=side;return sim;
  }

  [TestCase(0,1,true)] [TestCase(0,1,false)] [TestCase(1,1,true)] [TestCase(1,1,false)]
  [TestCase(0,2,true)] [TestCase(0,2,false)] [TestCase(1,2,true)] [TestCase(1,2,false)]
  public void MovingRunnerGoesAroundKeeperWithoutTeleporting(int side,int period,bool teammate)
  {
   var sim=Prepare(side,period,teammate,out var keeper,out var runner);int dir=sim.Direction(side);var goal=keeper.position-new Point(4,0)*dir;float closest=10,maxStep=0;
   for(int i=0;i<40;i++){
    var target=(Point)Invoke(sim,"KeeperBodySafeTarget",runner,keeper,goal);runner.previous=runner.position;
    Invoke(sim,"MoveActor",runner,target,5f);sim.ResolvePlayerContacts();
    closest=Math.Min(closest,Point.Distance(runner.position,keeper.position));maxStep=Math.Max(maxStep,Point.Distance(runner.position,runner.previous));
   }
   Assert.GreaterOrEqual(closest,1.15f,"A body-wide route must protect a keeper leaning over the ball.");
   Assert.LessOrEqual(maxStep,.501f,"Avoidance must use actual bounded movement, not reposition a runner.");
   Assert.Less(Point.Distance(runner.position,goal),1f,"The route must eventually pass around the keeper, not deadlock before him.");
  }

  [TestCase(0,1)] [TestCase(1,2)]
  public void DistantUnobstructedRouteIsUnchanged(int side,int period)
  {
   var sim=Prepare(side,period,true,out var keeper,out var runner);runner.position=keeper.position+new Point(3,4);var target=keeper.position+new Point(-3,4);
   var safe=(Point)Invoke(sim,"KeeperBodySafeTarget",runner,keeper,target);Assert.AreEqual(target.x,safe.x);Assert.AreEqual(target.z,safe.z);
  }

  [Test] public void GroundedBallAtKeepersFeetDoesNotCreateProtectedHandPossession()
  {
   var sim=Prepare(0,1,false,out var keeper,out var runner);sim.State.ball.held=false;keeper.action="run";keeper.actionTime=0;
   Assert.IsNull(Invoke(sim,"KeeperBodyInHands"));
  }

  [Test] public void HandDistributionFollowThroughStillHasBodyAfterBallIsReceived()
  {
   var sim=Prepare(0,1,true,out var keeper,out var runner);sim.State.ball.owner=runner.id;sim.State.ball.held=false;sim.State.ball.keeperDistribution=false;sim.State.ball.from=runner.id;
   keeper.action="keeper-roll";keeper.actionTime=.25f;
   Assert.AreSame(keeper,Invoke(sim,"KeeperBodyInHands"));
   keeper.actionTime=0;Assert.IsNull(Invoke(sim,"KeeperBodyInHands"));
  }
 }
}

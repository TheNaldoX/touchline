using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Touchline.Core;

namespace Touchline.Tests
{
 public sealed class KeeperAngleBisectorTests
 {
  MatchSimulation Prepare(int keeperSide,int period,float depth,float flank,out Actor keeper,out Actor attacker)
  {
   var db=new Database{players=Enumerable.Range(0,40).Select(i=>new PlayerData{id="keeper-angle"+i,name="K"+i,team=i<20?"a":"b",position=i%20==0?"GB":i%20<5?"DEF":i%20<9?"MIL":"ATT",rating=80}).ToArray()};var c=new Career{club="a"};c.lineup=Career.Select(db,"a",c.tactic);var sim=MatchSimulation.Create(db,c,"b",73,2700);
   sim.State.period=period;sim.State.restart=0;sim.State.phase="play";foreach(var a in sim.State.actors)a.sentOff=true;
   keeper=sim.State.actors[keeperSide*11];attacker=sim.State.actors[(1-keeperSide)*11+9];keeper.sentOff=attacker.sentOff=false;int dir=sim.Direction(keeperSide);
   keeper.position=new Point(-dir*51,0);attacker.position=new Point(dir*(-52.5f+depth),flank);
   sim.State.ball=new BallState{owner=attacker.id,side=attacker.side,kind="none",position=attacker.position};sim.State.possessionSide=attacker.side;return sim;
  }
  static Point Target(MatchSimulation sim,Actor keeper)=>(Point)typeof(MatchSimulation).GetMethod("KeeperTarget",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(sim,new object[]{keeper});

  [TestCase(0,1,-8)] [TestCase(0,1,8)] [TestCase(1,1,-8)] [TestCase(1,1,8)]
  [TestCase(0,2,-8)] [TestCase(0,2,8)] [TestCase(1,2,-8)] [TestCase(1,2,8)]
  public void AngledOneOnOneAdvanceCoversEqualAnglesToBothPosts(int side,int period,int flank)
  {
   foreach(float depth in new[]{6f,10f,16f}){
    var sim=Prepare(side,period,depth,flank,out var keeper,out var attacker);int dir=sim.Direction(side);var goal=new Point(-dir*52.5f,0);var target=Target(sim,keeper);
    float advance=(target.x-goal.x)*dir;
    Assert.AreEqual(Mathx.Clamp(depth*.43f,1.5f,7),advance,.0001f,"Preserve the chosen closing distance.");
    double nearAngle=Math.Atan2(3.66-flank,depth),farAngle=Math.Atan2(-3.66-flank,depth);
    double targetAngle=Math.Atan2(target.z-flank,depth-advance);
    Assert.AreEqual(nearAngle-targetAngle,targetAngle-farAngle,.00001,"The keeper must cover equal angles to the two goalposts.");
    Assert.Less(Point.Distance(target,attacker.position),Point.Distance(goal,attacker.position));
    Assert.AreEqual("close-angle",keeper.intent);
   }
  }

  [TestCase(0,1)] [TestCase(1,1)] [TestCase(0,2)] [TestCase(1,2)]
  public void CentralOneOnOneKeepsOriginalDepthAndCentre(int side,int period)
  {
   var sim=Prepare(side,period,16,0,out var keeper,out var attacker);var target=Target(sim,keeper);
   Assert.AreEqual(sim.Direction(side)*(-52.5f+16*.43f),target.x,.0001f);Assert.AreEqual(0,target.z);
  }

  [TestCase(0,1)] [TestCase(1,2)]
  public void DefensiveCoverRetainsNormalKeeperPosition(int side,int period)
  {
   var sim=Prepare(side,period,16,8,out var keeper,out var attacker);var defender=sim.State.actors[side*11+2];defender.sentOff=false;defender.position=attacker.position+new Point(2,0);
   var target=Target(sim,keeper);float offset=16*.11f;
   Assert.AreEqual(sim.Direction(side)*(-52.5f+offset),target.x,.0001f);Assert.AreEqual(8*offset/16,target.z,.0001f);Assert.AreNotEqual("close-angle",keeper.intent);
  }

  [TestCase(0,1)] [TestCase(1,2)]
  public void WideOutsideDuelCorridorUsesExistingNearPostCover(int side,int period)
  {
   var sim=Prepare(side,period,10,16,out var keeper,out var attacker);var target=Target(sim,keeper);float offset=10*.11f;
   Assert.AreEqual(sim.Direction(side)*(-52.5f+offset),target.x,.0001f);Assert.AreEqual(16*offset/10,target.z,.0001f);Assert.AreNotEqual("close-angle",keeper.intent);
  }
 }
}

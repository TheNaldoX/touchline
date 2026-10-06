using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Touchline.Core;
public static class EngineCloseShotBlocksScenarios034 {
 public static object Run(){
  var rows=new List<object>();
  foreach(int side in new[]{0,1})foreach(int period in new[]{1,2})foreach(bool inLane in new[]{true,false}){
   var db=new Database{players=Enumerable.Range(0,40).Select(i=>new PlayerData{id="block"+i,name="B"+i,team=i<20?"a":"b",position=i%20==0?"GB":i%20<5?"DEF":i%20<9?"MIL":"ATT",rating=80}).ToArray()};
   var c=new Career{club="a"};c.lineup=Career.Select(db,"a",c.tactic);var sim=MatchSimulation.Create(db,c,"b",73,2700);sim.State.period=period;sim.State.phase="play";sim.State.restart=0;
   foreach(var p in sim.State.actors)p.sentOff=true;
   var shooter=sim.State.actors[side*11+9];var defender=sim.State.actors[(1-side)*11+2];shooter.sentOff=defender.sentOff=false;int dir=sim.Direction(side);
   shooter.position=shooter.previous=new Point(dir*40,0);shooter.velocity=new Point();shooter.angle=dir*(float)Math.PI*.5f;
   defender.position=defender.previous=new Point(dir*41,inLane?0:3);defender.velocity=new Point();defender.action="kick";defender.actionTime=2;
   sim.State.ball=new BallState{owner=shooter.id,side=side,kind="none",position=shooter.position,height=.11f};sim.State.possessionSide=side;
   typeof(MatchSimulation).GetMethod("Shoot",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(sim,new object[]{shooter,false,false,false});
   sim.Advance(.4);var blocks=sim.State.events.Where(e=>e.kind=="block"&&e.player==defender.id).ToArray();
   if(inLane&&blocks.Length!=1)throw new Exception("The released shot passed through a close opponent's real body corridor");
   if(!inLane&&blocks.Length!=0)throw new Exception("A nearby opponent outside the swept ball path invented a block");
   if(sim.State.shots[side]!=1)throw new Exception("A genuine blocked shot was removed from the attempt statistics");
   rows.Add(new{side,period,inLane,blocks=blocks.Length,shots=sim.State.shots[side]});
  }
  return new{passed=true,count=rows.Count,rows};
 }
}
namespace Touchline.Tests {
 public sealed class EngineCloseShotBlocksRegressionTests034 {
  [NUnit.Framework.Test]
  public void PhysicalScenariosPreserveContactAndDecisionInvariants() {
   NUnit.Framework.Assert.DoesNotThrow(()=>EngineCloseShotBlocksScenarios034.Run());
  }
 }
}
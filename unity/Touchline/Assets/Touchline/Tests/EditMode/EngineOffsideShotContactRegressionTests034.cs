using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Touchline.Core;
public static class EngineOffsideShotContactScenarios034 {
 public static object Run(){
  var rows=new List<object>();
  foreach(int side in new[]{0,1})foreach(int period in new[]{1,2})foreach(bool close in new[]{true,false})foreach(bool illegal in new[]{true,false}){
   var db=new Database{players=Enumerable.Range(0,40).Select(i=>new PlayerData{id="shotOff"+i,name="S"+i,team=i<20?"a":"b",position=i%20==0?"GB":i%20<5?"DEF":i%20<9?"MIL":"ATT",rating=80}).ToArray()};
   var c=new Career{club="a"};c.lineup=Career.Select(db,"a",c.tactic);var sim=MatchSimulation.Create(db,c,"b",73,2700);sim.State.period=period;sim.State.phase="play";sim.State.restart=0;
   foreach(var p in sim.State.actors)p.sentOff=true;
   var shooter=sim.State.actors[side*11+9];var teammate=sim.State.actors[side*11+8];var defender=sim.State.actors[(1-side)*11+2];var keeper=sim.State.actors[(1-side)*11];int dir=sim.Direction(side);
   foreach(var p in new[]{shooter,teammate,defender,keeper}){p.sentOff=false;p.velocity=new Point();p.action="kick";p.actionTime=2;}
   shooter.position=shooter.previous=new Point(dir*(close?49:39),0);shooter.angle=dir*(float)Math.PI*.5f;
   defender.position=defender.previous=new Point(dir*(close?(illegal?48:51.2f):(illegal?40:47)),5);keeper.position=keeper.previous=new Point(dir*52.3f,0);
   sim.State.ball=new BallState{owner=shooter.id,side=side,position=shooter.position,height=.11f};sim.State.possessionSide=side;
   typeof(MatchSimulation).GetMethod("Shoot",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(sim,new object[]{shooter,false,false,false});
   var shot=sim.State.ball;float x=dir*(close?49.9f:45);float fraction=(x-shot.start.x)/(shot.end.x-shot.start.x);
   teammate.position=teammate.previous=Point.Lerp(shot.start,shot.end,fraction);
   sim.Advance(.5);bool offside=sim.State.events.Any(e=>e.kind=="offside"&&e.player==teammate.id);bool block=sim.State.events.Any(e=>e.kind=="block"&&e.player==teammate.id);
   if(illegal&&(!offside||block||sim.State.phase!="free-kick"||!sim.State.indirectRestart))throw new Exception("A flagged attacker directly contacting the released shot was recorded as a block instead of an indirect offside free kick");
   if(!illegal&&(offside||!block))throw new Exception("The equivalent legal attacking contact did not retain its normal physical shot block");
   if(sim.State.metrics.Sum(m=>m.ownGoals)!=0||sim.State.shots[side]!=1)throw new Exception("The offside contact was confused with an own goal or a nonexistent shot attempt");
   rows.Add(new{side,period,close,illegal,offside,block});
  }
  return new{passed=true,count=rows.Count,rows};
 }
}
namespace Touchline.Tests {
 public sealed class EngineOffsideShotContactRegressionTests034 {
  [NUnit.Framework.Test]
  public void ActualReleasedShotContactsRespectOffsideAndLegalPhysicalBlocks() {
   NUnit.Framework.Assert.DoesNotThrow(()=>EngineOffsideShotContactScenarios034.Run());
  }
 }
}
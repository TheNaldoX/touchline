using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Touchline.Core;
public static class EngineWingCarryWidthScenarios034 {
 public static object Run(){
  var rows=new List<object>();
  foreach(int side in new[]{0,1})foreach(int period in new[]{1,2}){
   var db=new Database{players=Enumerable.Range(0,40).Select(i=>new PlayerData{id="wing"+i,name="W"+i,team=i<20?"a":"b",position=i%20==0?"GB":i%20<5?"DEF":i%20<9?"MIL":"ATT",rating=80}).ToArray()};
   var c=new Career{club="a"};c.lineup=Career.Select(db,"a",c.tactic);var sim=MatchSimulation.Create(db,c,"b",73,2700);sim.State.period=period;sim.State.phase="play";sim.State.restart=0;
   foreach(var p in sim.State.actors)p.sentOff=true;
   var winger=sim.State.actors[side*11+8];var opponent=sim.State.actors[(1-side)*11+2];winger.sentOff=opponent.sentOff=false;int dir=sim.Direction(side);
   winger.position=winger.previous=new Point(dir*10,dir*19);winger.velocity=new Point(dir*4,0);opponent.position=opponent.previous=new Point(dir*13,dir*19);opponent.velocity=new Point();
   var choose=typeof(MatchSimulation).GetMethod("ChooseCarry",BindingFlags.Instance|BindingFlags.NonPublic);
   sim.Tactic(side).width=.1f;var narrow=(Point)choose.Invoke(sim,new object[]{winger});
   sim.Tactic(side).width=.95f;var wide=(Point)choose.Invoke(sim,new object[]{winger});
   if(wide.z*dir<=narrow.z*dir+1)throw new Exception("The winger's carry ignored his team's width when choosing either side of a blocking opponent");
   if(wide.z*dir<0||narrow.z*dir<0)throw new Exception("The left winger's carry crossed onto the wrong attacking flank");
   rows.Add(new{side,period,narrow=narrow.z*dir,wide=wide.z*dir});
  }
  return new{passed=true,count=rows.Count,rows};
 }
}
namespace Touchline.Tests {
 public sealed class EngineWingCarryWidthRegressionTests034 {
  [NUnit.Framework.Test]
  public void PhysicalScenariosPreserveContactAndDecisionInvariants() {
   NUnit.Framework.Assert.DoesNotThrow(()=>EngineWingCarryWidthScenarios034.Run());
  }
 }
}
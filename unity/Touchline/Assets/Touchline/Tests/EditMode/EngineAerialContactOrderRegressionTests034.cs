using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Touchline.Core;
public static class EngineAerialContactOrderScenarios034 {
 public static object Run(){
  var rows=new List<object>();
  foreach(int side in new[]{0,1})foreach(int period in new[]{1,2}){
   var db=new Database{players=Enumerable.Range(0,40).Select(i=>new PlayerData{id="head"+i,name="H"+i,team=i<20?"a":"b",position=i%20==0?"GB":i%20<5?"DEF":i%20<9?"MIL":"ATT",rating=80}).ToArray()};
   var c=new Career{club="a"};c.lineup=Career.Select(db,"a",c.tactic);var sim=MatchSimulation.Create(db,c,"b",73,2700);sim.State.period=period;sim.State.phase="play";sim.State.restart=0;
   foreach(var p in sim.State.actors)p.sentOff=true;
   var striker=sim.State.actors[side*11+9];striker.sentOff=false;int dir=sim.Direction(side);striker.angle=dir*(float)Math.PI*.5f;
   striker.position=striker.previous=new Point(dir*40,0);var contact=striker.position+new Point(dir*.25f,0);
   sim.State.ball=new BallState{kind="cross",side=side,to=striker.id,previous=contact,position=contact,previousHeight=1.9f,height=1.3f,elapsed=.8f,duration=1,passEligible=true};
   var reception=typeof(MatchSimulation).GetMethod("ResolveReception",BindingFlags.Instance|BindingFlags.NonPublic);
   var aerial=typeof(MatchSimulation).GetMethod("ResolveAerial",BindingFlags.Instance|BindingFlags.NonPublic);
   if((bool)reception.Invoke(sim,null))throw new Exception("A later chest contact won before the earlier swept head contact in the same tick");
   if(!(bool)aerial.Invoke(sim,new object[]{.8f})||striker.action!="header")throw new Exception("The earliest real header did not create its actual heading action");
   rows.Add(new{side,period,scenario="head-before-chest",action=striker.action});
   striker.action="idle";striker.actionTime=0;striker.controlTime=0;
   sim.State.ball=new BallState{kind="cross",side=side,to=striker.id,previous=contact,position=contact,previousHeight=.4f,height=.4f,elapsed=.8f,duration=1,passEligible=true};
   if(!(bool)reception.Invoke(sim,null)||striker.action!="control")throw new Exception("A low cross was wrongly reserved for an unreachable aerial contact");
   rows.Add(new{side,period,scenario="low-reception",action=striker.action});
  }
  return new{passed=true,count=rows.Count,rows};
 }
}
namespace Touchline.Tests {
 public sealed class EngineAerialContactOrderRegressionTests034 {
  [NUnit.Framework.Test]
  public void PhysicalScenariosPreserveContactAndDecisionInvariants() {
   NUnit.Framework.Assert.DoesNotThrow(()=>EngineAerialContactOrderScenarios034.Run());
  }
 }
}
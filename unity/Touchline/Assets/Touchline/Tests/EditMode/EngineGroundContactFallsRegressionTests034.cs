using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Touchline.Core;
public static class EngineGroundContactFallsScenarios034 {
 public static object Run(){
  var rows=new List<object>();
  foreach(int side in new[]{0,1})foreach(int period in new[]{1,2}){
   var db=new Database{players=Enumerable.Range(0,40).Select(i=>new PlayerData{id="fall"+i,name="F"+i,team=i<20?"a":"b",position=i%20==0?"GB":i%20<5?"DEF":i%20<9?"MIL":"ATT",rating=75}).ToArray()};
   var c=new Career{club="a"};c.lineup=Career.Select(db,"a",c.tactic);var sim=MatchSimulation.Create(db,c,"b",73,2700);sim.State.period=period;
   var runner=sim.State.actors[side*11+9];var defender=sim.State.actors[(1-side)*11+2];int dir=sim.Direction(side);
   var fall=typeof(MatchSimulation).GetMethod("BeginContactFall",BindingFlags.Instance|BindingFlags.NonPublic);
   Action reset=()=>{runner.action="run";runner.actionTime=0;runner.position=new Point(dir*1.15f,0);runner.previous=new Point(dir*.9f,0);runner.velocity=new Point(dir*5,0);defender.position=new Point();defender.velocity=new Point();defender.action="tackle";defender.actionTarget=new Point(dir,0);};
   Func<bool,bool> contact=foul=>(bool)fall.Invoke(sim,new object[]{runner,defender,foul});
   reset();if(!contact(true)||runner.action!="fall"||Point.Distance(runner.actionTarget,defender.actionTarget)>.001f)throw new Exception("A genuine extended boot trip lacked a fall at its contact point");rows.Add(new{side,period,scenario="extended-boot",passed=true});
   reset();defender.actionTarget=new Point(dir,2);if(contact(true))throw new Exception("A distant boot manufactured a fall");rows.Add(new{side,period,scenario="missed-boot",passed=true});
   reset();defender.actionTarget=new Point(dir*1.4f,0);if(contact(true))throw new Exception("An unreachable boot manufactured a fall");rows.Add(new{side,period,scenario="unreachable-boot",passed=true});
   reset();defender.action="run";if(contact(true))throw new Exception("A separated running challenger manufactured an extended-foot trip");rows.Add(new{side,period,scenario="no-tackle",passed=true});
   reset();if(contact(false))throw new Exception("A clean distant poke was treated as a guaranteed falling body collision");rows.Add(new{side,period,scenario="clean-ball",passed=true});
  }
  return new{passed=true,count=rows.Count,rows};
 }
}
namespace Touchline.Tests {
 public sealed class EngineGroundContactFallsRegressionTests034 {
  [NUnit.Framework.Test]
  public void PhysicalScenariosPreserveContactAndDecisionInvariants() {
   NUnit.Framework.Assert.DoesNotThrow(()=>EngineGroundContactFallsScenarios034.Run());
  }
 }
}
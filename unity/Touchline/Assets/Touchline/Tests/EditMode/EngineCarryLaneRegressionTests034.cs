using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Touchline.Core;
public static class EngineCarryLaneScenarios034 {
 public static object Run(bool weighted){
  var rows=new List<object>();
  foreach(int side in new[]{0,1})foreach(int period in new[]{1,2}){
   var db=new Database{players=Enumerable.Range(0,40).Select(i=>new PlayerData{id="carry"+i,name="C"+i,team=i<20?"a":"b",position=i%20==0?"GB":i%20<5?"DEF":i%20<9?"MIL":"ATT",rating=80}).ToArray()};
   var c=new Career{club="a"};c.lineup=Career.Select(db,"a",c.tactic);var s=MatchSimulation.Create(db,c,"b",73,2700);s.State.period=period;s.State.restart=0;s.State.phase="play";
   foreach(var actor in s.State.actors)actor.sentOff=true;
   var carrier=s.State.actors[side*11+9];var defender=s.State.actors[(1-side)*11+2];carrier.sentOff=defender.sentOff=false;int dir=s.Direction(side);
   carrier.position=new Point();carrier.velocity=new Point(dir*4,0);defender.position=new Point(dir*3,2);defender.velocity=new Point(0,-6);
   var carry=typeof(MatchSimulation).GetMethod("CarryLaneSafety",BindingFlags.Instance|BindingFlags.NonPublic);var pass=typeof(MatchSimulation).GetMethod("Safety",BindingFlags.Instance|BindingFlags.NonPublic);
   var end=new Point(dir*6,0);float crossing=(float)carry.Invoke(s,new object[]{carrier,end});float passing=(float)pass.Invoke(s,new object[]{carrier,end,false});
   if(crossing>=passing-.12f||passing<.9f)throw new Exception("Dribbling into an arriving opponent was valued as safely as a faster travelling pass");
   if(weighted){
    var player=db.Find(carrier.id);player.attributes=new[]{new AttributeValue{key="dribbling",value=30}};
    float poor=(float)carry.Invoke(s,new object[]{carrier,end});player.attributes=new[]{new AttributeValue{key="dribbling",value=95}};
    float skilled=(float)carry.Invoke(s,new object[]{carrier,end});player.attributes=null;
    if(skilled<=poor+.15f)throw new Exception("A skilled dribbler and weak dribbler expected the same outcome against a reachable contest");
   }
   defender.velocity=new Point(0,6);float retreating=(float)carry.Invoke(s,new object[]{carrier,end});if(retreating<.9f)throw new Exception("An opponent retreating from the dribble corridor remained an imaginary interception threat");
   defender.velocity=new Point(0,-6);defender.action="fall";defender.actionTime=1;float grounded=(float)carry.Invoke(s,new object[]{carrier,end});if(grounded<.9f)throw new Exception("A grounded player was credited with sprinting to intercept the dribble");
   defender.action="run";defender.actionTime=0;
   rows.Add(new{side,period,crossing,passing,retreating,grounded});
   foreach(bool patient in new[]{false,true}){
    carrier.position=new Point(dir*41,0);carrier.angle=dir*(float)Math.PI/2;carrier.previous=carrier.position;defender.position=new Point(dir*39.5f,0);defender.previous=defender.position;
    var keeper=s.State.actors[(1-side)*11];keeper.sentOff=false;keeper.position=keeper.previous=new Point(dir*51,0);
    s.State.ball=new BallState{owner=carrier.id,kind="none",side=side,position=carrier.position};s.State.possessionSide=side;s.Tactic(side).workIntoBox=patient;
    if(s.Decide(carrier)!="shot")throw new Exception("Carry risk prevented an available clear goal window");rows.Add(new{side,period,patient,action="shot"});
   }
  }
  return new{passed=true,count=rows.Count,rows};
 }
}
namespace Touchline.Tests {
 public sealed class EngineCarryLaneRegressionTests034 {
  [NUnit.Framework.Test]
  public void PhysicalScenariosPreserveContactAndDecisionInvariants() {
   NUnit.Framework.Assert.DoesNotThrow(()=>EngineCarryLaneScenarios034.Run(false));
  }
 }
}
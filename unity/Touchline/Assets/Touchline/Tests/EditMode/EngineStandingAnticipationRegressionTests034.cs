using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Touchline.Core;
public static class EngineStandingAnticipationScenarios034 {
 public static object Run(){
  var results=new List<string>();
  foreach(int side in new[]{0,1}) foreach(int period in new[]{1,2}){
   var db=new Database{players=Enumerable.Range(0,40).Select(i=>new PlayerData{id="duel"+i,name="D"+i,team=i<20?"a":"b",position=i%20==0?"GB":i%20<5?"DEF":i%20<9?"MIL":"ATT",rating=75}).ToArray()};
   var c=new Career{club="a"};c.lineup=Career.Select(db,"a",c.tactic);
   var s=MatchSimulation.Create(db,c,"b",73,2700);s.State.period=period;s.State.restart=0;s.State.phase="play";
   foreach(var actor in s.State.actors)actor.sentOff=true;
   var owner=s.State.actors[side*11+9];var defender=s.State.actors[(1-side)*11+2];int dir=s.Direction(side);
   var begin=typeof(MatchSimulation).GetMethod("BeginStandingDuel",BindingFlags.NonPublic|BindingFlags.Instance);
   Action reset=()=>{owner.position=owner.previous=new Point();owner.controlTime=0;owner.velocity=new Point();defender.position=defender.previous=new Point(1.25f*dir,0);defender.angle=-dir*(float)Math.PI*.5f;defender.velocity=new Point();defender.action="idle";defender.actionTime=0;defender.tackleOpponent=null;defender.duelCooldown=0;defender.controlTime=0;s.State.ball=new BallState{owner=owner.id,side=side,position=new Point(.5f*dir,0),height=.11f};};
   Func<bool> commit=()=>(bool)begin.Invoke(s,new object[]{defender,owner});
   reset();owner.velocity=new Point(-8*dir,0);uint seed=s.State.seed;
   if(commit()||s.State.seed!=seed||defender.action!="idle")throw new Exception("Escaping carrier caused an unreachable or random commitment");results.Add("escaping-ball-"+side+"-"+period);
   reset();owner.velocity=new Point(dir,0);
   if(!commit()||Point.Distance(defender.actionTarget,new Point(.7f*dir,0))>.0001f||s.State.seed!=seed)throw new Exception("Reachable future ball was not targeted at actual contact time");results.Add("reachable-ball-"+side+"-"+period);
   reset();owner.velocity=new Point(-4*dir,0);defender.velocity=new Point(-4*dir,0);
   if(!commit())throw new Exception("A closing defender's braking travel was ignored");results.Add("closing-defender-"+side+"-"+period);
   reset();owner.velocity=new Point(8*dir,0);
   if(!commit()||Point.Distance(defender.actionTarget,new Point(1.19f*dir,0))>.001f)throw new Exception("Prediction sent the carrier through the defender rather than judging the exposed ball before body contact");results.Add("ball-before-body-"+side+"-"+period);
   reset();defender.position=new Point(-.4f*dir,0);defender.angle=dir*(float)Math.PI*.5f;
   if(commit())throw new Exception("Standing commitment passed through shielding carrier");results.Add("shielded-ball-"+side+"-"+period);
   reset();defender.angle=dir*(float)Math.PI*.5f;
   if(commit())throw new Exception("Defender committed before turning toward the future ball");results.Add("behind-defender-"+side+"-"+period);
   foreach(string contact in new[]{"parallel-shoulder","body-first-crossing","ball-first-front","carrier-into-stationary-opponent"}){
    reset();owner.sentOff=defender.sentOff=false;s.State.professionalRules=true;s.State.restart=0;s.State.phase="play";
    owner.position=owner.previous=new Point();owner.angle=dir*(float)Math.PI*.5f;owner.velocity=new Point(2*dir,0);
    defender.previous=new Point(-.2f*dir,-.8f);defender.position=new Point(-.1f*dir,-.35f);defender.velocity=new Point(4*dir,6);
    if(contact=="parallel-shoulder")defender.velocity=new Point(2*dir,1.5f);
    if(contact=="ball-first-front"){defender.previous=new Point(.8f*dir,0);defender.position=new Point(.35f*dir,0);defender.velocity=new Point(-4*dir,0);}
    if(contact=="carrier-into-stationary-opponent"){
     owner.previous=new Point(dir*43.2f,0);owner.position=new Point(dir*43.6f,0);owner.velocity=new Point(dir*6,0);owner.angle=-dir*(float)Math.PI*.5f;
     defender.previous=defender.position=new Point(dir*44,0);defender.velocity=new Point();s.State.ball.position=owner.position-new Point(dir*.5f,0);
    }
    int fouls=s.State.metrics[1-side].fouls;seed=s.State.seed;s.ResolvePlayerContacts();
    bool called=s.State.metrics[1-side].fouls>fouls;
    if(called!=(contact=="body-first-crossing"))throw new Exception("Incorrect contact judgement: "+contact);
    if(seed!=s.State.seed)throw new Exception("Contact geometry consumed random balance roll");results.Add(contact+"-"+side+"-"+period);
   }
  }
  return new{passed=true,count=results.Count,scenarios=results};
 }
}
namespace Touchline.Tests {
 public sealed class EngineStandingAnticipationRegressionTests034 {
  [NUnit.Framework.Test]
  public void PhysicalScenariosPreserveContactAndDecisionInvariants() {
   NUnit.Framework.Assert.DoesNotThrow(()=>EngineStandingAnticipationScenarios034.Run());
  }
 }
}
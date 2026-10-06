using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Touchline.Core;
public static class EngineEmergencyBodyBlocksScenarios034 {
 public static object Run(bool unprepared){
  var rows=new List<object>();
  foreach(int side in new[]{0,1})foreach(int period in new[]{1,2}){
   var db=new Database{players=Enumerable.Range(0,40).Select(i=>new PlayerData{id="clear"+i,name="C"+i,team=i<20?"a":"b",position=i%20==0?"GB":i%20<5?"DEF":i%20<9?"MIL":"ATT",rating=80}).ToArray()};
   var c=new Career{club="a"};c.lineup=Career.Select(db,"a",c.tactic);var sim=MatchSimulation.Create(db,c,"b",73,2700);sim.State.period=period;sim.State.phase="play";sim.State.restart=0;
   foreach(var p in sim.State.actors)p.sentOff=true;
   var defender=sim.State.actors[side*11+2];var attacker=sim.State.actors[(1-side)*11+9];defender.sentOff=attacker.sentOff=false;int dir=sim.Direction(side);
   defender.position=defender.previous=new Point(-dir*40,0);defender.angle=dir*(float)Math.PI*.5f;attacker.position=attacker.previous=defender.position+new Point(dir*.8f,0);
   var receive=typeof(MatchSimulation).GetMethod("ResolveReception",BindingFlags.Instance|BindingFlags.NonPublic);
   Func<float,float> block=height=>{
    defender.controlTime=0;defender.action="idle";defender.actionTime=0;attacker.controlTime=1;
    sim.State.ball=new BallState{kind="cross",side=1-side,from=attacker.id,previous=defender.position+new Point(dir,0),position=defender.position-new Point(dir*.2f,0),velocity=new Point(-dir*20,0),height=height,previousHeight=height,elapsed=.5f};
    if(!(bool)receive.Invoke(sim,null)||defender.action!="block"||sim.State.ball.kind!="loose")throw new Exception("The physically reachable emergency impact lacked a contestable blocked rebound");
    return sim.State.ball.velocity.Length;
   };
   float foot=block(.2f);if(sim.State.ball.verticalVelocity<=0)throw new Exception("Low boot clearance lost its existing physical lift");
   if(unprepared){defender.angle=-dir*(float)Math.PI*.5f;float unexpected=block(.2f);if(unexpected>=foot*.5f)throw new Exception("An unexpected ball behind the defender became the same accurate upfield boot clearance");defender.angle=dir*(float)Math.PI*.5f;}
   float chest=block(1.2f);if(chest>=foot*.5f||sim.State.ball.verticalVelocity>=0)throw new Exception("A high body impact still became a precise high-energy boot delivery");
   if(sim.State.ball.owner!=null||defender.actionHeight<1)throw new Exception("High block manufactured possession or lost its actual torso contact height");
   rows.Add(new{side,period,foot,chest,height=defender.actionHeight});
  }
  return new{passed=true,count=rows.Count,rows};
 }
}
namespace Touchline.Tests {
 public sealed class EngineEmergencyBodyBlocksRegressionTests034 {
  [NUnit.Framework.Test]
  public void PhysicalScenariosPreserveContactAndDecisionInvariants() {
   NUnit.Framework.Assert.DoesNotThrow(()=>EngineEmergencyBodyBlocksScenarios034.Run(true));
  }
 }
}
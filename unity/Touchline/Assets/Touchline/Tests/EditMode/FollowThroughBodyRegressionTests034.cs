using System;
using System.Linq;
using System.Collections.Generic;
using Touchline.Core;
public static class CommittedKickerBodyScenarios034 {
 static MatchSimulation Create(int side,int period,out Actor passer,out Actor defender){
  var db=new Database{players=Enumerable.Range(0,40).Select(i=>new PlayerData{id="follow"+i,name="F"+i,team=i<20?"a":"b",position=i%20==0?"GB":i%20<5?"DEF":i%20<9?"MIL":"ATT",rating=75}).ToArray()};
  var c=new Career{club="a"};c.lineup=Career.Select(db,c.club,c.tactic);var s=MatchSimulation.Create(db,c,"b",73,2700);var m=s.State;
  m.period=period;m.clock=100;m.restart=0;m.phase="play";m.professionalRules=true;m.decision=100;
  foreach(var a in m.actors)a.sentOff=true;
  passer=m.actors[side*11+9];defender=m.actors[(1-side)*11+2];passer.sentOff=defender.sentOff=false;
  int dir=s.Direction(side);passer.position=passer.previous=new Point(dir*30,0);passer.angle=dir*(float)Math.PI*.5f;passer.velocity=new Point();passer.action="kick";passer.actionTime=.64f;passer.actionContactTime=.18f;
  defender.action="run";defender.actionTime=0;defender.angle=passer.angle;
  m.possessionSide=side;m.ball=new BallState{owner=null,side=side,lastTouch=side,lastTouchId=passer.id,from=passer.id,kind="pass",start=passer.position+new Point(dir*.42f,0),end=passer.position+new Point(dir*16,0),setupStart=passer.position+new Point(dir*.42f,0),setupHeight=.11f,startHeight=.11f,endHeight=.11f,height=.11f,previousHeight=.11f,releaseDelay=.18f,duration=.9f,elapsed=-.18f};m.ball.position=m.ball.previous=m.ball.start;
  return s;
 }
 public static object Run(){var rows=new List<object>();
  foreach(int side in new[]{0,1})foreach(int period in new[]{1,2}){
   var s=Create(side,period,out var passer,out var defender);int dir=s.Direction(side);
   var receiver=s.State.actors[side*11+8];receiver.sentOff=false;receiver.position=receiver.previous=passer.position+new Point(dir*16,0);receiver.controlTime=2;s.State.ball.to=receiver.id;
   defender.position=defender.previous=passer.position-new Point(dir*3,0);defender.velocity=new Point(dir*7,0);
   float minimum=100,lateral=0,rootDrift=0;var root=passer.position;
   for(int tick=0;tick<4;tick++){s.Advance(.1);minimum=Math.Min(minimum,Point.Distance(passer.position,defender.position));lateral=Math.Max(lateral,Math.Abs(defender.position.z));rootDrift=Math.Max(rootDrift,Point.Distance(root,passer.position));}
   rows.Add(new{side,period,situation="actual-release-pursuit",minimum,lateral,rootDrift,fouls=s.State.metrics.Sum(x=>x.fouls)});
   if((minimum<.65f||rootDrift>.0001f||s.State.metrics.Sum(x=>x.fouls)>0))throw new Exception("A preventable pursuit continued into the planted kicker's body through the actual release");

  }
  return new{passed=true,count=rows.Count,rows};
 }
}
namespace Touchline.Tests { public sealed class FollowThroughBodyRegressionTests034 { [NUnit.Framework.Test] public void ActualPassReleaseRetainsAvoidanceOfThePlantedKickerInBothHalves() => NUnit.Framework.Assert.DoesNotThrow(()=>CommittedKickerBodyScenarios034.Run()); } }

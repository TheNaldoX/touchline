using System;
using System.Linq;
using System.Collections.Generic;
using Touchline.Core;
public static class GoalMouthDecisionScenarios034 {
 public static object Run(){
  var rows=new List<object>();
  foreach(int side in new[]{0,1})foreach(int period in new[]{1,2})foreach(bool patient in new[]{false,true})foreach(bool wall in new[]{false,true}){
   var db=new Database{players=Enumerable.Range(0,40).Select(i=>new PlayerData{id="finish"+i,name="F"+i,team=i<20?"a":"b",position=i%20==0?"GB":i%20<5?"DEF":i%20<9?"MIL":"ATT",rating=90}).ToArray()};
   var c=new Career{club="a"};c.lineup=Career.Select(db,"a",c.tactic);var sim=MatchSimulation.Create(db,c,"b",73,2700);var m=sim.State;
   m.period=period;m.restart=0;m.phase="play";m.clock=100;sim.Tactic(side).workIntoBox=patient;
   foreach(var a in m.actors){a.sentOff=true;a.position=a.previous=new Point(0,30);}
   var striker=m.actors[side*11+9];var central=m.actors[(1-side)*11+2];var chasing=m.actors[(1-side)*11+4];
   striker.sentOff=central.sentOff=chasing.sentOff=false;int dir=sim.Direction(side);
   // This isolates the shooting corridor, with no keeper in the test. Keep
   // the inactive keeper reference on his line so it does not suggest a lob.
   m.actors[(1-side)*11].position=new Point(dir*51,0);
   striker.position=striker.previous=striker.carryTarget=new Point(dir*43,0);striker.angle=dir*(float)Math.PI*.5f;
   central.position=central.previous=new Point(dir*(wall?45:50),0);chasing.position=chasing.previous=new Point(dir*41,0);
   m.possessionSide=side;m.ball=new BallState{owner=striker.id,side=side,lastTouch=side,lastTouchId=striker.id,position=striker.position+new Point(dir*.43f,0),previous=striker.position+new Point(dir*.43f,0),controlOrigin=striker.position,kind="none"};
   string chosen=sim.Decide(striker);
   if(wall){if(chosen=="shot")throw new Exception("An opponent close enough to cover every achievable post lane did not prevent an unprepared pressured shot");rows.Add(new{side,period,patient,wall,chosen});continue;}
   if(chosen!="shot")throw new Exception("A chasing opponent and a defender guarding only goal centre prevented an achievable shot beside him");
   if(m.ball.shotOnTarget){
    float f=MatchSimulation.Projection(striker.position,new Point(dir*52.5f,m.ball.end.z),central.position);
    float gap=Point.Distance(central.position,Point.Lerp(striker.position,new Point(dir*52.5f,m.ball.end.z),f));
    if(gap<1.2f)throw new Exception("The decision recognised an open post but the accurate shot still aimed through the central blocker");
   }
   bool onTarget=m.ball.shotOnTarget;float endZ=m.ball.end.z;
   sim.Advance(.8);
   if(onTarget&&(m.score[side]!=1||m.events.Any(e=>e.kind=="block"&&e.player==central.id)))throw new Exception("The selected open lane did not survive the actual release, moving defender and swept ball contacts: side "+side+" period "+period+" score "+m.score[side]+" ball "+m.ball.kind+" phase "+m.phase+" events "+string.Join(";",m.events.Select(e=>e.kind+":"+e.player)));
   rows.Add(new{side,period,patient,chosen,onTarget,endZ,goal=m.score[side]});
  }
  return new{passed=true,count=rows.Count,rows};
 }
}
namespace Touchline.Tests { public class GoalMouthDecisionRegressionTests034 { [NUnit.Framework.Test] public void AchievablePostFinishesAndGenuinelyClosedGoalMouthsAreJudgedInBothHalves() => NUnit.Framework.Assert.DoesNotThrow(()=>GoalMouthDecisionScenarios034.Run()); } }
using System;
using System.Linq;
using System.Collections.Generic;
using Touchline.Core;
using Touchline;
public static class HighlightSelectionScenarios034 {
 public static object Run(){
  var rows=new List<object>();
  foreach(int side in new[]{0,1})foreach(int period in new[]{1,2}){
   var db=new Database{players=Enumerable.Range(0,40).Select(i=>new PlayerData{id="highlight"+i,name="H"+i,team=i<20?"a":"b",position=i%20==0?"GB":i%20<5?"DEF":i%20<9?"MIL":"ATT",rating=80}).ToArray()};
   var c=new Career{club="a"};c.lineup=Career.Select(db,"a",c.tactic);var sim=MatchSimulation.Create(db,c,"b",73,2700);
   var m=sim.State;m.period=period;m.phase="play";m.restart=0;m.clock=100;
   foreach(var a in m.actors){a.sentOff=true;a.position=a.previous=new Point(0,30);}
   var owner=m.actors[side*11+8];owner.sentOff=false;int dir=sim.Direction(side);
   owner.position=owner.previous=new Point(dir*34,25);owner.velocity=new Point();
   m.ball=new BallState{owner=owner.id,lastTouchId=owner.id,side=side,lastTouch=side,position=owner.position,kind="none",elapsed=-1};m.possessionSide=side;
   var view=new MatchBroadcast(sim){Enabled=true};m.clock=105;view.Refresh();
   if(!view.Quiet)throw new Exception("A stationary last-third winger still renewed the live view each tick");
   owner.position=new Point(dir*34,14);owner.velocity=new Point(0,5);m.ball.position=owner.position;m.clock=106;view.Refresh();
   if(!view.Quiet)throw new Exception("A lateral run away from the goal became a threatening penetration");
   owner.position=new Point(dir*43,3);owner.velocity=new Point(dir*4,0);m.ball.position=owner.position;m.clock=107;view.Refresh();
   if(view.Quiet)throw new Exception("A genuine open-goal finishing opportunity was hidden");
   owner.velocity=new Point();m.clock=112;view.Refresh();
   if(!view.Quiet)throw new Exception("Stationary ownership in a finishing area renewed an indefinite highlight");
   m.ball.owner=null;m.ball.kind="cross";m.ball.from=owner.id;m.ball.start=new Point(dir*34,24);m.ball.end=new Point(dir*44,0);m.ball.elapsed=.1f;owner.actionSequence++;
   m.clock=113;view.Refresh();if(view.Quiet)throw new Exception("A committed delivery towards the box was hidden");
   m.ball.elapsed=1;m.clock=118;view.Refresh();if(!view.Quiet)throw new Exception("The same delivery continuously reopened its expired window");
   m.ball.kind="pass";m.ball.end=new Point(dir*34,-24);m.ball.start=new Point(dir*34,24);m.ball.elapsed=0;owner.actionSequence++;
   m.clock=119;view.Refresh();if(!view.Quiet)throw new Exception("A lateral recycle pass opened an attacking highlight");
   foreach(var kind in new[]{"shot","save","woodwork","goal","red","injury"}){
    m.clock+=60;m.events.Add(new MatchEvent{kind=kind,side=side,player=owner.id,time=m.clock});view.Refresh();
    if(view.Quiet)throw new Exception("Important event hidden: "+kind);
    if(side==0&&(kind=="red"||kind=="injury")&&!view.PauseRequested)throw new Exception("Medical or dismissal decision interruption lost");
   }
   rows.Add(new{side,period,staticWingQuiet=true,lateralRecycleQuiet=true,goalOpportunityVisible=true,committedDeliveryVisible=true,importantEventsVisible=true});
  }
  return new{passed=true,count=rows.Count,rows};
 }
}
namespace Touchline.Tests { public class HighlightSelectionRegressionTests034 { [NUnit.Framework.Test] public void OnlyDangerousProgressAndCommittedActionsOpenHighlightsWhileEveryImportantEventRemainsVisible() => NUnit.Framework.Assert.DoesNotThrow(()=>HighlightSelectionScenarios034.Run()); } }
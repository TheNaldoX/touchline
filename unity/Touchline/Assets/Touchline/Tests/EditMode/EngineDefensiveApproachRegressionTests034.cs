using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Touchline.Core;
public static class EngineDefensiveApproachScenarios034 {
 public static object Run(){
  var rows=new List<object>();
  foreach(int side in new[]{0,1})foreach(int period in new[]{1,2}){
   var db=new Database{players=Enumerable.Range(0,40).Select(i=>new PlayerData{id="approach"+i,name="A"+i,team=i<20?"a":"b",position=i%20==0?"GB":i%20<5?"DEF":i%20<9?"MIL":"ATT",rating=75}).ToArray()};
   var c=new Career{club="a"};c.lineup=Career.Select(db,"a",c.tactic);var s=MatchSimulation.Create(db,c,"b",73,2700);
   s.State.period=period;s.State.restart=0;s.State.phase="play";s.State.professionalRules=true;s.State.decision=100;
   foreach(var actor in s.State.actors)actor.sentOff=true;
   int dir=s.Direction(side);var owner=s.State.actors[side*11+9];var defender=s.State.actors[(1-side)*11+2];owner.sentOff=defender.sentOff=false;
   owner.position=owner.previous=new Point(dir*30,0);owner.angle=dir*(float)Math.PI*.5f;owner.velocity=new Point(dir*2,0);owner.carryTarget=new Point(dir*45,0);
   defender.position=defender.previous=new Point(dir*27,0);defender.velocity=new Point(dir*7,0);defender.angle=owner.angle;owner.action=defender.action="run";owner.actionTime=defender.actionTime=0;
   var ball=owner.position+new Point(dir*.5f,0);s.State.ball=new BallState{owner=owner.id,side=side,lastTouch=side,lastTouchId=owner.id,kind="none",position=ball,previous=ball,controlOrigin=ball,controlElapsed=1,height=.11f,previousHeight=.11f};s.State.possessionSide=side;
   var target=typeof(MatchSimulation).GetMethod("ShieldSafePressTarget",BindingFlags.NonPublic|BindingFlags.Instance);
   var q=(Point)target.Invoke(s,new object[]{defender,owner,ball+new Point(dir*.65f,0)});
   if(Math.Abs(q.z)<.7f)throw new Exception("Shielding carrier was targeted through his centre");
   float lateral=0,minDistance=100;
   for(int tick=0;tick<24;tick++){s.Advance(.1);lateral=Math.Max(lateral,Math.Abs(defender.position.z-owner.position.z));minDistance=Math.Min(minDistance,Point.Distance(defender.position,owner.position));}
   if(lateral<.65f)throw new Exception("Pursuer did not actually take an outside shoulder");
   if(s.State.events.Any(e=>e.kind=="foul"||e.kind=="penalty"))throw new Exception("Controlled pursuit manufactured a violent charge");
   // Pressé, le porteur peut désormais jouer le ballon tout de suite ou se le faire chiper.
   bool released=s.State.ball.from==owner.id,poked=s.State.ball.owner==defender.id&&s.State.events.Any(e=>e.kind=="tackle");
   if(s.State.ball.owner!=owner.id&&s.State.ball.kind!="loose"&&!released&&!poked)throw new Exception("Unexpected isolated possession result");
   rows.Add(new{side,period,lateral,minDistance,events=s.State.events.Select(e=>e.kind).ToArray()});
   defender.position=new Point(dir*32,0);defender.velocity=new Point(dir,0);defender.angle=dir*(float)Math.PI*.5f;defender.action="run";defender.actionTime=0;
   owner.position=new Point(dir*30,0);owner.velocity=new Point();s.State.ball.owner=owner.id;s.State.ball.position=new Point(dir*30.5f,0);
   var mover=typeof(MatchSimulation).GetMethod("MoveActor",BindingFlags.NonPublic|BindingFlags.Instance);
   for(int tick=0;tick<10;tick++)mover.Invoke(s,new object[]{defender,defender.position+new Point(dir*2,0),2.5f});
   var facing=new Point((float)Math.Sin(defender.angle),(float)Math.Cos(defender.angle));float towardBall=Point.Dot(facing,(s.State.ball.position-defender.position).Normalized);
   if(towardBall<.96f||Point.Dot(defender.velocity,(defender.position-s.State.ball.position).Normalized)<.4f)throw new Exception("A slowly retreating defender turned his back to the ball instead of jockeying");
   rows.Add(new{side,period,action="backpedal-facing",towardBall});
   foreach(string action in new[]{"tackle","kick"}){
    var planted=MatchSimulation.Create(db,c,"b",73,2700);planted.State.period=period;planted.State.restart=0;planted.State.phase="play";planted.State.decision=100;
    foreach(var actor in planted.State.actors)actor.sentOff=true;
    var holder=planted.State.actors[side*11+9];var actorPlanted=planted.State.actors[(1-side)*11+2];holder.sentOff=actorPlanted.sentOff=false;
    holder.position=holder.previous=new Point(dir*30,0);holder.angle=dir*(float)Math.PI*.5f;holder.velocity=new Point();holder.carryTarget=holder.position;holder.controlTime=10;
    actorPlanted.position=actorPlanted.previous=new Point(dir*30.8f,0);actorPlanted.velocity=new Point();actorPlanted.action=action;actorPlanted.actionTime=.75f;actorPlanted.actionKind=action=="tackle"?MatchSimulation.StandingDuel:"pass";actorPlanted.actionTarget=new Point(dir*30.4f,0);actorPlanted.angle=-dir*(float)Math.PI*.5f;
    planted.State.ball=new BallState{owner=holder.id,side=side,lastTouch=side,lastTouchId=holder.id,kind="none",position=new Point(dir*30.5f,0),controlOrigin=new Point(dir*30.5f,0),height=.11f};planted.State.possessionSide=side;
    var root=actorPlanted.position;planted.Advance(.2);
    if(Point.Distance(root,actorPlanted.position)>.0001f)throw new Exception("Neighbour steering slid a planted "+action+" out of its contact point");
    rows.Add(new{side,period,action,rootDrift=Point.Distance(root,actorPlanted.position)});
   }
  }
  return new{passed=true,count=rows.Count,rows};
 }
}
namespace Touchline.Tests {
 public sealed class EngineDefensiveApproachRegressionTests034 {
  [NUnit.Framework.Test]
  public void PhysicalScenariosPreserveContactAndDecisionInvariants() {
   NUnit.Framework.Assert.DoesNotThrow(()=>EngineDefensiveApproachScenarios034.Run());
  }
 }
}
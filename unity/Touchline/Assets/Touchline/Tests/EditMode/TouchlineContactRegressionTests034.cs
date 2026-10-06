using System;
using System.Linq;
using System.Collections.Generic;
using Touchline.Core;
public static class TouchlineContactScenarios034 {
 public static object Run(){
  var rows=new List<object>();
  foreach(int side in new[]{0,1})foreach(int period in new[]{1,2})foreach(int flank in new[]{-1,1})foreach(string action in new[]{"kick","tackle"}){
   var db=new Database{players=Enumerable.Range(0,40).Select(i=>new PlayerData{id="edge"+i,name="E"+i,team=i<20?"a":"b",position=i%20==0?"GB":i%20<5?"DEF":i%20<9?"MIL":"ATT",rating=80}).ToArray()};
   var c=new Career{club="a"};c.lineup=Career.Select(db,"a",c.tactic);var sim=MatchSimulation.Create(db,c,"b",73,2700);var m=sim.State;
   m.period=period;m.restart=0;m.phase="play";m.clock=100;m.decision=10;
   foreach(var a in m.actors){a.sentOff=true;a.position=a.previous=new Point();}
   var player=m.actors[side*11+8];player.sentOff=false;int dir=sim.Direction(side);
   var origin=new Point(dir*51.8f,flank*33.6f);player.position=player.previous=origin;player.action=action;player.actionTime=.6f;player.actionTarget=origin;player.actionContactTime=.2f;player.angle=-flank*(float)Math.PI*.5f;
   if(action=="tackle")player.actionKind=MatchSimulation.StandingDuel;
   m.possessionSide=side;m.ball=new BallState{owner=null,side=side,lastTouch=side,lastTouchId=player.id,from=player.id,kind="pass",start=origin,end=origin+new Point(-dir*15,-flank*10),position=origin,previous=origin,duration=1,elapsed=-.4f,height=.11f,previousHeight=.11f};
   sim.Advance(.2);
   float drift=Point.Distance(player.position,origin);
   if(drift>.00001f)throw new Exception("A planted "+action+" was moved "+drift+"m by formation margins despite a legal touchline contact point");
   rows.Add(new{side,period,flank,action,drift});
  }
  foreach(int side in new[]{0,1})foreach(int period in new[]{1,2})foreach(int flank in new[]{-1,1}){
   var db=new Database{players=Enumerable.Range(0,40).Select(i=>new PlayerData{id="press-edge"+i,name="PE"+i,team=i<20?"a":"b",position=i%20==0?"GB":i%20<5?"DEF":i%20<9?"MIL":"ATT",rating=80}).ToArray()};
   var c=new Career{club="a"};c.lineup=Career.Select(db,"a",c.tactic);var sim=MatchSimulation.Create(db,c,"b",73,2700);var m=sim.State;
   m.period=period;m.restart=0;m.phase="play";m.clock=100;m.decision=10;
   foreach(var a in m.actors)a.sentOff=true;
   var owner=m.actors[side*11+8];var defender=m.actors[(1-side)*11+9];owner.sentOff=defender.sentOff=false;int dir=sim.Direction(side);
   owner.position=owner.previous=owner.carryTarget=new Point(dir*20,flank*33.6f);owner.controlTime=2;owner.angle=dir*(float)Math.PI*.5f;
   defender.position=defender.previous=owner.position+new Point(0,-flank*1.8f);defender.angle=flank>0?0:(float)Math.PI;
   m.possessionSide=side;m.ball=new BallState{owner=owner.id,side=side,lastTouch=side,lastTouchId=owner.id,position=owner.position+new Point(dir*.43f,0),previous=owner.position+new Point(dir*.43f,0),controlOrigin=owner.position+new Point(dir*.43f,0)};
   sim.Advance(.2);var target=sim.MovementTarget((1-side)*11+9);
   if(defender.intent!="press"&&defender.intent!="delay"&&defender.intent!="cover"&&defender.intent!="mark-carrier")throw new Exception("The near-line defender did not actively track the controlled ball");
   if(Math.Abs(target.z)<=32.55f)throw new Exception("The pressing defender stopped at a formation margin instead of approaching the legally controlled near-line ball");
   rows.Add(new{side,period,flank,action=defender.intent,targetZ=target.z,defenderZ=defender.position.z});
  }
  return new{passed=true,count=rows.Count,rows};
 }
}
namespace Touchline.Tests { public class TouchlineContactRegressionTests034 { [NUnit.Framework.Test] public void PlantedActionsKeepTheirLegalRootsAndPursuitCanReachNearLineCarriersInBothHalves() => NUnit.Framework.Assert.DoesNotThrow(()=>TouchlineContactScenarios034.Run()); } }
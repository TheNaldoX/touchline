using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Touchline.Core;
public static class EngineOffsideReleaseScenarios034 {
 public static object Run(){
  var rows=new List<object>();
  foreach(int side in new[]{0,1})foreach(int period in new[]{1,2}){
   var db=new Database{players=Enumerable.Range(0,40).Select(i=>new PlayerData{id="off"+i,name="O"+i,team=i<20?"a":"b",position=i%20==0?"GB":i%20<5?"DEF":i%20<9?"MIL":"ATT",rating=80}).ToArray()};
   var c=new Career{club="a"};c.lineup=Career.Select(db,"a",c.tactic);var sim=MatchSimulation.Create(db,c,"b",73,2700);sim.State.period=period;
   var kicker=sim.State.actors[side*11+6];var intended=sim.State.actors[side*11+5];var other=sim.State.actors[side*11+9];
   var defender=sim.State.actors[(1-side)*11+2];var keeper=sim.State.actors[(1-side)*11];int dir=sim.Direction(side);
   var flags=BindingFlags.Instance|BindingFlags.NonPublic;
   var flight=typeof(MatchSimulation).GetMethod("Flight",flags);var reception=typeof(MatchSimulation).GetMethod("ResolveReception",flags);
   var offside=typeof(MatchSimulation).GetMethod("OffsideReception",flags);var control=typeof(MatchSimulation).GetMethod("Control",flags);var update=typeof(MatchSimulation).GetMethod("UpdateBall",flags);
   Action reset=()=>{
    foreach(var p in sim.State.actors){p.sentOff=true;p.velocity=new Point();p.controlTime=0;p.action="idle";p.actionTime=0;}
    foreach(var p in new[]{kicker,intended,other,defender,keeper})p.sentOff=false;
    kicker.position=kicker.previous=new Point(dir*20,0);intended.position=intended.previous=new Point(dir*35,0);other.position=other.previous=new Point(dir*45,0);
    defender.position=defender.previous=new Point(dir*40,5);keeper.position=keeper.previous=new Point(dir*50,0);
    sim.State.phase="play";sim.State.restart=0;sim.State.ball=new BallState{owner=kicker.id,side=side,position=kicker.position,height=.11f};sim.State.possessionSide=side;
   };
   Action<bool,string> delivery=(exempt,kind)=>{sim.State.ball.restartExemption=exempt;flight.Invoke(sim,new object[]{kicker,kind=="shot"?null:intended,kind,new Point(dir*48,0),.11f,1f,.1f});};
   Func<Actor,bool> infringement=player=>(bool)offside.Invoke(sim,new object[]{player});
   Func<Actor,bool> flagged=player=>(bool)typeof(MatchSimulation).GetMethod("InOffsideSnapshot",flags).Invoke(sim,new object[]{player});
   reset();delivery(false,"pass");if(sim.State.ball.offsidePlayersMask>=0||flagged(other))throw new Exception("Snapshot lacked a durable pending marker or was taken at a decision before kick contact");sim.Advance(.2);
   if(sim.State.ball.offsidePlayersMask<=0||!flagged(other)||flagged(intended))throw new Exception("The actual release snapshot ignored an unintended offside attacker or marked the legal intended receiver");
   other.position=new Point(dir*39,0);if(!infringement(other)||!sim.State.indirectRestart)throw new Exception("An unintended attacker coming back from offside escaped the touch offence");rows.Add(new{side,period,scenario="unintended-attacker"});
   reset();delivery(false,"pass");sim.Advance(.2);intended.position=new Point(dir*45,0);if(infringement(intended))throw new Exception("A legal attacker running beyond the defence after release was punished");rows.Add(new{side,period,scenario="late-run"});
   foreach(string kind in new[]{"pass","cross","throw"}){reset();delivery(true,kind);sim.Advance(kind=="throw"?.6:.2);if(sim.State.ball.offsidePlayersMask<=0||infringement(other))throw new Exception("A direct exempt restart invented offside");rows.Add(new{side,period,scenario="restart-exemption-"+kind});}
   reset();delivery(false,"shot");sim.Advance(.2);
   typeof(MatchSimulation).GetMethod("LooseBall",flags).Invoke(sim,new object[]{other.position,new Point(),.11f,0f,1-side,keeper.id});
   sim.State.ball.previous=sim.State.ball.position;sim.State.ball.previousHeight=sim.State.ball.height;
   if(!(bool)reception.Invoke(sim,null)||sim.State.phase!="free-kick"||sim.State.restartSide!=1-side)throw new Exception("A passive defensive save/deflection erased the shot-release offside snapshot");rows.Add(new{side,period,scenario="save-rebound"});
   reset();delivery(false,"pass");sim.Advance(.2);control.Invoke(sim,new object[]{defender});if(infringement(other))throw new Exception("Deliberate controlled defensive possession retained a historical attacking restriction");rows.Add(new{side,period,scenario="deliberate-control"});
   foreach(float remaining in new[]{.02f,.08f}){
    reset();delivery(false,"pass");sim.State.ball.elapsed=-remaining;other.previous=new Point(dir*45,0);other.position=new Point(dir*35,0);update.Invoke(sim,null);
    bool captured=flagged(other);if(captured!=(remaining<.05f))throw new Exception("Release positions were not interpolated to the actual ball-contact fraction of the physical tick");rows.Add(new{side,period,scenario="release-interpolation",remaining,captured});
   }
   reset();delivery(false,"pass");sim.Advance(.2);int firstAt=Array.IndexOf(sim.State.actors,intended),secondAt=Array.IndexOf(sim.State.actors,other);int oldSlot=intended.slot;
   sim.State.actors[firstAt]=other;sim.State.actors[secondAt]=intended;intended.slot=other.slot;other.slot=oldSlot;
   if(!flagged(other)||flagged(intended))throw new Exception("A tactical positional swap transferred the historical offside restriction to another player");rows.Add(new{side,period,scenario="tactical-permutation"});
   reset();kicker.position=kicker.previous=new Point(dir*45,0);intended.position=intended.previous=new Point(dir*43,0);other.position=other.previous=new Point(dir*48,0);
   flight.Invoke(sim,new object[]{kicker,intended,"pass",new Point(dir*35,0),.11f,1f,.1f});sim.Advance(.2);
   if(!flagged(other)||flagged(intended))throw new Exception("Backward delivery direction replaced the legal position-based ball/second-defender test");other.position=new Point(dir*37,0);
   if(!infringement(other))throw new Exception("An attacker offside at the kick escaped by running backwards onto a backpass");rows.Add(new{side,period,scenario="backpass-position"});
   reset();sim.State.ball.offside=true;sim.State.ball.to=intended.id;sim.State.ball.offsidePlayersMask=0;if(!infringement(intended))throw new Exception("A legacy save lost its known intended offside flag");rows.Add(new{side,period,scenario="legacy-save"});
  }
  return new{passed=true,count=rows.Count,rows};
 }
}
namespace Touchline.Tests {
 public sealed class EngineOffsideReleaseRegressionTests034 {
  [NUnit.Framework.Test]
  public void OffsideUsesActualReleaseAndSurvivesDeflectionsAndTacticalSwaps() {
   NUnit.Framework.Assert.DoesNotThrow(()=>EngineOffsideReleaseScenarios034.Run());
  }
  [NUnit.Framework.TestCase(0)]
  [NUnit.Framework.TestCase(-1)]
  [NUnit.Framework.TestCase(1<<22)]
  [NUnit.Framework.TestCase((1<<22)|(1<<21))]
  public void SnapshotMaskSurvivesUnitySave(int mask) {
   var ball=new Touchline.Core.BallState{offsidePlayersMask=mask};
   var restored=UnityEngine.JsonUtility.FromJson<Touchline.Core.BallState>(UnityEngine.JsonUtility.ToJson(ball));
   NUnit.Framework.Assert.AreEqual(mask,restored.offsidePlayersMask);
  }
  [NUnit.Framework.Test]
  public void OlderSaveRetainsItsUnknownSnapshotAndKnownTargetFlag() {
   var ball=UnityEngine.JsonUtility.FromJson<Touchline.Core.BallState>("{\"offside\":true,\"to\":\"legacy-player\"}");
   NUnit.Framework.Assert.AreEqual(0,ball.offsidePlayersMask);
   NUnit.Framework.Assert.IsTrue(ball.offside);
   NUnit.Framework.Assert.AreEqual("legacy-player",ball.to);
  }
 }
}
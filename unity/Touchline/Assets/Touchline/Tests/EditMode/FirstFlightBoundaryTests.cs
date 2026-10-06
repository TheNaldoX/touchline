using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Touchline.Core;
namespace Touchline.Tests {
 public class FirstFlightBoundaryTests {
 static readonly MethodInfo Update=typeof(MatchSimulation).GetMethod("UpdateBall",BindingFlags.Instance|BindingFlags.NonPublic);
 static MatchSimulation Fixture(float fromX,float fromZ,float endX,float endZ,float height,float endHeight,float duration=.28f,float loft=.1f,string kind="shot"){
  var actor=new Actor{id="kicker",side=0,slot=9,position=new Point(fromX,fromZ),previous=new Point(fromX,fromZ),action="header",actionSequence=1,controlTime=100};
  var ball=new BallState{kind=kind,from="kicker",side=0,lastTouch=0,lastTouchId="kicker",offsidePlayersMask=1<<22,goalAttempt=kind=="shot",shotOnTarget=true,shotSide=0,position=actor.position,previous=actor.position,height=height,previousHeight=height,setupStart=actor.position,setupHeight=height,start=actor.position,startHeight=height,end=new Point(endX,endZ),endHeight=endHeight,elapsed=-.02f,releaseDelay=.12f,duration=duration,loft=loft,fixedStart=true};
  var db=new Database{players=new[]{new PlayerData{id="kicker",name="Fixture synthétique",rating=75,heightCm=188}}};
  return new MatchSimulation(db,new MatchState{home="h",away="a",phase="play",restart=0,engineVersion=4,homeTactic=new Tactic(),awayTactic=new Tactic(),actors=new[]{actor},ball=ball});
 }

 [Test] public void HeaderAboveTheTopOfThePostCannotUseTheLowerPreparationChord(){
  float h=2.58f-.1f*(float)Math.Sin(Math.PI*.1f)+.001f;
  var sim=Fixture(52.4f,3.6f,53.4f,5.7f,h,h);sim.State.ball.shotOnTarget=false;Update.Invoke(sim,null);
  Assert.IsFalse(sim.State.events.Any(e=>e.kind=="woodwork"));Assert.AreEqual("goal-kick",sim.State.phase);
 }
 [Test] public void RestoredBallBeyondTheFrontPlaneMustStillPassTheWholeGoalLineAtItsTrueHeight(){
  float u=(52.61f-52.55f)/.85f,h=2.44f-.1f*(float)Math.Sin(Math.PI*u)+.001f;
  var sim=Fixture(52.55f,0,53.4f,0,h,h);Update.Invoke(sim,null);
  Assert.AreEqual(0,sim.State.score.Sum());Assert.AreEqual("goal-kick",sim.State.phase);
 }
 [Test] public void RealPostUsesIncomingFlightVelocityForItsRebound(){
  float h=2.44f-.1f*(float)Math.Sin(Math.PI*.1f);
  var sim=Fixture(52.4f,3.6f,53.4f,5.7f,h,h);sim.State.ball.shotOnTarget=false;Update.Invoke(sim,null);
  Assert.IsTrue(sim.State.events.Any(e=>e.kind=="woodwork"));Assert.AreEqual(-(1/.28f)*.48f,sim.State.ball.velocity.x,.0001f);
 }
 [TestCase(1f,"goal",1)][TestCase(2.8f,"goal-kick",0)]
 public void StraightFlightRetainsTheExpectedOutcome(float height,string phase,int goals){
  var sim=Fixture(52.55f,0,53.4f,0,height,height,.28f,0);Update.Invoke(sim,null);
  Assert.AreEqual(phase,sim.State.phase);Assert.AreEqual(goals,sim.State.score.Sum());
 }
 [Test] public void InwardCornerCannotBeClassifiedAsAnExit(){
  var sim=Fixture(52.5f,33.8f,50.8f,32,1.4f,1.4f,.6f,1.2f,"cross");Update.Invoke(sim,null);
  Assert.AreEqual(0,sim.State.restart);Assert.AreEqual("play",sim.State.phase);Assert.IsEmpty(sim.State.events);
 }
 }
}

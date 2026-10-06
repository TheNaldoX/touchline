using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Touchline.Core;

namespace Touchline.Tests
{
    public class ReboundPossessionTests
    {
        MatchSimulation Create(int defending)
        {
            var db=new Database{players=Enumerable.Range(0,40).Select(i=>new PlayerData{id="r"+i,name="R"+i,team=i<20?"a":"b",position=i%20==0?"GB":i%20<5?"DEF":i%20<9?"MIL":"ATT",rating=75}).ToArray()};
            var career=new Career{club="a"};career.lineup=Career.Select(db,"a",career.tactic);var sim=MatchSimulation.Create(db,career,"b");
            var m=sim.State;m.restart=0;m.phase="play";m.clock=100;m.turnoverAt=-100;m.decision=100;m.possessionSide=1-defending;
            foreach(var actor in m.actors)actor.previous=actor.position=new Point(0,actor.slot*2-10);
            var keeper=m.actors[defending*11];keeper.previous=keeper.position=new Point(-sim.Direction(defending)*48,1);keeper.action="dive";keeper.actionTime=1;keeper.controlTime=.45f;
            var point=keeper.position+new Point(sim.Direction(defending)*3,1);
            m.ball=new BallState{kind="loose",previous=point,position=point,height=.5f,previousHeight=.5f,side=defending,lastTouch=defending,lastTouchId=keeper.id,goalAttempt=true,shotSide=1-defending};return sim;
        }
        [TestCase(0)] [TestCase(1)] public void DeflectionRetainsDefensiveShapeAndDoesNotInventPossession(int defending)
        {
            var sim=Create(defending);sim.Advance(.1);var m=sim.State;
            Assert.AreEqual(1-defending,MatchSimulation.PossessionSide(m));Assert.AreEqual(defending,m.ball.lastTouch,"Restart attribution must retain the actual last touch");
            Assert.That(m.metrics[1-defending].possessionSeconds,Is.EqualTo(.1f).Within(.001f));Assert.AreEqual(0,m.metrics[defending].possessionSeconds);
            foreach(var actor in m.actors.Where(a=>a.side==defending&&a.slot>0)){
                Assert.IsFalse(new[]{"support","counter","run-behind","near-post","far-post","box-arrival"}.Contains(actor.intent),"A deflection must not send the defending team into attacking shape");
                Assert.IsTrue(PlayerMotionContext.From(m,actor,1).defending,"Animation and simulation must agree on the defending side");
            }
        }
        [Test] public void SecureRecoveryChangesPossessionAndCountsOneTurnover()
        {
            var sim=Create(0);var m=sim.State;m.homeTactic.counterAttack=true;
            var control=typeof(MatchSimulation).GetMethod("Control",BindingFlags.Instance|BindingFlags.NonPublic);
            control.Invoke(sim,new object[]{m.actors[2]});Assert.AreEqual(0,MatchSimulation.PossessionSide(m));Assert.AreEqual(1,m.metrics[0].recoveries);Assert.AreEqual(1,m.metrics[0].counters);Assert.AreEqual(m.clock,m.turnoverAt);
            control.Invoke(sim,new object[]{m.actors[3]});Assert.AreEqual(1,m.metrics[0].recoveries);Assert.AreEqual(1,m.metrics[0].counters);
        }
        [TestCase(false,0)] [TestCase(true,1)] public void LegacyUnknownControllerHasDeterministicFallback(bool shot,int expected)
        {
            var m=new MatchState{possessionSide=-1,ball=new BallState{kind="loose",side=0,goalAttempt=shot,shotSide=1}};
            Assert.AreEqual(expected,MatchSimulation.PossessionSide(m));m.ball.owner="";Assert.AreEqual(expected,MatchSimulation.PossessionSide(m),"Unity may restore an absent owner as an empty string");m.ball.owner="secure-owner";Assert.AreEqual(0,MatchSimulation.PossessionSide(m));
        }
        [Test] public void SavedEmptyOwnerDoesNotCreateANewBallRequestGesture()
        {
            var sim=Create(0);var actor=sim.State.actors[20];actor.intent="near-post";actor.position=sim.State.ball.position;sim.State.ball.side=actor.side;sim.State.clock=1;sim.State.ball.owner=null;
            var before=PlayerMotionContext.From(sim.State,actor,1);sim.State.ball.owner="";var after=PlayerMotionContext.From(sim.State,actor,1);
            Assert.IsTrue(before.Same(after));Assert.AreEqual(0,after.requestWeight);
        }
    }
}

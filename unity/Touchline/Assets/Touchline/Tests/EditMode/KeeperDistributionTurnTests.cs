using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Touchline.Core;
using UnityEngine;

namespace Touchline.Tests
{
    public class KeeperDistributionTurnTests
    {
        Database db;MatchSimulation sim;Actor keeper,mate;
        bool Begin()=>(bool)typeof(MatchSimulation).GetMethod("BeginHandDistribution",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(sim,new object[]{keeper});
        [SetUp] public void Setup()
        {
            db=new Database{players=Enumerable.Range(0,40).Select(i=>new PlayerData{id="turn-"+i,name="P"+i,team=i<20?"a":"b",position=i%20==0?"GB":"MIL",rating=75}).ToArray()};
            var career=new Career{club="a"};career.lineup=Career.Select(db,"a",career.tactic);sim=MatchSimulation.Create(db,career,"b");
            var state=sim.State;state.restart=0;state.phase="play";state.decision=10;
            foreach(var actor in state.actors){actor.previous=actor.position=new Point(20,actor.slot*2-10);actor.velocity=new Point();}
            keeper=state.actors[0];mate=state.actors[2];keeper.previous=keeper.position=new Point(-48,0);mate.previous=mate.position=new Point(-35,0);
            keeper.angle=-Mathf.PI/2;keeper.action="keeper-hold";keeper.actionTime=0;
            state.ball=new BallState{owner=keeper.id,held=true,side=0,lastTouch=0,position=new Point(-48.36f,0),previous=new Point(-48.36f,0),height=1.1f,previousHeight=1.1f};
        }
        [Test] public void TurnSecuresBallBeforeAnyPassOrArmPreparation()
        {
            // Keep the available support still to isolate turning from recipient re-selection.
            mate.action="throw";mate.actionTime=10;
            uint seed=sim.State.seed;Assert.IsTrue(Begin());
            Assert.AreEqual("keeper-hold",keeper.action);Assert.AreEqual(MatchSimulation.KeeperDistributionTurn,keeper.actionKind);
            Assert.AreEqual(keeper.id,sim.State.ball.owner);Assert.IsTrue(sim.State.ball.held);Assert.AreEqual(0,sim.State.passes[0]);Assert.AreEqual(seed,sim.State.seed);
            Assert.That(keeper.actionTime,Is.InRange(.25f,.85f));Assert.AreEqual(-Mathf.PI/2,keeper.angle);
            float previous=keeper.angle;
            for(int i=0;i<12&&!MatchSimulation.HandDistribution(keeper.action);i++){
                sim.Advance(.1);Assert.LessOrEqual(Mathf.Abs(Mathf.DeltaAngle(previous*Mathf.Rad2Deg,keeper.angle*Mathf.Rad2Deg)),5*MatchSimulation.Step*Mathf.Rad2Deg+.001f);previous=keeper.angle;
            }
            Assert.IsTrue(MatchSimulation.HandDistribution(keeper.action));Assert.AreEqual(1,sim.State.passes[0]);
            Assert.Less(Mathf.Abs(Mathf.DeltaAngle(keeper.angle*Mathf.Rad2Deg,90)),9);
        }
        [Test] public void SaveDuringTurnResumesIdentically()
        {
            Begin();sim.Advance(.2);var restored=new MatchSimulation(db,JsonUtility.FromJson<MatchState>(JsonUtility.ToJson(sim.State)));
            sim.Advance(4);restored.Advance(4);Assert.AreEqual(JsonUtility.ToJson(sim.State),JsonUtility.ToJson(restored.State));
        }
        [Test] public void LostRecipientFallsBackWithoutAnEndlessTurn()
        {
            Begin();mate.sentOff=true;sim.Advance(1);Assert.AreNotEqual(MatchSimulation.KeeperDistributionTurn,keeper.actionKind);Assert.AreEqual(0,sim.State.passes[0]);
        }
        [TestCase(0)] [TestCase(90)] [TestCase(180)] [TestCase(270)]
        public void WindUpLookRemainsDirectedAtRecipientInsteadOfOwnBall(float degrees)
        {
            float angle=degrees*Mathf.Deg2Rad;var forward=new Point(Mathf.Sin(angle),Mathf.Cos(angle));var right=new Point(forward.z,-forward.x);
            keeper.action="keeper-roll";keeper.actionTarget=keeper.position+forward*.52f-right*.20f;
            var look=MatchSimulation.KeeperDistributionLook(keeper,new Point(-1,0));Assert.Greater(Point.Dot(look,forward),.9999f);
        }
        [Test] public void OrdinaryKeeperAndOutfielderLookAreUnchanged()
        {
            var fallback=new Point(3,4);Assert.AreEqual(fallback,MatchSimulation.KeeperDistributionLook(keeper,fallback));
            mate.action="keeper-roll";Assert.AreEqual(fallback,MatchSimulation.KeeperDistributionLook(mate,fallback));
        }
        [Test] public void AlreadyFacingRecipientDoesNotAddAnUnnecessaryDelay()
        {
            keeper.angle=Mathf.PI/2;Assert.IsTrue(Begin());Assert.AreEqual("keeper-roll",keeper.action);Assert.AreEqual(1,sim.State.passes[0]);
        }
        [Test] public void UnsupportedLegacyReleaseGeometryKeepsExistingLook()
        {
            keeper.action="keeper-roll";keeper.actionTarget=new Point(0,0);var fallback=new Point(1,0);Assert.AreEqual(fallback,MatchSimulation.KeeperDistributionLook(keeper,fallback));
        }
    }
}

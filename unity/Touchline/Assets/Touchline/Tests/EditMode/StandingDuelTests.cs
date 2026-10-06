using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Touchline.Core;

namespace Touchline.Tests
{
    public class StandingDuelTests
    {
        Database db;MatchSimulation sim;Actor owner,defender;
        object Call(string name,params object[] args)=>typeof(MatchSimulation).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(sim,args);
        [SetUp] public void Setup()
        {
            db=new Database{players=Enumerable.Range(0,40).Select(i=>new PlayerData{id="p"+i,name="P"+i,team=i<20?"a":"b",position=i%20==0?"GB":i%20<5?"DEF":i%20<9?"MIL":"ATT",rating=75}).ToArray()};
            var career=new Career{club="a"};career.lineup=Career.Select(db,"a",career.tactic);sim=MatchSimulation.Create(db,career,"b");
            sim.State.restart=0;sim.State.phase="play";sim.State.decision=10;
            foreach(var actor in sim.State.actors){actor.sentOff=true;actor.position=actor.previous=new Point(30,20);}
            owner=sim.State.actors[9];defender=sim.State.actors[12];owner.sentOff=defender.sentOff=false;
            owner.previous=owner.position=owner.carryTarget=new Point();owner.angle=0;
            defender.previous=defender.position=new Point(.8f,.43f);defender.angle=-Mathf.PI*.5f;
            sim.State.possessionSide=0;sim.State.ball=new BallState{owner=owner.id,side=0,lastTouch=0,lastTouchId=owner.id,position=new Point(0,.43f),previous=new Point(0,.43f),controlOrigin=new Point(0,.43f)};
        }
        void Begin(){Assert.IsTrue((bool)Call("BeginStandingDuel",defender,owner));}
        [Test] public void PreparationDoesNotChangeBallOrResolveBeforeContact()
        {
            uint seed=sim.State.seed;int events=sim.State.events.Count;Begin();
            Assert.AreEqual(owner.id,sim.State.ball.owner);Assert.AreEqual(seed,sim.State.seed);Assert.AreEqual(events,sim.State.events.Count);
            sim.Advance(.1);Assert.AreEqual(owner.id,defender.tackleOpponent);Assert.AreEqual(owner.id,sim.State.ball.owner);
            Assert.Greater(defender.actionTime,MatchSimulation.TackleRecovery);Assert.IsFalse(sim.State.events.Any(e=>e.kind=="tackle"||e.kind=="foul"));
            sim.Advance(.1);Assert.IsTrue(string.IsNullOrEmpty(defender.tackleOpponent));
            Assert.That(defender.actionTime,Is.EqualTo(MatchSimulation.TackleRecovery).Within(.0001f));
            Assert.Greater(defender.controlTime,.1f,"A planted tackle cannot turn into ball control in the same instant");
        }
        [Test] public void CarrierCanEscapeDuringCommittedIntervention()
        {
            Begin();owner.position=owner.previous=owner.carryTarget=new Point(4,0);sim.Advance(.2);
            Assert.AreEqual(owner.id,sim.State.ball.owner);Assert.IsTrue(string.IsNullOrEmpty(defender.tackleOpponent));
            Assert.IsFalse(sim.State.events.Any(e=>e.kind=="tackle"||e.kind=="foul"));Assert.Greater(defender.duelCooldown,0);
        }
        [Test] public void ReleasedPassCancelsTheCommittedTarget()
        {
            Begin();sim.State.ball.owner=null;sim.State.ball.kind="pass";sim.State.ball.start=sim.State.ball.position;sim.State.ball.end=new Point(0,10);sim.State.ball.duration=1;sim.State.ball.elapsed=-.18f;
            sim.Advance(.1);Assert.IsTrue(string.IsNullOrEmpty(defender.tackleOpponent));Assert.IsFalse(sim.State.events.Any(e=>e.kind=="tackle"||e.kind=="foul"));
            Assert.Less(defender.actionTime,MatchSimulation.TackleRecovery*.4f);
            Assert.That(defender.tackleWithdrawFrom,Is.EqualTo(.65f).Within(.0001f));
            var restored=new MatchSimulation(db,JsonUtility.FromJson<MatchState>(JsonUtility.ToJson(sim.State)));
            sim.Advance(.43);restored.Advance(.43);Assert.AreEqual(JsonUtility.ToJson(sim.State),JsonUtility.ToJson(restored.State));
        }
        [Test] public void ShieldingBodyCannotBePokedThrough()
        {
            defender.position=new Point();defender.angle=0;owner.position=new Point(0,.45f);sim.State.ball.position=new Point(0,.88f);
            uint before=sim.State.seed;int events=sim.State.events.Count;
            Assert.IsFalse((bool)Call("BeginStandingDuel",defender,owner),"The carrier already shields this boot path before commitment");
            Assert.AreEqual(before,sim.State.seed);Assert.AreEqual(events,sim.State.events.Count);Assert.IsTrue(string.IsNullOrEmpty(defender.tackleOpponent));
            // A legacy or already committed duel must also respect the body
            // shielding at contact, independently of prevention at decision time.
            defender.action="tackle";defender.actionKind=MatchSimulation.StandingDuel;defender.tackleOpponent=owner.id;
            defender.actionTarget=sim.State.ball.position;
            defender.actionTime=MatchSimulation.TackleRecovery;uint seed=sim.State.seed;
            Assert.IsFalse((bool)Call("ResolveStandingDuels",owner));Assert.AreEqual(seed,sim.State.seed);Assert.AreEqual(owner.id,sim.State.ball.owner);
        }
        [Test] public void PlayerMustOrientBeforeCommittingToBallBehindHim()
        {
            defender.angle=Mathf.PI*.5f;Assert.IsFalse((bool)Call("BeginStandingDuel",defender,owner));Assert.IsTrue(string.IsNullOrEmpty(defender.tackleOpponent));
        }
        [TestCase(.1)] [TestCase(.2)] public void SaveResumePreservesPendingDuelAndItsOutcome(double time)
        {
            Begin();sim.Advance(time);var restored=new MatchSimulation(db,JsonUtility.FromJson<MatchState>(JsonUtility.ToJson(sim.State)));
            sim.Advance(.73);restored.Advance(.73);Assert.AreEqual(JsonUtility.ToJson(sim.State),JsonUtility.ToJson(restored.State));
        }
        [Test] public void RestartClearsThePendingIntervention()
        {
            Begin();Call("Restart","free-kick",0,new Point(),3f);Assert.IsTrue(string.IsNullOrEmpty(defender.tackleOpponent));Assert.AreEqual("idle",defender.action);
        }
        [Test] public void ExistingSaveWithoutPendingOpponentRemainsValid()
        {
            defender.tackleOpponent="";defender.action="tackle";defender.actionTime=.4f;defender.duelCooldown=1;
            sim.Advance(.1);Assert.AreEqual(owner.id,sim.State.ball.owner);Assert.IsFalse(sim.State.events.Any(e=>e.kind=="tackle"));
        }
        [Test] public void ExposedBallWithNoBootToCarrierContactCannotProduceRandomFouls()
        {
            for(uint seed=1;seed<=128;seed++){
                Setup();sim.State.professionalRules=true;sim.State.seed=seed;
                owner.velocity=new Point(0,2);Begin();
                // The boot meets the ball in front of the runner, outside
                // the swept foot corridor. Winning/missing remains stochastic.
                defender.actionTime=MatchSimulation.TackleRecovery;
                Call("ResolveStandingDuels",owner);
                Assert.IsFalse(sim.State.events.Any(e=>e.kind=="foul"||e.kind=="penalty"),"Seed "+seed);
            }
        }
        [Test] public void MovingCarrierAcrossCommittedBootIsAContactButStationaryReachIsNot()
        {
            defender.actionTarget=new Point(0,.2f);owner.previous=new Point(0,-.4f);owner.position=new Point(0,.4f);owner.velocity=new Point(0,4);sim.State.ball.position=new Point(0,1);
            Assert.IsTrue((bool)Call("StandingTripContact",defender,owner));
            owner.velocity=new Point();Assert.IsFalse((bool)Call("StandingTripContact",defender,owner));
            owner.velocity=new Point(0,4);defender.actionTarget=new Point(.65f,.2f);
            Assert.IsFalse((bool)Call("StandingTripContact",defender,owner));
        }
    }
}

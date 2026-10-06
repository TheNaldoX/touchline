using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Touchline.Core;
using UnityEngine;

namespace Touchline.Tests
{
    public class SlidingContactTests
    {
        static object Invoke(MatchSimulation sim,string method,params object[] args)=>typeof(MatchSimulation).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(sim,args);
        MatchSimulation sim;Actor defender,owner;
        [SetUp] public void Setup()
        {
            var db=new Database{players=Enumerable.Range(0,40).Select(i=>new PlayerData{id="slide"+i,name="S"+i,team=i<20?"a":"b",position=i%20==0?"GB":i%20<5?"DEF":i%20<9?"MIL":"ATT",rating=80,heightCm=180}).ToArray()};
            var career=new Career{club="a"};career.lineup=Career.Select(db,"a",career.tactic);sim=MatchSimulation.Create(db,career,"b");owner=sim.State.actors[9];defender=sim.State.actors[12];
            owner.position=new Point(30,.45f);owner.velocity=new Point();defender.position=new Point(28.7f,0);defender.velocity=new Point(5,0);defender.angle=Mathf.PI*.5f;
            sim.State.ball=new BallState{owner=owner.id,side=0,position=new Point(30.3f,0),height=.11f};sim.State.restart=0;
        }
        [Test] public void SlidingChallengeRequiresSpeedUrgencyAndReachableForwardLane()
        {
            defender.velocity=new Point(1,0);Assert.IsFalse((bool)Invoke(sim,"BeginSlidingDuel",defender,owner));
            defender.velocity=new Point(5,0);defender.angle=-Mathf.PI*.5f;Assert.IsFalse((bool)Invoke(sim,"BeginSlidingDuel",defender,owner));
            defender.angle=Mathf.PI*.5f;Assert.IsTrue((bool)Invoke(sim,"BeginSlidingDuel",defender,owner));Assert.IsTrue(MatchSimulation.GroundedAction(defender));Assert.AreEqual(owner.id,defender.tackleOpponent);
        }
        [Test] public void SlidingPlayerCommitsToOneTrajectoryAndCannotTackleAgainBeforeGettingUp()
        {
            Assert.IsTrue((bool)Invoke(sim,"BeginSlidingDuel",defender,owner));var start=defender.position;float lastSpeed=defender.velocity.Length;
            for(int i=0;i<4;i++){Invoke(sim,"AdvanceGroundAction",defender);Assert.LessOrEqual(defender.velocity.Length,lastSpeed);lastSpeed=defender.velocity.Length;}
            Assert.That(Point.Distance(start,defender.position),Is.InRange(.9f,1.4f));var planted=defender.position;
            for(int i=0;i<9;i++){Invoke(sim,"AdvanceGroundAction",defender);Assert.AreEqual(planted.x,defender.position.x);Assert.AreEqual(planted.z,defender.position.z);Assert.IsFalse((bool)Invoke(sim,"BeginSlidingDuel",defender,owner));}
            Assert.IsTrue(MatchSimulation.GroundedAction(defender));
        }
        [Test] public void CommittedSlideCannotMagneticallyRetrieveAnEscapedBall()
        {
            Assert.IsTrue((bool)Invoke(sim,"BeginSlidingDuel",defender,owner));for(int i=0;i<3;i++)Invoke(sim,"AdvanceGroundAction",defender);
            sim.State.ball.position=new Point(35,0);uint seed=sim.State.seed;
            Assert.IsFalse((bool)Invoke(sim,"ResolveSlidingDuels",owner));Assert.AreEqual(owner.id,sim.State.ball.owner);Assert.IsNull(defender.tackleOpponent);Assert.AreEqual(seed,sim.State.seed);
        }
        [TestCase(160,1)] [TestCase(205,-1)]
        public void SlidingBootContactsBallAndReturnsToGroundedStance(int stature,int side)
        {
            var go=new GameObject("Slide contact");try{
                var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="slide-pose",heightCm=stature},0,9,Color.white);
                var actor=new Actor{action="slide",actionKind=MatchSimulation.SlidingDuel,actionSequence=1,diveSide=side,actionHeight=.11f,actionTarget=new Point(side*.12f,.85f)};
                var point=new Vector3(actor.actionTarget.x,.11f,.85f);float maxStep=0;Vector3 last=default;
                for(int i=0;i<=195;i++){
                    float time=i*.01f;actor.actionTime=Mathf.Max(0,MatchSimulation.SlidingDuelDuration-time);if(time>MatchSimulation.SlidingDuelDuration)actor.action="idle";
                    view.Render(actor,1,.01f,point);var foot=view.BootContactPosition(side>0);if(i>0)maxStep=Mathf.Max(maxStep,Vector3.Distance(last,foot));last=foot;
                    if(i==30)Assert.Less(Vector3.Distance(foot,point),.10f);Assert.Greater(view.FootPosition(true).y,.025f);Assert.Greater(view.FootPosition(false).y,.025f);
                }
                Assert.Less(maxStep,.12f);Assert.AreEqual(Vector3.zero,go.transform.position);
            }finally{Object.DestroyImmediate(go);}
        }
    }
}

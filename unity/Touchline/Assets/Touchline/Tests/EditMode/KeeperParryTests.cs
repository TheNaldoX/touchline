using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Touchline.Core;
using UnityEngine;

namespace Touchline.Tests
{
    public class KeeperParryTests
    {
        Database db;MatchSimulation sim;Actor keeper;
        [SetUp] public void Setup()
        {
            db=new Database{players=Enumerable.Range(0,40).Select(i=>new PlayerData{id="p"+i,name="P"+i,team=i<20?"a":"b",position=i%20==0?"GB":i%20<5?"DEF":i%20<9?"MIL":"ATT",rating=75}).ToArray()};
            var career=new Career{club="a"};career.lineup=Career.Select(db,"a",career.tactic);sim=MatchSimulation.Create(db,career,"b");
            sim.State.restart=0;sim.State.phase="play";sim.State.decision=10;
            foreach(var actor in sim.State.actors)actor.previous=actor.position=new Point(20,actor.slot*2-10);
            keeper=sim.State.actors[0];keeper.previous=keeper.position=new Point(-48,1);keeper.angle=Mathf.PI/2;
        }
        [TestCase(-1,-1)] [TestCase(-1,1)] [TestCase(1,-1)] [TestCase(1,1)]
        public void StretchedSaveContinuesBeyondPostWithoutAddingEnergy(int direction,int side)
        {
            var origin=new Point(-direction*48,side);var impact=new Point(-direction*48.5f,side*2.2f);var incoming=new Point(-direction*24,side);
            foreach(float variation in new[]{0f,.5f,1f}){
                var velocity=MatchSimulation.KeeperParryVelocity(incoming,origin,impact,direction,75,variation);
                Assert.Less(velocity.x*direction,0);Assert.LessOrEqual(velocity.Length,incoming.Length);
                float time=(-direction*52.61f-impact.x)/velocity.x;Assert.Greater(time,0);
                Assert.Greater((impact.z+velocity.z*time)*side,3.77f,"Stretched hand should push the shot beyond the post");
            }
        }
        [TestCase(-1)] [TestCase(1)] public void CentralBlockCanReturnIntoPlayAndCannotAccelerateSlowBall(int direction)
        {
            foreach(float speed in new[]{.1f,12,30}){
                var point=new Point(-direction*48,0);var velocity=MatchSimulation.KeeperParryVelocity(new Point(-direction*speed,0),point,point+new Point(direction*.3f,.1f),direction,75,.4f);
                Assert.Greater(velocity.x*direction,0);Assert.LessOrEqual(velocity.Length,speed+.0001f);
            }
        }
        [TestCase(-1)] [TestCase(1)] public void DeflectedBallCrossingGoalLineAwardsAttackingCorner(int side)
        {
            var impact=new Point(-48.5f,side*2.2f);keeper.previous=keeper.position=new Point(-48,side);keeper.action="dive";keeper.actionTime=1.2f;keeper.controlTime=.45f;
            sim.State.ball=new BallState{kind="loose",position=impact,previous=impact,velocity=MatchSimulation.KeeperParryVelocity(new Point(-24,side),keeper.position,impact,1,75,.5f),height=.5f,previousHeight=.5f,verticalVelocity=1.5f,lastTouch=0,lastTouchId=keeper.id,side=0};
            for(int i=0;i<30&&sim.State.phase!="corner";i++)sim.Advance(.1);
            Assert.AreEqual("corner",sim.State.phase);Assert.AreEqual(1,sim.State.restartSide);Assert.AreEqual(1,sim.State.metrics[1].corners);Assert.AreEqual(0,sim.State.score.Sum());
        }
        [Test] public void ParryDoesNotImmediatelyReturnPossessionAndResumesIdentically()
        {
            keeper.action="dive";keeper.actionTime=.9f; // Hand extension has already developed before impact.
            sim.State.ball=new BallState{kind="shot",from=sim.State.actors[11].id,side=1,lastTouch=1,start=new Point(-30,2.2f),previous=new Point(-45,2.2f),position=new Point(-50,2.2f),velocity=new Point(-25,0),height=.6f,previousHeight=.6f,elapsed=1,goalAttempt=true,shotOnTarget=true,shotSide=1};sim.State.shots[1]=1;
            Assert.IsTrue((bool)typeof(MatchSimulation).GetMethod("ResolveShotContact",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(sim,null));
            Assert.AreEqual("save-parry",keeper.actionKind);Assert.Greater(keeper.controlTime,.4f);Assert.IsNull(sim.State.ball.owner);Assert.AreEqual(0,sim.State.ball.lastTouch);
            var restored=new MatchSimulation(db,JsonUtility.FromJson<MatchState>(JsonUtility.ToJson(sim.State)));sim.Advance(.7);restored.Advance(.7);Assert.AreEqual(JsonUtility.ToJson(sim.State),JsonUtility.ToJson(restored.State));
        }
        [TestCase(false)] [TestCase(true)] public void AShotHeadingWideDoesNotInflateSavesOrShotsOnTarget(bool onTarget)
        {
            keeper.action="dive";keeper.actionTime=.9f;
            sim.State.ball=new BallState{kind="shot",from=sim.State.actors[11].id,side=1,lastTouch=1,start=new Point(-30,2.2f),previous=new Point(-45,2.2f),position=new Point(-50,2.2f),velocity=new Point(-25,0),height=.6f,previousHeight=.6f,elapsed=1,goalAttempt=true,shotOnTarget=onTarget,shotSide=1};sim.State.shots[1]=1;
            Assert.IsTrue((bool)typeof(MatchSimulation).GetMethod("ResolveShotContact",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(sim,null));
            Assert.AreEqual(onTarget?1:0,sim.State.metrics[0].saves);
            Assert.AreEqual(onTarget?1:0,sim.State.metrics[1].shotsOnTarget);
            Assert.That(keeper.actionKind,Does.StartWith("save-"),"The interception must still have its physical animation");
        }
        [Test] public void KeeperNeedsRecoveryTimeBeforeTouchingOwnReboundAgain()
        {
            keeper.action="dive";keeper.actionTime=1;keeper.controlTime=.45f;var point=keeper.position+new Point(.2f,0);
            sim.State.ball=new BallState{kind="loose",previous=point,position=point,height=.2f,previousHeight=.2f,side=0,lastTouch=0,lastTouchId=keeper.id};
            var receive=typeof(MatchSimulation).GetMethod("ResolveReception",BindingFlags.Instance|BindingFlags.NonPublic);
            Assert.IsFalse((bool)receive.Invoke(sim,null));Assert.IsNull(sim.State.ball.owner);
            keeper.controlTime=0;Assert.IsTrue((bool)receive.Invoke(sim,null));Assert.AreEqual(keeper.id,sim.State.ball.owner);
        }
        [Test] public void ADeflectionIntoTheGoalDoesNotCountAsBothASaveAndAGoal()
        {
            sim.State.metrics[0].saves=1;sim.State.metrics[1].shotsOnTarget=1;sim.State.shots[1]=1;
            sim.State.ball=new BallState{kind="loose",from=sim.State.actors[11].id,side=0,lastTouch=0,lastTouchId=keeper.id,previous=new Point(-52,0),position=new Point(-53,0),height=.4f,previousHeight=.4f,goalAttempt=true,shotOnTarget=true,onTargetCounted=true,shotSide=1,saveCredited=true,saveSide=0};
            Assert.IsTrue((bool)typeof(MatchSimulation).GetMethod("BallLeavesPitch",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(sim,null));
            Assert.AreEqual(1,sim.State.score[1]);Assert.AreEqual(0,sim.State.metrics[0].saves);Assert.AreEqual(1,sim.State.metrics[1].shotsOnTarget);Assert.AreEqual(0,sim.State.metrics[0].ownGoals);
        }
        [Test] public void AControlledReboundEndsThePreviousShotAttribution()
        {
            sim.State.ball.goalAttempt=true;sim.State.ball.shotOnTarget=true;sim.State.ball.saveCredited=true;sim.State.metrics[0].saves=1;
            typeof(MatchSimulation).GetMethod("Control",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(sim,new object[]{keeper});
            Assert.IsFalse(sim.State.ball.goalAttempt);Assert.IsFalse(sim.State.ball.saveCredited);Assert.AreEqual(1,sim.State.metrics[0].saves);
        }
    }
}

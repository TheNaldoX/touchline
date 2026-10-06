using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Touchline.Core;
using UnityEngine;

namespace Touchline.Tests
{
    public class KeeperDistributionTests
    {
        Database db;MatchSimulation sim;Actor keeper;
        [SetUp] public void Setup()
        {
            db=new Database{players=Enumerable.Range(0,40).Select(i=>new PlayerData{id="d"+i,name="P"+i,team=i<20?"a":"b",position=i%20==0?"GB":i%20<5?"DEF":i%20<9?"MIL":"ATT",rating=75}).ToArray()};
            var career=new Career{club="a"};career.lineup=Career.Select(db,"a",career.tactic);sim=MatchSimulation.Create(db,career,"b");
            var m=sim.State;m.restart=0;m.phase="play";m.decision=10;
            foreach(var a in m.actors)a.previous=a.position=new Point(20,a.slot*2-10);
            keeper=m.actors[0];keeper.previous=keeper.position=new Point(-48,0);keeper.angle=Mathf.PI/2;keeper.action="keeper-hold";keeper.actionTime=.1f;
            m.ball=new BallState{owner=keeper.id,held=true,side=0,lastTouch=0,position=new Point(-47.64f,0),previous=new Point(-47.64f,0),height=1.1f,previousHeight=1.1f};
        }
        [TestCase(13,"keeper-roll")] [TestCase(33,"keeper-throw")]
        public void SafeSupportReceivesAppropriateHandDistribution(float distance,string expected)
        {
            var mate=sim.State.actors[2];mate.previous=mate.position=keeper.position+new Point(distance,0);sim.Advance(.1);
            Assert.AreEqual(expected,keeper.action);Assert.AreEqual(mate.id,sim.State.ball.to);Assert.AreEqual(1,sim.State.passes[0]);Assert.IsTrue(sim.State.ball.keeperDistribution);Assert.IsFalse(sim.State.ball.held);
            for(int i=0;i<3;i++){Assert.That(MatchSimulation.KeeperDistributionDuration-keeper.actionTime,Is.EqualTo(sim.State.ball.elapsed+sim.State.ball.releaseDelay).Within(.001f));sim.Advance(.1);}
            var restored=new MatchSimulation(db,JsonUtility.FromJson<MatchState>(JsonUtility.ToJson(sim.State)));sim.Advance(8);restored.Advance(8);
            Assert.AreEqual(JsonUtility.ToJson(sim.State),JsonUtility.ToJson(restored.State));
        }
        [Test] public void BlockedShortLaneKeepsBallForFootDistribution()
        {
            sim.State.actors[2].previous=sim.State.actors[2].position=new Point(-35,0);sim.State.actors[15].previous=sim.State.actors[15].position=new Point(-40,0);sim.Advance(.1);
            Assert.AreEqual("place-ball",keeper.action);Assert.IsTrue(sim.State.ball.held);Assert.AreEqual(0,sim.State.passes[0]);
        }
        [TestCase(false)] [TestCase(true)] public void KeeperThrowsCannotScoreDirectlyAgainstOppositionButOwnGoalsCount(bool ownGoal)
        {
            int sign=ownGoal?-1:1;var b=sim.State.ball;b.owner=null;b.held=false;b.directThrow=true;b.keeperDistribution=true;b.kind="keeper-throw";b.from=b.lastTouchId=keeper.id;b.side=b.lastTouch=0;b.previous=new Point(sign*52.3f,0);b.position=new Point(sign*52.8f,0);b.height=b.previousHeight=1;
            Assert.IsTrue((bool)typeof(MatchSimulation).GetMethod("BallLeavesPitch",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(sim,null));
            Assert.AreEqual(ownGoal?"goal":"goal-kick",sim.State.phase);Assert.AreEqual(ownGoal?1:0,sim.State.score.Sum());
        }
        [TestCase("keeper-roll")] [TestCase("keeper-throw")]
        public void AnimatedHandMeetsReleaseAndFeetStayGrounded(string kind)
        {
            var go=new GameObject("Hand distribution release");try{
                var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="hand-distribution",heightCm=182},0,0,Color.yellow);
                var actor=new Actor{action=kind,actionKind=kind,actionSequence=1,actionContactTime=.48f,actionTarget=new Point(-.20f,.52f),actionHeight=kind=="keeper-roll"?.24f:1.78f};
                var b=new BallState{kind=kind,start=actor.actionTarget,startHeight=actor.actionHeight,setupStart=new Point(0,.36f),setupHeight=1.1f,end=new Point(0,20)};
                for(int frame=0;frame<=48;frame++){float t=frame*.01f;actor.actionTime=MatchSimulation.KeeperDistributionDuration-t;var p=MatchSimulation.KeeperDistributionPreparation(b,t/.48f,out float h);var ball=new Vector3(p.x,h,p.z);view.Render(actor,1,.01f,ball);Assert.Greater(view.FootPosition(true).y,-.025f);Assert.Greater(view.FootPosition(false).y,-.025f);if(frame==48)Assert.Less(Vector3.Distance(view.DistributionHandPosition,ball),.14f);}
            }finally{Object.DestroyImmediate(go);}
        }
    }
}

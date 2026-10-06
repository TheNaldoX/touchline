using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Touchline.Core;

namespace Touchline.Tests
{
    public class KeeperClaimContactTests
    {
        static MatchSimulation Scene(out Actor keeper)
        {
            var db=new Database{players=Enumerable.Range(0,40).Select(i=>new PlayerData{id="claim"+i,name="P"+i,team=i<20?"a":"b",position=i%20==0?"GB":i%20<5?"DEF":i%20<9?"MIL":"ATT",rating=80,heightCm=182}).ToArray()};
            var career=new Career{club="a"};career.lineup=Career.Select(db,"a",career.tactic);var sim=MatchSimulation.Create(db,career,"b",91,2700);var m=sim.State;
            m.restart=0;m.phase="play";m.clock=100;m.decision=10;foreach(var actor in m.actors)actor.sentOff=true;
            keeper=m.actors[0];keeper.sentOff=false;keeper.position=keeper.previous=new Point(-49,0);keeper.angle=Mathf.PI*.5f;keeper.action="idle";return sim;
        }
        [TestCase(1.1f,0f)] [TestCase(1.8f,.5f)] [TestCase(2.2f,1f)] [TestCase(2.2f,.2f)]
        public void ClaimMeetsImpactAtItsFractionAndGathersContinuously(float height,float fraction)
        {
            var sim=Scene(out var keeper);var impact=new Point(-48.52f,0);
            typeof(MatchSimulation).GetMethod("BeginRecordedKeeperClaim",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(sim,new object[]{keeper,impact,height,fraction});
            Assert.IsTrue(MatchSimulation.HasRecordedKeeperClaim(keeper));
            var go=new GameObject("Recorded claim timing");
            try{
                go.transform.rotation=Quaternion.Euler(0,90,0);var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="claim0",heightCm=182},0,0,Color.yellow);
                var ball=new Vector3(impact.x,height,impact.z);view.Render(keeper,fraction,.01f,ball);
                Assert.Less(Vector3.Distance(view.HeldBallPosition,ball),.17f,"The accepted impact cannot be pulled down to a lower pose");
                Assert.That(MatchSimulation.KeeperClaimSecondsAfterContact(keeper,keeper.actionTime+(1-fraction)*MatchSimulation.Step),Is.EqualTo(0).Within(.00001f));
                for(int frame=0;frame<=60;frame++){
                    float t=frame*.01f;keeper.actionTime=MatchSimulation.KeeperClaimPresentationDuration-keeper.actionContactTime-t;
                    view.Render(keeper,1,.01f,ball);
                    Assert.IsFalse(float.IsNaN(view.HeldBallPosition.y));Assert.Greater(view.FootPosition(true).y,.04f);
                }
                Assert.Less(Vector3.Distance(view.HeldBallPosition,new Vector3(-48.64f,1.1f,0)),.04f,"The gather ends at the same physical held point");
            }finally{Object.DestroyImmediate(go);}
        }

        [Test] public void RecordedContactSurvivesSerializationAndLegacyClaimStaysLegacy()
        {
            var sim=Scene(out var keeper);
            typeof(MatchSimulation).GetMethod("BeginRecordedKeeperClaim",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(sim,new object[]{keeper,new Point(-48.52f,0),2.2f,.5f});
            var restored=JsonUtility.FromJson<Actor>(JsonUtility.ToJson(keeper));Assert.IsTrue(MatchSimulation.HasRecordedKeeperClaim(restored));
            Assert.AreEqual(keeper.actionTarget,restored.actionTarget);Assert.AreEqual(keeper.actionHeight,restored.actionHeight);
            Assert.AreEqual(MatchSimulation.KeeperClaimSecondsAfterContact(keeper,.6f),MatchSimulation.KeeperClaimSecondsAfterContact(restored,.6f));
            Assert.IsFalse(MatchSimulation.HasRecordedKeeperClaim(new Actor{action="claim",actionTime=.8f}));
        }

        [Test] public void OwnedClaimHeightDescendsOverSeveralTicksInsteadOfSnapping()
        {
            var sim=Scene(out var keeper);var m=sim.State;
            typeof(MatchSimulation).GetMethod("BeginRecordedKeeperClaim",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(sim,new object[]{keeper,new Point(-48.52f,0),2.2f,1f});
            m.ball=new BallState{held=true,owner=keeper.id,position=keeper.actionTarget,height=2.2f};
            var update=typeof(MatchSimulation).GetMethod("UpdateHeldBall",BindingFlags.NonPublic|BindingFlags.Instance);float previous=2.2f;
            for(int tick=1;tick<=6;tick++){
                keeper.actionTime=.8f-tick*.1f;update.Invoke(sim,new object[]{keeper});
                Assert.LessOrEqual(m.ball.height,previous+.0001f);Assert.Less(previous-m.ball.height,.31f);
                if(tick==1)Assert.Greater(m.ball.height,2,"First update must preserve the high catch");previous=m.ball.height;
            }
            Assert.That(m.ball.height,Is.EqualTo(1.1f).Within(.0001f));
        }
    }
}

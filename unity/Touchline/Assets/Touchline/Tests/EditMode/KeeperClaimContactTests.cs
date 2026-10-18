using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Touchline.Core;

namespace Touchline.Tests
{
    public class KeeperClaimContactTests
    {
        [TestCase(30,1.8f)] [TestCase(60,1.8f)] [TestCase(120,1.8f)]
        [TestCase(30,2.2f)] [TestCase(60,2.2f)] [TestCase(120,2.2f)]
        public void ClaimWithoutPreviewApproachesBallAndGathersWithoutHandSnap(int fps,float height)
        {
            var go=new GameObject("Unanticipated claim");try{
                var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="late-claim",heightCm=182},0,0,Color.yellow);
                var actor=new Actor{slot=0,action="idle"};
                for(int frame=0;frame<fps/2;frame++)view.Render(actor,1,1f/fps);
                var wrists=go.GetComponentsInChildren<Transform>().Where(t=>t.name=="wrist.L"||t.name=="wrist.R").ToArray();
                var previous=wrists.Select(t=>t.position).ToArray();
                actor.action="claim";actor.actionKind=MatchSimulation.RecordedKeeperClaim;actor.actionSequence=1;actor.actionContactTime=.12f;
                actor.actionTarget=new Point(0,.48f);actor.actionHeight=height;
                for(int frame=0;frame<=Mathf.CeilToInt(.6f*fps);frame++){
                    actor.actionTime=.8f-frame/(float)fps;string before=JsonUtility.ToJson(actor);
                    view.Render(actor,1,1f/fps);
                    for(int hand=0;hand<wrists.Length;hand++){
                        Assert.Less(Vector3.Distance(wrists[hand].position,previous[hand])*fps,8,"No snap on a claim without anticipation");
                        previous[hand]=wrists[hand].position;
                    }
                    Assert.AreEqual(before,JsonUtility.ToJson(actor));
                }
                Assert.Less(Vector3.Distance(view.HeldBallPosition,new Vector3(0,1.1f,.36f)),.04f,"A claim without a preview must still gather to the actual held point");
            }finally{Object.DestroyImmediate(go);}
        }
        [TestCase(30)] [TestCase(60)] [TestCase(120)]
        public void HighClaimKeepsCapturedJumpAndLandsBeforeRelease(int fps)
        {
            var go=new GameObject("High claim landing");try{
                var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="high-claim",heightCm=182},0,0,Color.yellow);
                var actor=new Actor{slot=0,action="claim",actionKind=MatchSimulation.RecordedKeeperClaim,actionSequence=1,actionContactTime=.12f,actionTarget=new Point(0,.48f),actionHeight=2.2f};
                float airborne=0;
                for(int frame=0;frame<=Mathf.CeilToInt(MatchSimulation.KeeperClaimPresentationDuration*fps);frame++){
                    actor.actionTime=Mathf.Max(0,MatchSimulation.KeeperClaimPresentationDuration-frame/(float)fps);string before=JsonUtility.ToJson(actor);view.Render(actor,1,1f/fps);
                    airborne=Mathf.Max(airborne,Mathf.Min(view.FootPosition(true).y,view.FootPosition(false).y));
                    Assert.Greater(view.FootPosition(true).y,.025f);Assert.Greater(view.FootPosition(false).y,.025f);
                    Assert.AreEqual(before,JsonUtility.ToJson(actor));Assert.AreEqual(Vector3.zero,go.transform.position);
                }
                Assert.Greater(airborne,.18f,"High claim must retain the captured takeoff, not stretch the arms from a chest pose");
                Assert.Less(Mathf.Min(view.FootPosition(true).y,view.FootPosition(false).y),.18f,"Land before the simulated catch is over");
                Assert.Less(Vector3.Distance(view.HeldBallPosition,new Vector3(0,1.1f,.36f)),.04f);
            }finally{Object.DestroyImmediate(go);}
        }
        [TestCase(30)] [TestCase(60)] [TestCase(120)]
        public void RecordedClaimClipUsesMatchClockOnHighlightEntryAndPause(int fps)
        {
            var go=new GameObject("Claim clock");try{
                var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="clock",heightCm=182},0,0,Color.yellow);
                var actor=new Actor{slot=0,action="claim",actionKind=MatchSimulation.RecordedKeeperClaim,actionSequence=1,actionContactTime=.07f,actionHeight=1.8f,actionTarget=new Point(0,.48f)};
                var elapsed=typeof(PlayerView).GetField("mecanimActionElapsed",BindingFlags.NonPublic|BindingFlags.Instance);
                var contact=typeof(PlayerView).GetField("mecanimActionContact",BindingFlags.NonPublic|BindingFlags.Instance);
                actor.actionTime=.85f; // 0.92 - 0.07: exact recorded contact, including interpolation within a tick.
                view.Render(actor,1,1f/fps);
                Assert.That((float)elapsed.GetValue(view),Is.EqualTo(.07f).Within(.00001f));
                float clipImpact=(float)contact.GetValue(view)+(float)elapsed.GetValue(view);
                foreach(float remaining in new[]{.8f,.5f,.2f}){
                    actor.actionTime=remaining;string before=JsonUtility.ToJson(actor);view.Render(actor,1,1f/fps);
                    Assert.That((float)elapsed.GetValue(view),Is.EqualTo(MatchSimulation.KeeperClaimPresentationDuration-remaining).Within(.00001f));
                    float expected=(float)elapsed.GetValue(view);
                    for(int pause=0;pause<3;pause++)view.Render(actor,1,0);
                    Assert.AreEqual(expected,(float)elapsed.GetValue(view));Assert.AreEqual(before,JsonUtility.ToJson(actor));
                }
                // The recorded marker must sample the same clip instant as a legacy catch's 0.2 s contact.
                actor.actionKind="";actor.actionSequence++;actor.actionTime=.8f;view.Render(actor,1,.01f);
                Assert.That(clipImpact,Is.EqualTo((float)contact.GetValue(view)+.2f).Within(.00001f));
            }finally{Object.DestroyImmediate(go);}
        }
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

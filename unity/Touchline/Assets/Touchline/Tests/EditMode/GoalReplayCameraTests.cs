using NUnit.Framework;
using UnityEngine;
using Touchline.Core;
using System.Linq;

namespace Touchline.Tests
{
    public sealed class GoalReplayCameraTests
    {
        [TestCase(1.8f,.43f,false)] [TestCase(.43f,1.8f,false)]
        [TestCase(1.8f,.43f,true)] [TestCase(.43f,1.8f,true)]
        public void FoldingReplayKeepsRecordedBallVisibleAndRestoresLivePose(float recordedAspect,float changedAspect,bool finish)
        {
            var db=JsonUtility.FromJson<Database>(Resources.Load<TextAsset>("Data/database").text);var clubs=db.clubs.Where(c=>c.playable).Take(2).ToArray();var career=new Career{club=clubs[0].id};career.lineup=Career.Select(db,career.club,career.tactic);var sim=MatchSimulation.Create(db,career,clubs[1].id,7919);
            int previousReplays=PlayerPrefs.GetInt("match-goal-replays",1);var go=new GameObject("Replay folding");try{
                var arena=go.AddComponent<MatchArena>();arena.Initialize(db,sim);arena.enabled=false;arena.Paused=true;arena.SetAutomaticGoalReplays(true);arena.Broadcast.SetMode(MatchViewingMode.Full);arena.MatchCamera.aspect=recordedAspect;
                for(int i=0;i<30;i++){sim.State.clock=10+i*.1f;sim.State.ball.owner=null;sim.State.ball.position=sim.State.ball.previous=new Point(42,28-i);arena.RenderFrame(.1f);}
                sim.State.score[0]++;for(int i=0;i<10&&!arena.GoalReplayActive;i++){sim.State.clock=13+i*.1f;arena.RenderFrame(.1f);}
                Assert.IsTrue(arena.GoalReplayActive);
                string live=JsonUtility.ToJson(sim.State);var livePlayer=arena.PlayerVisual(0).transform.position;var liveCamera=arena.MatchCamera.transform.position;var liveRotation=arena.MatchCamera.transform.rotation;
                arena.RenderFrame(.1f);var sampledCamera=arena.MatchCamera.transform.position;var sampledRotation=arena.MatchCamera.transform.rotation;var sampledPlayer=arena.PlayerVisual(0).transform.position;
                // Ball is a private presentation transform, deliberately distinct from frozen Core.
                var ball=(Transform)typeof(MatchArena).GetField("ball",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(arena);var sampledBall=ball.position;
                arena.MatchCamera.aspect=changedAspect;arena.ReframeCamera();
                var projected=arena.MatchCamera.WorldToViewportPoint(sampledBall);Assert.That(projected.x,Is.InRange(.1f,.9f));Assert.That(projected.y,Is.InRange(.1f,.9f));Assert.Greater(projected.z,0);
                Assert.AreEqual(sampledPlayer,arena.PlayerVisual(0).transform.position);Assert.AreEqual(sampledBall,ball.position);Assert.AreEqual(live,JsonUtility.ToJson(sim.State));
                arena.MatchCamera.aspect=recordedAspect;arena.ReframeCamera();Assert.AreEqual(sampledCamera,arena.MatchCamera.transform.position);Assert.Less(Quaternion.Angle(sampledRotation,arena.MatchCamera.transform.rotation),.001f);
                arena.MatchCamera.aspect=changedAspect;arena.ReframeCamera();if(finish){arena.Paused=false;arena.RenderFrame(30);}else arena.SkipGoalReplay();
                Assert.IsFalse(arena.GoalReplayActive);Assert.AreEqual(livePlayer,arena.PlayerVisual(0).transform.position);Assert.AreEqual(liveCamera,arena.MatchCamera.transform.position);Assert.Less(Quaternion.Angle(liveRotation,arena.MatchCamera.transform.rotation),.001f);Assert.AreEqual(live,JsonUtility.ToJson(sim.State));
            }finally{Object.DestroyImmediate(go);PlayerPrefs.SetInt("match-goal-replays",previousReplays);}
        }
        [Test] public void RecordedAspectFollowsHistoryAndMarksOrientationBlendsForRefitting()
        {
            var go=new GameObject("Replay aspect history");try{
                var camera=go.AddComponent<Camera>();var poses=new GoalReplayPoses(new[]{go.transform},camera);
                camera.aspect=1.8f;poses.Capture(0);poses.Capture(.5f);camera.aspect=.43f;poses.Capture(1);poses.Capture(1.5f);
                Assert.IsTrue(poses.Begin());poses.Advance(.25f);Assert.AreEqual(1.8f,poses.RecordedCameraAspect);poses.Advance(.5f);Assert.Zero(poses.RecordedCameraAspect);poses.Advance(.5f);Assert.AreEqual(.43f,poses.RecordedCameraAspect);poses.Skip();Assert.AreEqual(.43f,poses.RecordedCameraAspect);Assert.IsFalse(poses.Active);
            }finally{Object.DestroyImmediate(go);}
        }
    }
}



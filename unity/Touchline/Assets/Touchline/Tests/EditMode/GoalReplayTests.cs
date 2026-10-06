using NUnit.Framework;
using UnityEngine;
using Touchline.Core;
using System.Linq;

namespace Touchline.Tests
{
    public sealed class GoalReplayTests
    {
        [Test] public void ReplayUsesCapturedPosesAndSkipRestoresExactLivePose()
        {
            var go=new GameObject("Replay test");try{
                var buffer=new GoalReplayPoses(new[]{go.transform});
                for(int i=0;i<31;i++){go.transform.localPosition=new Vector3(i,2,3);go.transform.localRotation=Quaternion.Euler(0,i,0);buffer.Capture(i*.1f);}
                var position=go.transform.localPosition;var rotation=go.transform.localRotation;
                Assert.IsTrue(buffer.Begin());buffer.Advance(.15f);Assert.That(go.transform.localPosition.x,Is.EqualTo(1.5f).Within(.0001));
                buffer.Skip();Assert.AreEqual(position,go.transform.localPosition);Assert.Less(Quaternion.Angle(rotation,go.transform.localRotation),.001f);Assert.IsFalse(buffer.Active);
            }finally{Object.DestroyImmediate(go);}
        }
        [Test] public void HistoryIsBoundedAndPlaybackAutomaticallyReturnsToLatestPose()
        {
            var go=new GameObject("Bounded replay");try{
                var buffer=new GoalReplayPoses(new[]{go.transform});for(int i=0;i<2000;i++){go.transform.localPosition=Vector3.right*i;buffer.Capture(i/60f);}
                Assert.LessOrEqual(buffer.Count,241);Assert.Less(buffer.EstimatedPayloadBytes,8000);Assert.IsTrue(buffer.Begin());buffer.Advance(20);Assert.IsFalse(buffer.Active);Assert.That(go.transform.localPosition.x,Is.InRange(1998,1999));
            }finally{Object.DestroyImmediate(go);}
        }
        [Test] public void ForcedFinalSampleRestoresPoseEvenInsideSamplingInterval()
        {
            var go=new GameObject("Exact final replay");try{
                var buffer=new GoalReplayPoses(new[]{go.transform});buffer.Capture(0);buffer.Capture(.3f);buffer.Capture(.6f);go.transform.localPosition=Vector3.right*5;buffer.Capture(.601f,true);Assert.IsTrue(buffer.Begin());buffer.Advance(.1f);buffer.Skip();Assert.AreEqual(Vector3.right*5,go.transform.localPosition);
                buffer.Reset();Assert.IsFalse(buffer.Begin());
            }finally{Object.DestroyImmediate(go);}
        }
        [Test] public void ArenaReplayAndSkipDoNotAdvanceOrRewriteLiveMatch()
        {
            var db=JsonUtility.FromJson<Database>(Resources.Load<TextAsset>("Data/database").text);var clubs=db.clubs.Where(c=>c.playable).Take(2).ToArray();var career=new Career{club=clubs[0].id};career.lineup=Career.Select(db,career.club,career.tactic);var sim=MatchSimulation.Create(db,career,clubs[1].id,7919);
            var go=new GameObject("Replay integration");try{
                var arena=go.AddComponent<MatchArena>();arena.Initialize(db,sim);arena.enabled=false;arena.Paused=true;arena.Broadcast.SetMode(MatchViewingMode.Full);
                for(int i=0;i<30;i++){sim.State.clock=10+i*.1f;arena.RenderFrame(.1f);}
                sim.State.score[0]++;for(int i=0;i<10&&!arena.GoalReplayActive;i++){sim.State.clock=13+i*.1f;arena.RenderFrame(.1f);}
                Assert.IsTrue(arena.GoalReplayActive,"A newly observed goal should replay after its net presentation");string live=JsonUtility.ToJson(sim.State);int saves=0;arena.SaveRequested=()=>saves++;var position=arena.PlayerVisual(0).transform.position;
                for(int i=0;i<10;i++)arena.RenderFrame(.05f);Assert.AreEqual(live,JsonUtility.ToJson(sim.State));Assert.Zero(saves);arena.SkipGoalReplay();Assert.AreEqual(live,JsonUtility.ToJson(sim.State));Assert.AreEqual(position,arena.PlayerVisual(0).transform.position);Assert.IsFalse(arena.GoalReplayActive);
            }finally{Object.DestroyImmediate(go);}
        }
    }
}

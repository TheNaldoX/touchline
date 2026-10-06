using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Touchline.Core;

namespace Touchline.Tests
{
    public class ArenaPauseContinuityTests
    {
        GameObject go;MatchArena arena;MatchSimulation sim;Color ambient;bool fog;
        Transform Ball=>(Transform)typeof(MatchArena).GetField("ball",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(arena);
        [SetUp] public void Setup()
        {
            ambient=RenderSettings.ambientLight;fog=RenderSettings.fog;
            var db=JsonUtility.FromJson<Database>(Resources.Load<TextAsset>("Data/database").text);
            var clubs=db.clubs.Where(c=>c.playable).Take(2).ToArray();var career=new Career{club=clubs[0].id};career.lineup=Career.Select(db,career.club,career.tactic);
            sim=MatchSimulation.Create(db,career,clubs[1].id);go=new GameObject("Pause continuity arena");arena=go.AddComponent<MatchArena>();arena.Initialize(db,sim);arena.Paused=false;
            for(int i=0;i<180;i++)arena.RenderFrame(1f/60);
            arena.RenderFrame(.037f);Assert.That(sim.State.remainder,Is.InRange(.02,.09),"Exercise an interpolated frame between ticks");
        }
        [TearDown] public void Cleanup(){if(go!=null)Object.DestroyImmediate(go);RenderSettings.ambientLight=ambient;RenderSettings.fog=fog;}
        [Test] public void ManualPauseFreezesExactlyTheDisplayedBodyAndBall()
        {
            var transforms=go.GetComponentsInChildren<PlayerView>().SelectMany(v=>v.GetComponentsInChildren<Transform>()).ToArray();
            var positions=transforms.Select(t=>t.position).ToArray();var rotations=transforms.Select(t=>t.rotation).ToArray();var ball=Ball.position;var clock=sim.State.clock;var remainder=sim.State.remainder;int evaluations=arena.PoseEvaluations;
            arena.Paused=true;for(int frame=0;frame<12;frame++)arena.RenderFrame(1f/60);
            Assert.AreEqual(clock,sim.State.clock);Assert.AreEqual(remainder,sim.State.remainder);
            for(int i=0;i<transforms.Length;i++){Assert.Less(Vector3.Distance(positions[i],transforms[i].position),.00001f,transforms[i].name+" moved on pause");Assert.Less(Quaternion.Angle(rotations[i],transforms[i].rotation),.05f);}
            Assert.Less(Vector3.Distance(ball,Ball.position),.00001f,"The ball must not jump to the next fixed-step position");
            Assert.AreEqual(evaluations,arena.PoseEvaluations,"An unchanged frozen pose does not need another evaluation");
            arena.Paused=false;arena.RenderFrame(1f/60);Assert.Greater(arena.PoseEvaluations,evaluations);Assert.Greater(sim.State.remainder,remainder);
        }
        [Test] public void HalfTimeStillDisplaysTheFinalAuthoritativePlayerPositions()
        {
            sim.State.clock=359.9f;arena.RenderFrame(.1f);Assert.IsTrue(sim.State.halfTime);Assert.IsTrue(arena.Paused);
            foreach(var view in go.GetComponentsInChildren<PlayerView>()){
                var actor=sim.State.actors.First(a=>a.id==view.PlayerId);
                Assert.Less(Vector3.Distance(view.transform.position,new Vector3(actor.position.x,0,actor.position.z)),.00001f);
            }
        }
        [Test] public void ExplicitStateAdvanceWhilePausedRefreshesTheDisplayedPose()
        {
            arena.Paused=true;arena.RenderFrame(1f/60);int evaluations=arena.PoseEvaluations;sim.Advance(.2);arena.RenderFrame(1f/60);
            Assert.Greater(arena.PoseEvaluations,evaluations);
            foreach(var view in go.GetComponentsInChildren<PlayerView>()){
                var actor=sim.State.actors.First(a=>a.id==view.PlayerId);
                Assert.Less(Vector3.Distance(view.transform.position,new Vector3(actor.position.x,0,actor.position.z)),.00001f);
            }
        }
        [Test] public void CalmPresentationStopsCameraAndPoseWorkThenRestoresCurrentPlayers()
        {
            var m=sim.State;m.clock=20;m.phase="throw-in";m.restart=20;m.ball=new BallState{position=new Point(0,34),previous=new Point(0,34)};arena.Broadcast.Enabled=true;
            arena.RenderFrame(.1f);Assert.IsTrue(arena.QuietPresentation);Assert.IsFalse(arena.MatchCamera.enabled);int evaluations=arena.PoseEvaluations;float before=m.clock;
            for(int i=0;i<4;i++)arena.RenderFrame(.1f);Assert.Greater(m.clock,before);Assert.AreEqual(evaluations,arena.PoseEvaluations,"Hidden 3D should not keep animating 22 skeletons");
            arena.Paused=true;before=m.clock;arena.RenderFrame(.1f);Assert.AreEqual(before,m.clock);Assert.AreEqual(evaluations,arena.PoseEvaluations);
            var db=JsonUtility.FromJson<Database>(Resources.Load<TextAsset>("Data/database").text);var incoming=db.Squad(m.home).First(p=>!m.used.Contains(p.id)&&p.unavailableDays==0);sim.Substitute(0,9,incoming.id);
            arena.RenderFrame(.1f);Assert.IsFalse(arena.QuietPresentation);Assert.IsTrue(arena.MatchCamera.enabled);Assert.Greater(arena.PoseEvaluations,evaluations);
            Assert.IsTrue(go.GetComponentsInChildren<PlayerView>().Any(p=>p.PlayerId==incoming.id));
            foreach(var view in go.GetComponentsInChildren<PlayerView>()){var actor=m.actors.First(p=>p.id==view.PlayerId);Assert.Less(Vector3.Distance(view.transform.position,new Vector3(actor.position.x,0,actor.position.z)),.00001f);}
        }
    }
}

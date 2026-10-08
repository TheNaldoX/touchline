using NUnit.Framework;
using UnityEngine;

namespace Touchline.Tests
{
    // Tribunes vivantes (CrowdReaction, StadiumFlags) et plan du ralenti de but (GoalReplayCamera).
    public sealed class CrowdLifeTests
    {
        [Test] public void GoalLiftsOnlyTheScoringSideAndEveryoneSitsBackDown()
        {
            var mesh=StadiumAtmosphere.Crowd("176","160",1,out var rig);
            try{
                var rest=mesh.vertices;var crowd=new CrowdReaction(mesh,rig);
                crowd.Trigger(1,CrowdReaction.Kind.Goal,new Vector3(52.5f,0,0));
                for(int i=0;i<30;i++)crowd.Advance(.05f); // 1,5 s : la vague a traversé le virage visiteur
                var lifted=mesh.vertices;int raised=0;
                for(int p=0;p<rig.People;p++)for(int v=rig.personStart[p];v<rig.End(p);v++){
                    float dy=lifted[v].y-rest[v].y;
                    if(!rig.visiting[p]){Assert.AreEqual(rest[v],lifted[v],"Les supporters du club recevant restent assis sur un but visiteur");continue;}
                    Assert.That(dy,Is.InRange(0f,CrowdReaction.StandLift+CrowdReaction.JumpHeight+StadiumAtmosphere.HandRaise+.001f));if(dy>.05f)raised++;
                }
                Assert.Greater(raised,0,"Le bloc visiteur se lève");
                for(float t=0;t<CrowdReaction.Duration(CrowdReaction.Kind.Goal)+1;t+=.1f)crowd.Advance(.1f);
                CollectionAssert.AreEqual(rest,mesh.vertices,"Tout le monde se rassoit exactement");
                Assert.AreEqual(CrowdReaction.Kind.None,crowd.Current(1));
            }finally{Object.DestroyImmediate(mesh);}
        }
        [Test] public void ReactionIsDeterministicAndRollsAsAWaveFromTheGoal()
        {
            Assert.Zero(CrowdReaction.Envelope(CrowdReaction.Kind.Goal,-.1f));Assert.AreEqual(1,CrowdReaction.Envelope(CrowdReaction.Kind.Goal,1f));
            Assert.Zero(CrowdReaction.Envelope(CrowdReaction.Kind.Chance,CrowdReaction.RiseTime+CrowdReaction.ChanceHold+CrowdReaction.SitTime+.01f));
            float goal=0,chance=0;
            for(int p=0;p<200;p++){
                CrowdReaction.Pose(CrowdReaction.Kind.Goal,1,p,out float lift,out float arms);CrowdReaction.Pose(CrowdReaction.Kind.Goal,1,p,out float again,out _);
                Assert.AreEqual(lift,again);goal=Mathf.Max(goal,lift);
                CrowdReaction.Pose(CrowdReaction.Kind.Chance,1,p,out lift,out arms);chance=Mathf.Max(chance,lift);Assert.LessOrEqual(arms,CrowdReaction.ChanceArms+1e-4f);
            }
            Assert.Greater(goal,chance,"Un but soulève plus qu'une occasion");Assert.Greater(chance,0);
            // Vague : juste après le but, les sièges proches se lèvent avant ceux du virage opposé.
            var mesh=StadiumAtmosphere.Crowd("176","160",1,out var rig);
            try{
                var rest=mesh.vertices;var crowd=new CrowdReaction(mesh,rig);crowd.Trigger(0,CrowdReaction.Kind.Goal,new Vector3(52.5f,0,0));
                for(int i=0;i<12;i++)crowd.Advance(.05f);var v=mesh.vertices;int near=0,far=0;
                for(int p=0;p<rig.People;p++){if(rig.visiting[p])continue;int first=rig.personStart[p];if(v[first].y-rest[first].y<.05f)continue;if(rig.seat[p].x>40)near++;if(rig.seat[p].x<-55)far++;}
                Assert.Greater(near,0);Assert.Zero(far,"La vague n'a pas encore atteint l'autre virage");
            }finally{Object.DestroyImmediate(mesh);}
        }
        [Test] public void HomeEndWearsScarvesAndOnlyLimbsAndScarvesRise()
        {
            var mesh=StadiumAtmosphere.Crowd("176","160",1,out var rig);
            try{
                var v=mesh.vertices;int scarves=0;Assert.AreEqual(v.Length,rig.raise.Length);
                for(int i=0;i<v.Length;i++){
                    if(rig.raise[i]==StadiumAtmosphere.ScarfRaise){scarves++;Assert.Less(v[i].x,-59,"Écharpes dans le virage populaire");}
                    Assert.That(rig.raise[i],Is.EqualTo(0).Or.EqualTo(StadiumAtmosphere.ScarfRaise).Or.EqualTo(StadiumAtmosphere.ElbowRaise).Or.EqualTo(StadiumAtmosphere.HandRaise));
                }
                Assert.Greater(scarves,40*4);
            }finally{Object.DestroyImmediate(mesh);}
        }
        [Test] public void FlagsWaveOffThePitchAndHarderAfterAGoal()
        {
            var calm=new StadiumFlags();var cheer=new StadiumFlags();
            try{
                var start=(Vector3[])calm.Vertices.Clone();float calmMove=0,cheerMove=0;
                for(int i=0;i<60;i++){calm.Advance(1/30f,0);cheer.Advance(1/30f,1);
                    for(int k=0;k<start.Length;k++){calmMove=Mathf.Max(calmMove,(calm.Vertices[k]-start[k]).magnitude);cheerMove=Mathf.Max(cheerMove,(cheer.Vertices[k]-start[k]).magnitude);
                        var p=cheer.Vertices[k];Assert.IsTrue(Mathf.Abs(p.x)>59||Mathf.Abs(p.z)>33.9f,"Drapeau sur la pelouse : "+p);}}
                Assert.Greater(calmMove,.01f,"Les drapeaux flottent même au calme");Assert.Greater(cheerMove,calmMove*1.5f);
                Assert.LessOrEqual(cheerMove,StadiumFlags.CheerWave+.001f);Assert.AreEqual(StadiumFlags.SubmeshCount,calm.Mesh.subMeshCount);
            }finally{Object.DestroyImmediate(calm.Mesh);Object.DestroyImmediate(cheer.Mesh);}
        }
        [TestCase(1.8f,1)] [TestCase(.43f,1)] [TestCase(1.8f,-1)]
        public void ReplayCameraStaysLowBesideTheGoalAndKeepsTheBallInFrame(float aspect,int side)
        {
            var go=new GameObject("Replay camera");try{
                var camera=go.AddComponent<Camera>();camera.aspect=aspect;camera.nearClipPlane=.15f;camera.farClipPlane=270;
                var shot=new GoalReplayCamera();var ball=new Vector3(side*10,.11f,-12);shot.Begin(side*53,ball);Vector3 previous=default;
                for(int i=0;i<=240;i++){
                    float t=i/240f;ball=Vector3.Lerp(new Vector3(side*10,.11f,-12),new Vector3(side*53.4f,.8f,1.5f),t*t);
                    shot.Advance(ball,1/30f);shot.Apply(camera);var p=camera.transform.position;
                    Assert.Greater(p.x*side,52.5f+1.8f-.01f,"Derrière la ligne, hors du filet");Assert.Greater(Mathf.Abs(p.z),3.66f+1);Assert.Less(p.y,2);
                    Assert.That(camera.fieldOfView,Is.InRange(GoalReplayCamera.MinFov,GoalReplayCamera.PortraitMaxFov));
                    if(i>0)Assert.Less((p-previous).magnitude,.05f,"Mouvement continu, sans saut");previous=p;
                    if(i>10){var view=camera.WorldToViewportPoint(ball);Assert.Greater(view.z,0);Assert.That(view.x,Is.InRange(.05f,.95f));Assert.That(view.y,Is.InRange(.05f,.95f));}
                }
            }finally{Object.DestroyImmediate(go);}
        }
    }
}

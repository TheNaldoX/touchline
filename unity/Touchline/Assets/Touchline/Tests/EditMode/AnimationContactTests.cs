using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Touchline.Core;

namespace Touchline.Tests
{
    public class AnimationContactTests
    {
        [TestCase(160)] [TestCase(205)] public void BootGeometryDoesNotPenetrateBallAtStrike(int height)
        {
            var go=new GameObject("Contact geometry");var ballObject=new GameObject("Ball collision probe");try{
                var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="geometry",heightCm=height,preferredFoot="Right"},0,9,Color.white);
                var actor=new Actor{action="kick",actionTime=.46f,actionSequence=1,actionTarget=new Point(0,.42f),actionHeight=.11f};var ball=new Vector3(0,.11f,.42f);view.Render(actor,1,.01f,ball);
                var sphere=ballObject.AddComponent<SphereCollider>();sphere.radius=.11f;ballObject.transform.position=ball;
                var boot=go.GetComponentsInChildren<MeshFilter>().First(f=>f.transform.parent.name=="foot.R");var collider=boot.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=boot.sharedMesh;collider.convex=true;
                bool intersects=Physics.ComputePenetration(collider,boot.transform.position,boot.transform.rotation,sphere,ball,Quaternion.identity,out var direction,out float depth);
                Assert.IsTrue(!intersects||depth<.015f,"The shoe cuts into the ball by "+depth+" metres");
                Assert.Less(Vector3.Distance(collider.ClosestPoint(ball),ball),.14f,"The boot must still make contact");
            }finally{Object.DestroyImmediate(ballObject);Object.DestroyImmediate(go);}
        }
        [TestCase(2.5f,0)] [TestCase(-2.5f,0)] [TestCase(0,-2.5f)]
        public void SideAndBackwardStepsFollowTravelWhileFacingBall(float x,float z)
        {
            var go=new GameObject("Keeper footwork");try{
                var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="shuffle"},0,0,Color.yellow);
                var actor=new Actor{action="run",velocity=new Point(x,z),angle=0};
                float acrossMin=100,acrossMax=-100,forwardMin=100,forwardMax=-100;
                for(int frame=0;frame<180;frame++){
                    actor.previous=actor.position;actor.position+=actor.velocity/60;view.Render(actor,1,1f/60,new Vector3(0,1,20));
                    var foot=go.transform.InverseTransformPoint(view.FootPosition(true));
                    if(frame>30){acrossMin=Mathf.Min(acrossMin,foot.x);acrossMax=Mathf.Max(acrossMax,foot.x);forwardMin=Mathf.Min(forwardMin,foot.z);forwardMax=Mathf.Max(forwardMax,foot.z);}
                    Assert.Greater(view.FootPosition(true).y,.025f);Assert.Greater(view.FootPosition(false).y,.025f);
                    Assert.Greater(Vector3.Dot(go.transform.forward,Vector3.forward),.99f,"Footwork must not turn the goalkeeper away from the ball");
                }
                if(x!=0){Assert.Greater(acrossMax-acrossMin,.35f);Assert.Less(forwardMax-forwardMin,.20f,"Side steps must not play a forward running cycle");}
                else Assert.Greater(forwardMax-forwardMin,.35f);
            }finally{Object.DestroyImmediate(go);}
        }
        [Test] public void BootsHaveClosedOutwardFacesAndAnkleOverlap()
        {
            var mesh=FootballBootMesh.Shared;Assert.AreSame(mesh,FootballBootMesh.Shared);Assert.That(mesh.bounds.size.z,Is.InRange(.25f,.32f));Assert.Greater(mesh.bounds.max.y,.09f);Assert.Greater(mesh.normals[0].x,0);Assert.Less(mesh.normals[32].z,0);Assert.Greater(mesh.normals[33].z,0);
        }
        [TestCase(.12f,1.8f)] [TestCase(.12f,2.05f)] [TestCase(.18f,1.8f)] [TestCase(.18f,2.05f)]
        public void HeaderMeetsBallAtTheEngineContactTime(float contact,float height)
        {
            var go=new GameObject("Header contact");try{
                var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="header",heightCm=180},0,9,Color.white);
                var actor=new Actor{action="run",velocity=new Point(0,2)};view.Render(actor,1,.01f);
                actor.action="header";actor.velocity=new Point();actor.actionSequence=1;actor.actionContactTime=contact;actor.actionTarget=new Point(.10f,.30f);actor.actionHeight=height;
                var ball=new Vector3(.10f,height,.30f);
                for(int i=0;i<=Mathf.RoundToInt(contact*100);i++){actor.actionTime=.64f-i*.01f;view.Render(actor,1,.01f,ball);}
                Assert.That(Vector3.Distance(view.HeaderContactPosition,ball),Is.LessThan(.06f));
                for(int i=Mathf.RoundToInt(contact*100)+1;i<=64;i++){actor.actionTime=.64f-i*.01f;view.Render(actor,1,.01f,ball);Assert.Greater(view.FootPosition(true).y,.025f);Assert.Greater(view.FootPosition(false).y,.025f);}
                Assert.That(go.transform.Find("Rig").localPosition.y,Is.InRange(-.08f,.02f),"Player must land after the header");
            }finally{Object.DestroyImmediate(go);}
        }
        [TestCase(-1)] [TestCase(1)] public void KeeperReplantsFeetBeforeStandingUp(int side)
        {
            var go=new GameObject("Dive recovery");try{
                var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="recovering"},0,0,Color.yellow);
                var actor=new Actor{action="dive",diveSide=side,actionSequence=1,actionTarget=new Point(side*.8f,.3f),actionHeight=.5f};
                float crouch=0;for(int i=0;i<=120;i++){
                    actor.actionTime=1.2f-i*.01f;view.Render(actor,1,.01f);
                    if(i==90)crouch=go.transform.Find("Rig").localPosition.y;
                    if(i>=90){Assert.Greater(view.FootPosition(true).y,.025f);Assert.Greater(view.FootPosition(false).y,.025f);}
                }
                Assert.That(crouch,Is.LessThan(-.12f));Assert.That(go.transform.Find("Rig").localPosition.y,Is.EqualTo(0).Within(.01f));
            }finally{Object.DestroyImmediate(go);}
        }
        [Test] public void HeaderContactMetadataComesFromTheReleasedBall()
        {
            var db=JsonUtility.FromJson<Database>(Resources.Load<TextAsset>("Data/database").text);
            var clubs=db.clubs.Where(c=>db.Squad(c.id).Count>=11).Take(2).ToArray();
            var career=new Career{club=clubs[0].id};career.lineup=Career.Select(db,career.club,career.tactic);var sim=MatchSimulation.Create(db,career,clubs[1].id,1);
            var actor=sim.State.actors[9];actor.position=new Point(42,0);sim.State.ball.position=new Point(42.15f,.2f);sim.State.ball.height=2;
            typeof(MatchSimulation).GetMethod("Shoot",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(sim,new object[]{actor,true,false,false});
            Assert.AreEqual("header",actor.action);Assert.AreEqual(sim.State.ball.start.x,actor.actionTarget.x);Assert.AreEqual(sim.State.ball.start.z,actor.actionTarget.z);
            Assert.AreEqual(sim.State.ball.startHeight,actor.actionHeight);Assert.AreEqual(sim.State.ball.releaseDelay,actor.actionContactTime);
            var copy=JsonUtility.FromJson<Actor>(JsonUtility.ToJson(actor));Assert.AreEqual(actor.actionContactTime,copy.actionContactTime);
        }
    }
}

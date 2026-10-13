using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Touchline.Core;
namespace Touchline.Tests
{
    public class CapturedBodyReceptionContactTests
    {
        [TestCase(MatchSimulation.ChestControl,30)] [TestCase(MatchSimulation.ChestControl,60)] [TestCase(MatchSimulation.ChestControl,120)]
        [TestCase(MatchSimulation.ThighControl,30)] [TestCase(MatchSimulation.ThighControl,60)] [TestCase(MatchSimulation.ThighControl,120)]
        public void RepeatedZeroProgressFramesPreserveOutgoingPoseThenResume(string kind,int hz)
        {
            bool old=PlayerView.UseMecanim;PlayerView.UseMecanim=true;var go=new GameObject("Physical reception clock");
            try{
                var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="entry-clock",heightCm=180},0,9,Color.white);
                var actor=new Actor{slot=9,action="run",velocity=new Point(0,3.5f)};float dt=1f/hz;
                for(int i=0;i<hz;i++){actor.previous=actor.position;actor.position+=actor.velocity*dt;view.Render(actor,1,dt);}
                var left=view.FootPosition(true);var right=view.FootPosition(false);
                actor.action="control";actor.actionKind=kind;actor.actionSequence=1;actor.actionTime=MatchSimulation.BodyControlDuration(kind);
                actor.actionTarget=actor.position+new Point(.12f,.3f);actor.actionHeight=kind==MatchSimulation.ChestControl?1.4f:.94f;
                var point=new Vector3(actor.actionTarget.x,actor.actionHeight,actor.actionTarget.z);
                for(int i=0;i<4;i++){view.Render(actor,1,dt,point);Assert.Less(Vector3.Distance(left,view.FootPosition(true)),.001f);Assert.Less(Vector3.Distance(right,view.FootPosition(false)),.001f);}
                actor.actionTime-=.04f;view.Render(actor,1,dt,point);
                Assert.Greater(Vector3.Distance(left,view.FootPosition(true))+Vector3.Distance(right,view.FootPosition(false)),.01f,"Resume when the physical clock advances");
            }finally{Object.DestroyImmediate(go);PlayerView.UseMecanim=old;}
        }
        [TestCase(MatchSimulation.ChestControl,160,30)] [TestCase(MatchSimulation.ChestControl,180,60)] [TestCase(MatchSimulation.ChestControl,205,120)]
        [TestCase(MatchSimulation.ThighControl,160,30)] [TestCase(MatchSimulation.ThighControl,180,60)] [TestCase(MatchSimulation.ThighControl,205,120)]
        public void CapturedReceptionMeetsPhysicalImpactAndKeepsVisibleSupport(string kind,int stature,int hz)
        {
            bool old=PlayerView.UseMecanim;PlayerView.UseMecanim=true;
            var go=new GameObject("Captured body contact");
            try{
                var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="captured-body",heightCm=stature},0,9,Color.white);
                var actor=new Actor{slot=9,action="idle"};float dt=1f/hz;
                for(int i=0;i<hz;i++)view.Render(actor,1,dt);
                actor.action="control";actor.actionKind=kind;actor.actionSequence=1;
                actor.actionHeight=stature*.01f*(kind==MatchSimulation.ChestControl?.78f:.52f);actor.actionTarget=new Point(.12f,.3f);
                float duration=MatchSimulation.BodyControlDuration(kind),contact=MatchSimulation.BodyControlContactTime;
                var point=new Vector3(.12f,actor.actionHeight,.3f);
                for(float t=0;t<contact;t+=dt){actor.actionTime=duration-t;view.Render(actor,1,dt,point);}
                actor.actionTime=duration-contact;view.Render(actor,1,dt,point);
                var touch=kind==MatchSimulation.ChestControl?view.ChestContactPosition:view.ThighContactPosition(true);
                Assert.Less(Vector3.Distance(touch,point),.10f,"Captured pose must reach the saved physical contact");
                var support=go.GetComponentsInChildren<MeshFilter>().Single(m=>m.name=="Football boot"&&m.transform.parent.name=="foot.R");
                float sole=support.sharedMesh.vertices.Min(v=>support.transform.TransformPoint(v).y);
                Assert.That(sole,Is.InRange(-.005f,.06f),"Measure the visible sole, not the ankle pivot of a rotated boot");
                Assert.AreEqual(Vector3.zero,go.transform.position,"Do not warp the simulation actor");
            }finally{Object.DestroyImmediate(go);PlayerView.UseMecanim=old;}
        }
    }
}
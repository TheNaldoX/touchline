using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Touchline.Core;

namespace Touchline.Tests
{
    public class SlidingExtensionTests
    {
        [TestCase(145,1,30,.14f)] [TestCase(175,1,60,.14f)] [TestCase(215,1,120,.14f)]
        [TestCase(145,-1,120,.14f)] [TestCase(175,-1,30,.14f)] [TestCase(215,-1,60,.14f)]
        [TestCase(160,1,60,.85f)] [TestCase(205,-1,60,.85f)]
        public void SlidingLeadLegExtendsWithoutMovingContactOrEnteringPitch(int height,int side,int fps,float distance)
        {
            var go=new GameObject("Sliding extension");var mesh=new Mesh();
            try{
                var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="slide-extension",heightCm=height},0,2,Color.white);
                var actor=new Actor{slot=2,action="idle"};for(int i=0;i<20;i++)view.Render(actor,1,1f/fps);
                string suffix=side>0?"L":"R";var bones=go.GetComponentsInChildren<Transform>();
                var hip=bones.Single(x=>x.name=="upperleg01."+suffix);var knee=bones.Single(x=>x.name=="lowerleg01."+suffix);var ankle=bones.Single(x=>x.name=="foot."+suffix);
                float length=Vector3.Distance(hip.position,knee.position)+Vector3.Distance(knee.position,ankle.position);
                var originalHip=hip.position;var ball=new Vector3(side*.12f,.11f,distance);
                var skins=go.GetComponentsInChildren<SkinnedMeshRenderer>();var boots=go.GetComponentsInChildren<MeshFilter>();
                actor.action="slide";actor.actionKind=MatchSimulation.SlidingDuel;actor.actionSequence=1;actor.actionContactTime=MatchSimulation.SlidingDuelContact;actor.diveSide=side;actor.actionTarget=new Point(ball.x,ball.z);actor.actionHeight=.11f;
                float extension=0,contact=100,lowest=100,maxHipStep=0;var previous=hip.position;
                var times=Enumerable.Range(0,fps*2+1).Select(i=>i/(float)fps).Append(MatchSimulation.SlidingDuelContact).Distinct().OrderBy(t=>t).ToArray();
                for(int frame=0;frame<times.Length;frame++){
                    float time=times[frame],dt=frame>0?time-times[frame-1]:1f/fps;actor.actionTime=Mathf.Max(0,MatchSimulation.SlidingDuelDuration-time);if(time>MatchSimulation.SlidingDuelDuration)actor.action="idle";
                    view.Render(actor,1,dt,ball);maxHipStep=Mathf.Max(maxHipStep,Vector3.Distance(previous,hip.position));previous=hip.position;
                    if(Mathf.Abs(time-MatchSimulation.SlidingDuelContact)<.000001f){extension=Vector3.Distance(hip.position,ankle.position)/length;contact=Vector3.Distance(view.BootContactPosition(side>0),ball);}
                    Assert.AreEqual(Vector3.zero,go.transform.position,"Presentation must not move the physical actor");
                    Assert.That(Vector3.Distance(hip.position,knee.position)+Vector3.Distance(knee.position,ankle.position),Is.EqualTo(length).Within(.0001f),"No limb stretching");
                    if(frame%Mathf.Max(1,fps/20)!=0)continue;
                    foreach(var skin in skins){skin.BakeMesh(mesh);foreach(var vertex in mesh.vertices)lowest=Mathf.Min(lowest,skin.transform.TransformPoint(vertex).y);}
                    foreach(var boot in boots)foreach(var vertex in boot.sharedMesh.vertices)lowest=Mathf.Min(lowest,boot.transform.TransformPoint(vertex).y);
                }
                TestContext.WriteLine($"extension={extension:F4}; contact={contact:F4}; surface={lowest:F4}; hipStep={maxHipStep:F4}");
                Assert.Less(contact,distance<.2f?.03f:.10f,"Keep the existing physical toe-ball tolerance");
                Assert.Greater(extension,.76f,"The reaching leg must extend instead of sitting above a folded knee");
                Assert.GreaterOrEqual(lowest,-.025f,"Visible geometry must remain above the pitch");
                Assert.Less(maxHipStep,.24f,"No abrupt relocation of the hips during descent or recovery");
                Assert.Less(Vector3.Distance(originalHip,hip.position),.025f,"Return to the original standing pelvis");
            }finally{Object.DestroyImmediate(mesh);Object.DestroyImmediate(go);}
        }
    }
}


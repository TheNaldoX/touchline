using NUnit.Framework;
using UnityEngine;
using Touchline.Core;

namespace Touchline.Tests
{
    public class DirectionalCaptureTests
    {
        [Test] public void SlowInjuredWalkUsesSameCaptureForDirectionAndVelocityInputs()
        {
            var direction=new Vector3[19];var velocity=new Vector3[19];
            DirectionalBodyMotion.Sample(.42f,.45f,Vector3.forward,0,true,direction);
            DirectionalBodyMotion.Sample(.42f,.45f,Vector3.forward*.45f,0,true,velocity);
            for(int i=0;i<19;i++)Assert.That(Vector3.Distance(direction[i],velocity[i]),Is.LessThan(.00001f));
            Assert.That(DirectionalBodyMotion.Stride(.45f,Vector3.forward,0,true),Is.EqualTo(DirectionalBodyMotion.Stride(.45f,Vector3.forward*.45f,0,true)).Within(.00001f));
        }
        [TestCase(1f)] [TestCase(3f)] [TestCase(6f)] public void MovingBootSolesRemainAbovePitchWhileAnklesArticulate(float speed)
        {
            var go=new GameObject("Boot sole clearance");try{var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="sole",heightCm=190},0,9,Color.white);var actor=new Actor{action="run",velocity=new Point(0,speed)};float rotation=0;
                for(int frame=0;frame<180;frame++){
                    actor.previous=actor.position;actor.position+=actor.velocity/60;view.Render(actor,1,1f/60);
                    foreach(var boot in go.GetComponentsInChildren<MeshFilter>())if(boot.name=="Football boot"){
                        foreach(var vertex in boot.sharedMesh.vertices)Assert.Greater(boot.transform.TransformPoint(vertex).y,-.01f,"Boot penetrates turf at "+speed+" m/s, frame "+frame+" ankle="+boot.transform.position+" rotation="+boot.transform.eulerAngles);
                        rotation=Mathf.Max(rotation,Quaternion.Angle(boot.transform.rotation,go.transform.rotation));
                    }
                }
                Assert.Greater(rotation,5,"Ankles should articulate instead of rigid skiing");
            }finally{Object.DestroyImmediate(go);}
        }
        [Test] public void CaptureCyclesAreAttributedAndCloseWithoutPoseJump()
        {
            foreach(string direction in new[]{"forward","back","left","right"})foreach(string pace in new[]{"walk","run"})for(int variant=0;variant<2;variant++){
                var text=Resources.Load<TextAsset>("Animations/style-"+direction+"-"+pace+"-"+variant);Assert.NotNull(text);
                var clip=JsonUtility.FromJson<BodyClip>(text.text);Assert.That(clip.source,Does.Contain("Ian Mason"));Assert.That(clip.sha256.Length,Is.EqualTo(64));Assert.That(clip.stride,Is.InRange(.5f,2.5f));
                var first=clip.samples[0];var last=clip.samples[clip.samples.Length-1];for(int i=0;i<first.joints.Length;i++)Assert.That(first.joints[i],Is.EqualTo(last.joints[i]).Within(.00001f));
                Assert.That(clip.endFrame,Is.GreaterThan(clip.startFrame));
            }
        }
        [TestCase(.8f)] [TestCase(2.5f)] [TestCase(6f)] public void DirectionBlendSpaceHasNoHardBoundary(float speed)
        {
            var previous=new Vector3[19];var current=new Vector3[19];Assert.IsTrue(DirectionalBodyMotion.Available);
            DirectionalBodyMotion.Sample(.35f,speed,Vector3.forward,0,false,previous);
            for(int angle=1;angle<=360;angle++){
                var direction=Quaternion.Euler(0,angle,0)*Vector3.forward;var ground=DirectionalBodyMotion.Sample(.35f,speed,direction,0,false,current);
                for(int i=0;i<19;i++){Assert.IsFalse(float.IsNaN(current[i].sqrMagnitude));Assert.That(Vector3.Distance(current[i],previous[i]),Is.LessThan(.035f),"direction "+angle+" joint "+i);previous[i]=current[i];}
                Assert.GreaterOrEqual(ground.left,0);Assert.GreaterOrEqual(ground.right,0);
                Assert.That(DirectionalBodyMotion.Stride(speed,direction,0,false),Is.InRange(.5f,4f));
            }
        }
        [TestCase(30)] [TestCase(60)] [TestCase(120)] public void DirectionChangesStayGroundedAndKeepFacingIndependent(int fps)
        {
            var go=new GameObject("Direction change capture");try{var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="directional",heightCm=180},0,0,Color.yellow);var actor=new Actor{action="run",slot=0};Vector3 last=default;
                for(int frame=0;frame<fps*6;frame++){
                    float angle=frame/(float)fps*60;var travel=Quaternion.Euler(0,angle,0)*Vector3.forward*2.5f;actor.velocity=new Point(travel.x,travel.z);actor.previous=actor.position;actor.position+=actor.velocity/fps;actor.stride+=2.5f/fps;
                    view.Render(actor,1,1f/fps,new Vector3(0,0,20));var foot=view.FootPosition(true);
                    Assert.Greater(foot.y,.025f);Assert.Greater(view.FootPosition(false).y,.025f);if(frame>2)Assert.Less(Vector3.Distance(foot,last),.48f);last=foot;
                    Assert.That(Mathf.Abs(Mathf.DeltaAngle(go.transform.eulerAngles.y,0)),Is.LessThan(.01f),"Travel must not rotate a keeper away from the ball");
                }
            }finally{Object.DestroyImmediate(go);}
        }
        [Test] public void FingersAreSkinnedAndOpenForKeeperGather()
        {
            var go=new GameObject("Articulated hands");try{var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="hands"},0,0,Color.yellow);var renderer=go.GetComponentInChildren<SkinnedMeshRenderer>();int fingerCount=0,weighted=0;
                for(int i=0;i<renderer.bones.Length;i++)if(renderer.bones[i].name.StartsWith("finger")){fingerCount++;foreach(var w in renderer.sharedMesh.boneWeights)if(w.boneIndex0==i&&w.weight0>.1f||w.boneIndex1==i&&w.weight1>.1f){weighted++;break;}}
                Assert.AreEqual(30,fingerCount);Assert.Greater(weighted,20);
                var actor=new Actor{action="run",velocity=new Point(0,3)};view.Render(actor,1,1);var finger=go.transform.Find("Rig");Transform joint=null;foreach(var bone in renderer.bones)if(bone.name=="finger3-2.L")joint=bone;Assert.NotNull(joint);var running=joint.localRotation;
                actor.action="claim";actor.actionTime=.8f;actor.velocity=new Point();view.Render(actor,1,1,new Vector3(0,1.1f,.3f));Assert.Greater(Quaternion.Angle(running,joint.localRotation),15);
                var open=joint.localRotation;actor.actionTime=.5f;view.Render(actor,1,1,new Vector3(0,1.1f,.3f));Assert.Greater(Quaternion.Angle(open,joint.localRotation),15,"Close around the ball after the gather");
            }finally{Object.DestroyImmediate(go);}
        }
        [TestCase(30)] [TestCase(60)] [TestCase(120)] public void StandingTurnReplantsFeetInsteadOfSlidingBoth(int fps)
        {
            var go=new GameObject("Standing turn");try{var v=go.AddComponent<PlayerView>();v.Build(new PlayerData{id="pivot"},0,9,Color.white);var a=new Actor{action="idle"};float highest=0;
                for(int frame=0;frame<fps*3;frame++){
                    a.angle=Mathf.Min(Mathf.PI,frame/(float)fps*Mathf.PI*.5f);v.Render(a,1,1f/fps);
                    float left=v.FootPosition(true).y,right=v.FootPosition(false).y;highest=Mathf.Max(highest,left,right);
                    Assert.Less(Mathf.Min(left,right),.15f,"One foot must support the turn");Assert.Greater(left,.025f);Assert.Greater(right,.025f);Assert.That(go.transform.position,Is.EqualTo(Vector3.zero));
                }
                Assert.That(highest,Is.InRange(.13f,.25f),"A standing turn needs an actual lifted step");
            }finally{Object.DestroyImmediate(go);}
        }
    }
}

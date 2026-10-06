using NUnit.Framework;
using UnityEngine;
using Touchline.Core;

namespace Touchline.Tests
{
    public class FullBodyMotionTests
    {
        [Test] public void CapturesHaveFiniteJointsAndMetreScaleCadence()
        {
            Assert.That(FullBodyMotion.Available,Is.True);Assert.That(FullBodyMotion.Stride(1.4f),Is.InRange(1.2f,1.8f));Assert.That(FullBodyMotion.Stride(4),Is.InRange(2f,3f));var joints=new Vector3[19];
            for(int variant=0;variant<6;variant++)for(int frame=0;frame<=64;frame++){FullBodyMotion.Kick(variant,frame*.01f,false,joints);foreach(var v in joints){Assert.That(float.IsNaN(v.x+v.y+v.z)||float.IsInfinity(v.x+v.y+v.z),Is.False);Assert.That(v.magnitude,Is.LessThan(1.6f));}}
        }
        [TestCase(false)] [TestCase(true)] public void KickBootMeetsAuthoritativeReleasePoint(bool left)
        {
            for(int variant=0;variant<6;variant++){
                var go=new GameObject("Contact test");try{var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="contact-"+variant,preferredFoot=left?"Left":"Right"},0,9,Color.white);
                    var actor=new Actor{id="contact-"+variant,position=new Point(4,6),previous=new Point(4,6),angle=variant*Mathf.PI/3,action="kick",actionSequence=variant+1};var direction=new Vector3(Mathf.Sin(actor.angle),0,Mathf.Cos(actor.angle));var ball=new Vector3(4,.11f,6)+direction*.42f;actor.actionTarget=new Point(ball.x,ball.z);actor.actionHeight=ball.y;go.transform.rotation=Quaternion.Euler(0,actor.angle*Mathf.Rad2Deg,0);
                    for(int frame=0;frame<=18;frame++){actor.actionTime=.64f-frame*.01f;view.Render(actor,1,.01f,ball);}
                    float gap=Vector3.Distance(view.BootContactPosition(left),ball);Assert.That(gap,Is.LessThan(.06f),"variant "+variant+" gap "+gap);
                }finally{Object.DestroyImmediate(go);}
            }
        }
        [Test] public void LocomotionKeepsFeetAbovePitchAndHasNoFrameTeleport()
        {
            var go=new GameObject("Gait test");try{var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="running",preferredFoot="Right"},0,9,Color.white);var actor=new Actor{id="running",action="run",velocity=new Point(0,3.5f)};Vector3 previous=default;
                for(int frame=0;frame<180;frame++){actor.previous=actor.position;actor.position.z=frame*3.5f/60;view.Render(actor,1,1f/60);var foot=view.FootPosition(true);Assert.That(foot.y,Is.GreaterThan(.035f));if(frame>2)Assert.That(Vector3.Distance(previous,foot),Is.LessThan(.42f),"frame "+frame);previous=foot;}
            }finally{Object.DestroyImmediate(go);}
        }
        [Test] public void ViewportDoesNotUseDeviceDpi()
        {
            var panel=ScriptableObject.CreateInstance<UnityEngine.UIElements.PanelSettings>();try{InterfaceViewport.Apply(panel,1280,966);Assert.That(panel.scaleMode,Is.EqualTo(UnityEngine.UIElements.PanelScaleMode.ConstantPixelSize));Assert.That(1280/panel.scale,Is.EqualTo(1120).Within(.1f));}finally{Object.DestroyImmediate(panel);}
        }
        [TestCase(false)] [TestCase(true)] public void RunningIntoKickPreservesContactAndSmoothFirstFrame(bool left)
        {
            var go=new GameObject("Running kick transition");try{
                var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="transition",preferredFoot=left?"Left":"Right"},0,9,Color.white);
                var actor=new Actor{id="transition",action="run",velocity=new Point(0,3.5f)};
                for(int i=0;i<45;i++){actor.previous=actor.position;actor.position.z+=3.5f/60;view.Render(actor,1,1f/60);}
                var prior=view.FootPosition(left);actor.previous=actor.position;actor.velocity=new Point();actor.action="kick";actor.actionSequence=1;
                var ball=new Vector3(actor.position.x,.11f,actor.position.z+.42f);actor.actionTarget=new Point(ball.x,ball.z);actor.actionHeight=.11f;
                for(int frame=0;frame<=18;frame++){
                    actor.actionTime=.64f-frame*.01f;view.Render(actor,1,.01f,ball);
                    if(frame==0)Assert.That(Vector3.Distance(prior,view.FootPosition(left)),Is.LessThan(.15f),"Action entry snaps the boot");
                    Assert.That(view.FootPosition(left).y,Is.GreaterThan(.03f));
                }
                Assert.That(Vector3.Distance(view.BootContactPosition(left),ball),Is.LessThan(.06f),"Transition delayed the strike contact");
            }finally{Object.DestroyImmediate(go);}
        }
        [Test] public void LateFirstKickFrameStillMeetsBall()
        {
            var go=new GameObject("Late observed strike");try{
                var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="late",preferredFoot="Right"},0,9,Color.white);
                var actor=new Actor{action="run",velocity=new Point(0,3)};view.Render(actor,1,1f/60);
                actor.velocity=new Point();actor.action="kick";actor.actionTime=.46f;actor.actionSequence=1;actor.actionTarget=new Point(0,.42f);actor.actionHeight=.11f;
                var ball=new Vector3(0,.11f,.42f);view.Render(actor,1,1f/120,ball);
                Assert.That(Vector3.Distance(view.BootContactPosition(false),ball),Is.LessThan(.06f));
            }finally{Object.DestroyImmediate(go);}
        }
        [TestCase(-.23f,false)] [TestCase(.23f,true)] public void ReceptionUsesTheFootOnTheBallSide(float lateral,bool left)
        {
            var go=new GameObject("Reception contact");try{
                var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="receiver",preferredFoot=left?"Right":"Left"},0,9,Color.white);
                var actor=new Actor{action="run",velocity=new Point(0,2)};view.Render(actor,1,1f/60);
                actor.action="control";actor.velocity=new Point();var ball=new Vector3(lateral,.11f,.42f);
                for(int frame=0;frame<=12;frame++){actor.actionTime=.3f-frame*.01f;view.Render(actor,1,.01f,ball);}
                Assert.That(Vector3.Distance(view.BootContactPosition(left),ball),Is.LessThan(.06f),"Control reaches for the ball rather than a fixed point ahead");
                Assert.That(view.FootPosition(!left).y,Is.GreaterThan(.03f));
            }finally{Object.DestroyImmediate(go);}
        }
    }
}

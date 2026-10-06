using System.Linq;
using NUnit.Framework;
using Touchline.Core;
using UnityEngine;
namespace Touchline.Tests
{
    public class SlidingRunHandoffTests
    {
        [TestCase(160,1,0)] [TestCase(182,-1,90)] [TestCase(205,1,180)]
        public void ReturningToLocomotionDoesNotCreateAFixedJointJump(int height,int side,float heading)
        {
            var slow=Measure(height,side,heading,30);var medium=Measure(height,side,heading,60);var fast=Measure(height,side,heading,120);
            if(height==182){
                TestContext.WriteLine($"supportPitch30={slow[8]:F5}; supportPitch60={medium[8]:F5}; supportPitch120={fast[8]:F5}");
                Assert.Less(Mathf.Abs(fast[8]-slow[8]),5f,"The same loaded support must not acquire extra ankle rotation on a faster display");
                Assert.Less(Mathf.Abs(fast[8]-medium[8]),5f,"Support articulation must be stable across refresh rates");
            }
            for(int j=0;j<8;j++){
                TestContext.WriteLine($"joint={j}; local30={slow[j]:F5}m/s; local60={medium[j]:F5}m/s; local120={fast[j]:F5}m/s");
                Assert.LessOrEqual(fast[j],Mathf.Max(slow[j],medium[j])*1.5f+.5f,"A pose discontinuity must not grow with rendering rate after getting up");
            }
        }
        static float[] Measure(int height,int side,float heading,int fps)
        {
            var go=new GameObject("Slide to run handoff");
            try{
                var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="slide-run",heightCm=height},0,2,Color.white);
                var actor=new Actor{slot=2,action="idle"};for(int i=0;i<20;i++)view.Render(actor,1,1f/fps);
                var all=go.GetComponentsInChildren<Transform>();string[] names={"upperleg01.L","upperleg01.R","lowerleg01.L","lowerleg01.R","foot.L","foot.R","wrist.L","wrist.R"};
                var joints=names.Select(n=>all.Single(b=>b.name==n)).ToArray();var previous=joints.Select(j=>go.transform.InverseTransformPoint(j.position)).ToArray();var maximum=new float[joints.Length+1];
                float limbL=Vector3.Distance(joints[0].position,joints[2].position)+Vector3.Distance(joints[2].position,joints[4].position),limbR=Vector3.Distance(joints[1].position,joints[3].position)+Vector3.Distance(joints[3].position,joints[5].position);
                const float travel=.48f+.3744f+.292032f;var ball=new Vector3(side*.12f,.11f,travel+.14f);
                actor.action="slide";actor.actionKind=MatchSimulation.SlidingDuel;actor.actionSequence=1;actor.diveSide=side;actor.actionContactTime=MatchSimulation.SlidingDuelContact;actor.actionTarget=new Point(ball.x,ball.z);actor.actionHeight=.11f;
                var direction=new Vector3(Mathf.Sin(heading*Mathf.Deg2Rad),0,Mathf.Cos(heading*Mathf.Deg2Rad));
                for(int frame=0;frame<=fps*3;frame++){
                    float time=frame/(float)fps;actor.previous=actor.position;
                    if(time<MatchSimulation.SlidingDuelDuration){
                        // Presentation fixture reproduces the three committed
                        // Core travel segments; it does not change Core rules.
                        float d=time<.1f?4.8f*time:time<.2f?.48f+3.744f*(time-.1f):time<.3f?.8544f+2.92032f*(time-.2f):travel;
                        float speed=time<.1f?4.8f:time<.2f?3.744f:time<.3f?2.92032f:0;
                        actor.position=new Point(0,d);actor.velocity=new Point(0,speed);actor.actionTime=MatchSimulation.SlidingDuelDuration-time;
                    }else{
                        float running=Mathf.Max(0,time-1.8f),speed=Mathf.Min(4.8f,running*6),d=running<=.8f?3*running*running:1.92f+(running-.8f)*4.8f;
                        actor.action=running>0?"run":"idle";actor.actionKind="";actor.actionTime=0;actor.angle=running>0?heading*Mathf.Deg2Rad:0;
                        actor.velocity=new Point(direction.x*speed,direction.z*speed);actor.position=new Point(direction.x*d,travel+direction.z*d);
                    }
                    actor.stride+=Point.Distance(actor.previous,actor.position);view.Render(actor,1,1f/fps,ball);
                    for(int j=0;j<joints.Length;j++){
                        var local=go.transform.InverseTransformPoint(joints[j].position);float speed=Vector3.Distance(previous[j],local)*fps*go.transform.localScale.y;
                        if(time>=1.7f&&time<=2.35f)maximum[j]=Mathf.Max(maximum[j],speed);previous[j]=local;
                    }
                    if(Mathf.Abs(time-2.1f)<.00001f)maximum[8]=Mathf.DeltaAngle(0,(Quaternion.Inverse(go.transform.rotation)*joints[5].rotation).eulerAngles.x);
                    Assert.That(Vector3.Distance(joints[0].position,joints[2].position)+Vector3.Distance(joints[2].position,joints[4].position),Is.EqualTo(limbL).Within(.0001f));
                    Assert.That(Vector3.Distance(joints[1].position,joints[3].position)+Vector3.Distance(joints[3].position,joints[5].position),Is.EqualTo(limbR).Within(.0001f));
                    Assert.Greater(view.FootPosition(true).y,.025f);Assert.Greater(view.FootPosition(false).y,.025f);
                }
                return maximum;
            }finally{Object.DestroyImmediate(go);}
        }
    }
}



using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Touchline.Core;

namespace Touchline.Tests
{
    public class SlidingRecoveryContinuityTests
    {
        // The same physical poses sampled at three display rates. A fixed
        // pose jump grows in measured speed with FPS; a smooth curve does not.
        [TestCase(145,1)] [TestCase(175,-1)] [TestCase(215,1)]
        public void RecoveryJointMotionHasNoFrameRateDependentJump(int height,int side)
        {
            var slow=Measure(height,side,30);var medium=Measure(height,side,60);var fast=Measure(height,side,120);
            for(int joint=0;joint<slow.Length;joint++){
                TestContext.WriteLine($"joint={joint}; recovery30={slow[joint]:F5}m/s; recovery60={medium[joint]:F5}m/s; recovery120={fast[joint]:F5}m/s");
                Assert.LessOrEqual(fast[joint],Mathf.Max(slow[joint],medium[joint])*1.5f+.5f,"A fixed discontinuity must not amplify with render frequency");
            }
        }
        static float[] Measure(int height,int side,int fps)
        {
            var go=new GameObject("Slide recovery continuity");
            try{
                var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="slide-recovery",heightCm=height},0,2,Color.white);
                var actor=new Actor{slot=2,action="idle"};for(int i=0;i<30;i++)view.Render(actor,1,1f/fps);
                var all=go.GetComponentsInChildren<Transform>();string[] names={"upperleg01.L","upperleg01.R","lowerleg01.L","lowerleg01.R","foot.L","foot.R","wrist.L","wrist.R"};
                var joints=names.Select(name=>all.Single(b=>b.name==name)).ToArray();var previous=joints.Select(b=>b.position).ToArray();var maximum=new float[joints.Length];
                var ball=new Vector3(side*.12f,.11f,.14f);actor.action="slide";actor.actionKind=MatchSimulation.SlidingDuel;actor.actionSequence=1;actor.actionContactTime=MatchSimulation.SlidingDuelContact;actor.diveSide=side;actor.actionTarget=new Point(ball.x,ball.z);actor.actionHeight=.11f;
                float limbL=Vector3.Distance(joints[0].position,joints[2].position)+Vector3.Distance(joints[2].position,joints[4].position);
                float limbR=Vector3.Distance(joints[1].position,joints[3].position)+Vector3.Distance(joints[3].position,joints[5].position);
                for(int frame=0;frame<=fps*2;frame++){
                    float time=frame/(float)fps;actor.actionTime=Mathf.Max(0,MatchSimulation.SlidingDuelDuration-time);if(time>MatchSimulation.SlidingDuelDuration)actor.action="idle";
                    view.Render(actor,1,1f/fps,ball);
                    for(int j=0;j<joints.Length;j++){
                        float speed=Vector3.Distance(previous[j],joints[j].position)*fps;
                        // Includes lead-leg retraction, support transfer, rise,
                        // the 1.7s transition back to idle and its pose blend.
                        if(time>=.48f&&frame>0)maximum[j]=Mathf.Max(maximum[j],speed);
                        previous[j]=joints[j].position;
                    }
                    Assert.AreEqual(Vector3.zero,go.transform.position);
                    Assert.That(Vector3.Distance(joints[0].position,joints[2].position)+Vector3.Distance(joints[2].position,joints[4].position),Is.EqualTo(limbL).Within(.0001f));
                    Assert.That(Vector3.Distance(joints[1].position,joints[3].position)+Vector3.Distance(joints[3].position,joints[5].position),Is.EqualTo(limbR).Within(.0001f));
                    Assert.Greater(view.FootPosition(true).y,.025f);Assert.Greater(view.FootPosition(false).y,.025f);
                }
                return maximum;
            }finally{UnityEngine.Object.DestroyImmediate(go);}
        }
    }
}

using System.Linq;
using NUnit.Framework;
using Touchline.Core;
using UnityEngine;
namespace Touchline.Tests
{
    public class SupportReleaseAuditTests
    {
        // Diagnostic only: compare identical fixtures on cache-only V2 and
        // rejected early-release V3. No new movement tolerance is introduced.
        [TestCase(160,1,0,true)] [TestCase(182,-1,90,false)]
        public void RecordActualSupportGeometry(int height,int side,float heading,bool slide)
        {
            foreach(int fps in new[]{30,60,120})Measure(height,side,heading,fps,slide);
        }
        static float[] Measure(int height,int side,float heading,int fps,bool slide)
        {
            var go=new GameObject("Slide to run handoff");
            try{
                var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="slide-run",heightCm=height},0,2,Color.white);
                var actor=new Actor{slot=2,action="idle"};for(int i=0;i<20;i++)view.Render(actor,1,1f/fps);
                var all=go.GetComponentsInChildren<Transform>();string[] names={"upperleg01.L","upperleg01.R","lowerleg01.L","lowerleg01.R","foot.L","foot.R","wrist.L","wrist.R"};
                var rig=all.Single(b=>b.name=="Rig");var joints=names.Select(n=>all.Single(b=>b.name==n)).ToArray();var previous=joints.Select(j=>go.transform.InverseTransformPoint(j.position)).ToArray();var maximum=new float[joints.Length];var maximumDetail=new string[joints.Length];
                var gaitField=typeof(PlayerView).GetField("gait",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
                var releasedField=typeof(PlayerView).GetField("captureReachReleased",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
                var releaseTimeField=typeof(PlayerView).GetField("captureReleaseTime",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
                var captureField=typeof(PlayerView).GetField("captureStance",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
                float limbL=Vector3.Distance(joints[0].position,joints[2].position)+Vector3.Distance(joints[2].position,joints[4].position),limbR=Vector3.Distance(joints[1].position,joints[3].position)+Vector3.Distance(joints[3].position,joints[5].position);
                float travel=slide?.48f+.3744f+.292032f:0;var ball=new Vector3(side*.12f,.11f,travel+.14f);
                actor.action="slide";actor.actionKind=MatchSimulation.SlidingDuel;actor.actionSequence=1;actor.diveSide=side;actor.actionContactTime=MatchSimulation.SlidingDuelContact;actor.actionTarget=new Point(ball.x,ball.z);actor.actionHeight=.11f;
                var direction=new Vector3(Mathf.Sin(heading*Mathf.Deg2Rad),0,Mathf.Cos(heading*Mathf.Deg2Rad));
                for(int frame=0;frame<=fps*3;frame++){
                    float time=frame/(float)fps;actor.previous=actor.position;
                    if(time<MatchSimulation.SlidingDuelDuration){
                        // Presentation fixture reproduces the three committed
                        // Core travel segments; it does not change Core rules.
                        float d=!slide?0:time<.1f?4.8f*time:time<.2f?.48f+3.744f*(time-.1f):time<.3f?.8544f+2.92032f*(time-.2f):travel;
                        float speed=!slide?0:time<.1f?4.8f:time<.2f?3.744f:time<.3f?2.92032f:0;
                        actor.position=new Point(0,d);actor.velocity=new Point(0,speed);actor.actionTime=slide?MatchSimulation.SlidingDuelDuration-time:0;actor.action=slide?"slide":"idle";actor.actionKind=slide?MatchSimulation.SlidingDuel:"";
                    }else{
                        float running=Mathf.Max(0,time-1.8f),speed=Mathf.Min(4.8f,running*6),d=running<=.8f?3*running*running:1.92f+(running-.8f)*4.8f;
                        actor.action=running>0?"run":"idle";actor.actionKind="";actor.actionTime=0;actor.angle=running>0?heading*Mathf.Deg2Rad:0;
                        actor.velocity=new Point(direction.x*speed,direction.z*speed);actor.position=new Point(direction.x*d,travel+direction.z*d);
                    }
                    actor.stride+=Point.Distance(actor.previous,actor.position);view.Render(actor,1,1f/fps,ball);
                    for(int j=0;j<joints.Length;j++){
                        var local=go.transform.InverseTransformPoint(joints[j].position);float speed=Vector3.Distance(previous[j],local)*fps*go.transform.localScale.y;
                        if(time>=1.7f&&time<=2.35f&&speed>maximum[j]){
                            maximum[j]=speed;var stance=(bool[])captureField.GetValue(view);
                            maximumDetail[j]=$"fps={fps}; joint={names[j]}; time={time:F6}; frame={frame}; action={actor.action}; speed={speed:F6}; previousLocal={previous[j]:F6}; currentLocal={local:F6}; gait={gaitField.GetValue(view)}; captureStance={stance[0]}/{stance[1]}; root={go.transform.position:F6}; yaw={go.transform.eulerAngles.y:F6}; leftFoot={view.FootPosition(true):F6}; rightFoot={view.FootPosition(false):F6}; rightHip={joints[1].position:F6}; rightKnee={joints[3].position:F6}";
                        }
                        previous[j]=local;
                    }
                    if(time>=1.9f&&time<=2.35f){
                        var stance=(bool[])captureField.GetValue(view);
                        var forced=releasedField==null?new bool[2]:(bool[])releasedField.GetValue(view);
                        var release=(float[])releaseTimeField.GetValue(view);
                        for(int leg=0;leg<2;leg++){
                            var hip=joints[leg];var knee=joints[leg+2];var foot=joints[leg+4];var rotation=foot.rotation;
                            var center=rotation*new Vector3(0,.02f,.075f);
                            float extent=Mathf.Abs((rotation*Vector3.right).y)*.057f+Mathf.Abs((rotation*Vector3.up).y)*.085f+Mathf.Abs((rotation*Vector3.forward).y)*.145f;
                            float sole=foot.position.y+(center.y-extent)*go.transform.localScale.y;
                            float length=Vector3.Distance(hip.position,knee.position)+Vector3.Distance(knee.position,foot.position);
                            float reach=Vector3.Distance(hip.position,foot.position)/length;
                            float bend=180-Vector3.Angle(hip.position-knee.position,foot.position-knee.position);
                            TestContext.WriteLine($"SUPPORT height={height}; slide={slide}; fps={fps}; leg={leg}; t={time:F6}; kneeLocal={go.transform.InverseTransformPoint(knee.position):F6}; footLocal={go.transform.InverseTransformPoint(foot.position):F6}; footWorld={foot.position:F6}; sole={sole:F6}; reach={reach:F6}; bendDegrees={bend:F6}; footPitch={Mathf.DeltaAngle(0,(Quaternion.Inverse(go.transform.rotation)*foot.rotation).eulerAngles.x):F6}; stance={stance[leg]}; forced={forced[leg]}; releaseSeconds={release[leg]:F6}; gait={gaitField.GetValue(view)}; yaw={go.transform.eulerAngles.y:F6}; velocity={actor.velocity.Length:F6}");
                        }
                    }
                    Assert.That(Vector3.Distance(joints[0].position,joints[2].position)+Vector3.Distance(joints[2].position,joints[4].position),Is.EqualTo(limbL).Within(.0001f));
                    Assert.That(Vector3.Distance(joints[1].position,joints[3].position)+Vector3.Distance(joints[3].position,joints[5].position),Is.EqualTo(limbR).Within(.0001f));
                    Assert.Greater(view.FootPosition(true).y,.025f);Assert.Greater(view.FootPosition(false).y,.025f);
                }
                foreach(var detail in maximumDetail)TestContext.WriteLine("HANDOFF_MAX: "+detail);
                return maximum;
            }finally{Object.DestroyImmediate(go);}
        }
    }
}



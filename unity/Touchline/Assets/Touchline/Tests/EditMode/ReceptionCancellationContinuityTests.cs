using NUnit.Framework;
using Touchline.Core;
using UnityEngine;
namespace Touchline.Tests
{
 public class ReceptionCancellationContinuityTests
 {
  [TestCase(30,false)][TestCase(60,false)][TestCase(120,false)]
  [TestCase(30,true)][TestCase(60,true)][TestCase(120,true)]
  public void CancelledIncomingControlLowersPreparedLegWithoutOneFrameSnap(int fps,bool shoulderContact){
   var go=new GameObject("Reception cancellation continuity");try{
    var data=new PlayerData{id="r",heightCm=182,rating=75};var view=go.AddComponent<PlayerView>();view.Build(data,0,9,Color.white);
    var actor=new Actor{id="r",slot=9,side=0,action="idle",angle=0};var m=new MatchState{phase="play",restart=0,actors=new[]{actor},ball=new BallState{kind="pass",from="s",to="r",side=0,start=new Point(.23f,2.8f),end=new Point(.23f,.3f),startHeight=.95f,endHeight=.95f,height=.95f,duration=1}};
    view.Render(actor,1,0);for(int f=1;f<=Mathf.CeilToInt(.36f*fps);f++){
     m.ball.elapsed=.60f+Mathf.Min(.36f,(float)f/fps);m.ball.position=Point.Lerp(m.ball.start,m.ball.end,m.ball.elapsed);
     view.Render(actor,1,1f/fps,new Vector3(m.ball.position.x,.95f,m.ball.position.z),PlayerMotionContext.From(m,actor,1,1.82f));
    }
    var before=view.FootPosition(true);Assert.Greater(before.y,.25f,"Fixture must have a visibly prepared thigh.");
    m.ball.kind="loose";m.ball.to=null;
    if(shoulderContact)m.actors=new[]{actor,new Actor{id="opponent",side=1,slot=5,position=new Point(0,.8f),velocity=new Point(0,-2)}};
    var context=PlayerMotionContext.From(m,actor,1,1.82f);if(shoulderContact)Assert.Greater(context.contactWeight,.1f);
    string saved=JsonUtility.ToJson(m);view.Render(actor,1,1f/fps,new Vector3(.23f,.95f,.3f),context);Assert.AreEqual(saved,JsonUtility.ToJson(m));
    float step=Vector3.Distance(before,view.FootPosition(true));TestContext.WriteLine($"{fps}fps shoulder={shoulderContact} firstDrop={step:F6}");
    Assert.Less(step,.03f+5f/fps,"Cancellation must lower readiness over time even when another player approaches the shoulder.");
    var last=view.FootPosition(true);float peak=step;
    for(int frame=1;frame<=Mathf.CeilToInt(.35f*fps);frame++){
     view.Render(actor,1,1f/fps,new Vector3(.23f,.95f,.3f),context);
     var foot=view.FootPosition(true);float movement=Vector3.Distance(last,foot);peak=Mathf.Max(peak,movement);last=foot;
     Assert.Less(movement,.03f+5f/fps,"The interruption must not defer its snap to a later recovery frame.");
    }
    TestContext.WriteLine($"{fps}fps shoulder={shoulderContact} recoveryPeak={peak:F6} finalFootY={last.y:F6}");Assert.Less(last.y,.15f,"Readiness must actually return to a grounded stance after cancellation.");
   }finally{Object.DestroyImmediate(go);}
  }
 }
}

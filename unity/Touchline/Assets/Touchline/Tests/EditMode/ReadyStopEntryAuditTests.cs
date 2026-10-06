using System;
using System.Reflection;
using NUnit.Framework;
using Touchline.Core;
using UnityEngine;
namespace Touchline.Tests
{
 public class ReadyStopEntryAuditTests
 {
  [TestCase(30)][TestCase(60)][TestCase(120)]
  public void ReadinessEntryDuringBrakingCannotSnapBothAnkles(int fps)
  {
   var go=new GameObject("Ready stop entry audit");try{
    var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="ready-audit",heightCm=182},0,9,Color.blue);
    Vector3[] previous=null;float maximum=0,peakTime=0;int peakFoot=0;Vector3 peakDelta=Vector3.zero;bool peakPlanner=false;
    for(int frame=0;frame<=fps*6/5;frame++){
     float t=(float)frame/fps,u=t-.5f,speed=t<.5f?3:t<1?3-6*u:0,z=t<.5f?3*t:t<1?1.5f+3*u-3*u*u:2.25f;
     var actor=new Actor{id="ready-audit",slot=9,action="run",intent="press",angle=0,position=new Point(0,z),previous=new Point(0,z),velocity=new Point(0,speed)};
     view.Render(actor,1,frame==0?0:1f/fps,new Vector3(0,.11f,4),new PlayerMotionContext{defending=true});
     var current=new[]{view.FootPosition(true),view.FootPosition(false)};
     if(previous!=null&&t>=.93f&&t<=1.05f)for(int foot=0;foot<2;foot++){
      var delta=current[foot]-previous[foot];if(delta.magnitude>maximum){maximum=delta.magnitude;peakTime=t;peakFoot=foot;peakDelta=delta;peakPlanner=((StopStepMotion)typeof(PlayerView).GetField("stoppingSteps",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(view)).Active;}
     }
     previous=current;
    }
    TestContext.WriteLine($"fps={fps} maxAnkleDelta={maximum:F6}m time={peakTime:F6} foot={peakFoot} vector={peakDelta:F6} plannerActive={peakPlanner}");
    Assert.Less(maximum,.15f,"Entering the ready stance before complete stop must not teleport a planted ankle by half a metre");
   }finally{UnityEngine.Object.DestroyImmediate(go);}
  }
 }
}

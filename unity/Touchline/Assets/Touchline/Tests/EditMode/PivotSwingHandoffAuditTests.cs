using System;
using System.Reflection;
using NUnit.Framework;
using Touchline.Core;
using UnityEngine;
namespace Touchline.Tests {
 public class PivotSwingHandoffAuditTests {
  [TestCase(30)][TestCase(60)][TestCase(120)]
  public void GentleRestartFromPivotDoesNotReturnToTheOldRunningSupport(int fps){
   var go=new GameObject("Pivot restart audit");try{
    var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="audit-pivot",heightCm=182},0,9,Color.blue);
    var actor=new Actor{id="audit-pivot",slot=9,action="run",velocity=new Point(0,3.5f)};
    for(int frame=0;frame<fps;frame++){actor.previous=actor.position;actor.position+=actor.velocity/fps;view.Render(actor,1,1f/fps);}
    actor.previous=actor.position;actor.velocity=new Point();actor.angle=Mathf.PI*.5f;
    for(int frame=0;frame<fps/2;frame++)view.Render(actor,1,1f/fps);
    float pivot=(float)typeof(PlayerView).GetField("pivotTime",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(view);
    var stop=(StopStepMotion)typeof(PlayerView).GetField("stoppingSteps",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(view);
    TestContext.WriteLine($"fps={fps} pivotRemaining={pivot:0.000} stopPlannerActive={stop.Active}");Assert.Greater(pivot,0,"Fixture must isolate an active pivot");Assert.IsFalse(stop.Active,"Do not accidentally test the stop planner's repaired anchors");
    var before=new[]{view.FootPosition(true),view.FootPosition(false)};actor.velocity=new Point(.6f,0);actor.position=actor.previous=actor.position+actor.velocity/fps;view.Render(actor,1,1f/fps);
    var after=new[]{view.FootPosition(true),view.FootPosition(false)};
    Assert.AreEqual(0,(float)typeof(PlayerView).GetField("pivotTime",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(view),"Fixture must leave PivotFootwork so captured locomotion owns the restart");
    for(int i=0;i<2;i++){float jump=Vector3.Distance(before[i],after[i]);TestContext.WriteLine($"fps={fps} foot{i} restartJump={jump:0.000000}m delta={(after[i]-before[i]):F6}");Assert.Less(jump,.01f+2f/fps,"A pivot-to-run handoff must consume elapsed time rather than switch airborne ankle poses instantly");}
   }finally{UnityEngine.Object.DestroyImmediate(go);}
  }
 }
}

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
    for(int frame=0;frame<fps;frame++){actor.position+=actor.velocity/fps;actor.previous=actor.position-actor.velocity*MatchSimulation.Step;view.Render(actor,1,1f/fps);}
    actor.previous=actor.position;actor.velocity=new Point();actor.angle=Mathf.PI*.5f;
    for(int frame=0;frame<Mathf.CeilToInt(fps*.15f);frame++)view.Render(actor,1,1f/fps);
    float remaining=Quaternion.Angle(go.transform.rotation,Quaternion.Euler(0,90,0));
    Assert.That(remaining,Is.InRange(1f,89f),"Fixture must observe an actual incomplete turn, independent of the animation backend");
    var before=new[]{view.FootPosition(true),view.FootPosition(false)};actor.velocity=new Point(.6f,0);actor.previous=actor.position;actor.position+=actor.velocity*MatchSimulation.Step;view.Render(actor,1f/(fps*MatchSimulation.Step),1f/fps);
    var after=new[]{view.FootPosition(true),view.FootPosition(false)};
    for(int i=0;i<2;i++){float jump=Vector3.Distance(before[i],after[i]);TestContext.WriteLine($"fps={fps} foot{i} restartJump={jump:0.000000}m delta={(after[i]-before[i]):F6}");Assert.Less(jump,.01f+2f/fps,"A pivot-to-run handoff must consume elapsed time rather than switch airborne ankle poses instantly");}
   }finally{UnityEngine.Object.DestroyImmediate(go);}
  }
 }
}

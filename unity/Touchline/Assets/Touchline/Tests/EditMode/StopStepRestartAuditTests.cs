using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Touchline.Core;
using UnityEngine;
namespace Touchline.Tests
{
 // Diagnostic fixture to promote only after the current validation freeze.
 public class StopStepRestartAuditTests
 {
  [TestCase(30)][TestCase(60)][TestCase(120)]
  public void GentleRestartCannotReuseAPlantFromBeforeTheCompletedStop(int fps)
  {
   var go=new GameObject("Stop then gentle restart");try{
    var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="restart-audit",heightCm=182},0,9,Color.blue);
    var actor=new Actor{id="restart-audit",slot=9,action="run",angle=0};
    for(int frame=0;frame<=fps*2;frame++){
     float t=(float)frame/fps,u=t-.5f,s=t<.5f?3:t<1?3-6*u:0,z=t<.5f?3*t:t<1?1.5f+3*u-3*u*u:2.25f;
     actor.position=actor.previous=new Point(0,z);actor.velocity=new Point(0,s);view.Render(actor,1,1f/fps,new Vector3(0,.11f,4));
    }
    var planner=(StopStepMotion)typeof(PlayerView).GetField("stoppingSteps",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(view);Assert.IsTrue(planner.Complete);
    var before=new[]{view.FootPosition(true),view.FootPosition(false)};
    actor.position=actor.previous=new Point(0,2.25f+.3f/fps);actor.velocity=new Point(0,.3f);view.Render(actor,1,1f/fps,new Vector3(0,.11f,4));
    var after=new[]{view.FootPosition(true),view.FootPosition(false)};
    for(int i=0;i<2;i++){float delta=Vector3.Distance(before[i],after[i]);TestContext.WriteLine("fps="+fps+" foot="+i+" firstRestartDisplacement="+delta);Assert.Less(delta,.15f,"A gentle restart must not snap the ankle back to a stale pre-stop support anchor");}
   }finally{Object.DestroyImmediate(go);}
  }
 }
}

using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Touchline.Core;
using UnityEngine;
namespace Touchline.Tests
{
 // Diagnostic fixture to promote only after the current validation freeze.
 public class StopStepRestartPoseAuditTests
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
    var feet=new[]{go.GetComponentsInChildren<Transform>().First(t=>t.name=="foot.L"),go.GetComponentsInChildren<Transform>().First(t=>t.name=="foot.R")};
    var rotations=new[]{feet[0].rotation,feet[1].rotation};
    actor.position=actor.previous=new Point(0,2.25f+.3f/fps);actor.velocity=new Point(0,.3f);view.Render(actor,1,1f/fps,new Vector3(0,.11f,4));
    var after=new[]{view.FootPosition(true),view.FootPosition(false)};
    var joints=(Vector3[])typeof(PlayerView).GetField("capturedJoints",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(view);
    for(int i=0;i<2;i++){
     var displacement=after[i]-before[i];int leg=i==0?11:15;var toe=joints[leg+3]-joints[leg+2];
     float rawPitch=Mathf.Clamp(-Mathf.Atan2(toe.y,new Vector2(toe.x,toe.z).magnitude)*Mathf.Rad2Deg,-20,32);
     float appliedPitch=Mathf.DeltaAngle(0,(Quaternion.Inverse(view.transform.rotation)*feet[i].rotation).eulerAngles.x);
     TestContext.WriteLine($"fps={fps} foot={i} before={before[i]:F6} after={after[i]:F6} delta={displacement:F6} footRotationJump={Quaternion.Angle(rotations[i],feet[i].rotation):F6}deg rawPitch={rawPitch:F6}deg appliedPitch={appliedPitch:F6}deg locomotionWeight={Mathf.InverseLerp(.15f,1.1f,.3f):F6}");
     Assert.Less(Mathf.Abs(displacement.y),.005f+1f/fps,"A very gentle restart must not instantly raise an ankle by a tenth of a metre independent of elapsed time");
    }
   }finally{Object.DestroyImmediate(go);}
  }
 }
}

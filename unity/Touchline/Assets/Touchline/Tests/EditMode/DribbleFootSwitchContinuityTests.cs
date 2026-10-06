using NUnit.Framework;
using Touchline.Core;
using UnityEngine;
namespace Touchline.Tests
{
 public class DribbleFootSwitchContinuityTests
 {
  [Test]
  public void ZeroWeightDribbleTouchPreservesFootRotationAndJointPose()
  {
   var go=new GameObject("Zero-weight dribble contact");try{
    var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="zero",heightCm=182,rating=75},0,9,Color.white);
    var actor=new Actor{id="zero",slot=9,action="run",velocity=new Point(0,1),stride=Mathf.PI*.5f/2.4f};view.Render(actor,1,0);
    var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
    var bones=(System.Collections.Generic.Dictionary<string,Transform>)typeof(PlayerView).GetField("bones",flags).GetValue(view);
    bones["foot.L"].rotation=Quaternion.Euler(18,7,2);bones["foot.R"].rotation=Quaternion.Euler(14,-6,-2);
    var before=new System.Collections.Generic.Dictionary<string,Quaternion>();foreach(var pair in bones)before[pair.Key]=pair.Value.localRotation;
    typeof(PlayerView).GetMethod("FinalBallContacts",flags).Invoke(view,new object[]{actor,0f,1f,new Vector3(0,.11f,.42f),new PlayerMotionContext{carrying=true}});
    foreach(var pair in bones)Assert.Less(Quaternion.Angle(before[pair.Key],pair.Value.localRotation),.01f,"Zero touch weight must not modify "+pair.Key);
   }finally{Object.DestroyImmediate(go);}
  }

  [TestCase(30,1.5f)][TestCase(60,1.5f)][TestCase(120,1.5f)]
  [TestCase(30,2.5f)][TestCase(60,2.5f)][TestCase(120,2.5f)]
  public void CrossingAlternatingFootBoundaryDoesNotMoveBothFeetAtPeakContact(int fps,float boundaryPi)
  {
   var go=new GameObject("Close-control foot boundary audit");try{
    var data=new PlayerData{id="carrier",name="Carrier",heightCm=182,rating=75};var view=go.AddComponent<PlayerView>();view.Build(data,0,9,Color.white);
    var actor=new Actor{id=data.id,slot=9,action="run",angle=0,velocity=new Point(0,.748f),stride=(boundaryPi*Mathf.PI-.001f)/2.4f};var context=new PlayerMotionContext{carrying=true};var ball=new Vector3(0,.11f,.42f);
    view.Render(actor,1,0,ball,context);for(int i=0;i<fps/5;i++)view.Render(actor,1,1f/fps,ball,context);
    var left=view.FootPosition(true);var right=view.FootPosition(false);actor.previous=actor.position;actor.position+=actor.velocity/fps;actor.stride+=actor.velocity.Length/fps;ball.z+=actor.velocity.Length/fps;
    string before=JsonUtility.ToJson(actor);view.Render(actor,1,1f/fps,ball,context);Assert.AreEqual(before,JsonUtility.ToJson(actor));
    var translation=new Vector3(0,0,actor.velocity.Length/fps);float l=Vector3.Distance(left,view.FootPosition(true)-translation),r=Vector3.Distance(right,view.FootPosition(false)-translation);
    TestContext.WriteLine($"{fps}fps boundaryPi={boundaryPi:F2} left={l:F6} right={r:F6}");Assert.Less(Mathf.Max(l,r),.025f+4f/fps,"Alternation must happen while the ball-contact overlay is zero, not while each foot is being fully placed at the ball.");
   }finally{Object.DestroyImmediate(go);}
  }
 }
}

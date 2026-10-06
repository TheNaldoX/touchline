using System.Reflection;
using NUnit.Framework;
using Touchline.Core;
using UnityEngine;
namespace Touchline.Tests
{
 public class BodyControlCarryHandoffTests
 {
  [TestCase(30)][TestCase(60)][TestCase(120)]
  public void RealBodyControlExitDoesNotInstantlyApplyMaximumDribbleContact(int fps)
  {
   var go=new GameObject("Body control to dribble handoff");try{
    var data=new PlayerData{id="receiver",name="Receiver",heightCm=182,rating=75};var actor=new Actor{id=data.id,slot=9,side=0,action="idle",angle=0,stride=(Mathf.PI*1.5f-.001f)/2.4f};
    var state=new MatchState{home="h",away="a",engineVersion=4,homeTactic=new Tactic(),awayTactic=new Tactic(),phase="play",restart=0,actors=new[]{actor},ball=new BallState{kind="pass",from="s",to=actor.id,side=0,position=new Point(0,.30f),height=.95f}};
    var simulation=new MatchSimulation(new Database{players=new[]{data}},state);var view=go.AddComponent<PlayerView>();view.Build(data,0,9,Color.white);view.Render(actor,1,0);
    var flags=BindingFlags.Instance|BindingFlags.NonPublic;typeof(MatchSimulation).GetMethod("Control",flags).Invoke(simulation,new object[]{actor});Assert.AreEqual(MatchSimulation.ThighControl,actor.actionKind);var contact=new Vector3(actor.actionTarget.x,actor.actionHeight,actor.actionTarget.z);
    for(int frame=0;frame<fps/2;frame++){
     float elapsed=(float)frame/fps,step=Mathf.Floor((elapsed+.00001f)/MatchSimulation.Step),alpha=Mathf.Clamp01((elapsed-step*MatchSimulation.Step)/MatchSimulation.Step);actor.actionTime=.48f-step*MatchSimulation.Step;
     view.Render(actor,alpha,1f/fps,contact,new PlayerMotionContext{carrying=true});
    }
    var left=view.FootPosition(true);var right=view.FootPosition(false);actor.previous=actor.position;actor.actionTime=.08f;
    typeof(MatchSimulation).GetMethod("MoveActor",flags).Invoke(simulation,new object[]{actor,new Point(0,2),3f});Assert.AreEqual("run",actor.action);Assert.Greater(actor.velocity.Length,.4f);
    string before=JsonUtility.ToJson(actor);view.Render(actor,0,1f/fps,new Vector3(0,.11f,.42f),new PlayerMotionContext{carrying=true});Assert.AreEqual(before,JsonUtility.ToJson(actor));
    float l=Vector3.Distance(left,view.FootPosition(true)),r=Vector3.Distance(right,view.FootPosition(false));TestContext.WriteLine($"{fps}fps firstCarryLeft={l:F6} firstCarryRight={r:F6}");
    Assert.Less(Mathf.Max(l,r),.025f+3f/fps,"The first rendered locomotion frame must not jump directly into a full dribble foot-contact overlay.");
   }finally{Object.DestroyImmediate(go);}
  }
 }
}

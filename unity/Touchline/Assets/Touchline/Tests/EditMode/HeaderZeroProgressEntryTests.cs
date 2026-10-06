using System.Reflection;
using NUnit.Framework;
using Touchline.Core;
using UnityEngine;
namespace Touchline.Tests
{
 public class HeaderZeroProgressEntryTests
 {
  [TestCase(30)][TestCase(60)][TestCase(120)]
  public void ActualCoreHeaderPreservesOutgoingFeetAtZeroPhysicalProgress(int fps){
   var go=new GameObject("Header zero-progress entry");try{
    var data=new PlayerData{id="header-entry",name="Header audit",heightCm=182,rating=75};var view=go.AddComponent<PlayerView>();view.Build(data,0,9,Color.white);
    var actor=new Actor{id=data.id,slot=9,side=0,action="run",angle=0,velocity=new Point(0,3.5f)};
    for(int i=0;i<fps;i++){actor.previous=actor.position;actor.position+=actor.velocity/fps;view.Render(actor,1,1f/fps);}
    var feet=new[]{view.FootPosition(true),view.FootPosition(false)};var head=view.HeaderContactPosition;var target=head+Vector3.up*.15f;
    var state=new MatchState{home="h",away="a",engineVersion=4,homeTactic=new Tactic(),awayTactic=new Tactic(),phase="play",restart=0,actors=new[]{actor},ball=new BallState{position=new Point(target.x,target.z),height=target.y,owner=actor.id,kind="cross"}};
    var sim=new MatchSimulation(new Database{players=new[]{data}},state);actor.velocity=new Point();typeof(MatchSimulation).GetMethod("DistributeHeader",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(sim,new object[]{actor,null});
    Assert.AreEqual("header",actor.action);Assert.AreEqual(.64f,actor.actionTime,.0001f);string before=JsonUtility.ToJson(state);
    view.Render(actor,1,1f/fps,target);Assert.AreEqual(before,JsonUtility.ToJson(state));
    for(int i=0;i<2;i++){float jump=Vector3.Distance(feet[i],view.FootPosition(i==0));TestContext.WriteLine($"{fps}fps foot{i} entryJump={jump:F6}");Assert.Less(jump,.04f,"At header entry the physical jump progress is still zero.");}
    actor.actionTime=.64f-actor.actionContactTime;view.Render(actor,1,actor.actionContactTime,target);
    Assert.Less(Vector3.Distance(view.HeaderContactPosition,target),.12f,"The smoothing must preserve actual forehead-ball contact.");
   }finally{Object.DestroyImmediate(go);}
  }
 }
}

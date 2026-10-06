using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Touchline.Core;
using UnityEngine;
namespace Touchline.Tests {
 public class BodyReceptionCoreEntryAuditTests {
  [TestCase(MatchSimulation.ChestControl,30)]
  [TestCase(MatchSimulation.ChestControl,60)]
  [TestCase(MatchSimulation.ChestControl,120)]
  [TestCase(MatchSimulation.ThighControl,30)]
  [TestCase(MatchSimulation.ThighControl,60)]
  [TestCase(MatchSimulation.ThighControl,120)]
  public void CoreGeneratedZeroProgressReceptionPreservesOutgoingForearm(string kind,int fps){
   var go=new GameObject("Core body reception entry audit");
   try{
    var player=new PlayerData{id="audit-core-entry",heightCm=182,rating=75};
    var view=go.AddComponent<PlayerView>();view.Build(player,0,9,Color.white);
    var actor=new Actor{id=player.id,slot=9,action="run",velocity=new Point(0,3.5f)};
    for(int frame=0;frame<fps;frame++){actor.previous=actor.position;actor.position+=actor.velocity/fps;view.Render(actor,1,1f/fps);}
    var forearm=go.GetComponentsInChildren<Transform>().Single(t=>t.name=="lowerarm01.L");
    var outgoing=forearm.localRotation;
    float bendBefore=Quaternion.Angle(outgoing,Quaternion.identity);
    Assert.Greater(bendBefore,15,"Fixture must actually contain a bent running elbow; otherwise no jump can be assessed");
    float height=kind==MatchSimulation.ChestControl?1.35f:.95f;
    var impact=new Point(actor.position.x+.23f,actor.position.z+.30f);
    var state=new MatchState{home="h",away="a",restart=0,phase="play",engineVersion=4,homeTactic=new Tactic(),awayTactic=new Tactic(),actors=new[]{actor},ball=new BallState{kind="pass",position=impact,height=height}};
    var sim=new MatchSimulation(new Database{players=new[]{player}},state);
    // Invoke the exact Core event that sets action/time/body kind and target.
    // This isolated event is not a full-match or biomechanical validation.
    typeof(MatchSimulation).GetMethod("Control",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(sim,new object[]{actor});
    Assert.AreEqual(kind,actor.actionKind);Assert.AreEqual("control",actor.action);
    Assert.AreEqual(MatchSimulation.BodyControlDuration(kind),actor.actionTime,.0001f);
    Assert.AreEqual(0,state.ball.controlElapsed);Assert.AreEqual(impact,actor.actionTarget);
    view.Render(actor,1,1f/fps,new Vector3(impact.x,height,impact.z));
    float jump=Quaternion.Angle(outgoing,forearm.localRotation);
    TestContext.WriteLine($"{kind} {fps}fps running elbow={bendBefore:0.000}deg, entry jump={jump:0.000}deg, endpoint={Quaternion.Angle(forearm.localRotation,Quaternion.identity):0.000}deg");
    Assert.Less(jump,8,"At zero progression the reception overlay must not erase the actual outgoing forearm bend after the transition blend");
   }finally{UnityEngine.Object.DestroyImmediate(go);}
  }
 }
}


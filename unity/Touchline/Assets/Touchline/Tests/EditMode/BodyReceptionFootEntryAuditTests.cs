using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Touchline.Core;
using UnityEngine;
namespace Touchline.Tests {
 public class BodyReceptionFootEntryAuditTests {
  [TestCase(MatchSimulation.ChestControl,30)][TestCase(MatchSimulation.ChestControl,60)][TestCase(MatchSimulation.ChestControl,120)]
  [TestCase(MatchSimulation.ThighControl,30)][TestCase(MatchSimulation.ThighControl,60)][TestCase(MatchSimulation.ThighControl,120)]
  public void CoreGeneratedZeroProgressReceptionPreservesBothOutgoingAnkles(string kind,int fps){
   var go=new GameObject("Core reception ankle entry audit");try{
    var player=new PlayerData{id="audit-foot-entry",heightCm=182,rating=75};var view=go.AddComponent<PlayerView>();view.Build(player,0,9,Color.white);
    var actor=new Actor{id=player.id,slot=9,action="run",velocity=new Point(0,3.5f)};
    for(int frame=0;frame<fps;frame++){actor.previous=actor.position;actor.position+=actor.velocity/fps;view.Render(actor,1,1f/fps);}
    var before=new[]{view.FootPosition(true),view.FootPosition(false)};float height=kind==MatchSimulation.ChestControl?1.35f:.95f;var impact=new Point(actor.position.x+.23f,actor.position.z+.30f);
    var state=new MatchState{home="h",away="a",restart=0,phase="play",engineVersion=4,homeTactic=new Tactic(),awayTactic=new Tactic(),actors=new[]{actor},ball=new BallState{kind="pass",position=impact,height=height}};
    var sim=new MatchSimulation(new Database{players=new[]{player}},state);typeof(MatchSimulation).GetMethod("Control",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(sim,new object[]{actor});
    Assert.AreEqual(kind,actor.actionKind);Assert.AreEqual(MatchSimulation.BodyControlDuration(kind),actor.actionTime,.0001f);
    string saved=JsonUtility.ToJson(actor);view.Render(actor,1,1f/fps,new Vector3(impact.x,height,impact.z));Assert.AreEqual(saved,JsonUtility.ToJson(actor));
    var after=new[]{view.FootPosition(true),view.FootPosition(false)};
    for(int i=0;i<2;i++){float jump=Vector3.Distance(before[i],after[i]);TestContext.WriteLine($"{kind} {fps}fps foot{i} before={before[i]} after={after[i]} entryJump={jump:0.000000}m");Assert.Less(jump,.04f,"At physical progression zero, the body control must not reposition an outgoing ankle");}
   }finally{UnityEngine.Object.DestroyImmediate(go);}
  }
 }
}

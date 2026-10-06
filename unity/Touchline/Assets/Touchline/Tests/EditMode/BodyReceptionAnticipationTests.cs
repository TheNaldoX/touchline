using System.Reflection;
using NUnit.Framework;
using Touchline.Core;
using UnityEngine;
namespace Touchline.Tests
{
 public class BodyReceptionAnticipationTests
 {
  static MatchState Fixture(float height=.95f){var actor=new Actor{id="r",slot=9,side=0,action="idle",angle=0};return new MatchState{home="h",away="a",engineVersion=4,homeTactic=new Tactic(),awayTactic=new Tactic(),phase="play",restart=0,actors=new[]{actor},ball=new BallState{kind="pass",from="s",to="r",side=0,elapsed=.5f,duration=1,start=new Point(0,2.8f),end=new Point(0,.3f),height=height,startHeight=height,endHeight=height,loft=0}};}
  [TestCase(1.8f,.95f,MatchSimulation.ThighControl)]
  [TestCase(1.8f,1.35f,MatchSimulation.ChestControl)]
  [TestCase(2.1f,1.25f,MatchSimulation.ThighControl)]
  public void ObservedPassUsesBodyControlThresholdsWithoutChangingState(float stature,float height,string kind){
   var m=Fixture(height);m.ball.elapsed=.7f;string before=JsonUtility.ToJson(m);var sample=BodyReceptionAnticipationSample.From(m,m.actors[0],1,stature);Assert.True(sample.active);Assert.AreEqual(kind,sample.kind);Assert.That(sample.eta,Is.InRange(0,.35f));Assert.AreEqual(before,JsonUtility.ToJson(m));
  }
  [TestCase("loose")][TestCase("shot")][TestCase("throw")]
  public void OtherFlightsNeverPreparePossession(string kind){var m=Fixture();m.ball.kind=kind;Assert.False(BodyReceptionAnticipationSample.From(m,m.actors[0],1,1.8f).active);}
  [TestCase(.65f)][TestCase(1.6f)]
  public void OutsideBodyReceptionHeightDoesNotActivate(float height){var m=Fixture(height);m.ball.elapsed=.7f;Assert.False(BodyReceptionAnticipationSample.From(m,m.actors[0],1,1.8f).active);}
  [Test]public void DestinationMustBeThisPlayerAndApproachMustBeImminent(){var m=Fixture();Assert.False(BodyReceptionAnticipationSample.From(m,m.actors[0],1,1.8f).active);m.ball.elapsed=.7f;m.ball.to="someoneElse";Assert.False(BodyReceptionAnticipationSample.From(m,m.actors[0],1,1.8f).active);}
  [Test]public void NearbyOpponentCancelsPreview(){var m=Fixture();m.ball.elapsed=.7f;m.actors=new[]{m.actors[0],new Actor{id="o",side=1,slot=4,position=new Point(.8f,0)}};Assert.False(BodyReceptionAnticipationSample.From(m,m.actors[0],1,1.8f).active);}
  [TestCase(false)][TestCase(true)]
  public void ChangingReceivingSideOrSourceDoesNotFreezeWrongPreparation(bool changeSource){
   var go=new GameObject("Opposite reception handoff audit");try{
    var data=new PlayerData{id="r",heightCm=182,rating=75};var view=go.AddComponent<PlayerView>();view.Build(data,0,9,Color.white);var m=Fixture();var actor=m.actors[0];m.ball.start=new Point(.23f,2.8f);m.ball.end=new Point(.23f,.3f);view.Render(actor,1,0);
    for(int i=0;i<23;i++){m.ball.elapsed=.6f+i/60f;m.ball.position=Point.Lerp(m.ball.start,m.ball.end,m.ball.elapsed);view.Render(actor,1,1f/60,new Vector3(m.ball.position.x,.95f,m.ball.position.z),new PlayerMotionContext{bodyReception=BodyReceptionAnticipationSample.From(m,actor,1,1.82f)});}
    var binding=BindingFlags.Instance|BindingFlags.NonPublic;
    Assert.Greater((float)typeof(PlayerView).GetField("receptionPreparationWeight",binding).GetValue(view),.5f);
    if(changeSource)m.ball.from="different-player";else{m.ball.position=new Point(-.12f,.30f);}
    var sim=new MatchSimulation(new Database{players=new[]{data}},m);typeof(MatchSimulation).GetMethod("Control",binding).Invoke(sim,new object[]{actor});
    view.Render(actor,1,1f/60,new Vector3(actor.actionTarget.x,actor.actionHeight,actor.actionTarget.z),new PlayerMotionContext{carrying=true,bodyReception=BodyReceptionAnticipationSample.From(m,actor,1,1.82f)});
    Assert.False((bool)typeof(PlayerView).GetField("receptionPreparedHandoff",binding).GetValue(view));
   }finally{Object.DestroyImmediate(go);}
  }
  [TestCase(30)][TestCase(60)][TestCase(120)]
  public void PreparatoryThighLiftIsGradualAndPreservesAuthoritativeContact(int fps){
   var go=new GameObject("Reception anticipation full pipeline audit");try{
    var data=new PlayerData{id="r",heightCm=182,rating=75};var view=go.AddComponent<PlayerView>();view.Build(data,0,9,Color.white);var actor=new Actor{id="r",slot=9,action="idle",angle=0};var m=Fixture();m.actors=new[]{actor};
    m.ball.start=new Point(.23f,2.8f);m.ball.end=new Point(.23f,.3f);m.ball.elapsed=.60f;
    view.Render(actor,1,0,new Vector3(.23f,.95f,1.3f));float maxStep=0;Vector3 last=view.FootPosition(true);
    for(int frame=1;frame<=Mathf.CeilToInt(.36f*fps);frame++){
     float t=Mathf.Min(.36f,(float)frame/fps);m.ball.elapsed=.60f+t;m.clock=t;m.ball.position=Point.Lerp(m.ball.start,m.ball.end,m.ball.elapsed);m.ball.height=.95f;
     var sample=BodyReceptionAnticipationSample.From(m,actor,1,1.82f);string before=JsonUtility.ToJson(m);
     view.Render(actor,1,1f/fps,new Vector3(m.ball.position.x,m.ball.height,m.ball.position.z),new PlayerMotionContext{bodyReception=sample,hasSimulationClock=true,simulationClock=t});
     Assert.AreEqual(before,JsonUtility.ToJson(m));var foot=view.FootPosition(true);maxStep=Mathf.Max(maxStep,Vector3.Distance(last,foot));last=foot;
    }
    var sim=new MatchSimulation(new Database{players=new[]{data}},m);typeof(MatchSimulation).GetMethod("Control",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(sim,new object[]{actor});
    var contact=new Vector3(actor.actionTarget.x,actor.actionHeight,actor.actionTarget.z);Assert.AreEqual(MatchSimulation.ThighControl,actor.actionKind);
    for(int frame=0;frame<=Mathf.CeilToInt(.08f*fps);frame++){
     float elapsed=Mathf.Min(.08f,(float)frame/fps);actor.actionTime=MatchSimulation.BodyControlDuration(actor.actionKind)-elapsed;
     var sample=BodyReceptionAnticipationSample.From(m,actor,1,1.82f);string before=JsonUtility.ToJson(m);view.Render(actor,1,1f/fps,contact,new PlayerMotionContext{bodyReception=sample,carrying=true});Assert.AreEqual(before,JsonUtility.ToJson(m));
     var foot=view.FootPosition(true);float delta=Vector3.Distance(last,foot);if(frame==0)Assert.Less(delta,.04f,"Real zero-progress event must preserve outgoing readiness");maxStep=Mathf.Max(maxStep,delta);last=foot;
    }
    float distance=Vector3.Distance(view.ThighContactPosition(true),contact);TestContext.WriteLine($"{fps}fps maxStep={maxStep:F6} contactDistance={distance:F6}");Assert.Less(distance,.11f);Assert.Less(maxStep,.03f+4f/fps,"Preparation must spread the former half-metre one-frame lift");
   }finally{Object.DestroyImmediate(go);}
  }
 }
}

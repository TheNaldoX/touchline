using NUnit.Framework;
using Touchline.Core;
using UnityEngine;
namespace Touchline.Tests
{
 public class BodyReceptionSweptPreviewTests
 {
  static MatchState GrazingFastPass(float elapsed){var actor=new Actor{id="receiver",slot=9,side=0,action="idle",angle=0};return new MatchState{phase="play",restart=0,actors=new[]{actor},ball=new BallState{kind="pass",from="passer",to=actor.id,side=0,elapsed=elapsed,duration=.3f,start=new Point(.23f,3.3f),end=new Point(.23f,-2.7f),startHeight=.95f,endHeight=.95f,height=.95f}};}
  [Test]public void FastPassCrossingTheReceptionEnvelopeBetweenSamplesStillPrepares(){
   var m=GrazingFastPass(.135f);string before=JsonUtility.ToJson(m);
   var sample=BodyReceptionAnticipationSample.From(m,m.actors[0],1,1.8f);
   Assert.True(sample.active,"The20m/s pass crosses the26cm Core reception envelope between25ms probes.");
   Assert.That(sample.eta,Is.InRange(.008f,.010f));Assert.AreEqual(MatchSimulation.ThighControl,sample.kind);Assert.AreEqual(before,JsonUtility.ToJson(m));
  }
  [TestCase(30)][TestCase(60)][TestCase(120)]
  public void StableIncomingFlightDoesNotFlickerWithRenderSamplePhase(int fps){
   for(int frame=0;.10f+(float)frame/fps<=.1401f;frame++){
    float elapsed=.10f+(float)frame/fps;var m=GrazingFastPass(elapsed);var sample=BodyReceptionAnticipationSample.From(m,m.actors[0],1,1.8f);
    TestContext.WriteLine($"fps={fps} elapsed={elapsed:F6} active={sample.active} eta={sample.eta:F6}");
    Assert.True(sample.active,"A continuously approaching pass must not lose readiness solely because the render sample phase changed.");
   }
  }
  [Test]public void PassOutsideBodyWidthStillDoesNotPrepare(){var m=GrazingFastPass(.135f);m.ball.start.x=m.ball.end.x=.261f;Assert.False(BodyReceptionAnticipationSample.From(m,m.actors[0],1,1.8f).active);}
 }
}

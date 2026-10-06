using System;
using System.Collections.Generic;
using NUnit.Framework;
using Touchline.Core;
namespace Touchline.Tests {
 public class ExitLifecycleTests {
  static ExitPresentationContact Fixture(out MatchState m){
   m=new MatchState{clock=100,phase="play",events=new List<MatchEvent>(),ball=new BallState{previous=new Point(52,5),previousHeight=1,position=new Point(53,5),height=1,velocity=new Point(25,0)}};
   var c=ExitPresentationContact.Capture(m,.5f,new Point(52.61f,5),1,"goal-kick",1,new Point(47,0));
   m.phase="goal-kick";m.restart=28;m.restartSide=1;m.ball=new BallState{position=c.restartSpot};m.events.Add(new MatchEvent{kind=c.kind,time=m.clock,side=1,position=c.restartSpot});
   Assert.That(ExitPresentationContact.BindRestartBall(ref c,m),Is.True);return c;
  }
  [Test]public void VisibleExitReceivesOneBoundedTailOnly(){
   var c=Fixture(out var m);var window=new ExitBroadcastWindow();float until=window.Extend(c,m,false,99.99f);
   Assert.That(until,Is.EqualTo(100.7f));m.clock=105;
   Assert.That(window.Extend(c,m,false,until),Is.EqualTo(until),"Persistent trace must not keep refreshing the end window");
  }
  [Test]public void QuietExitDoesNotBecomeAHighlightEvenOnLaterRefresh(){
   var c=Fixture(out var m);var window=new ExitBroadcastWindow();
   Assert.That(window.Extend(c,m,true,99),Is.EqualTo(99));
   Assert.That(window.Extend(c,m,false,99),Is.EqualTo(99),"An already consumed quiet exit is not replayed");
  }
  [Test]public void ExistingLongerShotWindowIsPreserved(){
   var c=Fixture(out var m);Assert.That(new ExitBroadcastWindow().Extend(c,m,false,120),Is.EqualTo(120));
  }
  [Test]public void QuietReturnResetsOutgoingBeforeItCanForceAnElapsedCut(){
   var c=Fixture(out var m);var view=new ExitRestartPresentation();var id=new object();
   Assert.That(view.TrySample(id,m,c,.8f,out var outgoing),Is.True);Assert.That(outgoing.repositioned,Is.False);
   // MatchArena resets on return from the statistics panel before sampling.
   view.Reset();m.clock=105;
   Assert.That(view.TrySample(id,m,c,1,out var resumed),Is.True);
   Assert.That(resumed.repositioned,Is.True);Assert.That(resumed.opacity,Is.Zero);Assert.That(resumed.ballVisible,Is.True);
   Assert.That(Point.Distance(resumed.pose.position,m.ball.position),Is.LessThan(.0001f));
  }
 }
}

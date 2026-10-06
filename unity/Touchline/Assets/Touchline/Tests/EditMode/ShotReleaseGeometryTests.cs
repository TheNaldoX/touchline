using System;
using System.Reflection;
using System.Collections.Generic;
using Touchline.Core;
public static class ShotReleaseCandidateChecks {
 static readonly MethodInfo Update=typeof(MatchSimulation).GetMethod("UpdateBall",BindingFlags.Instance|BindingFlags.NonPublic);
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 static MatchSimulation Fixture(bool header=false,bool moving=false,bool fixedStart=false,float height=.11f,float endHeight=.11f,float loft=0){
  var kicker=new Actor{id="kicker",side=0,slot=9,action=header?"header":"kick",actionSequence=1,controlTime=100};
  var defender=new Actor{id="defender",side=1,slot=5,previous=new Point(moving?-.3f:0,1.1f),position=new Point(moving?.5f:0,1.1f)};
  float delay=header?.12f:.18f;
  var b=new BallState{kind="shot",from=kicker.id,side=0,offsidePlayersMask=1<<22,position=new Point(0,header?.4f:.36666667f),previous=new Point(0,header?.4f:.36666667f),height=height,previousHeight=height,setupHeight=height,start=new Point(0,.42f),startHeight=height,end=new Point(0,10),endHeight=endHeight,elapsed=header?-.02f:-.08f,releaseDelay=delay,duration=.5f,loft=loft,fixedStart=fixedStart};
  return new MatchSimulation(new Database{players=new[]{new PlayerData{id=kicker.id,rating=75,heightCm=182},new PlayerData{id=defender.id,rating=75,heightCm=182}}},new MatchState{home="h",away="a",phase="play",restart=0,engineVersion=4,homeTactic=new Tactic(),awayTactic=new Tactic(),actors=new[]{kicker,defender},ball=b});
 }
 static float ImpactTime(BallReleaseContact r,Point impact){float u=Point.Dot(impact-r.release,r.flightEnd-r.release)/Point.Dot(r.flightEnd-r.release,r.flightEnd-r.release);return r.fraction+u*r.duration/MatchSimulation.Step;}
 public static object[] Run(){var report=new List<object>();
  foreach(bool header in new[]{false,true})foreach(bool fixedStart in new[]{false,true}){
   var sim=Fixture(header,false,fixedStart);Update.Invoke(sim,null);var r=sim.ReleaseContact;float f=ImpactTime(r,sim.State.ball.position);
   Check(sim.State.actors[1].action=="block","Real later contact must be retained");Check(f>=r.fraction&&f<=1,"No contact before launch");Check(Math.Abs(sim.State.ball.position.z-.54f)<.0001f,"Contact must remain on the real .56m sphere");
   report.Add(new{caseName="later-real-contact",header,fixedStart,release=r.fraction,impact=f,pass=true});
  }
  {
   var sim=Fixture(false,true);Update.Invoke(sim,null);Check(sim.State.actors[1].action!="block"&&sim.State.ball.kind=="shot","Moving-away false block must disappear");Check(Math.Abs(sim.State.ball.velocity.z-19.16f)<.0001f,"Initial outgoing speed must not include preparation displacement");report.Add(new{caseName="moving-away-no-contact",pass=true});
  }
  {
   var sim=Fixture(true,false,false,1.84f,1.84f,.6f);sim.State.ball.duration=.1f;Update.Invoke(sim,null);Check(sim.State.actors[1].action!="block","Actual lofted height is above the defender, though previous-end interpolation would allow a block");report.Add(new{caseName="true-height-rejects-false-body-block",pass=true});
  }
  {
   var sim=Fixture();sim.State.actors[1].sentOff=true;sim.State.ball.loft=.1f;Update.Invoke(sim,null);var b=sim.State.ball;float expected=(float)Math.Cos(Math.PI*b.elapsed/b.duration)*(float)Math.PI*b.loft/b.duration;Check(Math.Abs(b.velocity.z-19.16f)<.0001f&&Math.Abs(b.verticalVelocity-expected)<.0001f,"True launch derivative required");float firstZ=b.velocity.z,firstY=b.verticalVelocity;
   b.previous=b.position;b.previousHeight=b.height;float previousZ=b.position.z,previousY=b.height;Update.Invoke(sim,null);Check(Math.Abs(b.velocity.z-(b.position.z-previousZ)/MatchSimulation.Step)<.0001f&&Math.Abs(b.verticalVelocity-(b.height-previousY)/MatchSimulation.Step)<.0001f,"Later velocity convention remains unchanged");
   report.Add(new{caseName="first-derivative-and-next-step-convention",firstHorizontal=firstZ,firstVertical=firstY,pass=true});
  }
  {
   var sim=Fixture(false,false,true);var b=sim.State.ball;b.previous=b.position=new Point(0,33.8f);b.start=new Point(0,33.8f);b.end=new Point(0,63.8f);b.duration=.5f;sim.State.actors[0].position=sim.State.actors[0].previous=new Point(0,33.38f);sim.State.actors[1].position=sim.State.actors[1].previous=new Point(0,35);
   Update.Invoke(sim,null);Check(sim.State.actors[1].action!="block","Exit before late external body must win");Check(sim.State.restart>0&&sim.State.phase=="throw-in","Ball crossing the sideline still restarts");report.Add(new{caseName="exit-before-external-contact",pass=true});
  }
  {
   var sim=Fixture(true,true);Update.Invoke(sim,null);var r=sim.ReleaseContact;float f=ImpactTime(r,sim.State.ball.position);var hit=sim.State.actors[1];var center=Point.Lerp(hit.previous,hit.position,f);
   Check(hit.action=="block"&&center.x<0,"The real earlier body center must be on the negative-X side");Check(sim.State.ball.velocity.x>0,"The deflection must point away from the body's actual contact side, not its future side");report.Add(new{caseName="moving-body-contact-normal",impact=f,centerX=center.x,deflectionX=sim.State.ball.velocity.x,pass=true});
  }
  return report.ToArray();
 }
}



namespace Touchline.Tests {
 public class ShotReleaseGeometryTests {
  [NUnit.Framework.Test] public void FirstFlightRespectsPhysicalReleaseAndRelativeContactGeometry(){
   var cases=ShotReleaseCandidateChecks.Run();
   NUnit.Framework.Assert.AreEqual(9,cases.Length);
   foreach(var item in cases)NUnit.Framework.TestContext.WriteLine(item.ToString());
  }
 }
}

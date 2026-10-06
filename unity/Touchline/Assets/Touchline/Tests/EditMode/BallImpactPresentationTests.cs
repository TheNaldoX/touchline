using NUnit.Framework;
using UnityEngine;
using Touchline.Core;
namespace Touchline.Tests {
 public class BallImpactPresentationTests {
  static BallReleaseContact Release()=>new BallReleaseContact{valid=true,kind="shot",source="kicker",clock=10,fraction=.8f,previous=new Point(0,.3f),release=new Point(0,.42f),end=new Point(0,.82f),flightEnd=new Point(0,10.42f),previousHeight=.11f,releaseHeight=.11f,endHeight=.11f,flightEndHeight=.11f,duration=.5f};
  static MatchState State()=>new MatchState{clock=10,restart=0,ball=new BallState{kind="loose",position=new Point(0,.62f),previous=new Point(0,.3f),height=.11f,previousHeight=.11f}};
  [TestCase(false)][TestCase(true)]
  public void CollisionKeepsPreparationAndActualFlightUntilImpact(bool keeper){
   var release=Release();var state=State();var block=new BallImpactContact{valid=true,clock=10,fraction=.9f,start=release.previous,startHeight=.11f,impact=state.ball.position,height=.11f};
   var save=new KeeperPresentationContact{valid=true,caught=false,clock=10,fraction=.9f,start=release.previous,startHeight=.11f,impact=state.ball.position,height=.11f};
   for(int frame=0;frame<=100;frame++){
    float alpha=frame*.01f;
    var point=keeper?KeeperBallPresentation.Position(save,state,alpha,release:release):BallImpactPresentation.Position(block,state,alpha,release);
    float expected=alpha<.8f?Mathf.Lerp(.3f,.42f,alpha/.8f):alpha<.9f?.42f+(alpha-.8f)*2:.62f;
    Assert.AreEqual(expected,point.z,.00001f,"alpha="+alpha);Assert.AreEqual(.11f,point.y,.00001f);
   }
  }
  [Test] public void OutfieldBlockDoesNotStretchIncomingMotionOverTheWholeTick(){
   var state=State();var contact=new BallImpactContact{valid=true,clock=10,fraction=.25f,start=new Point(0,0),startHeight=.11f,impact=new Point(0,1),height=.11f};
   Assert.AreEqual(.5f,BallImpactPresentation.Position(contact,state,.125f).z,.00001f);
   Assert.AreEqual(1,BallImpactPresentation.Position(contact,state,.75f).z,.00001f);
  }
  [TestCase(false)][TestCase(true)]
  public void OldOrRestartedImpactCannotOverrideTheNewBallState(bool restart){
   var state=State();var contact=new BallImpactContact{valid=true,clock=restart?10:9,fraction=.25f,impact=new Point(0,99)};if(restart)state.restart=1;
   Assert.IsFalse(BallImpactPresentation.Current(contact,state));Assert.AreEqual(.46f,BallImpactPresentation.Position(contact,state,.5f).z,.00001f);
  }
 }
}

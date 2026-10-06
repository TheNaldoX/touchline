using System.Reflection;
using NUnit.Framework;
using Touchline.Core;
using UnityEngine;
namespace Touchline.Tests
{
 public class HeaderFacingContinuityTests
 {
  [Test]public void ContactFeasibilityDoesNotAdvanceFacingOrAngularVelocity(){
   var facing=new LocomotionFacing();facing.Sample(170,3.5f,0,true);facing.Sample(130,3.5f,.1f);float angle=facing.Angle,velocity=facing.Velocity;
   Assert.True(facing.CanReachHeading(101.35f,0,.26f));Assert.False(facing.CanReachHeading(0,0,.02f));Assert.AreEqual(angle,facing.Angle);Assert.AreEqual(velocity,facing.Velocity);
  }
  [TestCase(30)][TestCase(60)][TestCase(120)]
  public void ImpossibleHalfTurnPreservesContactFacingFallbackInsteadOfMissingTheAction(int fps){
   var go=new GameObject("Header with large existing facing lag");try{
    var data=new PlayerData{id="extreme",name="Header",heightCm=182,rating=75};var view=go.AddComponent<PlayerView>();view.Build(data,0,9,Color.white);var actor=new Actor{id=data.id,slot=9,side=0,action="idle",angle=170*Mathf.Deg2Rad};view.Render(actor,1,0);
    actor.angle=-10*Mathf.Deg2Rad;var center=actor.position+new Point(Mathf.Sin(actor.angle),Mathf.Cos(actor.angle))*.25f;var target=new Vector3(center.x,1.94f,center.z);
    var state=new MatchState{home="h",away="a",engineVersion=4,homeTactic=new Tactic(),awayTactic=new Tactic(),phase="play",restart=0,actors=new[]{actor},ball=new BallState{kind="cross",position=new Point(target.x,target.z),previous=new Point(target.x,target.z),height=target.y,previousHeight=target.y,owner=actor.id}};
    var sim=new MatchSimulation(new Database{players=new[]{data}},state);float fraction=(float)typeof(MatchSimulation).GetMethod("HeaderContactFraction",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(sim,new object[]{actor});Assert.LessOrEqual(fraction,1,"The fallback fixture must place the ball inside the actual Core heading envelope.");typeof(MatchSimulation).GetMethod("DistributeHeader",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(sim,new object[]{actor,null});var expected=view.transform.rotation;
    for(int frame=0;frame<=Mathf.CeilToInt(.22f*fps);frame++){
     float elapsed=Mathf.Min(.22f,(float)frame/fps),step=Mathf.Floor(elapsed/MatchSimulation.Step),alpha=(elapsed-step*MatchSimulation.Step)/MatchSimulation.Step;actor.actionTime=.64f-step*MatchSimulation.Step;
     expected=Quaternion.Slerp(expected,Quaternion.Euler(0,-10,0),1-Mathf.Exp(-16f/fps));view.Render(actor,alpha,1f/fps,target);
     Assert.Less(Quaternion.Angle(expected,view.transform.rotation),.05f,"An unreachable preparatory turn must keep the existing contact-facing fallback.");
    }
    float error=Vector3.Distance(view.HeaderContactPosition,target);TestContext.WriteLine($"{fps}fps extremeContactError={error:F6}");Assert.Less(error,.12f);
   }finally{Object.DestroyImmediate(go);}
  }
  [TestCase(30)][TestCase(60)][TestCase(120)]
  public void EnteringHeaderKeepsAngularMotionBoundedAndReachesContact(int fps){
   var go=new GameObject("Running turn into real header");try{
    var data=new PlayerData{id="h",name="Header",heightCm=182,rating=75};var view=go.AddComponent<PlayerView>();view.Build(data,0,9,Color.white);
    var actor=new Actor{id="h",slot=9,side=0,action="run",angle=170*Mathf.Deg2Rad,velocity=new Point(0,3.5f)};view.Render(actor,1,0);
    actor.angle=130*Mathf.Deg2Rad;for(int frame=0;frame<fps/10;frame++){actor.previous=actor.position;actor.position+=actor.velocity/fps;view.Render(actor,1,1f/fps);}
    float yaw=view.transform.eulerAngles.y;var target=view.HeaderContactPosition+Vector3.up*.12f;
    actor.angle=101.35f*Mathf.Deg2Rad;actor.velocity=new Point();actor.previous=actor.position;var state=new MatchState{home="h",away="a",engineVersion=4,homeTactic=new Tactic(),awayTactic=new Tactic(),phase="play",restart=0,actors=new[]{actor},ball=new BallState{kind="cross",position=new Point(target.x,target.z),previous=new Point(target.x,target.z),height=target.y,previousHeight=target.y,owner=actor.id}};
    var sim=new MatchSimulation(new Database{players=new[]{data}},state);typeof(MatchSimulation).GetMethod("DistributeHeader",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(sim,new object[]{actor,null});
    float maximum=0;
    for(int frame=0;frame<=Mathf.CeilToInt(.22f*fps);frame++){
     float elapsed=Mathf.Min(.22f,(float)frame/fps),step=Mathf.Floor(elapsed/MatchSimulation.Step),alpha=(elapsed-step*MatchSimulation.Step)/MatchSimulation.Step;actor.actionTime=.64f-step*MatchSimulation.Step;
     string before=JsonUtility.ToJson(actor);view.Render(actor,alpha,1f/fps,target);Assert.AreEqual(before,JsonUtility.ToJson(actor));
     float turn=Mathf.Abs(Mathf.DeltaAngle(yaw,view.transform.eulerAngles.y));maximum=Mathf.Max(maximum,turn);yaw=view.transform.eulerAngles.y;
     if(frame==0)TestContext.WriteLine($"{fps}fps firstHeaderTurn={turn:F6}");
     Assert.LessOrEqual(turn,360f/fps+.10f,"A header must not introduce an angular-velocity burst at the locomotion/action boundary.");
    }
    float headingError=Mathf.Abs(Mathf.DeltaAngle(yaw,actor.angle*Mathf.Rad2Deg));float contactError=Vector3.Distance(view.HeaderContactPosition,target);
    TestContext.WriteLine($"{fps}fps maxTurn={maximum:F6} headingError={headingError:F6} contactError={contactError:F6}");Assert.Less(headingError,3);Assert.Less(contactError,.12f);
   }finally{Object.DestroyImmediate(go);}
  }
 }
}

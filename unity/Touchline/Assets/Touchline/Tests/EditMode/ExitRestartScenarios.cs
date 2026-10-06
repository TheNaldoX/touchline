using System;
using System.Collections.Generic;
using System.Reflection;
using Touchline.Core;
namespace Touchline.Tests {
 public static class ExitRestartScenarios {
  static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
  static MatchState Fixture(string kind="goal-kick",int sign=1){
   return new MatchState{home="a",away="b",clock=100,score=new[]{0,0},periodSeconds=2700,phase="play",events=new List<MatchEvent>(),ball=new BallState{previous=new Point(sign*51.8f,5),previousHeight=1.6f,position=new Point(sign*53.1f,5),height=1.9f,velocity=new Point(sign*24,1),verticalVelocity=2,kind="shot",from="striker"}};
  }
  static ExitPresentationContact Bind(MatchState m,string kind="goal-kick",int sign=1){
   var impact=kind=="throw-in"?new Point(20,sign*34.11f):new Point(sign*52.61f,5);
   var spot=kind=="throw-in"?new Point(20,sign*34):kind=="corner"?new Point(sign*52.5f,33.8f):new Point(sign*47,0);
   if(kind=="throw-in"){m.ball.previous=new Point(20,sign*33.4f);m.ball.position=new Point(20,sign*34.6f);m.ball.velocity=new Point(1,sign*24);}
   var c=ExitPresentationContact.Capture(m,.5f,impact,1.7f,kind,1,spot);
   Check(c.valid,"capture valid");m.phase=kind;m.restart=28;m.restartSide=1;m.ball=new BallState{position=spot,previous=spot};m.events.Add(new MatchEvent{kind=kind,time=m.clock,side=1,position=spot});
   Check(ExitPresentationContact.BindRestartBall(ref c,m),"bind restart identity");return c;
  }
  static ExitRestartSample Sample(ExitRestartPresentation view,object id,MatchState m,ExitPresentationContact c,float age){
   double absolute=c.clock-MatchSimulation.Step+c.fraction*MatchSimulation.Step+age;
   m.clock=(float)(Math.Ceiling(absolute*10-1e-7)/10);float alpha=1-(float)((m.clock-absolute)/MatchSimulation.Step);
   Check(view.TrySample(id,m,c,alpha,out var pose),"sample remains bound");return pose;
  }
  public static void ExactCrossingAndNoReturnFlight(string kind,int sign){
   var m=Fixture(kind,sign);var c=Bind(m,kind,sign);var v=new ExitRestartPresentation();var id=new object();
   Check(v.TrySample(id,m,c,.5f,out var cross),"crossing visible");Check(Point.Distance(cross.pose.position,c.impact)<.001f&&Math.Abs(cross.pose.height-c.height)<.001f,"exact accepted boundary");
   var oldBall=m.ball;uint seed=m.seed;int count=m.events.Count;
   for(int i=0;i<121;i++){
    var p=Sample(v,id,m,c,i/120f);
    Check(p.pose.height>=MatchSimulation.BallRadius-.0001f,"no underground continuation");
    Check(Math.Abs(p.pose.position.x)<=58.0001f&&Math.Abs(p.pose.position.z)<=37.0001f,"never enters spectators");
    if(p.repositioned)Check(Point.Distance(p.pose.position,c.restartSpot)<.0001f,"replacement is at true spot, never on artificial retrieval arc");
    else Check(Point.Dot(p.pose.position-c.impact,c.velocity)>=-.001f,"outgoing never travels back toward replacement");
   }
   Check(ReferenceEquals(oldBall,m.ball)&&seed==m.seed&&count==m.events.Count,"presentation must not mutate core");
  }
  public static void OpaqueSwitchAtEveryCadence(int fps,int speed){
   var m=Fixture();var c=Bind(m);var v=new ExitRestartPresentation();var id=new object();
   Sample(v,id,m,c,0);bool switched=false;
   for(int f=1;f<=fps;f++){
    var s=Sample(v,id,m,c,f*speed/(float)fps);
    if(s.repositioned&&!switched){Check(s.opacity==1&&!s.ballVisible,"first changed scene must be covered by an opaque frame");switched=true;}
   }
   Check(switched,"cadence actually reached restart");
  }
  public static void PauseSkipRollbackAndIdentity(){
   var m=Fixture();var c=Bind(m);var v=new ExitRestartPresentation();var id=new object();
   var before=Sample(v,id,m,c,.1f);var paused=Sample(v,id,m,c,.1f);Check(Point.Distance(before.pose.position,paused.pose.position)<.0001f&&before.opacity==paused.opacity,"pause stable");
   var covered=Sample(v,id,m,c,1.0f);Check(covered.opacity==1,"skipped cut is covered when outgoing was seen");
   var pausedCut=Sample(v,id,m,c,1.0f);Check(pausedCut.opacity==1,"pause does not advance broadcast cut");
   var after=Sample(v,id,m,c,1.1f);Check(after.opacity==0,"resuming advances past old cut without a timer lock");
   var late=Sample(new ExitRestartPresentation(),new object(),m,c,2);Check(late.opacity==0&&late.repositioned,"new viewer must not replay elapsed cut");
   m.clock=100;Check(!v.TrySample(id,m,c,1,out _),"true clock rollback invalidates trace");
   m.clock=103;Check(!v.TrySample(id,m,c,1,out _),"rollback cannot relatch stale event");
   Check(new ExitRestartPresentation().TrySample(new object(),m,c,1,out _),"new simulation identity may show current restart");
   m.ball=new BallState{position=c.restartSpot};Check(!ExitPresentationContact.MatchesRestart(c,m),"same-side replacement ball cannot reuse old trace");
  }
  public static void PreparationAndReleaseInvalidateWithoutDelay(){
   var m=Fixture();var c=Bind(m);var v=new ExitRestartPresentation();var id=new object();Sample(v,id,m,c,.03f);
   m.phase="play";m.restart=0;m.ball.kind="pass";Check(!v.TrySample(id,m,c,1,out _),"real release/preparation overrides presentation immediately");
   m=Fixture();c=Bind(m);m.restart=60;m.clock=110;Check(ExitPresentationContact.MatchesRestart(c,m),"readiness delay retains event identity without fixed timeout");
   m.events[c.eventIndex].time+=.1f;Check(!ExitPresentationContact.MatchesRestart(c,m),"different event cannot reuse trace");
  }
  public static void FirstStepPreparationUsesGlobalFraction(){
   var m=Fixture();m.ball.elapsed=.02f;m.ball.previous=new Point(52,5);m.ball.previousHeight=.11f;m.ball.position=new Point(52.62f,5);m.ball.height=.11f;
   var r=new BallReleaseContact{valid=true,clock=100,kind="shot",source="striker",fraction=.8f,previous=m.ball.previous,previousHeight=.11f,release=new Point(52.6f,5),releaseHeight=.11f,end=m.ball.position,endHeight=.11f,flightEnd=new Point(53.6f,5),flightEndHeight=.11f,duration=1};
   var c=ExitPresentationContact.Capture(m,.9f,new Point(52.61f,5),.11f,"goal-kick",1,new Point(47,0),r);
   m.phase="goal-kick";m.restart=28;m.restartSide=1;m.ball=new BallState{position=new Point(47,0)};m.events.Add(new MatchEvent{kind="goal-kick",side=1,time=100,position=m.ball.position});Check(ExitPresentationContact.BindRestartBall(ref c,m),"first released bind");
   var v=new ExitRestartPresentation();var id=new object();Check(v.TrySample(id,m,c,.4f,out var p)&&Math.Abs(p.pose.position.x-52.3f)<.001f,"preparation before release must not become one full-step flight");
   Check(v.TrySample(id,m,c,.85f,out p)&&Math.Abs(p.pose.position.x-52.605f)<.001f,"global first-flight fraction");
   Check(v.TrySample(id,m,c,.9f,out p)&&Math.Abs(p.pose.position.x-52.61f)<.001f,"first-flight exact crossing");
  }
  public static void ActualBoundaryHook(bool firstRelease){
   var m=Fixture();m.engineVersion=4;m.actors=Array.Empty<Actor>();m.ball.lastTouch=0;
   var sim=new MatchSimulation(new Database{players=Array.Empty<PlayerData>()},m);
   if(firstRelease){
    m.ball.previous=new Point(52,5);m.ball.previousHeight=.11f;m.ball.position=new Point(52.62f,5);m.ball.height=.11f;m.ball.elapsed=.02f;
    var release=new BallReleaseContact{valid=true,clock=100,kind="shot",source="striker",fraction=.8f,previous=m.ball.previous,previousHeight=.11f,release=new Point(52.6f,5),releaseHeight=.11f,end=m.ball.position,endHeight=.11f,flightEnd=new Point(53.6f,5),flightEndHeight=.11f,duration=1};
    typeof(MatchSimulation).GetProperty("ReleaseContact").SetValue(sim,release);
   }
   const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
   var old=m.ball;uint seed=m.seed;
   Check((bool)typeof(MatchSimulation).GetMethod("BallLeavesPitch",flags).Invoke(sim,null),"Core accepted boundary");
   typeof(MatchSimulation).GetMethod("MaintainExitContact",flags).Invoke(sim,null);
   var c=sim.ExitContact;Check(c.valid,"actual Core hook binds new restart identity");
   Check(c.kind=="goal-kick"&&c.side==1&&Point.Distance(c.restartSpot,new Point(47,0))<.0001f,"correct real restart");
   Check(Point.Distance(c.impact,new Point(52.61f,5))<.0001f,"actual exact crossing");
   Check(Math.Abs(c.fraction-(firstRelease?.9f:(52.61f-51.8f)/(53.1f-51.8f)))<.0005f,"fraction is global including release preparation");
   Check(seed==m.seed&&m.score[0]==0&&m.score[1]==0&&m.events.Count==1&&m.events[0].kind=="goal-kick"&&m.restart==28,"trace does not alter seed/result/events/duration");
   Check(!ReferenceEquals(old,m.ball)&&ReferenceEquals(c.restartBallIdentity,m.ball),"authoritative restart ball remains new exact identity");
   m.clock+=.1f;typeof(MatchSimulation).GetMethod("MaintainExitContact",flags).Invoke(sim,null);Check(sim.ExitContact.valid,"trace survives later restart ticks");
  }
  public static int Run(){int count=0;foreach(string kind in new[]{"goal-kick","throw-in","corner"})foreach(int side in new[]{-1,1}){ExactCrossingAndNoReturnFlight(kind,side);count++;}foreach(int fps in new[]{30,60,120})foreach(int speed in new[]{1,2,10}){OpaqueSwitchAtEveryCadence(fps,speed);count++;}PauseSkipRollbackAndIdentity();PreparationAndReleaseInvalidateWithoutDelay();FirstStepPreparationUsesGlobalFraction();ActualBoundaryHook(false);ActualBoundaryHook(true);return count+5;}
 }
}

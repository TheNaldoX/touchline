using System;
using Touchline.Core;
namespace Touchline {
 public struct ExitRestartSample {
  public GoalBallPose pose;public float opacity;public bool repositioned,ballVisible;public string caption;
 }
 // Short broadcast cut, not a simulated retrieval. Absolute match-clock sampling
 // never advances physics and never postpones the authoritative restart release.
 public sealed class ExitRestartPresentation {
  const float Gravity=9.81f,Fade=.12f,OpaqueHold=.08f;
  object identity;ExitPresentationContact contact;bool active,sawOutgoing,relocated;
  double lastTime=double.NaN,forcedCutTime=double.NaN;float lastClock=float.NaN,blockedClock=float.NaN;
  public void Reset(){identity=null;contact=default;active=sawOutgoing=relocated=false;lastTime=forcedCutTime=double.NaN;lastClock=blockedClock=float.NaN;}
  static bool Finite(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v);
  static bool Finite(Point p)=>Finite(p.x)&&Finite(p.z);
  static float Smooth(float t){t=Mathx.Clamp(t,0,1);return t*t*(3-2*t);}
  static bool Safe(ExitPresentationContact c){
   if(!Finite(c.clock)||!Finite(c.fraction)||c.fraction<0||c.fraction>1||!Finite(c.previous)||!Finite(c.previousHeight)||!Finite(c.impact)||!Finite(c.height)||!Finite(c.velocity)||!Finite(c.verticalVelocity)||c.height<0||Math.Abs(c.impact.x)>52.611f||Math.Abs(c.impact.z)>34.111f)return false;
   if(Math.Abs(c.impact.x)<52.609f&&Math.Abs(c.impact.z)<34.109f)return false;
   var r=c.release;return !r.valid||Finite(r.previous)&&Finite(r.previousHeight)&&Finite(r.release)&&Finite(r.releaseHeight)&&Finite(r.end)&&Finite(r.endHeight)&&Finite(r.flightEnd)&&Finite(r.flightEndHeight)&&Finite(r.fraction)&&r.fraction>=0&&r.fraction<=c.fraction&&Finite(r.duration)&&r.duration>=.1f&&Finite(r.loft);
  }
  static GoalBallPose Lerp(GoalBallPose a,GoalBallPose b,float t)=>new GoalBallPose(Point.Lerp(a.position,b.position,t),a.height+(b.height-a.height)*t);
  static GoalBallPose ReleasePath(BallReleaseContact r,float alpha){
   if(alpha<=0)return new GoalBallPose(r.previous,r.previousHeight);
   if(alpha<r.fraction)return Lerp(new GoalBallPose(r.previous,r.previousHeight),new GoalBallPose(r.release,r.releaseHeight),alpha/r.fraction);
   float u=Mathx.Clamp((alpha-r.fraction)*MatchSimulation.Step/r.duration,0,1);
   return new GoalBallPose(Point.Lerp(r.release,r.flightEnd,u),r.releaseHeight+(r.flightEndHeight-r.releaseHeight)*u+(float)Math.Sin(Math.PI*u)*r.loft);
  }
  static GoalBallPose Incoming(ExitPresentationContact c,float alpha){
   var r=c.release;
   if(r.valid){var at=ReleasePath(r,c.fraction);if(Point.Distance(at.position,c.impact)<.002f&&Math.Abs(at.height-c.height)<.002f)return ReleasePath(r,alpha);}
   return Lerp(new GoalBallPose(c.previous,c.previousHeight),new GoalBallPose(c.impact,c.height),c.fraction>0?Mathx.Clamp(alpha/c.fraction,0,1):1);
  }
  public static float CutAge(ExitPresentationContact c){
   // Finish the outgoing image before the ball reaches spectator geometry.
   // Grass contact also ends this simple airborne continuation; no fake bounce.
   float t=.36f;
   if(Math.Abs(c.velocity.x)>.0001f)t=Math.Min(t,((c.velocity.x>0?58:-58)-c.impact.x)/c.velocity.x);
   if(Math.Abs(c.velocity.z)>.0001f)t=Math.Min(t,((c.velocity.z>0?37:-37)-c.impact.z)/c.velocity.z);
   if(c.height>MatchSimulation.BallRadius+.001f||c.verticalVelocity>0){
    float h=Math.Max(0,c.height-MatchSimulation.BallRadius),vy=c.verticalVelocity;
    t=Math.Min(t,(vy+(float)Math.Sqrt(vy*vy+2*Gravity*h))/Gravity);
   }
   return Math.Max(0,t);
  }
  static GoalBallPose Outgoing(ExitPresentationContact c,float age){
   float t=Math.Max(0,Math.Min(age,CutAge(c)));float h=c.height;
   if(h>MatchSimulation.BallRadius+.001f||c.verticalVelocity>0)h+=c.verticalVelocity*t-.5f*Gravity*t*t;
   return new GoalBallPose(c.impact+c.velocity*t,Math.Max(MatchSimulation.BallRadius,h));
  }
  public bool TrySample(object simulationIdentity,MatchState state,ExitPresentationContact fresh,float alpha,out ExitRestartSample sample){
   sample=default;
   if(simulationIdentity==null||state==null||!Finite(state.clock)||!Finite(alpha)){Reset();return false;}
   if(!ReferenceEquals(identity,simulationIdentity)){Reset();identity=simulationIdentity;}
   alpha=Mathx.Clamp(alpha,0,1);double time=(double)state.clock-MatchSimulation.Step*(1-(double)alpha);
   if(Finite(lastClock)&&state.clock<lastClock-.0001f){blockedClock=contact.clock;active=false;lastTime=time;lastClock=state.clock;return false;}
   if(!double.IsNaN(lastTime))time=Math.Max(time,lastTime);lastTime=time;lastClock=state.clock;
   if(active&&!ExitPresentationContact.MatchesRestart(contact,state)){blockedClock=contact.clock;active=false;}
   if(fresh.valid&&fresh.clock!=blockedClock&&ExitPresentationContact.MatchesRestart(fresh,state)&&Safe(fresh)&&(!active||fresh.clock!=contact.clock)){
    contact=fresh;active=true;sawOutgoing=relocated=false;forcedCutTime=double.NaN;
   }
   if(!active)return false;
   double impactTime=(double)contact.clock-MatchSimulation.Step+contact.fraction*(double)MatchSimulation.Step;
   float age=(float)(time-impactTime),cut=CutAge(contact);
   sample.caption=contact.kind=="goal-kick"?"Sortie de but · remise en place":contact.kind=="throw-in"?"Touche · remise en place":"Corner · remise en place";
   if(age<0){float a=Mathx.Clamp((float)((time-(contact.clock-(double)MatchSimulation.Step))/MatchSimulation.Step),0,1);sample.pose=Incoming(contact,a);sample.ballVisible=true;sawOutgoing=true;return true;}
   if(age<cut){sample.pose=Outgoing(contact,age);sample.opacity=Smooth((age-Math.Max(0,cut-Fade))/Math.Max(.000001f,Math.Min(Fade,cut)));sample.ballVisible=sample.opacity<.999f;sawOutgoing=true;return true;}
   if(!relocated&&sawOutgoing)forcedCutTime=time;
   relocated=true;sample.repositioned=true;sample.pose=new GoalBallPose(state.ball.position,state.ball.height);
   sample.opacity=age<cut+OpaqueHold?1:1-Smooth((age-cut-OpaqueHold)/Fade);
   // If x10 skips the opaque interval, still cover the one observed change of
   // scene. No delay of simulation; a first render already after it does not replay.
   if(!double.IsNaN(forcedCutTime)&&time==forcedCutTime)sample.opacity=1;
   sample.ballVisible=sample.opacity<.999f;return true;
  }
 }
}

using System;
namespace Touchline.Core {
 // Presentation evidence only; never serialized into authoritative match state.
 public struct ExitPresentationContact {
  public bool valid;public float clock,fraction,previousHeight,height,verticalVelocity;
  public Point previous,impact,velocity,restartSpot;public string kind;public int side,eventIndex;
  public BallReleaseContact release;
  [NonSerialized] public MatchState stateIdentity;
  [NonSerialized] public BallState restartBallIdentity;
  static bool Finite(float f)=>!float.IsNaN(f)&&!float.IsInfinity(f);
  static bool Finite(Point p)=>Finite(p.x)&&Finite(p.z);
  public static ExitPresentationContact Capture(MatchState m,float fraction,Point impact,float height,string kind,int side,Point spot,BallReleaseContact release=default,float? vertical=null){
   if(m==null||m.ball==null||m.events==null||!Finite(m.clock)||!Finite(fraction)||fraction<0||fraction>1||!Finite(impact)||!Finite(height)||!Finite(spot)||side<0||side>1||(kind!="throw-in"&&kind!="corner"&&kind!="goal-kick"))return default;
   var b=m.ball;float vy=vertical??b.verticalVelocity;
   if(!Finite(b.previous)||!Finite(b.previousHeight)||!Finite(b.velocity)||!Finite(vy))return default;
   bool current=release.valid&&release.clock==m.clock&&release.kind==b.kind&&release.source==b.from&&b.elapsed>=0&&b.elapsed<=MatchSimulation.Step+.000001f&&release.fraction>=0&&release.fraction<=fraction&&Point.Distance(release.end,b.position)<.00001f&&Math.Abs(release.endHeight-b.height)<.00001f;
   return new ExitPresentationContact{valid=true,clock=m.clock,fraction=fraction,previous=b.previous,previousHeight=b.previousHeight,impact=impact,height=height,velocity=b.velocity,verticalVelocity=vy,kind=kind,side=side,restartSpot=spot,eventIndex=m.events.Count,stateIdentity=m,release=current?release:default};
  }
  static bool MatchesFields(ExitPresentationContact c,MatchState m){
   if(!c.valid||m==null||m.ball==null||m.events==null||!ReferenceEquals(c.stateIdentity,m)||!Finite(m.clock)||m.clock<c.clock||m.phase!=c.kind||m.restart<=0||m.restartSide!=c.side||m.ball.kind!="none"||m.ball.owner!=null||Point.Distance(m.ball.position,c.restartSpot)>.0001f||Math.Abs(m.ball.height-MatchSimulation.BallRadius)>.0001f||c.eventIndex<0||c.eventIndex>=m.events.Count)return false;
   var e=m.events[c.eventIndex];return e!=null&&e.kind==c.kind&&e.time==c.clock&&e.side==c.side&&Point.Distance(e.position,c.restartSpot)<.0001f;
  }
  public static bool MatchesRestart(ExitPresentationContact c,MatchState m)=>MatchesFields(c,m)&&ReferenceEquals(c.restartBallIdentity,m.ball);
  public static bool BindRestartBall(ref ExitPresentationContact c,MatchState m){if(!MatchesFields(c,m))return false;if(c.restartBallIdentity==null&&m.clock==c.clock)c.restartBallIdentity=m.ball;return ReferenceEquals(c.restartBallIdentity,m.ball);}
 }
 public sealed partial class MatchSimulation {
  ExitPresentationContact exitContact;
  public ExitPresentationContact ExitContact=>ExitPresentationContact.MatchesRestart(exitContact,State)?exitContact:default;
  void RecordExitContact(float fraction,Point impact,float height,string kind,int side,Point spot){
   float vertical=State.ball.verticalVelocity;if(CurrentFirstReleasedFlight(out var r))vertical=ReleasedVerticalVelocity(r,fraction);
   exitContact=ExitPresentationContact.Capture(State,fraction,impact,height,kind,side,spot,ReleaseContact,vertical);
  }
  void MaintainExitContact(){if(!ExitPresentationContact.BindRestartBall(ref exitContact,State))exitContact=default;}
 }
}

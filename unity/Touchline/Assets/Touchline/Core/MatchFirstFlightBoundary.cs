using System;
namespace Touchline.Core {
 public sealed partial class MatchSimulation {
  bool CurrentFirstReleasedFlight(out BallReleaseContact release){
   release=ReleaseContact;var b=State.ball;
   return release.valid&&release.clock==State.clock&&release.kind==b.kind&&release.source==b.from&&b.elapsed>=0&&b.elapsed<=Step+.000001f&&Point.Distance(b.position,release.end)<.00001f&&Math.Abs(b.height-release.endHeight)<.00001f;
  }
  static float ReleasedVerticalVelocity(BallReleaseContact release,float fraction){
   float u=ReleaseSweepProgress(release,fraction);
   return (release.flightEndHeight-release.releaseHeight)/release.duration+(float)Math.Cos(Math.PI*u)*(float)Math.PI*release.loft/release.duration;
  }
 }
}

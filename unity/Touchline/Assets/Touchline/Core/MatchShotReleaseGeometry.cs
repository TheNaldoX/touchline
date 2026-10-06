using System;
namespace Touchline.Core {
 public sealed partial class MatchSimulation {
  static float ReleaseSweepFraction(float begin,float local)=>local>1?2:begin+(1-begin)*local;
  static float ReleaseSweepProgress(BallReleaseContact r,float fraction)=>Mathx.Clamp((fraction-r.fraction)*Step/r.duration,0,1);
  static Point ReleaseSweepPosition(BallReleaseContact r,float fraction)=>fraction>=1?r.end:Point.Lerp(r.release,r.flightEnd,ReleaseSweepProgress(r,fraction));
  static float ReleaseSweepHeight(BallReleaseContact r,float fraction){
   if(fraction>=1)return r.endHeight;
   float u=ReleaseSweepProgress(r,fraction);
   return r.releaseHeight+(r.flightEndHeight-r.releaseHeight)*u+(float)Math.Sin(Math.PI*u)*r.loft;
  }
 }
}

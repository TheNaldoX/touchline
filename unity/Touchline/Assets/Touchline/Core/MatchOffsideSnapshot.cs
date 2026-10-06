using System;
namespace Touchline.Core {
 public sealed partial class MatchSimulation {
  const int OffsideSnapshotKnown=1<<22,OffsideSnapshotPending=-1;
  int OffsideActorBit(Actor player){
   // A tactical permutation reorders actor slots. Unique ID order stays
   // stable without sorting/allocation. Substitutions are at stoppages.
   int rank=0;foreach(var actor in State.actors)if(string.CompareOrdinal(actor.id,player.id)<0)rank++;
   return 1<<rank;
  }
  void CaptureOffsidePlayers(BallState ball,float releaseFraction){
   // Zero is a legacy save; pending and known-empty are durable states.
   if(ball.offsidePlayersMask!=OffsideSnapshotPending)return;
   int direction=Direction(ball.side);float first=-100,second=-100;
   foreach(var actor in State.actors){
    if(actor.sentOff||actor.side==ball.side)continue;
    float depth=Point.Lerp(actor.previous,actor.position,releaseFraction).x*direction;
    if(depth>first){second=first;first=depth;}else if(depth>second)second=depth;
   }
   float line=Math.Max(0,Math.Max(ball.start.x*direction,second));int mask=OffsideSnapshotKnown;
   foreach(var actor in State.actors){
    if(actor.sentOff||actor.side!=ball.side||actor.id==ball.from)continue;
    float depth=Point.Lerp(actor.previous,actor.position,releaseFraction).x*direction;
    if(depth>line+.1f)mask|=OffsideActorBit(actor);
   }
   ball.offsidePlayersMask=mask;
  }
  bool InOffsideSnapshot(Actor player){
   var ball=State.ball;if(ball.offsidePlayersMask==OffsideSnapshotPending)return false;
   if((ball.offsidePlayersMask&OffsideSnapshotKnown)!=0)return (ball.offsidePlayersMask&OffsideActorBit(player))!=0;
   // Preserve the known target flag without fabricating old kick positions.
   return ball.offside&&player.id==ball.to;
  }
 }
}
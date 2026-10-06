using System;
using Touchline.Core;
namespace Touchline {
 // Preserve the end of an already televised sequence; never discover a highlight
 // merely because the ball went out. Each authoritative replacement is consumed once.
 public sealed class ExitBroadcastWindow {
  BallState observed;
  public float Extend(ExitPresentationContact contact,MatchState state,bool wasQuiet,float visibleUntil){
   if(!ExitPresentationContact.MatchesRestart(contact,state)||ReferenceEquals(observed,state.ball))return visibleUntil;
   observed=state.ball;
   return wasQuiet?visibleUntil:Math.Max(visibleUntil,contact.clock+.7f);
  }
 }
}

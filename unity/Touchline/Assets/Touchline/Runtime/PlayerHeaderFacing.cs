using UnityEngine;
using Touchline.Core;
namespace Touchline
{
 public sealed partial class PlayerView
 {
  bool headerFacingContinues;
  bool ContinueHeaderFacing(Actor actor,float remaining,float dt,bool reset,float facing)
  {
   if(actor.action!="header")return false;
   if(reset||poseAction!="header"||poseSequence!=actor.actionSequence){
    float contact=actor.actionContactTime>0?actor.actionContactTime:.12f;
    float untilContact=Mathf.Max(0,contact-(.64f-remaining));
    // Preserve turning momentum when it can still aim the action on time.
    // A large pre-existing lag retains the established contact orientation
    // route; slowing that impossible turn would move the visual contact away.
    headerFacingContinues=!reset&&(poseAction=="run"||poseAction=="idle")&&locomotionFacing.CanReachHeading(facing*Mathf.Rad2Deg,actor.velocity.Length,untilContact+Mathf.Max(0,dt),3);
   }
   return headerFacingContinues;
  }
 }
}

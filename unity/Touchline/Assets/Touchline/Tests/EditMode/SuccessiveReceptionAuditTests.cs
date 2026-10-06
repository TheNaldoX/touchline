using NUnit.Framework;
using Touchline.Core;
using UnityEngine;
namespace Touchline.Tests {
 public class SuccessiveReceptionAuditTests {
  [TestCase(30)][TestCase(60)][TestCase(120)]
  public void NewSavedReceptionOnOppositeSideUsesNewImpact(int fps){
   var go=new GameObject("Successive opposite reception");
   try{
    var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="audit-reception",heightCm=180,preferredFoot="Right"},0,9,Color.white);
    var actor=new Actor{id="audit-reception",slot=9,action="control",actionKind=MatchSimulation.ThighControl,actionSequence=1,actionTime=.4f,actionTarget=new Point(.23f,.30f),actionHeight=.95f};
    var first=new Vector3(.23f,.95f,.30f);view.Render(actor,1,1f/fps,first);
    actor.actionSequence=2;actor.actionTarget=new Point(-.23f,.30f);
    var second=new Vector3(-.23f,.95f,.30f);
    // No run is rendered between receptions: the current saved event is
    // authoritative even if a x10 frame skipped intermediate actions.
    for(int frame=0;frame<fps/10;frame++)view.Render(actor,1,1f/fps,new Vector3(.20f,.11f,.40f));
    Assert.Less(Vector3.Distance(view.ThighContactPosition(false),second),.13f,"The right thigh must meet the new recorded impact, not retain the first touch's left leg");
    Assert.Greater(view.FootPosition(true).y,.025f,"The opposite supporting sole remains above the pitch");
   }finally{Object.DestroyImmediate(go);}
  }
 }
}

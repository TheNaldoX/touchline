using System;
using UnityEngine;
using UnityEngine.UIElements;
namespace Touchline {
 // An explicit broadcast repositioning cut. Does not block match controls outside
 // the viewport, does not animate a retrieval, and owns no simulation clock.
 public sealed class MatchRestartCut : IDisposable {
  MatchArena arena;VisualElement curtain;Label caption;
  public void Bind(MatchArena match,Image viewport){
   Dispose();arena=match;
   curtain=new VisualElement{name="restart-broadcast-cut",pickingMode=PickingMode.Ignore};
   curtain.style.position=Position.Absolute;curtain.style.left=0;curtain.style.top=0;curtain.style.right=0;curtain.style.bottom=0;
   curtain.style.backgroundColor=new Color(.055f,.09f,.125f,1);curtain.style.alignItems=Align.Center;curtain.style.justifyContent=Justify.Center;
   caption=new Label{pickingMode=PickingMode.Ignore};caption.style.color=new Color(.88f,.94f,.96f);caption.style.fontSize=20;caption.style.unityFontStyleAndWeight=FontStyle.Bold;caption.style.whiteSpace=WhiteSpace.Normal;caption.style.unityTextAlign=TextAnchor.MiddleCenter;caption.style.paddingLeft=20;caption.style.paddingRight=20;
   curtain.Add(caption);viewport.Add(curtain);curtain.style.display=DisplayStyle.None;
  }
  public void Refresh(){
   if(curtain==null||arena==null)return;
   float opacity=arena.RestartCutOpacity;
   if(arena.QuietPresentation||opacity<=0){curtain.style.display=DisplayStyle.None;return;}
   caption.text=arena.RestartCutCaption;curtain.style.opacity=Mathf.Clamp01(opacity);curtain.style.display=DisplayStyle.Flex;curtain.BringToFront();
  }
  public void Hide(){if(curtain!=null)curtain.style.display=DisplayStyle.None;}
  public void Dispose(){curtain?.RemoveFromHierarchy();curtain=null;caption=null;arena=null;}
 }
}

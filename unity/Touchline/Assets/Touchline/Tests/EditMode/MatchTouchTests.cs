using NUnit.Framework;
using UnityEngine;

namespace Touchline.Tests
{
    public class MatchTouchTests
    {
        [Test] public void ShortTouchSelects(){var g=new MatchTouchGesture();g.Down(1,new Vector2(10,10));Assert.IsTrue(g.Up(1,new Vector2(13,12)));}
        [Test] public void DragReturningToOriginDoesNotSelect(){var g=new MatchTouchGesture();g.Down(1,Vector2.zero);g.Move(1,new Vector2(50,0));g.Move(1,Vector2.zero);Assert.IsFalse(g.Up(1,Vector2.zero));}
        [Test] public void SpreadingFingersZoomsInWithoutSelecting(){var g=new MatchTouchGesture();g.Down(1,Vector2.zero);g.Down(2,new Vector2(100,0));Assert.That(g.Move(2,new Vector2(200,0)),Is.EqualTo(.5f).Within(.001));Assert.IsFalse(g.Up(2,new Vector2(200,0)));Assert.IsFalse(g.Up(1,Vector2.zero));}
        [Test] public void NextTapAfterPinchWorks(){var g=new MatchTouchGesture();g.Down(1,Vector2.zero);g.Down(2,Vector2.one*30);g.Up(1,Vector2.zero);g.Up(2,Vector2.one*30);g.Down(3,Vector2.zero);Assert.IsTrue(g.Up(3,Vector2.zero));}
        [Test] public void CancelledTouchDoesNotSelect(){var g=new MatchTouchGesture();g.Down(1,Vector2.zero);g.Cancel(1);Assert.IsFalse(g.Up(1,Vector2.zero));}
        [Test] public void NearlyCoincidentFingersCannotCauseExtremeZoom(){var g=new MatchTouchGesture();g.Down(1,Vector2.zero);g.Down(2,Vector2.one);Assert.AreEqual(1,g.Move(2,Vector2.one*100));}
        [Test] public void RemovingThirdFingerDoesNotMakeZoomJump(){var g=new MatchTouchGesture();g.Down(1,Vector2.zero);g.Down(2,new Vector2(100,0));g.Down(3,new Vector2(300,0));g.Up(1,Vector2.zero);Assert.AreEqual(1,g.Move(3,new Vector2(300,0)));Assert.IsFalse(g.Up(2,new Vector2(100,0)));Assert.IsFalse(g.Up(3,new Vector2(300,0)));}
    }
}

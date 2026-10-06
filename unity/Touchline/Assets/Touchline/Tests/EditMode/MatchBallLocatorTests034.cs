using System.Linq;
using NUnit.Framework;
using Touchline.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Touchline.Tests
{
    public class MatchBallLocatorTests034
    {
        [TestCase(.1f,.2f,800,400)] [TestCase(.9f,.8f,800,400)]
        [TestCase(.5f,.5f,400,900)] [TestCase(.25f,.75f,1200,650)]
        public void BothSizesRetainTheExactBallCentreWithoutChangingScreenSize(float x,float y,float width,float height)
        {
            foreach(var mode in new[]{MatchBallDisplay.Discreet,MatchBallDisplay.Enhanced}){
                Assert.IsTrue(MatchBallLocator.Project(new Vector3(x,y,20),new Vector2(width,height),mode,out var bounds));
                Assert.That(bounds.center.x,Is.EqualTo(x*width).Within(.001f));Assert.That(bounds.center.y,Is.EqualTo((1-y)*height).Within(.001f));
                Assert.AreEqual(mode==MatchBallDisplay.Discreet?10:18,bounds.width);Assert.AreEqual(bounds.width,bounds.height);
                Assert.IsTrue(MatchBallLocator.Project(new Vector3(x,y,120),new Vector2(width,height),mode,out var distant));
                Assert.AreEqual(bounds,distant,"The overlay must stay readable without changing the physical ball's size");
            }
        }
        [Test] public void EdgeCentresStayTrueInsteadOfPointingAtAnArtificialInsetPosition()
        {
            Assert.IsTrue(MatchBallLocator.Project(new Vector3(0,1,20),new Vector2(800,400),MatchBallDisplay.Enhanced,out var bounds));
            Assert.AreEqual(Vector2.zero,bounds.center);Assert.Less(bounds.x,0);Assert.Less(bounds.y,0);
            var locator=new MatchBallLocator();Assert.AreEqual(Overflow.Hidden,locator.Layer.style.overflow.value);locator.Dispose();
        }
        [Test] public void OffAndInvalidOrOutsideProjectionsHideAPreviouslyVisibleMarker()
        {
            var locator=new MatchBallLocator();var size=new Vector2(800,400);
            foreach(var invalid in new[]{new Vector3(-.01f,.5f,20),new Vector3(1.01f,.5f,20),new Vector3(.5f,-.01f,20),new Vector3(.5f,1.01f,20),new Vector3(.5f,.5f,-1),new Vector3(float.NaN,.5f,20),new Vector3(.5f,float.PositiveInfinity,20)}){
                locator.ShowProjected(new Vector3(.5f,.5f,20),size,MatchBallDisplay.Discreet);Assert.IsTrue(locator.Visible);
                locator.ShowProjected(invalid,size,MatchBallDisplay.Discreet);Assert.IsFalse(locator.Visible);
            }
            locator.ShowProjected(new Vector3(.5f,.5f,20),size,MatchBallDisplay.Enhanced);
            locator.ShowProjected(new Vector3(.5f,.5f,20),size,MatchBallDisplay.Off);Assert.IsFalse(locator.Visible);
            Assert.IsFalse(MatchBallLocator.Project(new Vector3(.5f,.5f,20),new Vector2(float.NaN,400),MatchBallDisplay.Discreet,out _));locator.Dispose();
        }
        [Test] public void RetainedMarkerNeverPicksTouchesAndQuietRefreshHidesIt()
        {
            var locator=new MatchBallLocator();var first=new Image();var second=new Image();var original=locator.Marker;
            locator.Bind(null,first);locator.ShowProjected(new Vector3(.5f,.5f,20),new Vector2(800,400),MatchBallDisplay.Enhanced);
            Assert.AreEqual(PickingMode.Ignore,locator.Layer.pickingMode);Assert.AreEqual(PickingMode.Ignore,locator.Marker.pickingMode);
            Assert.AreEqual(PickingMode.Position,first.pickingMode,"The viewport still receives player selection and pinch events");
            locator.Refresh();Assert.IsFalse(locator.Visible,"A detached or quiet viewport cannot retain a stale marker");
            locator.Bind(null,second);Assert.AreSame(original,locator.Marker);Assert.AreEqual(0,first.childCount);Assert.AreEqual(1,second.childCount);Assert.AreEqual(1,locator.Layer.childCount);
            locator.Dispose();Assert.AreEqual(0,second.childCount);
        }
        [Test] public void DisplaySettingPersistsAllModesAndClampsInvalidValues()
        {
            bool existed=PlayerPrefs.HasKey("match-ball-locator");int before=PlayerPrefs.GetInt("match-ball-locator");var go=new GameObject("Ball locator preferences");
            try{var arena=go.AddComponent<MatchArena>();foreach(var mode in new[]{MatchBallDisplay.Off,MatchBallDisplay.Discreet,MatchBallDisplay.Enhanced}){arena.SetBallDisplay(mode);Assert.AreEqual(mode,arena.BallDisplay);Assert.AreEqual((int)mode,PlayerPrefs.GetInt("match-ball-locator"));}
                arena.SetBallDisplay((MatchBallDisplay)99);Assert.AreEqual(MatchBallDisplay.Enhanced,arena.BallDisplay);arena.SetBallDisplay((MatchBallDisplay)(-1));Assert.AreEqual(MatchBallDisplay.Off,arena.BallDisplay);
            }finally{Object.DestroyImmediate(go);if(existed)PlayerPrefs.SetInt("match-ball-locator",before);else PlayerPrefs.DeleteKey("match-ball-locator");}
        }
        [Test] public void RenderedPositionFollowsAirborneBallAndTheKeepersActualHands()
        {
            var ambient=RenderSettings.ambientLight;bool fog=RenderSettings.fog;var fogColour=RenderSettings.fogColor;var fogMode=RenderSettings.fogMode;float fogStart=RenderSettings.fogStartDistance,fogEnd=RenderSettings.fogEndDistance;GameObject go=null;
            try{
                var db=JsonUtility.FromJson<Database>(Resources.Load<TextAsset>("Data/database").text);var clubs=db.clubs.Where(c=>c.playable).Take(2).ToArray();var career=new Career{club=clubs[0].id};career.lineup=Career.Select(db,career.club,career.tactic);
                var simulation=MatchSimulation.Create(db,career,clubs[1].id);go=new GameObject("Rendered ball locator source");var arena=go.AddComponent<MatchArena>();arena.Initialize(db,simulation);arena.Paused=true;arena.Broadcast.Enabled=false;
                var m=simulation.State;m.restart=0;m.phase="play";m.ball.owner=null;m.ball.held=false;m.ball.position=m.ball.previous=new Point(21,12);m.ball.height=m.ball.previousHeight=7;
                arena.RenderFrame(1f/30);Assert.AreEqual(new Vector3(21,7,12),arena.BallDisplayPosition);Assert.AreEqual(new Point(21,12),m.ball.position);
                var keeper=m.actors[0];keeper.action="keeper-hold";keeper.actionTime=1;m.ball.owner=keeper.id;m.ball.side=0;m.ball.held=true;m.ball.position=m.ball.previous=keeper.position;m.ball.height=m.ball.previousHeight=1.4f;
                arena.RenderFrame(1f/30);Assert.That(Vector3.Distance(arena.BallDisplayPosition,arena.PlayerVisual(0).HeldBallPosition),Is.LessThan(.00001f));
                Assert.AreEqual(keeper.position,m.ball.position,"The display helper must not rewrite the simulated ball");
            }finally{if(go!=null)Object.DestroyImmediate(go);RenderSettings.ambientLight=ambient;RenderSettings.fog=fog;RenderSettings.fogColor=fogColour;RenderSettings.fogMode=fogMode;RenderSettings.fogStartDistance=fogStart;RenderSettings.fogEndDistance=fogEnd;}
        }
    }
}

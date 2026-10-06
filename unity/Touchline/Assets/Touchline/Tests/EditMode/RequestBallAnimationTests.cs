using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Touchline.Core;

namespace Touchline.Tests
{
    public class RequestBallAnimationTests
    {
        [Test] public void RequestComesFromPossessionAndSurvivesSaveReload()
        {
            var actor=new Actor{id="runner",side=0,slot=9,intent="near-post",position=new Point(42,2)};
            var m=new MatchState{clock=100.9f,restart=0,phase="play",ball=new BallState{owner="wing",side=0,position=new Point(40,25)},actors=new[]{actor}};
            var context=PlayerMotionContext.From(m,actor,1);Assert.Greater(context.requestWeight,.5f);
            var restored=JsonUtility.FromJson<MatchState>(JsonUtility.ToJson(m));Assert.IsTrue(context.Same(PlayerMotionContext.From(restored,restored.actors[0],1)));
            var cache=new PausedPoseCache();Assert.IsTrue(cache.NeedsUpdate(0,actor,Vector3.zero,context));Assert.IsFalse(cache.NeedsUpdate(0,actor,Vector3.zero,context));
            m.restart=2;Assert.AreEqual(0,PlayerMotionContext.From(m,actor,1).requestWeight);m.restart=0;
            m.ball.side=1;Assert.AreEqual(0,PlayerMotionContext.From(m,actor,1).requestWeight);
            m.ball.side=0;actor.action="kick";Assert.AreEqual(0,PlayerMotionContext.From(m,actor,1).requestWeight);actor.action="idle";
            m.ball.side=0;m.actors=new[]{actor,new Actor{id="marker",side=1,position=new Point(42,3)}};Assert.AreEqual(0,PlayerMotionContext.From(m,actor,1).requestWeight);
        }
        [TestCase(-1)] [TestCase(1)] public void RequestRaisesOnlyTheSignallingHandAndReturnsToRest(int side)
        {
            var go=new GameObject("Calling for a pass");try{
                var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="runner",heightCm=182},0,9,Color.white);var a=new Actor{action="idle"};
                var joints=go.GetComponentsInChildren<Transform>();var wrist=joints.Single(t=>t.name=="wrist."+(side>0?"L":"R"));var other=joints.Single(t=>t.name=="wrist."+(side>0?"R":"L"));
                for(int i=0;i<30;i++)view.Render(a,1,1f/60,Vector3.forward*10,new PlayerMotionContext{requestSide=side,requestWeight=1});
                Assert.Greater(wrist.position.y,1.55f);Assert.Less(other.position.y,1.2f);
                for(int i=0;i<60;i++)view.Render(a,1,1f/60,Vector3.forward*10);
                Assert.Less(wrist.position.y,1.2f);
            }finally{Object.DestroyImmediate(go);}
        }
    }
}

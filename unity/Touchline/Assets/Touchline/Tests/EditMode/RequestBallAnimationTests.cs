using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Touchline.Core;

namespace Touchline.Tests
{
    public class RequestBallAnimationTests
    {
        [TestCase(30)][TestCase(60)][TestCase(120)]
        public void ClappingPalmsFaceEachOtherRatherThanBothFacingForward(int fps)
        {
            var go=new GameObject("Clapping palm orientation");try{
                var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="palms",heightCm=182},0,9,Color.white);var actor=new Actor{slot=9,action="idle"};
                for(int i=0;i<=fps;i++)view.Render(actor,1,1f/fps,default,new PlayerMotionContext{reaction=1,reactionTime=i/(float)fps});
                var bones=go.GetComponentsInChildren<Transform>();
                foreach(string side in new[]{"L","R"}){
                    var wrist=bones.Single(t=>t.name=="wrist."+side);var middle=bones.Single(t=>t.name=="finger3-1."+side);var index=bones.Single(t=>t.name=="finger2-1."+side);var little=bones.Single(t=>t.name=="finger5-1."+side);
                    var other=bones.Single(t=>t.name=="wrist."+(side=="L"?"R":"L"));
                    var palm=Vector3.Cross(little.position-index.position,middle.position-wrist.position).normalized*(side=="L"?1:-1);
                    Assert.Greater(Vector3.Dot(palm,(other.position-wrist.position).normalized),.9f);
                }
            }finally{Object.DestroyImmediate(go);}
        }
        [TestCase(30,false)][TestCase(60,false)][TestCase(120,false)]
        [TestCase(30,true)][TestCase(60,true)][TestCase(120,true)]
        public void CapturedSignalsBlendPauseAndReleaseWithoutMovingFeetOrMatchState(int fps,bool applause)
        {
            var go=new GameObject("Captured signal");var reference=new GameObject("Unchanged locomotion");try{
                var view=go.AddComponent<PlayerView>();var idle=reference.AddComponent<PlayerView>();var data=new PlayerData{id="signal-continuity",heightCm=182};view.Build(data,0,9,Color.white);idle.Build(data,0,9,Color.white);
                var actor=new Actor{slot=9,action="idle"};var hands=go.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("wrist.")).ToArray();
                for(int i=0;i<fps;i++){view.Render(actor,1,1f/fps);idle.Render(actor,1,1f/fps);}
                var previous=hands.Select(h=>h.position).ToArray();string state=JsonUtility.ToJson(actor);
                for(int i=0;i<fps*2;i++){
                    var context=i<fps?(applause?new PlayerMotionContext{reaction=1,reactionTime=i/(float)fps}:new PlayerMotionContext{requestSide=1,requestWeight=1}):default;
                    view.Render(actor,1,1f/fps,default,context);idle.Render(actor,1,1f/fps);
                    for(int j=0;j<hands.Length;j++){Assert.Less(Vector3.Distance(hands[j].position,previous[j])*fps,8,"No hand snap when starting/releasing the signal.");previous[j]=hands[j].position;}
                    foreach(bool left in new[]{true,false})Assert.Less(Vector3.Distance(view.FootPosition(left),idle.FootPosition(left)),.001f);
                    if(i==fps/2){var paused=hands.Select(h=>h.position).ToArray();for(int k=0;k<20;k++){view.Render(actor,1,0,default,context);for(int j=0;j<hands.Length;j++)Assert.Less(Vector3.Distance(hands[j].position,paused[j]),.001f);}}
                }
                Assert.AreEqual(state,JsonUtility.ToJson(actor));
            }finally{Object.DestroyImmediate(go);Object.DestroyImmediate(reference);}
        }
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

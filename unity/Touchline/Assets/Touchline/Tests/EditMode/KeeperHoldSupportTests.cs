using NUnit.Framework;
using UnityEngine;
using Touchline.Core;

namespace Touchline.Tests
{
    public class KeeperHoldSupportTests
    {
        [TestCase(30)] [TestCase(60)] [TestCase(120)]
        public void InterruptedPlacementReturnsWithoutHandSnap(int fps)
        {
            var go=new GameObject("Interrupted keeper placement");
            try{
                var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="placement-interrupted",heightCm=182},0,0,Color.yellow);
                var actor=new Actor{slot=0,action="keeper-hold",actionTime=1};var ball=new Vector3(0,1.1f,.36f);
                for(int frame=0;frame<fps;frame++)view.Render(actor,1,1f/fps,ball);
                for(int frame=0;frame<fps*.4f;frame++){
                    float blend=Mathf.SmoothStep(0,1,frame/(fps*MatchSimulation.KeeperPlaceDuration));
                    actor.action="place-ball";actor.actionTime=MatchSimulation.KeeperPlaceDuration-frame/(float)fps;
                    ball=new Vector3(0,Mathf.Lerp(1.1f,.11f,blend),Mathf.Lerp(.36f,.42f,blend));view.Render(actor,1,1f/fps,ball);
                }
                var previous=view.HeldBallPosition;actor.action="idle";actor.actionTime=0;
                for(int frame=0;frame<fps;frame++){
                    view.Render(actor,1,1f/fps,ball);
                    Assert.Less(Vector3.Distance(previous,view.HeldBallPosition)*fps,8,"Cancellation must recover over time");previous=view.HeldBallPosition;
                }
                Assert.Less(Mathf.Abs(go.transform.Find("Rig").localPosition.y),.035f);
            }finally{Object.DestroyImmediate(go);}
        }
        [TestCase(160,60)] [TestCase(182,30)] [TestCase(182,60)] [TestCase(182,120)] [TestCase(205,60)]
        public void PlacementTracksThePhysicalBallAndRecoversContinuously(int height,int fps)
        {
            var go=new GameObject("Captured keeper placement");
            try{
                var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="placement-contact",heightCm=height},0,0,Color.yellow);
                var actor=new Actor{slot=0,action="keeper-hold",actionTime=1};var ball=new Vector3(0,1.1f,.36f);
                for(int frame=0;frame<fps;frame++)view.Render(actor,1,1f/fps,ball);
                var previous=view.HeldBallPosition;float maxTravel=0;
                for(int frame=0;frame<=Mathf.CeilToInt(MatchSimulation.KeeperPlaceDuration*fps);frame++){
                    float progress=Mathf.Clamp01(frame/(fps*MatchSimulation.KeeperPlaceDuration)),blend=Mathf.SmoothStep(0,1,progress);
                    actor.action="place-ball";actor.actionTime=MatchSimulation.KeeperPlaceDuration*(1-progress);ball=new Vector3(0,Mathf.Lerp(1.1f,.11f,blend),Mathf.Lerp(.36f,.42f,blend));
                    view.Render(actor,1,1f/fps,ball);
                    Assert.Less(Vector3.Distance(view.HeldBallPosition,ball),.12f,"Hands must accompany the ball down to the grass");
                    maxTravel=Mathf.Max(maxTravel,Vector3.Distance(previous,view.HeldBallPosition)*fps);previous=view.HeldBallPosition;
                }
                for(int frame=0;frame<=fps;frame++){
                    float elapsed=frame/(float)fps;actor.action=elapsed<MatchSimulation.KeeperRiseDuration?"keeper-rise":"idle";actor.actionTime=Mathf.Max(0,MatchSimulation.KeeperRiseDuration-elapsed);
                    view.Render(actor,1,1f/fps,ball);maxTravel=Mathf.Max(maxTravel,Vector3.Distance(previous,view.HeldBallPosition)*fps);previous=view.HeldBallPosition;
                }
                Assert.Less(maxTravel,8,"No hand jump when placement or recovery ends");
            }finally{Object.DestroyImmediate(go);}
        }
        [TestCase(160,30)] [TestCase(182,60)] [TestCase(205,30)] [TestCase(205,60)] [TestCase(205,120)] [TestCase(215,60)]
        public void HeldBallMatchesPhysicalHeightBeforeDistribution(int height,int fps)
        {
            var go=new GameObject("Keeper hold support");
            try{
                var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="hold-support",heightCm=height},0,0,Color.yellow);
                var actor=new Actor{slot=0,action="keeper-hold",actionTime=.3f};var ball=new Vector3(0,1.1f,.36f);
                for(int frame=0;frame<fps/2;frame++)view.Render(actor,1,1f/fps,ball);
                Assert.Less(Vector3.Distance(view.HeldBallPosition,ball),.04f,"The rendered grip must not jump to a different physical start point on distribution");
                Assert.That(view.FootPosition(true).y,Is.InRange(.05f,.12f));Assert.That(view.FootPosition(false).y,Is.InRange(.05f,.12f));
                var body=go.transform.Find("Rig");Assert.Less(Mathf.Abs(body.localPosition.y),.12f,"Support is a small bend, not a squat");
            }finally{Object.DestroyImmediate(go);}
        }

        [TestCase(182)] [TestCase(205)]
        public void SupportBlendsIntoPlacementAndLeavesRiseUpright(int height)
        {
            var go=new GameObject("Keeper placement support");
            try{
                var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="place-support",heightCm=height},0,0,Color.yellow);
                var actor=new Actor{slot=0,action="keeper-hold",actionTime=.3f};var ball=new Vector3(0,1.1f,.36f);view.Render(actor,1,.01f,ball);
                var previous=view.HeldBallPosition;
                for(int frame=0;frame<=65;frame++){
                    float progress=frame/65f,blend=Mathf.SmoothStep(0,1,progress);
                    actor.action="place-ball";actor.actionTime=.65f*(1-progress);ball=new Vector3(0,Mathf.Lerp(1.1f,.11f,blend),Mathf.Lerp(.36f,.42f,blend));
                    view.Render(actor,1,.01f,ball);Assert.Less(Vector3.Distance(previous,view.HeldBallPosition),.08f,"No placement snap");previous=view.HeldBallPosition;
                }
                for(int frame=0;frame<=45;frame++){actor.action="keeper-rise";actor.actionTime=.45f-frame*.01f;view.Render(actor,1,.01f,ball);}
                Assert.Less(Mathf.Abs(go.transform.Find("Rig").localPosition.y),.035f,"Without the ball the keeper stands fully upright");
            }finally{Object.DestroyImmediate(go);}
        }
    }
}

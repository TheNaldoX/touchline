using NUnit.Framework;
using UnityEngine;
using Touchline.Core;

namespace Touchline.Tests
{
    public class KeeperHoldSupportTests
    {
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

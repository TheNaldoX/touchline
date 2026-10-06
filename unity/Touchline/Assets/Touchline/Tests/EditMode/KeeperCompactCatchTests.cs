using NUnit.Framework;
using UnityEngine;
using Touchline.Core;

namespace Touchline.Tests
{
    public class KeeperCompactCatchTests
    {
        [TestCase(.22f,30)] [TestCase(.6f,60)] [TestCase(1.1f,120)]
        [TestCase(1.8f,30)] [TestCase(2.0f,60)]
        public void CentralCatchKeepsSupportAndMeetsTheBall(float height,int fps)
        {
            var go=new GameObject("Compact keeper test");
            try{
                var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="compact",heightCm=182},0,0,Color.yellow);
                var actor=new Actor{slot=0,action="idle"};view.Render(actor,1,1f/fps);
                actor.action="dive";actor.actionKind="save-catch";actor.actionSequence=1;
                actor.actionContactTime=.2f;actor.actionTarget=new Point(.2f,.65f);actor.actionHeight=height;
                var ball=new Vector3(.2f,height,.65f);var body=go.transform.Find("Rig");
                for(int frame=0;frame<=Mathf.CeilToInt(1.2f*fps);frame++){
                    float t=Mathf.Min(1.2f,frame/(float)fps);actor.actionTime=1.2f-t;view.Render(actor,1,1f/fps,ball);
                    Assert.Less(Quaternion.Angle(body.localRotation,Quaternion.identity),35,"A central gather must not launch sideways");
                    Assert.Less(Vector3.Distance(view.FootPosition(true),new Vector3(.18f,.08f,0)),.055f,"Left supporting sole");
                    Assert.Less(Vector3.Distance(view.FootPosition(false),new Vector3(-.18f,.08f,0)),.055f,"Right supporting sole");
                }
                Assert.Less(body.localPosition.magnitude,.005f);Assert.Less(Quaternion.Angle(body.localRotation,Quaternion.identity),.05f);
                actor.actionTime=1.0f;view.ResetPresentation();view.Render(actor,1,1f/fps,ball);
                Assert.Less(Vector3.Distance(view.HeldBallPosition,ball),.15f,"Both hands must meet the authoritative contact, even for low/high catches");
            }finally{Object.DestroyImmediate(go);}
        }

        [TestCase(-1)] [TestCase(1)]
        public void LateralSaveRetainsRealDive(int side)
        {
            var go=new GameObject("Lateral keeper test");
            try{
                var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="wide",heightCm=182},0,0,Color.yellow);
                var actor=new Actor{slot=0,action="dive",actionKind="save-catch",actionSequence=1,actionTime=1,actionContactTime=.2f,actionTarget=new Point(side*1.1f,.3f),actionHeight=.6f,diveSide=side};
                view.Render(actor,1,.01f,new Vector3(side*1.1f,.6f,.3f));
                Assert.Greater(Quaternion.Angle(go.transform.Find("Rig").localRotation,Quaternion.identity),50);
                Assert.Less(Vector3.Distance(view.HeldBallPosition,new Vector3(side*1.1f,.6f,.3f)),.17f);
            }finally{Object.DestroyImmediate(go);}
        }

        [Test] public void CompactToDiveSelectionHasNoHardGeometricThreshold()
        {
            float previous=1;
            for(int i=0;i<=110;i++){
                float weight=KeeperCompactCatch.Weight(new Vector3(i*.01f,1.1f,.6f));
                Assert.That(weight,Is.InRange(0,1));Assert.LessOrEqual(weight,previous+.00001f);
                Assert.Less(Mathf.Abs(weight-previous),.04f);previous=weight;
            }
            Assert.AreEqual(0,previous);
            Assert.AreEqual(0,KeeperCompactCatch.Weight(new Vector3(0,2.6f,.3f)));
            Assert.AreEqual(0,KeeperCompactCatch.Weight(new Vector3(0,1,-.6f)));
        }

        [TestCase(-1,.3f)] [TestCase(1,.3f)] [TestCase(-1,1.1f)] [TestCase(1,1.1f)]
        public void IntermediateSavesRetainContactAcrossTheBlendBand(int side,float height)
        {
            var go=new GameObject("Blended keeper reach");
            try{
                var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="blend",heightCm=182},0,0,Color.yellow);
                var actor=new Actor{slot=0,action="dive",actionKind="save-catch",actionSequence=1,actionTime=1,actionContactTime=.2f,actionHeight=height,diveSide=side};
                Vector3 previous=Vector3.zero;Quaternion previousRotation=Quaternion.identity;
                for(int step=0;step<=50;step++){
                    float x=side*(.47f+step*.01f);actor.actionTarget=new Point(x,.3f);var ball=new Vector3(x,height,.3f);
                    view.ResetPresentation();view.Render(actor,1,.01f,ball);
                    var body=go.transform.Find("Rig");
                    Assert.Less(Vector3.Distance(view.HeldBallPosition,ball),.17f,"Contact at lateral offset "+x);
                    if(step>0){Assert.Less(Vector3.Distance(body.localPosition,previous),.055f);Assert.Less(Quaternion.Angle(body.localRotation,previousRotation),4);}
                    Assert.GreaterOrEqual(view.FootPosition(true).y,.05f);Assert.GreaterOrEqual(view.FootPosition(false).y,.05f);
                    previous=body.localPosition;previousRotation=body.localRotation;
                }
            }finally{Object.DestroyImmediate(go);}
        }
    }
}

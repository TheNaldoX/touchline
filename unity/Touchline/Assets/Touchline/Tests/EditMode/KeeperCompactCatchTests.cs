using NUnit.Framework;
using UnityEngine;
using Touchline.Core;

namespace Touchline.Tests
{
    public class KeeperCompactCatchTests
    {
        [TestCase(30,1.85f)] [TestCase(60,1.85f)] [TestCase(120,1.85f)]
        [TestCase(30,2.0f)] [TestCase(60,2.0f)] [TestCase(120,2.0f)]
        public void ReachableOverheadCatchStaysUprightWithGroundSupport(int fps,float height)
        {
            var root=new GameObject("Central overhead gather");try{
                var view=root.AddComponent<PlayerView>();view.Build(new PlayerData{id="overhead-central",heightCm=182},0,0,Color.yellow);
                var actor=new Actor{slot=0,action="idle"};view.Render(actor,1,0);var animator=root.GetComponentInChildren<Animator>();
                actor.action="dive";actor.actionKind="save-catch";actor.actionSequence=1;actor.actionContactTime=.2f;actor.actionHeight=height;actor.actionTarget=new Point(.2f,.65f);
                var ball=new Vector3(.2f,height,.65f);
                float Sole(HumanBodyBones bone){var foot=animator.GetBoneTransform(bone);var r=foot.rotation;float extent=Mathf.Abs((r*Vector3.right).y)*.057f+Mathf.Abs((r*Vector3.up).y)*.085f+Mathf.Abs((r*Vector3.forward).y)*.145f;return foot.position.y+((r*new Vector3(0,.02f,.075f)).y-extent)*root.transform.localScale.y;}
                for(int f=0;f<=fps*1.2f;f++){
                    float t=f/(float)fps;actor.actionTime=1.2f-t;string before=JsonUtility.ToJson(actor);view.Render(actor,1,1f/fps,ball);
                    Assert.AreEqual("Goalkeeper Catch",view.MecanimGestureClip);
                    var hips=animator.GetBoneTransform(HumanBodyBones.Hips);var chest=animator.GetBoneTransform(HumanBodyBones.Chest);
                    Assert.Less(Vector3.Angle(chest.position-hips.position,Vector3.up),35,"Central overhead gather must not fall sideways");
                    float left=Sole(HumanBodyBones.LeftFoot),right=Sole(HumanBodyBones.RightFoot);Assert.GreaterOrEqual(Mathf.Min(left,right),-.025f,"Visible soles stay above the turf");
                    if(f==fps/5){Assert.Less(Vector3.Distance(view.HeldBallPosition,ball),.15f);Assert.Less(Mathf.Min(left,right),.08f,"A reachable overhead gather retains a grounded support");}
                    Assert.AreEqual(before,JsonUtility.ToJson(actor));Assert.AreEqual(Vector3.zero,root.transform.position);
                }
            }finally{Object.DestroyImmediate(root);}
        }
        [TestCase(30)] [TestCase(60)] [TestCase(120)]
        public void CapturedChestCatchKeepsTheAnatomicalTorsoUpright(int fps)
        {
            var go=new GameObject("Captured central catch");
            try{
                var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="captured-central",heightCm=182},0,0,Color.yellow);
                var actor=new Actor{slot=0,action="idle"};view.Render(actor,1,1f/fps);
                var animator=go.GetComponentInChildren<Animator>();
                actor.action="dive";actor.actionKind="save-catch";actor.actionSequence=1;actor.actionContactTime=.2f;
                actor.actionTarget=new Point(.2f,.65f);actor.actionHeight=1.1f;
                var ball=new Vector3(.2f,1.1f,.65f);
                for(int frame=0;frame<=fps;frame++){
                    float elapsed=frame/(float)fps;actor.actionTime=1.2f-elapsed;view.Render(actor,1,1f/fps,ball);
                    var hips=animator.GetBoneTransform(HumanBodyBones.Hips);var chest=animator.GetBoneTransform(HumanBodyBones.Chest);
                    Assert.Less(Vector3.Angle(chest.position-hips.position,Vector3.up),35,"Actual captured spine must not roll into a lateral dive");
                    if(frame==fps/5)Assert.Less(Vector3.Distance(view.HeldBallPosition,ball),.15f,"Capture must retain the simulated contact");
                }
            }finally{Object.DestroyImmediate(go);}
        }

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
                var animator=go.GetComponentInChildren<Animator>();
                var torso=animator.GetBoneTransform(HumanBodyBones.Chest).position-animator.GetBoneTransform(HumanBodyBones.Hips).position;
                Assert.Greater(Vector3.Angle(torso,Vector3.up),50,"The anatomical torso, not the capture container, must dive sideways");
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

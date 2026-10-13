using NUnit.Framework;
using UnityEngine;
using Touchline.Core;

namespace Touchline.Tests
{
    public class KeeperRecoverySupportTests
    {
        [TestCase(-1,30)][TestCase(1,30)][TestCase(-1,60)][TestCase(1,60)][TestCase(-1,120)][TestCase(1,120)]
        public void PausedLandingSupportDoesNotAccumulateRetargetingOrMoveTheSimulatedKeeper(int side,int fps)
        {
            var go=new GameObject("Paused keeper ground support");try{
                var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="paused-brace",heightCm=185},0,0,Color.yellow);
                var actor=new Actor{slot=0,action="idle"};view.Render(actor,1,0);
                actor.action="dive";actor.actionKind="save-parry";actor.actionSequence=1;actor.actionContactTime=.18f;
                actor.actionTarget=new Point(side*1.1f,.3f);actor.actionHeight=1.1f;actor.diveSide=side;
                var ball=new Vector3(side*1.1f,1.1f,.3f);float elapsed=0;
                while(elapsed<.65f){float next=Mathf.Min(.65f,elapsed+1f/fps);actor.actionTime=1.2f-next;view.Render(actor,1,next-elapsed,ball);elapsed=next;}
                var animator=go.GetComponentInChildren<Animator>();
                var hips=animator.GetBoneTransform(HumanBodyBones.Hips);var hip=hips.position;
                var left=view.FootPosition(true);var right=view.FootPosition(false);var hands=view.HeldBallPosition;string state=JsonUtility.ToJson(actor);
                for(int i=0;i<20;i++){
                    view.Render(actor,1,0,ball);
                    Assert.Less(Vector3.Distance(hip,hips.position),.002f);
                    Assert.Less(Vector3.Distance(left,view.FootPosition(true)),.002f);
                    Assert.Less(Vector3.Distance(right,view.FootPosition(false)),.002f);
                    Assert.Less(Vector3.Distance(hands,view.HeldBallPosition),.002f);
                }
                Assert.AreEqual(state,JsonUtility.ToJson(actor));Assert.Less(go.transform.position.sqrMagnitude,.000001f);
            }finally{Object.DestroyImmediate(go);}
        }
        [TestCase(-1,30)] [TestCase(1,30)] [TestCase(-1,60)] [TestCase(1,60)] [TestCase(-1,120)] [TestCase(1,120)]
        public void CapturedDiveReturnsToStandingBeforePhysicalRecoveryEnds(int side,int hz)
        {
            bool old=PlayerView.UseMecanim;PlayerView.UseMecanim=true;var go=new GameObject("Captured recovery clock");
            try{
                var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="recovery-clock",heightCm=185},0,0,Color.yellow);
                var actor=new Actor{slot=0,action="idle"};float dt=1f/hz;for(int i=0;i<hz;i++)view.Render(actor,1,dt);
                actor.action="dive";actor.actionKind="save-catch";actor.actionSequence=1;actor.actionContactTime=.2f;
                actor.actionTarget=new Point(side*1.1f,.3f);actor.actionHeight=.6f;actor.diveSide=side;
                var ball=new Vector3(side*1.1f,.6f,.3f);var animator=go.GetComponentInChildren<Animator>();
                for(int i=0;i<=Mathf.CeilToInt(1.2f*hz);i++){
                    float elapsed=Mathf.Min(1.2f,i*dt);actor.actionTime=1.2f-elapsed;view.Render(actor,1,dt,ball);
                    if(Mathf.Abs(elapsed-.2f)<.0001f)Assert.Less(Vector3.Distance(view.HeldBallPosition,ball),.15f,"Impact timing must remain intact");
                    Assert.Less(go.transform.position.sqrMagnitude,.000001f);
                }
                var hips=animator.GetBoneTransform(HumanBodyBones.Hips);var chest=animator.GetBoneTransform(HumanBodyBones.Chest);
                Assert.Less(Vector3.Angle(chest.position-hips.position,Vector3.up),35,"Recovery must not end in the lateral landing pose");
                Assert.Greater(hips.position.y,.7f,"Keeper must regain standing height before the next action");
            }finally{Object.DestroyImmediate(go);PlayerView.UseMecanim=old;}
        }
        [TestCase(-1,.22f)] [TestCase(1,.22f)] [TestCase(-1,1.1f)] [TestCase(1,1.1f)] [TestCase(-1,2.2f)] [TestCase(1,2.2f)]
        [TestCase(-1,.22f,160)] [TestCase(1,2.2f,205)]
        public void AfterAParryTheFreePalmBracesTheGroundDuringLanding(int side,float height,int stature=185)
        {
            var go=new GameObject("Keeper landing hand support");
            try{
                var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="brace",heightCm=stature},0,0,Color.yellow);
                var actor=new Actor{slot=0,action="idle"};view.Render(actor,1,.01f);
                actor.action="dive";actor.actionKind="save-parry";actor.actionSequence=1;actor.actionContactTime=.18f;
                actor.actionTarget=new Point(side*1.1f,.3f);actor.actionHeight=height;actor.diveSide=side;
                Transform wrist=null;foreach(var bone in go.GetComponentsInChildren<Transform>())if(bone.name==(side>0?"wrist.L":"wrist.R"))wrist=bone;
                Assert.IsNotNull(wrist);Vector3 anchor=Vector3.zero;
                for(int frame=0;frame<=120;frame++){
                    float t=frame*.01f;actor.actionTime=1.2f-t;view.Render(actor,1,.01f,new Vector3(side*1.1f,height,.3f));
                    if(frame==60)anchor=wrist.position;
                    if(frame>=60&&frame<=68){Assert.That(wrist.position.y,Is.InRange(.06f,.14f),"Free palm must brace close to the ground");Assert.Less(Vector3.Distance(wrist.position,anchor),.025f,"The support hand must not skate during landing");}
                }
                Assert.Greater(wrist.position.y,.7f,"The hand must release the ground during the rise");
            }finally{Object.DestroyImmediate(go);}
        }
        [TestCase(-1,30)] [TestCase(1,30)] [TestCase(-1,60)] [TestCase(1,120)]
        public void RecoverySetsOneSupportBeforeBringingTheOtherFootUnderTheBody(int side,int fps)
        {
            var go=new GameObject("Staggered keeper recovery");
            try{
                var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="support",heightCm=185},0,0,Color.yellow);
                var actor=new Actor{slot=0,action="idle"};view.Render(actor,1,1f/fps);
                actor.action="dive";actor.actionKind="save-catch";actor.actionSequence=1;
                actor.actionContactTime=.18f;actor.actionTarget=new Point(side*1.1f,.3f);actor.actionHeight=.5f;actor.diveSide=side;
                var ball=new Vector3(actor.actionTarget.x,.5f,.3f);
                for(int frame=0;frame<=Mathf.FloorToInt(.88f*fps);frame++){
                    actor.actionTime=1.2f-frame/(float)fps;view.Render(actor,1,1f/fps,ball);
                }
                actor.actionTime=1.2f-.88f;view.Render(actor,1,1f/fps,ball);
                var nearTarget=go.transform.TransformPoint(new Vector3(side*.18f,.08f,0));
                var farTarget=go.transform.TransformPoint(new Vector3(-side*.18f,.08f,0));
                float near=Vector3.Distance(view.FootPosition(side>0),nearTarget);
                float far=Vector3.Distance(view.FootPosition(side<0),farTarget);
                TestContext.WriteLine("At 0.88 s: support error "+near+" m; arriving foot distance "+far+" m");
                Assert.Less(near,.045f,"The first foot must support the crouched body before the second arrives");
                Assert.Greater(far,.075f,"Both feet must not slide back into stance together");
                Vector3 lastLeft=Vector3.zero,lastRight=Vector3.zero;
                for(int frame=Mathf.CeilToInt(.9f*fps);frame<=Mathf.CeilToInt(1.2f*fps);frame++){
                    float t=Mathf.Min(1.2f,frame/(float)fps);actor.actionTime=1.2f-t;view.Render(actor,1,1f/fps,ball);
                    if(t>=1.08f){
                        Assert.Less(Vector3.Distance(view.FootPosition(true),go.transform.TransformPoint(new Vector3(.18f,.08f,0))),.045f);
                        Assert.Less(Vector3.Distance(view.FootPosition(false),go.transform.TransformPoint(new Vector3(-.18f,.08f,0))),.045f);
                        if(lastLeft!=Vector3.zero){Assert.Less(Vector3.Distance(lastLeft,view.FootPosition(true)),.012f);Assert.Less(Vector3.Distance(lastRight,view.FootPosition(false)),.012f);}
                        lastLeft=view.FootPosition(true);lastRight=view.FootPosition(false);
                    }
                }
            }finally{Object.DestroyImmediate(go);}
        }
    }
}

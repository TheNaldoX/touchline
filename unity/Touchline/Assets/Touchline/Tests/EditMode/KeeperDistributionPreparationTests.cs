using System;
using NUnit.Framework;
using Touchline.Core;
using UnityEngine;
namespace Touchline.Tests
{
    public class KeeperDistributionPreparationTests
    {
        [TestCase(30,"keeper-roll")] [TestCase(60,"keeper-roll")] [TestCase(120,"keeper-roll")]
        [TestCase(30,"keeper-throw")] [TestCase(60,"keeper-throw")] [TestCase(120,"keeper-throw")]
        public void ReleaseReturnsToCaptureWithoutHandJump(int fps,string kind)
        {
            var go=new GameObject("Keeper release continuity");try{
                var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="release-continuity",heightCm=182},0,0,Color.yellow);
                var actor=new Actor{slot=0,action="keeper-hold",actionTime=1};var ball=new Vector3(0,1.1f,.36f);
                for(int i=0;i<fps;i++)view.Render(actor,1,1f/fps,ball);
                actor.action=actor.actionKind=kind;actor.actionSequence=1;actor.actionContactTime=.48f;actor.actionTarget=new Point(-.20f,.52f);actor.actionHeight=kind=="keeper-roll"?.24f:1.78f;
                var state=new BallState{kind=kind,start=actor.actionTarget,startHeight=actor.actionHeight,setupStart=new Point(0,.36f),setupHeight=1.1f,end=new Point(0,20)};
                var previous=view.DistributionHandPosition;
                for(int frame=0;frame<=fps*2;frame++){
                    float elapsed=frame/(float)fps;actor.action=elapsed<MatchSimulation.KeeperDistributionDuration?kind:"idle";actor.actionTime=Mathf.Max(0,MatchSimulation.KeeperDistributionDuration-elapsed);
                    if(elapsed<=.48f){var p=MatchSimulation.KeeperDistributionPreparation(state,elapsed/.48f,out var height);ball=new Vector3(p.x,height,p.z);}
                    else ball=new Vector3(actor.actionTarget.x,actor.actionHeight,actor.actionTarget.z+12*(elapsed-.48f));
                    view.Render(actor,1,1f/fps,ball);
                    if(elapsed>.3f)Assert.Less(Vector3.Distance(previous,view.DistributionHandPosition)*fps,12,"Release and recovery must not snap the hand");
                    previous=view.DistributionHandPosition;
                }
            }finally{UnityEngine.Object.DestroyImmediate(go);}
        }
        [TestCase(30,"keeper-roll","Goalkeeper Pass")] [TestCase(60,"keeper-roll","Goalkeeper Pass")] [TestCase(120,"keeper-roll","Goalkeeper Pass")]
        [TestCase(30,"keeper-throw","Goalkeeper Overhand Throw")] [TestCase(60,"keeper-throw","Goalkeeper Overhand Throw")] [TestCase(120,"keeper-throw","Goalkeeper Overhand Throw")]
        public void HandDistributionUsesTheRecordedGesture(int fps,string kind,string clip)
        {
            var go=new GameObject("Keeper distribution clip");try{
                var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="distribution-clip",heightCm=182},0,0,Color.yellow);
                var actor=new Actor{slot=0,action="keeper-hold",actionTime=1};var ball=new Vector3(0,1.1f,.36f);
                for(int i=0;i<fps;i++)view.Render(actor,1,1f/fps,ball);
                actor.action=actor.actionKind=kind;actor.actionSequence=1;actor.actionTime=MatchSimulation.KeeperDistributionDuration;actor.actionContactTime=MatchSimulation.KeeperDistributionContact;
                view.Render(actor,1,1f/fps,ball);Assert.AreEqual(clip,view.MecanimGestureClip,"A hand distribution must not remain in the idle animation");
            }finally{UnityEngine.Object.DestroyImmediate(go);}
        }
        [TestCase(30,166,"keeper-roll")]
        [TestCase(30,166,"keeper-throw")]
        [TestCase(30,182,"keeper-roll")]
        [TestCase(30,182,"keeper-throw")]
        [TestCase(30,200,"keeper-roll")]
        [TestCase(30,200,"keeper-throw")]
        [TestCase(60,166,"keeper-roll")]
        [TestCase(60,166,"keeper-throw")]
        [TestCase(60,182,"keeper-roll")]
        [TestCase(60,182,"keeper-throw")]
        [TestCase(60,200,"keeper-roll")]
        [TestCase(60,200,"keeper-throw")]
        [TestCase(120,166,"keeper-roll")]
        [TestCase(120,166,"keeper-throw")]
        [TestCase(120,182,"keeper-roll")]
        [TestCase(120,182,"keeper-throw")]
        [TestCase(120,200,"keeper-roll")]
        [TestCase(120,200,"keeper-throw")]
        public void HandFollowsEntirePreparationAndReleasesWithoutChangingSimulation(int fps,int height,string kind)
        {
            var go=new GameObject("Distribution preparation contact");try{
                var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="prep-contact",heightCm=height},0,0,Color.yellow);
                var actor=new Actor{slot=0,action="keeper-hold",actionTime=1};
                var setup=new Vector3(0,1.1f,.36f);view.Render(actor,1,1f/fps,setup);
                actor.action=actor.actionKind=kind;actor.actionSequence=1;actor.actionContactTime=.48f;
                actor.actionTarget=new Point(-.20f,.52f);actor.actionHeight=kind=="keeper-roll"?.24f:1.78f*height/182;
                var state=new BallState{kind=kind,start=actor.actionTarget,startHeight=actor.actionHeight,setupStart=new Point(0,.36f),setupHeight=1.1f,end=new Point(0,20)};
                float last=0;
                for(int i=0;i<=Mathf.CeilToInt(.48f*fps);i++){
                    float time=Mathf.Min(.48f,i/(float)fps);actor.actionTime=MatchSimulation.KeeperDistributionDuration-time;
                    var p=MatchSimulation.KeeperDistributionPreparation(state,time/.48f,out float h);var ball=new Vector3(p.x,h,p.z);
                    var snapshot=JsonUtility.ToJson(actor);var ballSnapshot=JsonUtility.ToJson(state);
                    view.Render(actor,1,time-last,ball);
                    Assert.That(Vector3.Distance(view.DistributionHandPosition,ball),Is.LessThan(.14f),"Hand missed held path at "+time);
                    Assert.Greater(view.FootPosition(true).y,-.025f);Assert.Greater(view.FootPosition(false).y,-.025f);
                    Assert.AreEqual(snapshot,JsonUtility.ToJson(actor));Assert.AreEqual(ballSnapshot,JsonUtility.ToJson(state));
                    if(i==0||time==.48f){var hand=view.DistributionHandPosition;view.Render(actor,1,0,ball);Assert.That(Vector3.Distance(hand,view.DistributionHandPosition),Is.LessThan(.001f),"Paused contact drift");}
                    last=time;
                }
                for(int i=1;i<=Mathf.CeilToInt(.6f*fps);i++){
                    float time=.48f+Mathf.Min(.6f,i/(float)fps);actor.actionTime=MatchSimulation.KeeperDistributionDuration-time;
                    var ball=new Vector3(actor.actionTarget.x,actor.actionHeight,actor.actionTarget.z+12*(time-.48f));
                    view.Render(actor,1,time-last,ball);last=time;
                    Assert.Greater(view.FootPosition(true).y,-.025f);Assert.Greater(view.FootPosition(false).y,-.025f);
                    Assert.IsFalse(float.IsNaN(view.DistributionHandPosition.x));
                    if(time>.8f)Assert.That(Vector3.Distance(view.DistributionHandPosition,ball),Is.GreaterThan(1),"Hand cannot keep following released ball");
                }
                actor.action="idle";actor.actionTime=0;view.Render(actor,1,1f/fps,new Vector3(100,1,100));
                Assert.That(Vector3.Distance(view.transform.position,Vector3.zero),Is.LessThan(.001f));
            }finally{UnityEngine.Object.DestroyImmediate(go);}
        }
    }
}

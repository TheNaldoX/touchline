using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Touchline.Core;

namespace Touchline.Tests
{
    public class KeeperDiveContactTests
    {
        [Test] public void MeasuredCaptureVariantsHaveFiniteAnatomicalLimbs()
        {
            Assert.IsTrue(KeeperBodyMotion.Available);var joints=new Vector3[19];
            for(int variant=0;variant<6;variant++)for(int frame=0;frame<=60;frame++)foreach(int side in new[]{-1,1}){
                Assert.IsTrue(KeeperBodyMotion.Sample(frame/60f,variant,side,joints,out var rotation));
                foreach(var point in joints)Assert.IsFalse(float.IsNaN(point.sqrMagnitude)||float.IsInfinity(point.sqrMagnitude));
                foreach(int hip in new[]{11,15}){Assert.That(Vector3.Distance(joints[hip],joints[hip+1]),Is.InRange(.32f,.55f));Assert.That(Vector3.Distance(joints[hip+1],joints[hip+2]),Is.InRange(.32f,.55f));}
                Assert.That(rotation.x*rotation.x+rotation.y*rotation.y+rotation.z*rotation.z+rotation.w*rotation.w,Is.EqualTo(1).Within(.001f));
            }
        }
        [TestCase(-1,.22f)] [TestCase(1,.22f)] [TestCase(-1,1.1f)] [TestCase(1,1.1f)] [TestCase(-1,2.2f)] [TestCase(1,2.2f)]
        [TestCase(-1,.22f,90)] [TestCase(1,1.1f,180)] [TestCase(-1,2.2f,270)]
        public void CatchMeetsBallAtScheduledContactAndFinishesUpright(int side,float height,float angle=0)
        {
            var go=new GameObject("Scheduled goalkeeper catch");try{
                var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="dive-"+side,heightCm=185},0,0,Color.yellow);
                go.transform.rotation=Quaternion.Euler(0,angle,0);var a=new Actor{slot=0,action="idle",angle=angle*Mathf.Deg2Rad};view.Render(a,1,.01f);
                var target=Quaternion.Euler(0,angle,0)*new Vector3(side*1.1f,0,.3f);
                a.action="dive";a.actionSequence=1;a.actionKind="save-catch";a.actionContactTime=.18f;a.actionTarget=new Point(target.x,target.z);a.actionHeight=height;a.diveSide=side;
                var ball=new Vector3(a.actionTarget.x,height,a.actionTarget.z);
                for(int i=0;i<=18;i++){a.actionTime=1.2f-i*.01f;view.Render(a,1,.01f,ball);}
                Assert.Less(Vector3.Distance(view.HeldBallPosition,ball),.17f,"The save must be visible at the actual impact height");
                for(int i=19;i<=120;i++){a.actionTime=1.2f-i*.01f;view.Render(a,1,.01f,ball);}
                Assert.That(go.transform.Find("Rig").localPosition.magnitude,Is.LessThan(.02f));Assert.That(Quaternion.Angle(go.transform.Find("Rig").localRotation,Quaternion.identity),Is.LessThan(.1f));
            }finally{Object.DestroyImmediate(go);}
        }
        [Test] public void SavedContactRecordsExactSubstepTimeAndParryOutcome()
        {
            var db=new Database{players=Enumerable.Range(0,40).Select(i=>new PlayerData{id="p"+i,name="P"+i,team=i<20?"a":"b",position=i%20==0?"GB":i%20<5?"DEF":i%20<9?"MIL":"ATT",rating=70}).ToArray()};
            var career=new Career{club="a"};career.lineup=Career.Select(db,"a",career.tactic);var sim=MatchSimulation.Create(db,career,"b");var m=sim.State;
            foreach(var a in m.actors)a.previous=a.position=new Point(20,a.slot*2-10);
            var keeper=m.actors[0];keeper.previous=keeper.position=new Point(-48,0);keeper.angle=Mathf.PI/2;keeper.action="dive";keeper.actionTime=.9f;keeper.actionSequence=2;
            m.ball=new BallState{kind="shot",side=1,from=m.actors[20].id,start=new Point(-30,1),previous=new Point(-45,1),position=new Point(-49,1),height=1.1f,previousHeight=1.1f,elapsed=.7f,velocity=new Point(-30,0)};
            Assert.IsTrue((bool)typeof(MatchSimulation).GetMethod("ResolveShotContact",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(sim,null));
            Assert.AreEqual("save-parry",keeper.actionKind);Assert.That(keeper.actionContactTime,Is.InRange(.2f,.3f));Assert.AreEqual(1,m.metrics[0].saves);
            var restored=JsonUtility.FromJson<Actor>(JsonUtility.ToJson(keeper));Assert.AreEqual(keeper.actionContactTime,restored.actionContactTime);Assert.AreEqual(keeper.actionKind,restored.actionKind);
        }
    }
}

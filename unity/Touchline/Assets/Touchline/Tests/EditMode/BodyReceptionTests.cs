using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Touchline.Core;
using UnityEngine;

namespace Touchline.Tests
{
    public class BodyReceptionTests
    {
        [TestCase(MatchSimulation.ChestControl)] [TestCase(MatchSimulation.ThighControl)]
        public void CapturedBodyEntryKeepsSupportAtZeroPhysicalProgress(string kind)
        {
            var go=new GameObject("Captured reception entry");bool old=PlayerView.UseMecanim;PlayerView.UseMecanim=true;
            try{
                var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="entry",heightCm=180},0,9,Color.white);
                var actor=new Actor{slot=9,action="idle"};for(int i=0;i<30;i++)view.Render(actor,1,1f/60);
                var foot=view.FootPosition(false);actor.action="control";actor.actionKind=kind;actor.actionSequence=1;actor.actionHeight=kind==MatchSimulation.ChestControl?1.4f:.94f;actor.actionTarget=new Point(.12f,.3f);actor.actionTime=MatchSimulation.BodyControlDuration(kind);
                view.Render(actor,1,1f/60,new Vector3(.12f,actor.actionHeight,.3f));
                Assert.Less(Vector3.Distance(foot,view.FootPosition(false)),.025f,"No support jump before the reception begins");
            }finally{Object.DestroyImmediate(go);PlayerView.UseMecanim=old;}
        }
        [TestCase(MatchSimulation.ChestControl,160,true)] [TestCase(MatchSimulation.ChestControl,205,false)]
        [TestCase(MatchSimulation.ThighControl,160,false)] [TestCase(MatchSimulation.ThighControl,205,true)]
        [TestCase(MatchSimulation.ChestControl,180,false)] [TestCase(MatchSimulation.ThighControl,180,true)]
        public void BodyTouchMeetsBallWithSupportFootGroundedAndSmoothRecovery(string kind,int stature,bool left)
        {
            var go=new GameObject("Cushioned reception");try{
                var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="body-control",heightCm=stature,preferredFoot=left?"Left":"Right"},0,9,Color.white);
                float height=stature*.01f*(kind==MatchSimulation.ChestControl?.78f:.52f),duration=MatchSimulation.BodyControlDuration(kind);
                var actor=new Actor{slot=9,action="control",actionKind=kind,actionSequence=1,actionHeight=height,actionTarget=new Point(left?.12f:-.12f,.30f)};
                var point=new Vector3(actor.actionTarget.x,height,.30f);float maxStep=0;Vector3 last=default;
                var supportBoot=go.GetComponentsInChildren<MeshFilter>().Single(b=>b.name=="Football boot"&&b.transform.parent.name==(left?"foot.R":"foot.L"));
                var receivingBoot=go.GetComponentsInChildren<MeshFilter>().Single(b=>b.name=="Football boot"&&b.transform.parent.name==(left?"foot.L":"foot.R"));
                var vertices=supportBoot.sharedMesh.vertices;
                for(int frame=0;frame<=80;frame++){
                    float time=frame*.01f;actor.actionTime=Mathf.Max(0,duration-time);if(time>duration)actor.action="idle";
                    var ball=new BallState{setupHeight=height,controlDuration=duration,controlElapsed=time};
                    var ballPoint=Vector3.Lerp(point,new Vector3(point.x,.11f,.4f),MatchSimulation.BodyControlProgress(actor,ball));
                    view.Render(actor,1,.01f,ballPoint);
                    var touch=kind==MatchSimulation.ChestControl?view.ChestContactPosition:view.ThighContactPosition(left);
                    if(frame>0)maxStep=Mathf.Max(maxStep,Vector3.Distance(touch,last));last=touch;
                    if(frame==8)Assert.Less(Vector3.Distance(touch,point),.10f,"Contact must meet the incoming ball, not follow an arbitrary control pose");
                    if(time<=duration){
                        // A tilted boot can have a high ankle while its toe is planted.
                        Assert.That(vertices.Min(v=>supportBoot.transform.TransformPoint(v).y),Is.InRange(-.005f,.06f),"Visible supporting sole at "+time+" seconds");
                        Assert.GreaterOrEqual(vertices.Min(v=>receivingBoot.transform.TransformPoint(v).y),-.005f);
                    }
                    Assert.AreEqual(Vector3.zero,go.transform.position,"Presentation must not move the simulation actor");
                }
                Assert.Less(maxStep,.18f,"No teleport between cushioning and locomotion");
            }finally{Object.DestroyImmediate(go);}
        }

        [TestCase(MatchSimulation.ChestControl)] [TestCase(MatchSimulation.ThighControl)]
        public void BallRetainsImpactHeightThenDropsMonotonicallyWithoutSaveDiscontinuity(string kind)
        {
            float duration=MatchSimulation.BodyControlDuration(kind);var actor=new Actor{action="control",actionKind=kind};
            var ball=new BallState{setupHeight=kind==MatchSimulation.ChestControl?1.4f:.95f,controlDuration=duration};float last=ball.setupHeight;
            for(int i=0;i<=100;i++){
                ball.controlElapsed=duration*i/100f;float y=MatchSimulation.ControlledBallHeight(actor,ball);
                Assert.LessOrEqual(y,last+.00001f);Assert.GreaterOrEqual(y,.1099f);
                if(ball.controlElapsed<=MatchSimulation.BodyControlContactTime)Assert.AreEqual(ball.setupHeight,y);
                var copy=JsonUtility.FromJson<BallState>(JsonUtility.ToJson(ball));Assert.AreEqual(y,MatchSimulation.ControlledBallHeight(actor,copy));last=y;
            }
            Assert.That(last,Is.EqualTo(.11f).Within(.0001f));
        }

        [TestCase(.30f,1.40f,true)] [TestCase(.80f,1.40f,false)] [TestCase(.30f,1.85f,false)] [TestCase(.30f,.40f,false)]
        public void HighReceptionRequiresBallWithinBodyHeightAndReach(float forward,float height,bool reachable)
        {
            var db=new Database{players=Enumerable.Range(0,40).Select(i=>new PlayerData{id="body"+i,name="Body "+i,team=i<20?"a":"b",position=i%20==0?"GB":i%20<5?"DEF":i%20<9?"MIL":"ATT",rating=75,heightCm=180}).ToArray()};
            var career=new Career{club="a"};career.lineup=Career.Select(db,"a",career.tactic);var sim=MatchSimulation.Create(db,career,"b");
            var actor=sim.State.actors[9];actor.position=new Point();actor.angle=0;
            sim.State.ball=new BallState{position=new Point(0,forward),previous=new Point(0,forward),height=height,previousHeight=height};
            float f=(float)typeof(MatchSimulation).GetMethod("BodyReceptionFraction",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(sim,new object[]{actor});
            Assert.AreEqual(reachable,f<=1);
        }
    }
}

using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Touchline.Core;
using UnityEngine;

namespace Touchline.Tests
{
    public class BodyAndGroundIntegrationTests
    {
        Database db;MatchSimulation sim;Actor receiver;
        static object Invoke(MatchSimulation sim,string method,params object[] args)=>typeof(MatchSimulation).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(sim,args);
        [SetUp] public void Setup()
        {
            db=new Database{players=Enumerable.Range(0,40).Select(i=>new PlayerData{id="integrated"+i,name="I"+i,team=i<20?"a":"b",position=i%20==0?"GB":i%20<5?"DEF":i%20<9?"MIL":"ATT",rating=80,heightCm=180}).ToArray()};
            var career=new Career{club="a"};career.lineup=Career.Select(db,"a",career.tactic);sim=MatchSimulation.Create(db,career,"b");
            sim.State.restart=0;sim.State.phase="play";foreach(var a in sim.State.actors){a.sentOff=true;a.position=a.previous=new Point(0,25);}
            receiver=sim.State.actors[9];receiver.sentOff=false;receiver.position=receiver.previous=new Point();receiver.angle=0;receiver.velocity=new Point();
        }
        [TestCase(1.4f,MatchSimulation.ChestControl)] [TestCase(.95f,MatchSimulation.ThighControl)]
        public void MatchLoopReceivesAtImpactHeightAndSynchronizesCushioningToTheRenderedBody(float height,string kind)
        {
            sim.State.ball=new BallState{kind="loose",side=0,to=receiver.id,position=new Point(.12f,.30f),previous=new Point(.12f,.30f),end=new Point(),height=height,previousHeight=height};
            sim.Advance(.1);
            Assert.AreEqual(receiver.id,sim.State.ball.owner);Assert.AreEqual(kind,receiver.actionKind);Assert.That(receiver.actionHeight,Is.EqualTo(height).Within(.001f));
            Assert.Greater(sim.State.ball.height,.65f,"A body reception must not teleport immediately to the turf");
            var go=new GameObject("Integrated cushioning");try{
                var view=go.AddComponent<PlayerView>();view.Build(db.Find(receiver.id),0,9,Color.white);
                var firstBall=new Vector3(sim.State.ball.position.x,sim.State.ball.height,sim.State.ball.position.z);view.Render(receiver,1,.016f,firstBall);
                // First presentation now restores the authoritative facing immediately.
                // Select the expected leg from that facing and the saved impact;
                // the old fixture assumed left while the root was still turning.
                var impact=new Vector3(receiver.actionTarget.x,receiver.actionHeight,receiver.actionTarget.z);
                float lateral=view.transform.InverseTransformPoint(impact).x;
                bool preferredLeft=db.Find(receiver.id).preferredFoot=="Left"||db.Find(receiver.id).preferredFoot=="Gauche";
                bool expectedLeft=Mathf.Abs(lateral)<.06f?preferredLeft:lateral>0;
                bool actualLeft=(bool)typeof(PlayerView).GetField("receivingLeft",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(view);
                Assert.AreEqual(expectedLeft,actualLeft,"The receiving leg follows the saved impact and preferred foot at initial authoritative facing");
                sim.Advance(.1);var b=sim.State.ball;
                for(int frame=1;frame<=8;frame++){
                    float alpha=frame*.1f;var point=Point.Lerp(b.previous,b.position,alpha);var ball=new Vector3(point.x,Mathf.Lerp(b.previousHeight,b.height,alpha),point.z);
                    view.Render(receiver,alpha,.01f,ball);
                    if(frame==8){
                        float leftDistance=Vector3.Distance(view.ThighContactPosition(true),ball),rightDistance=Vector3.Distance(view.ThighContactPosition(false),ball);
                        TestContext.WriteLine("kind="+kind+" yaw="+view.transform.eulerAngles.y+" impactLocalX="+lateral+" expectedLeft="+expectedLeft+" actualLeft="+actualLeft+" leftDistance="+leftDistance+" rightDistance="+rightDistance);
                        var contact=kind==MatchSimulation.ChestControl?view.ChestContactPosition:view.ThighContactPosition(expectedLeft);
                        Assert.Less(Vector3.Distance(contact,ball),.11f);
                    }
                }
                var resumed=new MatchSimulation(db,JsonUtility.FromJson<MatchState>(JsonUtility.ToJson(sim.State)));
                sim.Advance(.3);resumed.Advance(.3);Assert.AreEqual(JsonUtility.ToJson(sim.State),JsonUtility.ToJson(resumed.State));
                Assert.Less(sim.State.ball.height,height*.6f);Assert.GreaterOrEqual(sim.State.ball.height,.11f-.0001f);
            }finally{Object.DestroyImmediate(go);}
        }
        [TestCase("fall")] [TestCase("slide")]
        public void GroundedActorDoesNotMoveToOrRecoverALooseBallEvenWithoutLegacyControlTimer(string action)
        {
            receiver.action=action;receiver.actionTime=1;receiver.controlTime=0;receiver.velocity=new Point();
            sim.State.ball=new BallState{kind="loose",side=1,position=new Point(0,.20f),previous=new Point(0,.20f),height=.11f,previousHeight=.11f};
            sim.Advance(.1);Assert.IsNull(sim.State.ball.owner);Assert.AreEqual(0,receiver.position.Length);Assert.AreEqual(action,receiver.action);Assert.Less(receiver.actionTime,1);
        }
        [Test] public void StandingDuelMaintenanceCannotConsumeASlidingContact()
        {
            var defender=sim.State.actors[12];defender.sentOff=false;defender.position=receiver.position=new Point();defender.action="slide";defender.actionKind=MatchSimulation.SlidingDuel;defender.actionTime=.4f;defender.tackleOpponent=receiver.id;
            sim.State.ball.owner=receiver.id;sim.State.ball.position=new Point(.3f,0);sim.State.ball.height=.11f;uint seed=sim.State.seed;
            Invoke(sim,"CancelInvalidStandingDuels");Assert.AreEqual(receiver.id,defender.tackleOpponent);
            Assert.IsFalse((bool)Invoke(sim,"ResolveStandingDuels",receiver));Assert.AreEqual(receiver.id,defender.tackleOpponent);Assert.AreEqual(seed,sim.State.seed);
        }
        [Test] public void RestartChoosesAnAvailablePlayerInsteadOfTheFallenPreferredTaker()
        {
            var alternative=sim.State.actors[8];alternative.sentOff=false;alternative.position=new Point(0,3);receiver.action="fall";receiver.actionTime=1;
            sim.State.restartTaker=receiver.id;sim.State.restartSide=0;sim.State.phase="throw-in";sim.State.ball.position=new Point();
            Assert.AreEqual(alternative.id,((Actor)Invoke(sim,"RestartTaker")).id);
        }
    }
}

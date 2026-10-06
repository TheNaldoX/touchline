using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Touchline.Core;

namespace Touchline.Tests
{
    public class AerialReachTests
    {
        Database db;MatchSimulation sim;Actor player;
        bool Resolve(MatchSimulation value=null)=>(bool)typeof(MatchSimulation).GetMethod("ResolveAerial",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(value??sim,new object[]{.95f});
        [SetUp] public void Setup()
        {
            db=new Database{players=Enumerable.Range(0,40).Select(i=>new PlayerData{id="aerial"+i,name="A"+i,team=i<20?"a":"b",position=i%20==0?"GB":i%20<5?"DEF":i%20<9?"MIL":"ATT",rating=75,heightCm=180}).ToArray()};
            var career=new Career{club="a"};career.lineup=Career.Select(db,"a",career.tactic);sim=MatchSimulation.Create(db,career,"b",171);
            sim.State.restart=0;sim.State.phase="play";foreach(var a in sim.State.actors){a.sentOff=true;a.position=a.previous=new Point(0,20);}
            player=sim.State.actors[9];player.sentOff=false;player.position=player.previous=new Point(42,0);player.angle=Mathf.PI*.5f;
            sim.State.ball=new BallState{kind="cross",side=0,position=new Point(42.16f,0),previous=new Point(42.16f,0),height=2,previousHeight=2};
        }
        [TestCase(1.4f,2)] [TestCase(0,1.1f)] [TestCase(0,2.7f)]
        public void UnreachableBallDoesNotBecomeAHeader(float lateral,float height)
        {
            var b=sim.State.ball;b.previous=b.position=new Point(42.16f,lateral);b.height=b.previousHeight=height;uint seed=sim.State.seed;
            Assert.IsFalse(Resolve());Assert.AreEqual(seed,sim.State.seed);Assert.AreEqual("cross",b.kind);
        }
        [TestCase(160,false)] [TestCase(205,true)] public void PlayerStatureLimitsVerticalReach(int stature,bool reachable)
        {
            db.Find(player.id).heightCm=stature;sim.State.ball.height=sim.State.ball.previousHeight=2.25f;Assert.AreEqual(reachable,Resolve());
        }
        [Test] public void FastCrossContactsBetweenTicksInsteadOfSkippingOrSnappingToTheEndpoint()
        {
            var b=sim.State.ball;b.previous=new Point(42.16f,-1);b.position=new Point(42.16f,1);Assert.IsTrue(Resolve());
            Assert.That(b.start.z,Is.InRange(-.3f,-.15f));Assert.That(b.startHeight,Is.EqualTo(2).Within(.001f));Assert.AreEqual(b.start.z,player.actionTarget.z);Assert.AreEqual("header",player.action);
        }
        [Test] public void DescendingCrossOnlyContactsInsideTheVerticalWindow()
        {
            var b=sim.State.ball;b.previousHeight=2.6f;b.height=1.9f;Assert.IsTrue(Resolve());Assert.That(player.actionHeight,Is.InRange(1.62f,2.1f));
        }
        [Test] public void EarlierReachWinsBeforeTheBallArrivesAtAStrongerOpponent()
        {
            player.position=new Point(42,-.5f);var opponent=sim.State.actors[12];opponent.sentOff=false;opponent.position=new Point(42.32f,.5f);opponent.angle=-Mathf.PI*.5f;db.Find(opponent.id).rating=99;db.Find(player.id).rating=40;
            var b=sim.State.ball;b.previousHeight=b.height=1.9f;b.previous=new Point(42.16f,-1.2f);b.position=new Point(42.16f,1.2f);Assert.IsTrue(Resolve());Assert.AreEqual(player.id,b.from);
        }
        [TestCase("dive")] [TestCase("hurt")] [TestCase("header")] public void BusyPlayerCannotStartAnotherAerialGesture(string action)
        {
            player.action=action;player.actionTime=.4f;Assert.IsFalse(Resolve());
        }
        [Test] public void SavedFlightProducesTheSameContact()
        {
            var copy=new MatchSimulation(db,JsonUtility.FromJson<MatchState>(JsonUtility.ToJson(sim.State)));Assert.IsTrue(Resolve());Assert.IsTrue(Resolve(copy));Assert.AreEqual(JsonUtility.ToJson(sim.State),JsonUtility.ToJson(copy.State));
        }
        [TestCase(160,1.8f)] [TestCase(180,2.05f)] [TestCase(205,2.25f)]
        public void SelectedContactIsReachableByTheRenderedHead(int stature,float height)
        {
            db.Find(player.id).heightCm=stature;player.velocity=new Point(0,6);sim.State.ball.height=sim.State.ball.previousHeight=height;Assert.IsTrue(Resolve());
            var go=new GameObject("Resolved aerial contact");try{
                var view=go.AddComponent<PlayerView>();view.Build(db.Find(player.id),0,9,Color.white);go.transform.rotation=Quaternion.Euler(0,player.angle*Mathf.Rad2Deg,0);
                var point=new Vector3(player.actionTarget.x,player.actionHeight,player.actionTarget.z);
                for(int i=0;i<=12;i++){player.actionTime=.64f-i*.01f;view.Render(player,1,.01f,point);}
                Assert.Less(Vector3.Distance(view.HeaderContactPosition,point),.08f);
            }finally{Object.DestroyImmediate(go);}
        }
    }
}

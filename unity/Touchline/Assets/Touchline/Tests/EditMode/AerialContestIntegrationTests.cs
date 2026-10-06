using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Touchline.Core;

namespace Touchline.Tests
{
    public class AerialContestIntegrationTests
    {
        Database db;MatchSimulation sim;
        static object Invoke(MatchSimulation sim,string name,object[] args)=>typeof(MatchSimulation).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(sim,args);
        [SetUp] public void Setup()
        {
            db=new Database{players=Enumerable.Range(0,40).Select(i=>new PlayerData{id="duel"+i,name="D"+i,team=i<20?"a":"b",position=i%20==0?"GB":i%20<5?"DEF":i%20<9?"MIL":"ATT",rating=80,heightCm=180}).ToArray()};
            var career=new Career{club="a"};career.lineup=Career.Select(db,"a",career.tactic);sim=MatchSimulation.Create(db,career,"b",171,2700);
            foreach(var p in sim.State.actors){p.sentOff=true;p.position=p.previous=new Point(0,20);p.angle=sim.Direction(p.side)*Mathf.PI*.5f;}
            sim.State.restart=0;sim.State.phase="play";
        }
        [Test] public void WindowStaysAnchoredToFirstReachInsteadOfWalkingToALaterStrongerPlayer()
        {
            var candidates=new[]{sim.State.actors[7],sim.State.actors[8],sim.State.actors[9]};
            for(int i=0;i<3;i++){var p=candidates[i];p.sentOff=false;p.position=p.previous=new Point(-.25f,-.5f+i*.06f);db.Find(p.id).rating=35+i*30;}
            sim.State.ball=new BallState{kind="cross",previous=new Point(0,-1),position=new Point(0,1),height=1.8f,previousHeight=1.8f};
            var args=new object[]{0f,null};var winner=(Actor)Invoke(sim,"SelectAerialDuel",args);
            Assert.AreEqual(candidates[1].id,winner.id);Assert.LessOrEqual((float)args[0],.1651f,"Later candidates must not shift the eligibility deadline");
        }
        [Test] public void RealOpponentInReachContestsButReceivesNoBallOwnershipOrHeaderCredit()
        {
            var a=sim.State.actors[9];var b=sim.State.actors[12];a.sentOff=b.sentOff=false;
            a.position=a.previous=new Point(42,0);b.position=b.previous=new Point(42.56f,0);a.angle=Mathf.PI*.5f;b.angle=-Mathf.PI*.5f;
            db.Find(a.id).rating=95;db.Find(b.id).rating=55;
            sim.State.ball=new BallState{kind="cross",side=0,previous=new Point(42.28f,0),position=new Point(42.28f,0),height=2,previousHeight=2};
            Assert.IsTrue((bool)Invoke(sim,"ResolveAerial",new object[]{.95f}));
            Assert.AreEqual(a.id,sim.State.ball.from);Assert.AreEqual("header",a.action);Assert.AreEqual(MatchSimulation.AerialContest,b.action);
            Assert.AreNotEqual(b.id,sim.State.ball.owner);Assert.AreNotEqual(b.id,sim.State.ball.lastTouchId);
            var encoded=JsonUtility.ToJson(b);var restored=JsonUtility.FromJson<Actor>(encoded);uint after=sim.State.seed;
            var origin=b.position;for(int i=0;i<7;i++){Invoke(sim,"AdvanceAerialAction",new object[]{b});Invoke(sim,"AdvanceAerialAction",new object[]{restored});}
            Assert.AreEqual(JsonUtility.ToJson(b),JsonUtility.ToJson(restored));Assert.AreEqual(origin.x,b.position.x);Assert.AreEqual(origin.z,b.position.z);Assert.AreEqual(after,sim.State.seed,"Presentation/recovery cannot roll another outcome");Assert.AreEqual("idle",b.action);
        }
        [Test] public void HeaderPlantCannotBeDraggedAwayByAvoidanceTargets()
        {
            var player=sim.State.actors[9];player.sentOff=false;player.position=player.previous=new Point(10,2);player.action="header";player.actionTime=.64f;player.velocity=new Point(1,-2);
            for(int i=0;i<6;i++){
                Invoke(sim,"MoveActor",new object[]{player,new Point(15,-3),7f});
                Assert.AreEqual(10,player.position.x);Assert.AreEqual(2,player.position.z);Assert.AreEqual(0,player.velocity.Length);Assert.AreEqual("header",player.action);
            }
            Invoke(sim,"MoveActor",new object[]{player,new Point(15,-3),7f});Assert.AreEqual("idle",player.action);
        }
        [Test] public void UnfavourableAttackingHeaderUsesAnAvailableKnockDown()
        {
            var player=sim.State.actors[9];var mate=sim.State.actors[8];player.sentOff=mate.sentOff=false;
            player.position=player.previous=new Point(42,17);mate.position=mate.previous=new Point(37,14);
            sim.State.ball=new BallState{kind="cross",side=0,previous=new Point(42.25f,17),position=new Point(42.25f,17),height=1.8f,previousHeight=1.8f};
            Assert.LessOrEqual(sim.ShotQuality(player,true),.07f);Assert.IsTrue((bool)Invoke(sim,"ResolveAerial",new object[]{.95f}));
            Assert.AreEqual("pass",sim.State.ball.kind);Assert.AreEqual(mate.id,sim.State.ball.to);Assert.IsFalse(sim.State.ball.goalAttempt);Assert.AreEqual(1,sim.State.passes[0]);Assert.AreEqual(0,sim.State.shots[0]);
            Assert.AreEqual(.12f,player.actionContactTime);Assert.AreEqual(player.actionContactTime,sim.State.ball.releaseDelay);
        }
        [Test] public void IsolatedHeaderClearsTowardTouchlineInsteadOfMakingAnUncountedShot()
        {
            var player=sim.State.actors[9];player.sentOff=false;player.position=player.previous=new Point(48,17);
            sim.State.ball=new BallState{kind="cross",side=0,previous=new Point(48.25f,17),position=new Point(48.25f,17),height=1.8f,previousHeight=1.8f};
            Assert.LessOrEqual(sim.ShotQuality(player,true),.07f);Assert.IsTrue((bool)Invoke(sim,"ResolveAerial",new object[]{.95f}));
            Assert.AreEqual("clearance",sim.State.ball.kind);Assert.AreEqual(36,sim.State.ball.end.z);Assert.Less(sim.State.ball.end.x,52.5f);Assert.IsFalse(sim.State.ball.goalAttempt);Assert.AreEqual(0,sim.State.shots[0]);
        }
        [Test] public void DeliberateHeaderAtGoalIsCountedAsAShot()
        {
            var player=sim.State.actors[9];player.sentOff=false;player.position=player.previous=new Point(45,0);
            sim.State.ball=new BallState{kind="cross",side=0,previous=new Point(45.25f,0),position=new Point(45.25f,0),height=1.8f,previousHeight=1.8f};
            Assert.Greater(sim.ShotQuality(player,true),.07f);Assert.IsTrue((bool)Invoke(sim,"ResolveAerial",new object[]{.95f}));
            Assert.AreEqual("shot",sim.State.ball.kind);Assert.IsTrue(sim.State.ball.goalAttempt);Assert.AreEqual(1,sim.State.shots[0]);Assert.Greater(sim.State.metrics[0].xg,0);
        }
        [TestCase(false,"shot")] [TestCase(true,"pass")]
        public void WorkIntoBoxPrefersAKnockDownToALowQualityHeader(bool patient,string expected)
        {
            var player=sim.State.actors[9];var mate=sim.State.actors[8];player.sentOff=mate.sentOff=false;
            player.position=player.previous=new Point(38,0);mate.position=mate.previous=new Point(34,8);
            sim.State.homeTactic.workIntoBox=patient;
            sim.State.ball=new BallState{kind="cross",side=0,previous=new Point(38.25f,0),position=new Point(38.25f,0),height=1.8f,previousHeight=1.8f};
            Assert.That(sim.ShotQuality(player,true),Is.InRange(.0701f,.1199f));
            Assert.IsTrue((bool)Invoke(sim,"ResolveAerial",new object[]{.95f}));
            Assert.AreEqual(expected,sim.State.ball.kind);Assert.AreEqual(patient?0:1,sim.State.shots[0]);
            if(patient)Assert.AreEqual(mate.id,sim.State.ball.to);
        }
        [TestCase(160,30)] [TestCase(180,60)] [TestCase(205,120)]
        public void LosingContestantYieldsHeadSpaceAndLandsWithoutRootWarp(int stature,int fps)
        {
            var go=new GameObject("Aerial contestant");try{
                var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="contestant",heightCm=stature},1,2,Color.white);
                var actor=new Actor{slot=2,side=1,position=new Point(.56f,0),previous=new Point(.56f,0),angle=-Mathf.PI*.5f,action=MatchSimulation.AerialContest,actionKind=MatchSimulation.AerialContest,actionSequence=1,actionContactTime=.12f,actionTarget=new Point(.28f,0),actionHeight=stature*.01f+.20f};
                go.transform.rotation=Quaternion.Euler(0,-90,0);var point=new Vector3(.28f,actor.actionHeight,0);float maxJump=0;Vector3 last=default;
                for(int i=0;i<=Mathf.CeilToInt(.8f*fps);i++){
                    float t=i/(float)fps;actor.actionTime=Mathf.Max(0,.64f-t);if(t>.64f)actor.action="idle";
                    view.Render(actor,1,1f/fps,point);var head=view.HeaderContactPosition;if(i>0)maxJump=Mathf.Max(maxJump,Vector3.Distance(last,head));last=head;
                    Assert.AreEqual(new Vector3(.56f,0,0),go.transform.position);Assert.Greater(view.FootPosition(true).y,.025f);Assert.Greater(view.FootPosition(false).y,.025f);
                    if(Mathf.Abs(t-.12f)<1f/fps)Assert.Greater(Vector3.Distance(head,point),.20f,"The contestant must not impersonate a second head-to-ball contact");
                }
                Assert.Less(maxJump,.15f);
            }finally{Object.DestroyImmediate(go);}
        }
    }
}

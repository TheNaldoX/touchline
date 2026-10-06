using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Touchline.Core;

namespace Touchline.Tests
{
    public class KeeperBallPresentationTests
    {
        static MatchState State()=>new MatchState{clock=100,restart=0,phase="play",ball=new BallState{previous=new Point(45,0),position=new Point(48,0),previousHeight=1,height=1,held=true,owner="keeper"}};
        static KeeperPresentationContact Contact(bool caught=true)=>new KeeperPresentationContact{valid=true,clock=100,keeper="keeper",caught=caught,fraction=.6f,start=new Point(45,0),impact=new Point(48,0),startHeight=1,height=1};
        [TestCase(30)][TestCase(60)][TestCase(120)]
        public void IncomingBallKeepsItsSpeedUntilImpactAtEveryRefreshRate(int fps)
        {
            var state=State();var contact=Contact();var hands=new Vector3(48.1f,1.1f,0);
            for(int i=0;i<=fps;i++){
                float alpha=i/(float)fps;var actual=KeeperBallPresentation.Position(contact,state,alpha,hands);
                var expected=alpha<=.6f?new Vector3(45+5*alpha,1,0):Vector3.Lerp(new Vector3(48,1,0),hands,(alpha-.6f)/.4f);
                Assert.Less(Vector3.Distance(expected,actual),.00001f,"Wrong contact timing at "+alpha);
            }
        }
        [Test] public void CatchIsContinuousAndDoesNotAttachBeforeImpact()
        {
            var state=State();var contact=Contact();var hands=new Vector3(48.1f,1.1f,0);
            Assert.AreEqual(new Vector3(45,1,0),KeeperBallPresentation.Position(contact,state,0,hands));
            Assert.Less(Vector3.Distance(KeeperBallPresentation.Position(contact,state,.59999f,hands),KeeperBallPresentation.Position(contact,state,.60001f,hands)),.001f);
            Assert.AreEqual(new Vector3(48,1,0),KeeperBallPresentation.Position(contact,state,.6f,hands));
            Assert.AreEqual(hands,KeeperBallPresentation.Position(contact,state,1,hands));
        }
        [Test] public void ParryUsesTheImpactAndNeverAttachesToTheHands()
        {
            var state=State();var contact=Contact(false);
            Assert.AreEqual(new Vector3(46.5f,1,0),KeeperBallPresentation.Position(contact,state,.3f,new Vector3(90,90,90)));
            Assert.AreEqual(new Vector3(48,1,0),KeeperBallPresentation.Position(contact,state,.9f,new Vector3(90,90,90)));
        }
        [Test] public void StaleContactsAndRestartsUseTheNormalPath()
        {
            var state=State();var contact=Contact();state.clock+=.1f;
            Assert.AreEqual(new Vector3(46.5f,1,0),KeeperBallPresentation.Position(contact,state,.5f));
            state.clock=100;state.restart=1;
            Assert.AreEqual(new Vector3(46.5f,1,0),KeeperBallPresentation.Position(contact,state,.5f));
            Assert.AreEqual(Vector3.one,KeeperBallPresentation.Position(default,state,.5f,Vector3.one));
        }
        [TestCase(0f)][TestCase(1f)] public void StepBoundaryContactsStayFinite(float fraction)
        {
            var state=State();var contact=Contact();contact.fraction=fraction;
            foreach(float alpha in new[]{0f,.5f,1f}){
                var point=KeeperBallPresentation.Position(contact,state,alpha,Vector3.one);
                Assert.IsFalse(float.IsNaN(point.x)||float.IsInfinity(point.x));
            }
        }
        [TestCase(0,1)][TestCase(1,1)][TestCase(0,2)][TestCase(1,2)]
        public void SimulationRecordsTheActualContactAndClearsItOnTheNextStep(int side,int period)
        {
            var db=new Database{players=Enumerable.Range(0,40).Select(i=>new PlayerData{id="timing"+i,name="K"+i,team=i<20?"a":"b",position=i%20==0?"GB":i%20<5?"DEF":i%20<9?"MIL":"ATT",rating=80}).ToArray()};
            var career=new Career{club="a"};career.lineup=Career.Select(db,"a",career.tactic);var sim=MatchSimulation.Create(db,career,"b",91,2700);
            var m=sim.State;m.period=period;m.restart=0;m.phase="play";m.clock=100;m.decision=10;
            foreach(var p in m.actors)p.sentOff=true;
            var keeper=m.actors[side*11];keeper.sentOff=false;int dir=-sim.Direction(side);keeper.position=keeper.previous=new Point(dir*49,0);keeper.action="idle";
            m.ball=new BallState{kind="shot",side=1-side,from="unrelated",previous=new Point(dir*47,.2f),position=new Point(dir*50,.2f),height=.6f,previousHeight=.6f,elapsed=.6f,velocity=new Point(dir*30,0)};
            bool hit=(bool)typeof(MatchSimulation).GetMethod("ResolveShotContact",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(sim,null);
            Assert.IsTrue(hit);var contact=sim.KeeperContact;Assert.IsTrue(contact.valid);Assert.AreEqual(keeper.id,contact.keeper);
            Assert.AreEqual(m.ball.held,contact.caught);Assert.AreEqual(m.ball.position,contact.impact);Assert.AreEqual(new Point(dir*47,.2f),contact.start);
            Assert.That(contact.fraction,Is.InRange(0f,1f));Assert.AreEqual((contact.impact.x-dir*47)/(dir*3),contact.fraction,.00001f);
            string saved=JsonUtility.ToJson(m);for(int i=0;i<100;i++)KeeperBallPresentation.Position(contact,m,i/100f,Vector3.one);
            Assert.AreEqual(saved,JsonUtility.ToJson(m),"Presentation must never mutate the simulation");
            sim.Advance(.1);Assert.IsFalse(sim.KeeperContact.valid,"The incoming trajectory must not be replayed in a later step");
        }
    }
}

using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Touchline.Core;

namespace Touchline.Tests
{
    public class BallReleasePresentationTests
    {
        static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        public static MatchSimulation Released(string spec,out Actor actor)
        {
            bool hand=spec.StartsWith("keeper-"),header=spec=="header";
            float delay=header?.12f:hand?.48f:spec=="throw"?.5f:.18f;
            var p=new PlayerData{id="release-audit",heightCm=182,rating=75,positions=new[]{"ST"}};
            actor=new Actor{id=p.id,slot=hand?0:9,side=0,controlTime=100,action=header?"header":hand?spec:spec=="throw"?"throw":"kick",actionKind=spec,actionSequence=1,actionContactTime=delay};
            var b=new BallState{kind=header||spec=="penalty"?"shot":spec=="fixed-restart"?"pass":spec,from=p.id,setupStart=new Point(0,.30f),position=new Point(0,.30f),previous=new Point(0,.30f),start=new Point(0,.42f),end=new Point(0,10),elapsed=-delay,releaseDelay=delay,duration=.5f,startHeight=hand?1.78f:header?1.6f:.11f,setupHeight=hand?1.1f:header?1.6f:.11f,endHeight=.11f,height=hand?1.1f:header?1.6f:.11f,previousHeight=hand?1.1f:header?1.6f:.11f,penalty=spec=="penalty",fixedStart=hand||spec=="fixed-restart"};
            var m=new MatchState{home="h",away="a",restart=0,phase="play",engineVersion=4,actors=new[]{actor},ball=b};
            var sim=new MatchSimulation(new Database{players=new[]{p}},m);var update=typeof(MatchSimulation).GetMethod("UpdateBall",Private);
            do{b.previous=b.position;b.previousHeight=b.height;update.Invoke(sim,null);}while(b.elapsed<0);
            return sim;
        }
        [TestCase("pass")][TestCase("shot")][TestCase("header")][TestCase("keeper-roll")]
        [TestCase("keeper-throw")][TestCase("throw")][TestCase("penalty")][TestCase("fixed-restart")]
        public void ReleaseMeetsTheContactInsteadOfStartingTheFlightEarly(string kind)
        {
            var sim=Released(kind,out _);var c=sim.ReleaseContact;var m=sim.State;
            Assert.IsTrue(BallReleasePresentation.Current(c,m));
            var release=new Vector3(c.release.x,c.releaseHeight,c.release.z);
            Assert.Less(Vector3.Distance(release,BallReleasePresentation.Position(c,m,c.fraction)),.00001f);
            var start=new Vector3(c.previous.x,c.previousHeight,c.previous.z);var end=new Vector3(c.end.x,c.endHeight,c.end.z);
            Assert.AreEqual(start,BallReleasePresentation.Position(c,m,0));Assert.AreEqual(end,BallReleasePresentation.Position(c,m,1));
            string state=JsonUtility.ToJson(m);
            foreach(int fps in new[]{30,60,120})for(int i=0;i<=fps;i++){
                float alpha=i/(float)fps;var point=BallReleasePresentation.Position(c,m,alpha);
                Assert.IsFalse(float.IsNaN(point.x)||float.IsNaN(point.y));
                if(alpha<c.fraction)Assert.LessOrEqual(point.z,release.z+.00001f,"Ball escaped before release");
                if(alpha>c.fraction&&alpha<1){
                    float u=(alpha-c.fraction)*.1f/System.Math.Max(.1f,m.ball.duration);
                    var expected=Point.Lerp(m.ball.start,m.ball.end,u);float height=m.ball.startHeight+(m.ball.endHeight-m.ball.startHeight)*u+Mathf.Sin(Mathf.PI*u)*m.ball.loft;
                    Assert.Less(Vector3.Distance(point,new Vector3(expected.x,height,expected.z)),.00001f);
                }
            }
            Assert.AreEqual(state,JsonUtility.ToJson(m),"Rendering must not alter physical or saved state");
            TestContext.WriteLine(kind+": fraction="+c.fraction+", old contact error="+Vector3.Distance(Vector3.Lerp(start,end,c.fraction),release));
        }
        [TestCase("block")][TestCase("interception")][TestCase("restart")][TestCase("later-step")]
        public void ReleaseCannotOverrideAContactOrSurviveIntoAnotherStep(string change)
        {
            var sim=Released("shot",out _);var c=sim.ReleaseContact;var m=sim.State;
            if(change=="block"){m.ball.kind="loose";m.ball.position=new Point(0,.5f);}
            if(change=="interception"){m.ball.owner="receiver";m.ball.kind="none";}
            if(change=="restart")m.restart=1;
            if(change=="later-step")m.clock+=.1f;
            Assert.IsFalse(BallReleasePresentation.Current(c,m));
            var expected=Vector3.Lerp(new Vector3(m.ball.previous.x,m.ball.previousHeight,m.ball.previous.z),new Vector3(m.ball.position.x,m.ball.height,m.ball.position.z),.4f);
            Assert.AreEqual(expected,BallReleasePresentation.Position(c,m,.4f));
        }
        [Test] public void KeeperContactHasPriorityOverAnyIncomingRelease()
        {
            var sim=Released("shot",out _);var m=sim.State;
            var contact=new KeeperPresentationContact{valid=true,clock=m.clock,fraction=.3f,start=m.ball.previous,startHeight=m.ball.previousHeight,impact=new Point(0,.4f),height=.11f};
            Assert.AreEqual(new Vector3(0,.11f,.4f),KeeperBallPresentation.Position(contact,m,.8f,release:sim.ReleaseContact));
        }
    }
}

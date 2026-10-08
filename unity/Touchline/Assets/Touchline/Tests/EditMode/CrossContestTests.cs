using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Touchline.Core;

namespace Touchline.Tests
{
    public class CrossContestTests
    {
        MatchSimulation sim;
        object Call(string name,params object[] args)=>typeof(MatchSimulation).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(sim,args);
        [SetUp] public void Setup()
        {
            var db=new Database{players=Enumerable.Range(0,40).Select(i=>new PlayerData{id="c"+i,name="C"+i,team=i<20?"a":"b",position=i%20==0?"GB":i%20<5?"DEF":i%20<9?"MIL":"ATT",rating=70,fitness=100}).ToArray()};
            var career=new Career{club="a"};career.lineup=Career.Select(db,"a",career.tactic);sim=MatchSimulation.Create(db,career,"b");var m=sim.State;
            m.restart=0;m.phase="play";m.clock=100;m.turnoverAt=-100;m.decision=100;
            foreach(var a in m.actors){a.position=a.previous=new Point(a.side==0?-30:30,(a.slot-5)*3);a.action="idle";a.actionTime=0;a.velocity=new Point();}
        }
        Actor Home(int slot)=>sim.State.actors[slot];Actor Away(int slot)=>sim.State.actors[11+slot];

        [Test] public void FreeTargetIsAMoreLikelyCrossThanAMarkedOne()
        {
            int dir=sim.Direction(0);var crosser=Home(8);crosser.position=new Point(dir*38,25);var target=Home(9);target.position=new Point(dir*46,0);
            float free=(float)Call("CrossOutlook",crosser,target,false);
            Away(3).position=new Point(dir*46.5f,.4f);
            float marked=(float)Call("CrossOutlook",crosser,target,false);
            Assert.Greater(free,marked+.15f);Assert.That(marked,Is.InRange(.05f,.85f));
        }
        [Test] public void KeeperNearTheLandingPointMakesTheHighBallRiskier()
        {
            int dir=sim.Direction(0);var crosser=Home(8);crosser.position=new Point(dir*38,25);var target=Home(9);target.position=new Point(dir*47,0);
            Away(0).position=new Point(dir*51,0);float keeperHome=(float)Call("CrossOutlook",crosser,target,false);
            Away(0).position=new Point(dir*47.5f,0);float keeperOut=(float)Call("CrossOutlook",crosser,target,false);
            Assert.Greater(keeperHome,keeperOut);
        }
        [Test] public void NearestDefenderAttacksTheFlightOfTheCross()
        {
            var m=sim.State;int dir=sim.Direction(0);
            var crosser=Home(8);crosser.position=new Point(dir*40,25);var target=Home(9);target.position=new Point(dir*45,3);
            var defender=Away(3);defender.position=defender.previous=new Point(dir*46,1);
            m.possessionSide=0;m.ball=new BallState{owner=null,from=crosser.id,to=target.id,side=0,lastTouch=0,kind="cross",start=new Point(dir*40,25),position=new Point(dir*42,14),previous=new Point(dir*41.8f,14.5f),end=new Point(dir*46,2),elapsed=.5f,duration=1.8f,startHeight=.11f,endHeight=1.6f,loft=3.5f,height=2.5f};
            sim.Advance(.1);
            Assert.AreEqual("attack-cross",defender.intent);
            Assert.Less(Point.Distance(sim.MovementTarget(11+3),m.ball.end),Point.Distance(sim.MovementTarget(11+3),target.position)+3);
        }
        [Test] public void ContestedDefensiveHeaderSometimesGoesBehindForACorner()
        {
            int behind=0,dir=0;
            for(uint i=1;i<=40;i++){
                Setup();var m=sim.State;m.seed=unchecked(i*2654435761u);dir=sim.Direction(1);
                var header=Away(3);header.position=new Point(-dir*46,2);
                var attacker=Home(9);attacker.position=new Point(-dir*46.4f,2.3f);
                m.ball=new BallState{owner=header.id,side=1,lastTouch=1,position=new Point(-dir*46,2),previous=new Point(-dir*46,2),height=2.1f,kind="cross"};
                Call("DistributeHeader",header,attacker);
                if(m.ball.end.x*dir< -52.5f){behind++;Assert.Greater(Math.Abs(m.ball.end.z),5f,"A glance must miss the goal mouth");}
            }
            Assert.Greater(behind,3);Assert.Less(behind,36);
        }
    }
}

using System;
using System.Linq;
using NUnit.Framework;
using Touchline.Core;

namespace Touchline.Tests
{
    public class AttackingRunTests
    {
        MatchSimulation Setup(int side,int period,int flank)
        {
            var db=new Database{players=Enumerable.Range(0,40).Select(i=>new PlayerData{id="r"+i,name="R"+i,team=i<20?"a":"b",position=i%20==0?"GB":i%20<5?"DEF":i%20<9?"MIL":"ATT",rating=70,fitness=100}).ToArray()};
            var career=new Career{club="a"};career.lineup=Career.Select(db,"a",career.tactic);var sim=MatchSimulation.Create(db,career,"b");var m=sim.State;
            m.period=period;m.restart=0;m.phase="play";m.clock=100;m.turnoverAt=-100;m.decision=100;m.homeTactic.SetFormation("4-3-3");m.awayTactic.SetFormation("4-3-3");int dir=sim.Direction(side);
            foreach(var a in m.actors){a.position=a.previous=a.side==side?sim.Tactic(side).Position(a.slot,true,30)*dir:new Point(48*dir,(a.slot*3-15)*dir);a.action="idle";a.actionTime=0;}
            m.actors[(1-side)*11].position=new Point(51*dir,0);
            var owner=m.actors[side*11+(flank>0?8:10)];owner.position=owner.previous=new Point(40*dir,25*flank*dir);owner.carryTarget=owner.position;
            m.ball=new BallState{owner=owner.id,position=owner.position,previous=owner.position,side=side,height=.11f};m.possessionSide=side;return sim;
        }
        [TestCase(0,1,1)] [TestCase(0,1,-1)] [TestCase(1,1,1)] [TestCase(1,1,-1)]
        [TestCase(0,2,1)] [TestCase(0,2,-1)] [TestCase(1,2,1)] [TestCase(1,2,-1)]
        public void DeliveryOccupiesDistinctZonesAndRetainsCover(int side,int period,int flank)
        {
            var sim=Setup(side,period,flank);sim.Advance(.1);var m=sim.State;int dir=sim.Direction(side),start=side*11,far=start+(flank>0?10:8);
            Assert.AreEqual("near-post",m.actors[start+9].intent);Assert.AreEqual("far-post",m.actors[far].intent);
            Assert.Greater(sim.MovementTarget(start+9).x*dir,42);Assert.Greater(sim.MovementTarget(start+9).z*dir*flank,1);
            Assert.Less(sim.MovementTarget(far).z*dir*flank,-2);Assert.Less(Math.Abs(sim.MovementTarget(far).z),8);
            Assert.AreEqual(1,m.actors.Count(a=>a.side==side&&a.intent=="box-arrival"));
            foreach(int slot in new[]{2,3,6})Assert.Less(sim.MovementTarget(start+slot).x*dir,34,"Holding players must cover the attack");
        }
        [Test] public void DefensiveDutyCancelsFarPostRun()
        {
            var sim=Setup(0,1,1);sim.State.homeTactic.withBall[10].duty="defend";sim.Advance(.1);
            Assert.AreNotEqual("far-post",sim.State.actors[10].intent);Assert.Less(sim.MovementTarget(10).x,34);
        }
        [Test] public void RunsStayOnsideUntilTheCrossLeavesTheFoot()
        {
            var sim=Setup(0,1,1);var m=sim.State;foreach(var a in m.actors)if(a.side==1&&a.slot>0)a.position=new Point(42,a.position.z);
            sim.Advance(.1);Assert.LessOrEqual(sim.MovementTarget(9).x,41.11f);
            m.ball.owner=null;m.ball.from=m.actors[8].id;m.ball.kind="cross";m.ball.start=new Point(40,25);m.ball.position=new Point(42,12);m.ball.end=new Point(47,3);m.ball.elapsed=.4f;m.ball.duration=2;m.ball.startHeight=.11f;m.ball.endHeight=1.6f;m.ball.loft=3.5f;
            sim.Advance(.1);Assert.AreEqual("far-post",m.actors[10].intent);Assert.Greater(sim.MovementTarget(9).x,45);
        }
        [Test] public void CentralPossessionDoesNotForceCrossingRuns()
        {
            var sim=Setup(0,1,1);var owner=sim.State.actors[8];owner.position=new Point(40,8);owner.carryTarget=owner.position;sim.State.ball.position=owner.position;sim.Advance(.1);
            Assert.IsFalse(sim.State.actors.Any(a=>a.intent=="far-post"||a.intent=="near-post"||a.intent=="box-arrival"));
        }
    }
}

using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Touchline.Core;

namespace Touchline.Tests
{
    public class MatchDecisionScenarioTests
    {
        MatchSimulation sim;Actor striker,keeper,mate,defender;
        void Prepare(int side,int period)
        {
            var db=new Database{players=Enumerable.Range(0,40).Select(i=>new PlayerData{id="scenario"+i,name="S"+i,team=i<20?"a":"b",position=i%20==0?"GB":i%20<5?"DEF":i%20<9?"MIL":"ATT",rating=80}).ToArray()};
            var c=new Career{club="a"};c.lineup=Career.Select(db,"a",c.tactic);sim=MatchSimulation.Create(db,c,"b",73,2700);
            sim.State.period=period;sim.State.restart=0;sim.State.phase="play";
            foreach(var p in sim.State.actors){p.sentOff=true;p.position=p.previous=new Point(0,30);}
            striker=sim.State.actors[side*11+9];keeper=sim.State.actors[(1-side)*11];mate=sim.State.actors[side*11+8];defender=sim.State.actors[(1-side)*11+2];
            striker.sentOff=keeper.sentOff=false;int direction=sim.Direction(side);
            striker.position=striker.previous=new Point(direction*41,0);striker.angle=direction*(float)System.Math.PI/2;keeper.position=keeper.previous=new Point(direction*51,0);
            sim.State.ball=new BallState{owner=striker.id,side=side,lastTouch=side,position=striker.position,previous=striker.position};sim.State.possessionSide=side;
        }
        [TestCase(0,1,false)] [TestCase(0,1,true)] [TestCase(1,1,true)] [TestCase(0,2,true)]
        public void ClearGoalWindowIsShotEvenWhenPatientOrChasedFromBehind(int side,int period,bool patient)
        {
            Prepare(side,period);int direction=sim.Direction(side);sim.Tactic(side).workIntoBox=patient;
            defender.sentOff=false;defender.position=defender.previous=striker.position-new Point(direction*1.5f,0);
            Assert.AreEqual("shot",sim.Decide(striker));Assert.AreEqual(1,sim.State.shots[side]);Assert.AreEqual("shot",sim.State.ball.kind);
        }
        [Test] public void PoorAngleUsesAFreePartnerInFrontOfGoal()
        {
            Prepare(0,1);striker.position=new Point(41,17);sim.State.ball.position=striker.position;
            mate.sentOff=false;mate.position=mate.previous=new Point(47,0);striker.angle=(float)System.Math.Atan2(mate.position.x-striker.position.x,mate.position.z-striker.position.z);
            defender.sentOff=false;defender.position=defender.previous=new Point(50,-25); // Receiver is level with the second-last opponent.
            string action=sim.Decide(striker);Assert.AreNotEqual("shot",action);Assert.AreEqual(mate.id,sim.State.ball.to);Assert.AreEqual(0,sim.State.shots[0]);
        }
        [Test] public void FallenPartnerCannotBeChosenInsteadOfTheAvailableFinish()
        {
            Prepare(0,1);mate.sentOff=false;mate.position=new Point(48,0);mate.action="fall";mate.actionTime=2;
            Assert.AreEqual("shot",sim.Decide(striker));Assert.IsTrue(string.IsNullOrEmpty(sim.State.ball.to));
        }
        [Test] public void RequestedCrossDeliveryChangesTheActualBallFlightAndItsStatistics()
        {
            Prepare(0,1);mate.sentOff=false;striker.position=new Point(43,24);mate.position=new Point(45,0);
            sim.State.ball.position=striker.position;
            var pass=typeof(MatchSimulation).GetMethod("Pass",BindingFlags.NonPublic|BindingFlags.Instance);
            sim.State.homeTactic.crossing="low";pass.Invoke(sim,new object[]{striker,mate,"cross"});
            float lowDuration=sim.State.ball.duration;
            Assert.AreEqual(.11f,sim.State.ball.endHeight);Assert.Less(sim.State.ball.loft,.2f);Assert.AreEqual(1,sim.State.metrics[0].lowCrosses);Assert.AreEqual(0,sim.State.metrics[0].aerialCrosses);
            sim.State.homeTactic.crossing="floated";pass.Invoke(sim,new object[]{striker,mate,"cross"});
            Assert.Greater(sim.State.ball.endHeight,1.5f);Assert.Greater(sim.State.ball.loft,4);Assert.Greater(sim.State.ball.duration,lowDuration);
            Assert.AreEqual(1,sim.State.metrics[0].aerialCrosses);Assert.AreEqual(2,sim.State.passes[0]);
        }
        [Test] public void UnreactedKeeperCannotSaveAWideBallUsingFutureDivingReach()
        {
            Prepare(0,1);striker.sentOff=true;keeper.position=keeper.previous=new Point(50,0);
            sim.State.ball=new BallState{kind="shot",side=0,from=striker.id,previous=new Point(49,.9f),position=new Point(51,.9f),height=.5f,previousHeight=.5f,start=new Point(45,.9f),end=new Point(54,.9f),elapsed=.1f,goalAttempt=true,shotOnTarget=true};
            var resolve=typeof(MatchSimulation).GetMethod("ResolveShotContact",BindingFlags.NonPublic|BindingFlags.Instance);
            Assert.IsFalse((bool)resolve.Invoke(sim,null));Assert.AreEqual(0,sim.State.metrics[1].saves);
            sim.State.ball.previous.z=sim.State.ball.position.z=.2f;
            Assert.IsTrue((bool)resolve.Invoke(sim,null),"The keeper's existing body can still block a ball before a deliberate reaction");
        }
        [TestCase("4-3-3")] [TestCase("4-4-2")] [TestCase("4-2-3-1")] [TestCase("3-4-2-1")]
        public void LeftAndRightRolesKeepTheirOwnFlankForBothTeamsAndHalves(string formation)
        {
            Prepare(0,1);var tactic=new Tactic();tactic.SetFormation(formation);
            foreach(int side in new[]{0,1})foreach(int period in new[]{1,2})foreach(bool possession in new[]{false,true}){
                sim.State.period=period;int direction=sim.Direction(side);
                for(int slot=0;slot<11;slot++){
                    var role=tactic.withoutBall[slot].role;var world=tactic.Position(slot,possession,0)*direction;
                    // Dot the world position with the physical left vector of
                    // the team. It is independent of which way the TV faces.
                    float left=world.z*direction;
                    if(role=="LB"||role=="LW"||role=="LM")Assert.Greater(left,0,role);
                    if(role=="RB"||role=="RW"||role=="RM")Assert.Less(left,0,role);
                }
            }
        }
    }
}

using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Touchline.Core;

namespace Touchline.Tests
{
    public class DefensiveBlockDecisionTests
    {
        [TestCase(0,false,false)] [TestCase(1,false,false)]
        [TestCase(0,true,false)] [TestCase(0,false,true)]
        public void FirstContactBlockLosesEnergyAndUsesOnlyAnOpenForwardOutlet(int side,bool blocked,bool byline)
        {
            var db=new Database{players=Enumerable.Range(0,40).Select(i=>new PlayerData{id="block"+i,name="B"+i,team=i<20?"a":"b",position=i%20==0?"GB":i%20<5?"DEF":i%20<9?"MIL":"ATT",rating=75}).ToArray()};
            var career=new Career{club="a"};career.lineup=Career.Select(db,"a",career.tactic);
            var sim=MatchSimulation.Create(db,career,"b",73,2700);
            foreach(var p in sim.State.actors)p.sentOff=true;
            var defender=sim.State.actors[side*11+2];var attacker=sim.State.actors[(1-side)*11+9];defender.sentOff=attacker.sentOff=false;
            int direction=sim.Direction(side);float x=(byline?-46:-35)*direction;
            defender.position=defender.previous=new Point(x,8);
            attacker.position=attacker.previous=new Point(x+(blocked?2:-1)*direction,blocked?9:8);
            // Only the open-outlet fixture has a boot prepared to meet the
            // arriving ball AND send it forward. Default angle 0 faced along
            // the incoming ball, so it was previously an unprepared deflection.
            defender.angle=!blocked&&!byline?direction*(float)Math.PI*.75f:0;
            sim.State.restart=0;sim.State.phase="play";
            var incoming=new Point(0,18);
            sim.State.ball=new BallState{kind="cross",side=1-side,lastTouch=1-side,from=attacker.id,previous=new Point(x,6),position=new Point(x,8.1f),height=.11f,previousHeight=.11f,velocity=incoming,elapsed=1};
            bool resolved=(bool)typeof(MatchSimulation).GetMethod("ResolveReception",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(sim,null);
            Assert.IsTrue(resolved);Assert.AreEqual("loose",sim.State.ball.kind);Assert.AreEqual(side,sim.State.ball.lastTouch);
            Assert.AreEqual("block",defender.action);Assert.IsTrue(sim.State.events.Any(e=>e.kind=="clearance"));
            Assert.Less(sim.State.ball.velocity.Length,12,"A reflex block must not manufacture a new full-power kick");
            if(!blocked&&!byline){
                Assert.Greater(sim.State.ball.velocity.x*direction,0,"A prepared boot uses an available forward channel");
                var destination=defender.actionTarget+sim.State.ball.velocity.Normalized*16;
                float safety=(float)typeof(MatchSimulation).GetMethod("Safety",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(sim,new object[]{defender,destination,false});
                Assert.Greater(safety,.45f,"The selected outlet must physically avoid the pressing opponent");
            }else{
                var normal=(defender.actionTarget-defender.position).Normalized;
                if(normal.Length<.1f)normal=incoming.Normalized*-1;
                var reflected=(incoming-normal*(Math.Min(0,Point.Dot(incoming,normal))*1.25f))*.58f;
                Assert.That(sim.State.ball.velocity.x,Is.EqualTo(reflected.x).Within(.0001f));
                Assert.That(sim.State.ball.velocity.z,Is.EqualTo(reflected.z).Within(.0001f));
                Assert.Greater(Point.Dot(new Point(0,1),incoming.Normalized),.9f,"This fixture meets the ball with an unprepared, wrong-facing boot");
                Assert.IsNull(sim.State.ball.owner,"A rebound remains contestable instead of being an automatic interception");
            }
        }
    }
}

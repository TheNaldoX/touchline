using System.Linq;
using NUnit.Framework;
using Touchline.Core;

namespace Touchline.Tests
{
    // Frappe de loin : un milieu seul à 21 m, plein axe, avec un défenseur à 3 m
    // sur le côté (pas dans la ligne de tir), doit tenter sa chance.
    public sealed class LongShotDecisionTests
    {
        [TestCase(0)][TestCase(1)]
        public void UnmarkedMidfielderShootsFromTwentyOneMetres(int side)
        {
            var db=new Database{players=Enumerable.Range(0,40).Select(i=>new PlayerData{id="ls"+i,name="S"+i,team=i<20?"a":"b",position=i%20==0?"GB":i%20<5?"DEF":i%20<9?"MIL":"ATT",rating=80}).ToArray()};
            var c=new Career{club="a"};c.lineup=Career.Select(db,"a",c.tactic);var sim=MatchSimulation.Create(db,c,"b",11,2700);int dir=sim.Direction(side);
            var shooter=sim.State.actors[side*11+7];var defender=sim.State.actors[(1-side)*11+3];var keeper=sim.State.actors[(1-side)*11];
            foreach(var p in sim.State.actors){p.sentOff=true;p.velocity=new Point();}
            foreach(var p in new[]{shooter,defender,keeper})p.sentOff=false;
            shooter.position=shooter.previous=new Point(dir*31.5f,0);shooter.angle=dir>0?0:(float)System.Math.PI;
            defender.position=defender.previous=new Point(dir*32f,3f);keeper.position=keeper.previous=new Point(dir*51.5f,0);
            sim.State.phase="play";sim.State.restart=0;sim.State.ball=new BallState{owner=shooter.id,side=side,position=shooter.position,height=.11f};sim.State.possessionSide=side;sim.State.carryTime=1f;
            string decision=sim.Decide(shooter);
            Assert.That(decision,Is.EqualTo("shot").Or.EqualTo("prepare"),"Décision : "+decision);
        }
    }
}

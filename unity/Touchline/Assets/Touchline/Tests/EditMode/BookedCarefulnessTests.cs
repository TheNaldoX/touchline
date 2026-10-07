using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Touchline.Core;

namespace Touchline.Tests
{
    // Un joueur averti retient son geste : moins de fautes « en retard »,
    // plus de tacle glissé (donc moins de seconds jaunes).
    public sealed class BookedCarefulnessTests
    {
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        static readonly Database Db=new Database{players=Enumerable.Range(0,40).Select(i=>new PlayerData{id="bk"+i,name="B"+i,team=i<20?"a":"b",position=i%20==0?"GB":i%20<5?"DEF":i%20<9?"MIL":"ATT",rating=80}).ToArray()};

        static bool Mistimed(uint seed,int yellows)
        {
            var c=new Career{club="a"};c.lineup=Career.Select(Db,"a",c.tactic);var sim=MatchSimulation.Create(Db,c,"b",seed,2700);sim.State.professionalRules=true;
            var owner=sim.State.actors[7];var defender=sim.State.actors[11+6];defender.yellows=yellows;int dir=sim.Direction(0);
            owner.position=new Point(dir*5,0);owner.velocity=new Point(dir*5,0);defender.position=new Point(dir*4.6f,.3f);
            sim.State.phase="play";sim.State.restart=0;sim.State.ball=new BallState{owner=owner.id,side=0,position=owner.position,height=.11f};
            return (bool)typeof(MatchSimulation).GetMethod("MistimedChallenge",Flags).Invoke(sim,new object[]{defender,owner});
        }

        [Test]
        public void BookedDefenderMistimesLessOften()
        {
            int clean=0,booked=0;
            for(uint s=1;s<=400;s++){if(Mistimed(s,0))clean++;if(Mistimed(s,1))booked++;}
            Assert.Greater(clean,40);Assert.Less(booked,clean*.7f);
        }

        [Test]
        public void BookedDefenderDoesNotSlide()
        {
            var c=new Career{club="a"};c.lineup=Career.Select(Db,"a",c.tactic);var sim=MatchSimulation.Create(Db,c,"b",3,2700);
            var owner=sim.State.actors[7];var defender=sim.State.actors[11+6];defender.yellows=1;
            sim.State.ball=new BallState{owner=owner.id,side=0,position=owner.position,height=.11f};
            Assert.IsFalse((bool)typeof(MatchSimulation).GetMethod("BeginSlidingDuel",Flags).Invoke(sim,new object[]{defender,owner}));
        }
    }
}

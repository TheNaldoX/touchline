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

        // Same geometry, booked or not: a slide that is on for a clean defender
        // is no longer attempted once he is on a yellow.
        static bool Slides(float dz,float dx,int yellows)
        {
            var c=new Career{club="a"};c.lineup=Career.Select(Db,"a",c.tactic);var sim=MatchSimulation.Create(Db,c,"b",3,2700);int dir=sim.Direction(0);
            var owner=sim.State.actors[9];var defender=sim.State.actors[11+3];defender.yellows=yellows;
            owner.position=owner.previous=new Point(dir*28,0);owner.velocity=new Point(dir*4,0);
            sim.State.phase="play";sim.State.restart=0;sim.State.ball=new BallState{owner=owner.id,side=0,position=owner.position+new Point(dir*.5f,0),height=.11f};
            defender.position=defender.previous=sim.State.ball.position+new Point(dir*dx,dz);var toward=(sim.State.ball.position+owner.velocity*.3f-defender.position).Normalized;defender.velocity=toward*5;defender.angle=(float)System.Math.Atan2(toward.x,toward.z);
            return (bool)typeof(MatchSimulation).GetMethod("BeginSlidingDuel",Flags).Invoke(sim,new object[]{defender,owner});
        }

        [Test]
        public void BookedDefenderDoesNotSlide()
        {
            bool found=false;
            foreach(float dz in new[]{1.4f,1.6f,1.8f,-1.5f})foreach(float dx in new[]{0f,.4f,.8f,-.4f}){
                if(!Slides(dz,dx,0))continue;found=true;
                Assert.IsFalse(Slides(dz,dx,1),"Averti : pas de tacle glissé (dz="+dz+", dx="+dx+")");
            }
            Assert.IsTrue(found,"Aucune position de référence ne déclenche de tacle glissé : test à revoir");
        }
    }
}

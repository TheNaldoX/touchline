using System.Linq;
using NUnit.Framework;
using Touchline.Core;

namespace Touchline.Tests
{
    // Faute tactique : un porteur lancé en contre-attaque au milieu, avec un
    // adversaire battu à l'épaule, finit souvent accroché. Jamais par un joueur
    // déjà averti, jamais hors transition.
    public sealed class TacticalFoulTests
    {
        static MatchSimulation Setup(int side,float sinceTurnover,int chaserYellows,out Actor carrier,out Actor chaser)
        {
            var db=new Database{players=Enumerable.Range(0,40).Select(i=>new PlayerData{id="tf"+i,name="T"+i,team=i<20?"a":"b",position=i%20==0?"GB":i%20<5?"DEF":i%20<9?"MIL":"ATT",rating=80}).ToArray()};
            var c=new Career{club="a"};c.lineup=Career.Select(db,"a",c.tactic);var sim=MatchSimulation.Create(db,c,"b",17,2700);int dir=sim.Direction(side);
            sim.State.professionalRules=true;carrier=sim.State.actors[side*11+7];chaser=sim.State.actors[(1-side)*11+6];var keeper=sim.State.actors[(1-side)*11];
            foreach(var p in sim.State.actors){p.sentOff=true;p.velocity=new Point();}
            foreach(var p in new[]{carrier,chaser,keeper})p.sentOff=false;
            carrier.position=carrier.previous=new Point(dir*5,0);carrier.velocity=new Point(dir*6,0);
            chaser.position=chaser.previous=new Point(dir*4.4f,.8f);chaser.velocity=new Point(dir*5.5f,0);chaser.yellows=chaserYellows;keeper.position=new Point(dir*51,0);
            sim.State.phase="play";sim.State.restart=0;sim.State.clock=600;sim.State.turnoverAt=600-sinceTurnover;
            sim.State.ball=new BallState{owner=carrier.id,side=side,position=carrier.position,height=.11f};sim.State.possessionSide=side;
            return sim;
        }
        static bool Foul(MatchSimulation sim,Actor carrier)=>(bool)typeof(MatchSimulation).GetMethod("TacticalFoul",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(sim,new object[]{carrier});

        [TestCase(0)][TestCase(1)]
        public void CounterAttackCarrierIsEventuallyHeld(int side)
        {
            var sim=Setup(side,1,0,out var carrier,out var chaser);bool fouled=false;
            for(int i=0;i<300&&!fouled;i++)fouled=Foul(sim,carrier);
            Assert.IsTrue(fouled);Assert.AreEqual("free-kick",sim.State.phase);Assert.AreEqual(carrier.side,sim.State.restartSide);
            Assert.AreEqual(1,sim.State.metrics[chaser.side].fouls);
        }

        [Test]
        public void NoTacticalFoulWhenBookedOrOutsideTransition()
        {
            var sim=Setup(0,1,1,out var carrier,out _);for(int i=0;i<300;i++)Assert.IsFalse(Foul(sim,carrier),"Joueur averti");
            sim=Setup(0,20,0,out carrier,out _);for(int i=0;i<300;i++)Assert.IsFalse(Foul(sim,carrier),"Hors transition");
        }
    }
}

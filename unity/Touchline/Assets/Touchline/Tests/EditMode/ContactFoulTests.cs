using System.Linq;
using NUnit.Framework;
using Touchline.Core;
namespace Touchline.Tests
{
    public class ContactFoulTests
    {
        MatchSimulation sim;Actor carrier,challenger;
        [SetUp] public void Setup()
        {
            var db=new Database{players=Enumerable.Range(0,40).Select(i=>new PlayerData{id="c"+i,name="C"+i,team=i<20?"a":"b",position=i%20==0?"GB":i%20<5?"DEF":i%20<9?"MIL":"ATT",rating=75}).ToArray()};var career=new Career{club="a"};career.lineup=Career.Select(db,"a",career.tactic);sim=MatchSimulation.Create(db,career,"b",731,2700);
            foreach(var p in sim.State.actors)p.sentOff=true;carrier=sim.State.actors[9];challenger=sim.State.actors[12];carrier.sentOff=challenger.sentOff=false;carrier.action=challenger.action="run";carrier.actionTime=challenger.actionTime=0;
            carrier.position=carrier.previous=new Point();carrier.angle=(float)System.Math.PI/2;carrier.velocity=new Point(1,0);challenger.previous=new Point(-.8f,0);challenger.position=new Point(-.4f,0);challenger.velocity=new Point(5,0);
            sim.State.restart=0;sim.State.phase="play";sim.State.professionalRules=true;sim.State.ball=new BallState{owner=carrier.id,kind="none",position=new Point(.56f,0),side=0};
        }
        [Test] public void ChargeIntoBackWithoutBallAccessProducesOneFreeKick()
        {
            sim.ResolvePlayerContacts();sim.ResolvePlayerContacts();Assert.AreEqual("free-kick",sim.State.phase);Assert.AreEqual(0,sim.State.restartSide);Assert.AreEqual(1,sim.State.metrics[1].fouls);Assert.AreEqual(1,sim.State.events.Count(e=>e.kind=="foul"));Assert.IsNull(sim.State.ball.owner);Assert.AreEqual("fall",carrier.action);
        }
        [TestCase("shoulder")] [TestCase("slow")] [TestCase("ball-access")] [TestCase("teammate")] [TestCase("off-ball")] [TestCase("restart")]
        public void LegitimateContactDoesNotManufactureAFoul(string situation)
        {
            if(situation=="shoulder"){challenger.previous=new Point(0,-.8f);challenger.position=new Point(0,-.4f);challenger.velocity=new Point(0,5);carrier.velocity=new Point(0,1);}
            if(situation=="slow")challenger.velocity=new Point(2,0);
            if(situation=="ball-access")sim.State.ball.position=new Point(.1f,0);
            if(situation=="teammate")challenger.side=0;
            if(situation=="off-ball")sim.State.ball.owner=null;
            if(situation=="restart")sim.State.restart=10;
            uint seed=sim.State.seed;sim.ResolvePlayerContacts();Assert.AreEqual(0,sim.State.metrics[1].fouls);Assert.AreEqual(seed,sim.State.seed);
        }
    }
}

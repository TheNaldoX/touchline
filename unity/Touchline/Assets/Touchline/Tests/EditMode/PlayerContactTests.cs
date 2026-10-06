using System.Linq;
using NUnit.Framework;
using Touchline.Core;

namespace Touchline.Tests
{
    public class PlayerContactTests
    {
        MatchSimulation sim;Actor a,b;
        [SetUp] public void Setup(){var db=new Database{players=Enumerable.Range(0,40).Select(i=>new PlayerData{id="p"+i,name="P"+i,team=i<20?"a":"b",position=i%20==0?"GB":i%20<5?"DEF":i%20<9?"MIL":"ATT",rating=75}).ToArray()};var career=new Career{club="a"};career.lineup=Career.Select(db,"a",career.tactic);sim=MatchSimulation.Create(db,career,"b");foreach(var p in sim.State.actors)p.sentOff=true;a=sim.State.actors[9];b=sim.State.actors[12];a.sentOff=b.sentOff=false;a.action=b.action="run";a.actionTime=b.actionTime=0;}
        [TestCase(0)] [TestCase(.2f)] [TestCase(.5f)] public void UprightBodiesSeparateWithoutRandomness(float distance){a.position=a.previous=new Point();b.position=b.previous=new Point(distance,0);uint seed=sim.State.seed;sim.ResolvePlayerContacts();Assert.GreaterOrEqual(Point.Distance(a.position,b.position),MatchSimulation.BodySeparation-.001f);Assert.AreEqual(seed,sim.State.seed);}
        [Test] public void RunnersCannotPassThroughEachOtherBetweenTicks(){a.previous=new Point(-.7f,0);b.previous=new Point(.7f,0);a.position=new Point(.4f,0);b.position=new Point(-.4f,0);a.velocity=new Point(11,0);b.velocity=new Point(-11,0);sim.ResolvePlayerContacts();Assert.Less(a.position.x,b.position.x);Assert.GreaterOrEqual(b.position.x-a.position.x,MatchSimulation.BodySeparation-.001f);Assert.LessOrEqual(a.velocity.x-b.velocity.x,.001f);}
        [Test] public void GlancingContactPreservesTangentialMotion(){a.previous=a.position=new Point();b.previous=b.position=new Point(.3f,0);a.velocity=new Point(3,4);b.velocity=new Point(-3,4);sim.ResolvePlayerContacts();Assert.AreEqual(4,a.velocity.z);Assert.AreEqual(4,b.velocity.z);}
        [TestCase("kick")] [TestCase("dive")] [TestCase("tackle")] public void APlantedGestureIsNotDraggedAway(string action){a.previous=a.position=new Point();b.previous=b.position=new Point(.3f,0);a.action=action;a.actionTime=.5f;sim.ResolvePlayerContacts();Assert.AreEqual(0,a.position.x);Assert.That(b.position.x,Is.EqualTo(MatchSimulation.BodySeparation).Within(.001));}
        [Test] public void ExcludedPlayersDoNotObstructPlay(){a.position=a.previous=new Point();b.position=b.previous=new Point(.1f,0);b.sentOff=true;sim.ResolvePlayerContacts();Assert.AreEqual(.1f,b.position.x);Assert.AreEqual(0,a.position.x);}
        [Test] public void PreparingASetPieceDoesNotFreezeOverlappingBodies(){sim.State.restart=0;sim.State.restartTaker=a.id;sim.State.ball.fixedStart=true;sim.State.ball.kind="cross";sim.State.ball.elapsed=-.5f;a.position=a.previous=new Point();b.position=b.previous=new Point(.1f,0);sim.Advance(.1);Assert.GreaterOrEqual(Point.Distance(a.position,b.position),MatchSimulation.BodySeparation-.001f);}
        [Test] public void ContactsAtTouchlineRemainInBounds(){a.position=a.previous=new Point(0,33.75f);b.position=b.previous=new Point(0,33.65f);sim.ResolvePlayerContacts();Assert.LessOrEqual(a.position.z,33.8f);Assert.LessOrEqual(b.position.z,33.8f);Assert.Greater(Point.Distance(a.position,b.position),.5f);}
    }
}

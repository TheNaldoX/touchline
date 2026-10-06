using System;
using System.Linq;
using NUnit.Framework;
using Touchline.Core;

namespace Touchline.Tests
{
    public class ReceptionTests
    {
        MatchSimulation Sim()
        {
            var db=new Database{clubs=new[]{new ClubData{id="a"},new ClubData{id="b"}},players=Enumerable.Range(0,24).Select(i=>new PlayerData{id="p"+i,team=i<12?"a":"b",name="Player "+i,position=i%12==0?"GB":"MIL",positions=new[]{i%12==0?"GK":"CM"},rating=75,fitness=100,morale=75}).ToArray()};
            var career=new Career{club="a"};career.lineup=Career.Select(db,"a",career.tactic);return MatchSimulation.Create(db,career,"b");
        }
        void Pass(MatchSimulation s,string kind="pass"){var b=s.State.ball;b.owner=null;b.kind=kind;b.start=new Point(0,0);b.end=new Point(20,0);b.position=new Point(2,0);b.elapsed=.1f;b.duration=2;b.startHeight=b.endHeight=.11f;b.loft=.08f;b.to=s.State.actors[6].id;}
        [Test] public void ReceiverMeetsGroundPassBeforeItsEndpoint(){var s=Sim();Pass(s);var p=s.State.actors[6];p.position=new Point(11,2);var target=s.ReceptionTarget(p);Assert.That(target.x,Is.InRange(5,15));Assert.AreEqual(0,target.z);}
        [Test] public void ReceiverDoesNotChaseAnUnreachableEarlierPoint(){var s=Sim();Pass(s);var p=s.State.actors[6];p.position=new Point(25,15);Assert.AreEqual(20,s.ReceptionTarget(p).x);}
        [Test] public void MovingReceiverCanMeetPassEarlier(){var s=Sim();Pass(s);var p=s.State.actors[6];p.position=new Point(13,4);var standing=s.ReceptionTarget(p);p.velocity=new Point(-4,-3);var running=s.ReceptionTarget(p);Assert.Less(running.x,standing.x);}
        [Test] public void HighBallIsNotTreatedAsAReachableGroundPass(){var s=Sim();Pass(s);s.State.ball.startHeight=s.State.ball.endHeight=3;var p=s.State.actors[6];p.position=new Point(11,2);Assert.AreEqual(20,s.ReceptionTarget(p).x);}
        [Test] public void ReceptionTrajectoryMirrorsAcrossHalves(){var s=Sim();Pass(s);var p=s.State.actors[6];p.position=new Point(11,2);var a=s.ReceptionTarget(p);p.position=p.position*-1;s.State.ball.start=s.State.ball.start*-1;s.State.ball.end=s.State.ball.end*-1;s.State.period=2;var b=s.ReceptionTarget(p);Assert.That(b.x,Is.EqualTo(-a.x).Within(.001));Assert.That(b.z,Is.EqualTo(-a.z).Within(.001));}
        [Test] public void DegenerateFlightHasFiniteTarget(){var s=Sim();Pass(s);s.State.ball.duration=0;var q=s.ReceptionTarget(s.State.actors[6]);Assert.IsFalse(float.IsNaN(q.x)||float.IsNaN(q.z));Assert.AreEqual(20,q.x);}
    }
}

using System.Linq;
using NUnit.Framework;
using Touchline.Core;

namespace Touchline.Tests
{
    // Rendu du ballon en vol : la formule de rendu redonne exactement les positions
    // de la simulation à chaque pas, et une courbe (pas des segments) entre deux pas.
    public sealed class BallFlightTests
    {
        [Test]
        public void ScriptedFlightMatchesSimulationAtEachStep()
        {
            var db=new Database{players=Enumerable.Range(0,40).Select(i=>new PlayerData{id="bf"+i,name="F"+i,team=i<20?"a":"b",position=i%20==0?"GB":i%20<5?"DEF":i%20<9?"MIL":"ATT",rating=80}).ToArray()};
            var c=new Career{club="a"};c.lineup=Career.Select(db,"a",c.tactic);var sim=MatchSimulation.Create(db,c,"b",9,2700);
            foreach(var p in sim.State.actors)p.sentOff=p.slot!=0;
            var b=sim.State.ball;b.owner=null;b.kind="pass";b.from=null;b.start=new Point(-20,-10);b.end=new Point(15,20);b.startHeight=.11f;b.endHeight=.11f;b.loft=6;b.duration=2f;b.elapsed=.3f;
            var at=BallFlight.At(b,.3f);b.position=at.position;b.height=at.height;var before=BallFlight.At(b,.2f);b.previous=before.position;b.previousHeight=before.height;
            Assert.IsTrue(BallFlight.InScriptedFlight(b,MatchSimulation.Step));
            var mid=BallFlight.At(b,.25f);float linearMid=(b.height+b.previousHeight)/2;
            Assert.Greater(mid.height,linearMid,"La parabole passe au-dessus de la corde");
            b.position=new Point(0,0);Assert.IsFalse(BallFlight.InScriptedFlight(b,MatchSimulation.Step),"Ballon déplacé par un contact : interpolation simple");
        }
    }
}

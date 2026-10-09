using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Touchline.Core;

namespace Touchline.Tests
{
    public class TempoPassExecutionTests
    {
        static MatchSimulation PreparedPass(int side,float tempo,float distance,string kind)
        {
            var db=new Database{players=Enumerable.Range(0,44).Select(i=>new PlayerData{id="tempo"+i,name="Joueur "+i,team=i<22?"a":"b",rating=70,fitness=100,morale=75,
                position=i%22<2?"GB":i%22<10?"DEF":i%22<16?"MIL":"ATT"}).ToArray()};
            var career=new Career{club="a"};career.lineup=Career.Select(db,"a",career.tactic);
            var sim=MatchSimulation.Create(db,career,"b",712,2700);
            sim.Tactic(side).tempo=tempo;
            foreach(var p in sim.State.actors){p.position=new Point(-35,30);p.velocity=new Point();}
            var from=sim.State.actors[side*11+5];var to=sim.State.actors[side*11+9];
            from.position=new Point(-sim.Direction(side)*15,0);to.position=from.position+new Point(sim.Direction(side)*distance,3);
            sim.State.ball=new BallState{position=from.position,owner=from.id,side=side,height=.11f};
            typeof(MatchSimulation).GetMethod("Pass",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(sim,new object[]{from,to,kind});
            return sim;
        }
        [TestCase(0,10f,"pass")][TestCase(1,10f,"pass")]
        [TestCase(0,25f,"through")][TestCase(1,25f,"through")]
        [TestCase(0,35f,"switch")][TestCase(1,35f,"switch")]
        public void TempoChangesBallSpeedButNotAnUnpressuredStationaryPassError(int side,float distance,string kind)
        {
            var patient=PreparedPass(side,.2f,distance,kind);var quick=PreparedPass(side,.8f,distance,kind);
            Assert.Less(Point.Distance(patient.State.ball.end,quick.State.ball.end),.00001f,"Même joueur, même geste, même graine : pas de malus de dispersion indépendant de la situation.");
            Assert.Less(quick.State.ball.duration,patient.State.ball.duration,"Le rythme conserve son effet sur la vitesse de transmission.");
            Assert.AreEqual(patient.State.seed,quick.State.seed,"Pas de tirage aléatoire supplémentaire.");
        }
    }
}

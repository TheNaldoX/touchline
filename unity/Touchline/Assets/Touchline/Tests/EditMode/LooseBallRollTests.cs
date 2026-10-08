using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Touchline.Core;

namespace Touchline.Tests
{
    // Un ballon libre roule comme sur une pelouse tondue (~1 m/s²) : un ballon
    // frappé à 8 m/s doit parcourir une trentaine de mètres avant de s'arrêter.
    public sealed class LooseBallRollTests
    {
        [Test]
        public void GroundBallRollsAboutVSquaredOverTwoA()
        {
            var db=new Database{players=Enumerable.Range(0,40).Select(i=>new PlayerData{id="lb"+i,name="L"+i,team=i<20?"a":"b",position=i%20==0?"GB":i%20<5?"DEF":i%20<9?"MIL":"ATT",rating=80}).ToArray()};
            var c=new Career{club="a"};c.lineup=Career.Select(db,"a",c.tactic);var sim=MatchSimulation.Create(db,c,"b",5,2700);
            var b=sim.State.ball;b.owner=null;b.kind="loose";b.position=new Point();b.height=.11f;b.verticalVelocity=0;b.velocity=new Point(0,8);
            var roll=typeof(MatchSimulation).GetMethod("RollBall",BindingFlags.Instance|BindingFlags.NonPublic);
            for(int i=0;i<200&&b.velocity.Length>0;i++)roll.Invoke(sim,null);
            float expected=8f*8f/(2*MatchSimulation.GroundRollDeceleration);
            Assert.AreEqual(0f,b.velocity.Length,1e-4f);
            Assert.AreEqual(expected,b.position.z,1.5f,"Distance de roulement");
            Assert.Greater(b.position.z,28f,"Un ballon libre à 8 m/s ne doit pas mourir en moins de 28 m.");
        }
    }
}

using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Touchline.Core;

namespace Touchline.Tests
{
    public class TouchlineRecoveryTests
    {
        [TestCase(0,33.55f)] [TestCase(0,-33.55f)]
        [TestCase(52.25f,12)] [TestCase(-52.25f,-12)]
        public void AStationaryInPlayBallCanBeRecoveredBeyondFormationMargins(float x,float z)
        {
            var db=JsonUtility.FromJson<Database>(Resources.Load<TextAsset>("Data/database").text);
            var clubs=db.clubs.Where(c=>c.playable).Take(2).ToArray();var career=new Career{club=clubs[0].id};
            career.lineup=Career.Select(db,career.club,career.tactic);
            var sim=MatchSimulation.Create(db,career,clubs[1].id,15838,2700);var m=sim.State;
            m.restart=0;m.phase="play";m.events.Clear();
            foreach(var p in m.actors){p.sentOff=true;p.controlTime=0;p.action="idle";p.actionTime=0;}
            var collector=m.actors[7];collector.sentOff=false;
            collector.position=collector.previous=new Point(x*.96f,z*.96f);collector.velocity=new Point();
            m.ball=new BallState{kind="loose",position=new Point(x,z),previous=new Point(x,z),height=.11f,previousHeight=.11f,side=1,lastTouch=1};
            bool recovered=false;
            for(int i=0;i<100&&!recovered;i++){sim.Advance(.1);recovered=m.ball.owner==collector.id;}
            Assert.IsTrue(recovered,"Formation width must not leave an in-play ball unreachable");
            Assert.IsFalse(m.events.Any(e=>e.kind=="throw-in"||e.kind=="goal-kick"),"Recovery must not invent a restart");
        }
    }
}

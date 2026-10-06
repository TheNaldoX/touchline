using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Touchline.Core;

namespace Touchline.Tests
{
    public class KickoffTests
    {
        Database db;
        MatchSimulation Sim(){db=new Database{players=Enumerable.Range(0,32).Select(i=>new PlayerData{id="p"+i,name="Player "+i,team=i<16?"a":"b",position=i%16==0?"GB":"MIL",positions=new[]{i%16==0?"GK":"CM"},rating=70}).ToArray()};var c=new Career{club="a"};c.lineup=Career.Select(db,"a",c.tactic);return MatchSimulation.Create(db,c,"b");}
        [Test] public void BallWaitsOnCentreMarkUntilKickContact(){var s=Sim();Assert.AreEqual(0,s.State.ball.position.Length);s.Advance(1.9);Assert.AreEqual(0,s.State.ball.position.Length);s.Advance(.2);Assert.AreEqual("pass",s.State.ball.kind);Assert.IsNull(s.State.ball.owner);Assert.Less(s.State.ball.elapsed,0);Assert.AreEqual(0,s.State.ball.position.Length);s.Advance(.2);Assert.Greater(s.State.ball.position.Length,.2f);}
        [Test] public void PlayersRespectOwnHalfAndCentreCircle(){var s=Sim();var m=s.State;foreach(var p in m.actors){if(p!=m.actors[9])Assert.LessOrEqual(p.position.x*s.Direction(p.side),0);if(p.side==1)Assert.GreaterOrEqual(p.position.Length,9.15f);}s.Advance(2.1);foreach(var p in m.actors.Where(p=>p.side==1))Assert.GreaterOrEqual(p.position.Length,9.15f);}
        [Test] public void OppositeTeamStartsSecondHalf(){var s=Sim();s.State.halfTime=true;s.State.clock=360;s.ResumeHalf();s.Advance(3.1);Assert.AreEqual(1,s.State.ball.side);Assert.AreEqual("pass",s.State.ball.kind);Assert.IsNotNull(s.State.ball.to);Assert.AreNotEqual(s.State.ball.from,s.State.ball.to);}
        [Test] public void PlayersReturnAfterGoalWithoutTeleporting(){var s=Sim();var m=s.State;m.clock=100;m.restart=0;m.phase="play";m.decision=100;foreach(var p in m.actors){p.position=new Point(30+p.side*5,p.slot*2-10);p.previous=p.position;p.velocity=new Point();}m.ball=new BallState{kind="loose",position=new Point(52,0),previous=new Point(52,0),velocity=new Point(9,0),lastTouch=0,side=0};s.Advance(.1);Assert.AreEqual("goal",m.phase);bool restarted=false;for(int i=0;i<400;i++){var before=m.actors.Select(p=>p.position).ToArray();s.Advance(.1);for(int j=0;j<22;j++)Assert.Less(Point.Distance(before[j],m.actors[j].position),.9f,"Teleport at "+m.clock);if(m.phase=="play"){Assert.AreEqual(1,m.ball.side);Assert.AreEqual("pass",m.ball.kind);restarted=true;break;}}Assert.IsTrue(restarted,"Kickoff setup never completed");}
        [Test] public void SaveDuringKickoffKeepsTheSameContinuation(){var s=Sim();s.Advance(2.1);var other=new MatchSimulation(db,JsonUtility.FromJson<MatchState>(JsonUtility.ToJson(s.State)));s.Advance(5);other.Advance(5);Assert.AreEqual(JsonUtility.ToJson(s.State),JsonUtility.ToJson(other.State));}
    }
}

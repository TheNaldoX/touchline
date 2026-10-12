using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Touchline.Core;

namespace Touchline.Tests
{
    public class RestartTests
    {
        Database db;
        MatchSimulation Sim(string phase,Point spot,int side=0)
        {
            db=new Database{players=Enumerable.Range(0,32).Select(i=>new PlayerData{id="p"+i,name="Player "+i,team=i<16?"a":"b",position=i%16==0?"GB":"MIL",positions=new[]{i%16==0?"GK":"CM"},rating=70}).ToArray()};
            var c=new Career{club="a"};c.lineup=Career.Select(db,"a",c.tactic);var s=MatchSimulation.Create(db,c,"b");
            var m=s.State;m.clock=60;m.restart=.1f;m.phase=phase;m.restartSide=side;m.restartTaker=null;m.ball=new BallState{position=spot,previous=spot,side=side,lastTouch=side};return s;
        }
        void WaitForKick(MatchSimulation s)
        {
            var spot=s.State.ball.position;
            for(int i=0;i<600;i++){
                var before=s.State.actors.Select(p=>p.position).ToArray();s.Advance(.1);
                for(int p=0;p<22;p++)Assert.Less(Point.Distance(before[p],s.State.actors[p].position),.8f,"Restart teleported a player");
                Assert.Less(Point.Distance(spot,s.State.ball.position),.001f,"Ball moved before restart contact");
                if(s.State.phase=="play")return;
            }
            Assert.Fail("Restart never completed");
        }
        [TestCase(0)] [TestCase(1)] public void PenaltyWaitsForBothTeamsBehindArcAndKeeperOnLine(int side)
        {
            int dir=side==0?1:-1;var s=Sim("penalty",new Point(dir*41.5f,0),side);var m=s.State;
            foreach(var p in m.actors){p.position=new Point(dir*41,0);p.velocity=new Point();}
            WaitForKick(s);Assert.IsTrue(m.ball.penalty);Assert.IsTrue(m.ball.fixedStart);Assert.That(m.ball.start.x,Is.EqualTo(dir*41.5f).Within(.001));
            foreach(var p in m.actors){if(p.id==m.restartTaker)continue;if(p.side!=side&&p.slot==0){Assert.That(p.position.x,Is.EqualTo(dir*52.5f).Within(.04));continue;}
                Assert.LessOrEqual(p.position.x*dir,35.9f);Assert.GreaterOrEqual(Point.Distance(p.position,m.ball.start),9.15f);}
        }
        [Test] public void FreeKickHasWallAndUsesSpecialistWithoutMovingTheBall()
        {
            var s=Sim("free-kick",new Point(30,0));var m=s.State;var specialist=m.actors[8];db.Find(specialist.id).attributes=new[]{new AttributeValue{key="fkAccuracy",value=99}};
            WaitForKick(s);Assert.AreEqual(specialist.id,m.restartTaker);Assert.AreEqual(4,m.restartWall.Count);Assert.AreEqual("shot",m.ball.kind);Assert.Greater(m.ball.loft,1.5f);
            foreach(var id in m.restartWall){var p=m.actors.First(a=>a.id==id);Assert.GreaterOrEqual(Point.Distance(p.position,m.ball.position),9.15f);Assert.Greater(p.position.x,38);}
            Assert.That(m.ball.start.x,Is.EqualTo(30).Within(.001));
            var wall=m.restartWall.Select(id=>m.actors.First(a=>a.id==id)).ToArray();var positions=wall.Select(a=>a.position).ToArray();s.Advance(.1);
            for(int i=0;i<wall.Length;i++)Assert.Less(Point.Distance(wall[i].position,positions[i]),.02f,"Wall began running toward kickoff before the kick");
        }
        [Test] public void IndirectFreeKickMustFindAnotherPlayer()
        {
            var s=Sim("free-kick",new Point(30,0));s.State.indirectRestart=true;WaitForKick(s);Assert.AreEqual("pass",s.State.ball.kind);Assert.IsNotNull(s.State.ball.to);Assert.IsFalse(s.State.ball.goalAttempt);
        }
        [Test] public void SaveKeepsChosenTakerAndWallWhileTheyApproach()
        {
            var s=Sim("free-kick",new Point(30,4));s.Advance(1);var copy=new MatchSimulation(db,JsonUtility.FromJson<MatchState>(JsonUtility.ToJson(s.State)));
            s.Advance(20);copy.Advance(20);Assert.AreEqual(JsonUtility.ToJson(s.State),JsonUtility.ToJson(copy.State));
        }
        [Test] public void BroadcastReadinessDoesNotPickTakerOrAlterRestartState()
        {
            var s=Sim("free-kick",new Point(30,4));string before=JsonUtility.ToJson(s.State);
            Assert.IsFalse(s.FreeKickDeliveryApproaching);Assert.AreEqual(before,JsonUtility.ToJson(s.State));
            s.Advance(1);before=JsonUtility.ToJson(s.State);bool near=s.FreeKickDeliveryApproaching;Assert.AreEqual(before,JsonUtility.ToJson(s.State));
        }
        MatchSimulation WideRestart(int side,int flank)
        {
            int dir=side==0?1:-1;var s=Sim("free-kick",new Point(dir*25,flank*26),side);var m=s.State;
            foreach(var a in m.actors){a.position=a.side==side?new Point(-dir*10,flank*(a.slot*2-10)):new Point(dir*45,flank*22);a.velocity=new Point();}
            var taker=m.actors[side*11+8];taker.position=new Point(dir*24.58f,flank*26);m.restartTaker=taker.id;
            m.actors[side*11+9].position=new Point(dir*40,0);m.actors[side*11+7].position=new Point(dir*25,flank*22);
            m.actors[(1-side)*11].position=new Point(dir*52.5f,0);return s;
        }
        static void DeliverRestart(MatchSimulation s)=>typeof(MatchSimulation).GetMethod("RestartKick",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(s,null);
        [TestCase(0,-1)][TestCase(0,1)][TestCase(1,-1)][TestCase(1,1)]
        public void WideFreeKickCanDeliverToAnOnsideAerialTarget(int side,int flank)
        {
            var s=WideRestart(side,flank);DeliverRestart(s);Assert.AreEqual("cross",s.State.ball.kind);Assert.AreEqual(s.State.actors[side*11+9].id,s.State.ball.to);Assert.IsFalse(s.State.ball.offside);Assert.IsTrue(s.State.ball.fixedStart);
        }
        [Test] public void WideFreeKickKeepsShortOptionWhenReceiverIsOffside()
        {
            var s=WideRestart(0,1);s.State.actors[9].position=new Point(48,0);DeliverRestart(s);Assert.AreEqual("pass",s.State.ball.kind);Assert.AreEqual(s.State.actors[7].id,s.State.ball.to);
        }
        [Test] public void WideFreeKickDoesNotForceCrossIntoDominantAerialMarker()
        {
            var s=WideRestart(0,1);var target=s.State.actors[9];var marker=s.State.actors[12];marker.position=target.position;
            db.Find(target.id).attributes=new[]{new AttributeValue{key="headingAccuracy",value=20},new AttributeValue{key="jumping",value=20}};
            db.Find(marker.id).attributes=new[]{new AttributeValue{key="headingAccuracy",value=95},new AttributeValue{key="jumping",value=95}};
            DeliverRestart(s);Assert.AreEqual("pass",s.State.ball.kind);Assert.AreEqual(s.State.actors[7].id,s.State.ball.to);
        }
        [Test] public void GoalKickLeavesAreaClearAndLaunchesFromRest()
        {
            var s=Sim("goal-kick",new Point(-47,0));WaitForKick(s);Assert.IsTrue(s.State.ball.fixedStart);Assert.IsNull(s.State.ball.owner);Assert.AreEqual(s.State.actors[0].id,s.State.ball.from);
            foreach(var p in s.State.actors.Where(a=>a.side==1))Assert.IsFalse(p.position.x< -36&&Math.Abs(p.position.z)<20.16f);
        }
        [Test] public void ExcludedKeeperCannotTakeGoalKick()
        {
            var s=Sim("goal-kick",new Point(-47,0));s.State.actors[0].sentOff=true;WaitForKick(s);Assert.AreNotEqual(s.State.actors[0].id,s.State.ball.from);
        }
    }
}

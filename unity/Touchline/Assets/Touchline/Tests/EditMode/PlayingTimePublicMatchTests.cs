using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Touchline.Core;
namespace Touchline.Tests
{
    public sealed class PlayingTimePublicMatchTests
    {
        Database db;Career c;MatchSimulation sim;string starter,bench;
        [Test] public void OldClubWindowIsIgnoredWithoutChangingHistoryOnRead(){Setup(2700);var contract=c.Contract(db,starter);contract.role="key";contract.playingTime.club="c2";for(int i=0;i<5;i++)contract.playingTime.Add(false,0);Assert.IsFalse(c.PlayingTimeConcern(starter));Assert.That(c.PlayingTimeProgress(starter),Does.Contain("ancien club"));Assert.AreEqual(5,contract.playingTime.games.Count);Assert.AreEqual("c2",contract.playingTime.club);}
        [Test] public void FirstObservedMatchInNewClubStartsItsOwnWindow(){Setup(2700);var contract=c.Contract(db,starter);contract.playingTime.club="c2";for(int i=0;i<12;i++)contract.playingTime.Add(false,0);Finish();Assert.AreEqual(c.club,contract.playingTime.club);Assert.AreEqual(1,contract.playingTime.games.Count);Assert.AreEqual(90,Usage(starter).minutes);}
        [Test] public void LoanParentSnapshotRestoresItsOwnClubProvenance(){Setup(2700);var contract=c.Contract(db,starter);contract.playingTime.club=c.club;contract.playingTime.Add(true,75);typeof(Career).GetMethod("CaptureLoanParent",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(c,new object[]{contract,db.Find(starter)});contract.playingTime.club="c1";contract.playingTime.games[0].minutes=0;Assert.AreEqual(c.club,contract.parentConditions.playingTime.club);typeof(Career).GetMethod("RestoreLoanParent",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(c,new object[]{contract,db.Find(starter),c.club});Assert.AreEqual(c.club,contract.playingTime.club);Assert.AreEqual(75,contract.playingTime.games.Single().minutes);}
        [Test] public void NewSignatureStampsDestinationClub(){Setup(2700);sim.State.finished=true;c.ProposeTransfer(db,starter,0,1000,3,"starter");c.world.offers.Last().status="accepted";c.SignTransfer(db,starter);Assert.AreEqual(c.club,c.Contract(db,starter).playingTime.club);}
        void Setup(int half){c=StaffTrainingLifecycleTests.Fixture(out db);sim=MatchSimulation.Create(db,c,"c1",31,(int)half);c.match=sim.State;c.ApplyMatchContext(sim);starter=sim.State.actors[1].id;bench=db.Squad(c.club).First(x=>!sim.State.used.Contains(x.id)).id;foreach(var p in c.life.players)c.Contract(db,p.id).playingTime=new PlayingTimeUsage();}
        void Minute(float minute){sim.State.clock=minute*sim.State.SecondsPerMinute;}
        PlayingTimeGame Usage(string id)=>c.Contract(db,id).playingTime.games.Single();
        void Finish(float minute=90){Minute(minute);sim.State.finished=true;c.RecordMatch(db);}
        [TestCase(360)][TestCase(2700)]
        public void PublicMatchCountsOnceAndPreservesStarterAndBench(int half){Setup(half);Finish();Assert.AreEqual(90,Usage(starter).minutes);Assert.IsTrue(Usage(starter).started);Assert.AreEqual(0,Usage(bench).minutes);c.RecordMatch(db);Assert.AreEqual(1,c.Contract(db,starter).playingTime.games.Count);Assert.AreEqual(1,c.life.matches);}
        [TestCase(360)][TestCase(2700)]
        public void EarlyAbandonmentDoesNotInventSeventyMinutes(int half){Setup(half);Minute(20);var opponent=sim.State.actors.Where(a=>a.side==1).Select(a=>a.id).Take(5).ToArray();foreach(var id in opponent)sim.SendOff(id);Assert.IsTrue(sim.State.finished);c.RecordMatch(db);Assert.AreEqual(20,Usage(starter).minutes);Assert.AreEqual(0,c.Person(starter).appearances);c.RecordMatch(db);Assert.AreEqual(1,c.Contract(db,starter).playingTime.games.Count);}
        [TestCase(360)][TestCase(2700)]
        public void RedCardStopsBothRoleMinutesAndThirtyMinuteAppearanceCounter(int half){Setup(half);Minute(20);sim.SendOff(starter);Finish();Assert.AreEqual(20,Usage(starter).minutes);Assert.AreEqual(0,c.Person(starter).appearances);Assert.IsTrue(Usage(starter).started);}
        [TestCase(360)][TestCase(2700)]
        public void SubstituteWhoIsThenSentOffOnlyReceivesTimeActuallyOnPitch(int half){Setup(half);Minute(60);sim.Substitute(0,1,bench);Minute(70);sim.SendOff(bench);Finish();Assert.AreEqual(60,Usage(starter).minutes);Assert.AreEqual(10,Usage(bench).minutes);Assert.IsFalse(Usage(bench).started);Assert.AreEqual(0,c.Person(bench).appearances);}
        [TestCase(360)][TestCase(2700)]
        public void InjuredPlayerStillPlayingAccruesMinutesUntilActualSubstitution(int half){Setup(half);Minute(20);sim.State.actors[1].injured=true;sim.State.events.Add(new MatchEvent{kind="injury",side=0,player=starter,time=sim.State.clock});Minute(35);sim.Substitute(0,1,bench);Finish();Assert.AreEqual(35,Usage(starter).minutes);Assert.AreEqual(55,Usage(bench).minutes);}
        [TestCase(360)][TestCase(2700)]
        public void SubstitutedSubstituteReceivesOnlyHisOwnInterval(int half){Setup(half);Minute(45);sim.Substitute(0,1,bench);string second=db.Squad(c.club).First(x=>!sim.State.used.Contains(x.id)).id;Minute(65);sim.Substitute(0,1,second);Finish();Assert.AreEqual(45,Usage(starter).minutes);Assert.AreEqual(20,Usage(bench).minutes);Assert.AreEqual(25,Usage(second).minutes);Assert.IsFalse(Usage(second).started);}
    }
}

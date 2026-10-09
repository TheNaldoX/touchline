using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Touchline.Core;

namespace Touchline.Tests
{
    public class BoardObjectiveTests
    {
        Database db;Career c;
        [SetUp] public void Setup()
        {
            db=new Database{
                leagues=new[]{new LeagueData{id="fra.1",name="Ligue de test"}},
                clubs=Enumerable.Range(0,4).Select(i=>new ClubData{id="c"+i,name="Club "+i,league="fra.1",playable=true,annualRevenue=10000000+i*1000000}).ToArray(),
                players=Enumerable.Range(0,96).Select(i=>new PlayerData{id="p"+i,name="Joueur "+i,team="c"+(i/24),age=24,rating=65,potential=75,fitness=100,morale=75,wage=500,value=100000,
                    position=i%24<2?"GB":i%24<10?"DEF":i%24<18?"MIL":"ATT",positions=new[]{i%24<2?"GK":i%24<10?"CB":i%24<18?"CM":"ST"}}).ToArray()
            };
            c=new Career{club="c0"};c.lineup=Career.Select(db,c.club,c.tactic);c.EnsureWorld(db);
        }
        [Test] public void BoardTargetRemainsFixedAfterRecruitmentAndRevenueChanges()
        {
            var objective=c.EnsureBoardObjective(db);int rank=objective.targetRank;long ceiling=objective.weeklyWageCeiling;
            foreach(var p in db.Squad(c.club))p.rating=99;
            db.clubs.First(x=>x.id==c.club).annualRevenue=999999999;c.life.revenue=999999999;
            var again=c.EnsureBoardObjective(db);
            Assert.AreSame(objective,again);Assert.AreEqual(rank,again.targetRank);Assert.AreEqual(ceiling,again.weeklyWageCeiling);
        }
        [Test] public void OldSaveCreatesOneBoardAgreementWithoutResettingTrust()
        {
            c.world.boardObjectives=null;c.life.boardTrust=42;
            c.EnsureBoardObjective(db);c.EnsureBoardObjective(db);
            Assert.AreEqual(1,c.world.boardObjectives.Count);Assert.AreEqual(42,c.life.boardTrust);
        }
        [Test] public void BoardAgreementAndReviewSurviveSaveReload()
        {
            c.ReviewBoardObjective(db);string before=JsonUtility.ToJson(c.world.boardObjectives[0]);
            var restored=JsonUtility.FromJson<Career>(JsonUtility.ToJson(c));
            restored.EnsureBoardObjective(db);
            Assert.AreEqual(before,JsonUtility.ToJson(restored.world.boardObjectives[0]));
            Assert.AreEqual(c.life.boardTrust,restored.life.boardTrust);
        }
        [Test] public void ANewSeasonCreatesANewAgreementAndKeepsTheOldOne()
        {
            var old=c.EnsureBoardObjective(db);c.world.year++;var next=c.EnsureBoardObjective(db);
            Assert.AreNotSame(old,next);Assert.AreEqual(old.season+1,next.season);Assert.IsTrue(c.world.boardObjectives.Contains(old));
        }
        [Test] public void MovingClubDoesNotOverwritePreviousBoardTargets()
        {
            var old=c.EnsureBoardObjective(db);c.life.day=50;c.approaches.Add(new JobApproach{club="c1",until=64});
            c.AnswerApproach(db,"c1",true);var next=c.EnsureBoardObjective(db);
            Assert.AreEqual("c1",next.club);Assert.AreEqual("c0",old.club);Assert.IsTrue(c.world.boardObjectives.Contains(old));
        }
        [Test] public void EarlySeasonReviewDoesNotJudgeAnEmptyTable()
        {
            float trust=c.life.boardTrust;c.ReviewBoardObjective(db);
            Assert.AreEqual(trust,c.life.boardTrust);Assert.AreEqual(0,c.EnsureBoardObjective(db).lastSportingChange);
        }
        [Test] public void RepeatedReviewCannotFarmTrustOrMessages()
        {
            foreach(var f in c.world.fixtures.Where(f=>f.league=="fra.1")){f.played=true;f.hg=f.home==c.club?4:0;f.ag=f.away==c.club?4:0;}
            c.life.day=200;c.EnsureBoardObjective(db).targetRank=4;c.ReviewBoardObjective(db);
            float trust=c.life.boardTrust;int messages=c.life.messages.Count;
            c.ReviewBoardObjective(db);c.life.day++;c.ReviewBoardObjective(db);
            Assert.AreEqual(trust,c.life.boardTrust);Assert.AreEqual(messages,c.life.messages.Count);
            Assert.Greater(c.EnsureBoardObjective(db).lastSportingChange,0);
        }
        [Test] public void FormerManagerDoesNotReceiveBoardReviews()
        {
            c.world.managerStatus="dismissed";int count=c.life.messages.Count;float trust=c.life.boardTrust;
            c.ReviewBoardObjective(db);Assert.AreEqual(count,c.life.messages.Count);Assert.AreEqual(trust,c.life.boardTrust);
        }
        [Test] public void BoardReportUsesActualTrustChangeAtTheCeiling()
        {
            foreach(var f in c.world.fixtures.Where(f=>f.league=="fra.1")){f.played=true;f.hg=f.home==c.club?4:0;f.ag=f.away==c.club?4:0;}
            c.life.day=200;c.life.boardTrust=99;c.EnsureBoardObjective(db).targetRank=4;c.ReviewBoardObjective(db);
            Assert.AreEqual(100,c.life.boardTrust);Assert.AreEqual(1,c.EnsureBoardObjective(db).lastSportingChange);
        }
        [Test] public void LateArrivalAccountsForInheritedResultsAndRemainingGames()
        {
            int initial=c.EnsureBoardObjective(db).targetRank;
            foreach(var f in c.world.fixtures.Where(f=>f.league=="fra.1"&&(f.home==c.club||f.away==c.club)).Take(5)){
                f.played=true;f.hg=f.home==c.club?0:3;f.ag=f.away==c.club?0:3;
            }
            c.world.boardObjectives.Clear();c.life.day=250;
            Assert.Greater(c.EnsureBoardObjective(db).targetRank,initial,"Le nouvel entraîneur ne peut effacer les cinq défaites héritées en un match.");
        }
    }
}

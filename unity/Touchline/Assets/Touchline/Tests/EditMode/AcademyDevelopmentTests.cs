using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Touchline.Core;

namespace Touchline.Tests
{
    public partial class ProfessionalTests
    {
        void AcademyTick(int day){c.life.day=day;typeof(Career).GetMethod("AcademyDevelopmentDay",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(c,new object[]{db});}
        [Test] public void AcademySessionsDoNotInventMatchMinutesAndDailyProcessingIsIdempotent()
        {
            var y=c.world.youth[0];int minutes=y.minutes;for(int day=1;day<=7;day++){AcademyTick(day);AcademyTick(day);}
            Assert.AreEqual(4,y.trainingSessions);Assert.AreEqual(minutes,y.minutes);Assert.AreEqual(0,y.seniorMinutes);Assert.AreEqual(0,y.trackedLoanMinutes);
        }
        [Test] public void AcademyRestAndInjuriesNeverTrain()
        {
            var y=c.world.youth[0];var p=db.Find(y.player);p.fitness=70;c.SetAcademyLoad(db,p.id,"rest");AcademyTick(1);Assert.AreEqual(0,y.progress);Assert.Greater(p.fitness,70);
            c.SetAcademyLoad(db,p.id,"standard");p.unavailableDays=3;AcademyTick(3);Assert.AreEqual(0,y.progress);Assert.AreEqual(0,y.trainingSessions);
        }
        [Test] public void AcademyBetterStaffAndFacilitiesImproveActualProgress()
        {
            var y=c.world.youth[0];var p=db.Find(y.player);c.Staff("youth").coaching=5;AcademyTick(1);float low=y.progress;y.progress=0;p.fitness=100;
            c.Staff("youth").coaching=18;c.life.facilities.First(f=>f.kind=="academy").level=5;AcademyTick(3);Assert.Greater(y.progress,low);
        }
        [Test] public void AcademyIntensiveLoadCostsConditionAndExhaustionTriggersRecovery()
        {
            var y=c.world.youth[0];var p=db.Find(y.player);c.SetAcademyLoad(db,p.id,"intensive");AcademyTick(1);Assert.AreEqual(92,p.fitness);float progress=y.progress;p.fitness=40;AcademyTick(3);Assert.AreEqual(progress,y.progress);Assert.AreEqual(46,p.fitness);
        }
        [Test] public void AcademyIntensiveLoadCannotRemainFreeOfFatigueForAMonth()
        {
            var intensive=c.world.youth[0];var normal=c.world.youth[1];c.SetAcademyLoad(db,intensive.player,"intensive");
            for(int day=1;day<=28;day++)AcademyTick(day);
            Assert.Less(db.Find(intensive.player).fitness,db.Find(normal.player).fitness-10);
            Assert.Greater(normal.progress,0);Assert.LessOrEqual(intensive.trainingSessions,normal.trainingSessions);
        }
        [Test] public void AcademyGrowthStopsAtPotentialAndPreservesStrongAttributes()
        {
            var y=c.world.youth[0];var p=db.Find(y.player);p.rating=70;p.potential=70.2f;p.attributes[0].value=90;y.progress=.9999f;AcademyTick(1);Assert.AreEqual(70.2f,p.rating);Assert.GreaterOrEqual(p.attributes[0].value,90);AcademyTick(3);Assert.AreEqual(0,y.progress);
        }
        [Test] public void AcademyMentoringHasARealCapacityAndKeeperCompatibility()
        {
            var mentor=db.Find("p1");mentor.age=30;var players=c.world.youth.Where(y=>!db.Find(y.player).Goalkeeper).Take(4).ToArray();
            foreach(var y in players.Take(3))c.SetYouthPlan(db,y.player,"technical",mentor.id);
            Assert.Throws<InvalidOperationException>(()=>c.SetYouthPlan(db,players[3].player,"technical",mentor.id));
            Assert.Throws<InvalidOperationException>(()=>c.SetYouthPlan(db,c.world.youth.First(y=>db.Find(y.player).Goalkeeper).player,"technical",mentor.id));
        }
        [Test] public void AcademyCannotPromoteOrTrainAPlayerSoldToAnotherClub()
        {
            var y=c.world.youth[0];db.Find(y.player).team="c1";Assert.Throws<InvalidOperationException>(()=>c.PromoteYouth(db,y.player));Assert.Throws<InvalidOperationException>(()=>c.SetYouthPlan(db,y.player,"balanced",null));Assert.Throws<InvalidOperationException>(()=>c.SetAcademyLoad(db,y.player,"rest"));Assert.AreEqual("c1",db.Find(y.player).team);
        }
        [Test] public void AcademyPromotionCannotRecallALoan()
        {
            var y=c.world.youth[0];c.PromoteYouth(db,y.player);var p=db.Find(y.player);p.age=19;p.rating=70;c.LoanYouth(db,p.id,"c4");
            Assert.Throws<InvalidOperationException>(()=>c.PromoteYouth(db,p.id));Assert.AreEqual("c4",p.team);Assert.AreEqual(c.club,c.Contract(db,p.id).parent);
        }
        [Test] public void AcademyPromotionRespectsWagesBeforeChangingOwnership()
        {
            var y=c.world.youth[0];var p=db.Find(y.player);p.wage=c.WageBudget+1;int contracts=c.world.contracts.Count;
            string issue=c.YouthPromotionIssue(db,p.id);Assert.That(issue,Does.Contain("plafond salarial"));Assert.AreEqual(contracts,c.world.contracts.Count);
            Assert.Throws<InvalidOperationException>(()=>c.PromoteYouth(db,p.id));Assert.AreEqual("academy-"+c.club,p.team);Assert.AreEqual(contracts,c.world.contracts.Count);
        }
        [Test] public void AcademyPromotionPreviewDoesNotCreateContractsAndAcceptsAnAffordablePlayer()
        {
            var y=c.world.youth[0];int contracts=c.world.contracts.Count;Assert.IsNull(c.YouthPromotionIssue(db,y.player));Assert.AreEqual(contracts,c.world.contracts.Count);
            c.PromoteYouth(db,y.player);Assert.AreEqual(c.club,db.Find(y.player).team);Assert.IsNotNull(c.YouthPromotionIssue(db,y.player));
        }
        [Test] public void AcademyPlansAndDailyMarkerSurviveSaveWithoutDoubleGrowth()
        {
            var y=c.world.youth[0];c.SetAcademyLoad(db,y.player,"light");AcademyTick(1);float progress=y.progress;
            c=JsonUtility.FromJson<Career>(JsonUtility.ToJson(c));c.RestoreWorld(db);AcademyTick(1);var restored=c.world.youth.First(x=>x.player==y.player);
            Assert.AreEqual("light",restored.trainingLoad);Assert.AreEqual(progress,restored.progress);Assert.AreEqual(1,restored.trainingSessions);
        }
        [Test] public void AcademyNewgenBroadPositionsAgreeWithFullBackAndHoldingRoles()
        {
            foreach(var p in c.world.youth.Select(y=>db.Find(y.player))){if(p.positions.Contains("LB")||p.positions.Contains("RB"))Assert.AreEqual("DEF",p.position);if(p.positions.Contains("DM"))Assert.AreEqual("MIL",p.position);}
        }
        [Test] public void AcademyMonthlyReportIsConsolidatedAndNotDuplicated()
        {
            AcademyTick(28);AcademyTick(28);Assert.AreEqual(1,c.life.messages.Count(m=>m.subject=="Bilan du centre de formation"));
        }
        [Test] public void AcademySeniorMinutesCountOnlyOnceAfterTheActualMatch()
        {
            var y=c.world.youth.First(x=>db.Find(x.player).positions.Contains("ST"));c.PromoteYouth(db,y.player);c.lineup[9]=y.player;
            var sim=MatchSimulation.Create(db,c,"c1");c.match=sim.State;c.life.recordedMatch=false;sim.State.finished=true;sim.State.clock=sim.State.HalfDuration*2;
            c.RecordMatch(db);int recorded=y.seniorMinutes;Assert.AreEqual(90,recorded);c.RecordMatch(db);Assert.AreEqual(recorded,y.seniorMinutes);
        }
    }
}

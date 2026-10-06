using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Touchline.Core;

namespace Touchline.Tests
{
    public partial class ProfessionalTests
    {
        ScoutMission Mission(string role="Tous",string nationality="Tous",int min=18,int max=30,long fee=200000,long wage=4000,string priority="ready",int count=4)=>c.CreateScoutMission(db,role,nationality,min,max,fee,wage,priority,c.ObservationCost*count);
        [Test] public void ScoutMissionReservesOnlyAuthorizedBudgetAndRefundsUnusedEnvelope()
        {
            long cash=c.life.cash;var m=Mission();Assert.AreEqual(cash-m.budget,c.life.cash);Days(1);Assert.AreEqual(1,m.found);Assert.AreEqual(m.budget-c.ObservationCost,m.remaining);
            cash=c.life.cash;long remaining=m.remaining;c.StopScoutMission(m.id);Assert.AreEqual(cash+remaining,c.life.cash);Assert.AreEqual(0,m.remaining);Assert.AreEqual("stopped",m.status);Assert.Throws<InvalidOperationException>(()=>c.StopScoutMission(m.id));Assert.AreEqual(cash+remaining,c.life.cash);
        }
        [Test] public void ScoutMissionCriteriaExcludeUnaffordableAndWrongPositionProfiles()
        {
            foreach(var p in db.players)p.nationality="France";db.Find("p25").nationality="Spain";db.Find("p25").value=50000;db.Find("p25").wage=300;db.Find("p25").age=19;
            var m=Mission("CB","Spain",18,21,60000,1400,"prospect");CollectionAssert.AreEqual(new[]{"p25"},c.ScoutCandidates(db,m).Select(p=>p.id));Days(1);CollectionAssert.AreEqual(new[]{"p25"},m.players);
        }
        [Test] public void ScoutDoesNotFindYoungTalentByReadingHiddenPotential()
        {
            var m=Mission(priority:"prospect");var before=c.ScoutCandidates(db,m).Select(p=>p.id).ToArray();foreach(var p in db.players)p.potential=99-p.potential/2;CollectionAssert.AreEqual(before,c.ScoutCandidates(db,m).Select(p=>p.id));
        }
        [Test] public void EmptySearchRefundsBudgetAndCannotSilentlySpendOnOtherCriteria()
        {
            var m=Mission("ST","No matching country");Days(1);Assert.AreEqual("complete",m.status);Assert.AreEqual(0,m.found);Assert.AreEqual(0,m.remaining);Assert.AreEqual(m.budget,c.life.ledger.Last(l=>l.label=="Reliquat mission d’observation").amount);
        }
        [Test] public void DetailedAttributesStayEstimatedAfterCompletedScouting()
        {
            c.Scout(db,"p24");Days(16);var a=c.AssessedAttribute(db,"p24","shortPassing");Assert.IsTrue(a.known);Assert.Less(a.low,a.high);Assert.AreEqual(90,c.Knowledge("p24"));
            c.revealAttributes=true;a=c.AssessedAttribute(db,"p24","shortPassing");Assert.AreEqual(a.low,a.high);Assert.AreEqual(13,a.low);
        }
        [Test] public void ScoutSkillChangesDurationAndFinalUncertainty()
        {
            var scout=c.Staff("scout");scout.judging=4;c.Scout(db,"p24");var low=c.ReportFor("p24");scout.judging=20;c.Scout(db,"p25");var high=c.ReportFor("p25");Assert.Less(high.due-high.started,low.due-low.started);Assert.Less(high.uncertainty,low.uncertainty);Assert.Less(high.potentialUncertainty,low.potentialUncertainty);
        }
        [Test] public void VacantRecruitmentPostSuspendsReportsAndBlocksNewSpending()
        {
            c.Scout(db,"p24");var r=c.ReportFor("p24");int due=r.due;c.Staff("scout").wage=0;long cash=c.life.cash;Assert.Throws<InvalidOperationException>(()=>c.Scout(db,"p25"));Assert.AreEqual(cash,c.life.cash);Days(3);Assert.AreEqual(due+3,r.due);Assert.AreEqual(0,r.confidence);
        }
        [Test] public void OldReportsCanBeRenewedWithoutLeakingActualLevel()
        {
            c.Scout(db,"p24");Days(16);var report=c.ReportFor("p24");float estimate=report.estimate;db.Find("p24").rating=95;Assert.AreEqual(estimate,c.AssessedLevel(db,"p24"));
            c.life.day=report.lastObserved+210;Assert.Less(c.Knowledge("p24"),90);c.Scout(db,"p24");Assert.AreEqual(0,c.Knowledge("p24"));
        }
        [Test] public void MissionSearchRemainsUsefulWhenAllAttributesAreVisible()
        {
            c.revealAttributes=true;var m=Mission();Days(1);Assert.AreEqual(1,m.found);Assert.IsNotNull(c.ReportFor(m.players[0]));
        }
        [Test] public void ScoutMissionAndJudgementSurviveUnitySaveRoundTrip()
        {
            var m=Mission();Days(1);var restored=JsonUtility.FromJson<Career>(JsonUtility.ToJson(c));restored.RestoreWorld(db);restored.EnsureWorld(db);Assert.AreEqual(m.remaining,restored.world.scoutMissions.Single().remaining);Assert.AreEqual(m.players[0],restored.world.scoutMissions.Single().players[0]);Assert.AreEqual(c.ReportFor(m.players[0]).judging,restored.ReportFor(m.players[0]).judging);
        }
        [Test] public void TerritorialSearchIsIndependentOfPlayerNationality()
        {
            db.leagues[0].country="France";db.leagues[1].country="England";foreach(var p in db.players)p.nationality="Spain";
            var m=c.CreateScoutMission(db,"Tous","Spain",18,30,200000,4000,"ready",c.ObservationCost,"England");var candidates=c.ScoutCandidates(db,m).ToArray();Assert.IsNotEmpty(candidates);Assert.IsTrue(candidates.All(p=>db.clubs.First(t=>t.id==p.team).league=="fra.2"));Assert.IsTrue(candidates.All(p=>p.nationality=="Spain"));
        }
        [Test] public void MissionObservationPriceStaysFixedWhenClubRevenueChanges()
        {
            var m=Mission();long price=m.observationCost;c.life.revenue=100000000;Days(1);Assert.AreEqual(1,m.found);Assert.AreEqual(m.budget-price,m.remaining);Assert.AreEqual(price,m.observationCost);
        }
        [Test] public void ManagerDepartureRefundsOldClubAndDoesNotCarryItsMissions()
        {
            var m=Mission();Days(1);long cash=c.life.cash,refund=m.remaining;c.approaches.Add(new JobApproach{club="c1",until=c.life.day+14});c.AnswerApproach(db,"c1",true);
            Assert.AreEqual("c1",c.club);Assert.AreEqual(cash+refund,c.previousClubs.Single(x=>x.club=="c0").life.cash);Assert.AreEqual("stopped",m.status);Assert.AreEqual(0,m.remaining);Assert.IsFalse(c.world.reports.Any(r=>r.club=="c0"&&r.confidence<90));Assert.AreEqual(cash+refund,c.world.aiAccounts.Single(a=>a.club=="c0").cash);
        }
        [Test] public void FreeAgentsAreScoutedWithoutAClubTerritoryWhenScopeIsAll()
        {
            db.Find("p24").team="free";db.Find("p24").value=99999999;var m=Mission(fee:0,priority:"free");CollectionAssert.AreEqual(new[]{"p24"},c.ScoutCandidates(db,m).Select(p=>p.id));Days(1);Assert.AreEqual("p24",m.players.Single());
        }
        [Test] public void IntegrityReviewCostsMoneyAndCannotEraseAnInvestigation()
        {
            c.life.suspicion=40;c.life.investigations.Add(new IntegrityCase{opened=c.life.day,due=c.life.day+20,status="pending"});long cash=c.life.cash;c.ReviewIntegrity();Assert.AreEqual(cash-c.IntegrityReviewCost,c.life.cash);Assert.AreEqual(35,c.life.suspicion);Assert.AreEqual("pending",c.life.investigations.Single().status);Assert.Throws<InvalidOperationException>(()=>c.ReviewIntegrity());
        }
    }
}

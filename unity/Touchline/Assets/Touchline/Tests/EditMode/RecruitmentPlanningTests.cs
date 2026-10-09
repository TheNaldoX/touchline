using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Touchline.Core;

namespace Touchline.Tests
{
    public class RecruitmentPlanningTests
    {
        Database db; Career c;
        [SetUp] public void Setup()
        {
            db = new Database { leagues = new[] { new LeagueData { id = "test", name = "Test" } },
                clubs = Enumerable.Range(0, 4).Select(i => new ClubData { id = "c" + i, name = "Club " + i, league = "test", playable = true, annualRevenue = 20000000 }).ToArray(),
                players = Enumerable.Range(0, 96).Select(i => new PlayerData { id = "p" + i, name = "Joueur " + i, team = "c" + i / 24,
                    position = i % 24 < 2 ? "GK" : "CB", positions = new[] { i % 24 < 2 ? "GK" : "CB" },
                    age = 24, rating = 65, potential = 80, value = 100000, wage = 500 }).ToArray() };
            c = new Career { club = "c0" }; c.lineup = Career.Select(db, c.club, c.tactic); c.EnsureWorld(db);
        }
        void PlanningSquad(params string[][] roles)
        {
            foreach (var p in db.Squad(c.club)) p.team = "c1";
            for (int i = 0; i < roles.Length; i++) { var p = db.Find("p" + i); p.team = c.club; p.position = roles[i][0]; p.positions = roles[i]; }
            c.tactic.withoutBall = new[] { new Slot("LB", 10, 30), new Slot("RB", 90, 30) };
        }
        void PlanningReport(string id, int confidence = 90, float level = 70, int age = 0)
        {
            c.world.reports.Add(new ScoutReport { player = id, club = c.club, confidence = confidence, estimate = level, started = c.life.day - age - 14, due = c.life.day - age, lastObserved = c.life.day - age });
        }
        [Test] public void RecruitmentDepthDoesNotCountVersatilePlayerTwice()
        {
            PlanningSquad(new[] { "LB", "RB" });
            var needs = c.RecruitmentOverview(db).needs;
            Assert.AreEqual(1, needs.Sum(n => n.assigned));
            Assert.AreEqual(1, needs.SelectMany(n => n.players).Distinct().Count());
            Assert.AreEqual(1, needs.Count(n => n.priority == 3));
        }
        [Test] public void RecruitmentDepthReassignsVersatileCoverToAvoidFalseShortage()
        {
            PlanningSquad(new[] { "LB", "RB" }, new[] { "LB" }, new[] { "LB" }, new[] { "RB" });
            Assert.IsTrue(c.RecruitmentOverview(db).needs.All(n => n.assigned == 2));
        }
        [Test] public void RecruitmentPlanSeparatesTemporaryAbsenceFromPermanentDepth()
        {
            PlanningSquad(new[] { "LB" }, new[] { "LB" }, new[] { "RB" }, new[] { "RB" });
            c.Person("p0").restUntil = c.life.day + 5;
            var need = c.RecruitmentOverview(db).needs.Single(n => n.role == "LB");
            Assert.AreEqual(2, need.assigned); Assert.AreEqual(1, need.available); Assert.AreEqual(1, need.priority);
        }
        [Test] public void RecruitmentPlanAnticipatesLoanReturnRatherThanParentContract()
        {
            PlanningSquad(new[] { "LB" }, new[] { "LB" });
            var contract = c.Contract(db, "p0"); contract.parent = "c1"; contract.until = c.life.day + 900; contract.loanUntil = c.life.day + 60;
            Assert.AreEqual(1, c.RecruitmentOverview(db).needs.Single(n => n.role == "LB").expiring);
        }
        [Test] public void RecruitmentAbsentSpecialistDoesNotDisplaceAvailableVersatileCover()
        {
            PlanningSquad(new[] { "LB" }, new[] { "LB", "RB" }, new[] { "RB" });
            c.Person("p2").restUntil = c.life.day + 5;
            Assert.IsTrue(c.RecruitmentOverview(db).needs.All(n => n.available >= n.starters));
        }
        [Test] public void RecruitmentRecommendationsNeverReadUnobservedAbility()
        {
            Assert.IsEmpty(c.RecruitmentRecommendations(db));
            PlanningReport("p24", level: 62); PlanningReport("p25", level: 75);
            var before = c.RecruitmentRecommendations(db).Select(r => r.player + ":" + r.assessedLevel).ToArray();
            db.Find("p24").rating = 99; db.Find("p24").potential = 99; db.Find("p25").rating = 1;
            CollectionAssert.AreEqual(before, c.RecruitmentRecommendations(db).Select(r => r.player + ":" + r.assessedLevel));
        }
        [Test] public void RecruitmentRejectsLegacyEmptyEstimateAndForeignClubReports()
        {
            PlanningReport("p24", level: 0); PlanningReport("p25"); c.world.reports.Last().club = "c2";
            Assert.IsEmpty(c.RecruitmentRecommendations(db));
        }
        [Test] public void RecruitmentReportFreshnessAndProgressRemainDistinct()
        {
            c.life.day = 400; PlanningReport("p24", age: 121); PlanningReport("p25", confidence: 60); PlanningReport("p26", age: 120);
            var overview = c.RecruitmentOverview(db);
            Assert.AreEqual(1, overview.reportsStale); Assert.AreEqual(1, overview.reportsReady); Assert.AreEqual(1, overview.observations);
            Assert.IsTrue(c.RecruitmentRecommendations(db).Single(r => r.player == "p24").stale);
        }
        [Test] public void RecruitmentFreeAgentHasNoTransferFeeButStillNeedsSalaryRoom()
        {
            PlanningReport("p24"); var p = db.Find("p24"); p.team = "free"; p.value = 100000000; p.wage = 100000000;
            var item = c.RecruitmentRecommendations(db).Single();
            Assert.AreEqual(0, item.estimatedFee); Assert.AreEqual(Career.MonthlySalary(p.wage), item.currentMonthlyWage); Assert.IsFalse(item.affordable);
        }
        [Test] public void RecruitmentWageRoomDeductsSignedFutureArrivals()
        {
            long before = c.RecruitmentOverview(db).monthlyWageRoom;
            c.world.offers.Add(new TransferOffer { player = "p24", destination = c.club, status = "scheduled", wage = 12000 });
            long after = c.RecruitmentOverview(db).monthlyWageRoom;
            Assert.AreEqual(Math.Max(0, before - Career.MonthlySalary(12000)), after);
        }
        [Test] public void RecruitmentViewsAreReadOnlyAndDeterministic()
        {
            PlanningReport("p24"); string before = JsonUtility.ToJson(c);
            var first = c.RecruitmentOverview(db).needs.Select(n => n.role + string.Join(",", n.players)).ToArray();
            c.RecruitmentRecommendations(db);
            CollectionAssert.AreEqual(first, c.RecruitmentOverview(db).needs.Select(n => n.role + string.Join(",", n.players)));
            Assert.AreEqual(before, JsonUtility.ToJson(c));
        }
        [Test] public void RecruitmentRevealSettingEnablesKnownProfilesAndPositionFilter()
        {
            c.revealAttributes = true;
            var result = c.RecruitmentRecommendations(db, "CB");
            Assert.IsNotEmpty(result); Assert.IsTrue(result.All(r => r.knowledge == 100 && FootballPositions.Matches(db.Find(r.player), "CB")));
            Assert.IsEmpty(c.RecruitmentRecommendations(db, limit: 0));
        }
    }
}

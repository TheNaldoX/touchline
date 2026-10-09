using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Touchline.Core;

namespace Touchline.Tests
{
    // Scouting department, hidden personality, player willingness and settling of new signings.
    public class RecruitmentDepartmentTests
    {
        [Test] public void SavedRecommendationIsHiddenWhenPlayerNoLongerConsidersTheClub()
        {
            var player=db.players.First(p=>p.team!=c.club&&!p.Goalkeeper);
            c.revealAttributes=true;
            foreach(var teammate in db.Squad(player.team))teammate.rating=99;
            c.scoutRecommendations.Add(player.id);
            Assert.IsTrue(c.PlayerTransferInterest(db,player.id).refuses);
            Assert.IsFalse(c.DepartmentRecommendations(db).Contains(player.id));
            Assert.IsTrue(c.scoutRecommendations.Contains(player.id),"Keep the historical report without presenting it as a current recommendation.");
        }
        Database db; Career c;
        [SetUp] public void Setup()
        {
            db = new Database{leagues = new[]{new LeagueData{id = "fra.1", name = "L1", country = "France"}, new LeagueData{id = "esp.1", name = "L2", country = "Espagne"}},
                clubs = Enumerable.Range(0, 8).Select(i => new ClubData{id = "c" + i, name = "Club " + i, league = i < 4 ? "fra.1" : "esp.1", playable = i < 4, annualRevenue = 20000000 + i * 1000000}).ToArray(),
                players = Enumerable.Range(0, 8 * 24).Select(i => new PlayerData{id = "p" + i, name = "Alex " + i, team = "c" + (i / 24), age = 24, nationality = i / 24 < 4 ? "France" : "Spain",
                    position = i % 24 == 0 || i % 24 == 12 ? "GB" : i % 24 < 8 ? "DEF" : i % 24 < 16 ? "MIL" : "ATT",
                    positions = new[]{i % 24 == 0 || i % 24 == 12 ? "GK" : i % 24 < 8 ? "CB" : i % 24 < 16 ? "CM" : "ST"}, rating = 65, potential = 80, value = 100000, wage = 500, fitness = 100, morale = 75,
                    attributes = new[]{new AttributeValue{key = "standingTackle", value = 80}, new AttributeValue{key = "shortPassing", value = 65}, new AttributeValue{key = "sprintSpeed", value = 45},
                        new AttributeValue{key = "headingAccuracy", value = 70}, new AttributeValue{key = "finishing", value = 50}, new AttributeValue{key = "dribbling", value = 60}}}).ToArray()};
            c = new Career{club = "c0"}; c.lineup = Career.Select(db, c.club, c.tactic); c.EnsureWorld(db);
        }
        void Finish(){var f = c.NextFixture(); if (f == null || f.day > c.life.day) return; c.PrepareLineup(db); var sim = MatchSimulation.Create(db, c, f.home == c.club ? f.away : f.home); c.match = sim.State; c.world.activeFixture = f.id; c.ApplyMatchContext(sim); sim.State.finished = true; sim.State.clock = 720; sim.State.score[0] = 1; sim.State.score[1] = 1; c.RecordMatch(db); c.match = null;}
        void Days(int count){for (int i = 0; i < count; i++){Finish(); c.AdvanceDay(db);}}
        StaffMember ExtraScout(int judging)
        {
            c.EnsureStaffMarket(db); var s = c.staffMarket.First(m => m.role == "scout" && m.club == null);
            s.club = c.club; s.wage = 600; s.until = c.life.day + 700; s.judging = judging; c.life.staff.members.Add(s); return s;
        }

        [Test] public void DepartmentHiresScoutsUpToBudgetSlots()
        {
            c.EnsureStaffMarket(db); Assert.AreEqual(2, c.ScoutSlots, "20 M€ de recettes : chef recruteur + un recruteur");
            var free = c.staffMarket.Where(m => m.role == "scout" && m.club == null).Take(2).ToArray();
            c.ProposeStaffContract(db, free[0].id, Career.MonthlySalary(free[0].wage * 2), 2); Days(3); c.SignStaffContract(db, free[0].id);
            Assert.AreEqual(2, c.ScoutingDepartment(db).Count); Assert.IsTrue(c.ScoutingDepartment(db).Count(s => s.chief) == 1);
            c.ProposeStaffContract(db, free[1].id, Career.MonthlySalary(free[1].wage * 2), 2); Days(3);
            var full = Assert.Throws<InvalidOperationException>(() => c.SignStaffContract(db, free[1].id));
            StringAssert.Contains("cellule", full.Message);
        }

        [Test] public void AssignedScoutReportsOverDaysLearnsTerritoryAndRecommends()
        {
            foreach (var p in db.players.Where(p => p.team == "c5" && !p.Goalkeeper)) p.rating = 72; // above our eleven, still reachable
            var scout = ExtraScout(15); string key = scout.id; long cash = c.life.cash;
            int before = c.RegionKnowledge(key, "Espagne");
            c.AssignScout(db, key, "territory", "Espagne");
            Assert.AreEqual(0, c.ActiveObservations, "les affectations ne prennent pas les places des observations manuelles");
            Days(60);
            var reports = c.world.reports.Where(r => r.mission == "assign-" + key).ToArray();
            Assert.GreaterOrEqual(reports.Length, 2); Assert.IsTrue(reports.All(r => new[]{"c4", "c5", "c6", "c7"}.Contains(db.Find(r.player).team)));
            Assert.IsTrue(reports.Any(r => r.confidence >= 90), "un rapport arrive après plusieurs jours");
            Assert.IsTrue(c.life.ledger.Any(e => e.label.StartsWith("Cellule de recrutement") && e.amount < 0), "les déplacements sont facturés");
            Assert.Greater(c.RegionKnowledge(key, "Espagne"), before);
            Assert.IsTrue(c.DepartmentRecommendations(db).Count > 0, "la cellule recommande les profils notés A ou B");
            Assert.IsTrue(c.DepartmentRecommendations(db).All(id => !c.PlayerTransferInterest(db, id).refuses), "pas de recommandation d’un joueur qui refuse le club");
            Assert.IsTrue(c.life.messages.Any(m => m.subject == "Recommandation de la cellule"));
            Assert.AreEqual(1, c.ScoutingDepartment(db).Single(s => s.key == key).assignment.reports > 0 ? 1 : 0);
        }

        [Test] public void BetterScoutChoosesBetterPlayersToObserve()
        {
            var rng = new System.Random(7); foreach (var p in db.players.Where(p => p.team != "c0")) p.rating = 55 + rng.Next(0, 26);
            var good = ExtraScout(20); var poor = new StaffMember{id = "poor-scout", name = "Recruteur faible", role = "scout", judging = 1, people = 10, wage = 300, club = c.club};
            var a = new ScoutAssignment{focus = "territory", country = "Tous", role = "Tous", maxAge = 35};
            float Top(StaffMember s) => c.AssignmentCandidates(db, s, a).Take(20).Average(p => p.rating + p.development);
            Assert.Greater(Top(good), Top(poor) + 1, "un bon recruteur cible des joueurs réellement meilleurs");
            Assert.Greater(Top(good), 77.5f);
        }

        [Test] public void PotentialRangeFollowsTheScoutsPotentialJudging()
        {
            int day = c.life.day; var keen = "p30"; var blind = "p31";
            c.world.reports.Add(new ScoutReport{player = keen, club = c.club, started = day - 30, due = day - 1, lastObserved = day - 1, confidence = 90, judging = 12, estimate = 65, potential = 80, uncertainty = 3.26f, potentialUncertainty = 3 + 1.5f});
            c.world.reports.Add(new ScoutReport{player = blind, club = c.club, started = day - 30, due = day - 1, lastObserved = day - 1, confidence = 90, judging = 12, estimate = 65, potential = 80, uncertainty = 3.26f, potentialUncertainty = 3 + 5.68f});
            var a = c.ScoutReportCard(db, keen); var b = c.ScoutReportCard(db, blind);
            Assert.AreEqual(a.ability.high - a.ability.low, b.ability.high - b.ability.low, 1, "même jugement du niveau");
            Assert.Less(a.potential.high - a.potential.low, b.potential.high - b.potential.low);
            Assert.IsTrue(a.potential.low <= 80 && a.potential.high >= 80 && b.potential.low <= 80 && b.potential.high >= 80);
        }

        [Test] public void PersonalityIsHiddenUntilObservedAndRangesContainTheTruth()
        {
            Assert.IsFalse(c.AssessedTrait(db, "p30", Career.Ambition).known);
            var own = c.AssessedTrait(db, "p3", Career.Adaptability); Assert.IsTrue(own.known); Assert.AreEqual(own.low, own.high);
            c.Scout(db, "p30"); Days(25);
            foreach (var trait in Career.PersonalityTraits)
            {
                var t = c.AssessedTrait(db, "p30", trait); int truth = Career.PersonalityTrait("p30", trait);
                Assert.IsTrue(t.known); Assert.IsTrue(t.low <= truth && truth <= t.high, trait + " " + t + " / " + truth);
            }
            var card = c.ScoutReportCard(db, "p30"); Assert.IsTrue(card.ambition.known); StringAssert.StartsWith("Comparable à ", card.comparable); Assert.IsNotNull(card.interest);
            var values = db.players.Select(p => Career.PersonalityTrait(p.id, Career.Ambition)).ToArray();
            Assert.IsTrue(values.All(v => v >= 1 && v <= 20)); Assert.Greater(values.Distinct().Count(), 8);
        }

        [Test] public void StepDownRequiresWagePremiumOrIsRefused()
        {
            foreach (var p in db.players.Where(p => p.team == "c1")) p.rating = 72;   // a stronger seller
            foreach (var p in db.players.Where(p => p.team == "c3")) p.rating = 90;   // far out of reach
            Assert.IsTrue(c.WindowOpen, "fenêtre ouverte au début de la carrière de test"); c.life.cash = 100000000;
            var star = db.players.First(p => p.team == "c1" && !p.Goalkeeper && Career.PersonalityTrait(p.id, Career.Ambition) >= 15);
            var interest = c.PlayerTransferInterest(db, star.id, "starter");
            Assert.AreEqual(1, interest.level, "hésitant"); Assert.Greater(interest.requiredWeeklyWage, (long)(star.wage * 1.2f));
            c.ProposeTransfer(db, star.id, star.value * 2, (long)(star.wage * 1.13f), 3, "starter"); Days(3);
            var offer = c.world.offers.Last(o => o.player == star.id);
            Assert.AreEqual("counter", offer.status); Assert.AreEqual(interest.requiredWeeklyWage, offer.wage);
            Assert.GreaterOrEqual(c.PlayerAgent(db, star.id).monthlyLow, Career.MonthlySalary(interest.requiredWeeklyWage) * 95 / 100 - 1, "l’agent annonce la prime exigée");
            c.ProposeTransfer(db, star.id, star.value * 2, interest.requiredWeeklyWage, 3, "starter"); Days(3);
            Assert.AreEqual("accepted", c.world.offers.Last(o => o.player == star.id).status, "la prime salariale convainc le joueur");
            var unreachable = db.players.First(p => p.team == "c3" && !p.Goalkeeper && Career.PersonalityTrait(p.id, Career.Ambition) >= 10);
            Assert.IsTrue(c.PlayerTransferInterest(db, unreachable.id, "key").refuses);
            c.ProposeTransfer(db, unreachable.id, unreachable.value * 2, unreachable.wage * 3, 3, "key"); Days(3);
            Assert.AreEqual("declined", c.world.offers.Last(o => o.player == unreachable.id).status);
            var peer = db.players.First(p => p.team == "c2" && p.position == "ATT");
            Assert.AreEqual(3, c.PlayerTransferInterest(db, peer.id, "starter").level, "même niveau, statut de titulaire : très intéressé");
        }

        [Test] public void NewSigningSettlesAndItsMatchModifierFades()
        {
            var target = db.players.First(p => p.team == "c5" && p.position == "MIL"); target.rating = 60;
            c.Scout(db, target.id); Days(25); Assert.IsTrue(c.WindowOpen);
            c.life.cash = 100000000; var interest = c.PlayerTransferInterest(db, target.id, "starter");
            c.ProposeTransfer(db, target.id, target.value * 2, Math.Max(interest.requiredWeeklyWage, target.wage * 2), 3, "starter"); Days(3);
            Assert.AreEqual("accepted", c.world.offers.Last(o => o.player == target.id).status);
            c.SignTransfer(db, target.id); Days(1);
            var record = c.signings.Single(s => s.player == target.id);
            Assert.AreEqual("Espagne", record.fromCountry); Assert.AreEqual(.05f, record.penalty, 1e-4, "arrivée de l’étranger");
            Assert.Greater(record.estimate, 0); Assert.GreaterOrEqual(record.knowledge, 90);
            Assert.Less(c.SettlingModifier(target.id), -.04f); Assert.Less(db.Find(target.id).performanceModifier, -.04f);
            Days(record.settleDays / 2); float half = c.SettlingModifier(target.id); Assert.Less(half, 0); Assert.Greater(half, -.04f);
            Days(record.settleDays); Assert.AreEqual(0, c.SettlingModifier(target.id)); Assert.IsTrue(record.settled);
            Assert.AreEqual(0, db.Find(target.id).performanceModifier, 1e-6);
            Assert.IsTrue(c.life.messages.Any(m => m.subject == "Intégration terminée"));
            var review = c.RecruitmentReview(db).Single(r => r.player == target.id); Assert.AreEqual(1, review.progress); Assert.IsNotNull(review.verdict);
        }

        [Test] public void DepartmentStateSurvivesSave()
        {
            var scout = ExtraScout(12); c.AssignScout(db, scout.id, "youth", "Espagne", "ST", 30);
            c.scoutRegions.Add(new ScoutRegionKnowledge{staff = scout.id, country = "Italie", level = 33}); c.scoutRecommendations.Add("p100");
            c.signings.Add(new SigningRecord{player = "p101", from = "c5", fromCountry = "Espagne", day = 3, settleDays = 90, penalty = .05f, estimate = 66, actual = 64, knowledge = 90});
            var restored = JsonUtility.FromJson<Career>(JsonUtility.ToJson(c));
            var a = restored.scoutAssignments.Single(); Assert.AreEqual("youth", a.focus); Assert.AreEqual(21, a.maxAge, "jeunes : 21 ans au plus"); Assert.AreEqual("Espagne", a.country);
            Assert.AreEqual(33, restored.RegionKnowledge(scout.id, "Italie")); Assert.AreEqual("p100", restored.scoutRecommendations.Single());
            Assert.AreEqual(90, restored.signings.Single().settleDays); Assert.AreEqual(.05f, restored.signings.Single().penalty, 1e-6);
        }

        [Test] public void LegacySigningStaysWithFormerClubAfterDepartureAndSave()
        {
            c.signings.Add(new SigningRecord{player="p3",day=c.life.day,settleDays=90,penalty=.05f});
            Assert.Less(c.SettlingModifier("p3"),0);
            c.approaches.Add(new JobApproach{club="c1",until=c.life.day+1});
            c.AnswerApproach(db,"c1",true);
            Assert.AreEqual("c0",c.signings.Single().club);
            Assert.AreEqual("c1",c.club);
            var restored=JsonUtility.FromJson<Career>(JsonUtility.ToJson(c));
            Assert.AreEqual(0,restored.SettlingModifier("p3"));
            Assert.AreEqual(0,restored.RecruitmentReview(db).Count);
            Assert.AreEqual(1,restored.signings.Count,"History is preserved, not deleted.");
            restored.club="c0";
            Assert.Less(restored.SettlingModifier("p3"),0);
            Assert.AreEqual(1,restored.RecruitmentReview(db).Count);
        }

        [Test] public void RepeatSigningReviewUsesEachArrivalsOwnProgress()
        {
            c.life.day=200;
            c.signings.Add(new SigningRecord{player="p3",club=c.club,day=10,settleDays=90,penalty=.05f});
            c.signings.Add(new SigningRecord{player="p3",club=c.club,day=190,settleDays=100,penalty=.02f});
            c.signings.Add(new SigningRecord{player="p3",club="c1",day=195,settleDays=100,penalty=.05f});
            var reviews=c.RecruitmentReview(db);
            Assert.AreEqual(2,reviews.Count);
            Assert.AreEqual(1,reviews.Single(r=>r.record.day==10).progress);
            Assert.AreEqual(.1f,reviews.Single(r=>r.record.day==190).progress,1e-6);
            Assert.AreEqual(-.018f,c.SettlingModifier("p3"),1e-6,"A more recent arrival at another club cannot replace this club's record.");
        }

        [Test] public void FormerClubArrivalDoesNotSendSettledMailAtCurrentClub()
        {
            c.life.day=100;
            c.signings.Add(new SigningRecord{player="p3",club="c1",day=1,settleDays=45,penalty=.05f});
            typeof(Career).GetMethod("SigningsDay",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(c,new object[]{db});
            Assert.IsTrue(c.signings.Single().settled);
            Assert.IsFalse(c.life.messages.Any(m=>m.subject=="Intégration terminée"));
        }

        [Test] public void DeadlineDayAcceleratesRivalBidsWithinTheWindow()
        {
            int Day(int y, int m, int d) => (int)(new DateTime(y, m, d) - Career.Epoch).TotalDays;
            int year = c.Date.Year;
            c.life.day = Day(year, 7, 10); Assert.IsFalse(c.DeadlinePeriod); Assert.AreEqual(Day(year, 9, 1), c.WindowCloseDay);
            c.life.day = Day(year, 10, 10); Assert.AreEqual(-1, c.WindowCloseDay);
            foreach (var p in db.players.Where(p => p.team != c.club && !p.Goalkeeper)) c.shortlist.Add(p.id);
            var day = typeof(Career).GetMethod("RecruitmentRivalsDay", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            for (int d = Day(year, 8, 26); d <= Day(year, 8, 31); d++){c.life.day = d; Assert.IsTrue(c.DeadlinePeriod); day.Invoke(c, new object[]{db});}
            Assert.Greater(c.rivalBids.Count, 0, "activité quotidienne des rivaux en fin de mercato");
            Assert.IsTrue(c.rivalBids.All(b => b.decision <= Day(year, 9, 1)), "le vendeur tranche avant la fermeture");
            Assert.IsTrue(c.rivalBids.Any(b => b.day % 7 != 0), "offres possibles hors du cycle hebdomadaire");
        }

        [Test] public void LanguageAndAbroadLengthenSettling()
        {
            Assert.AreEqual(Career.PersonalityTrait("x", Career.Adaptability), Career.PersonalityTrait("x", Career.Adaptability), "valeur stable");
            var c2 = db.players.First(p => p.team == "c2" && p.position == "DEF"); c2.nationality = "France";
            var c6 = db.players.First(p => p.team == "c6" && p.position == "DEF"); c6.nationality = "Spain";
            c.life.cash = 100000000; Assert.IsTrue(c.WindowOpen);
            foreach (var p in new[]{c2, c6}){p.rating = 58; c.ProposeTransfer(db, p.id, p.value * 2, p.wage * 3, 2, "rotation");}
            Days(3); foreach (var p in new[]{c2, c6}) c.SignTransfer(db, p.id); Days(1);
            var home = c.signings.Single(s => s.player == c2.id); var away = c.signings.Single(s => s.player == c6.id);
            Assert.AreEqual(.02f, home.penalty, 1e-6); Assert.AreEqual(0, home.knowledge, "signé sans rapport");
            int adaptHome = Career.PersonalityTrait(c2.id, Career.Adaptability), adaptAway = Career.PersonalityTrait(c6.id, Career.Adaptability);
            Assert.AreEqual(Math.Max(21, 45 - (adaptHome - 10) * 4), home.settleDays);
            Assert.AreEqual(Math.Min(200, Math.Max(21, 120 + 30 - (adaptAway - 10) * 4)), away.settleDays);
            Assert.AreEqual("Recruté sans rapport complet", c.RecruitmentReview(db).Single(r => r.player == c2.id).verdict);
        }
    }
}

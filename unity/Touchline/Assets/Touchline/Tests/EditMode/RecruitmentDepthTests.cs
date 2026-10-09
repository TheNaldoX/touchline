using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Touchline.Core;

namespace Touchline.Tests
{
    public class RecruitmentDepthTests
    {
        Database db; Career c;
        [SetUp] public void Setup()
        {
            db = new Database{leagues = new[]{new LeagueData{id = "fra.1", name = "L1", country = "France"}, new LeagueData{id = "esp.1", name = "L2", country = "Espagne"}},
                clubs = Enumerable.Range(0, 8).Select(i => new ClubData{id = "c" + i, name = "Club " + i, league = i < 4 ? "fra.1" : "esp.1", playable = i < 4, annualRevenue = 20000000 + i * 1000000}).ToArray(),
                players = Enumerable.Range(0, 8 * 24).Select(i => new PlayerData{id = "p" + i, name = "Alex " + i, team = "c" + (i / 24), age = 24,
                    position = i % 24 == 0 || i % 24 == 12 ? "GB" : i % 24 < 8 ? "DEF" : i % 24 < 16 ? "MIL" : "ATT",
                    positions = new[]{i % 24 == 0 || i % 24 == 12 ? "GK" : i % 24 < 8 ? "CB" : i % 24 < 16 ? "CM" : "ST"}, rating = 65, potential = 80, value = 100000, wage = 500, fitness = 100, morale = 75,
                    attributes = new[]{new AttributeValue{key = "standingTackle", value = 80}, new AttributeValue{key = "shortPassing", value = 65}, new AttributeValue{key = "sprintSpeed", value = 45},
                        new AttributeValue{key = "headingAccuracy", value = 70}, new AttributeValue{key = "finishing", value = 50}, new AttributeValue{key = "dribbling", value = 60}}}).ToArray()};
            c = new Career{club = "c0"}; c.lineup = Career.Select(db, c.club, c.tactic); c.EnsureWorld(db);
        }
        void Finish(){var f = c.NextFixture(); if (f == null || f.day > c.life.day) return; c.PrepareLineup(db); var sim = MatchSimulation.Create(db, c, f.home == c.club ? f.away : f.home); c.match = sim.State; c.world.activeFixture = f.id; c.ApplyMatchContext(sim); sim.State.finished = true; sim.State.clock = 720; sim.State.score[0] = 1; sim.State.score[1] = 1; c.RecordMatch(db); c.match = null;}
        void Days(int count){for (int i = 0; i < count; i++){Finish(); c.AdvanceDay(db);}}
        static int Width(ScoutRange r) => r.high - r.low;
        static void Age(ScoutReport r, int days){r.started -= days; r.due -= days; r.lastObserved -= days;}

        // --- Generated leagues -------------------------------------------------
        [TestCase(false)][TestCase(true)] public void ImportedCareerDoesNotInventStartingYouthAndPolicySurvivesSave(bool imported)
        {
            db.players=db.players.Where(p=>!p.id.StartsWith("gen-",StringComparison.Ordinal)).ToArray();
            var state=new Career{club="c0",youthGenerationFromYear=imported?Career.ImportedRosterYear+1:0};
            state.lineup=Career.Select(db,state.club,state.tactic);state.EnsureWorld(db);
            Assert.AreEqual(!imported,db.players.Any(p=>p.id.StartsWith("gen-",StringComparison.Ordinal)));
            var restored=JsonUtility.FromJson<Career>(JsonUtility.ToJson(state));
            Assert.AreEqual(state.youthGenerationFromYear,restored.youthGenerationFromYear);
            if(imported){
                restored.world.year++;
                typeof(Career).GetMethod("CreateIntake",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(restored,new object[]{db});
                Assert.IsTrue(db.players.Any(p=>p.id.StartsWith("gen-",StringComparison.Ordinal)));
            }
        }
        [Test] public void GeneratedLeaguesAreDeterministicIdempotentAndFrozen()
        {
            var a = new Database{leagues = new LeagueData[0], clubs = new ClubData[0], players = new PlayerData[0]};
            var b = new Database{leagues = new LeagueData[0], clubs = new ClubData[0], players = new PlayerData[0]};
            Assert.AreEqual(2784, AppendFrozen(a)); Assert.AreEqual(0, AppendFrozen(a)); AppendFrozen(b);
            Assert.AreEqual(GeneratedWorld.Fingerprint(a), GeneratedWorld.Fingerprint(b));
            // Saves only store changed players: version 1 output must never change.
            Assert.AreEqual(FrozenFingerprintV1, GeneratedWorld.Fingerprint(a), "Générateur v1 modifié : créer une version 2 au lieu de changer v1.");
            Assert.AreEqual(8, a.leagues.Length); Assert.AreEqual(116, a.clubs.Length);
            Assert.IsTrue(a.leagues.All(l => l.scoutingOnly && l.name.Contains("générée") && l.rulesNote.Contains("fictifs")));
            Assert.IsTrue(a.clubs.All(x => !x.playable && GeneratedWorld.IsGenerated(x.id) && x.financeSource.Contains("fictif")));
            Assert.AreEqual(a.clubs.Length, a.clubs.Select(x => x.name).Distinct(StringComparer.OrdinalIgnoreCase).Count());
            Assert.AreEqual(a.players.Length, a.players.Select(p => p.id).Distinct().Count());
            Assert.IsTrue(a.players.All(p => p.source.StartsWith("touchline:generated") && p.assessment.Contains("fictif") && p.attributes.Length == 40));
        }
        const ulong FrozenFingerprintV1 = GeneratedWorld.FrozenFingerprintV1;
        static Database Frozen()=>JsonUtility.FromJson<Database>(Resources.Load<TextAsset>("Data/generated-world-v1").text);
        static int AppendFrozen(Database target)=>GeneratedWorld.AppendFrozen(target,Frozen());
        [Test] public void CorruptFrozenCatalogueIsRejectedBeforeChangingTheWorld()
        {
            var snapshot=Frozen();snapshot.players[0].wage++;
            int count=db.players.Length;Assert.Throws<InvalidOperationException>(()=>GeneratedWorld.AppendFrozen(db,snapshot));
            Assert.AreEqual(count,db.players.Length);
        }
        [Test] public void FrozenLoadsHaveIndependentMutablePlayersAndKeepRealData()
        {
            var real=db.players[0];AppendFrozen(db);var fresh=Frozen();
            var fictional=db.Find(fresh.players[0].id);fictional.wage++;
            Assert.AreNotEqual(fictional.wage,fresh.players[0].wage);Assert.AreSame(real,db.players[0]);
            Assert.AreEqual(FrozenFingerprintV1,GeneratedWorld.Fingerprint(Frozen()));
        }
        [Test] public void GeneratedPlayersFollowLeagueLevelPositionsAndAges()
        {
            var w = new Database{leagues = new LeagueData[0], clubs = new ClubData[0], players = new PlayerData[0]}; AppendFrozen(w);
            var league = w.clubs.GroupBy(x => x.league).ToDictionary(g => g.Key, g => g.Select(x => x.id).ToHashSet());
            float Mean(string id) => w.players.Where(p => league[id].Contains(p.team)).Average(p => p.rating);
            Assert.Greater(Mean("fic.pol.1"), Mean("fic.aut.2") + 4);
            Assert.That(Mean("fic.swe.1"), Is.InRange(56f, 63f));
            Assert.IsTrue(w.players.All(p => p.age >= 16 && p.age <= 37 && p.rating >= 42 && p.rating <= 82 && p.potential >= p.rating && p.value > 0 && p.wage >= 150));
            var keepers = w.players.Where(p => p.Goalkeeper).ToArray(); Assert.AreEqual(116 * 3, keepers.Length);
            Assert.IsTrue(keepers.Average(p => p.Attribute("gkDiving")) > keepers.Average(p => p.Attribute("finishing")) + 30);
            var defenders = w.players.Where(p => p.position == "DEF").ToArray();
            Assert.Greater(defenders.Average(p => p.Attribute("standingTackle")), defenders.Average(p => p.Attribute("finishing")) + 15);
            // Value grows with level inside one league.
            var pol = w.players.Where(p => league["fic.pol.1"].Contains(p.team) && p.age >= 24 && p.age <= 28).ToArray();
            Assert.Greater(pol.Where(p => p.rating >= 66).Average(p => p.value), pol.Where(p => p.rating <= 58).Average(p => p.value) * 2);
            Assert.IsTrue(w.players.Where(p => league["fic.swe.1"].Contains(p.team)).Count(p => p.nationality == "Sweden") > 300);
        }
        [Test] public void GeneratedLeaguesAreScoutableTerritoriesWithoutFixtures()
        {
            AppendFrozen(db); c.world = null; c.EnsureWorld(db);
            Assert.IsFalse(c.world.fixtures.Any(f => GeneratedWorld.IsGenerated(f.home) || GeneratedWorld.IsGenerated(f.away)));
            Assert.IsTrue(ScoutingGeography.Countries(db).Contains("Suède"));
            var clubs = ScoutingGeography.ClubIds(db, "Suède").ToArray(); Assert.AreEqual(16, clubs.Length);
            var p = db.players.First(x => x.team == clubs[0] && x.age >= 18); c.Scout(db, p.id); Assert.IsNotNull(c.ReportFor(p.id));
        }
        [Test] public void SimulatedTableIsDeterministicAndComplete()
        {
            AppendFrozen(db);
            var first = GeneratedWorld.SimulatedTable(db, "fic.irl.1", 2026); var again = GeneratedWorld.SimulatedTable(db, "fic.irl.1", 2026);
            Assert.AreSame(first, again); Assert.AreEqual(10, first.Count); Assert.IsTrue(first.All(r => r.played == 18));
            Assert.AreEqual(first.Sum(r => r.goalsFor), first.Sum(r => r.goalsAgainst));
            for (int i = 1; i < first.Count; i++) Assert.GreaterOrEqual(first[i - 1].Points, first[i].Points);
            Assert.AreNotEqual(string.Join(",", first.Select(r => r.club + r.Points)), string.Join(",", GeneratedWorld.SimulatedTable(db, "fic.irl.1", 2027).Select(r => r.club + r.Points)));
        }

        // --- Report cards --------------------------------------------------------
        [Test] public void ReportDoesNotLearnHiddenDevelopmentWithoutAnotherObservation()
        {
            c.Scout(db,"p30");Days(20);var before=c.ScoutReportCard(db,"p30");var attribute=c.AssessedAttribute(db,"p30","shortPassing");
            var p=db.Find("p30");p.rating+=20;p.potential+=15;p.development+=10;p.attributes.First(a=>a.key=="shortPassing").value+=20;
            var after=c.ScoutReportCard(db,"p30");var later=c.AssessedAttribute(db,"p30","shortPassing");
            Assert.AreEqual(before.ability.low,after.ability.low);Assert.AreEqual(before.ability.high,after.ability.high);
            Assert.AreEqual(before.potential.low,after.potential.low);Assert.AreEqual(before.potential.high,after.potential.high);
            Assert.AreEqual(attribute.low,later.low);Assert.AreEqual(attribute.high,later.high);
            var saved=JsonUtility.FromJson<Career>(JsonUtility.ToJson(c));Assert.AreEqual(c.ReportFor("p30").observedAttributes[0].value,saved.ReportFor("p30").observedAttributes[0].value);
            c.revealAttributes=true;Assert.Greater(c.ScoutReportCard(db,"p30").ability.low,after.ability.low);
        }
        [Test] public void LegacyReportsKeepTheirEstimateButRequireObservationForAttributes()
        {
            c.Scout(db,"p30");Days(20);var r=c.ReportFor("p30");float estimate=r.estimate;r.observedAttributes=null;r.attributesObserved=false;
            var saved=JsonUtility.FromJson<Career>(JsonUtility.ToJson(c));
            Assert.AreEqual(estimate,saved.ReportFor("p30").estimate);
            Assert.IsFalse(saved.AssessedAttribute(db,"p30","shortPassing").known);
            Assert.IsTrue(saved.RecruitmentReportNeedsRefresh(saved.ReportFor("p30")));
            c.Scout(db,"p30");Assert.IsNotNull(c.ReportFor("p30").observedAttributes);
        }
        [Test] public void ObservationCompletionUpdatesSnapshotThenFreezesIt()
        {
            c.Scout(db,"p30");var p=db.Find("p30");p.attributes.First(a=>a.key=="shortPassing").value=90;
            Days(20);Assert.AreEqual(p.Attribute("shortPassing"),c.ReportFor("p30").observedAttributes.First(a=>a.key=="shortPassing").value);
            Age(c.ReportFor("p30"),300);p.attributes.First(a=>a.key=="shortPassing").value=40;c.Scout(db,"p30");Days(20);
            Assert.AreEqual(p.Attribute("shortPassing"),c.ReportFor("p30").observedAttributes.First(a=>a.key=="shortPassing").value);
        }
        [Test] public void ReportRangesContainTruthAndNarrowWithObservationTime()
        {
            var p = db.Find("p30"); p.rating = 71; p.potential = 76;
            Assert.IsFalse(c.ScoutReportCard(db, "p30").ability.known);
            c.Scout(db, "p30"); Days(8); var early = c.ScoutReportCard(db, "p30");
            Days(12); var done = c.ScoutReportCard(db, "p30");
            Assert.IsTrue(early.ability.known && done.ability.known); Assert.AreEqual(90, done.knowledge);
            Assert.Less(Width(done.ability), Width(early.ability));
            Assert.That(71, Is.InRange(done.ability.low, done.ability.high)); Assert.That(76, Is.InRange(done.potential.low, done.potential.high));
            Assert.IsNotNull(done.fit); Assert.IsNotNull(done.grade);
        }
        [Test] public void ScoutJudgingFamiliarityAndRepeatedObservationChangeWidth()
        {
            c.Staff("scout").judging = 6; c.Scout(db, "p30"); Days(20); var weak = c.ScoutReportCard(db, "p30");
            c.Staff("scout").judging = 19; c.Scout(db, "p31"); Days(20); var strong = c.ScoutReportCard(db, "p31");
            Assert.Less(Width(strong.ability), Width(weak.ability));
            // p100 plays in Spain: an unknown territory for a French club.
            c.Scout(db, "p100"); Days(20); var abroad = c.ScoutReportCard(db, "p100");
            Assert.Less(abroad.familiarity, .6f); Assert.Greater(Width(abroad.ability), Width(strong.ability));
            // Completed reports in the territory improve familiarity; a renewed report narrows.
            foreach (var id in new[]{"p101", "p102"}){c.Scout(db, id); Days(20);}
            Assert.Greater(c.ScoutingFamiliarity(db, db.Find("p100")), abroad.familiarity);
            Age(c.ReportFor("p31"), 300); Assert.IsTrue(c.ScoutReportCard(db, "p31").stale);
            c.Scout(db, "p31"); Days(20); var renewed = c.ScoutReportCard(db, "p31");
            Assert.AreEqual(1, renewed.depth); Assert.LessOrEqual(Width(renewed.ability), Width(strong.ability));
        }
        [Test] public void StaleReportWidensAgainAndIsFlagged()
        {
            c.Scout(db, "p30"); Days(20); var fresh = c.ScoutReportCard(db, "p30");
            c.life.day += 400; var old = c.ScoutReportCard(db, "p30");
            Assert.IsTrue(old.stale); Assert.Greater(old.reportAge, 390); Assert.Greater(Width(old.ability), Width(fresh.ability));
        }
        [Test] public void GradeIsRelativeToSquadAndNeedsAndCardDoesNotTouchRandomStream()
        {
            c.revealAttributes = true;
            db.Find("p30").rating = 80; db.Find("p31").rating = 50;
            uint seed = c.life.seed; int messages = c.life.messages.Count;
            var good = c.ScoutReportCard(db, "p30"); var poor = c.ScoutReportCard(db, "p31");
            Assert.AreEqual("A", good.grade); Assert.AreEqual("E", poor.grade);
            Assert.AreEqual(seed, c.life.seed); Assert.AreEqual(messages, c.life.messages.Count);
            Assert.AreEqual(good.grade, c.ScoutReportCard(db, "p30").grade);
            Assert.IsTrue(good.strengths.Contains("tacle debout")); Assert.IsTrue(good.weaknesses.Contains("vitesse"));
        }
        [Test] public void DepthAlertsAndRivalBidsSurviveUnitySaveRoundTrip()
        {
            c.Scout(db, "p30"); Days(20); Age(c.ReportFor("p30"), 300); c.Scout(db, "p30"); c.recruitmentAlerts.Add("interest:p30:2026"); c.rivalBids.Add(new RivalBid{player = "p30", club = "c5", seller = "c1", decision = 99, fee = 5});
            var restored = JsonUtility.FromJson<Career>(JsonUtility.ToJson(c));
            Assert.AreEqual(1, restored.world.reports.Single(r => r.player == "p30").depth);
            CollectionAssert.AreEqual(c.recruitmentAlerts, restored.recruitmentAlerts); Assert.AreEqual("c5", restored.rivalBids.Single().club);
        }

        // --- Rivals and alerts ---------------------------------------------------
        void OpenWindow(){while (!c.WindowOpen) c.life.day++;}
        [Test] public void RivalBidsCompeteForFollowedTargetsAndCanSignThem()
        {
            OpenWindow(); var target = db.Find("p40"); Assert.AreEqual("c1", target.team);
            Assert.IsNotNull(c.RecruitmentRival(db, target, out _)); c.ToggleShortlist("p40");
            for (int i = 0; i < 120 && !c.rivalBids.Any(); i++) Days(1);
            var bid = c.rivalBids.Single(); Assert.AreEqual("open", bid.status); Assert.AreNotEqual("c0", bid.club); Assert.AreNotEqual("c1", bid.club);
            Assert.IsTrue(c.life.messages.Any(m => m.subject == "Offre concurrente"));
            Assert.IsTrue(c.life.messages.Any(m => m.subject == "Concurrence sur une cible"));
            while (c.life.day < bid.decision) Days(1);
            if (bid.status == "signed"){Assert.AreEqual(bid.club, target.team); Assert.IsTrue(c.world.aiTransfers.Any(t => t.player == "p40" && t.buyer == bid.club));}
            else Assert.AreEqual("c1", target.team);
            Assert.AreNotEqual("open", bid.status);
        }
        [Test] public void OurSignedAgreementStopsARivalBid()
        {
            OpenWindow(); c.rivalBids.Add(new RivalBid{player = "p40", club = "c5", seller = "c1", day = c.life.day, decision = c.life.day + 1, fee = 100000, wage = 550});
            c.world.offers.Add(new TransferOffer{player = "p40", seller = "c1", destination = "c0", status = "accepted", due = c.life.day + 5, fee = 100000, wage = 600, years = 2, role = "rotation"});
            Days(2); Assert.AreEqual("lapsed", c.rivalBids.Single().status); Assert.AreEqual("c1", db.Find("p40").team);
        }
        [TestCase("c1")]
        [TestCase("c5")]
        public void MovingToABiddingClubDoesNotAuthorizeAnAutomaticTransfer(string newClub)
        {
            OpenWindow();
            var bid = new RivalBid{player="p40",club="c5",seller="c1",decision=c.life.day,fee=100000,wage=550};
            c.rivalBids.Add(bid); c.club=newClub;
            typeof(Career).GetMethod("ResolveRivalBid",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(c,new object[]{db,bid});
            Assert.AreEqual("lapsed",bid.status);
            Assert.AreEqual("c1",db.Find("p40").team);
            Assert.IsFalse(c.world.aiTransfers.Any(t=>t.player=="p40"));
        }
        [Test] public void ExpiringContractAlertIsSentOnceForShortlistedPlayer()
        {
            var contract = c.Contract(db, "p40"); contract.until = c.life.day + 100; c.ToggleShortlist("p40");
            Days(3); Assert.AreEqual(1, c.life.messages.Count(m => m.subject == "Fin de contrat en vue"));
        }
        [Test] public void AiRivalsJudgeTargetsWithoutReadingExactHiddenLevel()
        {
            var team = db.clubs.First(x => x.id == "c5"); var p = db.Find("p40");
            float seen = c.AiPerceivedLevel(team, p); Assert.AreEqual(seen, c.AiPerceivedLevel(team, p));
            Assert.AreNotEqual(p.rating + p.development, seen, "L’IA doit estimer avec une erreur stable");
            Assert.LessOrEqual(Math.Abs(seen - p.rating), 6.001f);
        }
    }
}

using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Touchline.Core;
using UnityEngine;

namespace Touchline.Tests
{
    public class OfferClubContextTests
    {
        Database db; Career c;
        [SetUp] public void Setup()
        {
            db = new Database { leagues = new[] { new LeagueData { id = "test", name = "Test" } },
                clubs = Enumerable.Range(0, 4).Select(i => new ClubData { id = "c" + i, name = "Club " + i, league = "test", playable = true, annualRevenue = 20000000 }).ToArray(),
                players = Enumerable.Range(0, 96).Select(i => new PlayerData { id = "p" + i, name = "Joueur " + i, team = "c" + i / 24,
                    position = i % 24 < 2 ? "GK" : "CB", positions = new[] { i % 24 < 2 ? "GK" : "CB" },
                    age = 24, rating = 65, potential = 90, value = 100000, wage = 500 }).ToArray() };
            c = new Career { club = "c0" }; c.lineup = Career.Select(db, c.club, c.tactic); c.EnsureWorld(db);
        }
        void Move()
        {
            foreach (var p in db.Squad("c1")) p.rating = 90;
            c.approaches.Add(new JobApproach { club = "c1", until = c.life.day + 14 }); c.AnswerApproach(db, "c1", true);
        }
        void Respond()
        {
            c.life.day += 2; typeof(Career).GetMethod("ManagementDay", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(c, new object[] { db });
        }
        TransferOffer Offer(string role = "rotation")
        {
            db.Find("p48").rating = 70; c.ProposeTransfer(db, "p48", 120000, 600, 3, role); return c.world.offers.Last();
        }
        [Test] public void PreviousClubOfferKeepsItsOriginalSportingContextAfterManagerMove()
        {
            var offer = Offer(); Move(); Respond();
            Assert.AreEqual("c0", offer.destination); Assert.AreEqual("counter", offer.status);
            Assert.IsFalse(c.life.messages.Any(m => m.player == "p48" && m.action == "transfer"));
        }
        [TestCase(null)] [TestCase("")] public void LegacyUnsignedOfferGetsDestinationBeforeManagerLeaves(string destination)
        {
            var offer = Offer(); offer.destination = destination; Move();
            Assert.AreEqual("c0", offer.destination); Respond(); Assert.AreEqual("counter", offer.status);
        }
        [Test] public void PreviousClubPendingOfferDoesNotBlockNewClubNegotiation()
        {
            var old = Offer(); Move(); c.ProposeTransfer(db, "p48", 120000, 600, 3, "starter");
            Assert.AreEqual("pending", old.status); Assert.AreEqual("c1", c.world.offers.Last().destination);
        }
        [Test] public void PreviousClubAttemptsDoNotConsumeNewClubNegotiationAttempts()
        {
            var old = Offer(); old.status = "counter"; old.attempts = 3; Move();
            c.ProposeTransfer(db, "p48", 120000, 600, 3, "starter"); Assert.AreEqual(1, c.world.offers.Last().attempts);
        }
        [Test] public void WithdrawalAndTermsAreScopedToManagedClub()
        {
            var own = Offer(); var foreign = new TransferOffer { player = "p48", destination = "c1", seller = "c2", status = "pending", terms = new MarketTerms() };
            c.world.offers.Add(foreign); var terms = new MarketTerms { goalBonus = 100 };
            c.SetOfferTerms("p48", terms); Assert.AreEqual(100, own.terms.goalBonus); Assert.AreEqual(0, foreign.terms.goalBonus);
            c.RejectOffer("p48"); Assert.AreEqual("withdrawn", own.status); Assert.AreEqual("pending", foreign.status);
        }
        [Test] public void ForeignOnlyOfferCannotBeChangedOrWithdrawn()
        {
            var old = Offer(); Move(); string before = JsonUtility.ToJson(old);
            Assert.Throws<InvalidOperationException>(() => c.SetOfferTerms("p48", new MarketTerms()));
            Assert.Throws<InvalidOperationException>(() => c.RejectOffer("p48")); Assert.AreEqual(before, JsonUtility.ToJson(old));
        }
        [TestCase(60,20)] [TestCase(80,-20)] public void RoleExpectationsUseEffectiveAbility(float rating, float development)
        {
            var p = db.Find("p48"); p.age = 21; p.rating = rating; p.development = development;
            string rotation = c.PlayingTimeOfferIssue(db, p.id, "rotation"), youth = c.PlayingTimeOfferIssue(db, p.id, "youth");
            p.rating += p.development; p.development = 0;
            Assert.AreEqual(c.PlayingTimeOfferIssue(db, p.id, "rotation"), rotation);
            Assert.AreEqual(c.PlayingTimeOfferIssue(db, p.id, "youth"), youth);
        }
        [TestCase(60,20,"counter")] [TestCase(80,-20,"accepted")] public void SportingAttractivenessUsesEffectiveAbility(float rating, float development, string expected)
        {
            var p = db.Find("p48"); p.rating = rating; p.development = development;
            c.ProposeTransfer(db, p.id, 120000, 600, 3, "starter"); Respond(); Assert.AreEqual(expected, c.world.offers.Last(o => o.player == p.id).status);
        }
        [Test] public void SignedPrecontractStillArrivesAtOriginalClubAfterManagerMove()
        {
            var player = db.Find("p48"); c.Contract(db, player.id).until = c.life.day + 9;
            var offer = new TransferOffer { player = player.id, destination = c.club, seller = player.team, status = "scheduled", precontract = true, joinDay = c.life.day + 10, wage = 600, years = 3, role = "starter" };
            c.world.offers.Add(offer); Move(); Assert.AreEqual("scheduled", offer.status);
            c.life.day = offer.joinDay; typeof(Career).GetMethod("ActivatePrecontracts", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(c, new object[] { db });
            Assert.AreEqual("c0", player.team); Assert.AreEqual("signed", offer.status); Assert.AreEqual("c1", c.club);
        }
    }
}

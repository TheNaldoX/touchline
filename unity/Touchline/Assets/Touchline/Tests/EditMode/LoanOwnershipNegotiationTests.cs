using System;
using System.Linq;
using NUnit.Framework;
using Touchline.Core;
using UnityEngine;

namespace Touchline.Tests
{
    public class LoanOwnershipNegotiationTests
    {
        Database db; Career career;
        [SetUp] public void Setup()
        {
            db = new Database { leagues = new[] { new LeagueData { id = "test", name = "Test" } },
                clubs = Enumerable.Range(0, 4).Select(i => new ClubData { id = "c" + i, name = "Club " + i, league = "test", playable = true, annualRevenue = 20000000 }).ToArray(),
                players = Enumerable.Range(0, 96).Select(i => new PlayerData { id = "p" + i, name = "Joueur " + i, team = "c" + i / 24,
                    position = i % 24 < 2 ? "GK" : "CB", positions = new[] { i % 24 < 2 ? "GK" : "CB" },
                    age = 24, rating = 65, potential = 80, value = 100000, wage = 500 }).ToArray() };
            career = new Career { club = "c0" }; career.lineup = Career.Select(db, career.club, career.tactic); career.EnsureWorld(db);
        }
        void Borrow(long option = 0)
        {
            career.ProposeTransfer(db, "p24", 20000, 500, 3, "key", loan: true,
                terms: new MarketTerms { loanEndDay = career.life.day + 90, optionFee = option });
            career.world.offers.Last().status = "accepted"; career.SignTransfer(db, "p24");
            Assert.AreEqual("c1", career.Contract(db, "p24").parent);
        }
        void LegacyAgreement(bool renewal = true)
        {
            career.world.offers.Add(new TransferOffer { player = "p24", seller = career.club, destination = career.club,
                status = "accepted", renewal = renewal, wage = 600, years = 3, role = "key", due = career.life.day });
        }
        [Test] public void BorrowedPlayerCannotBeRenewedAsOwnedPlayer()
        {
            Borrow(); string before = JsonUtility.ToJson(career);
            Assert.Throws<InvalidOperationException>(() => career.ProposeTransfer(db, "p24", 0, 600, 3, "key"));
            Assert.AreEqual(before, JsonUtility.ToJson(career));
        }
        [TestCase(true)] [TestCase(false)] public void LegacyAcceptedAgreementCannotEraseLoanOwnership(bool renewal)
        {
            Borrow(); LegacyAgreement(renewal); string before = JsonUtility.ToJson(career); string player = JsonUtility.ToJson(db.Find("p24"));
            Assert.Throws<InvalidOperationException>(() => career.SignTransfer(db, "p24"));
            Assert.AreEqual(before, JsonUtility.ToJson(career)); Assert.AreEqual(player, JsonUtility.ToJson(db.Find("p24")));
        }
        [Test] public void RestoredLegacyAgreementCannotEraseLoanOwnership()
        {
            Borrow(); LegacyAgreement(); career = JsonUtility.FromJson<Career>(JsonUtility.ToJson(career)); career.RestoreWorld(db); career.EnsureWorld(db);
            string before = JsonUtility.ToJson(career);
            Assert.Throws<InvalidOperationException>(() => career.SignTransfer(db, "p24"));
            Assert.AreEqual(before, JsonUtility.ToJson(career)); Assert.AreEqual("c1", career.Contract(db, "p24").parent);
        }
        [Test] public void LegitimatePurchaseOptionStillPaysOwnerAndAllowsLaterRenewal()
        {
            Borrow(100000); var owner = career.world.aiAccounts.First(a => a.club == "c1"); long ownerCash = owner.cash;
            long cash = career.life.cash; long spent = career.world.transferSpent;
            career.ExerciseLoanOption(db, "p24");
            Assert.AreEqual(ownerCash + 100000, owner.cash); Assert.AreEqual(cash - 100000, career.life.cash);
            Assert.AreEqual(spent + 100000, career.world.transferSpent); Assert.IsNull(career.Contract(db, "p24").parent);
            career.ProposeTransfer(db, "p24", 0, 600, 3, "key"); career.world.offers.Last().status = "accepted"; career.SignTransfer(db, "p24");
            Assert.AreEqual(600, db.Find("p24").wage); Assert.IsNull(career.Contract(db, "p24").parent);
        }
        [TestCase(null)] [TestCase("")] public void OwnedPlayerCanRenewWithAbsentParent(string parent)
        {
            career.Contract(db, "p0").parent = parent;
            career.ProposeTransfer(db, "p0", 0, 600, 3, "key"); career.world.offers.Last().status = "accepted"; career.SignTransfer(db, "p0");
            Assert.AreEqual(600, db.Find("p0").wage); Assert.IsNull(career.Contract(db, "p0").parent);
        }
        [Test] public void ElapsedLoanKeepsOwnershipUntilReturnIsProcessed()
        {
            Borrow(); career.Contract(db, "p24").loanUntil = career.life.day - 1;
            Assert.Throws<InvalidOperationException>(() => career.ProposeTransfer(db, "p24", 0, 600, 3, "key"));
            Assert.AreEqual("c1", career.Contract(db, "p24").parent);
        }
    }
}

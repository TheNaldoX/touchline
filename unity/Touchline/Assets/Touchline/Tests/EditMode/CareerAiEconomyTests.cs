using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Touchline.Core;

namespace Touchline.Tests
{
    public partial class ProfessionalTests
    {
        void AiSummer(){typeof(Career).GetMethod("AiSummerEconomy",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(c,new object[]{db});}
        [Test] public void AiSummerTransfersConserveFeesAndNeverSellUserPlayers()
        {
            foreach(var p in db.Squad("c1").Where(p=>!p.Goalkeeper)){p.rating=80;p.value=100000;}
            var owned=db.Squad(c.club).Select(p=>p.id).ToArray();c.world.year++;c.life.day=365;AiSummer();
            Assert.That(c.world.aiTransfers.Count,Is.GreaterThan(0));
            foreach(var transfer in c.world.aiTransfers){Assert.AreNotEqual(c.club,transfer.seller);Assert.AreNotEqual(c.club,transfer.buyer);Assert.That(db.Squad(transfer.seller).Count,Is.GreaterThanOrEqualTo(22));}
            long fees=c.world.aiTransfers.Sum(t=>t.fee);
            Assert.AreEqual(fees,c.world.aiAccounts.Sum(a=>a.ledger.Where(e=>e.label.StartsWith("Vente IA")).Sum(e=>e.amount)));
            Assert.AreEqual(-fees,c.world.aiAccounts.Sum(a=>a.ledger.Where(e=>e.label.StartsWith("Recrutement IA")).Sum(e=>e.amount)));
            CollectionAssert.AreEquivalent(owned,db.Squad(c.club).Select(p=>p.id));
        }
        [Test] public void AnnualAiSettlementIsIdempotent()
        {
            c.world.year++;c.life.day=365;AiSummer();long cash=c.world.aiAccounts.Sum(a=>a.cash);int transfers=c.world.aiTransfers.Count;long own=c.life.cash;
            AiSummer();Assert.AreEqual(cash,c.world.aiAccounts.Sum(a=>a.cash));Assert.AreEqual(transfers,c.world.aiTransfers.Count);Assert.AreEqual(own,c.life.cash);
        }
        [Test] public void OwnerDoesNotDistributeCommittedMoneyOrFunding()
        {
            var account=c.world.aiAccounts.Single(a=>a.club==c.club);c.life.cash=30000000;account.openingCash=1000000;account.externalFunding=29000000;
            c.world.year++;c.life.day=365;AiSummer();Assert.AreEqual(0,account.dividends);Assert.AreEqual(30000000,c.life.cash);
            c.world.year++;c.life.cash+=30000000;c.payments.Add(new PaymentDue{club=c.club,amount=60000000,due=900});AiSummer();Assert.AreEqual(0,account.dividends);
        }
        [Test] public void OwnerDistributionIsRecordedWithoutRepayingDailyReceipts()
        {
            var account=c.world.aiAccounts.Single(a=>a.club==c.club);long before=40000000;c.life.cash=before;account.openingCash=20000000;
            c.world.year++;c.life.day=365;AiSummer();Assert.That(account.dividends,Is.GreaterThan(0));
            Assert.AreEqual(before-account.dividends,c.life.cash);Assert.AreEqual(c.life.cash,account.cash);
            Assert.That(c.life.messages.Any(m=>m.subject=="Affectation annuelle du résultat"));Assert.That(c.life.cash,Is.GreaterThanOrEqualTo(c.life.revenue/2));
        }
        [Test] public void AiInfrastructureCostsMoneyAndUserInfrastructureStaysUnderUserControl()
        {
            c.world.aiAccounts.Single(a=>a.club=="c1").cash=30000000;int own=c.Level("academy");c.world.year++;c.life.day=365;AiSummer();
            var account=c.world.aiAccounts.Single(a=>a.club=="c1");Assert.That(account.investments,Is.GreaterThan(0));Assert.AreEqual(2,account.academy);Assert.AreEqual(own,c.Level("academy"));
        }
        [Test] public void ChangingClubKeepsItsAccumulatedAiTreasury()
        {
            var account=c.world.aiAccounts.Single(a=>a.club=="c1");account.cash=9876543;
            c.approaches.Add(new JobApproach{club="c1",until=14});c.AnswerApproach(db,"c1",true);
            Assert.AreEqual(9876543,c.life.cash);Assert.AreEqual(c.life.day,account.projectedFromDay);
        }
        [Test] public void AiProjectionOnlyChargesDaysNotAlreadySettled()
        {
            var account=c.world.aiAccounts.Single(a=>a.club=="c1");var team=db.clubs.Single(t=>t.id=="c1");
            var method=typeof(Career).GetMethod("ProjectAiOperatingPeriod",BindingFlags.NonPublic|BindingFlags.Instance);
            method.Invoke(c,new object[]{team,account,db.Squad(team.id),180});long cash=account.cash;
            method.Invoke(c,new object[]{team,account,db.Squad(team.id),180});Assert.AreEqual(cash,account.cash);Assert.AreEqual(180,account.projectedFromDay);
        }
        [Test] public void AiOperatingDeficitIsDebtAndCannotFundDividends()
        {
            var account=c.world.aiAccounts.Single(a=>a.club=="c1");account.cash=0;foreach(var p in db.Squad("c1"))p.wage=100000;
            // These wages are the starting conditions of the simulated period,
            // not a new roster retroactively applied at the following summer.
            typeof(Career).GetMethod("CaptureAiOperatingSnapshot",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(c,new object[]{db.clubs.Single(t=>t.id=="c1"),account,db.Squad("c1").Sum(p=>p.wage)});
            c.world.year++;c.life.day=365;AiSummer();Assert.AreEqual(0,account.cash);Assert.That(account.operatingDebt,Is.GreaterThan(0));Assert.AreEqual(0,account.dividends);
            Assert.AreEqual(account.operatingDebt,account.ledger.Where(e=>e.label.StartsWith("Dette d'exploitation")).Sum(e=>e.amount));
        }
    }
}

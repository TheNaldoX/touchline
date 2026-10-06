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
        void Annual(){c.world.year++;c.life.day+=365;typeof(Career).GetMethod("SeasonPlayers",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(c,new object[]{db});}
        [Test] public void TwoGenerationsRetainClubLevelAndSalaryScale()
        {
            long start=db.Squad("c1").Sum(p=>p.wage);for(int i=0;i<30;i++)Annual();var players=db.Squad("c1");
            Assert.That(players.Count,Is.InRange(22,32));Assert.That(players.Average(p=>p.rating),Is.InRange(59,71));
            Assert.That(players.Sum(p=>p.wage)/(double)start,Is.InRange(.55,1.8));Assert.That(players.All(p=>p.age<40));
            Assert.That(players.Count(p=>p.Goalkeeper),Is.GreaterThanOrEqualTo(2));Assert.That(db.players.Select(p=>p.id).Distinct().Count(),Is.EqualTo(db.players.Length));
        }
        [Test] public void YouthLeavesAcademyInsteadOfRemainingThereForDecades()
        {
            var player=db.Find(c.world.youth[0].player);player.age=21;Annual();
            Assert.AreEqual("free",player.team);Assert.AreEqual("released",c.world.youth.First(y=>y.player==player.id).group);
            Assert.That(c.life.messages.Any(m=>m.player==player.id&&m.subject=="Fin du parcours au centre"));
        }
        [Test] public void EmptySquadDoesNotResetDateCashOrCareer()
        {
            c.life.day=122;c.life.cash=123456;c.life.players.Clear();foreach(var p in db.Squad(c.club))p.team="free";
            var life=c.life;c.EnsureLife(db);Assert.AreSame(life,c.life);Assert.AreEqual(122,c.life.day);Assert.AreEqual(123456,c.life.cash);
        }
        [Test] public void PromotionRelegationCyclesCannotMultiplyRevenue()
        {
            var method=typeof(Career).GetMethod("ChangeDivisionRevenue",BindingFlags.Instance|BindingFlags.NonPublic);long initial=c.life.revenue;
            for(int i=0;i<15;i++){method.Invoke(c,new object[]{db,c.club,true});method.Invoke(c,new object[]{db,c.club,false});}
            Assert.AreEqual(initial,c.life.revenue);Assert.AreEqual(initial,c.changedRevenues.Single().revenue);
        }
        [Test] public void AnnualIncomeExcludesOnlyReferenceGateBeforeAnyNegotiatedDeals()
        {
            long recurring=c.RecurringAnnualIncome(db);Assert.AreEqual(c.life.revenue,recurring+c.life.financeBaselineGate);
            c.world.ticket=200;Assert.AreEqual(recurring,c.RecurringAnnualIncome(db),"Changing ticket prices must affect real gates, not cancel itself through the baseline.");
        }
        [Test] public void NegotiatedSponsorReplacesImplicitCommercialIncome()
        {
            long original=c.RecurringAnnualIncome(db);c.NegotiateSponsor(0,100000,2);c.SignSponsor(0);
            Assert.AreEqual(original-c.life.revenue*5/100,c.RecurringAnnualIncome(db));
            c.world.sponsors[0].status="expired";Assert.AreEqual(original-c.life.revenue*5/100,c.RecurringAnnualIncome(db),"Expired income must not return implicitly.");
        }
        [Test] public void LongTermReferencesSurviveSaveAndContinue()
        {
            Annual();c.RecurringAnnualIncome(db);var restored=JsonUtility.FromJson<Career>(JsonUtility.ToJson(c));
            Assert.AreEqual(c.world.developmentReferences.Count,restored.world.developmentReferences.Count);
            Assert.AreEqual(c.life.financeBaselineGate,restored.life.financeBaselineGate);
            Assert.AreEqual(c.world.developmentReferences[1].wage,restored.world.developmentReferences[1].wage);
        }
        [Test] public void AiDevelopmentNeverExceedsPotentialOrChangesManagedTrainingTwice()
        {
            var ai=db.Find("p24");ai.age=19;ai.rating=79.9f;ai.potential=80;
            var managed=db.Find("p0");managed.age=19;float own=managed.rating;Annual();
            Assert.AreEqual(80,ai.rating);Assert.AreEqual(own,managed.rating);
        }
        [Test] public void RetiringPlayerContractCannotRemainOnActivePayroll()
        {
            var player=db.Find("p25");player.age=36;var contract=c.Contract(db,player.id);contract.parent=c.club;
            Annual();Assert.AreEqual("retired",player.team);Assert.AreEqual("retired",contract.club);Assert.IsNull(contract.parent);
        }
        [Test] public void HighLevelVeteranCanContinueBeyondTheOrdinaryRetirementThreshold()
        {
            var player=db.Find("p25");player.age=36;player.rating=85;player.potential=85;Annual();
            Assert.AreEqual("c1",player.team);Assert.AreEqual(37,player.age);Assert.AreEqual(40,Career.RetirementThreshold(player));
        }
        [Test] public void ExplicitRetirementAnnouncementRemainsBindingForAnEliteVeteran()
        {
            var player=db.Find("p25");player.age=36;player.rating=85;player.potential=85;var contract=c.Contract(db,player.id);contract.retirement=c.life.day+300;
            Annual();Assert.AreEqual("retired",player.team);Assert.AreEqual("retired",contract.club);
        }
        [Test] public void AiRenewalRespectsActiveLoanSalaryAndOwnership()
        {
            var player=db.Find("p25");player.age=20;var contract=c.Contract(db,player.id);contract.parent=c.club;contract.wage=1234;player.wage=1234;contract.until=c.life.day+20;
            Annual();Assert.AreEqual(1234,player.wage);Assert.AreEqual(c.club,contract.parent);
        }
        [Test] public void InitialAiContractsHaveStableEstimatedExpiriesBeforeScouting()
        {
            var contract=c.world.contracts.Single(x=>x.player=="p25");int expiry=contract.until;
            c.world.year+=3;Assert.AreEqual(expiry,c.Contract(db,"p25").until);Assert.IsTrue(contract.estimated);
        }
        [Test] public void UnsustainableRenewalIsDeclinedWithoutCuttingSignedWage()
        {
            var p=db.Find("p25");var contract=c.Contract(db,p.id);p.wage=500000;contract.wage=p.wage;contract.until=c.life.day+370;
            Annual();Assert.IsTrue(contract.aiRelease);Assert.AreEqual(500000,p.wage);Assert.AreEqual("c1",p.team);
            c.life.day=contract.until;typeof(Career).GetMethod("ProcessAiEmployment",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(c,new object[]{db});
            Assert.AreEqual("free",p.team);Assert.That(db.Squad("c1").Count,Is.GreaterThanOrEqualTo(22));
        }
        [Test] public void AgreedAiWageChangeWaitsUntilOldContractExpires()
        {
            var p=db.Find("p25");var contract=c.Contract(db,p.id);long signed=p.wage;contract.until=370;
            p.rating=69;Annual();Assert.That(contract.nextWage,Is.GreaterThan(0));Assert.AreEqual(signed,p.wage);
            long agreed=contract.nextWage;c.life.day=371;typeof(Career).GetMethod("ProcessAiEmployment",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(c,new object[]{db});
            Assert.AreEqual(agreed,p.wage);Assert.AreEqual(0,contract.nextWage);
        }
    }
}

using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Touchline.Core;
using UnityEngine;

namespace Touchline.Tests
{
    public sealed class PermanentDepartureGrowthTests
    {
        Career c;Database db;
        static object Call(Career career,string method,params object[] args)=>typeof(Career).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(career,args);
        [SetUp] public void Setup()
        {
            db=new Database{leagues=new[]{new LeagueData{id="fra.1",name="L1"}},clubs=Enumerable.Range(0,4).Select(i=>new ClubData{id="c"+i,name="Club "+i,league="fra.1",playable=true,annualRevenue=20000000+i*1000000}).ToArray(),players=Enumerable.Range(0,96).Select(i=>new PlayerData{id="p"+i,name="Test "+i,team="c"+(i/24),age=24,position=i%24==0?"GB":"MIL",positions=new[]{i%24==0?"GK":"CM"},rating=65,potential=80,value=100000,wage=500,fitness=100,morale=75,attributes=new[]{new AttributeValue{key="shortPassing",value=65}}}).ToArray()};
            c=new Career{club="c0"};c.lineup=Career.Select(db,c.club,c.tactic);c.EnsureWorld(db);c.Person("p10").growth=2.25f;c.ApplyLife(db);
        }
        void Sale(){c.ListForSale(db,"p10");c.AcceptSale(db,"p10");}
        void Expire(bool ai=false)
        {
            var e=c.Contract(db,"p10");e.until=1;e.aiRelease=ai;c.life.day=1;Call(c,ai?"ProcessAiEmployment":"ManagementDay",db);
        }
        void PrecontractDeparture()
        {
            c.world.offers.Add(new TransferOffer{player="p10",seller=c.club,destination="c1",status="scheduled",joinDay=1,wage=600,years=3,role="rotation"});c.life.day=1;Call(c,"ActivatePrecontracts",db);
        }
        void Settled(string destination)
        {
            Assert.AreEqual(destination,db.Find("p10").team);Assert.AreEqual(67.25f,db.Find("p10").rating);Assert.AreEqual(67.25f,db.Find("p10").attributes[0].value);Assert.AreEqual(0,db.Find("p10").development);Assert.IsFalse(c.life.players.Any(p=>p.id=="p10"));
        }
        [Test] public void SaleConsolidatesKnownTrainingBeforePlayerLeaves(){Sale();Settled("c1");}
        [Test] public void OrdinaryExpiryConsolidatesKnownTrainingBeforePlayerLeaves(){Expire();Settled("free");}
        [Test] public void AiReleaseConsolidatesKnownTrainingBeforeAnnualProcessing(){Expire(true);Settled("free");}
        [Test] public void ScheduledPrecontractDepartureConsolidatesKnownTraining(){PrecontractDeparture();Settled("c1");}
        [Test] public void ReSigningSoldPlayerDoesNotErasePreviouslyEarnedAbility()
        {
            Sale();c.ProposeTransfer(db,"p10",120000,600,3,"rotation");c.world.offers.Last().status="accepted";c.SignTransfer(db,"p10");c.ApplyLife(db);Assert.AreEqual("c0",db.Find("p10").team);Assert.AreEqual(67.25f,db.Find("p10").rating);Assert.AreEqual(0,c.Person("p10").growth);Assert.AreEqual(0,db.Find("p10").development);
        }
        [Test] public void ReSigningReleasedPlayerDoesNotErasePreviouslyEarnedAbility()
        {
            Expire();c.ProposeTransfer(db,"p10",0,600,3,"rotation");c.world.offers.Last().status="accepted";c.SignTransfer(db,"p10");c.ApplyLife(db);Assert.AreEqual(67.25f,db.Find("p10").rating);Assert.AreEqual(0,db.Find("p10").development);
        }
        [Test] public void SaleRejectsWithoutSettlingGrowthOrChangingMoney()
        {
            c.ListForSale(db,"p10");c.life.day=8;long cash=c.life.cash;Assert.Throws<InvalidOperationException>(()=>c.AcceptSale(db,"p10"));Assert.AreEqual(cash,c.life.cash);Assert.AreEqual(2.25f,c.Person("p10").growth);Assert.AreEqual(65,db.Find("p10").rating);
        }
        [Test] public void RenewalDoesNotSettleTrainingAsIfPlayerHadLeft()
        {
            c.ProposeTransfer(db,"p10",0,600,3,"rotation");c.world.offers.Last().status="accepted";c.SignTransfer(db,"p10");Assert.AreEqual(2.25f,c.Person("p10").growth);Assert.AreEqual(65,db.Find("p10").rating);
        }
        [Test] public void BuyingFormerClubsPlayerSettlesOnlyItsRecordedAccruedTraining()
        {
            var former=new ClubLife();former.players.Add(new PlayerLife{id="p24",growth=1.25f});c.previousClubs.Add(new ManagedClub{club="c1",life=former});c.ProposeTransfer(db,"p24",120000,600,3,"rotation");c.world.offers.Last().status="accepted";c.SignTransfer(db,"p24");c.ApplyLife(db);Assert.AreEqual(66.25f,db.Find("p24").rating);Assert.AreEqual(0,former.players.Single().growth);Assert.AreEqual(0,c.Person("p24").growth);
        }
        [Test] public void BuyingUnobservedPlayerDoesNotInventTraining()
        {
            c.ProposeTransfer(db,"p24",120000,600,3,"rotation");c.world.offers.Last().status="accepted";c.SignTransfer(db,"p24");Assert.AreEqual(65,db.Find("p24").rating);
        }
        [Test] public void ExpiryDuringAnnualUpdateCannotLoseOrDoubleSettleTraining()
        {
            c.Contract(db,"p10").until=1;c.Contract(db,"p10").aiRelease=true;c.life.day=1;db.Find("p10").age=27;Call(c,"AnnualPlayerDevelopment",db);Settled("free");Call(c,"AnnualPlayerDevelopment",db);Assert.AreEqual(67.25f,db.Find("p10").rating);
        }
        [Test] public void SaleSettlementRespectsPotentialAndAttributeBounds()
        {
            db.Find("p10").potential=66;db.Find("p10").attributes[0].value=98.75f;Sale();Assert.AreEqual(66,db.Find("p10").rating);Assert.AreEqual(99,db.Find("p10").attributes[0].value);
        }
        [Test] public void SoldPlayerRoundtripKeepsPermanentGain()
        {
            Sale();var restored=JsonUtility.FromJson<Career>(JsonUtility.ToJson(c));restored.RestoreWorld(db);Assert.AreEqual(67.25f,db.Find("p10").rating);Assert.AreEqual(0,db.Find("p10").development);Assert.AreEqual("c1",db.Find("p10").team);
        }
        [Test] public void GrowthSettlementDoesNotAlterNegotiatedTransferAmount()
        {
            c.ListForSale(db,"p10");long fee=c.world.offers.Last().fee;long cash=c.life.cash;c.AcceptSale(db,"p10");Assert.AreEqual(cash+fee,c.life.cash);Assert.AreEqual(fee,c.world.offers.Last().fee);
        }
    }
}

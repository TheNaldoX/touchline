using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Touchline.Core;
using UnityEngine;

namespace Touchline.Tests
{
    public sealed class LoanPlayerHistoryTests
    {
        Career c;Database db;
        static object Call(Career career,string method,params object[] args)=>typeof(Career).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(career,args);
        [SetUp] public void Setup()
        {
            db=new Database{leagues=new[]{new LeagueData{id="fra.1",name="L1"}},clubs=Enumerable.Range(0,4).Select(i=>new ClubData{id="c"+i,name="Club "+i,league="fra.1",playable=true,annualRevenue=20000000+i*1000000}).ToArray(),players=Enumerable.Range(0,96).Select(i=>new PlayerData{id="p"+i,name="Test "+i,team="c"+(i/24),age=24,position=i%24==0?"GB":"MIL",positions=new[]{i%24==0?"GK":"CM"},rating=65,potential=80,value=100000,wage=500,fitness=100,morale=75,attributes=new[]{new AttributeValue{key="shortPassing",value=65}}}).ToArray()};
            c=new Career{club="c0"};c.lineup=Career.Select(db,c.club,c.tactic);c.EnsureWorld(db);
            var known=c.Person("p10");known.appearances=41;known.trust=88;known.growth=2.25f;known.lastTalk=-1;known.promiseUntil=12;known.promiseStarts=40;
            var e=c.Contract(db,"p10");e.appearancesAtSigning=9;e.role="key";e.playingTime=new PlayingTimeUsage{club="c0"};e.playingTime.games.Add(new PlayingTimeGame{started=true,minutes=90});c.ApplyLife(db);
        }
        void Outgoing(MarketTerms terms=null)
        {
            c.ProposeOutgoingLoan(db,"p10","c1",0,terms??new MarketTerms{loanWagePercent=40,loanEndDay=28});
            c.life.day=2;Call(c,"OutgoingLoanDay",db);Assert.AreEqual("accepted",c.outgoingLoans.Last().status);c.SignOutgoingLoan(db,"p10");
        }
        void Return(int day=28){c.life.day=day;Call(c,"ResolveLoans",db);c.ApplyLife(db);}
        [Test] public void NegotiatedReturnKeepsObservedHistoryAndAccruedAbility()
        {
            Outgoing();Assert.IsFalse(c.life.players.Any(x=>x.id=="p10"));Assert.AreEqual(1,c.life.loanedPlayers.Count);
            var p=db.Find("p10");Assert.AreEqual(67.25f,p.rating);Assert.AreEqual(67.25f,p.attributes[0].value);Assert.AreEqual(0,p.development);
            p.fitness=82;p.morale=66;Return();var known=c.Person("p10");Assert.AreEqual(41,known.appearances);Assert.AreEqual(88,known.trust);Assert.AreEqual(0,known.growth);Assert.AreEqual(-1,known.lastTalk);Assert.AreEqual(82,known.fitness);Assert.AreEqual(66,known.morale);Assert.AreEqual(0,c.life.loanedPlayers.Count);
            Assert.AreEqual(9,c.Contract(db,p.id).appearancesAtSigning);Assert.AreEqual(1,c.Contract(db,p.id).playingTime.games.Count);Assert.AreEqual("c0",c.Contract(db,p.id).playingTime.club);
        }
        [Test] public void DevelopmentLoanUsesTheSameHistoryPath()
        {
            db.Find("p10").age=19;c.world.youth.Add(new YouthPath{player="p10",group="senior"});c.LoanYouth(db,"p10","c1");int end=c.Contract(db,"p10").loanUntil;Return(end);Assert.AreEqual(41,c.Person("p10").appearances);Assert.AreEqual(88,c.Person("p10").trust);Assert.AreEqual(67.25f,db.Find("p10").rating);
        }
        [Test] public void ArchivedHistoryIsDetachedFromOldReferences()
        {
            var old=c.Person("p10");Outgoing();old.appearances=900;old.trust=1;old.growth=5;Return();Assert.AreEqual(41,c.Person("p10").appearances);Assert.AreEqual(88,c.Person("p10").trust);Assert.AreEqual(67.25f,db.Find("p10").rating);
        }
        [Test] public void SaveReloadKeepsArchiveAndRestoresItOnlyOnce()
        {
            Outgoing();c=JsonUtility.FromJson<Career>(JsonUtility.ToJson(c));c.RestoreWorld(db);c.EnsureWorld(db);Assert.AreEqual(41,c.life.loanedPlayers.Single().player.appearances);Return();var snapshot=JsonUtility.ToJson(c.Person("p10"));Return();Assert.AreEqual(snapshot,JsonUtility.ToJson(c.Person("p10")));Assert.AreEqual(67.25f,db.Find("p10").rating);
        }
        [Test] public void LegacySaveDoesNotInventLostTrustAppearancesOrGrowth()
        {
            Outgoing();c.life.loanedPlayers=null;Return();Assert.AreEqual(0,c.Person("p10").appearances);Assert.AreEqual(60,c.Person("p10").trust);Assert.AreEqual(0,c.Person("p10").growth);Assert.AreEqual(9,c.Contract(db,"p10").appearancesAtSigning);
        }
        [Test] public void RecallPreservesHistoryAndPausesPromiseForExactAbsence()
        {
            Outgoing(new MarketTerms{loanEndDay=28,loanWagePercent=40,recall=true});c.life.day=10;c.RecallLoan(db,"p10");Assert.AreEqual(20,c.Person("p10").promiseUntil);Assert.AreEqual(40,c.Person("p10").promiseStarts);Assert.AreEqual(41,c.Person("p10").appearances);Assert.AreEqual(88,c.Person("p10").trust);
        }
        [Test] public void LoanEstimatesNeverSatisfyManagerPromise()
        {
            Outgoing();var path=c.world.youth.Single(y=>y.player=="p10");path.loanAppearances+=50;path.trackedLoanMinutes=4000;Return();Assert.AreEqual(41,c.Person("p10").appearances);Assert.AreEqual(38,c.Person("p10").promiseUntil);c.life.day=37;c.life.nextFixture=int.MaxValue;c.AdvanceDay(db);Assert.AreEqual(-1,c.Person("p10").promiseUntil);Assert.Less(c.Person("p10").trust,88);
        }
        [Test] public void OldMedicalInstructionsDoNotRestartAfterLoan()
        {
            var old=c.Person("p10");old.boostUntil=90;old.restUntil=90;old.rehabUntil=90;old.banUntil=40;Outgoing();Return();var known=c.Person("p10");Assert.AreEqual(-1,known.boostUntil);Assert.AreEqual(-1,known.restUntil);Assert.AreEqual(0,known.rehabUntil);Assert.AreEqual(40,known.banUntil);Assert.AreEqual(0,db.Find("p10").performanceModifier);
        }
        [Test] public void GrowthSettlementIsBoundedByPotentialAndAttributeCeiling()
        {
            db.Find("p10").potential=66;db.Find("p10").attributes[0].value=98.75f;Outgoing();Return();Assert.AreEqual(66,db.Find("p10").rating);Assert.AreEqual(99,db.Find("p10").attributes[0].value);Assert.AreEqual(0,c.Person("p10").growth);
        }
        [Test] public void NegativeGrowthDoesNotReduceAbilityOnLoan()
        {
            c.Person("p10").growth=-2;Outgoing();Return();Assert.AreEqual(65,db.Find("p10").rating);Assert.AreEqual(0,c.Person("p10").growth);
        }
        [Test] public void SeasonRolloverDoesNotSettleParentGrowthTwice()
        {
            Outgoing();db.Find("p10").age=27;Call(c,"AnnualPlayerDevelopment",db);Assert.AreEqual(67.25f,db.Find("p10").rating);Return();Call(c,"AnnualPlayerDevelopment",db);Assert.AreEqual(67.25f,db.Find("p10").rating);Assert.AreEqual(41,c.Person("p10").appearances);
        }
        [Test] public void ParentHistoryReturnsEvenWhileManagerWorksElsewhere()
        {
            Outgoing();var former=c.life;c.previousClubs.Add(new ManagedClub{club="c0",life=former});c.club="c2";c.life=null;c.EnsureLife(db);Return();Assert.AreEqual(41,former.players.Single(x=>x.id=="p10").appearances);Assert.AreEqual(0,former.loanedPlayers.Count);Assert.IsFalse(c.life.players.Any(x=>x.id=="p10"));c.club="c0";c.life=former;c.EnsureLife(db);Assert.AreEqual(88,c.Person("p10").trust);
        }
        [Test] public void OutgoingObligationClosesArchiveWithoutInventingBorrowerObservations()
        {
            Outgoing(new MarketTerms{loanEndDay=28,loanWagePercent=40,obligationFee=10000});Return();Assert.AreEqual("c1",db.Find("p10").team);Assert.AreEqual(0,c.life.loanedPlayers.Count);Assert.IsFalse(c.life.players.Any(x=>x.id=="p10"));Assert.IsNull(c.Contract(db,"p10").parent);Assert.AreEqual("c1",c.Contract(db,"p10").playingTime.club);Assert.AreEqual(0,c.Contract(db,"p10").playingTime.games.Count);
        }
        [Test] public void RetirementDuringLoanKeepsKnownParentHistory()
        {
            Outgoing();c.Contract(db,"p10").retirement=28;Return();Assert.AreEqual("retired",db.Find("p10").team);Assert.AreEqual(41,c.life.retiredPlayers.Single(x=>x.id=="p10").appearances);Assert.AreEqual(88,c.life.retiredPlayers.Single(x=>x.id=="p10").trust);Assert.AreEqual(0,c.life.loanedPlayers.Count);Assert.IsNull(c.Contract(db,"p10").parent);
        }
        [Test] public void IncomingLoanReturnConsolidatesObservedTrainingWithoutAttributingAppearancesToParent()
        {
            c.ProposeTransfer(db,"p24",0,500,3,"rotation",true,0,0,new MarketTerms{loanEndDay=28,loanWagePercent=40});c.world.offers.Last().status="accepted";c.SignTransfer(db,"p24");c.Person("p24").appearances=7;c.Person("p24").growth=1.5f;Return();Assert.AreEqual("c1",db.Find("p24").team);Assert.AreEqual(66.5f,db.Find("p24").rating);Assert.IsFalse(c.life.players.Any(x=>x.id=="p24"));Assert.AreEqual(0,c.life.loanedPlayers.Count);
        }
        [Test] public void IncomingOptionRetainsBorrowerPersonAndClosesKnownParentArchive()
        {
            var parent=new ClubLife();parent.players.Add(new PlayerLife{id="p24",appearances=30,trust=82,growth=1.25f});c.previousClubs.Add(new ManagedClub{club="c1",life=parent});c.ProposeTransfer(db,"p24",0,500,3,"rotation",true,0,0,new MarketTerms{loanEndDay=28,loanWagePercent=40,optionFee=10000});c.world.offers.Last().status="accepted";c.SignTransfer(db,"p24");Assert.AreEqual(1,parent.loanedPlayers.Count);Assert.AreEqual(66.25f,db.Find("p24").rating);c.Person("p24").appearances=3;c.Person("p24").trust=70;c.ExerciseLoanOption(db,"p24");Assert.AreEqual(0,parent.loanedPlayers.Count);Assert.AreEqual(3,c.Person("p24").appearances);Assert.AreEqual(70,c.Person("p24").trust);Assert.AreEqual("c0",c.Contract(db,"p24").playingTime.club);
        }
        [Test] public void UnrelatedPlayersAreUnchangedByLoanHistory()
        {
            string before=JsonUtility.ToJson(c.Person("p11"));float rating=db.Find("p11").rating;Outgoing();Return();Assert.AreEqual(before,JsonUtility.ToJson(c.Person("p11")));Assert.AreEqual(rating,db.Find("p11").rating);
        }
    }
}

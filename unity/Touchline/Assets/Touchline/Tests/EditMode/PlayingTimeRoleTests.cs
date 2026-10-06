using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Touchline.Core;
using UnityEngine;
namespace Touchline.Tests
{
    public sealed class PlayingTimeRoleTests
    {
        Database db;Career c;Employment contract;
        [SetUp] public void Setup(){c=StaffTrainingLifecycleTests.Fixture(out db);contract=c.Contract(db,"p1");c.life.day=60;contract.joined=0;contract.role="key";contract.playingTime=new PlayingTimeUsage();}
        void Invoke(string name,params object[] args)=>typeof(Career).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(c,args);
        void Record(bool start,float minutes)=>Invoke("RecordPlayingTime","p1",start,minutes);
        [TestCase("key",true,70,true)][TestCase("key",false,90,false)][TestCase("starter",true,45,true)][TestCase("rotation",false,20,true)][TestCase("impact_sub",false,20,true)][TestCase("impact_sub",false,5,false)][TestCase("youth",false,0,true)]
        public void NewRolesUseTheirActualSelectionRequirement(string role,bool starts,int minutes,bool respected){contract.role=role;for(int i=0;i<4;i++)Record(starts,minutes);Assert.AreEqual(!respected,c.PlayingTimeConcern("p1"));}
        [Test] public void EarlyReviewWaitsForFourObservedOpportunities(){Record(false,0);Record(false,0);Record(false,0);Assert.IsFalse(c.PlayingTimeConcern("p1"));Record(false,0);Assert.IsTrue(c.PlayingTimeConcern("p1"));}
        [Test] public void InjuriesSuspensionsAndRestDoNotInventOpportunity(){c.Person("p1").banUntil=61;Record(false,0);c.Person("p1").banUntil=0;c.Person("p1").restUntil=61;Record(false,0);Assert.AreEqual(0,contract.playingTime.games.Count);}
        [Test] public void AppearanceCountsEvenWhenPlayerBecameUnavailableDuringMatch(){c.Person("p1").banUntil=61;Record(true,40);Assert.AreEqual(1,contract.playingTime.games.Count);}
        [Test] public void StartsCannotBeBoughtWithLateSubstitutions(){for(int i=0;i<10;i++)Record(false,30);Assert.IsTrue(c.PlayingTimeConcern("p1"));Assert.That(c.PlayingTimeProgress("p1"),Does.Contain("0 titularisations").And.Contain("10 entrées"));}
        [Test] public void SuperSubGetsCreditForStartsAsWellAsEntrances(){contract.role="impact_sub";Record(true,90);Record(false,20);Record(false,0);Record(false,0);Assert.IsFalse(c.PlayingTimeConcern("p1"));}
        [Test] public void MeaningfulEntranceDoesNotRoundUpShortAppearance(){contract.role="impact_sub";for(int i=0;i<4;i++)Record(false,14.7f);Assert.IsTrue(c.PlayingTimeConcern("p1"));}
        [Test] public void RecentWindowDropsOldStarts(){for(int i=0;i<12;i++)Record(true,90);for(int i=0;i<12;i++)Record(false,0);Assert.AreEqual(12,contract.playingTime.games.Count);Assert.IsTrue(c.PlayingTimeConcern("p1"));}
        [Test] public void IncomingLoanTracksItsOwnPromise(){contract.parent="c1";for(int i=0;i<4;i++)Record(false,0);Invoke("DevelopmentAndRoles",db);Assert.AreEqual(71,c.Person("p1").morale);}
        [Test] public void YouthDoesNotReceiveBrokenSeniorQuotaComplaint(){contract.role="youth";for(int i=0;i<12;i++)Record(false,0);Invoke("DevelopmentAndRoles",db);Assert.AreEqual(75,c.Person("p1").morale);}
        [Test] public void FreshContractIsNotJudgedBeforeSixtyDays(){contract.joined=30;for(int i=0;i<12;i++)Record(false,0);Invoke("DevelopmentAndRoles",db);Assert.AreEqual(75,c.Person("p1").morale);}
        [Test] public void LegacyContractDoesNotPretendItsStartsAreKnown(){contract.playingTime=null;Assert.That(c.PlayingTimeProgress("p1"),Does.Contain("titularisations non documentées"));Assert.IsNull(contract.playingTime);}
        [Test] public void SaveRoundTripRetainsStatusAndObservedSelection(){contract.role="impact_sub";Record(false,24);var restored=JsonUtility.FromJson<Employment>(JsonUtility.ToJson(contract));Assert.AreEqual("impact_sub",restored.role);Assert.AreEqual(24,restored.playingTime.games.Single().minutes);Assert.IsFalse(restored.playingTime.games.Single().started);}
        [Test] public void ParentLoanSnapshotDoesNotShareMutableUsage(){Record(true,80);Invoke("CaptureLoanParent",contract,db.Find("p1"));contract.playingTime.games[0].minutes=0;Assert.AreEqual(80,contract.parentConditions.playingTime.games[0].minutes);Invoke("RestoreLoanParent",contract,db.Find("p1"),c.club);Assert.AreEqual(80,contract.playingTime.games[0].minutes);}
        [Test] public void NewPermanentLoanPurchaseBeginsANewPromiseWindow(){Record(true,80);contract.purchaseConditions=new LoanContractConditions{role="starter",wage=500,until=1000};Invoke("ConvertLoanPurchase",contract,db.Find("p1"));Assert.AreEqual(0,contract.playingTime.games.Count);Assert.AreEqual("starter",contract.role);}
        [TestCase("key")][TestCase("starter")][TestCase("rotation")][TestCase("impact_sub")][TestCase("youth")]
        public void AllFiveRolesCanBeProposedAndSigned(string role){db.Find("p1").age=21;c.ProposeTransfer(db,"p1",0,1000,3,role);var offer=c.world.offers.Last();Assert.AreEqual(role,offer.role);offer.status="accepted";c.SignTransfer(db,"p1");Assert.AreEqual(role,contract.role);Assert.AreEqual(0,contract.playingTime.games.Count);}
        [Test] public void ExperiencedPlayerCannotBePromisedYouthStatus(){Assert.Throws<ArgumentException>(()=>c.ProposeTransfer(db,"p1",0,1000,3,"youth"));}
        [Test] public void UnknownStatusIsRejectedInsteadOfSilentlyChanged(){Assert.Throws<ArgumentException>(()=>c.ProposeTransfer(db,"p1",0,1000,3,"infinite"));}
        [Test] public void DismissalStopsCountedMinutes(){c.match=new MatchState{periodSeconds=2700};c.match.events.Add(new MatchEvent{kind="red",side=0,player="p1",time=1200});Record(true,90);Assert.AreEqual(20,contract.playingTime.games.Single().minutes);}
        [TestCase("rotation",false)][TestCase("impact_sub",false)][TestCase("starter",true)][TestCase("key",true)]
        public void StrongPlayerRequiresAStartingProjectEvenWithGenerousWages(string role,bool accepted){db.Find("p1").rating=90;c.ProposeTransfer(db,"p1",0,5000,3,role);c.life.day+=2;Invoke("ManagementDay",db);Assert.AreEqual(accepted?"accepted":"counter",c.world.offers.Last().status);if(!accepted)Assert.That(c.life.messages.Last(m=>m.player=="p1"&&m.action=="transfer").text,Does.Contain("rôle de titulaire"));}
        [Test] public void SeniorReadyYoungsterCanRejectYouthProject(){db.Find("p1").age=22;contract.role="rotation";Assert.IsNotNull(c.PlayingTimeOfferIssue(db,"p1","youth"));}
        [Test] public void DevelopmentProjectCanSuitAnEighteenYearOld(){db.Find("p1").age=18;contract.role="rotation";Assert.IsNull(c.PlayingTimeOfferIssue(db,"p1","youth"));}
        [Test] public void StartingPlayerCanRejectContractualDemotion(){Assert.IsNotNull(c.PlayingTimeOfferIssue(db,"p1","rotation"));Assert.IsNull(c.PlayingTimeOfferIssue(db,"p1","starter"));}
        [Test] public void ConversationDoesNotTreatASatisfiedSuperSubAsUnused(){contract.role="impact_sub";for(int i=0;i<4;i++)Record(false,20);c.life.matches=100;Invoke("TalkContext",db,"p1","role",false);Assert.That(c.life.messages.Last(m=>m.subject=="Réponse").text,Does.Contain("Engagement respecté"));}
        [Test] public void AliasesRemainReadable(){Assert.AreEqual("key",PlayingTimeRoles.Normalize("star"));Assert.AreEqual("youth",PlayingTimeRoles.Normalize("prospect"));Assert.AreEqual("Remplaçant",PlayingTimeRoles.Label("rotation"));}
    }
}

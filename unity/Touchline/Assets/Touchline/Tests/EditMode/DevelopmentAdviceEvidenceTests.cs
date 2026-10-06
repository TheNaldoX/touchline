using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Touchline.Core;
namespace Touchline.Tests
{
    public sealed class DevelopmentAdviceEvidenceTests
    {
        Database db;Career c;Employment contract;
        [SetUp] public void Setup(){c=StaffTrainingLifecycleTests.Fixture(out db);db.Find("p9").age=20;contract=c.Contract(db,"p9");contract.role="key";contract.playingTime=new PlayingTimeUsage{club=c.club};c.Staff("youth").coaching=16;c.Staff("youth").wage=100;}
        string Advice()=>c.PlayerDevelopmentAdvice(db,"p9");
        void Games(int count,bool starter,int minutes){for(int i=0;i<count;i++)contract.playingTime.Add(starter,minutes);}
        [TestCase("key")][TestCase("starter")] public void BenchAppearancesCannotBeCalledARegularStartingRole(string role){contract.role=role;Games(4,false,20);Assert.IsTrue(c.PlayingTimeConcern("p9"));StringAssert.Contains("statut contractuel",Advice());}
        [Test] public void ImpactSubBelowAgreedFrequencyNeedsMoreMatches(){contract.role="impact_sub";Games(3,false,20);Games(5,false,0);Assert.IsTrue(c.PlayingTimeConcern("p9"));StringAssert.Contains("manque surtout des matchs",Advice());}
        [Test] public void RegularImpactSubDoesNotNeedStarts(){contract.role="impact_sub";Games(4,false,20);Games(4,false,0);Assert.IsFalse(c.PlayingTimeConcern("p9"));StringAssert.Contains("régulier",Advice());}
        [TestCase(0)][TestCase(1)][TestCase(3)] public void InsufficientObservedGamesAreNotCalledRegular(int count){Games(count,true,90);StringAssert.Contains("quatre rencontres",Advice());StringAssert.DoesNotContain("régulier",Advice());}
        [Test] public void MissingLegacyGameListIsAnIncompleteObservationNotAnException(){contract.playingTime.games=null;StringAssert.Contains("quatre rencontres",Advice());}
        [Test] public void ForeignClubHistoryCannotProduceLocalStartingRoleCriticism(){contract.playingTime.club="c1";Games(8,false,0);StringAssert.Contains("autre club",Advice());StringAssert.DoesNotContain("statut contractuel",Advice());}
        [Test] public void LegitimateStartingThresholdIsRecognized(){Games(7,true,80);Games(3,false,0);Assert.IsFalse(c.PlayingTimeConcern("p9"));StringAssert.Contains("régulier",Advice());}
        [Test] public void VeryShortStartsNeedDevelopmentMinutesWithoutInventingContractBreach(){Games(4,true,5);Assert.IsFalse(c.PlayingTimeConcern("p9"));StringAssert.Contains("manque surtout des matchs",Advice());StringAssert.DoesNotContain("statut contractuel",Advice());}
        [Test] public void YouthWithoutSeniorMinutesStillNeedsADevelopmentPath(){contract.role="youth";Games(4,false,0);Assert.IsFalse(c.PlayingTimeConcern("p9"));StringAssert.Contains("manque surtout des matchs",Advice());}
        [Test] public void WeakStaffDoesNotInventPreciseAssessment(){Games(4,false,20);c.Staff("youth").coaching=6;StringAssert.Contains("trop limité",Advice());}
        [Test] public void InjuryAndRecoveryStillHavePriority(){Games(4,false,20);c.OpenInjury(db,"p9","bruise");StringAssert.StartsWith("Priorité au dossier médical",Advice());}
        [Test] public void ReadingAdviceDoesNotMutateRatingsUsageOrRng(){Games(4,false,20);string state=JsonUtility.ToJson(c);string player=JsonUtility.ToJson(db.Find("p9"));Advice();Advice();Assert.AreEqual(state,JsonUtility.ToJson(c));Assert.AreEqual(player,JsonUtility.ToJson(db.Find("p9")));}
    }
}

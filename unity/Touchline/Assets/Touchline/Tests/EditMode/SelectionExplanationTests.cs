using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Touchline.Core;
namespace Touchline.Tests
{
    public sealed class SelectionExplanationTests
    {
        Database db;Career c;Employment contract;
        [SetUp] public void Setup(){c=StaffTrainingLifecycleTests.Fixture(out db);contract=c.Contract(db,"p9");contract.role="key";contract.playingTime=new PlayingTimeUsage{club=c.club};for(int i=0;i<8;i++)contract.playingTime.Add(false,0);c.Person("p9").trust=60;c.Person("p9").fitness=100;}
        void Explain()=>c.Talk(db,"p9","explain",false);
        [TestCase("key")][TestCase("starter")][TestCase("impact_sub")][TestCase("rotation")] public void RecordedUnmetRoleCannotProduceConfidenceFromAnUnsupportedExplanation(string role){contract.role=role;Assert.IsTrue(c.PlayingTimeConcern("p9"));Explain();Assert.LessOrEqual(c.Person("p9").trust,60);StringAssert.Contains("rôle convenu",c.life.messages.Last().text);}
        [Test] public void WeeklyRepetitionCannotEraseSameUnmetPlayingCommitment(){Explain();c.life.day+=7;Explain();Assert.LessOrEqual(c.Person("p9").trust,60);Assert.IsTrue(c.PlayingTimeConcern("p9"));Assert.AreEqual(8,contract.playingTime.games.Count);}
        [Test] public void RealInjuryStillJustifiesAnExplanation(){c.OpenInjury(db,"p9","bruise");float before=c.Person("p9").trust;Explain();Assert.AreEqual(before+2,c.Person("p9").trust,.001);StringAssert.Contains("reprise",c.life.messages.Last().text);}
        [Test] public void RealFatigueStillJustifiesAnExplanation(){c.Person("p9").fitness=70;Explain();Assert.AreEqual(62,c.Person("p9").trust,.001);StringAssert.Contains("fatigue",c.life.messages.Last().text);}
        [Test] public void AgreedRoleWithRegularStartsPreservesLegitimateTrust(){contract.playingTime.games.Clear();for(int i=0;i<8;i++)contract.playingTime.Add(true,80);Assert.IsFalse(c.PlayingTimeConcern("p9"));Explain();Assert.AreEqual(62,c.Person("p9").trust,.001);}
        [Test] public void YouthRoleDoesNotInventAStartingQuota(){contract.role="youth";Assert.IsFalse(c.PlayingTimeConcern("p9"));Explain();Assert.AreEqual(62,c.Person("p9").trust,.001);}
        [Test] public void ForeignClubHistoryDoesNotDamageTrustHere(){contract.playingTime.club="c1";Explain();Assert.AreEqual(62,c.Person("p9").trust,.001);}
        [Test] public void InitialObservationWindowDoesNotInventAnUnmetCommitment(){contract.playingTime.games.RemoveRange(0,5);Explain();Assert.AreEqual(62,c.Person("p9").trust,.001);}
        [Test] public void ExplanationNeverChangesRecordedMinutesOrPromiseProgress(){var person=c.Person("p9");person.promiseUntil=21;person.promiseStarts=person.appearances;string usage=JsonUtility.ToJson(contract.playingTime);uint rng=c.life.seed;Explain();Assert.AreEqual(usage,JsonUtility.ToJson(contract.playingTime));Assert.AreEqual(21,person.promiseUntil);Assert.AreEqual(person.promiseStarts,person.appearances);Assert.AreEqual(rng,c.life.seed);}
        [Test] public void AgreedRestRemainsAValidReasonForCurrentNonSelection(){c.Person("p9").restUntil=3;Explain();Assert.AreEqual(62,c.Person("p9").trust,.001);StringAssert.Contains("convenu",c.life.messages.Last().text);}
        [Test] public void ConversationCooldownRemainsEnforced(){Explain();float trust=c.Person("p9").trust;Assert.Throws<InvalidOperationException>(()=>Explain());Assert.AreEqual(trust,c.Person("p9").trust);}
    }
}

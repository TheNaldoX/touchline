using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Touchline.Core;
namespace Touchline.Tests
{
    public sealed class SubstitutionFitnessEvidenceTests
    {
        Database db;Career c;MatchSimulation sim;string starter,bench;
        void Setup(int half=2700){c=StaffTrainingLifecycleTests.Fixture(out db);sim=MatchSimulation.Create(db,c,"c1",31,half);c.match=sim.State;c.ApplyMatchContext(sim);starter=sim.State.actors[1].id;bench=db.Squad(c.club).First(p=>!sim.State.used.Contains(p.id)).id;foreach(var p in c.life.players)c.Contract(db,p.id).playingTime=new PlayingTimeUsage{club=c.club};}
        void Minute(float minute){sim.State.clock=minute*sim.State.SecondsPerMinute;sim.State.period=minute>45?2:1;}
        MatchEvent Replace(float minute=75,float fitness=90.25f){Minute(minute);sim.State.actors[1].fitness=fitness;sim.Substitute(0,1,bench);return sim.State.events.Last(e=>e.kind=="substitution"&&e.side==0);}
        void Record(){Minute(90);sim.State.finished=true;c.life.seed=1;c.RecordMatch(db);}
        string Debrief()=>c.life.messages.Last(m=>m.subject=="Débrief de la rencontre").text;
        [TestCase(360)][TestCase(2700)] public void RecorderUsesObservedExitConditionWithoutChangingMinutes(int half){Setup(half);var e=Replace();Assert.IsTrue(e.hasOutgoingFitness);Assert.AreEqual(90.25f,e.observedOutgoingFitness);Record();Assert.AreEqual(90.25f,c.Person(starter).fitness);Assert.AreEqual(75,c.Contract(db,starter).playingTime.games.Single().minutes);Assert.AreEqual(15,c.Contract(db,bench).playingTime.games.Single().minutes);Assert.AreEqual(1,c.Person(starter).appearances);Assert.AreEqual(0,c.Person(bench).appearances);StringAssert.DoesNotContain("Condition estimée",Debrief());}
        [TestCase(0f)][TestCase(100f)] public void BoundaryFitnessMeasurementsRemainValid(float value){Setup();Replace(75,value);Record();Assert.AreEqual(value,c.Person(starter).fitness);StringAssert.DoesNotContain("Condition estimée",Debrief());}
        [Test] public void LegacyEventWithoutEvidenceRetainsEstimateAndLabelsIt(){Setup();var e=Replace();e.hasOutgoingFitness=false;e.observedOutgoingFitness=0;Record();Assert.AreEqual(86.5f,c.Person(starter).fitness,.001);StringAssert.Contains("Condition estimée pour 1 joueur(s)",Debrief());}
        [TestCase(-1f)][TestCase(101f)][TestCase(float.NaN)][TestCase(float.PositiveInfinity)] public void InvalidRecordedMeasurementFallsBackToLabelledEstimate(float invalid){Setup();var e=Replace();e.hasOutgoingFitness=true;e.observedOutgoingFitness=invalid;Record();Assert.AreEqual(86.5f,c.Person(starter).fitness,.001);StringAssert.Contains("inexploitable",Debrief());}
        [TestCase(float.NaN)][TestCase(float.PositiveInfinity)][TestCase(-1f)][TestCase(101f)] public void WriterDoesNotPutInvalidFitnessIntoTheSavedEvent(float invalid){Setup();var e=Replace(75,invalid);Assert.IsFalse(e.hasOutgoingFitness);Assert.AreEqual(0,e.observedOutgoingFitness);}
        [Test] public void CurrentMedicalConditionIsNotHealedByAnEarlierMeasurement(){Setup();Replace();c.OpenInjury(db,starter,"bruise");Assert.AreEqual(70,c.Person(starter).fitness);Record();Assert.AreEqual(70,c.Person(starter).fitness);}
        [Test] public void ASubstitutedSubstituteKeepsHisOwnConditionAndInterval(){Setup();Replace(30,94);string firstIncoming=bench;bench=db.Squad(c.club).First(p=>!sim.State.used.Contains(p.id)).id;var second=Replace(65,91);Assert.AreEqual(firstIncoming,second.receiver);Record();Assert.AreEqual(94,c.Person(starter).fitness);Assert.AreEqual(91,c.Person(firstIncoming).fitness);Assert.AreEqual(30,c.Contract(db,starter).playingTime.games.Single().minutes);Assert.AreEqual(35,c.Contract(db,firstIncoming).playingTime.games.Single().minutes);Assert.AreEqual(25,c.Contract(db,bench).playingTime.games.Single().minutes);}
        [Test] public void FullCareerSaveRestoresTheMeasuredSubstitution(){Setup();Replace();var saved=JsonUtility.FromJson<Career>(JsonUtility.ToJson(c));Assert.IsTrue(CareerSaveRestore.TryRestore(db,saved,out var restoredDb));c=saved;db=restoredDb;sim=new MatchSimulation(db,c.match);Record();Assert.AreEqual(90.25f,c.Person(starter).fitness);Assert.IsTrue(sim.State.events.Single(e=>e.kind=="substitution"&&e.receiver==starter).hasOutgoingFitness);}
        [Test] public void LegacyMatchJsonWithoutNewFieldsDoesNotInventZeroCondition(){Setup();Replace();string json=JsonUtility.ToJson(sim.State);json=System.Text.RegularExpressions.Regex.Replace(json,"\"hasOutgoingFitness\"\\s*:\\s*(?:true|false)\\s*,?","");json=System.Text.RegularExpressions.Regex.Replace(json,",?\\s*\"observedOutgoingFitness\"\\s*:\\s*[-0-9.]+","");sim=new MatchSimulation(db,JsonUtility.FromJson<MatchState>(json));c.match=sim.State;Record();Assert.AreEqual(86.5f,c.Person(starter).fitness,.001);StringAssert.Contains("Condition estimée",Debrief());}
        [Test] public void RepeatedRecordDoesNotApplyFatigueTwice(){Setup();Replace();Record();float fitness=c.Person(starter).fitness;int count=c.life.messages.Count;c.RecordMatch(db);Assert.AreEqual(fitness,c.Person(starter).fitness);Assert.AreEqual(count,c.life.messages.Count);}
        [Test] public void PlayersStillOnPitchRetainTheirActualFullTimeCondition(){Setup();sim.State.actors[1].fitness=88;Record();Assert.AreEqual(88,c.Person(starter).fitness);StringAssert.DoesNotContain("Condition estimée",Debrief());}
    }
}

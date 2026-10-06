using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Touchline.Core;

namespace Touchline.Tests
{
    public sealed class DelegatedCoachTests
    {
        Database db; Career career; MatchSimulation sim;
        void Setup(int half=2700)
        {
            var roles=new[]{"GK","LB","CB","CB","RB","DM","CM","CM","LW","ST","RW"};
            db=new Database{clubs=new[]{new ClubData{id="a"},new ClubData{id="b"}},players=Enumerable.Range(0,44).Select(i=>new PlayerData{id="p"+i,name="Player "+i,team=i<22?"a":"b",position=roles[i%11],positions=new[]{roles[i%11]},rating=70,potential=80,fitness=100,morale=75}).ToArray()};
            career=new Career{club="a",lineup=Enumerable.Range(0,11).Select(i=>"p"+i).ToArray()};sim=MatchSimulation.Create(db,career,"b",731,half);
        }
        void Stop(int minute=60){sim.State.clock=minute*sim.State.SecondsPerMinute;sim.State.period=minute>45?2:1;sim.State.restart=2;sim.State.phase="throw-in";}
        void Tired(int slot=9){sim.State.actors[slot].fitness=60;}
        [TestCase(360)][TestCase(2700)] public void ExplicitDelegationReplacesTiredPlayerAtStoppage(int half){Setup(half);Stop();Tired();sim.ReviewDelegatedSubstitutions();Assert.AreEqual("p20",sim.State.actors[9].id);Assert.AreEqual(1,sim.State.substitutions[0]);Assert.AreEqual(1,sim.State.homeWindows.Count);}
        [Test] public void NormalSimulationNeverAppliesDelegatedChanges(){Setup();Stop();Tired();sim.Advance(.1);Assert.AreEqual("p9",sim.State.actors[9].id);Assert.AreEqual(0,sim.State.substitutions[0]);}
        [Test] public void OpenPlayWaitsForAStoppage(){Setup();Stop();Tired();sim.State.restart=0;sim.ReviewDelegatedSubstitutions();Assert.AreEqual(0,sim.State.substitutions[0]);sim.State.restart=2;sim.ReviewDelegatedSubstitutions();Assert.AreEqual(1,sim.State.substitutions[0]);}
        [Test] public void EarlyUninjuredPlayersArePreserved(){Setup();Stop(15);Tired();sim.ReviewDelegatedSubstitutions();Assert.AreEqual(0,sim.State.substitutions[0]);}
        [Test] public void FreshButInjuredStrikerIsReplacedImmediately(){Setup();Stop(15);sim.State.actors[9].injured=true;sim.ReviewDelegatedSubstitutions();Assert.AreEqual("p20",sim.State.actors[9].id);}
        [Test] public void InjuredKeeperGetsAKeeper(){Setup();Stop(15);sim.State.actors[0].injured=true;sim.ReviewDelegatedSubstitutions();Assert.AreEqual("p11",sim.State.actors[0].id);}
        [Test] public void SentOffPlayerCannotBeReplaced(){Setup();Stop();Tired();sim.State.actors[9].sentOff=true;sim.State.actors[9].injured=true;sim.ReviewDelegatedSubstitutions();Assert.AreEqual("p9",sim.State.actors[9].id);Assert.AreEqual(0,sim.State.substitutions[0]);}
        [Test] public void InjuredBenchAndUsedPlayersCannotEnter(){Setup();Stop();Tired();db.Find("p20").unavailableDays=2;foreach(var p in db.Squad("a").Where(p=>p.id!="p20"&&!sim.State.used.Contains(p.id)))sim.State.used.Add(p.id);sim.ReviewDelegatedSubstitutions();Assert.AreEqual(0,sim.State.substitutions[0]);}
        [Test] public void MultipleInjuriesUseOneWindowAndFivePlayerCap(){Setup();Stop(20);for(int i=1;i<=6;i++)sim.State.actors[i].injured=true;sim.ReviewDelegatedSubstitutions();Assert.AreEqual(5,sim.State.substitutions[0]);Assert.AreEqual(1,sim.State.homeWindows.Count);sim.ReviewDelegatedSubstitutions();Assert.AreEqual(5,sim.State.substitutions[0]);}
        [Test] public void ThreeUsedWindowsPreventFurtherChanges(){Setup();Stop();Tired();sim.State.homeWindows.AddRange(new[]{10f,20f,30f});sim.ReviewDelegatedSubstitutions();Assert.AreEqual(0,sim.State.substitutions[0]);}
        [Test] public void SameCurrentWindowRemainsUsable(){Setup();Stop();Tired();sim.State.homeWindows.AddRange(new[]{10f,20f,sim.State.clock});sim.ReviewDelegatedSubstitutions();Assert.AreEqual(1,sim.State.substitutions[0]);Assert.AreEqual(3,sim.State.homeWindows.Count);}
        [Test] public void HalfTimeChangesDoNotUseAWindowAndPrecedeResume(){Setup();Stop(45);Tired();sim.State.halfTime=true;sim.State.homeWindows.AddRange(new[]{10f,20f,30f});sim.AdvanceDelegatedStep();Assert.AreEqual(1,sim.State.substitutions[0]);Assert.AreEqual(3,sim.State.homeWindows.Count);Assert.IsFalse(sim.State.halfTime);Assert.AreEqual(2,sim.State.period);}
        [Test] public void RoutineChangesAreBatchedAndNotRepeatedDuringSameStop(){Setup();Stop();for(int i=1;i<=5;i++)Tired(i);sim.ReviewDelegatedSubstitutions();Assert.AreEqual(2,sim.State.substitutions[0]);sim.State.clock+=.1f;sim.State.restart-=.1f;sim.ReviewDelegatedSubstitutions();Assert.AreEqual(2,sim.State.substitutions[0]);Assert.AreEqual(1,sim.State.homeWindows.Count);}
        [Test] public void ManualQueuedChangesTakePriorityWithoutExtraWindow(){Setup();Stop();sim.State.restart=0;Tired(8);Tired(9);sim.RequestSubstitution(0,9,"p20");sim.State.restart=2;sim.AdvanceDelegatedStep();sim.ReviewDelegatedSubstitutions();Assert.AreEqual(1,sim.State.substitutions[0]);Assert.AreEqual("p20",sim.State.actors[9].id);Assert.AreEqual("p8",sim.State.actors[8].id);Assert.AreEqual(1,sim.State.homeWindows.Count);}
        [Test] public void NoSuitableHealthyReserveLeavesInjuredKeeperOnPitch(){Setup();Stop();sim.State.actors[0].injured=true;db.Find("p11").unavailableDays=5;sim.ReviewDelegatedSubstitutions();Assert.AreEqual(0,sim.State.substitutions[0]);}
        [Test] public void ReviewDoesNotChangeTacticsOrConsumeRandomness(){Setup();Stop();Tired();string before=JsonUtility.ToJson(career.tactic);uint seed=sim.State.seed;sim.ReviewDelegatedSubstitutions();Assert.AreEqual(before,JsonUtility.ToJson(career.tactic));Assert.AreEqual(seed,sim.State.seed);Assert.AreSame(career.tactic,sim.State.homeTactic);}
        [Test] public void CancelAndManualPlayKeepTheCurrentRemainingEleven(){Setup();Stop();Tired();sim.AdvanceDelegatedStep();string[] ids=sim.State.actors.Select(p=>p.id).ToArray();Tired(8);sim.State.restart=0;sim.Advance(.1);sim.State.restart=2;sim.Advance(.1);CollectionAssert.AreEqual(ids,sim.State.actors.Select(p=>p.id));}
        [Test] public void ReviewCooldownSurvivesSaveAndResumesIdentically(){Setup();Stop();Tired();sim.ReviewDelegatedSubstitutions();var resumed=new MatchSimulation(db,JsonUtility.FromJson<MatchState>(JsonUtility.ToJson(sim.State)));for(int i=0;i<100;i++){sim.AdvanceDelegatedStep();resumed.AdvanceDelegatedStep();}Assert.AreEqual(JsonUtility.ToJson(sim.State),JsonUtility.ToJson(resumed.State));}
        [Test] public void EarlyInjuryBypassesRoutineCooldownAtNextStoppage(){Setup();Stop();Tired();sim.ReviewDelegatedSubstitutions();sim.State.restart=0;sim.ReviewDelegatedSubstitutions();sim.State.clock+=30;sim.State.restart=2;sim.State.actors[8].injured=true;sim.ReviewDelegatedSubstitutions();Assert.AreEqual(2,sim.State.substitutions[0]);Assert.AreEqual(2,sim.State.homeWindows.Count);}
        [Test] public void ResumingDelegationAtAnotherStoppageAfterManualPlayReviewsAnInjury(){Setup();Stop();Tired();sim.ReviewDelegatedSubstitutions();sim.State.clock+=40;sim.State.restart=2;sim.State.actors[8].injured=true;sim.ReviewDelegatedSubstitutions();Assert.AreEqual(2,sim.State.substitutions[0]);Assert.AreEqual(2,sim.State.homeWindows.Count);}
        [Test] public void FullTimeCannotAddChanges(){Setup();Stop();Tired();sim.State.finished=true;sim.ReviewDelegatedSubstitutions();Assert.AreEqual(0,sim.State.substitutions[0]);}
        [Test] public void ClearlyInferiorFreshReserveDoesNotReplaceHealthyStar(){Setup();Stop();Tired();foreach(var p in db.Squad("a").Where(p=>!sim.State.used.Contains(p.id)))p.rating=20;sim.ReviewDelegatedSubstitutions();Assert.AreEqual(0,sim.State.substitutions[0]);}
    }
}

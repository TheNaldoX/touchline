using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Touchline.Core;
namespace Touchline.Tests
{
    public class FullMatchDurationTests
    {
        Database db;
        [OneTimeSetUp] public void Setup()=>db=JsonUtility.FromJson<Database>(Resources.Load<TextAsset>("Data/database").text);
        MatchSimulation Create(uint seed=911)
        {
            var clubs=db.clubs.Where(c=>c.playable).Take(2).ToArray();var career=new Career{club=clubs[0].id};
            career.lineup=Career.Select(db,career.club,career.tactic);return MatchSimulation.Create(db,career,clubs[1].id,seed,2700);
        }
        [Test] public void PhysicalMinuteAndHalfTimeAreNoLongerCompressed()
        {
            var sim=Create();Assert.AreEqual(60,sim.State.SecondsPerMinute);
            sim.Advance(60);Assert.AreEqual(1,sim.State.Minute);Assert.AreEqual("01:00",TouchlineApp.BroadcastClock(sim.State.clock,sim.State.SecondsPerMinute));
            sim.State.clock=2699.9f;sim.Advance(2);Assert.AreEqual(2700,sim.State.clock);Assert.IsTrue(sim.State.halfTime);Assert.AreEqual(45,sim.State.Minute);
            sim.ResumeHalf();sim.State.clock=5399.9f;sim.Advance(2);Assert.AreEqual(5400,sim.State.clock);Assert.IsTrue(sim.State.finished);Assert.AreEqual(90,sim.State.Minute);
        }
        [Test] public void LegacySaveKeepsItsExistingClock()
        {
            var sim=Create();var state=JsonUtility.FromJson<MatchState>(JsonUtility.ToJson(sim.State));state.periodSeconds=0;state.clock=359.9f;
            var legacy=new MatchSimulation(db,state);legacy.Advance(.1);Assert.IsTrue(state.halfTime);Assert.AreEqual(45,state.Minute);Assert.AreEqual(360,state.HalfDuration);
        }
        [TestCase(false)] [TestCase(true)] public void FullDurationHighlightsPreserveEverySavedSimulationField(bool professionalRules)
        {
            var direct=Create();var visible=Create();direct.State.professionalRules=visible.State.professionalRules=professionalRules;
            direct.PlayToEnd();var broadcast=new MatchBroadcast(visible){Enabled=true,QuietSpeed=24};int frames=0,calm=0;
            while(!visible.State.finished&&frames++<60000){broadcast.Advance(.1f,3);if(broadcast.Quiet)calm++;if(visible.State.halfTime)visible.ResumeHalf();}
            Assert.IsTrue(visible.State.finished);Assert.Greater(calm,0);Assert.AreEqual(5400,visible.State.clock);
            Assert.AreEqual(JsonUtility.ToJson(direct.State),JsonUtility.ToJson(visible.State));
        }
        [Test] public void FullMatchReloadRetainsDurationAndExactContinuation()
        {
            var sim=Create(179);sim.Advance(713.27);var saved=JsonUtility.ToJson(sim.State);
            var restored=new MatchSimulation(db,JsonUtility.FromJson<MatchState>(saved));sim.Advance(51.19);restored.Advance(51.19);
            Assert.AreEqual(2700,restored.State.HalfDuration);Assert.AreEqual(JsonUtility.ToJson(sim.State),JsonUtility.ToJson(restored.State));
        }
        [Test] public void AttributeCachePreservesCompleteMatchIncludingMidMatchChanges()
        {
            var cached=Create(391);cached.CacheAttributeReads=true;var uncached=Create(391);uncached.CacheAttributeReads=false;
            cached.Advance(1700);uncached.Advance(1700);
            foreach(var sim in new[]{cached,uncached}){sim.State.homeTactic.SetFormation("4-4-2");sim.State.homeTactic.pressing=.8f;sim.State.actors[4].fitness=45;}
            cached.PlayToEnd();uncached.PlayToEnd();Assert.AreEqual(JsonUtility.ToJson(uncached.State),JsonUtility.ToJson(cached.State));
        }
        [Test] public void LongCornerPreparationIsQuietButDeliveryReturnsToLive()
        {
            var sim=Create();var view=new MatchBroadcast(sim){Enabled=true};var m=sim.State;
            m.clock=60;m.phase="corner";m.restart=25;m.ball.position=new Point(52.5f,34);view.Refresh();Assert.IsTrue(view.Quiet);
            m.restart=5;view.Refresh();Assert.IsFalse(view.Quiet);Assert.AreEqual("Corner",view.Reason);
        }
        [Test] public void CoachRespondsToObservedWidthWithoutChangingManagersInstructions()
        {
            var sim=Create();var m=sim.State;string home=JsonUtility.ToJson(m.homeTactic);
            m.clock=901;m.awayTacticalReviewAt=900;m.metrics[0].crosses=5;m.restart=0;float width=m.awayTactic.defensiveWidth;
            sim.Advance(.1);Assert.Greater(m.awayTactic.defensiveWidth,width);Assert.AreEqual(home,JsonUtility.ToJson(m.homeTactic));
            int events=m.events.Count(e=>e.kind=="opponent-adjustment");sim.Advance(1);Assert.AreEqual(events,m.events.Count(e=>e.kind=="opponent-adjustment"));
        }
        [Test] public void ExistingMatchMigratesCoachBaselineWithoutReplacingItsClockOrTactics()
        {
            var sim=Create();var m=sim.State;m.engineVersion=3;m.periodSeconds=0;m.clock=160;m.awayTactic.pressing=.73f;m.awayTactic.defensiveWidth=.61f;m.awayBasePress=0;m.awayBaseDefensiveWidth=0;
            string before=JsonUtility.ToJson(m.awayTactic);var restored=new MatchSimulation(db,m);
            Assert.AreEqual(360,m.HalfDuration);Assert.AreEqual(.73f,m.awayBasePress);Assert.AreEqual(.61f,m.awayBaseDefensiveWidth);Assert.AreEqual(240,m.awayTacticalReviewAt);Assert.AreEqual(before,JsonUtility.ToJson(m.awayTactic));Assert.AreEqual(4,m.engineVersion);
        }
    }
}

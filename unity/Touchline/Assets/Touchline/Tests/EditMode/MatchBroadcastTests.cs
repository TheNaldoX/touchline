using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Touchline.Core;

namespace Touchline.Tests
{
    public class MatchBroadcastTests
    {
        Database db;Career career;string opponent;
        [OneTimeSetUp] public void Setup(){db=JsonUtility.FromJson<Database>(Resources.Load<TextAsset>("Data/database").text);var clubs=db.clubs.Where(c=>c.playable).Take(2).ToArray();career=new Career{club=clubs[0].id};career.lineup=Career.Select(db,career.club,career.tactic);opponent=clubs[1].id;}
        MatchSimulation Create(uint seed=751){var independent=new Career{club=career.club,lineup=(string[])career.lineup.Clone(),tactic=JsonUtility.FromJson<Tactic>(JsonUtility.ToJson(career.tactic))};return MatchSimulation.Create(db,independent,opponent,seed);}
        static MatchBroadcast Calm(MatchSimulation sim)
        {
            var broadcast=new MatchBroadcast(sim){Enabled=true};sim.State.clock=20;sim.State.phase="throw-in";sim.State.restart=20;sim.State.ball.position=new Point(0,34);broadcast.Refresh();Assert.IsTrue(broadcast.Quiet);return broadcast;
        }
        [TestCase(8)] [TestCase(12)] [TestCase(24)] public void CalmPeriodsAdvanceEveryPhysicsStepAtSelectedRate(int speed)
        {
            var sim=Create();var view=Calm(sim);view.QuietSpeed=speed;view.Advance(.1f,1);Assert.That(sim.State.clock,Is.EqualTo(20+speed*.1f).Within(.001f));Assert.LessOrEqual(view.LastFrameSteps,32);
        }
        [Test] public void IntegralViewKeepsNormalSpeed(){var sim=Create();var view=Calm(sim);view.Enabled=false;view.Advance(.1f,1);Assert.That(sim.State.clock,Is.EqualTo(20.1f).Within(.001f));Assert.IsFalse(view.Quiet);}
        [TestCase(-1,0,MatchViewingMode.Full)][TestCase(-1,1,MatchViewingMode.Highlights)]
        [TestCase(1,0,MatchViewingMode.KeyMoments)][TestCase(3,1,MatchViewingMode.Extended)][TestCase(99,1,MatchViewingMode.Highlights)]
        public void ViewingPreferenceMigratesTheOldSwitchWithoutLosingTheNewSelection(int saved,int legacy,MatchViewingMode expected)=>Assert.AreEqual(expected,MatchBroadcast.ResolveMode(saved,legacy));
        [TestCase(MatchViewingMode.KeyMoments,false,false)]
        [TestCase(MatchViewingMode.Highlights,true,false)]
        [TestCase(MatchViewingMode.Extended,true,true)]
        public void SelectionLevelsActuallyDistinguishApproachAndBuildup(MatchViewingMode mode,bool approach,bool buildup)
        {
            var sim=Create();var m=sim.State;var view=new MatchBroadcast(sim);view.SetMode(mode);
            m.restart=0;m.phase="play";m.clock=100;m.ball=new BallState{kind="cross",side=0,from=m.actors[8].id,start=new Point(24,25),end=new Point(35,0),position=new Point(24,25),elapsed=.2f};
            view.Refresh();Assert.AreEqual(approach,!view.Quiet,"Approach selection");
            m.clock=120;m.ball.kind="pass";m.ball.start=new Point(10,20);m.ball.end=new Point(24,15);m.ball.elapsed=0;
            view.Refresh();Assert.AreEqual(buildup,!view.Quiet,"Progressive buildup selection");
        }
        [TestCase(MatchViewingMode.KeyMoments)][TestCase(MatchViewingMode.Highlights)][TestCase(MatchViewingMode.Extended)]
        public void EverySelectionShowsMajorEventsAndInterruptsForManagerDecisions(MatchViewingMode mode)
        {
            var sim=Create();var m=sim.State;var view=new MatchBroadcast(sim);view.SetMode(mode);m.phase="throw-in";m.restart=30;m.ball=new BallState{position=new Point(0,34)};
            foreach(var kind in new[]{"shot","save","woodwork","goal","penalty","injury","red"}){
                m.clock+=100;view.Advance(.001f,1);m.events.Add(new MatchEvent{kind=kind,side=0,time=m.clock});float clock=m.clock;view.Advance(.001f,1);
                Assert.IsFalse(view.Quiet,kind+" was hidden");if(kind=="injury"||kind=="red"){Assert.IsTrue(view.PauseRequested);Assert.AreEqual(clock,m.clock);}
            }
        }
        [Test] public void ChangingModeKeepsStateAndStopsTheQuietViewImmediately()
        {
            var sim=Create();var view=Calm(sim);string before=JsonUtility.ToJson(sim.State);
            foreach(var mode in new[]{MatchViewingMode.Full,MatchViewingMode.Extended,MatchViewingMode.KeyMoments,MatchViewingMode.Highlights}){
                view.SetMode(mode);Assert.AreEqual(mode,view.Mode);Assert.IsFalse(view.Quiet);Assert.AreEqual(before,JsonUtility.ToJson(sim.State));
            }
        }
        [TestCase(MatchViewingMode.Full)][TestCase(MatchViewingMode.KeyMoments)][TestCase(MatchViewingMode.Highlights)][TestCase(MatchViewingMode.Extended)]
        public void EveryViewingModePreservesTheSameNinetyMinuteMatch(MatchViewingMode mode)
        {
            var reference=Create(20261005);var shown=Create(20261005);reference.State.periodSeconds=shown.State.periodSeconds=2700;
            reference.PlayToEnd();var view=new MatchBroadcast(shown);view.SetMode(mode);int frames=0,quiet=0;
            while(!shown.State.finished&&frames++<15000){view.Advance(.1f,10);if(view.Quiet)quiet++;if(shown.State.halfTime)shown.ResumeHalf();}
            Assert.IsTrue(shown.State.finished);Assert.AreEqual(JsonUtility.ToJson(reference.State),JsonUtility.ToJson(shown.State),"Viewing must not alter any saved match state");
            if(mode!=MatchViewingMode.Full)Assert.Greater(quiet,0);else Assert.AreEqual(0,quiet);
            TestContext.WriteLine(mode+": frames="+frames+", quiet="+quiet+", score="+shown.State.score[0]+"-"+shown.State.score[1]);
        }
        [TestCase(1)][TestCase(2)][TestCase(3)][TestCase(5)][TestCase(10)]
        public void AllLiveSpeedsAdvanceTheSameFixedSimulationWithoutSkippingTicks(int speed)
        {
            var live=Create(932);var expected=Create(932);var view=new MatchBroadcast(live){Enabled=false};
            expected.Advance(.1f*speed);view.Advance(.1f,speed);
            Assert.That(live.State.clock,Is.EqualTo(.1f*speed).Within(.0001f));
            Assert.AreEqual(expected.State.seed,live.State.seed);Assert.AreEqual(expected.State.events.Count,live.State.events.Count);
            for(int i=0;i<22;i++)Assert.That(Point.Distance(expected.State.actors[i].position,live.State.actors[i].position),Is.LessThan(.00001f));
            Assert.That(Point.Distance(expected.State.ball.position,live.State.ball.position),Is.LessThan(.00001f));Assert.LessOrEqual(view.LastFrameSteps,64);
        }
        [Test] public void QuietSituationExplainsPreparationAndActiveConstruction()
        {
            var sim=Create();var view=Calm(sim);Assert.That(view.QuietSituation,Does.Contain("Touche").And.Contain("20 s"));
            sim.State.restart=0;sim.State.ball.elapsed=-1;sim.State.ball.position=new Point(0,0);sim.State.turnoverAt=0;
            Assert.That(view.QuietSituation,Does.Contain("Construction").And.Contain("axe central"));
            sim.State.ball.held=true;Assert.That(view.QuietSituation,Does.Contain("Gardien"));
        }
        [TestCase("shot","FRAPPE")][TestCase("goal","BUT !")][TestCase("save","ARRÊT DU GARDIEN")]
        public void ImportantActionsHaveDistinctUnambiguousHeadlines(string kind,string expected)=>Assert.AreEqual(expected,MatchBroadcast.EventHeadline(kind));
        [TestCase("shot",2.1f)][TestCase("goal",5f)]
        public void ExpressHighlightsRemainLiveForAReadableRealDuration(string kind,float duration)
        {
            var sim=Create();sim.State.periodSeconds=2700;var view=Calm(sim);sim.State.events.Add(new MatchEvent{kind=kind,side=0,time=20,player=sim.State.actors[9].id});
            int frames=Mathf.FloorToInt(duration*10)-1;
            for(int i=0;i<frames;i++){view.Advance(.1f,10);Assert.IsFalse(view.Quiet,kind+" disappeared before its readable display duration");}
            Assert.That(sim.State.clock,Is.GreaterThan(20+duration*5),"The simulation must keep advancing, not freeze during the banner");
        }
        [Test] public void SlowFrameReportsItsQuietSimulationBudgetWithoutSkippingPhysics()
        {
            var sim=Create();sim.State.periodSeconds=2700;var view=Calm(sim);view.QuietSpeed=120;view.Advance(.1f,1);
            Assert.AreEqual(64,view.LastFrameSteps);Assert.IsTrue(view.BudgetLimited);Assert.That(view.EffectiveSpeed,Is.InRange(60,65));
            Assert.That(sim.State.clock,Is.InRange(26,26.5f));
        }
        [TestCase("shot")] [TestCase("cross")] [TestCase("through")] public void DangerousFlightReturnsToLiveBeforeConsumingIt(string kind)
        {
            var sim=Create();var view=Calm(sim);sim.State.restart=0;sim.State.ball.kind=kind;sim.State.ball.end=new Point(40,0);view.Refresh();Assert.IsFalse(view.Quiet);
        }
        [TestCase("goal")] [TestCase("penalty")] [TestCase("corner")] public void ImportantRestartAlwaysStaysVisible(string kind)
        {
            var sim=Create();var view=Calm(sim);sim.State.phase=kind;view.Refresh();Assert.IsFalse(view.Quiet);
        }
        [TestCase("injury")] [TestCase("red")] public void ManagerInterruptionStopsTheFastClockImmediately(string kind)
        {
            var sim=Create();var view=Calm(sim);sim.State.events.Add(new MatchEvent{kind=kind,side=0,time=20,player=sim.State.actors[9].id});view.Advance(.1f,1);Assert.IsTrue(view.PauseRequested);Assert.IsFalse(view.Quiet);Assert.AreEqual(20,sim.State.clock);
            view.Advance(.1f,1);Assert.IsFalse(view.PauseRequested,"Acknowledged event must not trap the manager in an endless pause");
        }
        [Test] public void HalfTimeStopsAccelerationAtTheExactBoundary()
        {
            var sim=Create();var view=Calm(sim);sim.State.clock=359.9f;view.Advance(.1f,1);Assert.AreEqual(360,sim.State.clock);Assert.IsTrue(sim.State.halfTime);Assert.IsFalse(view.Quiet);Assert.AreEqual(0,sim.State.remainder);
        }
        [TestCase(0,"00:00")] [TestCase(8,"01:00")] [TestCase(360,"45:00")] [TestCase(720,"90:00")]
        public void BroadcastClockUsesMinutesAndSeconds(float time,string expected)=>Assert.AreEqual(expected,TouchlineApp.BroadcastClock(time));
        void HalfTimeChanges(MatchSimulation sim)
        {
            var incoming=db.Squad(sim.State.home).First(p=>!sim.State.used.Contains(p.id)&&p.unavailableDays==0);
            var outgoing=sim.State.actors.Last(p=>p.side==0&&!p.sentOff);
            sim.RequestSubstitution(0,outgoing.slot,incoming.id);
            sim.State.homeTactic.pressing=.8f;sim.ResumeHalf();
        }
        [TestCase(711,false)] [TestCase(20261007,false)] [TestCase(817,false)] [TestCase(2701,false)]
        [TestCase(711,true)] [TestCase(20261007,true)] [TestCase(817,true)] [TestCase(2701,true)]
        public void HighlightAndIntegralViewsProduceTheSameCompleteMatch(int seed,bool professionalRules)
        {
            var normal=Create((uint)seed);var accelerated=Create((uint)seed);var view=new MatchBroadcast(accelerated){Enabled=true};
            normal.State.professionalRules=accelerated.State.professionalRules=professionalRules;
            normal.Advance(720);HalfTimeChanges(normal);normal.Advance(720);
            bool halftime=false;int frames=0,quiet=0;while(!accelerated.State.finished&&frames++<50000){int before=accelerated.State.events.Count;view.Advance(1f/30,1);if(view.Quiet)quiet++;for(int i=before;i<accelerated.State.events.Count;i++){var kind=accelerated.State.events[i].kind;if(kind=="goal"||kind=="shot"||kind=="injury"||kind=="red")Assert.IsFalse(view.Quiet,"Important event skipped by accelerated presentation: "+kind);}if(accelerated.State.halfTime){halftime=true;HalfTimeChanges(accelerated);}}
            Assert.IsTrue(halftime);Assert.IsTrue(accelerated.State.finished);Assert.Greater(quiet,30,"A real fixture must contain calm periods");
            Assert.AreEqual(JsonUtility.ToJson(normal.State),JsonUtility.ToJson(accelerated.State),"Presentation must preserve RNG, score, events, fitness, tactics and all other saved simulation state");
            TestContext.WriteLine("Frames "+frames+"; calm frames "+quiet+"; result "+accelerated.State.score[0]+"-"+accelerated.State.score[1]);
        }
        [Test] public void SaveRestoreCanReconstructPresentationWithoutChangingTheMatch()
        {
            var sim=Create();var view=new MatchBroadcast(sim){Enabled=true};for(int i=0;i<700;i++)view.Advance(1f/30,1);
            var restored=new MatchSimulation(db,JsonUtility.FromJson<MatchState>(JsonUtility.ToJson(sim.State)));var next=new MatchBroadcast(restored){Enabled=true};
            TestContext.WriteLine("Saved clock "+sim.State.clock+"; phase "+sim.State.phase+"; ball "+sim.State.ball.kind+"; fixed start "+sim.State.ball.fixedStart+"; restart taker null "+(sim.State.restartTaker==null));
            System.IO.File.WriteAllText(System.IO.Path.GetFullPath("../../artifacts/unity/broadcast-save-original.json"),JsonUtility.ToJson(sim.State));
            System.IO.File.WriteAllText(System.IO.Path.GetFullPath("../../artifacts/unity/broadcast-save-restored.json"),JsonUtility.ToJson(restored.State));
            sim.Advance(720);sim.ResumeHalf();sim.Advance(720);int frames=0;while(!restored.State.finished&&frames++<50000){next.Advance(1f/30,1);if(restored.State.halfTime)restored.ResumeHalf();}
            Assert.IsTrue(restored.State.finished);Assert.AreEqual(JsonUtility.ToJson(sim.State),JsonUtility.ToJson(restored.State));
        }
    }
}

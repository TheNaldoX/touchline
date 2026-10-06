using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Touchline.Core;

namespace Touchline.Tests
{
    public sealed class GoalNetPresentationTests
    {
        static MatchState Fixture(float x = 52.61f, float z = -1.9f, float h = 1.7f, float vx = 15, float vz = -10, float vy = 3)
        {
            return new MatchState { home = "home", away = "away", clock = 100, periodSeconds = 2700,
                score = new[] { 1, 0 }, ball = new BallState { previous = new Point(x - .7f, z + .5f), previousHeight = h - .15f,
                    position = new Point(x + .8f, z - .5f), height = h + .15f, velocity = new Point(vx, vz), verticalVelocity = vy, kind = "shot", from = "striker" },
                events = new List<MatchEvent> { new MatchEvent { kind = "goal", side = 0, player = "striker", time = 100, position = new Point(x + .8f, z - .5f) } } };
        }
        static GoalPresentationContact Bind(MatchState state, float x = 52.61f, float z = -1.9f, float h = 1.7f)
        {
            var contact = GoalPresentationContact.Capture(state, .47776464f, new Point(x, z), h, 0);
            Assert.That(contact.valid, Is.True);
            state.phase = "goal"; state.restart = 45; state.restartSide = 1;
            state.ball = new BallState { position = new Point(Math.Sign(x) * 53, z) };
            Assert.That(GoalPresentationContact.BindRestartBall(ref contact, state), Is.True); return contact;
        }
        static GoalBallPose Position(GoalNetPresentation net, object identity, MatchState state, GoalPresentationContact contact, float alpha = 1)
        {
            Assert.That(net.TryPosition(identity, state, contact, alpha, out var pose), Is.True); return pose;
        }
        static void Equal(GoalBallPose actual, GoalBallPose expected, float tolerance = .0004f)
        {
            Assert.That(actual.position.x, Is.EqualTo(expected.position.x).Within(tolerance));
            Assert.That(actual.position.z, Is.EqualTo(expected.position.z).Within(tolerance));
            Assert.That(actual.height, Is.EqualTo(expected.height).Within(tolerance));
        }
        [TestCase(1,30)] [TestCase(1,60)] [TestCase(1,120)]
        [TestCase(2,30)] [TestCase(2,60)] [TestCase(2,120)]
        [TestCase(10,30)] [TestCase(10,60)] [TestCase(10,120)]
        public void FrameCadenceAndSpeedDoNotChangeAnalyticPose(int speed, int fps)
        {
            var state = Fixture(); var contact = Bind(state); var net = new GoalNetPresentation(); var identity = new object();
            for (int frame = 0; frame <= fps * 2; frame++) {
                double elapsed = frame * speed / (double)fps; float ticks = (float)(Math.Floor(elapsed / .1) * .1);
                float alpha = (float)((elapsed - ticks) / .1); state.clock = 100 + ticks; state.restart = 45 - ticks;
                Equal(Position(net, identity, state, contact, alpha), Position(new GoalNetPresentation(), new object(), state, contact, alpha));
            }
        }
        [Test] public void SameTickPauseAccumulatorResetHoldsPoseThenContinues()
        {
            var state = Fixture(); var contact = Bind(state); var net = new GoalNetPresentation(); var identity = new object();
            var pose = Position(net, identity, state, contact, .8f);
            Equal(Position(net, identity, state, contact, 0), pose);
            for (int i = 0; i < 10; i++) Equal(Position(net, identity, state, contact, .8f), pose);
            state.clock += .1f; Position(net, identity, state, contact, .2f); Assert.That(net.Active, Is.True);
        }
        [Test] public void ActualClockRewindRevokesProofUntilNewSimulation()
        {
            var state = Fixture(); var contact = Bind(state); var net = new GoalNetPresentation(); var identity = new object();
            Position(net, identity, state, contact); state.clock = 101; Position(net, identity, state, contact);
            state.clock = 100.5f; Assert.That(net.TryPosition(identity, state, contact, 1, out _), Is.False);
            state.clock = 101.1f; Assert.That(net.TryPosition(identity, state, contact, 1, out _), Is.False);
            Position(net, new object(), state, contact);
        }
        [Test] public void FirstRenderAfterSkippedTicksLatchesSameGoal()
        {
            var state = Fixture(); var contact = Bind(state); state.clock += 1.2f; state.restart -= 1.2f;
            var net = new GoalNetPresentation(); Position(net, new object(), state, contact, .3f);
            Assert.That(net.Active, Is.True); Assert.That(net.ResidualSpeed, Is.LessThan(.0001f));
        }
        [Test] public void BallReplacementAndPhaseChangeRejectStaleGoal()
        {
            var state = Fixture(); var contact = Bind(state); var net = new GoalNetPresentation(); var identity = new object();
            Position(net, identity, state, contact);
            state.ball = new BallState { position = new Point(53, -1.9f) };
            Assert.That(net.TryPosition(identity, state, contact, 1, out _), Is.False);
            state = Fixture(); contact = Bind(state); state.phase = "kickoff";
            Assert.That(net.TryPosition(new object(), state, contact, 1, out _), Is.False);
        }
        [Test] public void SlowAirborneLateralBallContinuesUntilRest()
        {
            var state = Fixture(52.61f, -3, 2, 0, 4, 0); var contact = Bind(state, 52.61f, -3, 2);
            var net = new GoalNetPresentation(); var identity = new object(); Position(net, identity, state, contact);
            Assert.That(net.SettleDuration, Is.GreaterThan(1.5f).And.LessThanOrEqualTo(8));
            Assert.That(net.ResidualSpeed, Is.LessThan(.0001f)); Assert.That(net.PathSamples, Is.LessThanOrEqualTo(1921));
            state.clock += 4; var pose = Position(net, identity, state, contact);
            state.clock += 30; Equal(Position(net, identity, state, contact), pose); Assert.That(pose.height, Is.EqualTo(.11f).Within(.00001f));
        }
        [TestCase(-1,-3.659f,2.439f)] [TestCase(-1,3.659f,.01f)]
        [TestCase(1,3.659f,2.439f)] [TestCase(1,-3.659f,.01f)]
        public void NearPostAndBarKeepExactCrossingThenRespectRadiusBounds(float sign, float z, float h)
        {
            var state = Fixture(sign * 52.61f, z, h, sign * 18, z < 0 ? -8 : 8, 2);
            var contact = Bind(state, sign * 52.61f, z, h); var net = new GoalNetPresentation(); var identity = new object();
            Equal(Position(net, identity, state, contact, contact.fraction), new GoalBallPose(contact.impact, h), .00001f);
            for (int i = 2; i <= 40; i++) {
                state.clock = 100 + i / 10f; var pose = Position(net, identity, state, contact);
                Assert.That(Math.Abs(pose.position.x), Is.InRange(52.6098f,54.1901f));
                Assert.That(Math.Abs(pose.position.z), Is.LessThanOrEqualTo(3.5501f)); Assert.That(pose.height, Is.InRange(.1099f,2.4401f));
            }
        }
        [Test] public void NullMalformedAndNonfiniteInputsFailClosed()
        {
            Assert.That(GoalPresentationContact.Capture(null,.5f,new Point(),1,0).valid, Is.False);
            for(int kind=0;kind<6;kind++) {
                var state=Fixture();
                if(kind==0)state.ball=null;if(kind==1)state.events=null;if(kind==2)state.score=null;
                if(kind==3)state.score=new[]{1};if(kind==4)state.events[0]=null;if(kind==5)state.ball.previousHeight=float.NaN;
                Assert.That(GoalPresentationContact.Capture(state,.5f,new Point(52.61f,0),1,0).valid,Is.False);
            }
            var valid=Fixture();var proof=Bind(valid);proof.previous.x=float.NaN;
            Assert.That(new GoalNetPresentation().TryPosition(new object(),valid,proof,0,out _),Is.False);
        }
        [Test] public void CoreHookRecordsActualFirstFlightDerivativeAndRetainsAcrossTicks()
        {
            var state=Fixture();state.actors=Array.Empty<Actor>();state.engineVersion=4;
            state.ball.elapsed=.02f;state.ball.position=new Point(52.62f,0);state.ball.height=.698f;state.ball.verticalVelocity=-5;
            var originalBall=state.ball;var seed=state.seed;int events=state.events.Count;
            var sim=new MatchSimulation(new Database{players=Array.Empty<PlayerData>()},state);
            var release=new BallReleaseContact {valid=true,clock=100,kind="shot",source="striker",fraction=.8f,
                previous=new Point(52,0),previousHeight=.11f,release=new Point(52.6f,0),releaseHeight=.11f,
                end=state.ball.position,endHeight=state.ball.height,flightEnd=new Point(52.7f,0),flightEndHeight=.11f,duration=.1f,loft=1};
            typeof(MatchSimulation).GetProperty("ReleaseContact").SetValue(sim,release);
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            typeof(MatchSimulation).GetMethod("RecordGoalContact",flags).Invoke(sim,new object[]{.9f,new Point(52.61f,0),.419f,0});
            Assert.That(state.ball,Is.SameAs(originalBall));Assert.That(state.seed,Is.EqualTo(seed));Assert.That(state.events.Count,Is.EqualTo(events));
            Assert.That(state.score,Is.EqualTo(new[]{1,0}));Assert.That(state.ball.height,Is.EqualTo(.698f));
            state.phase="goal";state.restart=45;state.restartSide=1;state.ball=new BallState{position=new Point(53,0)};
            var maintain=typeof(MatchSimulation).GetMethod("MaintainGoalContact",flags);maintain.Invoke(sim,null);
            Assert.That(sim.GoalContact.valid,Is.True);
            float progress=(.9f-.8f)*MatchSimulation.Step/release.duration;
            float expected=(float)Math.Cos(Math.PI*progress)*(float)Math.PI/release.duration;
            Assert.That(sim.GoalContact.verticalVelocity,Is.EqualTo(expected).Within(.0001f));
            state.clock+=1;maintain.Invoke(sim,null);Assert.That(sim.GoalContact.valid,Is.True);
            state.phase="kickoff";maintain.Invoke(sim,null);Assert.That(sim.GoalContact.valid,Is.False);
        }
        [Test] public void PreparationAndActualFlightBeforeGoalAreNotCollapsedIntoOneChord()
        {
            var state=Fixture();state.ball.elapsed=.02f;
            var release=new BallReleaseContact {valid=true,clock=100,kind="shot",source="striker",fraction=.8f,
                previous=new Point(52,0),previousHeight=.11f,release=new Point(52.6f,0),releaseHeight=.11f,
                end=new Point(52.62f,0),endHeight=.11f,flightEnd=new Point(53.6f,0),flightEndHeight=.11f,duration=1};
            state.ball.previous=release.previous;state.ball.previousHeight=release.previousHeight;state.ball.position=release.end;state.ball.height=release.endHeight;
            var contact=GoalPresentationContact.Capture(state,.9f,new Point(52.61f,0),.11f,0,release);
            Assert.That(contact.release.valid,Is.True);
            state.phase="goal";state.restart=45;state.restartSide=1;state.ball=new BallState{position=new Point(53,0)};
            Assert.That(GoalPresentationContact.BindRestartBall(ref contact,state),Is.True);
            var net=new GoalNetPresentation();var identity=new object();
            Assert.That(Position(net,identity,state,contact,.4f).position.x,Is.EqualTo(52.3f).Within(.0001f));
            Assert.That(Position(net,identity,state,contact,.85f).position.x,Is.EqualTo(52.605f).Within(.0001f));
            Assert.That(Position(net,identity,state,contact,.9f).position.x,Is.EqualTo(52.61f).Within(.0001f));
        }
        [TestCase(true,false)] [TestCase(false,true)]
        public void IntervalAndFulltimeDoNotAbruptlyDisableCurrentGoalPath(bool interval, bool finished)
        {
            var state=Fixture();var contact=Bind(state);var net=new GoalNetPresentation();var identity=new object();
            var pose=Position(net,identity,state,contact,.8f);state.halfTime=interval;state.finished=finished;
            Assert.That(GoalPresentationContact.MatchesRestart(contact,state),Is.True);
            for(int i=0;i<20;i++)Equal(Position(net,identity,state,contact,.8f),pose);
            state.phase="kickoff";Assert.That(net.TryPosition(identity,state,contact,.8f,out _),Is.False);
        }
    }
}

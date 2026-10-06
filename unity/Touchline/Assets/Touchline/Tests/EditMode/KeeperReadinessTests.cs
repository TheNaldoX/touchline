using System;
using NUnit.Framework;
using Touchline.Core;
namespace Touchline.Tests
{
    public sealed class KeeperReadinessTests
    {
        static MatchState State(float distance,bool opponent,bool shot,out Actor keeper)
        {
            keeper=new Actor{id="keeper",slot=0,side=0,position=new Point(-48,0),previous=new Point(-48,0),fitness=100};
            var owner=new Actor{id="owner",slot=9,side=opponent?1:0,position=new Point(-48+distance,0)};
            var point=owner.position;
            return new MatchState{clock=100,restart=0,phase="play",actors=new[]{keeper,owner},ball=new BallState{side=owner.side,owner=shot?null:owner.id,
                position=point,previous=point,start=point,end=new Point(-53,0),kind=shot?"shot":"none",elapsed=shot?-.18f:0,duration=shot?.4f:0}};
        }
        static KeeperReadinessSample At(float distance,bool opponent=false,bool shot=false)
        {Actor keeper;return KeeperReadiness.Evaluate(State(distance,opponent,shot,out keeper),keeper,1);}
        [Test] public void PossessionProximityAndPreparationAreDistinctWithoutChangingState()
        {
            var own=At(83);var near=At(8,true);var shot=At(8,true,true);
            Assert.That(own.active&&near.active&&shot.active,Is.True);
            Assert.That(own.weight,Is.EqualTo(.25f).Within(.00001f));
            Assert.That(near.weight,Is.GreaterThan(own.weight+.4f));
            Assert.That(shot.weight,Is.GreaterThan(near.weight+.2f));Assert.That(shot.weight,Is.LessThanOrEqualTo(1));
            Actor actor;var state=State(8,true,true,out actor);var before=state.ball.position;int sequence=actor.actionSequence;float clock=state.clock;
            KeeperReadiness.Evaluate(state,actor,.4f);
            Assert.That(state.ball.position.x,Is.EqualTo(before.x));Assert.That(state.ball.position.z,Is.EqualTo(before.z));
            Assert.That(state.clock,Is.EqualTo(clock));Assert.That(actor.actionSequence,Is.EqualTo(sequence));
        }
        [Test] public void OpponentApproachIncreasesContinuouslyAndOurPossessionStaysAlert()
        {
            float previous=At(40,true).weight;
            for(int distance=39;distance>=2;distance--){float current=At(distance,true).weight;Assert.That(current,Is.GreaterThanOrEqualTo(previous));Assert.That(current-previous,Is.LessThan(.04));previous=current;}
            Assert.That(At(2).weight,Is.EqualTo(At(83).weight));
        }
        [Test] public void ShotDirectedAwayDoesNotAddStrikeReadiness()
        {Actor actor;var state=State(8,true,true,out actor);float owned=At(8,true).weight;state.ball.end=new Point(52,0);Assert.That(KeeperReadiness.Evaluate(state,actor,1).weight,Is.EqualTo(owned).Within(.00001));}
        [TestCase("dive")][TestCase("claim")][TestCase("keeper-hold")][TestCase("keeper-rise")][TestCase("keeper-roll")][TestCase("keeper-throw")][TestCase("control")][TestCase("kick")]
        public void SpecialisedActionsDoNotUseThreatLayer(string action)
        {Actor actor;var state=State(8,true,true,out actor);actor.action=action;Assert.That(KeeperReadiness.Evaluate(state,actor,1).active,Is.False);}
        [Test] public void ClaimCarryRestartUnavailableAndMalformedGuards()
        {
            Actor actor;var state=State(8,true,true,out actor);
            Assert.That(KeeperReadiness.Evaluate(state,actor,1,true).active,Is.False);
            state.ball.owner=actor.id;Assert.That(KeeperReadiness.Evaluate(state,actor,1).active,Is.False);state.ball.owner=null;
            actor.slot=1;Assert.That(KeeperReadiness.Evaluate(state,actor,1).active,Is.False);actor.slot=0;
            actor.controlTime=.1f;Assert.That(KeeperReadiness.Evaluate(state,actor,1).active,Is.False);actor.controlTime=0;
            state.restart=.1f;Assert.That(KeeperReadiness.Evaluate(state,actor,1).active,Is.False);state.restart=0;
            state.halfTime=true;Assert.That(KeeperReadiness.Evaluate(state,actor,1).active,Is.False);state.halfTime=false;
            state.finished=true;Assert.That(KeeperReadiness.Evaluate(state,actor,1).active,Is.False);state.finished=false;
            actor.sentOff=true;Assert.That(KeeperReadiness.Evaluate(state,actor,1).active,Is.False);actor.sentOff=false;
            state.ball.position=new Point(float.NaN,0);Assert.That(KeeperReadiness.Evaluate(state,actor,1).active,Is.False);
            Assert.That(KeeperReadiness.Evaluate(null,actor,1).active,Is.False);Assert.That(KeeperReadiness.Evaluate(state,null,1).active,Is.False);
        }
        static KeeperReadinessSample Sample(float value)=>new KeeperReadinessSample{active=true,weight=value};
        [TestCase(30,1)][TestCase(60,1)][TestCase(120,1)][TestCase(30,2)][TestCase(60,2)][TestCase(120,2)][TestCase(30,10)][TestCase(60,10)][TestCase(120,10)]
        public void FullElapsedIntervalMatchesAnalyticResponseAtAllCadencesAndSpeeds(int fps,int speed)
        {
            var filter=new KeeperReadinessPresentation();filter.Sample(Sample(.25f),0,true,100,true);
            int frames=fps/2;float elapsed=speed/(float)fps;
            for(int i=1;i<=frames;i++)filter.Sample(Sample(1),elapsed,true,100+(float)Math.Floor(i*elapsed*10+.00001)/10);
            float expected=1-.75f*(float)Math.Exp(-12*(frames*elapsed));Assert.That(filter.Value,Is.EqualTo(expected).Within(.000002));
        }
        [Test] public void PauseRollbackIdentityAndInactiveLayerDoNotLeakPreviousThreat()
        {
            var filter=new KeeperReadinessPresentation();filter.Sample(Sample(.25f),0,true,100,true);float mid=filter.Sample(Sample(1),.02f,true,100.1f);
            Assert.That(filter.Sample(Sample(1),0,true,100.1f),Is.EqualTo(mid));
            Assert.That(filter.Sample(Sample(.25f),0,true,99),Is.EqualTo(.25f));
            Assert.That(filter.Sample(Sample(1),0,true,99,true),Is.EqualTo(1));
            Assert.That(filter.Sample(default,.1f,true,99.1f),Is.EqualTo(1));
            Assert.That(filter.Sample(Sample(.25f),.01f,true,99.2f),Is.InRange(.9f,1f));
        }
        [Test] public void SmoothThreatRiseAndReleaseHaveNoInstantStepAndStayBounded()
        {
            var filter=new KeeperReadinessPresentation();filter.Sample(Sample(.25f),0,true,100,true);
            float rise=filter.Sample(Sample(1),1f/60,true,100);Assert.That(rise,Is.InRange(.25f,.4f));
            float lower=filter.Sample(Sample(.25f),1f/60,true,100);Assert.That(lower,Is.InRange(.25f,rise));
            Assert.That(filter.Sample(Sample(float.NaN),1,true,100),Is.EqualTo(lower));
            Assert.That(filter.Sample(Sample(1),float.NaN,true,100),Is.EqualTo(lower));
        }
        [Test] public void SpecialisedPoseReturnAtZeroDeltaKeepsDisplayedWeightThenBlends()
        {
            var filter=new KeeperReadinessPresentation();filter.Sample(Sample(.25f),0,true,100,true);
            Assert.That(filter.Sample(default,.1f,true,100.1f),Is.EqualTo(1));
            Assert.That(filter.Sample(Sample(.25f),0,true,100.1f),Is.EqualTo(1),"Paused re-entry cannot consume the blend.");
            float next=filter.Sample(Sample(.25f),1f/60,true,100.1f);
            Assert.That(next,Is.GreaterThan(.9f));Assert.That(next,Is.LessThan(1));
            Assert.That(filter.Sample(Sample(.25f),0,true,100.1f),Is.EqualTo(next));
        }
        [TestCase(30,1)][TestCase(60,1)][TestCase(120,1)][TestCase(30,2)][TestCase(60,2)][TestCase(120,2)][TestCase(30,10)][TestCase(60,10)][TestCase(120,10)]
        public void ReentryAfterClaimOrControlFollowsEntireElapsedInterval(int fps,int speed)
        {
            var filter=new KeeperReadinessPresentation();filter.Sample(Sample(.25f),0,true,100,true);
            // Evaluate excludes both claim anticipation and controlTime;
            // their resulting inactive samples all use this same contract.
            for(int i=1;i<=fps;i++)filter.Sample(default,speed/(float)fps,true,100+i*speed/(float)fps);
            int frames=fps/2;float elapsed=speed/(float)fps,clock=100+speed;
            for(int i=1;i<=frames;i++)filter.Sample(Sample(.25f),elapsed,true,clock+i*elapsed);
            float expected=.25f+.75f*(float)Math.Exp(-7*frames*elapsed);
            Assert.That(filter.Value,Is.EqualTo(expected).Within(.000002));
        }
        [Test] public void ExplicitIdentityResetAndClockRollbackMaySnapAfterSpecialisation()
        {
            var filter=new KeeperReadinessPresentation();filter.Sample(Sample(.25f),0,true,100,true);filter.Sample(default,.1f,true,100.1f);
            Assert.That(filter.Sample(Sample(.25f),0,true,100.1f,true),Is.EqualTo(.25f));
            filter.Sample(default,.1f,true,100.2f);
            Assert.That(filter.Sample(Sample(.25f),0,true,90),Is.EqualTo(.25f));
        }
        [Test] public void RepeatedSpecialisedSamplesKeepOriginalPoseAndResetBlendOrigin()
        {
            var filter=new KeeperReadinessPresentation();filter.Sample(Sample(.25f),0,true,100,true);
            for(int i=1;i<=20;i++)Assert.That(filter.Sample(default,.1f,true,100+i*.1f),Is.EqualTo(1));
            float first=filter.Sample(Sample(.25f),1f/60,true,102);
            Assert.That(first,Is.EqualTo(.25f+.75f*(float)Math.Exp(-7f/60)).Within(.000001));
            filter.Sample(default,.1f,true,102.1f);
            float second=filter.Sample(Sample(.25f),1f/60,true,102.1f);
            Assert.That(second,Is.EqualTo(first).Within(.000001));
        }
    }
}

using NUnit.Framework;
using Touchline.Core;
using Touchline.Analysis;

namespace Touchline.Tests
{
    public class ThroughPassObserverTests
    {
        static MatchState Flight()=>new MatchState{phase="play",restart=0,actors=new[]{new Actor{id="ours",side=0},new Actor{id="theirs",side=1}},ball=new BallState{kind="through",from="theirs",side=1}};
        static ThroughPassObserver Start(MatchState m){var o=new ThroughPassObserver();m.events.Add(new MatchEvent{kind="through",side=1,player="theirs"});o.Sample(m);return o;}
        [TestCase("offside",1,1)] [TestCase("interception",0,3)] [TestCase("miscontrol",1,4)]
        public void FirstTouchEventsAreAttributedExactlyOnce(string kind,int side,int outcome)
        {
            var m=Flight();var o=Start(m);m.events.Add(new MatchEvent{kind=kind,side=side});m.ball.kind="loose";
            o.Sample(m);o.Sample(m);Assert.AreEqual(1,o.Counts[1,outcome]);Assert.AreEqual(1,o.Attempts[1]);
        }
        [Test] public void CompletedPassWinsOverTheNextActionInTheSameTick()
        {
            var m=Flight();var o=Start(m);m.completedPasses[1]++;m.ball.kind="shot";m.events.Add(new MatchEvent{kind="shot",side=1});
            o.Sample(m);Assert.AreEqual(1,o.Counts[1,0]);
        }
        [Test] public void OpponentPossessionDiffersFromADeflection()
        {
            var m=Flight();var o=Start(m);m.ball.owner="ours";m.ball.kind="none";m.events.Add(new MatchEvent{kind="interception",side=0});
            o.Sample(m);Assert.AreEqual(1,o.Counts[1,2]);Assert.AreEqual(0,o.Counts[1,3]);
        }
        [Test] public void ALooseBallIsNotInventedAsACompletedPass()
        {
            var m=Flight();var o=Start(m);m.ball.kind="loose";o.Sample(m);Assert.AreEqual(1,o.Counts[1,6]);Assert.AreEqual(0,o.Counts[1,0]);
        }
        [TestCase("throw-in",5)] [TestCase("goal-kick",5)] [TestCase("free-kick",7)]
        public void RestartsRemainDistinctFromSuccessfulInterceptions(string phase,int outcome)
        {
            var m=Flight();var o=Start(m);m.ball.kind="none";m.phase=phase;m.restart=1;o.Sample(m);Assert.AreEqual(1,o.Counts[1,outcome]);
        }
        [Test] public void HalfTimeClosesAnUnfinishedFlightWithoutInventingAnOutcome()
        {
            var m=Flight();var o=Start(m);m.halfTime=true;o.Sample(m);Assert.AreEqual(1,o.Counts[1,8]);Assert.AreEqual("through",m.ball.kind);Assert.AreEqual(0,m.completedPasses[1]);
        }
    }
}

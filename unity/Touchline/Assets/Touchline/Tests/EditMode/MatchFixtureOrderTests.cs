using NUnit.Framework;
using Touchline.Core;

namespace Touchline.Tests
{
    public class MatchFixtureOrderTests
    {
        Career career;MatchState match;Fixture fixture;
        [SetUp] public void Setup(){match=new MatchState{home="manager",away="opponent",score=new[]{2,5}};fixture=new Fixture{id="fixture",home="opponent",away="manager",day=40};career=new Career{club="manager",match=match,life=new ClubLife{day=40},world=new CareerWorld{activeFixture="fixture"}};career.world.fixtures.Add(fixture);}
        [Test] public void AwayManagerDisplaysActualHomeFirstWithAsymmetricScore(){var order=MatchFixtureOrder.From(career,match);Assert.AreEqual("opponent",order.HomeClub);Assert.AreEqual("manager",order.AwayClub);Assert.AreEqual("5 – 2",order.Score(match));Assert.AreEqual(1,order.HomeSide);Assert.AreEqual(0,order.AwaySide);Assert.AreEqual(1,order.SideAt(0));Assert.AreEqual(0,order.SideAt(1));CollectionAssert.AreEqual(new[]{2,5},match.score);Assert.AreEqual("manager",match.home);}
        [TestCase(0,"0 – 1","moment-away")][TestCase(1,"1 – 0","moment-home")] public void GoalByEitherSimulationSideKeepsCorrectScoreAndColour(int scorer,string expected,string colour){match.score=new[]{0,0};match.score[scorer]=1;var order=MatchFixtureOrder.From(career,match);Assert.AreEqual(expected,order.Score(match));Assert.AreEqual(colour,order.MomentClass(scorer));}
        [Test] public void HomeManagerKeepsSimulationOrder(){fixture.home="manager";fixture.away="opponent";var order=MatchFixtureOrder.From(career,match);Assert.AreEqual(0,order.HomeSide);Assert.AreEqual("2 – 5",order.Score(match));}
        [Test] public void UnknownActiveFixtureDoesNotBorrowAnotherFixture(){career.world.activeFixture="missing";Assert.AreEqual(0,MatchFixtureOrder.From(career,match).HomeSide);}
        [Test] public void ActiveFixtureWithDifferentTeamsFallsBack(){fixture.home="other";Assert.AreEqual(0,MatchFixtureOrder.From(career,match).HomeSide);}
        [Test] public void MatchWithoutWorldUsesItsExistingOrder(){career.world=null;Assert.AreEqual("manager",MatchFixtureOrder.From(career,match).HomeClub);}
        [Test] public void ClearedActiveFixtureOnRecordedFinishedMatchUsesUniqueMatchingRecord(){career.world.activeFixture=null;fixture.played=true;fixture.hg=5;fixture.ag=2;match.finished=true;Assert.AreEqual(1,MatchFixtureOrder.From(career,match).HomeSide);}
        [Test] public void AmbiguousRecordedResultsDoNotInventHomeOrder(){career.world.activeFixture=null;fixture.played=true;fixture.hg=5;fixture.ag=2;match.finished=true;career.world.fixtures.Add(new Fixture{id="other-result",home="manager",away="opponent",day=40,played=true,hg=2,ag=5});Assert.AreEqual(0,MatchFixtureOrder.From(career,match).HomeSide);}
        [Test] public void ArchivedDuplicateDoesNotMakeExactResultAmbiguous(){career.world.activeFixture=null;fixture.played=true;fixture.hg=5;fixture.ag=2;match.finished=true;career.world.history.Add(fixture);Assert.AreEqual(1,MatchFixtureOrder.From(career,match).HomeSide);}
    }
}

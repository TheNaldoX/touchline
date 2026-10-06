using System;
using System.Linq;
using NUnit.Framework;
using Touchline.Core;

namespace Touchline.Tests
{
    public class PrematchBriefingTests
    {
        Database db;Career career;Fixture fixture;
        [SetUp] public void Setup()
        {
            db=new Database{clubs=new[]{new ClubData{id="a",stadium="Stade A"},new ClubData{id="b",stadium="Stade B"}}};
            career=new Career{club="b",world=new CareerWorld()};
            career.world.divisions.Add(new Division{id="league",clubs={"a","b"}});
            fixture=new Fixture{id="today",league="league",home="a",away="b",day=50};career.world.fixtures.Add(fixture);
        }
        [Test] public void AwayManagerDoesNotReverseTheTwoStartingElevens()
        {
            var match=new MatchState{home="b",away="a",actors=new[]{new Actor{id="mine",side=0,slot=0},new Actor{id="theirs",side=1,slot=0}}};
            Assert.AreEqual("theirs",PrematchBriefing.Starters(match,fixture.home).Single().id);
            Assert.AreEqual("mine",PrematchBriefing.Starters(match,fixture.away).Single().id);
            Assert.Throws<ArgumentException>(()=>PrematchBriefing.Side(match,"other"));
        }
        [Test] public void ExistingResultsRemainInTheBriefingWithoutMutation()
        {
            career.world.fixtures.Add(new Fixture{id="old",league="league",home="a",away="b",played=true,hg=2,ag=0,day=20});
            var before=career.world.fixtures.Count;var result=PrematchBriefing.Create(career,db,fixture);
            Assert.IsTrue(result.HasResults);Assert.AreEqual("a",result.table[0].club);Assert.AreEqual(3,result.table[0].points);
            Assert.AreEqual(1,result.table[0].played);Assert.AreEqual(before,career.world.fixtures.Count);Assert.IsFalse(fixture.played);
        }
        [Test] public void SeasonOpeningDoesNotClaimAnArbitraryRanking()
        {
            var result=PrematchBriefing.Create(career,db,fixture);Assert.IsFalse(result.HasResults);StringAssert.Contains("pas encore établi",result.context);
        }
        [Test] public void FriendlyDoesNotPretendToAwardLeaguePoints()
        {
            fixture.league="friendly";var result=PrematchBriefing.Create(career,db,fixture);Assert.IsEmpty(result.table);StringAssert.Contains("Aucun point",result.context);
        }
        [Test] public void ReturnLegAggregateUsesTodaysHomeAwayOrderAndOnlyItsTie()
        {
            fixture.knockout=true;fixture.leg=2;fixture.tie="tie1";
            career.world.fixtures.Add(new Fixture{id="first",league="league",home="b",away="a",played=true,hg=3,ag=1,day=30,tie="tie1",knockout=true});
            career.world.fixtures.Add(new Fixture{id="other",league="league",home="a",away="b",played=true,hg=9,ag=0,day=30,tie="tie2",knockout=true});
            var result=PrematchBriefing.Create(career,db,fixture);Assert.IsEmpty(result.table);StringAssert.Contains("1 – 3",result.context);
        }
        [Test] public void MissingFirstLegIsNotDisplayedAsZeroZero()
        {
            fixture.knockout=true;fixture.leg=2;fixture.tie="missing";StringAssert.Contains("non disponible",PrematchBriefing.Create(career,db,fixture).context);
        }
        [Test] public void FormUsesClubPerspectiveExcludesFutureAndDeduplicatesHistory()
        {
            var old=new Fixture{id="old",home="a",away="b",played=true,hg=0,ag=2,day=20};career.world.history.Add(old);career.world.fixtures.Add(old);
            career.world.fixtures.Add(new Fixture{id="future",home="a",away="b",played=true,hg=9,ag=0,day=90});
            career.world.fixtures.Add(new Fixture{id="unplayed",home="a",away="b",day=30});
            Assert.AreEqual("V",PrematchBriefing.Form(career,fixture,"b"));Assert.AreEqual("D",PrematchBriefing.Form(career,fixture,"a"));
        }
        [Test] public void FormKeepsTheFiveMostRecentResultsInChronologicalOrder()
        {
            for(int i=0;i<8;i++)career.world.fixtures.Add(new Fixture{id="old"+i,home="a",away="b",played=true,hg=i<4?2:0,ag=1,day=i});
            Assert.AreEqual("V  D  D  D  D",PrematchBriefing.Form(career,fixture,"a"));
        }
        [Test] public void VenueUsesKnownDataAndNeverInventsANeutralVenue()
        {
            Assert.AreEqual("Stade A",PrematchBriefing.Create(career,db,fixture).venue);
            fixture.neutral=true;StringAssert.Contains("non renseigné",PrematchBriefing.Create(career,db,fixture).venue);
            fixture.venue="Stade de la finale";Assert.AreEqual(fixture.venue,PrematchBriefing.Create(career,db,fixture).venue);
        }
    }
}

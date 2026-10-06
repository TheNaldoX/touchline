using System;
using NUnit.Framework;
using Touchline.Core;

namespace Touchline.Tests
{
    public class PrematchProgrammeTests
    {
        Career career;Fixture today;
        [SetUp] public void Setup(){career=new Career{world=new CareerWorld()};today=new Fixture{id="today",home="a",away="b",day=50};}
        Fixture Result(string id,int day,string home="a",string away="b")=>new Fixture{id=id,day=day,home=home,away=away,played=true,hg=2,ag=1};
        [Test] public void ResultsExcludeUnplayedFutureAndCurrentAndUseLatestDuplicate()
        {
            career.world.history.Add(Result("duplicate",10));var corrected=Result("duplicate",10);corrected.hg=3;career.world.fixtures.Add(corrected);
            career.world.fixtures.Add(Result("future",51));career.world.fixtures.Add(new Fixture{id="unplayed",day=30,home="a",away="b"});career.world.fixtures.Add(Result("today",50));career.world.fixtures.Add(Result("unrelated",49,"c","d"));
            var results=PrematchProgramme.RecentResults(career,today,"a");Assert.AreEqual(1,results.Length);Assert.AreEqual(3,results[0].hg);Assert.AreEqual(5,career.world.fixtures.Count);
        }
        [Test] public void RecentResultsAreBoundedAndNewestFirstAcrossHomeAndAway()
        {
            for(int i=1;i<6;i++)career.world.fixtures.Add(Result("match"+i,i,i%2==0?"a":"b",i%2==0?"b":"a"));
            var result=PrematchProgramme.RecentResults(career,today,"a");Assert.AreEqual(3,result.Length);Assert.AreEqual("match5",result[0].id);Assert.AreEqual("match3",result[2].id);
        }
        [Test] public void LastMeetingIgnoresMoreRecentOtherOpponentsAndKeepsRecordedScoreOrder()
        {
            career.world.history.Add(Result("meeting",20,"b","a"));career.world.fixtures.Add(Result("other",40,"a","c"));
            var result=PrematchProgramme.LastMeeting(career,today);Assert.AreEqual("meeting",result.id);Assert.AreEqual("b",result.home);Assert.AreEqual(2,result.hg);
        }
        [Test] public void EmptyProgrammeNeverInventsResults(){Assert.IsEmpty(PrematchProgramme.RecentResults(career,today,"a"));Assert.IsNull(PrematchProgramme.LastMeeting(career,today));Assert.IsEmpty(PrematchProgramme.RecentResults(null,today,"a"));Assert.IsEmpty(PrematchProgramme.RecentResults(career,null,"a"));}
        [TestCase(false)][TestCase(true)] public void AwayManagerGetsActualClubTacticAndPreservesFlanks(bool withBall)
        {
            var match=new MatchState{home="b",away="a",homeTactic=new Tactic(),awayTactic=new Tactic()};
            match.homeTactic.withBall[1].x=24;match.awayTactic.withBall[1].x=7;match.awayTactic.withBall[1].y=76;match.awayTactic.withBall[1].duty="attack";
            var original=(withBall?match.awayTactic.withBall:match.awayTactic.withoutBall)[1];var result=PrematchProgramme.Position(match,"a",1,withBall);
            Assert.AreEqual(original.x,result.x);Assert.AreEqual(original.y,result.y);Assert.AreEqual(original.duty,result.duty);Assert.Less(result.x,50);Assert.Greater(PrematchProgramme.Position(match,"a",4,withBall).x,50);
            result.x=88;result.duty="defend";Assert.AreNotEqual(88,original.x);if(withBall)Assert.AreEqual("attack",original.duty);
        }
        [Test] public void MissingPositionDoesNotInventAFormation(){var match=new MatchState{home="a",away="b",homeTactic=new Tactic()};Assert.IsNull(PrematchProgramme.Position(match,"a",30,false));Assert.Throws<ArgumentException>(()=>PrematchProgramme.Position(match,"unknown",1,false));}
        [Test] public void InstructionsUseTheActualOppositionSettingsForEachPhase()
        {
            var match=new MatchState{home="b",away="a",homeTactic=new Tactic(),awayTactic=new Tactic()};match.awayTactic.tempo=1;match.awayTactic.directness=0;match.awayTactic.workIntoBox=true;match.awayTactic.line=0;match.awayTactic.pressing=1;
            StringAssert.Contains("très rapide",PrematchProgramme.Instructions(match,"a",true));StringAssert.Contains("très courtes",PrematchProgramme.Instructions(match,"a",true));StringAssert.Contains("Construire",PrematchProgramme.Instructions(match,"a",true));StringAssert.Contains("très basse",PrematchProgramme.Instructions(match,"a",false));StringAssert.Contains("très intense",PrematchProgramme.Instructions(match,"a",false));StringAssert.DoesNotContain("Construire",PrematchProgramme.Instructions(match,"a",false));
        }
        [Test] public void UnknownFootIsExplicitInsteadOfDefaultRight(){Assert.AreEqual("Non renseigné",PrematchProgramme.PreferredFoot(new PlayerData()));Assert.AreEqual("Gauche",PrematchProgramme.PreferredFoot(new PlayerData{preferredFoot="Left"}));}
    }
}

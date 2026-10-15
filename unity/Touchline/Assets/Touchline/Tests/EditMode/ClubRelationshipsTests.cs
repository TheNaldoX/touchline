using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Touchline.Core;

namespace Touchline.Tests
{
    public sealed class ClubRelationshipsTests
    {
        Database db;Career c;
        [SetUp] public void Setup()
        {
            db=new Database{leagues=new[]{new LeagueData{id="test",country="France",name="Test"}},
                clubs=Enumerable.Range(0,4).Select(i=>new ClubData{id="c"+i,name="Club "+i,league="test",playable=true,annualRevenue=20000000}).ToArray(),
                players=Enumerable.Range(0,96).Select(i=>new PlayerData{id="p"+i,name="Joueur "+i,team="c"+i/24,age=25,position=i%24<2?"GB":"MIL",positions=new[]{i%24<2?"GK":"CM"},rating=65,potential=75,wage=500,value=100000,fitness=100,morale=75}).ToArray()};
            c=new Career{club="c0"};c.lineup=Career.Select(db,c.club,c.tactic);c.EnsureWorld(db);
        }
        void Concern()
        {
            var contract=c.Contract(db,"p4");contract.role="starter";contract.playingTime=new PlayingTimeUsage{club=c.club};
            for(int i=0;i<6;i++)contract.playingTime.Add(false,0);
            c.Person("p4").morale=30;c.Person("p4").trust=30;
        }
        void Request(){Concern();c.ReviewDepartureRequests(db);c.life.day+=28;c.ReviewDepartureRequests(db);}
        [Test] public void DepartureNeedsSustainedDiscontentAndSendsOneActionableMessage()
        {
            Concern();c.ReviewDepartureRequests(db);Assert.AreEqual("watching",c.DepartureFor("p4").status);
            c.life.day+=27;c.ReviewDepartureRequests(db);Assert.AreEqual("watching",c.DepartureFor("p4").status);
            c.life.day++;c.ReviewDepartureRequests(db);c.ReviewDepartureRequests(db);
            Assert.AreEqual("requested",c.DepartureFor("p4").status);
            var messages=c.life.messages.Where(m=>m.subject=="Je souhaite partir").ToArray();Assert.AreEqual(1,messages.Length);Assert.IsTrue(c.MessageNeedsDecision(messages[0]));
        }
        [Test] public void InjuryOrYouthRoleDoesNotTriggerDeparture()
        {
            Concern();c.Person("p4").restUntil=c.life.day+60;c.ReviewDepartureRequests(db);c.life.day+=28;c.ReviewDepartureRequests(db);Assert.IsNull(c.DepartureFor("p4"));
            c.Person("p4").restUntil=-1;c.Contract(db,"p4").role="youth";c.ReviewDepartureRequests(db);Assert.IsNull(c.DepartureFor("p4"));
        }
        [Test] public void RefusalHasOneConsequenceAndConcreteRecoveryWithdrawsRequest()
        {
            Request();float trust=c.Person("p4").trust;c.AnswerDeparture(db,"p4",false);Assert.AreEqual(trust-5,c.Person("p4").trust);
            Assert.Throws<InvalidOperationException>(()=>c.AnswerDeparture(db,"p4",false));
            c.Person("p4").morale=60;c.Person("p4").trust=60;var usage=c.Contract(db,"p4").playingTime;for(int i=0;i<12;i++)usage.Add(true,90);
            c.life.day++;c.ReviewDepartureRequests(db);Assert.IsNull(c.DepartureFor("p4"));Assert.AreEqual("withdrawn",c.departureRequests.Last().status);
        }
        [Test] public void AllowingDepartureCreatesOnlyAnOfferAndNoAutomaticSale()
        {
            foreach(var player in db.Squad("c1"))player.rating=60; // A genuine upgrade for the buyer, rather than an automatic bid.
            Request();long cash=c.life.cash;c.AnswerDeparture(db,"p4",true);
            Assert.AreEqual("listed",c.DepartureFor("p4").status);Assert.AreEqual("c0",db.Find("p4").team);Assert.AreEqual(cash,c.life.cash);
            Assert.IsTrue(c.world.offers.Any(o=>o.player=="p4"&&o.status=="sale"));
        }
        [Test] public void RefusedPlayerReturnsAfterFourWeeksOfUnresolvedConcernOnlyOnce()
        {
            Request();c.AnswerDeparture(db,"p4",false);int answered=c.life.day;
            c.life.day=answered+27;c.ReviewDepartureRequests(db);Assert.AreEqual("refused",c.DepartureFor("p4").status);
            c.life.day++;c.ReviewDepartureRequests(db);c.ReviewDepartureRequests(db);
            Assert.AreEqual("requested",c.DepartureFor("p4").status);
            var reminders=c.life.messages.Where(m=>m.subject=="Ma situation n’a pas changé").ToArray();
            Assert.AreEqual(1,reminders.Length);Assert.IsTrue(c.MessageNeedsDecision(reminders[0]));
            Assert.IsFalse(c.MessageNeedsDecision(c.life.messages.First(m=>m.subject=="Je souhaite partir")));
            c.life.day++;c.ReviewDepartureRequests(db);Assert.AreEqual(1,c.life.messages.Count(m=>m.subject=="Ma situation n’a pas changé"));
            c.AnswerDeparture(db,"p4",false);Assert.AreEqual("refused",c.DepartureFor("p4").status);
        }
        [Test] public void InjuredOrReconciledPlayerDoesNotRepeatDepartureRequest()
        {
            Request();c.AnswerDeparture(db,"p4",false);c.Person("p4").restUntil=c.life.day+60;
            c.life.day+=28;c.ReviewDepartureRequests(db);Assert.AreEqual("refused",c.DepartureFor("p4").status);
            c.Person("p4").morale=60;c.Person("p4").trust=60;
            for(int i=0;i<12;i++)c.Contract(db,"p4").playingTime.Add(true,90);
            c.life.day++;c.ReviewDepartureRequests(db);Assert.IsNull(c.DepartureFor("p4"));
            Assert.IsFalse(c.life.messages.Any(m=>m.subject=="Ma situation n’a pas changé"));
        }
        [Test] public void InjuryResetsTheFollowupClockAndSavePreservesIt()
        {
            Request();c.AnswerDeparture(db,"p4",false);c.Person("p4").restUntil=c.life.day+60;
            c.life.day+=28;c.ReviewDepartureRequests(db);
            c=JsonUtility.FromJson<Career>(JsonUtility.ToJson(c));c.Person("p4").restUntil=-1;
            c.life.day++;c.ReviewDepartureRequests(db);Assert.AreEqual("refused",c.DepartureFor("p4").status);
            c.life.day+=27;c.ReviewDepartureRequests(db);Assert.AreEqual("requested",c.DepartureFor("p4").status);
        }
        [Test] public void FailedListingPreservesThePendingRequest()
        {
            Request();db.players=db.players.Where(p=>p.team!="c0"||int.Parse(p.id.Substring(1))<18).ToArray();
            Assert.Throws<InvalidOperationException>(()=>c.AnswerDeparture(db,"p4",true));Assert.AreEqual("requested",c.DepartureFor("p4").status);
        }
        [Test] public void RequestsSurviveSaveAndCannotBeAnsweredFromAnotherClub()
        {
            Request();var restored=JsonUtility.FromJson<Career>(JsonUtility.ToJson(c));Assert.AreEqual("requested",restored.DepartureFor("p4").status);
            c.club="c1";Assert.Throws<InvalidOperationException>(()=>c.AnswerDeparture(db,"p4",true));
            c.departureRequests=null;c.ReviewDepartureRequests(db);Assert.IsNotNull(c.departureRequests);
        }
        Fixture Result(int goals,int conceded)
        {
            var f=c.world.fixtures.First(x=>x.home==c.club||x.away==c.club);f.played=true;f.day=c.life.day;f.hg=f.home==c.club?goals:conceded;f.ag=f.home==c.club?conceded:goals;return f;
        }
        [Test] public void PostMatchConferenceUsesLatestDateAndExpires()
        {
            var recent=Result(0,2);Assert.AreEqual(recent.id,c.Conference("after").fixture.id);Assert.IsTrue(c.Conference("after").question.Contains("défaite"));
            c.life.day+=3;Assert.IsFalse(c.Conference("after").Available);Assert.Throws<InvalidOperationException>(()=>c.Press("after","protect"));
        }
        [Test] public void PressAnswerCannotBeFarmedAndProtectingAfterDefeatRaisesMorale()
        {
            Result(0,2);float morale=c.Person("p4").morale;c.Press("after","protect");Assert.AreEqual(morale+2,c.Person("p4").morale);
            Assert.Throws<InvalidOperationException>(()=>c.Press("after","protect"));Assert.AreEqual(morale+2,c.Person("p4").morale);
        }
        [Test] public void AmbitionAfterDefeatCanBackfireOnDistrustfulPlayers()
        {
            Result(0,2);c.Person("p4").trust=30;float morale=c.Person("p4").morale;float supporters=c.world.supporterTrust;
            c.Press("after","ambition");Assert.AreEqual(morale-2,c.Person("p4").morale);Assert.AreEqual(supporters-1,c.world.supporterTrust);
        }
        [Test] public void PressInjuriesAreCurrentDistinctPlayersAndTranscriptMatchesPreview()
        {
            Result(0,2);
            foreach(var id in new[]{"p1","p2","p3","p3"})c.life.medical.Add(new MedicalCase{player=id,remaining=10});
            var context=c.Conference("after");Assert.AreEqual(3,context.absentees);Assert.AreEqual("injuries",context.groupTopic);
            string preview=Career.PressReply(context,2,"protect");Assert.IsTrue(preview.Contains("santé"));
            c.PressInterview("after",new[]{"calm","calm","protect"});
            Assert.AreEqual(preview,c.world.press.Last().replies[2]);
            var restored=JsonUtility.FromJson<Career>(JsonUtility.ToJson(c));Assert.AreEqual(preview,restored.world.press.Last().replies[2]);
        }
        [Test] public void PressDoesNotCountRecoveredOrFormerPlayersAsInjured()
        {
            Result(1,0);
            c.life.medical.Add(new MedicalCase{player="p1",remaining=0});
            c.life.medical.Add(new MedicalCase{player="p2",remaining=10,closed=1});
            c.life.medical.Add(new MedicalCase{player="p3",remaining=10,responsibilityEndedReason="sold"});
            c.life.medical.Add(new MedicalCase{player="p90",remaining=10});
            Assert.AreEqual(0,c.Conference("after").absentees);
        }
        [Test] public void PressUsesPendingDepartureButNotAnOldRefusal()
        {
            Request();Result(1,0);var context=c.Conference("after");Assert.AreEqual("departure",context.groupTopic);
            Assert.IsTrue(Career.PressQuestion(context,2).Contains("quitter"));
            c.AnswerDeparture(db,"p4",false);Assert.AreNotEqual("departure",c.Conference("after").groupTopic);
        }
        [Test] public void PressCongestionRequiresThreeMatchesForOurClubInSevenDays()
        {
            Result(1,0);c.world.fixtures.Add(new Fixture{id="busy1",home=c.club,away="c1",day=c.life.day+2});
            Assert.AreNotEqual("congestion",c.Conference("after").groupTopic);
            c.world.fixtures.Add(new Fixture{id="busy2",home="c2",away=c.club,day=c.life.day+3});
            Assert.AreEqual("congestion",c.Conference("after").groupTopic);
            Assert.IsTrue(Career.PressReply(c.Conference("after"),2,"calm").Contains("fatigue"));
        }
        [Test] public void InterviewRequiresThreeValidAnswersBeforeAnyEffect()
        {
            Result(0,2);float morale=c.Person("p4").morale;int count=c.world.press.Count;
            Assert.Throws<InvalidOperationException>(()=>c.PressInterview("after",new[]{"protect","calm"}));
            Assert.Throws<InvalidOperationException>(()=>c.PressInterview("after",new[]{"protect","invalid","calm"}));
            Assert.AreEqual(count,c.world.press.Count);Assert.AreEqual(morale,c.Person("p4").morale);
        }
        [Test] public void InterviewAveragesEffectsAndPreservesTranscriptOnSave()
        {
            Result(0,2);float morale=c.Person("p4").morale;c.PressInterview("after",new[]{"protect","protect","protect"});
            Assert.AreEqual(morale+2,c.Person("p4").morale);Assert.AreEqual(1,c.world.press.Count);
            var restored=JsonUtility.FromJson<Career>(JsonUtility.ToJson(c));Assert.AreEqual(3,restored.world.press[0].replies.Length);
            Assert.AreEqual(Career.PressReply("after",1,"protect"),restored.world.press[0].replies[1]);
            Assert.Throws<InvalidOperationException>(()=>c.PressInterview("after",new[]{"protect","protect","protect"}));
        }    }
}

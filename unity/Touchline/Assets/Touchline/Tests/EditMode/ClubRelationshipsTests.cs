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
    }
}

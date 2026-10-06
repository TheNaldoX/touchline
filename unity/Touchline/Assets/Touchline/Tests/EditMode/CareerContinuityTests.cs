using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Touchline.Core;

namespace Touchline.Tests
{
    public partial class ProfessionalTests
    {
        [Test] public void ReturningManagerDatesOrphanMedicalClosureAtActualReturn()
        {
            var episode=c.OpenInjury(db,"p0","muscle");int remaining=episode.remaining;
            c.life.day=10;c.approaches.Add(new JobApproach{club="c1",until=24});c.AnswerApproach(db,"c1",true);
            db.Find("p0").team="c2"; // Legacy departure not previously persisted in the old club archive.
            c.life.day=150;c.approaches.Add(new JobApproach{club="c0",until=164});c.AnswerApproach(db,"c0",true);
            Assert.AreEqual("outside-current-follow-up",episode.responsibilityEndedReason);
            Assert.AreEqual(150,episode.responsibilityEndedDay);
            Assert.AreEqual(-1,episode.closed);Assert.AreEqual(remaining,episode.remaining);
            Assert.IsNull(c.Injury("p0"));
        }
        [Test] public void DailyDevelopmentBecomesPermanentExactlyOnceAtSeasonEnd()
        {
            var p=db.Find("p0");p.age=20;p.rating=65;p.potential=80;var life=c.Person(p.id);life.growth=2.5f;
            float attribute=p.attributes[0].value;Annual();
            Assert.AreEqual(67.5f,p.rating);Assert.AreEqual(attribute+2.5f,p.attributes[0].value,.001f);
            Assert.AreEqual(0,life.growth);Assert.AreEqual(0,p.development);Annual();Assert.AreEqual(67.5f,p.rating);
        }
        [Test] public void FormerClubDevelopsUnderAiAndStopsBlamingFormerManager()
        {
            var p=db.Find("p0");p.age=19;p.rating=65;p.potential=80;c.world.managerStatus="dismissed";
            var person=c.Person(p.id);person.morale=20;person.promiseUntil=1;c.life.day=13;c.life.nextFixture=int.MaxValue;
            c.AdvanceDay(db);Assert.Greater(person.morale,20);Assert.AreEqual(0,person.growth);
            Assert.IsFalse(c.life.messages.Any(m=>m.day==14&&(m.subject=="Un moment pour parler ?"||m.subject=="Notre engagement")));
            Annual();Assert.Greater(p.rating,65);
        }
        [Test] public void UnattachedYoungPlayerCannotReceiveAutomaticClubTrainingDevelopment()
        {
            var p=db.Find("p24");p.team="free";p.age=19;p.rating=30;p.potential=80;float initial=p.attributes[0].value;
            Annual();Assert.AreEqual("free",p.team);Assert.AreEqual(30,p.rating);Assert.AreEqual(initial,p.attributes[0].value);
        }
        [Test] public void FreeAcademyGraduateSignsSeniorContractWithProperWage()
        {
            Annual();var reference=c.world.developmentReferences.Single(r=>r.club=="c1");
            var old=db.Find("p24");old.team="free";old.age=40;
            var free=db.Find(c.world.youth[0].player);free.team="free";free.age=21;free.rating=reference.rating;free.potential=reference.rating;free.position="GB";free.positions=new[]{"GK"};free.wage=250;
            c.world.contracts.RemoveAll(x=>x.player==free.id);
            Annual();var contract=c.world.contracts.Single(x=>x.player==free.id);
            Assert.AreEqual("c1",free.team);Assert.AreEqual("c1",contract.club);Assert.Greater(contract.wage,250);Assert.AreEqual(free.wage,contract.wage);
            Assert.Greater(contract.until,c.life.day);Assert.IsTrue(contract.estimated);
        }
        [Test] public void AiSurplusLeavesAtExpiryWithoutImmediateSalaryCut()
        {
            Annual();var reference=c.world.developmentReferences.Single(r=>r.club=="c1");reference.squadSize=22;
            foreach(var p in db.Squad("c1")){var e=c.Contract(db,p.id);e.until=c.life.day+370;}
            Annual();var releases=c.world.contracts.Where(e=>e.club=="c1"&&e.aiRelease).ToArray();
            Assert.AreEqual(2,releases.Length);Assert.AreEqual(24,db.Squad("c1").Count);
            c.life.day=releases.Max(e=>e.until);typeof(Career).GetMethod("ProcessAiEmployment",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(c,new object[]{db});
            Assert.AreEqual(22,db.Squad("c1").Count);Assert.GreaterOrEqual(db.Squad("c1").Count(p=>p.Goalkeeper),2);
        }
    }
}

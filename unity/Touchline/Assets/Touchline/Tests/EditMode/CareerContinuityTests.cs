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
        [Test] public void AcademyReplacementDoesNotConsumeTheWholeAvailableSalaryEnvelope()
        {
            var reference=new ClubDevelopmentReference{club="c1",rating=65,wage=500,value=100000,revenue=21000000,squadSize=24};
            c.world.developmentReferences.Add(reference);
            foreach(var player in db.Squad("c1")){
                var contract=c.Contract(db,player.id);contract.until=c.life.day+730;
                if(player.Goalkeeper){player.age=31;player.rating=55;player.potential=55;contract.until=c.life.day+365;}
            }
            Annual();
            var newcomers=db.Squad("c1").Where(p=>p.salarySource!=null&&p.salarySource.StartsWith("Premier contrat simulé")).ToArray();
            Assert.Greater(newcomers.Length,0,"The fixture must reach replacement after a non-renewal, not a simple vacancy");
            foreach(var player in newcomers){
                Assert.LessOrEqual(player.rating,reference.rating);
                Assert.That(player.wage,Is.InRange(250,reference.wage),"An unproven graduate cannot earn more than the reference senior merely because the club has headroom");
                Assert.AreEqual(player.wage,c.Contract(db,player.id).wage);
            }
        }
        [Test] public void AiSurplusLeavesAtExpiryWithoutImmediateSalaryCut()
        {
            Annual();var reference=c.world.developmentReferences.Single(r=>r.club=="c1");reference.squadSize=22;
            // The first summer also promotes a youngster. Preserve that actual
            // roster and its signed wages until the announced expiries.
            var signedWages=db.Squad("c1").ToDictionary(p=>p.id,p=>p.wage);
            Assert.Greater(signedWages.Count,reference.squadSize);
            // Fix the contract terms for this scenario; a previously scheduled
            // raise must not survive replacing its associated expiry date.
            foreach(var p in db.Squad("c1")){var e=c.Contract(db,p.id);e.until=c.life.day+370;e.nextWage=0;e.wageChangeDay=0;}
            Annual();var releases=c.world.contracts.Where(e=>e.club=="c1"&&e.aiRelease).ToArray();
            Assert.AreEqual(signedWages.Count-reference.squadSize,releases.Length);Assert.AreEqual(signedWages.Count,db.Squad("c1").Count);
            foreach(var signed in signedWages){Assert.AreEqual(signed.Value,db.Find(signed.Key).wage);Assert.AreEqual(signed.Value,c.Contract(db,signed.Key).wage);}
            c.life.day=releases.Max(e=>e.until);typeof(Career).GetMethod("ProcessAiEmployment",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(c,new object[]{db});
            Assert.AreEqual(22,db.Squad("c1").Count);Assert.GreaterOrEqual(db.Squad("c1").Count(p=>p.Goalkeeper),2);
        }
    }
}

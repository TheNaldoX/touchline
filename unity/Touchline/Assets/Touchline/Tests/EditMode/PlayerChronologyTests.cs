using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Touchline.Core;

namespace Touchline.Tests
{
    public class PlayerChronologyTests
    {
        [TestCase("2000-07-21","2026-06-15",25)]
        [TestCase("2000-07-21","2026-07-20",25)]
        [TestCase("2000-07-21","2026-07-21",26)]
        [TestCase("2000-07-21","2036-07-21",36)]
        [TestCase("2000-12-31","2026-12-30",25)]
        [TestCase("2000-12-31","2027-01-01",26)]
        [TestCase("2000-02-29","2027-02-27",26)]
        [TestCase("2000-02-29","2027-02-28",27)]
        [TestCase("2000-02-29","2028-02-28",27)]
        [TestCase("2000-02-29","2028-02-29",28)]
        public void AgeFollowsCalendarBirthday(string birth,string at,int expected)
        {
            Assert.IsTrue(PlayerChronology.TryAge(new PlayerData{birthDate=birth,age=99},DateTime.Parse(at),out int age));Assert.AreEqual(expected,age);
        }
        [TestCase(null)] [TestCase("")] [TestCase("2000")] [TestCase("21/07/2000")] [TestCase("2000-02-30")] [TestCase("2030-01-01")] [TestCase("0020-01-01")]
        public void UnknownInvalidAndFutureDatesDoNotInventAnAge(string birth)
        {
            Assert.IsFalse(PlayerChronology.TryAge(new PlayerData{birthDate=birth,age=25},Career.Epoch,out _));
        }
        [Test] public void ExistingIdentityEvidenceIsUsedWhenThePrimaryDateIsUnavailable()
        {
            var p=new PlayerData{birthDate="invalid",evidence=new PlayerIdentityEvidence{birthDate="2000-07-21"}};
            Assert.IsTrue(PlayerChronology.TryAge(p,Career.Epoch,out int age));Assert.AreEqual(25,age);
        }
        [Test] public void PlayerCopySeparatesMutableFieldsAndRetainsSerializedEvidence()
        {
            var p=new PlayerData{id="p",age=26,birthDate="2000-07-21",evidence=new PlayerIdentityEvidence{birthDate="2000-07-21"},positions=new[]{"ST"},attributes=new[]{new AttributeValue{key="finishing",value=90}},salarySource="Salary estimate",rosterAsOf="2026-10-05",surname="Player",wage=1234};var copy=p.Copy();
            Assert.AreEqual(JsonUtility.ToJson(p),JsonUtility.ToJson(copy));copy.attributes[0].value=1;copy.positions[0]="GK";copy.evidence.birthDate="2001-01-01";
            Assert.AreEqual(90,p.attributes[0].value);Assert.AreEqual("ST",p.positions[0]);Assert.AreEqual("2000-07-21",p.evidence.birthDate);
        }
    }
    public partial class ProfessionalTests
    {
        [Test] public void ContinueUpdatesBirthdayBeforeTrainingAndRepeatedLoadsDoNotAgeAgain()
        {
            var p=db.Find("p0");p.birthDate="2000-06-16";p.age=26;c.EnsureLife(db);Assert.AreEqual(25,p.age);
            c.AdvanceDay(db);Assert.AreEqual(26,p.age);c.EnsureLife(db);c.EnsureLife(db);Assert.AreEqual(26,p.age);
        }
        [Test] public void AnnualTransitionDoesNotAddASecondYearForKnownBirthdays()
        {
            var p=db.Find("p0");p.birthDate="2000-07-21";c.EnsureLife(db);Assert.AreEqual(25,p.age);
            Annual();Assert.AreEqual(26,p.age);c.life.day=(int)(new DateTime(2027,7,21)-Career.Epoch).TotalDays;c.EnsureLife(db);Assert.AreEqual(27,p.age);
        }
        [Test] public void UnknownBirthdaysKeepExistingAnnualGrowthAndGeneratedPlayersAreNotRewritten()
        {
            var p=db.Find("p0");int before=p.age;var y=db.Find(c.world.youth[0].player);int young=y.age;
            c.EnsureLife(db);Assert.AreEqual(before,p.age);Annual();Assert.AreEqual(before+1,p.age);Assert.AreEqual(young+1,y.age);
        }
        [Test] public void RestoringTwoCareerDatesNeverContaminatesTheSharedCatalogue()
        {
            var p=db.Find("p24");p.birthDate="2000-07-21";p.age=26;string original=JsonUtility.ToJson(db);
            var early=JsonUtility.FromJson<Career>(JsonUtility.ToJson(c));Assert.IsTrue(CareerSaveRestore.TryRestore(db,early,out var first));Assert.AreEqual(25,first.Find(p.id).age);
            var later=JsonUtility.FromJson<Career>(JsonUtility.ToJson(c));later.life.day=(int)(new DateTime(2030,7,21)-Career.Epoch).TotalDays;
            Assert.IsTrue(CareerSaveRestore.TryRestore(db,later,out var second));Assert.AreEqual(30,second.Find(p.id).age);Assert.AreEqual(25,first.Find(p.id).age);Assert.AreEqual(original,JsonUtility.ToJson(db));
        }
        [TestCase(false)] [TestCase(true)] public void LegacyRosterChangesRecoverBirthDateWithoutResettingCareerTerms(bool fallback)
        {
            var p=db.Find("p24");p.birthDate=fallback?"invalid":"2000-07-21";if(fallback)p.evidence=new PlayerIdentityEvidence{birthDate="2000-07-21"};
            c.world.rosterChanges.Add(new PlayerData{id=p.id,name=p.name,team=p.team,age=31,rating=81,wage=8765,value=99999});
            Assert.IsTrue(CareerSaveRestore.TryRestore(db,c,out var restored));var saved=restored.Find(p.id);
            Assert.AreEqual(25,saved.age);Assert.AreEqual(81,saved.rating);Assert.AreEqual(8765,saved.wage);Assert.AreEqual(99999,saved.value);Assert.AreEqual("2000-07-21",saved.birthDate);
        }
    }
}

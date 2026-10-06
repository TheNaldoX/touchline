using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Touchline.Core;
namespace Touchline.Tests
{
    public sealed class StaffEmployerSerializationTests
    {
        Database db;Career c;
        [SetUp] public void Setup(){c=StaffTrainingLifecycleTests.Fixture(out db);c.life.reputation=90;}
        void Reload()
        {
            string json=JsonUtility.ToJson(c);string path=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../artifacts/unity/staff-employer-save-unit",Guid.NewGuid().ToString("N")+".json"));
            Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,json);
            var restored=LaunchCareerStorage.Read(path,"Staff employer validation",db);Assert.IsTrue(restored.Valid,restored.Error);Assert.IsNull(restored.State.match);Assert.AreEqual(json,File.ReadAllText(path));
            c=restored.State;db=restored.Restored;c.EnsureStaffMarket(db);
        }
        void Tick(){c.life.day+=2;typeof(Career).GetMethod("StaffMarketDay",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(c,new object[]{db});}
        string PrepareFreeOffer()
        {
            string id=c.staffMarket.First(s=>s.club==null&&s.role=="assistant").id;c.FireStaff(db,c.Staff("assistant").id);var person=c.staffMarket.Single(s=>s.id==id);
            c.ProposeStaffContract(db,id,Career.MonthlySalary(person.wage*2),2);return id;
        }
        [Test] public void FiredIdentityReloadsAsGenuinelyRecruitableWithoutResurrection()
        {
            string id=c.Staff("assistant").id;c.FireStaff(db,id);Reload();var person=c.staffMarket.Single(s=>s.id==id);
            Assert.IsNull(person.club);Assert.AreEqual(0,person.until);Assert.AreEqual(0,c.Staff("assistant").wage);StringAssert.Contains("libre",c.StaffAgent(id).message);Assert.AreEqual(0,c.StaffAgent(id).feeLow);
            c.ProposeStaffContract(db,id,Career.MonthlySalary(person.wage*2),2);Tick();Assert.AreEqual("accepted",c.staffOffers.Single().status);c.SignStaffContract(db,id);Assert.AreEqual(c.club,person.club);Assert.AreEqual(id,c.Staff("assistant").id);
        }
        [Test] public void PendingFreeOfferSurvivesRealLoaderAndAcceptsWithZeroCompensation()
        {
            string id=PrepareFreeOffer();Reload();var offer=c.staffOffers.Single();Assert.IsNull(offer.employer);Assert.AreEqual("pending",offer.status);Assert.AreEqual(0,offer.compensation);
            Tick();Assert.AreEqual("accepted",offer.status);long before=c.life.cash;c.SignStaffContract(db,id);Assert.AreEqual(offer.wage*2,before-c.life.cash);Assert.AreEqual("signed",offer.status);Assert.AreEqual(id,c.Staff("assistant").id);
        }
        [Test] public void AcceptedFreeOfferCanBeSignedImmediatelyAfterReload()
        {
            string id=PrepareFreeOffer();Tick();Assert.AreEqual("accepted",c.staffOffers.Single().status);Reload();var offer=c.staffOffers.Single();long before=c.life.cash;c.SignStaffContract(db,id);Assert.AreEqual(offer.wage*2,before-c.life.cash);Assert.AreEqual("signed",offer.status);
        }
        [Test] public void ExplicitLegacyEmptyEmployersAreNormalizedBeforeOfferResolution()
        {
            string id=PrepareFreeOffer();c.staffMarket.Single(s=>s.id==id).club="";c.staffOffers.Single().employer="";c.EnsureStaffMarket(db);Assert.IsNull(c.staffMarket.Single(s=>s.id==id).club);Assert.IsNull(c.staffOffers.Single().employer);Tick();Assert.AreEqual("accepted",c.staffOffers.Single().status);c.SignStaffContract(db,id);Assert.AreEqual(id,c.Staff("assistant").id);
        }
        [Test] public void NonemptyEmployersAndOfferStatusesAndTermsRemainUnchanged()
        {
            var person=c.staffMarket.First(s=>s.club=="c1");int until=person.until;long wage=person.wage;
            var offer=new StaffOffer{staff=person.id,club=c.club,employer="c1",status="counter",wage=999,compensation=12345,years=3,due=22};c.staffOffers.Add(offer);
            Reload();var saved=c.staffMarket.Single(s=>s.id==person.id);var savedOffer=c.staffOffers.Single();Assert.AreEqual("c1",saved.club);Assert.AreEqual(until,saved.until);Assert.AreEqual(wage,saved.wage);Assert.AreEqual("c1",savedOffer.employer);Assert.AreEqual(c.club,savedOffer.club);Assert.AreEqual("counter",savedOffer.status);Assert.AreEqual(999,savedOffer.wage);Assert.AreEqual(12345,savedOffer.compensation);Assert.AreEqual(3,savedOffer.years);Assert.AreEqual(22,savedOffer.due);
        }
    }
}

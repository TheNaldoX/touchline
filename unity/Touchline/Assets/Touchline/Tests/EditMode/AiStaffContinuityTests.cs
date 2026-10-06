using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Touchline.Core;
using UnityEngine;

namespace Touchline.Tests
{
    public sealed class AiStaffContinuityTests
    {
        Career c;Database db;
        static object Call(Career career,string method,params object[] args)=>typeof(Career).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(career,args);
        [SetUp] public void Setup()
        {
            db=new Database{leagues=new[]{new LeagueData{id="fra.1",name="L1"}},clubs=Enumerable.Range(0,4).Select(i=>new ClubData{id="c"+i,name="Club "+i,league="fra.1",playable=true,annualRevenue=20000000+i*1000000}).ToArray(),players=Enumerable.Range(0,96).Select(i=>new PlayerData{id="p"+i,name="Test "+i,team="c"+(i/24),age=24,position=i%24==0?"GB":"MIL",positions=new[]{i%24==0?"GK":"CM"},rating=65,potential=80,value=100000,wage=500,fitness=100,morale=75}).ToArray()};
            c=new Career{club="c0"};c.lineup=Career.Select(db,c.club,c.tactic);c.EnsureWorld(db);
        }
        void Tick(int day){c.life.day=day;Call(c,"StaffMarketDay",db);}
        void Vacate(string club,string role)
        {
            c.staffMarket.RemoveAll(s=>s.club==club&&s.role==role);
        }
        [Test] public void NpcRenewsAtExpiryAtTheSameSalaryWhileOurStaffExpires()
        {
            var member=c.staffMarket.First(s=>s.club=="c1");long wage=member.wage;int until=member.until;Tick(until);Assert.AreEqual("c1",member.club);Assert.AreEqual(wage,member.wage);Assert.AreEqual(until+730,member.until);Assert.AreEqual(0,c.staffMarket.Count(s=>s.club==c.club));Assert.AreEqual(0,c.life.staff.members.Count);
        }
        [Test] public void UnexpiredContractIsNotRewrittenEarly()
        {
            var member=c.staffMarket.First(s=>s.club=="c1");string before=JsonUtility.ToJson(member);Tick(100);Assert.AreEqual(before,JsonUtility.ToJson(member));
        }
        [Test] public void VacancyIsFilledByFreeStaffWithOneRecordedSigningCost()
        {
            Vacate("c1","scout");var account=c.world.aiAccounts.Single(a=>a.club=="c1");long cash=account.cash;Tick(1);var hired=c.staffMarket.Single(s=>s.club=="c1"&&s.role=="scout");Assert.AreEqual(731,hired.until);Assert.AreEqual(cash-hired.wage*2,account.cash);Assert.AreEqual(1,account.ledger.Count(e=>e.label.StartsWith("Signature staff IA")));Tick(1);Assert.AreEqual(cash-hired.wage*2,account.cash);
        }
        [Test] public void EmptyTreasuryCannotFundReplacementOrCreateDebt()
        {
            Vacate("c1","scout");var account=c.world.aiAccounts.Single(a=>a.club=="c1");account.cash=0;Tick(1);Assert.IsFalse(c.staffMarket.Any(s=>s.club=="c1"&&s.role=="scout"));Assert.AreEqual(0,account.cash);Assert.AreEqual(0,account.operatingDebt);
        }
        [Test] public void HumanFreeStaffNegotiationIsProtectedFromNpcRecruitment()
        {
            Vacate("c1","scout");var target=c.staffMarket.First(s=>s.club==null&&s.role=="scout");c.staffMarket.RemoveAll(s=>s.club==null&&s.role=="scout"&&s.id!=target.id);c.staffOffers.Add(new StaffOffer{staff=target.id,club=c.club,status="accepted",due=1,employer=null,wage=target.wage,years=2});Tick(1);Assert.IsNull(target.club);Assert.IsFalse(c.staffMarket.Any(s=>s.club=="c1"&&s.role=="scout"));
        }
        [Test] public void ExpiringTargetIsNotSilentlyRenewedWhileHumanNegotiates()
        {
            var target=c.staffMarket.First(s=>s.club=="c1"&&s.role=="scout");target.until=1;c.staffOffers.Add(new StaffOffer{staff=target.id,club=c.club,employer="c1",status="pending",due=2,wage=target.wage,years=2});Tick(1);Assert.IsNull(target.club);Assert.AreEqual(1,target.until);
        }
        [Test] public void NpcCannotPoachStaffUnderAnActiveContract()
        {
            Vacate("c1","scout");c.staffMarket.RemoveAll(s=>s.club==null&&s.role=="scout");var target=c.staffMarket.Single(s=>s.club=="c2"&&s.role=="scout");Tick(1);Assert.AreEqual("c2",target.club);Assert.IsFalse(c.staffMarket.Any(s=>s.club=="c1"&&s.role=="scout"));
        }
        [Test] public void ActiveHumanClubVacancyRemainsAManagerDecision()
        {
            var target=c.staffMarket.Single(s=>s.club==c.club&&s.role=="scout");c.FireStaff(db,target.id);Tick(1);Assert.IsFalse(c.staffMarket.Any(s=>s.club==c.club&&s.role=="scout"));Assert.AreEqual(0,c.Staff("scout").wage);
        }
        [Test] public void ExpensiveNpcContractIsNotRenewedOrSilentlyCut()
        {
            var target=c.staffMarket.Single(s=>s.club=="c1"&&s.role=="scout");target.wage=100000;target.until=1;Tick(1);Assert.IsNull(target.club);Assert.AreEqual(100000,target.wage);var members=c.staffMarket.Where(s=>s.club=="c1");Assert.LessOrEqual(members.Sum(s=>s.wage),Math.Max(1500,db.clubs.Single(t=>t.id=="c1").annualRevenue/52/40));
        }
        [Test] public void ReviewWaitsThirtyDaysBeforeFundingANewVacancy()
        {
            Tick(1);Vacate("c1","scout");Tick(2);Assert.IsFalse(c.staffMarket.Any(s=>s.club=="c1"&&s.role=="scout"));Tick(31);Assert.IsTrue(c.staffMarket.Any(s=>s.club=="c1"&&s.role=="scout"));
        }
        [Test] public void SaveReloadDoesNotRepeatSigningExpenseOrMonthlyReview()
        {
            Vacate("c1","scout");Tick(1);c=JsonUtility.FromJson<Career>(JsonUtility.ToJson(c));c.RestoreWorld(db);c.EnsureWorld(db);long cash=c.world.aiAccounts.Single(a=>a.club=="c1").cash;Tick(1);Assert.AreEqual(cash,c.world.aiAccounts.Single(a=>a.club=="c1").cash);Assert.AreEqual(1,c.aiStaffReviewDay);Assert.AreEqual(1,c.staffMarket.Count(s=>s.club=="c1"&&s.role=="scout"));
        }
        [Test] public void RejoiningPreviousClubBindsActualRecruitedStaffAndKeepsDelegation()
        {
            Call(c,"MoveManager",db,"c1");c.SetDelegation("training",true);string departed=c.Staff("scout").id;Call(c,"MoveManager",db,"c0");Vacate("c1","scout");Tick(c.life.day+31);var hired=c.staffMarket.Single(s=>s.club=="c1"&&s.role=="scout");Assert.AreNotEqual(departed,hired.id);Call(c,"MoveManager",db,"c1");Assert.AreEqual(hired.id,c.Staff("scout").id);Assert.IsTrue(c.life.staff.training);
        }
        [Test] public void RenewalsDoNotDuplicateIdentitiesOrInventRealNames()
        {
            string[] ids=c.staffMarket.Select(s=>s.id).ToArray();for(int i=1;i<=25;i++)Tick(i*365);CollectionAssert.AreEquivalent(ids,c.staffMarket.Select(s=>s.id));Assert.AreEqual(ids.Length,c.staffMarket.Select(s=>s.id).Distinct().Count());Assert.AreEqual(12,c.staffMarket.Count(s=>s.club!=null));Assert.IsTrue(c.staffMarket.Where(s=>s.club!=null).All(s=>s.until>c.life.day));
        }
        [Test] public void HumanNegotiationProtectionExpiresWithTheOfferWindow()
        {
            Vacate("c1","scout");var target=c.staffMarket.First(s=>s.club==null&&s.role=="scout");c.staffMarket.RemoveAll(s=>s.club==null&&s.role=="scout"&&s.id!=target.id);c.staffOffers.Add(new StaffOffer{staff=target.id,club=c.club,status="accepted",due=1,employer=null,wage=target.wage,years=2});Tick(1);Assert.IsNull(target.club);Tick(31);Assert.AreEqual("c1",target.club);
        }
    }
}

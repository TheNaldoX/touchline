using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Touchline.Core;
namespace Touchline.Tests
{
    public sealed class StaffTrainingLifecycleTests
    {
        Database db;Career c;
        public static Career Fixture(out Database db)
        {
            db=new Database{leagues=new[]{new LeagueData{id="fra.1",name="L1"}},clubs=Enumerable.Range(0,4).Select(i=>new ClubData{id="c"+i,name="Club "+i,league="fra.1",playable=true,annualRevenue=20000000+i*1000000}).ToArray(),players=Enumerable.Range(0,96).Select(i=>new PlayerData{id="p"+i,name="Test "+i,team="c"+(i/24),age=24,position=i%24==0?"GB":"MIL",positions=new[]{i%24==0?"GK":"CM"},rating=65,potential=80,value=100000,wage=500,fitness=100,morale=75}).ToArray()};
            var career=new Career{club="c0"};career.lineup=Career.Select(db,career.club,career.tactic);career.EnsureWorld(db);career.EnsureStaffMarket(db);return career;
        }
        [SetUp] public void SetUp(){c=Fixture(out db);}
        public static void StaffTick(Career c,Database db,int day)
        {c.life.day=day;typeof(Career).GetMethod("StaffDay",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(c,new object[]{db});}
        StaffMember HireAssistant(string sameName=null)
        {
            var next=c.staffMarket.First(s=>s.club==null&&s.role=="assistant"&&s.id.StartsWith("staff-free-"));if(sameName!=null)next.name=sameName;
            c.life.reputation=90;c.ProposeStaffContract(db,next.id,Career.MonthlySalary(next.wage*2),2);StaffTick(c,db,c.life.day+2);c.SignStaffContract(db,next.id);return next;
        }
        [Test] public void StartChargesOnceAndNamesExactBeneficiaryAndDueDate()
        {
            var member=c.Staff("assistant");long cash=c.life.cash;c.TrainStaff("assistant");Assert.That(cash-c.life.cash,Is.EqualTo(c.StaffTrainingCost));
            Assert.That(c.life.staffTrainingStaffId,Is.EqualTo(member.id));Assert.That(c.life.staffTrainingUntil,Is.EqualTo(45));
            var mail=c.life.messages.Single(m=>m.subject=="Formation du staff engagée");Assert.That(mail.text,Does.Contain(member.name));Assert.That(mail.reference,Does.Contain(member.id));Assert.That(c.MessageNeedsDecision(mail),Is.False);
            cash=c.life.cash;Assert.Throws<InvalidOperationException>(()=>c.TrainStaff("fitness"));Assert.That(c.life.cash,Is.EqualTo(cash));
        }
        [Test] public void CompletionGrantsOnceOnlyAtActualDueDate()
        {
            var member=c.Staff("assistant");int before=member.tactics;c.TrainStaff("assistant");StaffTick(c,db,44);Assert.That(member.tactics,Is.EqualTo(before));
            StaffTick(c,db,45);Assert.That(member.tactics,Is.EqualTo(Math.Min(20,before+1)));Assert.That(c.StaffTrainingInProgress,Is.False);Assert.That(c.life.staffTrainingUntil,Is.EqualTo(0));
            StaffTick(c,db,46);Assert.That(member.tactics,Is.EqualTo(Math.Min(20,before+1)));Assert.That(c.life.messages.Count(m=>m.subject=="Formation terminée"),Is.EqualTo(1));
        }
        [Test] public void ExpirationThenRealHireNeverTransfersPaidTraining()
        {
            var old=c.Staff("assistant");old.until=1;c.TrainStaff("assistant");int oldSkill=old.tactics;StaffTick(c,db,1);Assert.That(c.StaffTrainingInProgress,Is.False);
            var replacement=HireAssistant();int replacementSkill=replacement.tactics;StaffTick(c,db,45);
            Assert.That(old.tactics,Is.EqualTo(oldSkill));Assert.That(replacement.tactics,Is.EqualTo(replacementSkill));Assert.That(c.life.messages.Any(m=>m.subject=="Formation terminée"),Is.False);
            Assert.That(c.life.messages.Count(m=>m.subject=="Formation interrompue"),Is.EqualTo(1));
        }
        [Test] public void IdenticalNamesAndRolesCannotTransferTrainingAcrossIdentities()
        {
            var old=c.Staff("assistant");old.until=1;c.TrainStaff("assistant");StaffTick(c,db,1);var replacement=HireAssistant(old.name);int before=replacement.tactics;
            Assert.That(replacement.name,Is.EqualTo(old.name));Assert.That(replacement.id,Is.Not.EqualTo(old.id));StaffTick(c,db,45);Assert.That(replacement.tactics,Is.EqualTo(before));
        }
        [Test] public void FiringCancelsCourseAndReleasesNewTrainingWithoutRefund()
        {
            var member=c.Staff("assistant");long cash=c.life.cash,release=c.StaffReleaseCost(member),fee=c.StaffTrainingCost;c.TrainStaff("assistant");c.FireStaff(db,member.id);
            Assert.That(cash-c.life.cash,Is.EqualTo(fee+release));Assert.That(c.life.staffTrainingUntil,Is.EqualTo(0));Assert.That(c.StaffTrainingInProgress,Is.False);
            c.TrainStaff("fitness");Assert.That(c.life.staffTrainingStaffId,Is.EqualTo(c.Staff("fitness").id));Assert.That(cash-c.life.cash,Is.EqualTo(2*fee+release));
        }
        [Test] public void OtherStaffDepartureLeavesIdentifiedCourseIntact()
        {c.TrainStaff("assistant");string id=c.life.staffTrainingStaffId;c.FireStaff(db,c.Staff("fitness").id);Assert.That(c.life.staffTrainingStaffId,Is.EqualTo(id));Assert.That(c.StaffTrainingInProgress,Is.True);Assert.That(c.life.messages.Any(m=>m.subject=="Formation interrompue"),Is.False);}
        [Test] public void ExpirationOnCompletionDayTakesPriorityOverSkillGrant()
        {var member=c.Staff("assistant");member.until=45;int before=member.tactics;c.TrainStaff("assistant");StaffTick(c,db,45);Assert.That(member.tactics,Is.EqualTo(before));Assert.That(c.life.messages.Any(m=>m.subject=="Formation terminée"),Is.False);Assert.That(c.StaffTrainingInProgress,Is.False);}
        [TestCase(null)][TestCase("")]
        public void UnverifiableLegacyTrainingClosesOnceWithoutRefundOrGrant(string identity)
        {
            var member=c.Staff("assistant");int before=member.tactics;c.TrainStaff("assistant");c.life.staffTrainingStaffId=identity;long cash=c.life.cash;
            StaffTick(c,db,1);Assert.That(member.tactics,Is.EqualTo(before));Assert.That(c.life.cash,Is.EqualTo(cash));Assert.That(c.life.staffTrainingUntil,Is.EqualTo(0));
            Assert.That(c.life.messages.Last(m=>m.subject=="Formation interrompue").text,Does.Contain("dossier"));StaffTick(c,db,2);Assert.That(c.life.messages.Count(m=>m.subject=="Formation interrompue"),Is.EqualTo(1));
            c.TrainStaff("fitness");Assert.That(c.StaffTrainingInProgress,Is.True);
        }
        [Test] public void MissingOrAmbiguousCurrentMemberNeverReceivesCourseBenefit()
        {
            var member=c.Staff("assistant");c.TrainStaff("assistant");c.life.staff.members.Add(member);Assert.That(c.StaffTrainingBeneficiary(),Is.Null);
            typeof(Career).GetMethod("ReviewStaffTraining",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(c,null);Assert.That(c.StaffTrainingInProgress,Is.False);
        }
        [Test] public void OrphanDeadlineWithoutRoleDoesNotBlockANewCycle()
        {c.life.staffTrainingUntil=45;c.life.staffTrainingRole=null;c.TrainStaff("assistant");Assert.That(c.life.staffTrainingStaffId,Is.EqualTo(c.Staff("assistant").id));}
        [Test] public void AllMaximumSkillsBlockButOneMaximumDoesNotWasteOtherProgress()
        {
            var member=c.Staff("assistant");member.people=20;member.tactics=10;member.coaching=19;member.judging=20;c.TrainStaff("assistant");StaffTick(c,db,45);
            Assert.That(member.tactics,Is.EqualTo(11));Assert.That(member.coaching,Is.EqualTo(20));Assert.That(member.judging,Is.EqualTo(20));Assert.That(member.people,Is.EqualTo(20));
            member.tactics=20;long cash=c.life.cash;Assert.Throws<InvalidOperationException>(()=>c.TrainStaff("assistant"));Assert.That(c.life.cash,Is.EqualTo(cash));
        }
        [TestCase("expired")][TestCase("identity")][TestCase("suspended")][TestCase("dismissed")][TestCase("match")]
        public void UnavailableStartDoesNotChargeOrCreateCourse(string reason)
        {
            var member=c.Staff("assistant");if(reason=="expired")member.until=0;else if(reason=="identity")member.id=null;else if(reason=="suspended")c.life.managerBanUntil=3;else if(reason=="dismissed")c.world.managerStatus="unemployed";else c.match=new MatchState();
            long cash=c.life.cash;Assert.Throws<InvalidOperationException>(()=>c.TrainStaff("assistant"));Assert.That(c.life.cash,Is.EqualTo(cash));Assert.That(c.StaffTrainingInProgress,Is.False);
        }
    }
}

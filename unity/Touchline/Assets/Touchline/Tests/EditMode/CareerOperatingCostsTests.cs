using System.Linq;
using NUnit.Framework;
using Touchline.Core;

namespace Touchline.Tests
{
    public partial class ProfessionalTests
    {
        [Test] public void EmployerProjectionDoesNotCountVisibleStaffTwice()
        {
            var a=Career.EmploymentProjection("fra.1",1000000,0);var b=Career.EmploymentProjection("fra.1",1000000,100000);
            Assert.That(b.Total,Is.EqualTo(a.Total).Within(2));Assert.That(b.staffGross,Is.EqualTo(100000));Assert.That(b.otherPersonnel,Is.LessThan(a.otherPersonnel));
        }
        [Test] public void WageCeilingReservesEmployerAndOtherPersonnelCosts()
        {
            long gross=Career.GrossWageCeiling("fra.1",20000000,200000);var costs=Career.EmploymentProjection("fra.1",gross*52,200000);
            Assert.That(costs.Total,Is.LessThanOrEqualTo(11000000));Assert.That(gross,Is.LessThan(20000000*.55/52));
        }
        [Test] public void PsgOperatingProjectionSeparatesTransferAmortisation()
        {
            var team=new ClubData{id="160",league="fra.1",annualRevenue=837011000};
            Assert.That(Career.AnnualOperatingCosts(team),Is.EqualTo(229173000).Within(1),"159.909m transfer amortisation must not be debited a second time.");
        }
        [Test] public void DismissalDoesNotCancelStaffWageLiabilities()
        {
            c.life.day=6;c.life.nextFixture=int.MaxValue;c.world.managerStatus="dismissed";long wages=c.life.staff.members.Sum(s=>s.wage);
            c.AdvanceDay(db);var entries=c.life.ledger.Where(e=>e.day==7&&e.label=="Salaires du staff").ToArray();
            Assert.AreEqual(1,entries.Length);Assert.AreEqual(-wages,entries[0].amount);
            Assert.AreEqual(1,c.life.ledger.Count(e=>e.day==7&&e.label=="Salaires de l’effectif"));
        }
        [Test] public void ExistingSaveRegistersAiFinancialReferencesWithoutReset()
        {
            c.world.aiAccounts.Clear();c.world.initialAiEmployment=false;c.world.contracts.RemoveAll(x=>x.club!=c.club);c.life.day=81;long cash=c.life.cash;
            c.EnsureWorld(db);Assert.AreEqual(81,c.life.day);Assert.AreEqual(cash,c.life.cash);Assert.IsTrue(c.world.initialAiEmployment);
            Assert.That(c.world.contracts.Any(x=>x.club=="c1"&&x.estimated));Assert.That(c.world.aiAccounts.Count,Is.EqualTo(db.clubs.Length));
        }
    }
}

using NUnit.Framework;
using Touchline.Core;

namespace Touchline.Tests
{
    public class SalaryConversionTests
    {
        // Défaut corrigé : 10 000 € saisis par mois → 2 308 €/semaine → réaffichés 10 001 €.
        [TestCase(10000)][TestCase(12500)][TestCase(2450)][TestCase(10)][TestCase(1000000)]
        public void RoundMonthlyAmountsTypedInTheNegotiationAreDisplayedUnchanged(long monthly)
        {
            Assert.AreEqual(monthly,Career.MonthlySalary(Career.WeeklySalary(monthly)));
        }
        [Test] public void EveryMonthlyMultipleOfTenEurosRoundTripsExactly()
        {
            for(long monthly=0;monthly<=2000000;monthly+=10)
                if(Career.MonthlySalary(Career.WeeklySalary(monthly))!=monthly)Assert.Fail(monthly+" € par mois réaffiché "+Career.MonthlySalary(Career.WeeklySalary(monthly))+" €");
        }
        [Test] public void StoredWeeklyWagesAreNeverChangedByADisplayedAmountSentBack()
        {
            // Formulaire prérempli avec le mensuel affiché puis renvoyé : la semaine stockée ne bouge pas.
            for(long weekly=0;weekly<=500000;weekly++)
                if(Career.WeeklySalary(Career.MonthlySalary(weekly))!=weekly)Assert.Fail("Semaine "+weekly+" modifiée par l'aller-retour");
        }
        [TestCase(2308,10000)][TestCase(1001,4338)][TestCase(0,0)]
        public void DisplayedMonthlyStaysWithinTheWeeklyStoragePrecision(long weekly,long expected)
        {
            long shown=Career.MonthlySalary(weekly);
            Assert.AreEqual(expected,shown);
            Assert.LessOrEqual(System.Math.Abs(shown-weekly*52m/12m),2.17m,"Écart borné par la précision d'une semaine entière (52/12/2 €)");
        }
    }
}

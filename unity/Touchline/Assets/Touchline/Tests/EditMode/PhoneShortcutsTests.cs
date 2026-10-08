using NUnit.Framework;

namespace Touchline.Tests
{
    public class PhoneShortcutsTests
    {
        // Densité du Galaxy Z Fold : pixels physiques par dp.
        const float FoldPixelsPerDp=2.625f;

        [TestCase(1080,2520,.97f,1.08f)] // écran plié
        [TestCase(2184,1968,.9f,1.08f)]  // écran déplié
        public void FoldUnitsStayCloseToRealDp(int width,int height,float minDp,float maxDp)
        {
            float dpPerUnit=InterfaceViewport.Scale(width,height)/FoldPixelsPerDp;
            Assert.That(dpPerUnit,Is.InRange(minDp,maxDp));
            // Une cible de 48 unités (feuille de style) fait au moins ~44 dp au doigt.
            Assert.That(48*dpPerUnit,Is.GreaterThanOrEqualTo(43.5f));
        }

        [Test] public void UnfoldedKeepsSideMenuAndFoldedUsesCompactLayout()
        {
            float unfolded=InterfaceViewport.Scale(2184,1968),folded=InterfaceViewport.Scale(1080,2520);
            Assert.That(InterfaceViewport.WideLayout(2184/unfolded,1968/unfolded),Is.True);
            Assert.That(InterfaceViewport.WideLayout(1080/folded,2520/folded),Is.False);
        }

        [Test] public void MentalityShortcutMatchesNearestLevel()
        {
            Assert.That(PhoneShortcuts.MentalityLevels.Length,Is.EqualTo(PhoneShortcuts.MentalityLabels.Length));
            Assert.That(PhoneShortcuts.NearestMentality(.5f),Is.EqualTo(1));
            Assert.That(PhoneShortcuts.NearestMentality(.15f),Is.EqualTo(0));
            Assert.That(PhoneShortcuts.NearestMentality(.9f),Is.EqualTo(2));
            for(int i=0;i<PhoneShortcuts.MentalityLevels.Length;i++)Assert.That(PhoneShortcuts.NearestMentality(PhoneShortcuts.MentalityLevels[i]),Is.EqualTo(i));
        }

        [Test] public void AdvanceToMatchStopsOnMatchDayAndHasSafetyLimit()
        {
            Assert.That(PhoneShortcuts.KeepAdvancing(10,17,0),Is.True);
            Assert.That(PhoneShortcuts.KeepAdvancing(17,17,7),Is.False);
            Assert.That(PhoneShortcuts.KeepAdvancing(0,int.MaxValue,PhoneShortcuts.MaxAdvanceDays),Is.False);
        }
    }
}

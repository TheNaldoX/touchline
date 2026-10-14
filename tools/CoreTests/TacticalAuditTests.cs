using System;
using NUnit.Framework;
using Touchline.Analysis;

namespace Touchline.Tests
{
    public class TacticalAuditTests
    {
        [Test] public void IdenticalPairedMatchesRemainInconclusive()
        {
            var s=new PairedStatistics(new[]{1d,4,9},new[]{1d,4,9});
            Assert.AreEqual(0d,s.Difference);Assert.AreEqual(0d,s.HalfInterval);Assert.AreEqual("non concluant",s.Verdict);
        }
        [Test] public void PairingRemovesFixtureStrengthVariance()
        {
            var s=new PairedStatistics(new[]{1d,40,900},new[]{3d,42,902});
            Assert.AreEqual(2d,s.Difference);Assert.AreEqual(0d,s.HalfInterval);Assert.AreEqual("hausse",s.Verdict);
        }
        [Test] public void NoisyDifferencesAreNotPresentedAsCertain()
        {
            var s=new PairedStatistics(new[]{10d,10,10},new[]{5d,17,10});
            Assert.AreEqual("non concluant",s.Verdict);Assert.Greater(s.HalfInterval,s.Difference);
        }
        [Test] public void InvalidOrMissingSamplesFailInsteadOfBeingDropped()
        {
            Assert.Throws<ArgumentException>(()=>new PairedStatistics(new[]{1d,2},new[]{1d}));
            Assert.Throws<ArgumentException>(()=>new PairedStatistics(new[]{1d,double.NaN},new[]{1d,2}));
        }
    }
}

using NUnit.Framework;

namespace Touchline.Tests
{
    public class RenderBudgetTests
    {
        [Test] public void BoundedMatchTextureKeepsFullBalancedResolution(){Assert.AreEqual(1,RenderBudget.ResolutionCap(1,1600,1000));Assert.Less(RenderBudget.ResolutionCap(1,3200,2400),.6f);}
        [Test] public void EconomyModeRetainsItsSmallerPixelBudget(){Assert.That(RenderBudget.ResolutionCap(0,1600,1000),Is.InRange(.78f,.80f));}
        [Test] public void QualityModesNeverSupersampleOrReturnInvalidScales(){foreach(int mode in new[]{0,1,2})foreach(int side in new[]{0,1,720,1600,4000})Assert.That(RenderBudget.ResolutionCap(mode,side,side),Is.InRange(.55f,1));}
    }
}

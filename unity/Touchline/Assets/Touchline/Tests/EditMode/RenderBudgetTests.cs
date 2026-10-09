using NUnit.Framework;

namespace Touchline.Tests
{
    public class RenderBudgetTests
    {
        [TestCase(412,961)] [TestCase(832,750)]
        public void MaximumQualityActuallyRaisesViewportResolutionWithinPixelBudget(int width,int height)
        {
            var balanced=RenderBudget.ViewportSize(1,width,height,2.625f);
            var maximum=RenderBudget.ViewportSize(2,width,height,2.625f);
            Assert.Greater(maximum.x,balanced.x);Assert.Greater(maximum.y,balanced.y);
            Assert.LessOrEqual((long)maximum.x*maximum.y,2600000);
            Assert.AreEqual(width/(float)height,maximum.x/(float)maximum.y,.002f);
            Assert.AreEqual(4,RenderBudget.MsaaSamples(2));Assert.AreEqual(2,RenderBudget.MsaaSamples(1));
        }
        [Test] public void ViewportDoesNotUpsampleSmallPanelsAndHandlesInvalidSize()
        {
            var small=RenderBudget.ViewportSize(2,320,200,1);Assert.AreEqual(320,small.x);Assert.AreEqual(200,small.y);
            var invalid=RenderBudget.ViewportSize(2,float.NaN,200,1);Assert.AreEqual(16,invalid.x);Assert.AreEqual(16,invalid.y);
            var low=RenderBudget.ViewportSize(0,832,750,2.625f);Assert.LessOrEqual((long)low.x*low.y,1000000);
        }
        [Test] public void BoundedMatchTextureKeepsFullBalancedResolution(){Assert.AreEqual(1,RenderBudget.ResolutionCap(1,1600,1000));Assert.Less(RenderBudget.ResolutionCap(1,3200,2400),.6f);}
        [Test] public void EconomyModeRetainsItsSmallerPixelBudget(){Assert.That(RenderBudget.ResolutionCap(0,1600,1000),Is.InRange(.78f,.80f));}
        [Test] public void QualityModesNeverSupersampleOrReturnInvalidScales(){foreach(int mode in new[]{0,1,2})foreach(int side in new[]{0,1,720,1600,4000})Assert.That(RenderBudget.ResolutionCap(mode,side,side),Is.InRange(.55f,1));}
    }
}

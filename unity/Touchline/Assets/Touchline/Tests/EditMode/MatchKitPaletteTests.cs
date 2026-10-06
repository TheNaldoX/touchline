using NUnit.Framework;
using UnityEngine;

namespace Touchline.Tests
{
    public class MatchKitPaletteTests
    {
        [TestCase("#EF0107","#DA291C")]
        [TestCase("#FFFFFF","#F0F0F0")]
        [TestCase("#101010","#151515")]
        [TestCase("#6CABDD","#70AADD")]
        [TestCase("#008844","#007733")]
        public void ClashingTeamsReceiveClearlyDifferentStrips(string first,string second)
        {
            ColorUtility.TryParseHtmlString(first,out var home);ColorUtility.TryParseHtmlString(second,out var away);
            home=MatchKitPalette.Home(home);var resolved=MatchKitPalette.Away(home,away);
            Assert.Greater(MatchKitPalette.Contrast(home,resolved),3f,"Change strip must be distinguishable in luminance");
            Assert.AreEqual(resolved,MatchKitPalette.Away(home,away),"Selection is deterministic");
        }
        [Test] public void DistinctExistingColoursStayUnchanged()
        {
            var home=new Color(.85f,.02f,.04f);var away=new Color(.1f,.2f,.9f);
            Assert.AreEqual(away,MatchKitPalette.Away(home,away));
        }
    }
}

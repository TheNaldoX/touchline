using NUnit.Framework;

namespace Touchline.Tests
{
    public class MatchFrameHistoryTests
    {
        [Test] public void FrameRateUsesElapsedTimeRatherThanAverageInstantaneousFps(){var h=new MatchFrameHistory();h.Add(.01f);h.Add(.03f);Assert.That(h.Read().FramesPerSecond,Is.EqualTo(50).Within(.01));Assert.That(h.Read().averageMs,Is.EqualTo(20).Within(.01));}
        [Test] public void StuttersRemainVisibleInTailAndWorstFrame(){var h=new MatchFrameHistory();for(int i=0;i<90;i++)h.Add(.016f);for(int i=0;i<10;i++)h.Add(.1f);Assert.That(h.Read().p95Ms,Is.EqualTo(100).Within(.01));Assert.That(h.Read().worstMs,Is.EqualTo(100).Within(.01));}
        [Test] public void RollingWindowExpiresOldFramesAndIgnoresInvalidValues(){var h=new MatchFrameHistory();h.Add(2);for(int i=0;i<600;i++)h.Add(.02f);h.Add(0);h.Add(-1);h.Add(float.NaN);h.Add(float.PositiveInfinity);Assert.AreEqual(600,h.Read().count);Assert.That(h.Read().worstMs,Is.EqualTo(20).Within(.01));h.Reset();Assert.AreEqual(0,h.Read().count);}
    }
}

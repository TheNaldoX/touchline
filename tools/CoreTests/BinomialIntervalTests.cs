using System;
using NUnit.Framework;
using Touchline.Analysis;

namespace Touchline.Tests
{
public class BinomialIntervalTests
{
    [Test] public void FavouriteSampleRetainsSamplingUncertainty()
    {
        var interval=new BinomialInterval(63,99);
        Assert.That(interval.Lower,Is.InRange(.537,.539));
        Assert.That(interval.Upper,Is.InRange(.724,.725));
    }
    [Test] public void BoundarySamplesStayWithinProbabilityBounds()
    {
        var none=new BinomialInterval(0,20);var all=new BinomialInterval(20,20);
        Assert.That(none.Lower,Is.InRange(0,.00001));
        Assert.That(all.Upper,Is.InRange(.99999,1));
        Assert.That(none.Upper,Is.InRange(.16,.162));
        Assert.That(all.Lower,Is.InRange(.838,.84));
    }
    [Test] public void EmptyOrImpossibleSamplesAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(()=>new BinomialInterval(0,0));
        Assert.Throws<ArgumentOutOfRangeException>(()=>new BinomialInterval(3,2));
        Assert.Throws<ArgumentOutOfRangeException>(()=>new BinomialInterval(-1,2));
    }
}
}

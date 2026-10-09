using System;

namespace Touchline.Analysis
{
    // Wilson 95% interval: report uncertainty instead of treating 99 fixtures
    // as the exact probability of a favourite winning.
    public sealed class BinomialInterval
    {
        public readonly double Lower, Upper;
        public BinomialInterval(int successes,int total)
        {
            if(total<=0||successes<0||successes>total)throw new ArgumentOutOfRangeException(nameof(total));
            const double Z95=1.96;
            double p=(double)successes/total,z2=Z95*Z95,denominator=1+z2/total;
            double centre=(p+z2/(2*total))/denominator;
            double half=Z95*Math.Sqrt(p*(1-p)/total+z2/(4*total*total))/denominator;
            Lower=Math.Max(0,centre-half);Upper=Math.Min(1,centre+half);
        }
    }
}

using System;
using System.Linq;

namespace Touchline.Analysis
{
    // Differences from identical fixtures/seeds. A 95% normal approximation,
    // not a guarantee that a tactic wins or that seven comparisons are independent.
    public sealed class PairedStatistics
    {
        public readonly double Low, High, Difference, HalfInterval;
        public string Verdict => Difference - HalfInterval > 0 ? "hausse" :
            Difference + HalfInterval < 0 ? "baisse" : "non concluant";
        public PairedStatistics(double[] low, double[] high)
        {
            if(low==null||high==null||low.Length!=high.Length||low.Length<2)
                throw new ArgumentException("Deux séries appariées de même taille (au moins 2 matchs) sont requises.");
            if(low.Concat(high).Any(x=>double.IsNaN(x)||double.IsInfinity(x)))
                throw new ArgumentException("Mesure non finie.");
            Low=low.Average();High=high.Average();Difference=High-Low;
            double variance=low.Select((x,i)=>Math.Pow(high[i]-x-Difference,2)).Sum()/(low.Length-1);
            const double Normal95=1.96;
            HalfInterval=Normal95*Math.Sqrt(variance/low.Length);
        }
    }
}

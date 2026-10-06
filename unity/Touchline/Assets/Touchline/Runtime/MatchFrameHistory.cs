using System;

namespace Touchline
{
    public readonly struct MatchFrameSummary
    {
        public readonly int count;public readonly float seconds,averageMs,p95Ms,worstMs;
        public float FramesPerSecond=>seconds>0?count/seconds:0;
        public MatchFrameSummary(int count,float seconds,float p95,float worst){this.count=count;this.seconds=seconds;averageMs=count>0?seconds/count*1000:0;p95Ms=p95*1000;worstMs=worst*1000;}
    }
    public sealed class MatchFrameHistory
    {
        readonly float[] samples=new float[600];int count,next;
        public void Add(float seconds){if(seconds<=0||float.IsNaN(seconds)||float.IsInfinity(seconds))return;samples[next]=seconds;next=(next+1)%samples.Length;count=Math.Min(count+1,samples.Length);}
        public void Reset(){count=next=0;}
        public MatchFrameSummary Read(){if(count==0)return default;var sorted=new float[count];Array.Copy(samples,sorted,count);Array.Sort(sorted);float sum=0;foreach(float value in sorted)sum+=value;return new MatchFrameSummary(count,sum,sorted[Math.Max(0,(int)Math.Ceiling(count*.95)-1)],sorted[count-1]);}
    }
}

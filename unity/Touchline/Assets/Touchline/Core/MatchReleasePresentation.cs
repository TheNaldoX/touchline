namespace Touchline.Core
{
    // Read-only evidence of a launch inside the last fixed step. Presentation
    // must not interpolate the outgoing flight before the actual release.
    public struct BallReleaseContact
    {
        public bool valid;
        public string kind,source;
        public float clock,fraction,previousHeight,releaseHeight,endHeight,flightEndHeight,duration,loft;
        public Point previous,release,end,flightEnd;
    }
    public sealed partial class MatchSimulation
    {
        public BallReleaseContact ReleaseContact {get;private set;}
        void RecordBallRelease(float fraction)
        {
            var b=State.ball;
            ReleaseContact=new BallReleaseContact{valid=true,kind=b.kind,source=b.from,clock=State.clock,fraction=Mathx.Clamp(fraction,0,1),previous=b.previous,previousHeight=b.previousHeight,release=b.start,releaseHeight=b.startHeight,end=b.position,endHeight=b.height,flightEnd=b.end,flightEndHeight=b.endHeight,duration=System.Math.Max(.1f,b.duration),loft=b.loft};
        }
    }
}

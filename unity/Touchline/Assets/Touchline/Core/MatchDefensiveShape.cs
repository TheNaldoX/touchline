using System;
namespace Touchline.Core
{
    public sealed partial class MatchSimulation
    {
        Point DefensiveFocus(int side)
        {
            var b=State.ball;
            if(string.IsNullOrEmpty(b.owner)&&b.side!=side&&!string.IsNullOrEmpty(b.to)&&b.kind!="none"&&b.kind!="loose"){
                var receiver=Find(b.to);
                if(receiver!=null&&!receiver.sentOff&&receiver.side!=side)
                    return Point.Lerp(receiver.position,b.end,.65f);
            }
            return b.position;
        }
    }
}

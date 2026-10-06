using System;
namespace Touchline.Core {
    public sealed partial class MatchSimulation {
        // A zonal waypoint can cross another runner during a cutback. Keep a
        // nearby central attacker on the goal side until a teammate can take
        // physical responsibility, rather than abandoning him for that waypoint.
        public static Actor CloseGoalSideMark(Actor defender,Actor preferred,Actor[] actors,int direction)
        {
            if(defender==null||actors==null||defender.sentOff||defender.slot==0||defender.position.x*direction>-32.5f)return preferred;
            Actor local=null;float nearest=3.5f;
            foreach(var threat in actors){
                if(threat==null||threat.sentOff||GroundedAction(threat)||threat.action=="hurt"&&threat.actionTime>0||threat.side==defender.side||threat.slot==0||Math.Abs(threat.position.z)>16.5f)continue;
                // A runner who gets half a step ahead still needs tracking.
                // Only the teammate taking over must already be goal side.
                float gap=Point.Distance(defender.position,threat.position);if(gap>=nearest)continue;
                bool covered=false;
                foreach(var mate in actors){
                    if(mate==null||mate==defender||mate.sentOff||GroundedAction(mate)||mate.action=="hurt"&&mate.actionTime>0||mate.side!=defender.side||mate.slot==0)continue;
                    if(mate.position.x*direction>threat.position.x*direction+.5f)continue;
                    float otherGap=Point.Distance(mate.position,threat.position);
                    if(otherGap<2.5f&&otherGap+.5f<gap){covered=true;break;}
                }
                if(!covered){nearest=gap;local=threat;}
            }
            return local??preferred;
        }
    }
}

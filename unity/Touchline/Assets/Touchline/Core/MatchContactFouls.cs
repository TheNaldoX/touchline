using System;
namespace Touchline.Core
{
    public sealed partial class MatchSimulation
    {
        // Tactical foul: a counter-attack carrier breaking through midfield with
        // an opponent chasing at his shoulder gets his shirt pulled. Rate per
        // second for an average-aggression chaser; window (s) after the turnover;
        // minimum forward speed (m/s); chaser distance (m); share booked.
        // Zone (m from halfway, attacking direction) where a counter is stopped,
        // how far ahead of the carrier the chaser may already be (m), and the
        // aggression / pressing factors on the rate.
        const float TacticalFoulZoneFrom=-25f,TacticalFoulZoneTo=28f,TacticalFoulAhead=.3f;
        const float TacticalFoulBaseTemper=.5f,TacticalFoulTemperPerPoint=.01f,TacticalFoulPressingThreshold=.5f,TacticalFoulPressingFactor=1.2f;
        public const float TacticalFoulRatePerSecond=.6f,TacticalFoulWindow=6f,TacticalFoulMinSpeed=3.5f,TacticalFoulReach=1.4f,TacticalFoulCaution=.25f;
        bool TacticalFoul(Actor carrier)
        {
            var m=State;if(!m.professionalRules||m.restart>0||m.ball.held||carrier.slot==0||m.clock-m.turnoverAt>TacticalFoulWindow)return false;
            int dir=Direction(carrier.side);float x=carrier.position.x*dir;if(x<TacticalFoulZoneFrom||x>TacticalFoulZoneTo||carrier.velocity.x*dir<TacticalFoulMinSpeed)return false;
            Actor chaser=null;float best=TacticalFoulReach;
            foreach(var p in m.actors){if(p.sentOff||p.side==carrier.side||p.slot==0||p.yellows>0||p.duelCooldown>0||GroundedAction(p))continue;
                // Only an unbooked chaser level with or behind the carrier (he has lost
                // the race; a booked player does not risk a second yellow).
                if((p.position.x-carrier.position.x)*dir>TacticalFoulAhead)continue;float d=Point.Distance(p.position,carrier.position);if(d<best){best=d;chaser=p;}}
            if(chaser==null)return false;
            float rate=TacticalFoulRatePerSecond*(TacticalFoulBaseTemper+Skill(chaser,"aggression")*TacticalFoulTemperPerPoint)*(Tactic(chaser.side).pressing>TacticalFoulPressingThreshold?TacticalFoulPressingFactor:1f);
            if(Random()>=rate*Step)return false;
            chaser.duelCooldown=2;m.metrics[chaser.side].fouls++;
            Emit("foul",chaser.side,chaser.id,Data(chaser).name+" tire le maillot pour stopper la contre-attaque.");
            if(Random()<TacticalFoulCaution)Caution(chaser.id);
            Restart("free-kick",carrier.side,carrier.position,3);return true;
        }
        void ConsiderContactFoul(Actor a,Actor b,float closing)
        {
            if(!State.professionalRules||State.restart>0||State.ball.held||State.ball.elapsed<0||a.side==b.side||closing>=-2.2f)return;
            var carrier=Owner;if(carrier!=a&&carrier!=b)return;var challenger=carrier==a?b:a;
            if(carrier.slot==0||challenger.slot==0||GroundedAction(carrier)||GroundedAction(challenger)||challenger.duelCooldown>0||challenger.action=="tackle")return;
            var towardCarrier=(carrier.previous-challenger.previous).Normalized;
            float driving=Point.Dot(challenger.velocity,towardCarrier);
            float carrierDriving=Math.Max(0,-Point.Dot(carrier.velocity,towardCarrier));
            // Possession does not automatically make the other player the
            // offender. A carrier running into a stationary/backing-away
            // opponent has not been charged by that opponent.
            if(driving<=Math.Max(1.2f,carrierDriving))return;
            var forward=new Point((float)Math.Sin(carrier.angle),(float)Math.Cos(carrier.angle));
            var approach=(challenger.previous-carrier.previous).Normalized;
            // A shoulder-to-shoulder contest remains legal. A runner striking
            // the carrier's back with no path to the ball commits a charge.
            bool backCharge=Point.Dot(approach,forward)<=-.45f&&Point.Distance(challenger.position,State.ball.position)>=.85f;
            var relative=challenger.velocity-carrier.velocity;
            // Parallel runners can contest shoulder to shoulder. A fast
            // transverse charge is different: the body blocks the challenger's
            // path before the ball, so it is not an attempted ball contact.
            bool crossingCharge=closing< -3.8f&&relative.Length>4&&
                Point.Dot(challenger.velocity.Normalized,carrier.velocity.Normalized)<.7f&&
                Point.Dot(State.ball.position-carrier.position,relative.Normalized)>.12f;
            if(!backCharge&&!crossingCharge)return;
            challenger.duelCooldown=2;State.metrics[challenger.side].fouls++;
            Emit("foul",challenger.side,challenger.id,Data(challenger).name+(backCharge?" déséquilibre son adversaire par une charge dans le dos.":" percute son adversaire par une charge transversale sans atteindre le ballon."));
            BeginContactFall(carrier,challenger,true);
            if(closing< -5.5f)Caution(challenger.id);
            bool penalty=carrier.position.x*Direction(carrier.side)>36&&Math.Abs(carrier.position.z)<20.16f;
            Restart(penalty?"penalty":"free-kick",carrier.side,penalty?new Point(Direction(carrier.side)*41.5f,0):carrier.position,3);
        }
    }
}

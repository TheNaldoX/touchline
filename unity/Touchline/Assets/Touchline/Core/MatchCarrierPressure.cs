using System;

namespace Touchline.Core
{
    public sealed partial class MatchSimulation
    {
        // Pression rapprochée sur le porteur. Un défenseur debout dont le pied
        // atteint le ballon finit par le chiper (pied glissé, ballon touché
        // dans la course, porteur bousculé), même sans tacle engagé. Le risque
        // est un taux par seconde : il dépend du tacle et du placement des
        // presseurs face à la conduite et à la protection du porteur, de sa
        // vitesse (le ballon s'éloigne du pied) et du nombre de presseurs.
        public const string CarrierPressurePoke="pressure-poke";
        public const float CarrierPressureReach=1.3f;     // m, distance pied du défenseur – ballon
        public const float CarrierPressureRate=.45f;      // pertes par seconde : un presseur au contact, niveaux égaux, porteur à l'arrêt
        public const float CarrierPressureSkillScale=.035f; // facteur exp par point d'écart défense – conduite (±20 pts : ×2,0 / ×0,5)
        public const float CarrierPressureSpeedFactor=.12f; // +12 % de risque par m/s de vitesse du porteur
        public const float CarrierPressureCrowd=.6f;      // +60 % par presseur supplémentaire au contact
        public const float CarrierPressureShielded=.4f;   // part du risque quand le corps du porteur s'interpose
        public const float CarrierPressureOwnBox=.35f;    // facteur de risque dans la surface du presseur : il retient son geste
        public const float CarrierPressureFoulShare=.12f; // part des duels de pression sifflés (joueurs égaux, agressivité 60)
        const float CarrierPressureFoulCaution=.06f;      // part de ces fautes sanctionnées d'un carton
        const float CarrierPressurePokeSpeed=2.5f,CarrierPressurePokeExtra=3f; // m/s : vitesse du ballon chipé, de 2,5 à 5,5

        // Risque par seconde que le porteur perde le ballon sous la pression
        // actuelle ; renvoie aussi le presseur le mieux placé.
        public float CarrierPressureHazard(Actor carrier,out Actor presser)
        {
            presser=null;var b=State.ball;
            if(carrier==null||carrier.slot==0||b.held||b.height>.45f)return 0;
            float pressure=0,best=0;int count=0;
            foreach(var d in State.actors){
                if(d.sentOff||d.side==carrier.side||d.slot==0||GroundedAction(d)||d.controlTime>0||d.duelCooldown>0||d.action=="tackle"||d.action=="hurt")continue;
                float reach=Point.Distance(d.position,b.position);if(reach>=CarrierPressureReach)continue;
                float weight=1-reach/CarrierPressureReach;
                float projection=Projection(d.position,b.position,carrier.position);
                if(projection>.15f&&projection<.9f&&Point.Distance(carrier.position,Point.Lerp(d.position,b.position,projection))<.3f)weight*=CarrierPressureShielded;
                float skill=Skill(d,"standingTackle")*.6f+Skill(d,"defensiveAwareness")*.4f;
                weight*=(float)Math.Exp((skill-CarrierControl(carrier))*CarrierPressureSkillScale);
                pressure+=weight;count++;if(weight>best){best=weight;presser=d;}
            }
            if(count==0)return 0;
            int dir=Direction(carrier.side);bool box=carrier.position.x*dir>36&&Math.Abs(carrier.position.z)<20.16f;
            return (box?CarrierPressureOwnBox:1)*CarrierPressureRate*pressure*(1+CarrierPressureCrowd*(count-1))*(1+carrier.velocity.Length*CarrierPressureSpeedFactor);
        }
        // Décision sous pression : délai (s) avant de choisir quand un presseur
        // atteint le ballon, plus un retard par point de sang-froid manquant.
        public const float PressedDecisionDelay=.25f,PressedDecisionPerPoint=.004f;
        // IA : points de score retirés à une conduite par adversaire au-delà
        // du premier dans le couloir de conduite (rayon en m), et à une passe
        // vers un receveur dont l'adversaire le plus proche est sous MarkedReceiverDistance (m),
        // hors de la zone de finition (au-delà de MarkedReceiverZone m de la médiane).
        public const float CarryCrowdRadius=2.2f,CarryCrowdPenalty=4f;
        public const float MarkedReceiverZone=36f,MarkedReceiverDistance=2f,MarkedReceiverPenalty=8f;
        // Pressing haut : distance (m) à la ligne médiane, dans le camp adverse,
        // au-delà de laquelle le porteur est dans son tiers défensif, et
        // consigne de pressing minimale (0–1) pour aller le chercher.
        public const float HighPressZone=17.5f,HighPressMinPressing=.35f;
        int OpponentsOnCarry(Actor carrier,Point end)
        {
            int count=0;
            foreach(var o in State.actors){
                if(o.sentOff||o.side==carrier.side||o.slot==0||GroundedAction(o))continue;
                float along=Projection(carrier.position,end,o.position);if(along<.05f)continue;
                if(Point.Distance(o.position,Point.Lerp(carrier.position,end,along))<CarryCrowdRadius)count++;
            }
            return count;
        }
        // Dans le dernier quart (au-delà de CarryCrowdZone m de la médiane),
        // provoquer la défense reste un choix d'attaquant : pénalité réduite,
        // et nulle à hauteur de la surface (au-delà de CarryCrowdBoxZone m).
        public const float CarryCrowdZone=25f,CarryCrowdBoxZone=36f,CarryCrowdFinalPenalty=1.5f;
        float CarryCrowdCost(Actor carrier,Point end)
        {
            float x=carrier.position.x*Direction(carrier.side);
            if(x>CarryCrowdBoxZone)return 0;
            return Math.Max(0,OpponentsOnCarry(carrier,end)-1)*(x>CarryCrowdZone?CarryCrowdFinalPenalty:CarryCrowdPenalty);
        }
        float MarkedReceiverCost(float space)=>space<MarkedReceiverDistance?MarkedReceiverPenalty*(1-space/MarkedReceiverDistance):0;
        float CarrierControl(Actor carrier)=>Skill(carrier,"dribbling")*.5f+Skill(carrier,"ballControl")*.3f+Skill(carrier,"strength")*.2f;

        bool ResolveCarrierPressure(Actor carrier)
        {
            var m=State;var b=m.ball;
            if(m.restart>0||carrier.controlTime>0)return false;
            float hazard=CarrierPressureHazard(carrier,out var presser);
            if(presser==null)return false;
            // Un porteur pressé relève la tête plus tôt : il ne garde pas le
            // ballon jusqu'à sa prochaine décision ordinaire.
            m.decision=Math.Min(m.decision,PressedDecisionDelay+(100-Skill(carrier,"composure"))*PressedDecisionPerPoint);
            if(Random()>=1-(float)Math.Exp(-hazard*Step))return false;
            int dir=Direction(carrier.side);
            bool ownBox=carrier.position.x*dir>36&&Math.Abs(carrier.position.z)<20.16f;
            float foul=Mathx.Clamp(CarrierPressureFoulShare+(Skill(presser,"aggression")-60)*.003f+(CarrierControl(carrier)-Skill(presser,"standingTackle"))*.002f,.05f,.45f);
            if(presser.yellows>0)foul*=BookedCarefulness;
            // Dans sa surface, le défenseur retient son geste : pas de faute ici.
            if(m.professionalRules&&!ownBox&&Random()<foul){
                presser.duelCooldown=2;m.metrics[presser.side].fouls++;
                Emit("foul",presser.side,presser.id,Data(presser).name+" bouscule le porteur pour lui prendre le ballon.");
                if(Random()<CarrierPressureFoulCaution)Caution(presser.id);
                BeginContactFall(carrier,presser,true);
                Restart("free-kick",carrier.side,carrier.position,3);return true;
            }
            var away=b.position-carrier.position;var toward=presser.position-carrier.position;
            var direction=Deflect((toward.Normalized+away.Normalized*.5f).Normalized,PokeSpread);
            m.metrics[carrier.side].pressuredLosses++;
            LooseBall(b.position,direction*(CarrierPressurePokeSpeed+Random()*CarrierPressurePokeExtra),BallRadius,.2f,presser.side,presser.id);
            carrier.controlTime=Math.Max(carrier.controlTime,.35f);
            presser.action="tackle";presser.actionKind=CarrierPressurePoke;presser.actionTime=TackleRecovery;presser.actionContactTime=0;
            presser.actionTarget=b.position;presser.actionHeight=BallRadius;presser.actionSequence++;presser.duelCooldown=.9f;
            Emit("tackle",presser.side,presser.id,Data(presser).name+" chipe le ballon sous la pression.");
            return true;
        }
    }
}

using System;

namespace Touchline.Core
{
    // Appels en profondeur : un attaquant proche de la ligne défensive part dans
    // le dos quand le porteur a le temps de lever la tête. Tant que la course
    // dure, il n'est plus retenu derrière la ligne : passe trop tardive ou
    // mauvais timing = hors-jeu, comme dans un vrai match.
    public sealed partial class MatchSimulation
    {
        const float RunBehindRatePerSecond=.38f;   // départs par seconde, attaquant moyen, porteur libre
        const float RunBehindSeconds=1.8f;         // durée d'une course (s)
        const float RunBehindDepth=9f;             // cible au-delà de la ligne (m)
        const float RunBehindTrigger=4f;           // distance max à la ligne pour partir (m)
        const float ThroughSpaceAhead=6f;          // un appel est servi dans l'espace devant lui (m)
        const float PassReadLagMin=.30f,PassReadLagPerPoint=.006f; // retard de lecture du passeur (s, s par point de vision manquant)
        const float ThroughMinSafety=.15f;          // risque accepté pour servir un appel (0–1)
        const float ThroughBonus=1.5f;               // attrait d'une passe dans la course (points de score)
        const float RunBehindOwnerSpace=3.5f;      // espace minimal autour du porteur (m)
        const float RunBehindOwnerDepth=6f;        // le porteur doit être au moins à cette distance de la ligne (m)
        const float RunBehindAttackDuty=1.4f,RunBehindSupportDuty=.6f; // fréquence selon la consigne du joueur (×)
        const float RunBehindBaseTendency=.5f;     // + placement/100 : 0,5× (placement 0) à 1,5× (placement 100)
        const float RunBehindReactionMin=.1f,RunBehindReactionPerPoint=.004f; // réaction du porteur (s, s par point de vision manquant)

        // Retourne true si p est en train de faire un appel ; met à jour sa cible.
        bool RunBehind(Actor p,Actor owner,string duty,ref Point q)
        {
            if(p.runBehind>0){q.x=Math.Min(48,Math.Max(q.x,OffsideLine(p.side)+RunBehindDepth));p.intent="run-behind";return true;}
            if(duty=="defend"||p.slot==0)return false;
            int dir=Direction(p.side);float line=OffsideLine(p.side),x=p.position.x*dir,ownerX=owner.position.x*dir;
            if(line>46||x<line-RunBehindTrigger||x>line||ownerX>line-RunBehindOwnerDepth||Space(owner.position,1-p.side)<RunBehindOwnerSpace)return false;
            float off=Skill(p,"positioning")*.01f;float rate=RunBehindRatePerSecond*(duty=="attack"?RunBehindAttackDuty:RunBehindSupportDuty)*(RunBehindBaseTendency+off);
            if(Random()>=rate*Step)return false;
            p.runBehind=RunBehindSeconds;
            // Le porteur voit l'appel et relève la tête : réaction 0,1 s (vision 100) à 0,5 s (vision 0).
            State.decision=Math.Min(State.decision,RunBehindReactionMin+(100-Skill(owner,"vision"))*RunBehindReactionPerPoint);q.x=Math.Min(48,line+RunBehindDepth);p.intent="run-behind";return true;
        }
    }
}

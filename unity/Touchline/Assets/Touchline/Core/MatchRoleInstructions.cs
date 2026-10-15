using System;

namespace Touchline.Core
{
    // Points d'entrée lus par le moteur pour les rôles et consignes
    // individuelles (TacticalRoles). Tous valent zéro / un pour le rôle
    // standard : une équipe sans rôle joue exactement comme avant.
    public sealed partial class MatchSimulation
    {
        public RoleEffect RoleOf(Actor p)
        {
            var t=Tactic(p.side);if(t?.withoutBall==null||p.slot<0||p.slot>=t.withoutBall.Length)return RoleEffect.Neutral;
            return TacticalRoles.Effect(t.withoutBall[p.slot]);
        }
        // Position de base (repère local, x vers le but adverse) corrigée
        // par le rôle : hauteur en possession, largeur, profondeur sans ballon.
        Point RoleShape(Actor p,Point q,bool possession)
        {
            if(p.slot==0)return q;var e=RoleOf(p);
            float shift=possession?e.advance:e.defendDepth;if(possession)q.z*=e.width;
            if(shift!=0)q.x=Mathx.Clamp(q.x+shift,-49,48);return q;
        }
        float RolePress(Actor p)=>RoleOf(p).press;
        float RoleMark(Actor p)=>RoleOf(p).mark;
        float RoleRunBehind(Actor p)=>RoleOf(p).runBehind;
        float RoleWidth(Actor p)=>RoleOf(p).width;
        float RoleShot(Actor p)=>RoleOf(p).shot;
        float RoleCarry(Actor p)=>RoleOf(p).carry;
        // Seuil de sécurité d'une passe abaissé (risque +) ou relevé (risque −).
        public const float RoleRiskSafety=.05f;
        float RoleSafetyShift(Actor p)=>RoleOf(p).risk*RoleRiskSafety;
        // Score ajouté à une passe : préférence du passeur, risque vers
        // l'avant, centres, et joueurs que l'équipe cherche (meneur, pivot).
        public const float RoleForwardRisk=.05f,RoleThroughRisk=1.5f;
        float RolePassScore(Actor from,Actor to,string kind,float forward,float distance)
        {
            var passer=RoleOf(from);var receiver=RoleOf(to);
            float score=passer.pass+passer.risk*(Math.Max(0,forward)*RoleForwardRisk+(kind=="through"?RoleThroughRisk:0));
            if(kind=="cross")score+=passer.cross;
            score+=receiver.target;
            if(kind=="cross"||distance>27)score+=receiver.aerialTarget;
            return score;
        }
        float RoleSweep(Actor keeper)=>RoleOf(keeper).sweep;
        float RoleKeeperDepth(Actor keeper)=>RoleOf(keeper).keeperDepth;
    }
}

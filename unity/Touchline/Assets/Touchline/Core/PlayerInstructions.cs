using System;

namespace Touchline.Core
{
    // Consignes individuelles : chacune change un comportement visible avec un
    // compromis. Coordonnées locales (attaque vers +X, gauche du joueur = +Z).
    public static class PlayerInstructions
    {
        public const string None="",StayWide="wide",GetForward="forward",TightMarking="tight";
        public static readonly string[] All={None,StayWide,GetForward,TightMarking};
        public static string Label(string key)=>key switch{StayWide=>"Rester large",GetForward=>"Se projeter",TightMarking=>"Marquage serré",_=>"Aucune"};
        public static string Tradeoff(string key)=>key switch{
            StayWide=>"Étire la défense et ouvre le couloir ; moins de soutien axial et une couverture plus large à défendre.",
            GetForward=>"Apporte un joueur de plus devant ; laisse un espace dans son dos à la perte du ballon.",
            TightMarking=>"Colle à l’attaquant de sa zone ; peut être attiré hors de position et ouvrir un intervalle.",
            _=>"Le joueur suit son poste et sa mission."};
        public static void Set(Tactic tactic,int slot,string key)
        {
            if(tactic?.withoutBall==null||slot<=0||slot>=tactic.withoutBall.Length)throw new ArgumentOutOfRangeException(nameof(slot),"Le gardien ne reçoit pas de consigne individuelle.");
            if(Array.IndexOf(All,key??"")<0)throw new ArgumentException("Consigne individuelle inconnue.");
            tactic.withoutBall[slot].instruction=key??"";
        }
    }

    public sealed partial class MatchSimulation
    {
        const string TightMarking=PlayerInstructions.TightMarking;
        static float FlankSign(Tactic t,int slot,Point q)
        {
            float board=50-t.withoutBall[slot].x;return board>1?1:board< -1?-1:q.z>=0?1:-1;
        }
        void InstructionWithBall(Actor p,Tactic t,ref Point q)
        {
            string instruction=Instruction(t,p.slot);
            if(instruction==PlayerInstructions.StayWide)q.z+=FlankSign(t,p.slot,q)*WideInstructionMetres;
            else if(instruction==PlayerInstructions.GetForward)q.x+=ForwardInstructionMetres;
        }
        void InstructionWithoutBall(Actor p,Tactic t,ref Point q)
        {
            string instruction=Instruction(t,p.slot);
            // Un joueur qui reste large ou haut sans ballon défend moins l'axe et se replie de moins loin.
            if(instruction==PlayerInstructions.StayWide)q.z+=FlankSign(t,p.slot,q)*WideDefensiveMetres;
            else if(instruction==PlayerInstructions.GetForward)q.x+=ForwardDefensiveMetres;
        }
    }
}

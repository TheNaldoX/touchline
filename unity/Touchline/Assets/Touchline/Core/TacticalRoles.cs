using System;
using System.Collections.Generic;
using System.Linq;

namespace Touchline.Core
{
    // Effet d'un rôle et des consignes individuelles sur le comportement d'un
    // joueur en match. Valeurs neutres = rôle standard (aucun effet), ce qui
    // garde la calibration des équipes sans rôle (IA adverses) inchangée.
    public struct RoleEffect
    {
        public float advance;      // m vers le but adverse, en possession
        public float defendDepth;  // m vers le but adverse, sans ballon (négatif = plus bas)
        public float width;        // multiplicateur de l'écartement en possession (1 = poste)
        public float runBehind;    // multiplicateur des appels en profondeur
        public float shot;         // multiplicateur de l'envie de frapper (1 = neutre)
        public float carry;        // points de score ajoutés à la conduite de balle
        public float pass;         // points de score ajoutés à chaque passe du joueur
        public float risk;         // -1…+1 : passes vers l'avant et ballons en profondeur
        public float cross;        // points de score ajoutés à un centre du joueur
        public float target;       // points ajoutés aux passes que reçoit ce joueur
        public float aerialTarget; // points ajoutés aux centres et longs ballons qu'il reçoit
        public float press;        // m ajoutés au rayon de pressing
        public float mark;         // 0–1 ajouté à l'attraction vers l'adversaire marqué
        public float sweep;        // s d'avance acceptée pour sortir sur un ballon (gardien)
        public float keeperDepth;  // multiplicateur de la distance du gardien à sa ligne
        public static RoleEffect Neutral=>new RoleEffect{width=1,runBehind=1,shot=1,keeperDepth=1};
    }
    public sealed class RoleDefinition
    {
        public string code,label,summary;public string[] positions;
        public RoleDefinition(string code,string label,string summary,params string[] positions){this.code=code;this.label=label;this.summary=summary;this.positions=positions;}
    }
    public static class TacticalRoles
    {
        static readonly string[] Wide={"LM","RM","LW","RW"};
        public static readonly RoleDefinition[] All=
        {
            new RoleDefinition("","Standard","Comportement habituel du poste.","GK","LB","RB","CB","DM","CM","LM","RM","AM","LW","RW","ST"),
            new RoleDefinition("sweeper","Gardien libéro","Joue haut et sort au-devant des ballons en profondeur ; exposé aux lobs.","GK"),
            new RoleDefinition("wing-back","Piston","Monte très haut et centre souvent ; laisse son couloir libre derrière.","LB","RB"),
            new RoleDefinition("inverted","Latéral inversé","Rentre au milieu en possession pour aider à construire ; couloir moins occupé.","LB","RB"),
            new RoleDefinition("stopper","Stoppeur","Sort au contact du joueur entre les lignes ; laisse un espace dans son dos.","CB"),
            new RoleDefinition("cover","Couverture","Reste en retrait pour couvrir la profondeur ; presse moins.","CB"),
            new RoleDefinition("anchor","Ancre","Reste devant la défense et ne monte pas ; moins de présence offensive.","DM"),
            new RoleDefinition("ball-winner","Récupérateur","Harcèle le porteur et marque de près ; plus de fautes.","DM","CM"),
            new RoleDefinition("deep-playmaker","Meneur reculé","Reçoit bas et oriente le jeu ; l'équipe passe par lui.","DM","CM"),
            new RoleDefinition("playmaker","Meneur de jeu","L'équipe le cherche, il tente la passe décisive ; moins de pressing.","CM","AM"),
            new RoleDefinition("box-to-box","Box-to-box","Couvre tout le terrain et arrive dans la surface ; se fatigue plus.","CM"),
            new RoleDefinition("shadow-striker","Attaquant de l'ombre","Attaque la surface depuis la deuxième ligne et frappe.","AM"),
            new RoleDefinition("winger","Ailier","Reste collé à la ligne, déborde et centre.","LM","RM","LW","RW"),
            new RoleDefinition("inside-forward","Attaquant intérieur","Rentre sur son bon pied pour frapper ; moins de centres.","LM","RM","LW","RW"),
            new RoleDefinition("target","Pivot","Point d'appui pour les centres et les longs ballons ; peu de courses.","ST"),
            new RoleDefinition("poacher","Renard des surfaces","Vit sur la dernière ligne et frappe dès qu'il peut ; participe peu au jeu.","ST"),
            new RoleDefinition("false9","Faux neuf","Décroche au milieu pour combiner ; moins présent dans la surface.","ST"),
        };
        public static IEnumerable<RoleDefinition> For(string position){position=FootballPositions.Canonical(position);return All.Where(r=>r.positions.Contains(position));}
        public static RoleDefinition Find(string code)=>All.FirstOrDefault(r=>r.code==(code??""))??All[0];
        public static string Label(string code)=>Find(code).label;
        public static bool Allowed(string position,string code)=>For(position).Any(r=>r.code==(code??""));

        // Consignes de frappe et de risque (-1, 0, +1), en plus de PlayerInstructions
        // (rester large, se projeter, marquage serré) : (clé, libellé -1, libellé +1, compromis).
        public static readonly (string key,string less,string more,string tradeOff)[] Instructions=
        {
            ("shooting","Tirer moins","Tirer plus","Plus : davantage de tentatives, souvent de moins bonne qualité."),
            ("risk","Jouer simple","Prendre des risques","Risques : plus de passes vers l'avant et en profondeur, plus de pertes."),
        };
        public static int Get(Slot s,string key)=>s==null?0:key=="shooting"?s.shooting:key=="risk"?s.risk:0;
        public static void Set(Slot s,string key,int value)
        {
            value=Math.Max(-1,Math.Min(1,value));
            if(key=="shooting")s.shooting=value;else if(key=="risk")s.risk=value;else throw new ArgumentException("Consigne inconnue.");
        }
        // Rôle et consignes sont portés par le poste sans ballon, comme PlayerInstructions.
        public static void SetRole(Tactic tactic,int slot,string code)
        {
            var position=tactic.withoutBall[slot].role;
            if(!Allowed(position,code))throw new ArgumentException("Ce rôle n'existe pas pour ce poste.");
            tactic.withoutBall[slot].playerRole=code??"";
        }

        // Valeurs de chaque rôle. Les ordres de grandeur restent ceux des
        // consignes collectives existantes (missions ±5 m, pressing 5–23 m).
        public static RoleEffect Effect(Slot instructions)
        {
            var e=RoleEffect.Neutral;if(instructions==null)return e;
            switch(instructions.playerRole??""){
                case "sweeper":e.sweep=.45f;e.keeperDepth=1.7f;e.risk=.3f;break;
                case "wing-back":e.advance=7;e.width=1.12f;e.cross=1.2f;e.runBehind=1.4f;e.defendDepth=1.5f;break;
                case "inverted":e.width=.42f;e.advance=1;e.pass=.6f;e.cross=-2;e.runBehind=.5f;break;
                case "stopper":e.defendDepth=3.5f;e.press=4;e.mark=.15f;break;
                case "cover":e.defendDepth=-3.5f;e.press=-3;e.mark=-.05f;break;
                case "anchor":e.advance=-6;e.defendDepth=-2;e.runBehind=.15f;e.press=-2;e.risk=-.5f;e.pass=.3f;e.shot=.7f;break;
                case "ball-winner":e.press=6;e.mark=.25f;e.defendDepth=1.5f;e.pass=-.3f;break;
                case "deep-playmaker":e.target=1.8f;e.pass=1;e.risk=.4f;e.advance=-2;e.carry=-.5f;break;
                case "playmaker":e.target=1.8f;e.pass=.8f;e.risk=.5f;e.carry=.3f;e.press=-2;break;
                case "box-to-box":e.advance=4.5f;e.runBehind=1.6f;e.shot=1.2f;e.press=2;break;
                case "shadow-striker":e.advance=6;e.runBehind=1.9f;e.shot=1.35f;e.pass=-.4f;break;
                case "winger":e.width=1.18f;e.cross=1.6f;e.carry=.5f;e.shot=.8f;break;
                case "inside-forward":e.width=.7f;e.shot=1.4f;e.carry=.6f;e.cross=-1.6f;e.runBehind=1.2f;break;
                case "target":e.aerialTarget=1.5f;e.target=.3f;e.advance=1;e.carry=-1;e.runBehind=.6f;break;
                case "poacher":e.advance=2;e.runBehind=1.6f;e.shot=1.35f;e.carry=-1.2f;e.pass=-.8f;e.press=-2;break;
                case "false9":e.advance=-9;e.runBehind=.3f;e.pass=.8f;e.target=.6f;e.shot=.75f;e.carry=.4f;break;
            }
            // Frappes : seuil de décision ×0,5 (moins) ou ×1,8 (plus) face à la meilleure passe.
            if(instructions.shooting<0)e.shot*=.5f;else if(instructions.shooting>0)e.shot*=1.8f;
            e.risk+=instructions.risk;
            return e;
        }
    }
}

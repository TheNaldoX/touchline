using System;
using System.Collections.Generic;
using System.Linq;

namespace Touchline.Core
{
    // Combinaisons sur coups de pied arrêtés. Valeurs par défaut ("mixed",
    // "auto", 0, tireurs vides) = comportement historique du moteur.
    [Serializable] public class SetPieceRoutine
    {
        public string corner="mixed";   // mixed | near | far | short
        public string freeKick="auto";  // auto | shoot | cross | short
        public string cornerTaker="",freeKickTaker="",penaltyTaker="";
        public int stayBack;            // joueurs de champ qui restent derrière sur corner (0 = auto, 2–4)
        public SetPieceRoutine Copy()=>(SetPieceRoutine)MemberwiseClone();
        public static readonly string[] CornerCodes={"mixed","near","far","short"};
        public static readonly string[] CornerLabels={"Variés","Premier poteau","Second poteau","Corner court"};
        public static readonly string[] CornerTradeOffs={"Centre vers le point de penalty, le meilleur jeu de tête est visé.","Ballon tendu au premier poteau : déviation rapide, le gardien a moins de temps, mais le défenseur du premier poteau intervient souvent.","Ballon long au second poteau : plus de duels gagnés loin du gardien, ballon plus lent.","Passe courte puis jeu placé : moins de duels aériens, garde le ballon."};
        public static readonly string[] FreeKickCodes={"auto","shoot","cross","short"};
        public static readonly string[] FreeKickLabels={"Au choix du tireur","Frapper au but","Centrer dans la surface","Jouer court"};
        public static readonly string[] FreeKickTradeOffs={"Frappe directe à moins de 27 m dans l'axe, sauf combinaison nettement meilleure.","Frappe toujours, jusqu'à 30 m et un peu plus excentré : plus de tirs, moins précis de loin.","Centre aérien vers les meilleurs joueurs de tête au lieu de frapper.","Passe courte : on garde le ballon mais on renonce à la frappe directe."};
    }

    public sealed partial class MatchSimulation
    {
        // Distances de frappe directe sur coup franc (m) : historique / "frapper".
        public const float FreeKickShotRange=27f,FreeKickShotWidth=18f,FreeKickExtendedRange=30f,FreeKickExtendedWidth=20f;
        // Points visés sur corner, en m depuis la ligne de but (profondeur) et l'axe (largeur).
        public const float NearPostDepth=5f,NearPostWidth=3f,FarPostDepth=7.5f,FarPostWidth=5f,PenaltySpotDepth=9.5f;
        // Corner court : distance (m) maximale du partenaire servi au tireur.
        public const float ShortCornerReach=14f;
        SetPieceRoutine Routine(int side)=>Tactic(side)?.setPieces??new SetPieceRoutine();

        // Tireur désigné, s'il est sur le terrain et disponible.
        Actor DesignatedTaker(Actor fallback)
        {
            var routine=Routine(State.restartSide);
            string id=State.phase=="corner"?routine.cornerTaker:State.phase=="penalty"?routine.penaltyTaker:State.phase=="free-kick"?routine.freeKickTaker:null;
            if(string.IsNullOrEmpty(id))return fallback;var chosen=Find(id);
            return chosen!=null&&!chosen.sentOff&&!chosen.injured&&!GroundedAction(chosen)&&chosen.side==State.restartSide&&chosen.slot>0?chosen:fallback;
        }
        // Placement des attaquants sur corner selon la combinaison. Retourne
        // false pour garder le placement historique ; ceux qui restent derrière
        // gardent leur position tactique q.
        bool CornerPosition(Actor p,Actor taker,ref Point q)
        {
            var m=State;if(m.phase!="corner"||p.side!=m.restartSide||p==taker||p.slot==0)return false;
            var routine=Routine(p.side);if(routine.corner=="mixed"&&routine.stayBack==0)return false;
            int attack=Direction(p.side);float near=Math.Sign(m.ball.position.z+.01f);
            var outfield=m.actors.Where(a=>a.side==p.side&&!a.sentOff&&a.slot>0&&a!=taker).ToList();
            int stay=routine.stayBack>0?Math.Min(routine.stayBack,outfield.Count-1):outfield.Count(a=>a.slot<=4);
            var back=outfield.OrderBy(a=>Tactic(p.side).withoutBall[a.slot].y).Take(stay).ToList();
            if(back.Contains(p))return true;
            var up=outfield.Where(a=>!back.Contains(a)).OrderByDescending(a=>Skill(a,"headingAccuracy")+Skill(a,"jumping")).ThenBy(a=>a.slot).ToList();
            int rank=up.IndexOf(p);
            var spots=new List<Point>();
            if(routine.corner=="near")spots.AddRange(new[]{new Point(52.5f-NearPostDepth,near*NearPostWidth),new Point(52.5f-7,near*1),new Point(52.5f-4,near*5.5f)});
            else if(routine.corner=="far")spots.AddRange(new[]{new Point(52.5f-FarPostDepth,-near*FarPostWidth),new Point(52.5f-5.5f,-near*3),new Point(52.5f-10,-near*7)});
            else if(routine.corner=="short")spots.Add(new Point(52.5f-3,near*26));
            spots.AddRange(new[]{new Point(52.5f-PenaltySpotDepth,0),new Point(52.5f-11,near*4),new Point(52.5f-11,-near*4),new Point(52.5f-6,0),new Point(52.5f-14,0),new Point(52.5f-17,near*8)});
            // Corner court : le moins fort de la tête vient jouer court.
            if(routine.corner=="short"){var shortMan=up.LastOrDefault();if(p==shortMan){q=new Point(attack*spots[0].x,spots[0].z);return true;}spots.RemoveAt(0);up.Remove(shortMan);rank=up.IndexOf(p);}
            var spot=spots[Math.Min(Math.Max(0,rank),spots.Count-1)];if(rank>=spots.Count)spot=new Point(spot.x-(rank-spots.Count+1)*2,spot.z);
            q=new Point(attack*spot.x,spot.z);return true;
        }
        // Point visé par le centre de corner.
        Point CornerAim(Actor taker)
        {
            int attack=Direction(taker.side);float near=Math.Sign(State.ball.position.z+.01f);var routine=Routine(taker.side);
            if(routine.corner=="near")return new Point(attack*(52.5f-NearPostDepth),near*NearPostWidth);
            if(routine.corner=="far")return new Point(attack*(52.5f-FarPostDepth),-near*FarPostWidth);
            return new Point(attack*43,0);
        }
        // Coup de pied arrêté joué court : passe au partenaire le plus proche.
        bool PlayShort(Actor taker,string phase)
        {
            var routine=Routine(taker.side);bool wanted=phase=="corner"?routine.corner=="short":phase=="free-kick"&&routine.freeKick=="short";if(!wanted)return false;
            Actor best=null;float closest=ShortCornerReach;
            foreach(var a in State.actors){if(a.sentOff||a==taker||a.side!=taker.side||a.slot==0||GroundedAction(a))continue;float d=Point.Distance(a.position,taker.position);if(d<2||d>=closest)continue;if(phase=="free-kick"&&a.position.x*Direction(taker.side)>OffsideLine(taker.side)+.1f)continue;closest=d;best=a;}
            if(best==null)return false;Pass(taker,best,"pass");return true;
        }
        // Frappe directe selon la combinaison choisie.
        bool DirectFreeKick(Actor taker)
        {
            var routine=Routine(taker.side);float d=Point.Distance(taker.position,new Point(Direction(taker.side)*52.5f,0));float z=Math.Abs(taker.position.z);
            if(routine.freeKick=="cross"||routine.freeKick=="short")return false;
            if(routine.freeKick=="shoot")return d<FreeKickExtendedRange&&z<FreeKickExtendedWidth;
            return d<FreeKickShotRange&&z<FreeKickShotWidth;
        }
        // Coup franc centré : vise les meilleurs joueurs de tête comme un corner.
        bool CrossFreeKick(Actor taker)
        {
            if(Routine(taker.side).freeKick!="cross"||State.ball.position.x*Direction(taker.side)<20)return false;
            Actor target=null;float best=float.MinValue;var aim=new Point(Direction(taker.side)*43,0);
            foreach(var a in State.actors){if(a.sentOff||GroundedAction(a)||a.side!=taker.side||a==taker||a.slot==0)continue;if(a.position.x*Direction(taker.side)>OffsideLine(taker.side)+.1f)continue;float score=Skill(a,"headingAccuracy")-Point.Distance(a.position,aim);if(score>best){best=score;target=a;}}
            if(target==null)return false;Pass(taker,target,"cross");return true;
        }
    }
}

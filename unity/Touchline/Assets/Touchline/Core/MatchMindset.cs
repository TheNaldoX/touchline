using System;
using System.Collections.Generic;
using System.Linq;

namespace Touchline.Core
{
    // Automatismes et état mental d'une équipe pendant un match. Toutes les
    // valeurs d'effet sont centrées sur 0 (−1 à +1) : 0 = moteur neutre, ce qui
    // garde la calibration et les anciennes sauvegardes identiques.
    [Serializable] public class TeamMindset
    {
        // Affichage (0–100) ; −1 = non mesuré (adversaire, match amical hors carrière).
        public float familiarity=-1,cohesion=-1;
        // Compréhension collective : familiarité tactique + vécu commun (−1 à +1).
        public float understanding;
        // Sang-froid (nervosité < 0) et engagement (intensité) par poste, −1 à +1.
        public float[] composure=new float[11],drive=new float[11];
        // Préparation physique (−1 à +1) : fatigue plus lente si positive.
        public float conditioning;
        // Coups de pied arrêtés répétés à l'entraînement (−1 à +1).
        public float setPieces;
        // Consigne criée depuis la touche : impulsion temporaire jusqu'à shoutUntil (secondes de match).
        public float shoutComposure,shoutDrive,shoutUntil=-1,lastShout=-1000;
        public string lastShoutKind="";
        // Causeries déjà tenues : "moment|cible|ton".
        public List<string> talks=new List<string>();
        // Observations de l'adjoint déjà publiées (clé|minute).
        public List<string> observations=new List<string>();
        public float nextObservation=-1;
        public static TeamMindset Neutral()=>new TeamMindset();
        public float Average(float[] values)=>values==null||values.Length==0?0:values.Average();
    }

    public sealed partial class MatchSimulation
    {
        // ---- Effets mesurables (constantes nommées) -------------------------
        // Nervosité −1 : bruit de décision +35 % ; sang-froid +1 : −35 %.
        public const float ComposureDecisionNoise=.35f;
        // Précision des passes : erreur ×(1 − compréhension × 0,30) ×(1 − sang-froid × 0,15).
        public const float UnderstandingPassPrecision=.30f,ComposurePassPrecision=.15f;
        // Tir cadré : ±5 points de probabilité entre nervosité et sang-froid maximaux.
        public const float ComposureShotAccuracy=.05f;
        // Engagement : rayon de pressing ±25 %, vitesse de pressing ±6 %, replis ±8 %.
        public const float DrivePressReach=.25f,DrivePressSpeed=.06f,DriveShapeSpeed=.08f;
        // Un engagement élevé coûte : fatigue jusqu'à +18 %.
        public const float DriveFatigue=.18f;
        // Préparation physique : fatigue ∓15 %.
        public const float ConditioningFatigue=.15f;
        // Coordination du pressing : rayon du second presseur ±30 % selon la compréhension.
        public const float UnderstandingCoverReach=.30f;
        // Défenseurs mal coordonnés : décalage de ligne jusqu'à 3 m (mètres) quand la compréhension est négative.
        public const float LineStaggerMetres=3f;
        // Replacement plus lent sans automatismes : jusqu'à 30 % de retard vers la cible de bloc.
        public const float ShapeLagShare=.30f;
        // Coups de pied arrêtés travaillés : erreur de livraison ∓25 %.
        public const float SetPieceDeliveryPrecision=.25f;
        // Consignes individuelles (mètres).
        public const float WideInstructionMetres=6f,WideDefensiveMetres=2f;
        public const float ForwardInstructionMetres=7f,ForwardDefensiveMetres=3f;
        public const float TightMarkingReach=16f,TightMarkingPull=.62f,DefaultMarkingReach=12f;
        // Le reste d'une causerie d'avant-match à la reprise (part conservée).
        public const float HalfTimeCarryOver=.5f;
        // Consignes criées : durée (minutes de match) et intervalle minimal entre deux cris.
        public const float ShoutMinutes=8f,ShoutCooldownMinutes=5f;
        // Adjoint : une observation au plus toutes les 12 minutes.
        public const float ObservationMinutes=12f;

        public PlayerData Player(string id)=>id!=null&&roster.TryGetValue(id,out var p)?p:null;
        TeamMindset Mind(int side)=>State.mindset!=null&&State.mindset.Length==2?State.mindset[side]:null;
        public float Understanding(int side)=>Mind(side)?.understanding??0;
        static float SlotValue(float[] values,int slot)=>values!=null&&slot>=0&&slot<values.Length?values[slot]:0;
        public float Composure(Actor p)
        {
            var mind=Mind(p.side);if(mind==null)return 0;
            float v=SlotValue(mind.composure,p.slot);if(State.clock<mind.shoutUntil)v+=mind.shoutComposure;
            return Mathx.Clamp(v,-1,1);
        }
        public float Drive(Actor p)
        {
            var mind=Mind(p.side);if(mind==null)return 0;
            float v=SlotValue(mind.drive,p.slot);if(State.clock<mind.shoutUntil)v+=mind.shoutDrive;
            return Mathx.Clamp(v,-1,1);
        }
        float DecisionNoiseFactor(Actor p)=>1-Composure(p)*ComposureDecisionNoise;
        float PassErrorFactor(Actor p)
        {
            float factor=(1-Understanding(p.side)*UnderstandingPassPrecision)*(1-Composure(p)*ComposurePassPrecision);
            var mind=Mind(p.side);
            if(mind!=null&&mind.setPieces!=0&&(State.phase=="corner"||State.phase=="free-kick"))factor*=1-mind.setPieces*SetPieceDeliveryPrecision;
            return factor;
        }
        float FatigueFactor(Actor p)
        {
            var mind=Mind(p.side);if(mind==null)return 1;
            return (1+Math.Max(0,Drive(p))*DriveFatigue)*(1-mind.conditioning*ConditioningFatigue);
        }
        // Part fixe du décalage par poste : un défenseur central (poste 2) décroche
        // franchement et couvre les attaquants du hors-jeu, les autres à peine.
        static float StaggerPattern(int slot)=>slot==2?1f:slot==3?.35f:.15f;
        float LineStagger(Actor p)
        {
            float u=Understanding(p.side);return u<0?-u*LineStaggerMetres*StaggerPattern(p.slot):0;
        }
        public static string Instruction(Tactic t,int slot)=>t?.withoutBall!=null&&slot>=0&&slot<t.withoutBall.Length?t.withoutBall[slot].instruction??"":"";

        // ---- Consignes depuis la touche (côté 0 : club dirigé) ----------------
        public static readonly string[] ShoutKinds={"focus","press","calm","forward"};
        public static string ShoutLabel(string kind)=>kind switch{"focus"=>"Concentration !","press"=>"Pressez !","calm"=>"Calmez le jeu","forward"=>"Allez de l’avant !",_=>kind};
        public static string ShoutTradeoff(string kind)=>kind switch{
            "focus"=>"Sang-froid en hausse quelques minutes : moins de décisions hâtives.",
            "press"=>"Pressing plus large et plus rapide, mais la fatigue monte plus vite.",
            "calm"=>"Rythme plus patient et sang-froid, au prix d’un peu d’intensité.",
            "forward"=>"Mentalité plus offensive et engagement ; davantage d’espaces derrière.",_=>""};
        public bool CanShout(int side=0)
        {
            var mind=Mind(side);return mind!=null&&!State.finished&&!State.halfTime&&State.clock-mind.lastShout>=ShoutCooldownMinutes*State.SecondsPerMinute;
        }
        public void Shout(string kind,int side=0)
        {
            var mind=Mind(side);if(mind==null)throw new InvalidOperationException("Les consignes depuis la touche ne sont disponibles qu’en match de carrière.");
            if(!ShoutKinds.Contains(kind))throw new ArgumentException("Consigne inconnue.");
            if(!CanShout(side))throw new InvalidOperationException("Laissez à vos joueurs le temps d’appliquer la consigne précédente.");
            var t=Tactic(side);float composure=0,drive=0;
            if(kind=="focus")composure=.25f;
            else if(kind=="press")drive=.35f;
            else if(kind=="calm"){composure=.2f;drive=-.15f;t.tempo=Mathx.Clamp(t.tempo-.15f,0,1);}
            else{drive=.2f;t.mentality=Mathx.Clamp(t.mentality+.15f,0,1);}
            mind.shoutComposure=composure;mind.shoutDrive=drive;mind.lastShout=State.clock;mind.shoutUntil=State.clock+ShoutMinutes*State.SecondsPerMinute;mind.lastShoutKind=kind;
            Emit("shout",side,null,"Depuis la touche : « "+ShoutLabel(kind)+" »");
        }

        // ---- Mi-temps : les émotions retombent, l'IA parle à son groupe -------
        void MindsetHalfTime()
        {
            if(State.mindset==null||State.mindset.Length!=2)return;
            for(int side=0;side<2;side++){var mind=State.mindset[side];if(mind==null)continue;
                for(int i=0;i<11;i++){if(i<mind.composure.Length)mind.composure[i]*=HalfTimeCarryOver;if(i<mind.drive.Length)mind.drive[i]*=HalfTimeCarryOver;}
                mind.shoutUntil=-1;}
            // L'adversaire réagit au score comme un entraîneur : il secoue un groupe mené,
            // rassure un groupe qui mène largement.
            var away=State.mindset[1];if(away==null)return;int margin=State.score[1]-State.score[0];
            for(int i=0;i<11;i++){
                if(margin<0){away.drive[i]=Mathx.Clamp(away.drive[i]+.3f,-1,1);away.composure[i]=Mathx.Clamp(away.composure[i]-.05f,-1,1);}
                else if(margin>=2){away.composure[i]=Mathx.Clamp(away.composure[i]+.1f,-1,1);away.drive[i]=Mathx.Clamp(away.drive[i]-.05f,-1,1);}
            }
            Emit("opponent-talk",1,null,margin<0?"Dans l’autre vestiaire, le ton monte : l’adversaire revient avec plus d’intensité.":margin>=2?"L’adversaire revient détendu, peut-être trop.":"L’adversaire revient avec le même plan.");
        }
        // Un remplaçant prend l'état moyen du groupe (il a entendu la causerie sur le banc).
        void MindsetSubstitution(int side,int slot)
        {
            var mind=Mind(side);if(mind==null||slot<0||slot>=mind.composure.Length||slot>=mind.drive.Length)return;
            mind.composure[slot]=mind.composure.Where((_,i)=>i!=slot).DefaultIfEmpty(0).Average();
            mind.drive[slot]=mind.drive.Where((_,i)=>i!=slot).DefaultIfEmpty(0).Average();
        }
        // Observations de l'adjoint pendant le match (aucun tirage aléatoire).
        void MindsetObservations()
        {
            var mind=Mind(0);if(mind==null||State.clock<mind.nextObservation)return;
            mind.nextObservation=State.clock+ObservationMinutes*State.SecondsPerMinute;
            if(State.clock<10*State.SecondsPerMinute)return;
            foreach(var o in MatchAssistant.Observe(State)){
                if(mind.observations.Any(x=>x.StartsWith(o.key+"|",StringComparison.Ordinal)&&State.clock-float.Parse(x.Substring(o.key.Length+1),System.Globalization.CultureInfo.InvariantCulture)<30*State.SecondsPerMinute))continue;
                mind.observations.Add(o.key+"|"+State.clock.ToString("R",System.Globalization.CultureInfo.InvariantCulture));
                Emit("assistant",0,null,o.text);return;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

namespace Touchline.Core
{
    // Préparation d'une équipe pour un match : effets de l'entraînement et
    // des causeries. Valeurs neutres = aucun effet.
    [Serializable] public class MatchPreparation
    {
        public float skill;       // multiplicateur additionnel des attributs (0,02 = +2 %), familiarité tactique
        public float talk;        // idem, cumul des causeries de ce match
        public float fatigue=1;   // multiplicateur de la fatigue en match (condition physique travaillée)
        public float setPiece;    // part de l'erreur de centre retirée sur coup de pied arrêté (0–0,5)
        public float setPieceSkill; // points ajoutés à la frappe de coup franc
        public string talks="";   // moments déjà utilisés : "prematch;halftime;fulltime;"
    }
    [Serializable] public class IndividualTraining { public string player,focus; }
    [Serializable] public class TrainingPlan
    {
        public int version;       // 0 = plan absent d'une ancienne sauvegarde, remis aux valeurs de départ
        public string focus="balanced",familiarFormation="";
        public float familiarity=TrainingFocus.FamiliarityStart,sharpness=TrainingFocus.Neutral,conditioning=TrainingFocus.Neutral;
        public List<IndividualTraining> individual=new List<IndividualTraining>();
    }
    [Serializable] public class SavedTactic { public string name="";public bool used;public Tactic tactic; }

    public static class TrainingFocus
    {
        public const float FamiliarityStart=60,Neutral=50;
        // Familiarité (30–100) : à 100, +2 % sur tous les attributs ; à 30, −1,5 %.
        public const float FamiliarityMin=30,FamiliarityEffect=.02f,FamiliarityRange=40;
        // Changement du système de jeu habituel : familiarité perdue (points).
        public const float FormationChangeCost=25;
        // Condition (0–100) : à 100 la fatigue en match est réduite de 20 %.
        public const float ConditioningEffect=.004f;
        // Automatismes sur coups de pied arrêtés (0–100) : à 100, −40 % d'erreur de centre, +7,5 points de frappe.
        public const float SharpnessError=.008f,SharpnessShot=.15f;
        // Travail individuel : progression multipliée.
        public const float IndividualGrowth=1.35f;
        public static readonly string[] Codes={"balanced","tactical","physical","setpieces","recovery"};
        public static readonly string[] Labels={"Équilibrée","Tactique","Physique","Coups de pied arrêtés","Récupération"};
        public static readonly string[] TradeOffs={
            "Un peu de tout : familiarité en légère hausse, aucun excès.",
            "Familiarité tactique +2,5/jour (jusqu'à +2 % en match) ; progression et récupération un peu ralenties.",
            "Condition +2/jour (moins de fatigue en match) ; récupération plus lente, risque de blessure +30 %.",
            "Automatismes +3/jour (centres et coups francs plus précis) ; peu de travail du reste.",
            "Récupération ×1,6, blessures ÷2 ; progression ÷2, les automatismes s'émoussent."};
        public static readonly string[] IndividualCodes={"","finishing","passing","defending","physical","aerial"};
        public static readonly string[] IndividualLabels={"Aucun","Finition","Passes","Défense","Physique","Jeu de tête"};
        // (récupération, blessure, progression, familiarité/j, automatismes/j, condition/j) par thème.
        public static (float recovery,float injury,float growth,float familiarity,float sharpness,float conditioning) Effects(string focus)
        {
            switch(focus){
                case "tactical":return (.9f,.9f,.85f,2.5f,-.3f,-.3f);
                case "physical":return (.75f,1.3f,1f,.3f,-.3f,2f);
                case "setpieces":return (.95f,.9f,.9f,.6f,3f,-.3f);
                case "recovery":return (1.6f,.5f,.5f,-.2f,-.4f,-.6f);
                default:return (1f,1f,1f,.8f,0,0);
            }
        }
        public static string Label(string code){int i=Array.IndexOf(Codes,code);return i<0?Labels[0]:Labels[i];}
    }

    // Causeries : un ton par moment (avant-match, mi-temps, fin de match).
    public static class TeamTalks
    {
        public static readonly string[] Moments={"prematch","halftime","fulltime"};
        public static readonly string[] Tones={"calm","encourage","demand","fire"};
        public static readonly string[] ToneLabels={"Rester calme","Encourager","Exiger plus","Enflammer"};
        public static readonly string[] ToneHints={
            "Rassure ; utile quand on mène ou face à plus fort.",
            "Donne confiance ; utile pour l'outsider ou quand on est mené.",
            "Met la pression ; efficace si le favori traîne, mal vécu quand tout va bien.",
            "Survolte ; fort avant le match ou mené, risqué quand on mène."};
        // Écart de niveau (note moyenne) à partir duquel une équipe est favorite.
        public const float FavouriteGap=2;
        // Effet (multiplicateur additionnel des attributs) et moral, selon le
        // contexte : écart au score (nous − eux) et écart de niveau.
        public static (float skill,float morale,string reaction) Evaluate(string moment,string tone,int goalDiff,float strengthGap)
        {
            bool favourite=strengthGap>=FavouriteGap,underdog=strengthGap<=-FavouriteGap;
            bool leading=goalDiff>0,losing=goalDiff<0;
            if(moment=="fulltime"){
                switch(tone){
                    case "calm":return (0,1,"Le groupe apprécie votre sérénité.");
                    case "encourage":return (0,losing?3:1.5f,losing?"Les joueurs relèvent la tête.":"Les joueurs savourent.");
                    case "demand":return leading?(0,-3,"Les joueurs ne comprennent pas vos reproches après une victoire."):favourite?(0,1,"Message reçu : ce n'était pas assez."):(0,-2,"Les joueurs trouvent le reproche injuste.");
                    default:return leading?(0,2,"Le vestiaire exulte."):(0,-1,"Le ton passe mal après ce résultat.");
                }
            }
            switch(tone){
                case "calm":return leading?(.012f,1,"Les joueurs gardent la tête froide."):losing?(-.005f,0,"Les joueurs semblent trop détendus."):underdog?(.01f,1,"La pression retombe."):(.005f,.5f,"Le groupe reste concentré.");
                case "encourage":return underdog||losing?(.018f,2,"Les joueurs y croient."):(.006f,1,"Le groupe est confiant.");
                case "demand":return losing&&!underdog?(.03f,-1,"Piqués au vif, les joueurs réagissent."):!leading&&favourite?(.018f,-.5f,"Les joueurs haussent le ton."):leading?(-.015f,-2,"Les joueurs se crispent sous les reproches."):(-.01f,-1.5f,"Les joueurs se sentent injustement visés.");
                default:return moment=="prematch"?(.02f,1,"Le vestiaire est survolté."):losing?(.025f,0,"Les joueurs repartent à l'assaut."):leading?(-.01f,0,"Les joueurs se découvrent trop."):(.008f,0,"L'énergie remonte.");
            }
        }
    }

    public sealed partial class MatchSimulation
    {
        MatchPreparation Preparation(int side)=>State.preparation!=null&&side>=0&&side<State.preparation.Length?State.preparation[side]:null;
        float PreparationSkill(int side){var p=Preparation(side);return p==null?0:p.skill+p.talk;}
        float PreparationFatigue(int side){var p=Preparation(side);return p==null||p.fatigue<=0?1:p.fatigue;}
        float PreparationSetPiece(int side)=>Mathx.Clamp(Preparation(side)?.setPiece??0,-.3f,.5f);
        float SetPieceSkillBonus(int side)=>Preparation(side)?.setPieceSkill??0;
        public MatchPreparation PreparationFor(int side){State.preparation??=new MatchPreparation[0];if(State.preparation.Length<2){var grown=new MatchPreparation[2];for(int i=0;i<State.preparation.Length;i++)grown[i]=State.preparation[i];State.preparation=grown;}return State.preparation[side]??=new MatchPreparation();}
    }

    public partial class Career
    {
        public SavedTactic[] savedTactics;
        public TrainingPlan Training()
        {
            life.trainingPlan??=new TrainingPlan();var plan=life.trainingPlan;
            if(plan.version==0){var fresh=new TrainingPlan{version=1,familiarFormation=tactic.formation};life.trainingPlan=plan=fresh;}
            plan.individual??=new List<IndividualTraining>();if(string.IsNullOrEmpty(plan.familiarFormation))plan.familiarFormation=tactic.formation;
            return plan;
        }
        public void SetTrainingFocus(string focus)
        {
            OffPitch();if(!TrainingFocus.Codes.Contains(focus))throw new ArgumentException("Thème d'entraînement inconnu.");Training().focus=focus;
        }
        public void SetIndividualTraining(string player,string focus)
        {
            OffPitch();if(!TrainingFocus.IndividualCodes.Contains(focus??""))throw new ArgumentException("Travail individuel inconnu.");
            var plan=Training();plan.individual.RemoveAll(x=>x.player==player);if(!string.IsNullOrEmpty(focus))plan.individual.Add(new IndividualTraining{player=player,focus=focus});
        }
        public string IndividualTrainingOf(string player)=>Training().individual.FirstOrDefault(x=>x.player==player)?.focus??"";
        // Évolution quotidienne de la familiarité, des automatismes et de la condition.
        void TrainingDay()
        {
            var plan=Training();var fx=TrainingFocus.Effects(plan.focus);
            if(plan.familiarFormation!=tactic.formation){plan.familiarity=Math.Max(TrainingFocus.FamiliarityMin,plan.familiarity-TrainingFocus.FormationChangeCost);plan.familiarFormation=tactic.formation;}
            plan.familiarity=Mathx.Clamp(plan.familiarity+fx.familiarity,TrainingFocus.FamiliarityMin,100);
            float Drift(float value,float delta)=>Mathx.Clamp(delta!=0?value+delta:value+(TrainingFocus.Neutral-value)*.05f,0,100);
            plan.sharpness=Drift(plan.sharpness,fx.sharpness);plan.conditioning=Drift(plan.conditioning,fx.conditioning);
        }
        float TrainingRecovery(bool rest)=>rest?1:TrainingFocus.Effects(Training().focus).recovery;
        float TrainingInjuryRisk()=>TrainingFocus.Effects(Training().focus).injury;
        float TrainingGrowth(string player)=>TrainingFocus.Effects(Training().focus).growth*(string.IsNullOrEmpty(IndividualTrainingOf(player))?1:TrainingFocus.IndividualGrowth);
        // Effets de l'entraînement sur le match du club (côté 0).
        public static void ApplyTraining(MatchSimulation simulation,TrainingPlan plan)
        {
            var prep=simulation.PreparationFor(0);
            prep.skill=(plan.familiarity-TrainingFocus.FamiliarityStart)/TrainingFocus.FamiliarityRange*TrainingFocus.FamiliarityEffect;
            prep.fatigue=1-(plan.conditioning-TrainingFocus.Neutral)*TrainingFocus.ConditioningEffect;
            prep.setPiece=(plan.sharpness-TrainingFocus.Neutral)*TrainingFocus.SharpnessError;
            prep.setPieceSkill=(plan.sharpness-TrainingFocus.Neutral)*TrainingFocus.SharpnessShot;
        }
        // Moment de causerie disponible maintenant, ou null.
        public string TalkMoment()
        {
            if(match==null)return null;var done=match.preparation!=null&&match.preparation.Length>0&&match.preparation[0]!=null?match.preparation[0].talks??"":"";
            string moment=match.finished?"fulltime":match.halfTime?"halftime":match.period==1&&match.clock<1?"prematch":null;
            return moment==null||done.Contains(moment+";")?null:moment;
        }
        public string TeamTalk(Database db,string tone)
        {
            if(!TeamTalks.Tones.Contains(tone))throw new ArgumentException("Ton inconnu.");
            string moment=TalkMoment();if(moment==null)throw new InvalidOperationException("Aucune causerie possible maintenant.");
            var sim=new MatchSimulation(db,match);var prep=sim.PreparationFor(0);
            float ours=(float)match.actors.Take(11).Where(a=>!a.sentOff).Average(a=>db.Find(a.id).rating),theirs=(float)match.actors.Skip(11).Where(a=>!a.sentOff).Average(a=>db.Find(a.id).rating);
            var outcome=TeamTalks.Evaluate(moment,tone,match.score[0]-match.score[1],ours-theirs);
            prep.talk+=outcome.skill;prep.talks+=moment+";";
            if(life!=null)foreach(var p in life.players)if(match.actors.Take(11).Any(a=>a.id==p.id)||match.used.Contains(p.id))p.morale=Mathx.Clamp(p.morale+outcome.morale,10,100);
            return outcome.reaction;
        }
        // Plans tactiques enregistrés (3 emplacements), applicables en match.
        public const int TacticSlots=3;
        public SavedTactic[] TacticPlans()
        {
            if(savedTactics==null||savedTactics.Length!=TacticSlots){var grown=new SavedTactic[TacticSlots];for(int i=0;i<TacticSlots;i++)grown[i]=savedTactics!=null&&i<savedTactics.Length?savedTactics[i]:null;savedTactics=grown;}
            for(int i=0;i<TacticSlots;i++)savedTactics[i]??=new SavedTactic();return savedTactics;
        }
        public void SaveTacticPlan(int slot,string name)
        {
            var plans=TacticPlans();if(slot<0||slot>=TacticSlots)throw new ArgumentOutOfRangeException(nameof(slot));
            plans[slot]=new SavedTactic{name=string.IsNullOrWhiteSpace(name)?"Plan "+(char)('A'+slot):name.Trim(),used=true,tactic=tactic.Copy()};
        }
        public void ApplyTacticPlan(Database db,int slot)
        {
            var plans=TacticPlans();if(slot<0||slot>=TacticSlots||!plans[slot].used||plans[slot].tactic==null)throw new InvalidOperationException("Cet emplacement est vide.");
            var plan=plans[slot].tactic.Copy();if(plan.formation!=tactic.formation)ChangeTacticalFormation(db,plan.formation);
            var current=tactic;var keepSlots=current.withoutBall.Length==plan.withoutBall?.Length&&current.withBall.Length==plan.withBall?.Length;
            current.width=plan.width;current.line=plan.line;current.tempo=plan.tempo;current.pressing=plan.pressing;current.directness=plan.directness;current.defensiveWidth=plan.defensiveWidth;current.mentality=plan.mentality;
            current.counterPress=plan.counterPress;current.counterAttack=plan.counterAttack;current.workIntoBox=plan.workIntoBox;current.attackFocus=plan.attackFocus;current.crossing=plan.crossing;current.setPieces=plan.setPieces??new SetPieceRoutine();
            if(keepSlots){current.withBall=plan.withBall;current.withoutBall=plan.withoutBall;}
            BindMatchTactic();
        }
    }
}

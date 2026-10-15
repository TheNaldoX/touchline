using System;
using System.Collections.Generic;
using System.Linq;

namespace Touchline.Core
{
    [Serializable] public class SavedTactic { public string name="";public bool used;public Tactic tactic; }
    [Serializable] public class IndividualTraining { public string player,focus; }

    // Travail individuel : en plus du thème collectif (TacticalPreparation),
    // un joueur en progression peut recevoir un programme spécifique.
    public static class IndividualTrainingFocus
    {
        // Progression multipliée pour le joueur suivi (le moral et la fraîcheur restent ceux du groupe).
        public const float Growth=1.35f;
        public static readonly string[] Codes={"","finishing","passing","defending","physical","aerial"};
        public static readonly string[] Labels={"Aucun","Finition","Passes","Défense","Physique","Jeu de tête"};
    }

    public partial class Career
    {
        // Plans tactiques enregistrés (3 emplacements), applicables en match en une touche.
        public SavedTactic[] savedTactics;
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
        // Applique système, consignes collectives et individuelles, rôles et coups
        // de pied arrêtés. En match, le changement de système replace les joueurs
        // comme depuis l'écran tactique.
        public void ApplyTacticPlan(Database db,int slot)
        {
            var plans=TacticPlans();if(slot<0||slot>=TacticSlots||!plans[slot].used||plans[slot].tactic==null)throw new InvalidOperationException("Cet emplacement est vide.");
            var plan=plans[slot].tactic.Copy();if(plan.formation!=tactic.formation)ChangeTacticalFormation(db,plan.formation);
            var current=tactic;bool sameShape=current.withoutBall.Length==plan.withoutBall?.Length&&current.withBall.Length==plan.withBall?.Length;
            current.width=plan.width;current.line=plan.line;current.tempo=plan.tempo;current.pressing=plan.pressing;current.directness=plan.directness;current.defensiveWidth=plan.defensiveWidth;current.mentality=plan.mentality;
            current.counterPress=plan.counterPress;current.counterAttack=plan.counterAttack;current.workIntoBox=plan.workIntoBox;current.attackFocus=plan.attackFocus;current.crossing=plan.crossing;current.setPieces=plan.setPieces??new SetPieceRoutine();
            if(sameShape){current.withBall=plan.withBall;current.withoutBall=plan.withoutBall;}
            BindMatchTactic();
        }

        public void SetIndividualTraining(string player,string focus)
        {
            OffPitch();if(!IndividualTrainingFocus.Codes.Contains(focus??""))throw new ArgumentException("Travail individuel inconnu.");
            if(life.players.All(p=>p.id!=player))throw new ArgumentException("Ce joueur n'est pas dans l'effectif.");
            life.individualTraining??=new List<IndividualTraining>();life.individualTraining.RemoveAll(x=>x.player==player);
            if(!string.IsNullOrEmpty(focus))life.individualTraining.Add(new IndividualTraining{player=player,focus=focus});
        }
        public string IndividualTrainingOf(string player)=>life?.individualTraining?.FirstOrDefault(x=>x.player==player)?.focus??"";
        float IndividualGrowth(string player)=>string.IsNullOrEmpty(IndividualTrainingOf(player))?1:IndividualTrainingFocus.Growth;
    }
}

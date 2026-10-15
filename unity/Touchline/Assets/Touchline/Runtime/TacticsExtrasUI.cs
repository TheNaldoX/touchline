using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Touchline.Core;

namespace Touchline
{
    // Rôles et consignes individuelles, coups de pied arrêtés, plans
    // enregistrés et travail individuel à l'entraînement.
    public sealed partial class TouchlineApp
    {
        // Boutons côte à côte ; le choix actif est mis à jour sans reconstruire l'écran.
        static Button[] Segmented(VisualElement parent,string[] labels,int selected,Action<int> apply,string name=null)
        {
            var row=Row(parent,"instruction-options");if(name!=null)row.name=name;var buttons=new Button[labels.Length];
            for(int i=0;i<labels.Length;i++){int index=i;buttons[i]=Button(row,labels[i],()=>{apply(index);for(int j=0;j<buttons.Length;j++)buttons[j].EnableInClassList("active",j==index);});buttons[i].EnableInClassList("active",i==selected);if(name!=null)buttons[i].name=name+"-"+i;}
            return buttons;
        }
        void TacticChanged(){Career.BindMatchTactic();Save();}

        void RolesTab()
        {
            var scroll=Scroll(content);tacticsOuter=scroll;var chips=Row(scroll,"instruction-options");chips.name="role-slots";
            for(int i=0;i<11;i++){int index=i;var p=Database.Find(Lineup[i]);var slot=Career.tactic.withoutBall[i];
                var b=Button(chips,FrenchFootballPositions.Short(Career.tactic.withoutBall[i].role)+" "+MatchPlayerLabels.Surname(p.name,p.nationality)+(string.IsNullOrEmpty(slot.playerRole)?"":" ·"),()=>{selectedSlot=index;Build();});b.name="role-slot-"+i;b.EnableInClassList("active",i==selectedSlot);}
            var s=Career.tactic.withoutBall[selectedSlot];var withBall=Career.tactic.withBall[selectedSlot];string position=Career.tactic.withoutBall[selectedSlot].role;var player=Database.Find(Lineup[selectedSlot]);
            var roleCard=Card(scroll,"instruction-card");Text(roleCard,"Rôle · "+FrenchFootballPositions.Label(position)+" · "+player.name,"section-title");
            var roles=TacticalRoles.For(position).ToArray();var summary=new Label(TacticalRoles.Find(s.playerRole).summary);summary.AddToClassList("muted");
            Segmented(roleCard,roles.Select(r=>r.label).ToArray(),Math.Max(0,Array.FindIndex(roles,r=>r.code==(s.playerRole??""))),i=>{TacticalRoles.SetRole(Career.tactic,selectedSlot,roles[i].code);summary.text=roles[i].summary;TacticChanged();},"role-choice");roleCard.Add(summary);
            if(selectedSlot==0){Text(scroll,"Le gardien n'a pas de consignes individuelles en dehors de son rôle.","muted");return;}
            var dutyCard=Card(scroll,"instruction-card");Text(dutyCard,"Mission","section-title");var duties=new[]{"defend","support","attack"};
            Segmented(dutyCard,new[]{"Défense","Soutien","Attaque"},Math.Max(0,Array.IndexOf(duties,withBall.duty)),i=>{withBall.duty=duties[i];TacticChanged();},"role-duty");
            Text(dutyCard,"Défense : reste derrière le ballon. Attaque : prend la profondeur dès que possible.","muted");
            var individual=Card(scroll,"instruction-card");Text(individual,"Consigne individuelle","section-title");var individualHint=new Label(PlayerInstructions.Tradeoff(s.instruction??""));individualHint.AddToClassList("muted");
            Segmented(individual,PlayerInstructions.All.Select(PlayerInstructions.Label).ToArray(),Math.Max(0,Array.IndexOf(PlayerInstructions.All,s.instruction??"")),i=>{PlayerInstructions.Set(Career.tactic,selectedSlot,PlayerInstructions.All[i]);individualHint.text=PlayerInstructions.Tradeoff(PlayerInstructions.All[i]);TacticChanged();},"instruction-individual");individual.Add(individualHint);
            foreach(var instruction in TacticalRoles.Instructions){var key=instruction.key;var card=Card(scroll,"instruction-card");Text(card,key=="shooting"?"Frappes":"Passes","section-title");
                int current=TacticalRoles.Get(s,key);
                Segmented(card,new[]{instruction.less,"Normal",instruction.more},current+1,i=>{TacticalRoles.Set(s,key,i-1);TacticChanged();},"instruction-"+key);Text(card,instruction.tradeOff,"muted");}
        }

        void SetPiecesTab()
        {
            var scroll=Scroll(content);tacticsOuter=scroll;var routine=Career.tactic.setPieces??=new SetPieceRoutine();
            var corner=Card(scroll,"instruction-card");Text(corner,"Corners","section-title");var cornerHint=new Label(SetPieceRoutine.CornerTradeOffs[Math.Max(0,Array.IndexOf(SetPieceRoutine.CornerCodes,routine.corner))]);cornerHint.AddToClassList("muted");
            Segmented(corner,SetPieceRoutine.CornerLabels,Math.Max(0,Array.IndexOf(SetPieceRoutine.CornerCodes,routine.corner)),i=>{routine.corner=SetPieceRoutine.CornerCodes[i];cornerHint.text=SetPieceRoutine.CornerTradeOffs[i];TacticChanged();},"corner-routine");corner.Add(cornerHint);
            var back=Card(scroll,"instruction-card");Text(back,"Restent derrière sur corner","section-title");var counts=new[]{0,2,3,4};
            Segmented(back,new[]{"Auto","2","3","4"},Math.Max(0,Array.IndexOf(counts,routine.stayBack)),i=>{routine.stayBack=counts[i];TacticChanged();},"corner-stay");
            Text(back,"Moins de joueurs derrière : plus de monde dans la surface, plus de risque en contre. Auto : la défense reste.","muted");
            var free=Card(scroll,"instruction-card");Text(free,"Coups francs offensifs","section-title");var freeHint=new Label(SetPieceRoutine.FreeKickTradeOffs[Math.Max(0,Array.IndexOf(SetPieceRoutine.FreeKickCodes,routine.freeKick))]);freeHint.AddToClassList("muted");
            Segmented(free,SetPieceRoutine.FreeKickLabels,Math.Max(0,Array.IndexOf(SetPieceRoutine.FreeKickCodes,routine.freeKick)),i=>{routine.freeKick=SetPieceRoutine.FreeKickCodes[i];freeHint.text=SetPieceRoutine.FreeKickTradeOffs[i];TacticChanged();},"freekick-routine");free.Add(freeHint);
            var takers=Card(scroll,"instruction-card");Text(takers,"Tireurs","section-title");var outfield=Lineup.Skip(1).Select(Database.Find).Where(p=>p!=null).ToList();var names=new List<string>{"Automatique"};names.AddRange(outfield.Select(p=>p.name));
            void Taker(string label,string current,Action<string> apply,string name){var field=new DropdownField(label,names,Math.Max(0,outfield.FindIndex(p=>p.id==current)+1)){name=name};takers.Add(field);field.RegisterValueChangedCallback(_=>{apply(field.index<=0?"":outfield[field.index-1].id);TacticChanged();});}
            Taker("Corners",routine.cornerTaker,v=>routine.cornerTaker=v,"taker-corner");Taker("Coups francs",routine.freeKickTaker,v=>routine.freeKickTaker=v,"taker-freekick");Taker("Penalties",routine.penaltyTaker,v=>routine.penaltyTaker=v,"taker-penalty");
            Text(takers,"Automatique : le meilleur centreur ou frappeur présent. Un tireur absent est remplacé automatiquement.","muted");
            if(Career.life?.preparation!=null)Text(scroll,"Automatismes travaillés : "+Career.life.preparation.setPieces.ToString("0")+" / 100 · thème « Coups de pied arrêtés » à l'entraînement (Bureau) pour mieux les livrer.","muted");
        }

        void PlansTab()
        {
            var scroll=Scroll(content);tacticsOuter=scroll;var plans=Career.TacticPlans();
            Text(scroll,"Enregistrez jusqu'à trois tactiques complètes (système, consignes, rôles, coups de pied arrêtés). En match, une touche sur A, B ou C sous la mentalité.","muted");
            for(int i=0;i<plans.Length;i++){int index=i;var plan=plans[i];var card=Card(scroll,"instruction-card");char letter=(char)('A'+i);
                Text(card,"Plan "+letter+(plan.used?" · "+plan.name:" · vide"),"section-title");
                if(plan.used&&plan.tactic!=null)Text(card,plan.tactic.formation+" · mentalité "+PhoneShortcuts.MentalityLabels[PhoneShortcuts.NearestMentality(plan.tactic.mentality)].ToLowerInvariant()+" · "+plan.tactic.withoutBall.Count(x=>!string.IsNullOrEmpty(x.playerRole))+" rôle(s) défini(s)","muted");
                var name=new TextField("Nom"){value=plan.used?plan.name:"",name="plan-name-"+i};card.Add(name);var actions=Row(card,"instruction-options");
                Button(actions,"Enregistrer l'actuelle",()=>RunDecision(()=>{Career.SaveTacticPlan(index,name.value);Save();Build();})).name="plan-save-"+i;
                var apply=Button(actions,"Appliquer",()=>RunDecision(()=>{Career.ApplyTacticPlan(Database,index);Save();Build();}));apply.name="plan-apply-"+i;apply.SetEnabled(plan.used);}
        }

        // Plans en une touche pendant le match (affiché seulement si un plan existe).
        void MatchPlanShortcut()
        {
            var plans=Career.TacticPlans();if(!plans.Any(p=>p.used))return;
            var row=Row(content,"match-mentality");row.name="match-plans";Text(row,"Plans","match-mentality-label");
            for(int i=0;i<plans.Length;i++){if(!plans[i].used)continue;int index=i;var b=Button(row,((char)('A'+i)).ToString()+" "+plans[i].name,()=>RunDecision(()=>{Career.ApplyTacticPlan(Database,index);QueuePreferenceSave();comment.text="Plan "+(char)('A'+index)+" appliqué : "+plans[index].name+".";}));b.name="match-plan-"+i;}
        }

        void TrainingSheet()
        {
            var panel=Modal("Travail individuel");panel.name="training-sheet";var scroll=Scroll(panel);
            Text(scroll,"Thème collectif de la semaine : "+Core.Career.TrainingFocusLabel(Career.TrainingFocus)+" (Bureau). Un programme individuel multiplie la progression par "+IndividualTrainingFocus.Growth.ToString("0.00")+" pour un joueur de moins de 24 ans sous son potentiel.","muted");
            foreach(var p in Database.Squad(Career.club).OrderBy(p=>p.age).ThenByDescending(p=>p.potential-p.rating)){
                var row=Row(scroll,"bench-player");var label=new Label(p.name+" · "+p.age+" ans"+(p.age<24&&p.rating<p.potential?" · marge "+(p.potential-p.rating).ToString("0"):" · progression terminée"));label.AddToClassList("bench-detail");row.Add(label);
                var choice=new DropdownField(IndividualTrainingFocus.Labels.ToList(),Math.Max(0,Array.IndexOf(IndividualTrainingFocus.Codes,Career.IndividualTrainingOf(p.id)))){name="individual-"+p.id};row.Add(choice);
                choice.RegisterValueChangedCallback(_=>RunDecision(()=>{Career.SetIndividualTraining(p.id,IndividualTrainingFocus.Codes[choice.index]);Save();}));}
        }
    }
}

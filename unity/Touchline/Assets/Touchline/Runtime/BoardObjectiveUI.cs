using UnityEngine.UIElements;

namespace Touchline
{
    public sealed partial class TouchlineApp
    {
        void ShowBoardObjective(VisualElement parent)
        {
            var objective=Career.EnsureBoardObjective(Database);if(objective==null)return;
            var card=Card(parent);card.name="board-season-objective";
            Text(card,"LE CONTRAT AVEC LA DIRECTION","eyebrow");
            Text(card,"Viser la "+objective.targetRank+"e place ou mieux","section-title");
            Text(card,Career.CompetitionName(Database,objective.division)+" · "+objective.season+"–"+(objective.season+1));
            Text(card,"Objectif fixé selon les moyens, l'effectif et la situation à votre arrivée. Vos transferts ne déplacent pas cette référence.","muted");
            Text(card,"Plafond salarial à l'accord : "+Core.Career.MonthlySalary(objective.weeklyWageCeiling).ToString("N0")+" € / mois. Consultez Finances pour le plafond actuel.","muted");
            if(objective.lastReviewDay>=0){
                Text(card,objective.played<5?"Dernier bilan : pas encore cinq matchs de championnat, évaluation sportive en attente.":"Dernier bilan : "+objective.lastRank+"e après "+objective.played+" matchs · confiance "+objective.lastSportingChange.ToString("+0.0;-0.0;0")+" points.");
            }
            Text(card,"Objectif simulé pour votre carrière ; aucune déclaration réelle de la direction.","footnote");
        }
    }
}

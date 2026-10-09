using System;
using System.Linq;
using Touchline.Core;
using UnityEngine.UIElements;

namespace Touchline
{
    public sealed partial class TouchlineApp
    {
        bool recruitmentAllNeeds;
        string recruitmentRecommendationRole="Tous";

        void OpenRecruitmentMarket(string role="Tous",string market="Tous")
        {
            ResetRecruitmentCriteria();recruitRole=role;marketFilter=market;
            recruitmentTab="Marché";Build();
        }
        void OpenRecruitmentReports(string filter)
        {
            scoutReportFilter=filter;recruitmentTab="Rapports";Build();
        }
        void RecruitmentHub(VisualElement parent)
        {
            parent.name="recruit-hub";parent.AddToClassList("recruit-hub");
            var overview=Career.RecruitmentOverview(Database);
            var finance=Row(parent,"recruit-hub-finances");
            RecruitmentMetric(finance,"Indemnités disponibles",Money(overview.transferBudget),"recruit-hub-transfer-budget");
            RecruitmentMetric(finance,"Marge salariale / mois",Money(overview.monthlyWageRoom),"recruit-hub-wage-room",overview.monthlyWageRoom<0);
            Text(parent,"Marges après engagements réservés. Valeurs et salaires du marché restent des estimations, à confirmer avec l’agent.","recruit-hub-note");

            var pipeline=Row(parent,"recruit-hub-pipeline");
            HubAction(pipeline,overview.observations+" en observation","recruit-hub-observations",()=>OpenRecruitmentReports("En cours"));
            HubAction(pipeline,(overview.reportsReady+overview.reportsStale)+((overview.reportsReady+overview.reportsStale)==1?" rapport terminé":" rapports terminés"),"recruit-hub-reports",()=>OpenRecruitmentReports("Terminés"));
            HubAction(pipeline,overview.actionableOffers+" offres à traiter","recruit-hub-offers",()=>{recruitmentTab="Négociations et prêts";Build();});
            if(overview.reportsStale>0)HubAction(parent,overview.reportsStale+" rapports à actualiser","recruit-hub-stale",()=>OpenRecruitmentReports("À actualiser"));

            var shortcuts=Row(parent,"recruit-hub-shortcuts");
            HubAction(shortcuts,"Joueurs libres","recruit-hub-free",()=>OpenRecruitmentMarket(market:"Libres"));
            HubAction(shortcuts,"Précontrats ≤ 6 mois","recruit-hub-precontracts",()=>OpenRecruitmentMarket(market:"Fin de contrat ≤ 6 mois"));
            HubAction(shortcuts,"Ma sélection","recruit-hub-shortlist",()=>OpenRecruitmentMarket(market:"Ma sélection"));
            if(Career.Staff("scout").wage<=0)Text(parent,"Aucun recruteur en poste : embauchez un membre du staff pour lancer vos missions.","recruit-hub-detail");

            var columns=Row(parent,"recruit-hub-columns");
            var needs=Card(columns,"recruit-hub-section");needs.name="recruit-hub-needs";
            Text(needs,"Priorités de l’effectif","section-title");
            Text(needs,"Couverture de votre système actuel. Un joueur polyvalent compte à un seul poste.","recruit-hub-note");
            var displayed=recruitmentAllNeeds?overview.needs:overview.needs.Take(3);
            foreach(var need in displayed)
            {
                var card=new VisualElement();card.AddToClassList("recruit-hub-need");card.name="recruit-hub-need-"+need.role;needs.Add(card);
                var title=Row(card,"recruit-hub-need-title");Text(title,FrenchFootballPositions.Label(need.role),"recruit-hub-player-name");
                var badge=Text(title,need.priority>=3?"Urgent":need.priority==2?"Renforcer":need.priority==1?"Anticiper":"Couvert","recruit-hub-badge");badge.EnableInClassList("recruit-hub-alert",need.priority>=2);
                Text(card,need.available+(need.available==1?" disponible / ":" disponibles / ")+need.target+" visés · "+need.reason,"recruit-hub-detail");
                var actions=Row(card,"recruit-hub-actions");
                HubAction(actions,"Chercher","recruit-hub-search-"+need.role,()=>OpenRecruitmentMarket(need.searchRole));
                var mission=HubAction(actions,"Mission ciblée","recruit-hub-mission-"+need.role,()=>{ResetRecruitmentCriteria();recruitRole=need.searchRole;NewScoutingMission();});mission.SetEnabled(Career.Staff("scout").wage>0);
            }
            if(overview.needs.Count>3)HubAction(needs,recruitmentAllNeeds?"Réduire la liste":"Tous les postes ("+overview.needs.Count+")","recruit-hub-all-needs",()=>{recruitmentAllNeeds=!recruitmentAllNeeds;Build();});
            HubAction(needs,"Vérifier ma composition","recruit-hub-tactics",()=>Navigate("Tactique"));

            var decisions=Card(columns,"recruit-hub-section");decisions.name="recruit-hub-decisions";
            Text(decisions,"Candidats de votre cellule","section-title");
            Text(decisions,"Pistes issues de la connaissance du club. Une estimation de niveau n’est pas une garantie de réussite.","recruit-hub-note");
            var roles=ScoutingRoles();var filter=new DropdownField("Poste",roles,recruitmentRecommendationRole){name="recruit-hub-candidate-role",formatListItemCallback=FrenchFootballPositions.Label,formatSelectedValueCallback=FrenchFootballPositions.Label};decisions.Add(filter);
            filter.RegisterValueChangedCallback(e=>{recruitmentRecommendationRole=e.newValue;Build();});
            var recommendations=Career.RecruitmentRecommendations(Database,recruitmentRecommendationRole,4);
            if(recommendations.Count==0)
            {
                Text(decisions,"Pas de piste suffisamment connue pour ce poste. Lancez une mission ou observez des profils sur le marché.","recruit-hub-detail");
                var launch=HubAction(decisions,"Lancer une mission","recruit-hub-new-mission",()=>{ResetRecruitmentCriteria();recruitRole=recruitmentRecommendationRole;NewScoutingMission();});launch.AddToClassList("primary");launch.SetEnabled(Career.Staff("scout").wage>0);
            }
            foreach(var recommendation in recommendations)
            {
                var player=Database.Find(recommendation.player);if(player==null)continue;
                var card=new VisualElement();card.AddToClassList("recruit-hub-candidate");card.name="recruit-hub-candidate-"+player.id;decisions.Add(card);
                Text(card,player.name,"recruit-hub-player-name");
                Text(card,FrenchFootballPositions.PlayerLabel(player)+" · "+player.age+" ans · "+ClubName(player.team),"recruit-hub-detail");
                Text(card,recommendation.reason,"recruit-hub-detail");
                Text(card,recommendation.comparison,"recruit-hub-assessment").name="recruit-hub-comparison-"+player.id;
                Text(card,recommendation.tacticalFit,"recruit-hub-detail");
                if(recommendation.levelKnown)Text(card,Stars(recommendation.assessedLevel,ClubRatingBaseline())+" · estimation pour votre club","recruit-hub-assessment");
                var report=Career.ScoutReportCard(Database,player.id,CachedRecruitmentOverview());if(report.ability.known)Text(card,"Note "+report.grade+" · niveau "+report.ability+(report.potential.known?" · potentiel "+report.potential:"")+(report.generated?" · ligue générée":""),"recruit-hub-assessment").name="recruit-hub-grade-"+player.id;
                Text(card,Money(recommendation.estimatedFee)+" · "+Money(recommendation.currentMonthlyWage)+" / mois estimés","recruit-hub-detail");
                Text(card,"Connaissance "+recommendation.knowledge+" %"+(recommendation.stale?" · rapport à actualiser":"")+(recommendation.affordable?"":" · hors enveloppe actuelle"),"recruit-hub-note");
                var actions=Row(card,"recruit-hub-actions");
                HubAction(actions,"Fiche","recruit-hub-profile-"+player.id,()=>PlayerProfile(player.id));
                HubAction(actions,"Comparer","recruit-hub-compare-"+player.id,()=>ComparePlayer(player.id));
                HubAction(actions,"Agent","recruit-hub-agent-"+player.id,()=>TransferDialog(player.id));
            }
            DepartmentRecommendationsPanel(columns);SigningsReviewPanel(parent);
            HubAction(parent,"Gérer les recruteurs","recruit-hub-staff",()=>Navigate("Staff et délégation"));
        }
        static Button HubAction(VisualElement parent,string label,string name,Action action)
        {
            var button=Button(parent,label,action);button.name=name;button.AddToClassList("recruit-hub-action");return button;
        }
        static void RecruitmentMetric(VisualElement parent,string label,string value,string name,bool warning=false)
        {
            var tile=new VisualElement();tile.AddToClassList("recruit-hub-metric");parent.Add(tile);
            Text(tile,label,"recruit-hub-note");var amount=Text(tile,value,"recruit-hub-amount");amount.name=name;amount.EnableInClassList("recruit-hub-alert",warning);
        }
    }
}

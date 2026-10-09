using System;
using System.Collections.Generic;
using System.Linq;
using Touchline.Core;
using UnityEngine.UIElements;

namespace Touchline
{
    public sealed partial class TouchlineApp
    {
        string scoutReportFilter="Tous",baselineClub;int baselineDay=-1,baselineCount;float scoutingBaseline;
        float ClubRatingBaseline()
        {
            if(baselineClub!=Career.club||baselineDay!=Career.life.day||baselineCount!=Database.players.Length){baselineClub=Career.club;baselineDay=Career.life.day;baselineCount=Database.players.Length;scoutingBaseline=Database.Squad(Career.club).OrderByDescending(x=>x.rating+x.development).Take(11).Select(x=>x.rating+x.development).DefaultIfEmpty(65).Average();}
            return scoutingBaseline;
        }
        static List<string> ScoutingRoles()=>new List<string>{"Tous","GB","DEF","MIL","ATT","GK","CB","LB","RB","DM","CM","AM","LW","RW","ST"};
        List<string> ScoutingNationalities()=>new[]{"Tous"}.Concat(Database.players.Select(p=>p.nationality).Where(n=>!string.IsNullOrWhiteSpace(n)).Distinct().OrderBy(n=>n)).ToList();
        List<string> ScoutingTerritories()=>new[]{"Tous"}.Concat(ScoutingGeography.Countries(Database)).ToList();
        void ScoutingMissions(VisualElement parent)
        {
            var scout=Career.Staff("scout");var header=Card(parent,"scout-summary");Text(header,"Votre réseau d’observation","section-title");Text(header,scout.name+" · jugement "+scout.judging+" / 20 · "+Career.ActiveObservations+" / 3 observations en cours","muted");
            Text(header,"Recherches par poste et marché, puis observation détaillée. Un meilleur recruteur travaille plus vite et réduit l’incertitude. Les dépenses sont fixées avant le départ ; le reliquat vous est rendu.","footnote");
            var actions=Row(header,"scout-actions");var create=Button(actions,"+ Nouvelle mission",NewScoutingMission);create.name="scout-new-mission";create.AddToClassList("primary");create.SetEnabled(scout.wage>0);Button(actions,"Gérer les recruteurs",()=>Navigate("Staff et délégation"));
            if(scout.wage<=0)Text(header,"Poste vacant : les observations sont suspendues jusqu’à l’embauche d’un recruteur.","notice");
            var missions=(Career.world.scoutMissions??new List<ScoutMission>()).Where(m=>m.club==Career.club).OrderByDescending(m=>m.started).Take(20).ToArray();
            if(missions.Length==0)Text(parent,"Aucune mission. Définissez le poste, le profil, la nationalité et votre budget ; votre staff vous proposera les candidats.","empty-state");
            foreach(var m in missions)
            {
                var card=Card(parent,"scout-mission-card");card.name="scout-mission-"+m.id;Text(card,FrenchFootballPositions.Label(m.role)+" · territoire "+(m.country??"Tous")+" · "+m.minAge+"–"+m.maxAge+" ans","section-title");Text(card,"Nationalité "+m.nationality+" · "+MissionPriorityLabel(m.priority)+" · "+MissionStatusLabel(m.status),"scout-status");
                Text(card,"Valeur maxi "+Money(m.maxFee)+" · salaire maxi "+Money(m.maxMonthlyWage)+" / mois","muted");Text(card,"Recherche du "+Core.Career.Epoch.AddDays(m.started).ToString("dd MMM",French)+" au "+Core.Career.Epoch.AddDays(m.until).ToString("dd MMM",French)+" · "+m.found+" profil(s) repéré(s)","muted");
                Text(card,"Enveloppe "+Money(m.budget)+" · restant réservé "+Money(m.remaining),"footnote");
                var row=Row(card,"scout-actions");Button(row,"Voir les rapports",()=>{recruitmentTab="Rapports";Build();});if(m.status=="active"||m.status=="finishing")Button(row,"Arrêter et restituer le reliquat",()=>RunDecision(()=>Career.StopScoutMission(m.id)));
            }
        }
        void NewScoutingMission()
        {
            var panel=Modal("Mission de recrutement");panel.name="scout-mission-dialog";panel.AddToClassList("scout-mission-dialog");var body=Scroll(panel);Text(body,"28 jours de recherche · au plus quatre profils · trois observations simultanées dans la cellule.","muted");
            var fields=Row(body,"scout-form");
            var role=new DropdownField("Poste",ScoutingRoles(),recruitRole){name="scout-mission-role",formatListItemCallback=FrenchFootballPositions.Label,formatSelectedValueCallback=FrenchFootballPositions.Label};fields.Add(role);
            var territories=ScoutingTerritories();var country=new DropdownField("Territoire du club",territories,territories.Contains(recruitCountry)?recruitCountry:"Tous"){name="scout-mission-country"};fields.Add(country);
            var nationality=new DropdownField("Nationalité",ScoutingNationalities(),recruitNationality){name="scout-mission-nationality"};fields.Add(nationality);
            var priority=new DropdownField("Priorité",new List<string>{"Renfort immédiat","Développement des jeunes","Joueurs libres","Coût maîtrisé"},0){name="scout-mission-priority"};fields.Add(priority);
            var min=new IntegerField("Âge minimum"){value=Math.Max(16,recruitMinAge),name="scout-mission-min-age"};fields.Add(min);var max=new IntegerField("Âge maximum"){value=Math.Min(45,recruitMaxAge),name="scout-mission-max-age"};fields.Add(max);
            var fee=new LongField("Valeur maximale (€)"){value=recruitMaxFee>0?recruitMaxFee:Career.TransferBudget,name="scout-mission-fee"};fields.Add(fee);
            long monthlyRoom=Career.RecruitmentOverview(Database).monthlyWageRoom;
            var wage=new LongField("Salaire maximal / mois (€)"){value=recruitMaxMonthly>0?recruitMaxMonthly:monthlyRoom/2,name="scout-mission-wage"};fields.Add(wage);
            var observations=new IntegerField("Observations financées (1–4)"){value=4,name="scout-mission-observations"};fields.Add(observations);
            var budget=Text(body,"","scout-status");budget.name="scout-mission-budget";void UpdateBudget()=>budget.text="Enveloppe prépayée : "+Money(Career.ObservationCost*Math.Max(1,Math.Min(4,observations.value)))+" · "+Money(Career.ObservationCost)+" par profil";observations.RegisterValueChangedCallback(_=>UpdateBudget());UpdateBudget();
            Text(body,"Territoire : clubs rattachés à ce pays, y compris hors des championnats jouables. Nationalité : filtre indépendant. Pour les joueurs libres, choisissez Tous les territoires. Les profils disponibles ne constituent pas des effectifs mondiaux exhaustifs. Les joueurs de moins de 18 ans peuvent être observés, leur transfert reste limité dans la simulation.","footnote");
            var wageHint=Text(body,"","footnote");wageHint.name="scout-mission-wage-hint";
            if(monthlyRoom<=0)Text(body,"Aucune marge salariale après les engagements réservés. Vous pouvez préparer une liste pour plus tard ou étudier un prêt avec prise en charge partielle. Un joueur libre demande aussi un salaire.","notice");
            var start=Button(panel,"Envoyer le recruteur",()=>{try{if(observations.value<1||observations.value>4)throw new InvalidOperationException("Financez de une à quatre observations.");Career.CreateScoutMission(Database,role.value,nationality.value,min.value,max.value,fee.value,wage.value,new[]{"ready","prospect","free","value"}[priority.index],Career.ObservationCost*observations.value,country.value);Save();recruitmentTab="Missions";Build();}catch(Exception e){Message(e.Message);}});start.name="scout-mission-send";start.AddToClassList("primary");
            void UpdateWageHint(){start.SetEnabled(wage.value>0);wageHint.text="Marge salariale : "+Money(monthlyRoom)+" / mois. Ce plafond filtre les observations ; il ne constitue pas une offre ni une réservation de salaire. "+(wage.value<=0?"Renseignez un plafond positif pour lancer la recherche.":wage.value>monthlyRoom?"Ce plafond dépasse votre marge actuelle : un recrutement nécessitera de dégager du budget.":"");}
            wage.RegisterValueChangedCallback(_=>UpdateWageHint());UpdateWageHint();
        }
        void ScoutingReports(VisualElement parent)
        {
            var tools=Row(parent,"scout-actions");foreach(var label in new[]{"Tous","En cours","Terminés","À actualiser","Ma sélection"}){var button=Button(tools,label,()=>{scoutReportFilter=label;Build();});button.AddToClassList(scoutReportFilter==label?"active":"scout-filter");}
            var reports=Career.world.reports.Where(r=>(string.IsNullOrEmpty(r.club)||r.club==Career.club)&&Database.Find(r.player)!=null);
            if(scoutReportFilter=="En cours")reports=reports.Where(r=>r.confidence<90);else if(scoutReportFilter=="Terminés")reports=reports.Where(r=>r.confidence>=90);else if(scoutReportFilter=="À actualiser")reports=reports.Where(Career.RecruitmentReportNeedsRefresh);else if(scoutReportFilter=="Ma sélection")reports=reports.Where(r=>Career.shortlist.Contains(r.player));
            var found=reports.OrderByDescending(r=>r.started).ToArray();Text(parent,found.Length+" rapports · évaluations relatives à votre effectif · les observations anciennes perdent de leur précision","muted");
            if(found.Length==0)Text(parent,"Aucun rapport dans cette catégorie. Envoyez une mission ou observez un joueur depuis Marché.","empty-state");
            foreach(var report in found.Take(80))
            {
                var p=Database.Find(report.player);int knowledge=Career.Knowledge(p.id);var card=Card(parent,"scout-report-card");card.name="scout-report-"+p.id;
                Text(card,p.name+" · "+p.age+" ans · "+FrenchFootballPositions.List(p.positions??new[]{p.position}),"section-title");Text(card,ClubName(p.team)+" · "+(report.scout??"Recruteur")+" · jugement "+(report.judging>0?report.judging.ToString():"non renseigné")+" / 20","muted");
                var progress=new ProgressBar{lowValue=0,highValue=90,value=knowledge,title="Connaissance · "+knowledge+" %"};card.Add(progress);
                if(report.confidence<90)Text(card,"Observation en cours · rapport attendu le "+Core.Career.Epoch.AddDays(report.due).ToString("dd MMM",French),"scout-status");
                if(knowledge>=40){Text(card,Stars(Career.AssessedLevel(Database,p.id),ClubRatingBaseline())+" · niveau estimé pour votre club","profile-stars");Text(card,knowledge>=65?Stars(Career.AssessedLevel(Database,p.id,true),ClubRatingBaseline())+" · potentiel incertain":"Potentiel encore à préciser","muted");}
                Text(card,report.advice??"Le recruteur affine son avis. Les attributs se précisent progressivement.","scout-report-advice");Text(card,"Valeur "+Money(p.value)+" · salaire actuel estimé "+Money(Core.Career.MonthlySalary(p.wage))+" / mois","footnote");
                var actions=Row(card,"scout-actions");Button(actions,"Fiche et attributs",()=>PlayerProfile(p.id));Button(actions,"Comparer à mon effectif",()=>ComparePlayer(p.id));Button(actions,Career.shortlist.Contains(p.id)?"★ Retirer de ma sélection":"☆ Suivre ce joueur",()=>RunDecision(()=>Career.ToggleShortlist(p.id)));
                if(report.confidence>=90&&knowledge<90)Button(actions,"Actualiser le rapport",()=>RunDecision(()=>Career.Scout(Database,p.id)));Button(actions,"Contacter l’agent",()=>TransferDialog(p.id));
            }
            if(found.Length>80)Text(parent,"Les 80 rapports les plus récents sont affichés. Utilisez Ma sélection pour garder vos priorités visibles.","footnote");
        }
        static string MissionPriorityLabel(string value)=>value=="prospect"?"Développement des jeunes":value=="free"?"Joueurs libres":value=="value"?"Coût maîtrisé":"Renfort immédiat";
        static string MissionStatusLabel(string value)=>value=="complete"?"Terminée":value=="stopped"?"Arrêtée":value=="finishing"?"Derniers rapports en cours":"Recherche active";
    }
}

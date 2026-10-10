using System;
using System.Collections.Generic;
using System.Linq;
using Touchline.Core;
using UnityEngine;
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
            ScoutingDepartmentPanel(parent);
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
            var blocked=Text(panel,"Indiquez un salaire mensuel maximal supérieur à 0 € pour lancer la recherche.","notice");blocked.name="scout-mission-blocked";
            var start=Button(panel,"Envoyer le recruteur",()=>{try{if(observations.value<1||observations.value>4)throw new InvalidOperationException("Financez de une à quatre observations.");Career.CreateScoutMission(Database,role.value,nationality.value,min.value,max.value,fee.value,wage.value,new[]{"ready","prospect","free","value"}[priority.index],Career.ObservationCost*observations.value,country.value);Save();recruitmentTab="Missions";Build();}catch(Exception e){Message(e.Message);}});start.name="scout-mission-send";start.AddToClassList("primary");
            void UpdateWageHint(){start.SetEnabled(wage.value>0);wageHint.text="Marge salariale : "+Money(monthlyRoom)+" / mois. Ce plafond filtre les observations ; il ne constitue pas une offre ni une réservation de salaire. "+(wage.value<=0?"Renseignez un plafond positif pour lancer la recherche.":wage.value>monthlyRoom?"Ce plafond dépasse votre marge actuelle : un recrutement nécessitera de dégager du budget.":"");}
            void UpdateBlocked()=>blocked.style.display=wage.value<=0?DisplayStyle.Flex:DisplayStyle.None;
            wage.RegisterValueChangedCallback(_=>{UpdateWageHint();UpdateBlocked();});UpdateWageHint();UpdateBlocked();
        }
        static readonly string[] ReportSorts={"Récents","Note","Niveau estimé","Âge","Rapport le plus ancien"};
        string scoutReportSort="Récents";
        // Overview reused by every card of one screen: its assignment pass is the costly part.
        RecruitmentOverviewData overviewCache;string overviewKey;
        RecruitmentOverviewData CachedRecruitmentOverview()
        {
            string key=Career.club+"/"+Career.life.day+"/"+Database.players.Length+"/"+Career.tactic?.formation+"/"+Database.Squad(Career.club).Count;
            if(overviewCache==null||overviewKey!=key){overviewKey=key;overviewCache=Career.RecruitmentOverview(Database);}
            return overviewCache;
        }
        static int GradeRank(string grade)=>grade=="A"?0:grade=="B"?1:grade=="C"?2:grade=="D"?3:grade=="E"?4:5;
        void ScoutingReports(VisualElement parent)
        {
            var tools=Row(parent,"scout-report-controls");var filters=new[]{"Tous","En cours","Terminés","À actualiser","Ma sélection","Notes A–B"};
            var filter=new DropdownField("Afficher",filters.ToList(),Math.Max(0,Array.IndexOf(filters,scoutReportFilter))){name="scout-report-filter"};tools.Add(filter);filter.RegisterValueChangedCallback(e=>{scoutReportFilter=e.newValue;Build();});
            var sort=new DropdownField("Trier",ReportSorts.ToList(),Math.Max(0,Array.IndexOf(ReportSorts,scoutReportSort))){name="scout-report-sort"};tools.Add(sort);sort.RegisterValueChangedCallback(e=>{scoutReportSort=e.newValue;Build();});
            var reports=Career.world.reports.Where(r=>(string.IsNullOrEmpty(r.club)||r.club==Career.club)&&Database.Find(r.player)!=null).GroupBy(r=>r.player).Select(g=>g.OrderByDescending(r=>r.started).First());
            if(scoutReportFilter=="En cours")reports=reports.Where(r=>r.confidence<90);else if(scoutReportFilter=="Terminés")reports=reports.Where(r=>r.confidence>=90);else if(scoutReportFilter=="À actualiser")reports=reports.Where(Career.RecruitmentReportNeedsRefresh);else if(scoutReportFilter=="Ma sélection")reports=reports.Where(r=>Career.shortlist.Contains(r.player));
            var overview=CachedRecruitmentOverview();
            var cards=reports.Select(r=>(report:r,card:Career.ScoutReportCard(Database,r.player,overview))).ToList();
            if(scoutReportFilter=="Notes A–B")cards=cards.Where(x=>x.card.grade=="A"||x.card.grade=="B").ToList();
            var ordered=scoutReportSort=="Note"?cards.OrderBy(x=>GradeRank(x.card.grade)).ThenByDescending(x=>x.card.ability.Middle):scoutReportSort=="Niveau estimé"?cards.OrderByDescending(x=>x.card.ability.known?x.card.ability.Middle:-1):scoutReportSort=="Âge"?cards.OrderBy(x=>Database.Find(x.report.player).age):scoutReportSort=="Rapport le plus ancien"?cards.OrderByDescending(x=>x.card.reportAge):cards.OrderByDescending(x=>x.report.started);
            var found=ordered.ThenBy(x=>x.report.player,StringComparer.Ordinal).ToArray();
            Text(parent,found.Length+" rapports · fourchettes et notes relatives à votre effectif · un rapport ancien redevient incertain","muted");
            if(found.Length==0)Text(parent,"Aucun rapport dans cette catégorie. Envoyez une mission ou observez un joueur depuis Marché.","empty-state");
            foreach(var (report,data) in found.Take(80))
            {
                var p=Database.Find(report.player);var card=Card(parent,"scout-report-card");card.name="scout-report-"+p.id;
                var head=Row(card,"scout-report-head");head.style.alignItems=Align.Center;GradeBadge(head,data.grade).name="scout-grade-"+p.id;
                var identity=new VisualElement();identity.style.flexShrink=1;identity.style.flexGrow=1;head.Add(identity);
                Text(identity,p.name+" · "+p.age+" ans · "+FrenchFootballPositions.List(p.positions??new[]{p.position}),"section-title");
                Text(identity,ClubName(p.team)+(GeneratedWorld.IsGenerated(p.team)?" · club fictif (ligue générée)":"")+" · "+(report.scout??"Recruteur")+" · jugement "+(data.judging>0?data.judging.ToString():"?")+" / 20","muted");
                if(data.pending)Text(card,"Observation en cours · rapport attendu le "+Core.Career.Epoch.AddDays(report.due).ToString("dd MMM",French),"scout-status");
                var actions=Row(card,"scout-actions");Button(actions,"Fiche",()=>PlayerProfile(p.id));Button(actions,"Comparer",()=>ComparePlayer(p.id));Button(actions,Career.shortlist.Contains(p.id)?"★ Suivi":"☆ Suivre",()=>RunDecision(()=>Career.ToggleShortlist(p.id))).name="scout-follow-"+p.id;
                if(report.confidence>=90&&data.knowledge<90)Button(actions,"Actualiser",()=>RunDecision(()=>Career.Scout(Database,p.id))).name="scout-refresh-"+p.id;
                Button(actions,"Contacter l’agent",()=>TransferDialog(p.id));
                ReportCardBody(card,p,data);
            }
            if(found.Length>80)Text(parent,"Les 80 premiers rapports sont affichés. Utilisez le tri ou Ma sélection pour garder vos priorités visibles.","footnote");
        }
        static readonly Dictionary<string,string> GradeColors=new Dictionary<string,string>{{"A","#2e7d32"},{"B","#558b2f"},{"C","#9e7c0c"},{"D","#c25e00"},{"E","#b71c1c"}};
        Label GradeBadge(VisualElement parent,string grade)
        {
            var badge=new Label(grade);badge.AddToClassList("scout-grade");parent.Add(badge);
            badge.style.minWidth=40;badge.style.height=40;badge.style.marginRight=10;badge.style.unityTextAlign=TextAnchor.MiddleCenter;badge.style.fontSize=22;badge.style.unityFontStyleAndWeight=FontStyle.Bold;badge.style.color=Color.white;
            badge.style.borderTopLeftRadius=8;badge.style.borderTopRightRadius=8;badge.style.borderBottomLeftRadius=8;badge.style.borderBottomRightRadius=8;
            badge.style.backgroundColor=GradeColors.TryGetValue(grade,out var hex)&&ColorUtility.TryParseHtmlString(hex,out var color)?color:new Color(.45f,.47f,.5f);
            badge.tooltip="Note de recrutement A–E : comparaison prudente avec votre titulaire au même poste, besoins, âge, budget et incertitude.";
            return badge;
        }
        // Range bar; the yellow tick marks the average level of your current eleven.
        void RangeBar(VisualElement parent,string label,ScoutRange range,float reference,string name)
        {
            float baseline=ClubRatingBaseline();
            var row=Row(parent,"scout-range-row");row.name=name;row.style.flexDirection=FlexDirection.Column;row.style.alignItems=Align.Stretch;
            var caption=Text(row,label+" estimé : "+ClubRatingScale.Range(range,baseline),"scout-range-label");caption.style.whiteSpace=WhiteSpace.Normal;
            caption.tooltip="Évaluation relative à votre club : trois étoiles correspondent au niveau moyen de votre onze. La fourchette traduit l’incertitude du recruteur.";
            var track=new VisualElement();track.style.flexGrow=1;track.style.height=12;track.style.backgroundColor=new Color(.5f,.5f,.55f,.25f);track.style.minWidth=90;row.Add(track);
            float Pos(float v)=>(ClubRatingScale.Relative(v,baseline)-1)/4*100;
            if(range.known){var fill=new VisualElement();fill.style.position=UnityEngine.UIElements.Position.Absolute;fill.style.top=0;fill.style.bottom=0;fill.style.left=Length.Percent(Pos(range.low));fill.style.width=Length.Percent(Mathf.Max(1.5f,Pos(range.high)-Pos(range.low)));fill.style.backgroundColor=new Color(.25f,.6f,.95f);track.Add(fill);}
            if(reference>0){var tick=new VisualElement();tick.style.position=UnityEngine.UIElements.Position.Absolute;tick.style.top=-3;tick.style.bottom=-3;tick.style.width=3;tick.style.left=Length.Percent(Pos(reference));tick.style.backgroundColor=new Color(.95f,.75f,.2f);track.Add(tick);}
        }
        void ReportCardBody(VisualElement card,PlayerData p,ScoutReportCardData data)
        {
            if(!data.exact&&Career.ReportFor(p.id)?.attributesObserved==false)Text(card,"Ancien rapport : attributs non archivés. Actualisez l’observation pour les obtenir.","notice").name="scout-missing-snapshot-"+p.id;
            if(!data.ability.known)Text(card,data.gradeReason??"Niveau à observer.","scout-report-advice");
            else
            {
                RangeBar(card,"Niveau",data.ability,ClubRatingBaseline(),"scout-range-ability-"+p.id);
                if(data.potential.known)RangeBar(card,"Potentiel",data.potential,0,"scout-range-potential-"+p.id);else Text(card,"Potentiel : à préciser (connaissance ≥ 65 %)","footnote");
                Text(card,"Trois étoiles : niveau moyen de votre onze (repère jaune). La fourchette reflète l’incertitude de l’observation.","footnote");
                Text(card,data.gradeReason,"scout-report-advice");
                if(data.strengths.Count>0)Text(card,"Points forts : "+string.Join(", ",data.strengths),"body-text").name="scout-strengths-"+p.id;
                if(data.weaknesses.Count>0)Text(card,"Points faibles : "+string.Join(", ",data.weaknesses),"body-text").name="scout-weaknesses-"+p.id;
                if(!string.IsNullOrEmpty(data.fit))Text(card,data.fit,"body-text");
            }
            PersonalityAndInterest(card,p,data);
            string age=data.observedDay<0?"":data.reportAge==0?"rapport du jour":"rapport de "+data.reportAge+" j";
            Text(card,"Connaissance "+data.knowledge+" %"+(age==""?"":" · "+age)+(data.stale?" · à actualiser":"")+" · territoire "+(data.territory??"—")+" : connaissance "+data.familiarityLabel+(data.depth>0?" · "+(data.depth+1)+" observations":""),"footnote");
            if(!string.IsNullOrEmpty(data.rival))Text(card,"Concurrence : "+data.rival,"scout-status").name="scout-rival-"+p.id;
            if(data.generated){var club=Database.clubs.FirstOrDefault(c=>c.id==p.team);var table=club==null?null:GeneratedWorld.SimulatedTable(Database,club.league,Career.world.year);int rank=table==null?-1:table.FindIndex(r=>r.club==club.id);
                Text(card,(rank>=0?"Classement simulé : "+(rank+1)+"e / "+table.Count+" · ":"")+(GeneratedWorld.IsGenerated(p.id)?"joueur fictif généré, aucune personne réelle":"club fictif ; identité du joueur issue de la base importée"),"footnote");}
            Text(card,"Valeur "+Money(p.value)+" · salaire actuel estimé "+Money(Core.Career.MonthlySalary(p.wage))+" / mois","footnote");
        }
        static string MissionPriorityLabel(string value)=>value=="prospect"?"Développement des jeunes":value=="free"?"Joueurs libres":value=="value"?"Coût maîtrisé":"Renfort immédiat";
        static string MissionStatusLabel(string value)=>value=="complete"?"Terminée":value=="stopped"?"Arrêtée":value=="finishing"?"Derniers rapports en cours":"Recherche active";
    }
}

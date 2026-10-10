using System;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;
using Touchline.Core;

namespace Touchline
{
    public sealed partial class TouchlineApp
    {
        string recruitmentTab="Synthèse",recruitOrder="Valeur",recruitNationality="Tous",recruitCountry="Tous",recruitLeague="Tous";
        bool recruitAdvanced;
        int recruitMinAge=16,recruitMaxAge=45;long recruitMaxFee,recruitMaxMonthly;
        void ResetRecruitmentFilters()
        {
            ResetRecruitmentCriteria();Build();
        }
        void ResetRecruitmentCriteria()
        {
            recruitSearch="";recruitRole=marketFilter=recruitNationality=recruitCountry=recruitLeague="Tous";
            recruitOrder="Valeur";recruitMinAge=16;recruitMaxAge=45;recruitMaxFee=recruitMaxMonthly=0;recruitAdvanced=false;
        }
        void RecruitmentWorkspace()
        {
            content.AddToClassList("recruit-workspace");var title=Row(content,"recruit-titlebar");Heading(title,"Recrutement");if(Career.world==null)return;
            Text(title,"Transferts : "+Money(Career.TransferBudget)+" · Plafond salarial : "+Money(Core.Career.MonthlySalary(Career.WageBudget))+" / mois","recruit-budget");
            var tabs=Row(content,"recruit-tabs");foreach(var tab in new[]{"Synthèse","Marché","Missions","Rapports","Négociations et prêts"}){var b=Button(tabs,tab=="Négociations et prêts"?"Dossiers":tab,()=>{recruitmentTab=tab;Build();});b.name="recruit-tab-"+tab;b.AddToClassList(tab==recruitmentTab?"active":"recruit-tab");}
            if(recruitmentTab=="Synthèse"){RecruitmentHub(Scroll(content));return;}
            if(recruitmentTab=="Missions"){ScoutingMissions(Scroll(content));return;}if(recruitmentTab=="Rapports"){ScoutingReports(Scroll(content));return;}if(recruitmentTab!="Marché"){RecruitmentDossiers(Scroll(content));return;}
            var toolbar=Row(content,"recruit-toolbar");var searchTools=Row(toolbar,"recruit-search-tools");var field=new TextField("Nom"){value=recruitSearch,name="recruit-search",tooltip="Rechercher un joueur par son nom"};searchTools.Add(field);
            var clear=Button(searchTools,"Effacer",()=>{field.value="";});clear.name="recruit-clear";
            Button(searchTools,"Réinitialiser",ResetRecruitmentFilters).name="recruit-reset";
            var filters=Row(toolbar,"recruit-filters");
            var roles=ScoutingRoles();var role=new DropdownField(roles,recruitRole){name="recruit-role",tooltip="Filtrer les postes",formatListItemCallback=FrenchFootballPositions.Label,formatSelectedValueCallback=position=>position=="Tous"?"Postes":FrenchFootballPositions.Short(position)};filters.Add(role);
            var markets=new List<string>{"Tous","Libres","Bientôt libres","Fin de contrat ≤ 6 mois","Ma sélection","Observés"};var market=new DropdownField(markets,marketFilter){name="recruit-market",tooltip="Marché et sélection",formatSelectedValueCallback=selection=>root.ClassListContains("narrow")?(selection switch{"Tous"=>"Marché","Bientôt libres"=>"À libérer","Fin de contrat ≤ 6 mois"=>"≤ 6 mois","Ma sélection"=>"Suivis",_=>selection}):selection};filters.Add(market);
            var order=new DropdownField(new List<string>{"Valeur","Salaire","Âge","Nom","Note"},recruitOrder){name="recruit-order",tooltip="Ordre de recherche"};filters.Add(order);
            var advancedButton=Button(toolbar,recruitAdvanced?"Fermer les filtres":"Filtres +",()=>{});advancedButton.name="recruit-advanced-button";advancedButton.AddToClassList("recruit-advanced-button");advancedButton.tooltip="Pays, championnat, nationalité, âge et budgets";
            var summary=Text(content,"","recruit-results");summary.name="recruit-summary";
            var advanced=new Foldout{text="Filtres avancés",tooltip="Pays, championnat, nationalité, âge et budgets",value=recruitAdvanced,name="recruit-advanced"};advanced.AddToClassList("recruit-advanced");advanced.RegisterValueChangedCallback(e=>{recruitAdvanced=e.newValue;advancedButton.text=e.newValue?"Fermer les filtres":"Filtres +";advancedButton.EnableInClassList("active",e.newValue);});advancedButton.clicked+=()=>advanced.value=!advanced.value;advancedButton.EnableInClassList("active",recruitAdvanced);content.Add(advanced);var criteria=Scroll(advanced);criteria.name="recruit-criteria-scroll";criteria.AddToClassList("recruit-criteria-scroll");var advancedRow=Row(criteria,"recruit-advanced-row");
            var country=new DropdownField("Pays du club",ScoutingTerritories(),recruitCountry){name="recruit-country"};advancedRow.Add(country);
            var leagueChoices=new[]{"Tous"}.Concat(Database.leagues.Where(l=>recruitCountry=="Tous"||l.country==recruitCountry).Select(l=>l.name).Distinct().OrderBy(l=>l)).ToList();
            if(!leagueChoices.Contains(recruitLeague))recruitLeague="Tous";
            var league=new DropdownField("Championnat",leagueChoices,recruitLeague){name="recruit-league"};advancedRow.Add(league);
            var nationality=new DropdownField("Nationalité",ScoutingNationalities(),recruitNationality){name="recruit-nationality"};advancedRow.Add(nationality);
            var minAge=new IntegerField("Âge mini"){value=recruitMinAge,name="recruit-min-age",isDelayed=true};advancedRow.Add(minAge);var maxAge=new IntegerField("Âge maxi"){value=recruitMaxAge,name="recruit-max-age",isDelayed=true};advancedRow.Add(maxAge);
            var fee=new LongField("Valeur maxi (€)"){value=recruitMaxFee,name="recruit-max-fee",isDelayed=true};advancedRow.Add(fee);var salary=new LongField("Salaire maxi / mois (€)"){value=recruitMaxMonthly,name="recruit-max-salary",isDelayed=true};advancedRow.Add(salary);var criteriaHint=Text(criteria,"0 € signifie sans plafond. Pays et championnat filtrent le club actuel ; utilisez Tous pour les joueurs libres. Les valeurs et salaires sont des estimations de carrière, à confirmer avec l’agent.","footnote");criteriaHint.name="recruit-criteria-hint";criteriaHint.AddToClassList("recruit-criteria-hint");
            var empty=Text(content,"Aucun joueur trouvé. Modifiez la recherche ou les filtres.","empty-state");empty.name="recruit-empty";
            // Wide rows allow wrapped club/salary and observation details without overlap.
            var list=new ListView{name="recruit-list",fixedItemHeight=root.ClassListContains("narrow")?144:104,selectionType=SelectionType.None,virtualizationMethod=CollectionVirtualizationMethod.FixedHeight};list.AddToClassList("recruit-list");content.Add(list);
            portraits??=gameObject.AddComponent<PortraitStore>();var visible=new List<PlayerData>();
            list.makeItem=()=>new RecruitmentRow(this,()=>{if(marketFilter=="Ma sélection")Populate();else list.RefreshItems();});
            list.bindItem=(element,index)=>BindRecruitmentRow((RecruitmentRow)element,visible[index],index);
            list.unbindItem=(element,index)=>((RecruitmentRow)element).Release();
            void Populate()
            {
                var clubs=new HashSet<string>(ScoutingGeography.ClubIds(Database,recruitCountry,recruitLeague));

                var rows=Database.players.Where(p=>p.team!=Career.club&&p.team!="retired"&&!string.IsNullOrEmpty(p.team)&&!p.team.StartsWith("academy-")&&Core.FootballPositions.Matches(p,recruitRole)&&French.CompareInfo.IndexOf(p.name,recruitSearch,CompareOptions.IgnoreCase|CompareOptions.IgnoreNonSpace)>=0&&p.age>=recruitMinAge&&p.age<=recruitMaxAge&&(recruitNationality=="Tous"||p.nationality==recruitNationality)&&(recruitMaxFee<=0||p.team=="free"||p.value<=recruitMaxFee)&&(recruitMaxMonthly<=0||Core.Career.MonthlySalary(p.wage)<=recruitMaxMonthly));
                if(recruitCountry!="Tous"||recruitLeague!="Tous")rows=rows.Where(p=>clubs.Contains(p.team));
                if(marketFilter=="Libres")rows=rows.Where(p=>p.team=="free");else if(marketFilter=="Bientôt libres")rows=rows.Where(p=>Career.AnnouncedFreeAgentRelease(Database,p.id)!=null);else if(marketFilter=="Fin de contrat ≤ 6 mois")rows=rows.Where(p=>Career.CanPrecontract(Database,p.id));else if(marketFilter=="Ma sélection")rows=rows.Where(p=>Career.shortlist.Contains(p.id));else if(marketFilter=="Observés")rows=rows.Where(p=>Career.Knowledge(p.id)>=40);
                visible=recruitOrder=="Note"?rows.Select(p=>(p,card:Career.Knowledge(p.id)>=40?Career.ScoutReportCard(Database,p.id,CachedRecruitmentOverview()):null)).OrderBy(x=>GradeRank(x.card?.grade)).ThenByDescending(x=>x.p.value).ThenBy(x=>x.p.name).Select(x=>x.p).ToList():recruitOrder=="Salaire"?rows.OrderBy(p=>p.wage).ThenBy(p=>p.name).ToList():recruitOrder=="Âge"?rows.OrderBy(p=>p.age).ThenBy(p=>p.name).ToList():recruitOrder=="Nom"?rows.OrderBy(p=>p.name).ToList():rows.OrderByDescending(p=>p.value).ThenBy(p=>p.name).ToList();
                int activeFilters=new[]{!string.IsNullOrWhiteSpace(recruitSearch),recruitRole!="Tous",marketFilter!="Tous",recruitNationality!="Tous",recruitCountry!="Tous",recruitLeague!="Tous",recruitMinAge!=16,recruitMaxAge!=45,recruitMaxFee>0,recruitMaxMonthly>0}.Count(active=>active);
                summary.text=visible.Count+" profils · "+activeFilters+" filtre(s) actif(s) · "+Career.shortlist.Count+" suivis · "+Career.ActiveObservations+" observations en cours";
                empty.text=marketFilter=="Libres"&&(recruitCountry!="Tous"||recruitLeague!="Tous")?"Les filtres de pays ou de championnat portent sur le club actuel : ils masquent les joueurs libres. Choisissez Tous dans ces critères, ou utilisez Réinitialiser puis Libres.":"Aucun joueur trouvé. Modifiez la recherche ou utilisez Réinitialiser.";
                empty.style.display=visible.Count==0?DisplayStyle.Flex:DisplayStyle.None;list.style.display=visible.Count==0?DisplayStyle.None:DisplayStyle.Flex;list.itemsSource=visible;list.Rebuild();
            }
            IVisualElementScheduledItem pending=null;
            country.RegisterValueChangedCallback(e=>{recruitCountry=e.newValue;league.choices=new[]{"Tous"}.Concat(Database.leagues.Where(l=>recruitCountry=="Tous"||l.country==recruitCountry).Select(l=>l.name).Distinct().OrderBy(l=>l)).ToList();if(!league.choices.Contains(recruitLeague)){recruitLeague="Tous";league.SetValueWithoutNotify("Tous");}Populate();});league.RegisterValueChangedCallback(e=>{recruitLeague=e.newValue;Populate();});
            field.RegisterValueChangedCallback(e=>{recruitSearch=e.newValue;pending?.Pause();pending=field.schedule.Execute(Populate).StartingIn(120);});
            role.RegisterValueChangedCallback(e=>{recruitRole=e.newValue;Populate();});market.RegisterValueChangedCallback(e=>{marketFilter=e.newValue;Populate();});order.RegisterValueChangedCallback(e=>{recruitOrder=e.newValue;Populate();});nationality.RegisterValueChangedCallback(e=>{recruitNationality=e.newValue;Populate();});
            minAge.RegisterValueChangedCallback(e=>{recruitMinAge=Mathf.Clamp(e.newValue,16,45);recruitMaxAge=Math.Max(recruitMinAge,recruitMaxAge);minAge.SetValueWithoutNotify(recruitMinAge);maxAge.SetValueWithoutNotify(recruitMaxAge);Populate();});
            maxAge.RegisterValueChangedCallback(e=>{recruitMaxAge=Mathf.Clamp(e.newValue,16,45);recruitMinAge=Math.Min(recruitMaxAge,recruitMinAge);minAge.SetValueWithoutNotify(recruitMinAge);maxAge.SetValueWithoutNotify(recruitMaxAge);Populate();});
            fee.RegisterValueChangedCallback(e=>{recruitMaxFee=Math.Max(0,e.newValue);fee.SetValueWithoutNotify(recruitMaxFee);Populate();});salary.RegisterValueChangedCallback(e=>{recruitMaxMonthly=Math.Max(0,e.newValue);salary.SetValueWithoutNotify(recruitMaxMonthly);Populate();});Populate();
        }

        sealed class RecruitmentRow:VisualElement
        {
            public string player;public int version;public Image photo;public Label initials,identity,detail;public Button open,observe,agent,shortlist;
            public RecruitmentRow(TouchlineApp app,Action refresh)
            {
                AddToClassList("recruit-player-row");var portrait=new VisualElement();portrait.AddToClassList("recruit-portrait");Add(portrait);initials=Text(portrait,"","squad-row-initials");photo=new Image{scaleMode=ScaleMode.ScaleToFit};photo.AddToClassList("recruit-photo");portrait.Add(photo);
                var copy=new VisualElement();copy.AddToClassList("recruit-copy");Add(copy);identity=Text(copy,"","recruit-player-name");detail=Text(copy,"","recruit-player-detail");var actions=Row(this,"recruit-player-actions");
                open=Button(actions,"Fiche",()=>{if(player!=null)app.PlayerProfile(player);});
                observe=Button(actions,"Observer",()=>{if(player==null)return;try{app.Career.Scout(app.Database,player);app.Save();refresh();}catch(Exception e){app.Message(e.Message);}});
                agent=Button(actions,"Agent / offre",()=>{if(player!=null)app.TransferDialog(player);});
                shortlist=Button(actions,"☆",()=>{if(player==null)return;app.Career.ToggleShortlist(player);app.Save();refresh();});shortlist.AddToClassList("recruit-star");
                RegisterCallback<DetachFromPanelEvent>(_=>Release());
            }
            public void Release(){version++;player=null;photo.image=null;photo.style.display=DisplayStyle.None;initials.style.display=DisplayStyle.Flex;}
        }
        void BindRecruitmentRow(RecruitmentRow row,PlayerData p,int index)
        {
            row.Release();row.player=p.id;row.name="recruit-row-"+p.id;row.EnableInClassList("recruit-even",index%2==0);row.identity.text=p.name+" · "+p.age+" ans · "+FrenchFootballPositions.PlayerLabel(p);
            int knowledge=Career.Knowledge(p.id);var departure=Career.AnnouncedFreeAgentRelease(Database,p.id);string marketStatus=departure!=null?(string.IsNullOrEmpty(departure.contractEndsOn)?" · libération annoncée, échéance non vérifiée":" · fin annoncée "+DateTime.Parse(departure.contractEndsOn).ToString("dd MMM",French)):"";var assessment=knowledge>=40?Career.ScoutReportCard(Database,p.id,CachedRecruitmentOverview()):null;row.detail.text=ClubName(p.team)+(GeneratedWorld.IsGenerated(p.team)?" (fictif)":"")+" · "+Money(p.value)+" · "+Money(Core.Career.MonthlySalary(p.wage))+" / mois\n"+(assessment!=null&&assessment.ability.known?"Note "+assessment.grade+" · niveau "+ClubRatingScale.Range(assessment.ability,ClubRatingBaseline())+(assessment.potential.known?" · potentiel "+ClubRatingScale.Range(assessment.potential,ClubRatingBaseline()):""): "Niveau à observer")+" · connaissance "+knowledge+" %"+marketStatus;
            row.identity.tooltip=p.name+" · "+FrenchFootballPositions.PlayerList(p);row.detail.tooltip=row.detail.text;
            row.initials.text=string.Concat(p.name.Split(' ').Where(s=>s.Length>0).Take(2).Select(s=>s.Substring(0,1)));
            row.open.name="recruit-open-"+p.id;row.agent.name="recruit-agent-"+p.id;row.shortlist.name="recruit-shortlist-"+p.id;row.observe.name="recruit-observe-"+p.id;
            bool pending=Career.ReportFor(p.id)?.confidence<90;row.observe.text=pending?"En cours":knowledge>=90?"Observé":Career.ReportFor(p.id)!=null?"Actualiser":"Observer";row.observe.SetEnabled(!pending&&knowledge<90);row.observe.tooltip=pending?"Le rapport progresse pendant les prochains jours":"Mandater un recruteur";
            row.shortlist.text=Career.shortlist.Contains(p.id)?"★":"☆";row.shortlist.tooltip=Career.shortlist.Contains(p.id)?"Retirer de ma sélection":"Ajouter à ma sélection";
            int version=row.version;portraits.Load(p.id,texture=>{if(row.player!=p.id||row.version!=version||texture==null)return;row.photo.image=texture;row.photo.style.display=DisplayStyle.Flex;row.initials.style.display=DisplayStyle.None;});
        }
        void RecruitmentDossiers(VisualElement parent)
        {
            ActiveLoansPanel(parent);Heading(parent,"Négociations");
            var offers=Career.world.offers.Where(o=>o.destination==null||o.destination==Career.club).TakeLast(30).Reverse().ToArray();
            if(offers.Length==0)Text(parent,"Aucune négociation. Ouvrez une fiche ou contactez un agent depuis Marché.","empty-state");
            foreach(var o in offers){var p=Database.Find(o.player);if(p==null)continue;var card=Card(parent,"recruit-dossier");Text(card,p.name+" · "+OfferStatus(o.status),"section-title");Text(card,Money(o.fee)+" d’indemnité · "+Money(Core.Career.MonthlySalary(o.wage))+" / mois","muted");var actions=Row(card,"recruit-dossier-actions");Button(actions,"Fiche",()=>PlayerProfile(p.id));
                if(p.team=="retired"||Career.PlayerRetirementEffective(p.id)){Text(card,"Retraite effective · dossier historique", "muted");continue;}
                bool decision=Career.RecruitmentOfferNeedsDecision(Database,o);
                if(Career.HasActiveLoan(o.player)&&(o.status=="accepted"||o.status=="counter"))Button(actions,"Consulter le prêt",()=>ActiveLoanContractDialog(o.player));
                if(decision&&o.status=="accepted")Button(actions,"Signer l’accord",()=>RunDecision(()=>Career.SignTransfer(Database,o.player))).AddToClassList("primary");
                if(decision&&o.status=="counter")Button(actions,"Reprendre la négociation",()=>TransferDialog(o.player));
                if(decision&&o.status=="sale")Button(actions,"Accepter la vente · "+ClubName(o.seller),()=>Confirm("Accepter la cession ?",Money(o.fee)+" pour "+p.name,()=>RunDecision(()=>Career.AcceptSale(Database,o.player))));
                if(o.status=="pending"||o.status=="accepted")Button(actions,"Retirer l’offre",()=>RunDecision(()=>Career.RejectOffer(o.player)));
            }
        }
        static string OfferStatus(string status)=>status switch{"pending"=>"Réponse attendue","accepted"=>"Accord de principe","counter"=>"Contre-proposition","sale"=>"Offre de vente","signed"=>"Signé","scheduled"=>"Arrivée programmée","withdrawn"=>"Retiré","expired"=>"Expiré","declined"=>"Refusé",_=>status};
    }
}

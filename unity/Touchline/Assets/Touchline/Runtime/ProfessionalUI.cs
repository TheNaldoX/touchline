using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Touchline.Core;

namespace Touchline
{
    public sealed partial class TouchlineApp
    {
        string calendarCompetition,recruitSearch="",recruitRole="Tous";int recruitPage;bool presentationPending;AudioSource anthem;string marketFilter="Tous";
        string ClubName(string id)=>id=="free"?"Libre de contrat":Database.clubs.FirstOrDefault(c=>c.id==id)?.name??id;
        string FixtureDay(Fixture f)=>Touchline.Core.Career.Epoch.AddDays(f.day).ToString("dd MMM yyyy",French);
        void CalendarPage()=>CalendarWorkspace();
        void ClubProfile(string id)
        {
            var panel=Modal(ClubName(id));var s=Scroll(panel);var data=Database.clubs.First(c=>c.id==id);Text(s,"Recettes prévisionnelles de jeu : "+Money(data.annualRevenue),"muted");if(!string.IsNullOrEmpty(data.stadium))Text(s,"Stade : "+data.stadium+(data.stadiumCapacity>0?" · "+data.stadiumCapacity.ToString("N0",French)+" places annoncées":""));if(data.referenceRevenue>0){Text(s,"Revenus publiés "+data.referenceSeason+" : "+Money(data.referenceRevenue)+". Ce montant ne représente pas le budget de transferts.","muted");Text(s,data.referenceFinanceSource,"footnote");}if(!string.IsNullOrEmpty(data.sourceSeason))Text(s,"Effectif : "+data.sourceSeason,"muted");Text(s,data.financeSource??"Estimation de simulation","footnote");
            Button(s,"Sources et finances publiées",()=>ClubProvenancePanel(id)).name="club-sources";
            Heading(s,"Palmarès de la carrière");var honours=Career.world.honours.Where(h=>h.club==id).Reverse();foreach(var h in honours)Text(s,h.year+" / "+(h.year+1)+" · "+Career.CompetitionName(Database,h.competition));if(!honours.Any())Text(s,"Aucun titre enregistré dans cette carrière. L’historique réel antérieur reste disponible dans l’ancienne version.","muted");
            Heading(s,"Effectif");foreach(var p in Database.Squad(id)){var row=Row(s,"list-row");Text(row,p.name+" · "+p.age+" ans · "+FrenchFootballPositions.Label(p.position));Button(row,"Fiche",()=>PlayerProfile(p.id));}
        }
        void ProfessionalMatchLobby()
        {
            var s=Scroll(content);var f=Career.NextFixture();Heading(s,"Le rendez-vous");if(f==null){Text(s,"Aucun match restant. Continuez jusqu’à la nouvelle saison.");return;}
            var card=Card(s,"dashboard-hero");Text(card,Career.CompetitionName(Database,f.league).ToUpper(French),"eyebrow");Text(card,ClubName(f.home)+"\ncontre "+ClubName(f.away),"display-title");Text(card,FixtureDay(f)+" · "+(f.venue??"Stade de "+ClubName(f.home)));Text(card,f.published?"Calendrier publié · "+f.source:"Calendrier de votre carrière","footnote");
            Text(s,Career.StaffAdvice(Database));var controls=Row(s);Button(controls,"Composition",()=>Navigate("Tactique"));Button(controls,"Conférence",()=>Navigate("Presse"));Button(controls,"Entrer sur le terrain",()=>RunDecision(()=>StartCareerMatch(false))).AddToClassList("primary");Button(controls,"Déléguer à l’adjoint",()=>RunDecision(()=>StartCareerMatch(true)));
            var unavailable=Career.lineup.Where(id=>!Career.Available(id)).Select(id=>Database.Find(id).name).ToArray();if(unavailable.Length>0)Text(s,"À remplacer : "+string.Join(", ",unavailable)+". L’adjoint complétera le onze si vous lancez directement.","notice");
        }
        void RecruitmentPage()=>RecruitmentWorkspace();
        sealed class TransferFormDraft
        {
            public long fee,monthly,bonus,clause,option,obligation;
            public int years=3,role,share=100,days,appearances,upfront=100,instalments;
            public bool precontract,recall;
        }
        int LoanMaximumDays(Employment parent)=>Math.Min(365,Math.Max(0,parent.until-Career.life.day));
        int LoanDefaultDays(Employment parent)=>Math.Min(LoanMaximumDays(parent),Math.Max(28,Career.world.seasonEnd-Career.life.day));
        bool LoanDurationValid(Employment parent,int days)=>days>=28&&days<=LoanMaximumDays(parent);
        string LoanDurationText(Employment parent,int days)
        {
            int maximum=LoanMaximumDays(parent);
            if(maximum<28)return "Prêt indisponible : le contrat parent expire dans moins de 28 jours. Une prolongation est nécessaire.";
            if(days<28||days>365)return "Durée invalide : choisissez de 28 à "+maximum+" jours, sans dépasser le contrat parent.";
            if(days>maximum)return "Durée invalide : le prêt dépasse l’échéance du contrat parent. Maximum : "+maximum+" jours.";
            return "Fin du prêt : "+Core.Career.Epoch.AddDays(Career.life.day+days).ToString("dd MMM yyyy",French)+" · maximum "+maximum+" jours.";
        }
        void TransferDialog(string id)
        {
            var p=Database.Find(id);if(p==null){Message("Ce joueur n’est plus disponible.");return;}
            if(p.team=="retired"||Career.PlayerRetirementEffective(id)){Message("La retraite de ce joueur est effective. Aucun nouveau contrat de joueur ne peut être négocié.");return;}
            var accepted=Career.world.offers.LastOrDefault(o=>o.player==id&&(o.destination==null||o.destination==Career.club)&&o.status=="accepted"&&o.due+7>=Career.life.day);if(accepted!=null){TransferAgreementReview(accepted);return;}var outgoing=Career.outgoingLoans.LastOrDefault(o=>o.player==id&&o.owner==Career.club&&o.status=="accepted"&&o.due+7>=Career.life.day);if(outgoing!=null){OutgoingLoanAgreementReview(outgoing);return;}
            var panel=Modal("Négociation · "+p.name);panel.name="transfer-negotiation-form";var s=Scroll(panel);bool own=p.team==Career.club;
            var previous=Career.world.offers.LastOrDefault(o=>o.player==id&&o.status=="counter"&&(o.destination==null||o.destination==Career.club)&&o.seller==p.team);
            var normalPrevious=previous?.loan==false?previous:null;var loanPrevious=previous?.loan==true?previous:null;
            var range=Career.PlayerAgent(Database,id);var loanRange=Career.PlayerAgent(Database,id,true);var agent=Card(s);Text(agent,"AGENT · PREMIER ÉCHANGE","eyebrow");var agentMessage=Text(agent,range.message);agentMessage.name="negotiation-agent-message";
            var agentConditions=Text(agent,"");agentConditions.name="negotiation-agent-conditions";
            var contract=Career.Contract(Database,id);Text(s,"Échéance actuelle : "+Core.Career.Epoch.AddDays(contract.until).ToString("dd MMM yyyy",French)+(contract.estimated?" · estimation de jeu":""),"muted");
            var permanent=new TransferFormDraft{fee=own||p.team=="free"?0:normalPrevious?.fee??p.value,monthly=Core.Career.MonthlySalary(normalPrevious?.wage??(long)(p.wage*1.12f)),years=normalPrevious?.years??3,role=Array.IndexOf(PlayingTimeRoles.All,PlayingTimeRoles.Normalize(normalPrevious?.role??(own?contract.role:"rotation"))),bonus=normalPrevious?.bonus??0,clause=normalPrevious?.clause??0,precontract=normalPrevious?.precontract==true,upfront=normalPrevious?.terms?.upfrontPercent??100,instalments=normalPrevious?.terms?.instalments??0};
            var borrowed=new TransferFormDraft{fee=loanPrevious?.fee??loanRange.feeHigh,monthly=Core.Career.MonthlySalary(p.wage),years=loanPrevious?.years??3,role=Array.IndexOf(PlayingTimeRoles.All,PlayingTimeRoles.Normalize(loanPrevious?.role??"rotation")),bonus=loanPrevious?.bonus??0,clause=loanPrevious?.clause??0,share=loanPrevious?.terms?.loanWagePercent??100,days=loanPrevious?.terms!=null?loanPrevious.terms.loanEndDay-Career.life.day:LoanDefaultDays(contract),option=loanPrevious?.terms?.optionFee??0,obligation=loanPrevious?.terms?.obligationFee??0,appearances=loanPrevious?.terms?.obligationAppearances??0,recall=loanPrevious?.terms?.recall==true};
            var fee=new LongField("Indemnité proposée (€)");fee.name="negotiation-fee";s.Add(fee);fee.SetEnabled(!own&&p.team!="free");
            var wage=new LongField("Salaire mensuel proposé (€)");wage.name="negotiation-monthly-wage";s.Add(wage);var years=new IntegerField("Durée (années)");years.name="negotiation-years";s.Add(years);
            var roleCard=Card(s,"negotiation-role-card");Text(roleCard,"VOTRE PROJET SPORTIF","eyebrow");
            var role=new DropdownField("Temps de jeu promis",PlayingTimeRoles.All.Select(PlayingTimeRoles.Label).ToList(),2);role.name="negotiation-role";roleCard.Add(role);
            var roleDescription=Text(roleCard,"","notice");roleDescription.name="negotiation-role-description";
            Action refreshRole=()=>{string promised=PlayingTimeRoles.All[Mathf.Clamp(role.index,0,PlayingTimeRoles.All.Length-1)];roleDescription.text=PlayingTimeRoles.Description(promised)+(promised=="youth"&&p.age>=24?" Ce statut est réservé aux joueurs de moins de 24 ans. Choisissez un autre engagement.":"");};
            role.RegisterValueChangedCallback(_=>refreshRole());
            if(own)Text(roleCard,"Engagement actuel : "+PlayingTimeRoles.Label(contract.role),"muted").name="negotiation-current-role";
            Text(roleCard,"Le joueur et son agent évaluent le rôle proposé. Après la signature, votre utilisation du joueur compte dans sa satisfaction ; les indisponibilités médicales connues sont prises en compte.","footnote");
            var bonus=new LongField("Prime de présence (€)");bonus.name="negotiation-bonus";s.Add(bonus);var clause=new LongField("Clause libératoire (€), 0 = aucune");clause.name="negotiation-clause";s.Add(clause);
            var pre=new Toggle("Précontrat : arrivée après le contrat actuel");pre.name="negotiation-precontract";s.Add(pre);pre.SetEnabled(Career.CanPrecontract(Database,id));
            var loan=new Toggle("Négocier un prêt");loan.name="negotiation-loan";s.Add(loan);loan.SetEnabled(!own&&p.team!="free"&&contract.parent==null&&LoanMaximumDays(contract)>=28);if(!own&&p.team!="free"&&contract.parent==null&&LoanMaximumDays(contract)<28)Text(s,LoanDurationText(contract,0),"notice").name="negotiation-loan-unavailable";
            var terms=new Foldout{text="Prêt : salaire, durée et achat",value=false};terms.name="negotiation-loan-terms";s.Add(terms);
            var share=new IntegerField("Salaire pris en charge (%)");share.name="negotiation-loan-share";terms.Add(share);var days=new IntegerField("Durée du prêt (jours)");days.name="negotiation-loan-days";terms.Add(days);
            var option=new LongField("Option d’achat (€), 0 = aucune");option.name="negotiation-loan-option";terms.Add(option);var obligation=new LongField("Obligation d’achat (€), 0 = aucune");obligation.name="negotiation-loan-obligation";terms.Add(obligation);var appearances=new IntegerField("Déclenchement après apparitions (0 = automatique)");appearances.name="negotiation-loan-appearances";terms.Add(appearances);var recall=new Toggle("Rappel possible pendant le mercato");recall.name="negotiation-loan-recall";terms.Add(recall);
            var payments=new Foldout{text="Échelonnement de l’indemnité",value=false};s.Add(payments);var upfront=new IntegerField("Part payée immédiatement (%)");upfront.name="negotiation-upfront";payments.Add(upfront);var instalments=new IntegerField("Échéances trimestrielles (0 à 4)");instalments.name="negotiation-instalments";payments.Add(instalments);
            var endDate=Text(terms,"","muted");endDate.name="negotiation-loan-end-date";var contribution=Text(terms,"","notice");contribution.name="loan-monthly-contributions";bool loading=false;Button submit=null;
            Action refreshContribution=()=>{
                contribution.style.display=loan.value?DisplayStyle.Flex:DisplayStyle.None;
                endDate.text=LoanDurationText(contract,days.value);endDate.EnableInClassList("notice",!LoanDurationValid(contract,days.value));
                bool valid=share.value>=0&&share.value<=100;submit?.SetEnabled(PlayerManagementAvailable&&(PlayingTimeRoles.All[Mathf.Clamp(role.index,0,PlayingTimeRoles.All.Length-1)]!="youth"||p.age<24)&&(!loan.value||(valid&&loan.enabledSelf&&LoanDurationValid(contract,days.value))));
                if(!valid){contribution.text="Pourcentage invalide : indiquez une prise en charge entre 0 et 100 %. Aucun accord ne sera envoyé avec cette valeur.";return;}
                long borrower=p.wage*share.value/100,owner=p.wage-borrower;contribution.text="Votre club : "+Money(Core.Career.MonthlySalary(borrower))+" / mois · "+ClubName(p.team)+" : "+Money(Core.Career.MonthlySalary(owner))+" / mois. Répartition du contrat parent, sans augmentation salariale.";
            };
            Action<TransferFormDraft> capture=d=>{d.fee=fee.value;d.monthly=wage.value;d.years=years.value;d.role=role.index;d.bonus=bonus.value;d.clause=clause.value;d.precontract=pre.value;d.share=share.value;d.days=days.value;d.option=option.value;d.obligation=obligation.value;d.appearances=appearances.value;d.recall=recall.value;d.upfront=upfront.value;d.instalments=instalments.value;};
            Action<bool> restore=isLoan=>{
                loading=true;var d=isLoan?borrowed:permanent;loan.SetValueWithoutNotify(isLoan);pre.SetValueWithoutNotify(!isLoan&&d.precontract);fee.SetValueWithoutNotify(d.fee);wage.SetValueWithoutNotify(isLoan?Core.Career.MonthlySalary(p.wage):d.monthly);years.SetValueWithoutNotify(d.years);role.SetValueWithoutNotify(role.choices[Mathf.Clamp(d.role,0,role.choices.Count-1)]);bonus.SetValueWithoutNotify(d.bonus);clause.SetValueWithoutNotify(d.clause);share.SetValueWithoutNotify(d.share);days.SetValueWithoutNotify(d.days);option.SetValueWithoutNotify(d.option);obligation.SetValueWithoutNotify(d.obligation);appearances.SetValueWithoutNotify(d.appearances);recall.SetValueWithoutNotify(d.recall);upfront.SetValueWithoutNotify(d.upfront);instalments.SetValueWithoutNotify(d.instalments);
                wage.SetEnabled(!isLoan);wage.label=isLoan?"Salaire mensuel du contrat parent (€)":"Salaire mensuel proposé (€)";terms.SetEnabled(isLoan);terms.value=isLoan;payments.SetEnabled(!isLoan&&!pre.value);years.SetEnabled(!isLoan);
                var estimate=isLoan?loanRange:range;agentMessage.text=estimate.message;agentConditions.text="Indemnité indicative : "+Money(estimate.feeLow)+" à "+Money(estimate.feeHigh)+(isLoan?" · Contrat parent : "+Money(estimate.monthlyLow)+" / mois. Votre part se règle en pourcentage.":" · Salaire : "+Money(estimate.monthlyLow)+" à "+Money(estimate.monthlyHigh)+" / mois");loading=false;refreshContribution();refreshRole();
            };
            loan.RegisterValueChangedCallback(e=>{if(loading)return;capture(e.previousValue?borrowed:permanent);restore(e.newValue);});
            long transferFeeBeforePre=permanent.precontract&&!own&&p.team!="free"?p.value:permanent.fee;
            pre.RegisterValueChangedCallback(e=>{if(loading)return;if(e.newValue){if(loan.value){loan.value=false;pre.SetValueWithoutNotify(true);}transferFeeBeforePre=fee.value;fee.value=0;}else fee.value=transferFeeBeforePre;payments.SetEnabled(!loan.value&&!pre.value);});
            share.RegisterValueChangedCallback(e=>refreshContribution());days.RegisterValueChangedCallback(e=>refreshContribution());role.RegisterValueChangedCallback(e=>refreshContribution());
            Text(s,"La prime d’agent représente deux semaines du salaire intégral. Le salaire mensuel est converti en coût annuel sur 12 mois. Le staff ne signera jamais à votre place.","footnote");
            var error=Text(s,"","notice");error.name="negotiation-error";error.style.display=DisplayStyle.None;error.style.fontSize=14;error.style.whiteSpace=WhiteSpace.Normal;error.focusable=true;
            void ShowError(string message){error.text=message+" Vos conditions sont conservées : corrigez-les puis renvoyez l’offre.";error.style.display=DisplayStyle.Flex;error.schedule.Execute(()=>{s.ScrollTo(error);error.Focus();});}
            submit=Button(s,"Envoyer à l’agent",()=>{error.style.display=DisplayStyle.None;RunDecision(()=>Career.ProposeTransfer(Database,id,fee.value,loan.value?p.wage:Core.Career.WeeklySalary(wage.value),years.value,PlayingTimeRoles.All[Mathf.Clamp(role.index,0,PlayingTimeRoles.All.Length-1)],loan.value,bonus.value,clause.value,new MarketTerms{loanWagePercent=loan.value?share.value:100,loanEndDay=Career.life.day+days.value,optionFee=loan.value?option.value:0,obligationFee=loan.value?obligation.value:0,obligationAppearances=loan.value?appearances.value:0,recall=loan.value&&recall.value,upfrontPercent=loan.value||pre.value?100:upfront.value,instalments=loan.value||pre.value?0:instalments.value},pre.value),ShowError);});submit.name="negotiation-send";submit.AddToClassList("primary");
            restore(loanPrevious!=null);
        }


        void AcademyPage()=>AcademyWorkspace();
        void YouthLoanDialog(string id)
        {
            var panel=Modal("Proposer un prêt · "+Database.Find(id).name);panel.name="outgoing-loan-form";var s=Scroll(panel);
            var parent=Career.Contract(Database,id);Text(s,"Échéance du contrat parent : "+Core.Career.Epoch.AddDays(parent.until).ToString("dd MMM yyyy",French)+(parent.estimated?" · estimation de jeu":""),"muted");
            var mode=new DropdownField("Destinations",new List<string>{"Pistes recommandées","Tous les clubs éligibles"},0);mode.name="outgoing-loan-mode";s.Add(mode);
            var search=new TextField("Rechercher un club, pays ou ligue");search.name="outgoing-loan-search";s.Add(search);
            var opponent=new DropdownField("Club emprunteur",new List<string>{"Aucun club"},0);opponent.name="outgoing-loan-club";s.Add(opponent);
            var selection=Text(s,"","muted");selection.name="outgoing-loan-selection";var advice=Text(s,"","notice");advice.name="outgoing-loan-advice";
            var wage=new IntegerField("Part salariale du club d’accueil (%)"){value=50};wage.name="outgoing-loan-share";s.Add(wage);
            var fee=new LongField("Indemnité demandée (€)");fee.name="outgoing-loan-fee";s.Add(fee);var days=new IntegerField("Durée (jours)"){value=LoanDefaultDays(parent)};days.name="outgoing-loan-days";s.Add(days);
            var option=new LongField("Option d’achat (€), 0 = aucune");option.name="outgoing-loan-option";s.Add(option);var obligation=new LongField("Obligation d’achat (€), 0 = aucune");obligation.name="outgoing-loan-obligation";s.Add(obligation);var count=new IntegerField("Apparitions déclenchant l’obligation (0 = automatique)");count.name="outgoing-loan-appearances";s.Add(count);var recall=new Toggle("Clause de rappel, incompatible avec obligation");recall.name="outgoing-loan-recall";s.Add(recall);
            var duration=Text(s,"","muted");duration.name="outgoing-loan-end-date";var shareError=Text(s,"","notice");shareError.name="outgoing-loan-share-error";
            Text(s,"Recommandations selon ce joueur, ses postes, la concurrence et les moyens simulés du club. Les minutes sont une estimation par rencontre jouée, jamais une garantie de titularisation ou une révélation du potentiel.","footnote");
            string selected=null;bool loading=false;var reports=new List<LoanProspect>();var pickerIds=new List<string>();
            MarketTerms Terms()=>new MarketTerms{loanWagePercent=wage.value,loanEndDay=Career.life.day+days.value,optionFee=option.value,obligationFee=obligation.value,obligationAppearances=count.value,recall=recall.value};
            var submit=Button(s,"Soumettre le prêt",()=>RunDecision(()=>Career.ProposeOutgoingLoan(Database,id,selected,fee.value,Terms())));submit.name="outgoing-loan-send";
            var leagues=Database.leagues.ToDictionary(l=>l.id);
            string ClubLabel(LoanProspect report){leagues.TryGetValue(report.club.league??"",out var league);return report.club.name+" · "+(league?.country??"")+" · "+(league?.name??report.club.league);}
            Action updateAdvice=()=>{
                var report=reports.FirstOrDefault(r=>r.club.id==selected);bool validShare=wage.value>=0&&wage.value<=100;
                duration.text=LoanDurationText(parent,days.value);duration.EnableInClassList("notice",!LoanDurationValid(parent,days.value));shareError.style.display=validShare?DisplayStyle.None:DisplayStyle.Flex;shareError.text="Pourcentage invalide : indiquez une prise en charge entre 0 et 100 %.";
                submit.SetEnabled(PlayerManagementAvailable&&parent.parent==null&&LoanDurationValid(parent,days.value)&&validShare&&report!=null);
                if(report==null){advice.text="Aucun club admissible avec au moins onze joueurs. Élargissez votre base de données.";return;}
                advice.text="Poste envisagé : "+FrenchFootballPositions.Label(report.role)+" · "+report.competition+" joueur(s) déjà capables d’y évoluer. "+(report.levelCompatible?"Niveau compatible avec les critères du club.":"Écart de niveau : le club ou le joueur devrait refuser.")+"\n"+(report.affordable?"Coût proposé compatible avec l’enveloppe simulée.":"Coût ou obligation au-dessus des moyens estimés ; réduisez les conditions.")+"\n"+(report.calendarLoaded?"Temps de jeu projeté : environ "+report.estimatedMinutesPerMatch+" minutes par rencontre réellement jouée, sous réserve de disponibilité et de concurrence.":"Pas de rencontre programmée dans la durée proposée : aucune minute de match ne sera inventée pour la progression.");
            };
            Action updatePicker=()=>{
                var pool=mode.index==0?reports.Take(12):reports.AsEnumerable();var matches=pool.Where(r=>MessageContains(ClubLabel(r),search.value)).ToList();
                var current=reports.FirstOrDefault(r=>r.club.id==selected);var visible=current==null?matches:new[]{current}.Concat(matches.Where(r=>r.club.id!=selected)).ToList();
                loading=true;pickerIds=visible.Select(r=>r.club.id).ToList();opponent.choices=visible.Select(ClubLabel).DefaultIfEmpty("Aucun club").ToList();opponent.SetValueWithoutNotify(opponent.choices[0]);loading=false;
                selection.text=matches.Count+" résultat(s) · "+reports.Count+" clubs éligibles au total"+(current==null?"":" · sélection conservée : "+current.club.name);updateAdvice();
            };
            Action refresh=()=>{reports=Career.LoanClubRecommendations(Database,id,Terms(),fee.value);if(selected==null&&reports.Count>0)selected=reports[0].club.id;updatePicker();};
            opponent.RegisterValueChangedCallback(e=>{if(loading||opponent.index<0||opponent.index>=pickerIds.Count)return;selected=pickerIds[opponent.index];updatePicker();});
            mode.RegisterValueChangedCallback(e=>updatePicker());search.RegisterValueChangedCallback(e=>updatePicker());days.RegisterValueChangedCallback(e=>refresh());wage.RegisterValueChangedCallback(e=>refresh());fee.RegisterValueChangedCallback(e=>refresh());obligation.RegisterValueChangedCallback(e=>refresh());refresh();
        }
        void FinancePage()
        {
            var s=Scroll(content);Heading(s,"Les moyens de votre ambition");var w=Career.world;var metrics=Row(s,"metric-grid");Metric(metrics,"TRÉSORERIE",Money(Career.life.cash),"Disponible");Metric(metrics,"DETTE",Money(w.debt),"Avances du propriétaire");Metric(metrics,"SALAIRES",Money(Core.Career.MonthlySalary(Career.Payroll(Database))),"Chaque mois");Metric(metrics,"TRANSFERTS",Money(Career.TransferBudget),"Plafond de dépenses");
            var employment=Career.AnnualEmploymentCosts(Database);var payroll=Card(s);Text(payroll,"Le coût du personnel","section-title");
            Text(payroll,"Joueurs · brut : "+Money(employment.playersGross/12)+" / mois · cotisations projetées : "+Money(employment.playerContributions/12));
            Text(payroll,"Staff sous contrat · brut : "+Money(employment.staffGross/12)+" / mois · cotisations projetées : "+Money(employment.staffContributions/12));
            Text(payroll,"Autres personnels : "+Money(employment.otherPersonnel/12)+" / mois · total personnel : "+Money(employment.Total/12)+" / mois");
            Text(payroll,"Plafond des salaires bruts joueurs : "+Money(Core.Career.MonthlySalary(Career.WageBudget))+" / mois. Il tient compte des coûts employeur.","muted");
            Text(payroll,Career.OperatingCostSource(Database),"footnote");
            var gate=Card(s);Text(gate,"Le stade et ses supporters","section-title");var price=new IntegerField("Prix moyen d’une place (€)"){value=w.ticket};gate.Add(price);Text(gate,"Affluence estimée : "+(Career.Occupancy*100).ToString("0")+" % · Recette nette estimée : "+Money(Career.GateIncome()),"muted");Button(gate,"Appliquer ce tarif",()=>RunDecision(()=>Career.SetTicketPrice(price.value)));Text(gate,"Une modification par semaine. Un prix trop élevé réduit l’affluence et peut détériorer la confiance des supporters.","footnote");
            Heading(s,"Vos partenaires");Text(s,"Entreprises fictives et montants simulés, dimensionnés aux recettes du club. Un engagement actif par emplacement.","muted");
            for(int i=0;i<w.sponsors.Count;i++){int index=i;var d=w.sponsors[i];var card=Card(s);Text(card,d.name+" · "+d.status,"section-title");Text(card,Money(d.annual)+" / an · "+d.years+" ans");if(d.status=="signed")Text(card,"Échéance : "+Touchline.Core.Career.Epoch.AddDays(d.until).ToString("dd MMM yyyy",French));else if(d.status!="expired"){var amount=new LongField("Demande annuelle (€)"){value=d.asking>0?d.asking:d.annual};card.Add(amount);var years=new IntegerField("Durée (1 à 5 ans)"){value=d.years};card.Add(years);Button(card,"Négocier",()=>RunDecision(()=>Career.NegotiateSponsor(index,amount.value,years.value)));if(d.status=="accepted"||d.status=="counter")Button(card,"Signer à "+Money(d.asking)+" / an",()=>RunDecision(()=>Career.SignSponsor(index))).AddToClassList("primary");}}
            if(w.debt>0){var debt=new LongField("Rembourser (€)"){value=Math.Min(w.debt,Math.Max(0,Career.life.cash/10))};s.Add(debt);Button(s,"Rembourser la dette",()=>RunDecision(()=>Career.RepayDebt(debt.value)));}
            Heading(s,"Mouvements comptables");foreach(var item in Career.life.ledger.TakeLast(35).Reverse())Text(s,Touchline.Core.Career.Epoch.AddDays(item.day).ToString("dd MMM",French)+" · "+item.label+" · "+Money(item.amount));Text(s,Career.life.financeSource,"footnote");
        }
        void PressPage()
        {
            var s=Scroll(content);Heading(s,"La salle de presse");var f=Career.NextFixture();var stage=Card(s,"press-stage");Text(stage,"CONFÉRENCE DE PRESSE","eyebrow");Text(stage,f?.venue??"Centre d’entraînement · "+Own.name,"display-title");var wall=Row(stage,"sponsor-wall");foreach(var d in Career.world.sponsors.Where(d=>d.status=="signed"))Text(wall,d.name,"pill");Text(wall,Own.name,"pill");Text(s,"Votre message agit sur le moral et sur les attentes. Une prise de parole par conférence.","muted");
            foreach(var phase in new[]{"before","after"}){var card=Card(s);Text(card,phase=="before"?"Avant-match : quel est votre objectif ?":"Après-match : quel message pour le groupe ?","section-title");foreach(var answer in new[]{"calm","ambition","protect"})Button(card,answer=="calm"?"Rester mesuré":answer=="ambition"?"Afficher notre ambition":"Protéger mes joueurs",()=>RunDecision(()=>Career.Press(phase,answer)));}
        }
        void ManagerPage()
        {
            var s=Scroll(content);Heading(s,"Votre parcours");Text(s,Career.manager+" · "+Career.world.managerStatus,"display-title");Text(s,"Réputation : "+Career.life.reputation.ToString("0")+" · Confiance du conseil : "+Career.life.boardTrust.ToString("0")+" %");Text(s,"Propriétaire : "+Career.world.owner,"muted");
            ShowJobOffers(s);
            if(Career.world.managerStatus=="dismissed"){Text(s,"De nouveaux postes deviennent accessibles sept jours après votre départ.");foreach(var c in Database.clubs.Where(c=>c.id!=Career.club&&c.playable&&c.annualRevenue<=Career.life.revenue*1.5).Take(20))Button(s,"Postuler · "+c.name,()=>RunDecision(()=>Career.TakeJob(Database,c.id)));}
            Heading(s,"Archives des saisons");foreach(var h in Career.world.honours.Where(h=>h.club==Career.club).Reverse())Text(s,h.year+" · "+Career.CompetitionName(Database,h.competition));
        }
        void SettingsPage()=>SettingsControls(Scroll(content),false);
        void SettingsControls(VisualElement s,bool fromLaunch)
        {
            Heading(s,"Votre expérience");var budget=GetComponent<RenderBudget>();var quality=new DropdownField("Rendu 3D",new List<string>{"Économie · 30 images/s","Équilibré · 60 images/s","Qualité · 60 images/s"},PlayerPrefs.GetInt("render-quality",1));s.Add(quality);quality.RegisterValueChangedCallback(e=>{PlayerPrefs.SetInt("render-quality",quality.index);budget.SetQuality(quality.index);var label=s.Q<Label>("match-performance-summary");if(label!=null)label.text=MatchPerformanceText();});
            var interfaceScale=new DropdownField("Taille de l’interface",new List<string>{"Compacte","Standard","Grande"},PlayerPrefs.GetInt("interface-size",1));s.Add(interfaceScale);
            interfaceScale.RegisterValueChangedCallback(e=>{PlayerPrefs.SetInt("interface-size",interfaceScale.index);PlayerPrefs.Save();});
            Text(s,"L’interface s’adapte à l’écran ouvert ou fermé. Ce réglage change les textes et les commandes, indépendamment de la qualité 3D.","muted");
            var motion=new Toggle("Réduire les animations de l’interface"){value=!MotionEnabled};s.Add(motion);motion.RegisterValueChangedCallback(e=>{PlayerPrefs.SetInt("reduce-motion",e.newValue?1:0);PlayerPrefs.Save();root.EnableInClassList("reduce-motion",e.newValue);});
            var volume=new Slider("Volume des hymnes",0,1){value=PlayerPrefs.GetFloat("music-volume",.55f)};s.Add(volume);volume.RegisterValueChangedCallback(e=>{PlayerPrefs.SetFloat("music-volume",e.newValue);if(anthem!=null)anthem.volume=e.newValue;});
            Heading(s,"Dernières images en match");Text(s,MatchPerformanceText()).name="match-performance-summary";Text(s,"Les pauses et les menus ne remplacent pas cette mesure. Changer de qualité ou lancer un nouveau match réinitialise l’échantillon.","muted");Button(s,"Copier le diagnostic",()=>{GUIUtility.systemCopyBuffer=MatchDiagnostic;Message("Diagnostic copié.");});Button(s,"Exporter le diagnostic",()=>{var path=System.IO.Path.Combine(Application.persistentDataPath,"performance.txt");System.IO.File.WriteAllText(path,MatchDiagnostic);Message("Diagnostic enregistré : "+path);});
            if(!fromLaunch){
                Heading(s,"Votre carrière");var actions=Row(s);
                Button(actions,"Sauvegarder maintenant",()=>{Save();Message(lastCareerSaveSucceeded?"Votre carrière est enregistrée.":"La sauvegarde a échoué. Votre partie reste ouverte.");}).name="manager-save-now";
                Button(actions,"Menu principal",ReturnToLaunchMenu).name="settings-main-menu";
            }
            var credits=new Foldout{text="Crédits des animations et modèles",value=false};s.Add(credits);
            Text(credits,"Plongeons : DataDivingGoalkeepers — Rafael Monteiro et collaborateurs (2022), GPL-3.0. Coordonnées adaptées et recalées sur le contact du jeu. Sources : github.com/rafaellmmonteiro/DataDivingGoalkeepers ; conversion fournie dans le projet Touchline.","muted");
            Text(credits,"The 100STYLE Dataset — Ian Mason, Sebastian Starke, Taku Komura. Licence CC BY 4.0. Captures adaptées, mises à l’échelle et mélangées pour Touchline.","muted");
            Text(credits,"www.ianxmason.com/100style/ · creativecommons.org/licenses/by/4.0/","muted");
            Text(credits,"Captures complémentaires : CMU Graphics Lab, conversion BVH de Bruce Hahne. Modèle humain, pondérations, textures de peau, yeux et cheveux : MakeHuman Community, CC0. Corps génériques, sans ressemblance 3D vérifiée avec les joueurs réels.","muted");
        }
        void ShowJobOffers(VisualElement s)
        {
            Text(s,"Identité de jeu : "+Career.Philosophy+" · Performance au-delà du budget : "+Career.Overperformance(Database).ToString("0.00"),"muted");
            foreach(var o in Career.approaches.Where(o=>o.status=="open"&&o.until>=Career.life.day&&o.club!=Career.club).OrderBy(o=>o.until)){var c=Card(s);Text(c,"Approche · "+ClubName(o.club),"section-title");Text(c,o.reason);Text(c,"Compatibilité : "+o.fit.ToString("0")+" % · Salaire proposé : "+Money(Core.Career.MonthlySalary(o.weeklySalary))+" / mois · réponse au plus tard "+AgreementDate(o.until),"muted");var accept=Button(c,"Accepter le poste",()=>Confirm("Rejoindre "+ClubName(o.club)+" ?","Vous quittez votre poste actuel et prenez en charge les moyens du nouveau club, à la même date.",()=>RunDecision(()=>Career.AnswerApproach(Database,o.club,true))));accept.SetEnabled(CareerApproachActionsAvailable);Button(c,"Décliner",()=>RunDecision(()=>Career.AnswerApproach(Database,o.club,false))).SetEnabled(CareerApproachActionsAvailable);}
        }
    }
}

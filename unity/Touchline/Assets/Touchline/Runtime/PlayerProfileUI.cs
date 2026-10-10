using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using Touchline.Core;

namespace Touchline
{
    public sealed partial class TouchlineApp
    {
        PortraitStore portraits;
        bool PlayerManagementAvailable=>(Career.world==null||Career.world.managerStatus=="employed")&&(Career.match==null||Career.match.finished)&&Career.life.managerBanUntil<=Career.life.day;
        Button PlayerManagementButton(VisualElement parent,string text,Action action)
        {
            var button=Button(parent,text,action);button.SetEnabled(PlayerManagementAvailable);
            if(!PlayerManagementAvailable)button.tooltip=Career.world!=null&&Career.world.managerStatus!="employed"?"Vous n’êtes plus en poste dans ce club.":Career.match?.finished==false?"Cette décision sera disponible après le match.":"L’adjoint gère les décisions jusqu’à votre retour de suspension.";
            return button;
        }
        float PlayerFitness(PlayerData p)=>Career.match?.actors.FirstOrDefault(a=>a.id==p.id)?.fitness??p.fitness;
        public void PlayerProfile(string id)=>PlayerProfile(id,null);
        public void PlayerProfile(string id,Action returnToContext,string returnLabel="Retour à l’analyse")
        {
            var p=Database.Find(id);if(p==null){Message("Ce joueur n’est plus disponible dans la base.");return;}
            if(arena!=null)arena.Paused=true;
            var panel=Modal(p.name);panel.AddToClassList("player-profile");panel.name="player-profile";panel.Children().First().AddToClassList("profile-top");
            int knowledge=Career.world==null?(p.team==Career.club||Career.revealAttributes?100:0):Career.Knowledge(id);
            bool own=p.team==Career.club;
            var hero=Row(panel,"profile-hero");var portrait=new VisualElement();portrait.AddToClassList("profile-portrait");hero.Add(portrait);
            var initials=string.Concat(p.name.Split(' ').Where(s=>s.Length>0).Take(2).Select(s=>s.Substring(0,1)));Text(portrait,initials,"portrait-initials");Text(portrait,"Sans portrait","portrait-missing");
            portraits??=gameObject.AddComponent<PortraitStore>();portraits.Load(id,texture=>{if(texture==null||portrait.panel==null)return;portrait.Clear();var image=new Image{image=texture,scaleMode=ScaleMode.ScaleToFit,name="player-photo"};image.style.flexGrow=1;image.style.width=Length.Percent(100);image.style.height=Length.Percent(100);portrait.Add(image);});
            var identity=new VisualElement();identity.AddToClassList("profile-identity");hero.Add(identity);
            Text(identity,p.team=="retired"?"Retraité":ClubName(p.team),"profile-club");Text(identity,p.age+" ans · "+p.nationality,"muted");Text(identity,FrenchFootballPositions.PlayerList(p)+" · "+(string.IsNullOrEmpty(p.preferredFoot)?"Pied non renseigné":"Pied "+(p.preferredFoot=="Left"?"gauche":p.preferredFoot=="Right"?"droit":p.preferredFoot.ToLower(French))),"muted").name="profile-positions";
            float baseline=Database.Squad(Career.club).OrderByDescending(x=>x.rating+x.development).Take(11).Select(x=>x.rating+x.development).DefaultIfEmpty(65).Average();
            var report=Career.ReportFor(id);float level=Career.world!=null?Career.AssessedLevel(Database,id):p.rating+p.development;
            var evaluation=new VisualElement();evaluation.AddToClassList("profile-evaluation");hero.Add(evaluation);
            if(p.team=="retired")Text(evaluation,"Carrière de joueur terminée","profile-stars");
            else{
                Text(evaluation,knowledge>=40?Stars(level,baseline)+"  Niveau dans votre club":"Niveau à observer","profile-stars");
                Text(evaluation,knowledge>=65?Stars(Career.AssessedLevel(Database,id,true),baseline)+"  Potentiel estimé · incertain":"Potentiel encore incertain","profile-potential");
            }
            var tabs=Row(panel,"profile-tabs");var body=Scroll(panel);body.name="profile-body";body.AddToClassList("profile-body");
            void Show(string tab){foreach(var button in tabs.Query<Button>().ToList())button.EnableInClassList("active",button.text==tab);body.Clear();body.scrollOffset=Vector2.zero;
                body.name="profile-body";switch(tab){case "Attributs":ProfileAttributes(body,p,knowledge);break;case "Contrat":ProfileContract(body,p);break;case "Relations":ProfileRelations(body,p);break;case "Sources":ProfileProvenance(body,p,knowledge);break;default:ProfileOverview(body,p,knowledge);break;}
                AnimateEntry(body);
            }
            foreach(var tab in new[]{"Vue d’ensemble","Attributs","Contrat","Relations","Sources"})Button(tabs,tab,()=>Show(tab));Show("Vue d’ensemble");
            var actions=Row(panel,"profile-actions");if(returnToContext!=null)Button(actions,returnLabel,returnToContext);Button(actions,"Comparer",()=>ComparePlayer(id)).name="profile-compare";if(Career.world!=null&&Career.world.managerStatus=="employed"){if(own){PlayerManagementButton(actions,Career.HasActiveLoan(id)?"Contrat de prêt":"Contrat / prolonger",()=>TransferDialog(id));PlayerManagementButton(actions,"SMS / Appeler",()=>Conversation(id)).AddToClassList("primary");}else if(p.team!="retired"){var observe=Button(actions,"Observer",()=>{try{Career.Scout(Database,id);Save();PlayerProfile(id,returnToContext,returnLabel);}catch(Exception e){Message(e.Message);}});bool observing=Career.ReportFor(id)?.confidence<90;observe.SetEnabled(PlayerManagementAvailable&&!observing&&knowledge<90);if(!PlayerManagementAvailable)observe.tooltip="L’observation pourra être lancée lorsque vos décisions de manager seront disponibles, après le match ou votre retour en poste.";observe.text=observing?"Observation en cours":knowledge>=90?"Rapport complet":report!=null?"Actualiser l’observation":"Observer";Button(actions,Career.shortlist.Contains(id)?"★ Suivi":"☆ Suivre",()=>{Career.ToggleShortlist(id);Save();PlayerProfile(id,returnToContext,returnLabel);});PlayerManagementButton(actions,"Négocier",()=>TransferDialog(id)).AddToClassList("primary");}}
        }
        static string Stars(float rating,float baseline)=>ClubRatingScale.Stars(rating,baseline);
        void ProfileOverview(VisualElement body,PlayerData p,int knowledge)
        {
            var metrics=Row(body,"profile-metrics");ProfileMetric(metrics,"ÂGE",p.age+" ans");ProfileMetric(metrics,p.team=="retired"?"DERNIÈRE VALEUR ESTIMÉE":"VALEUR ESTIMÉE",Money(p.value));ProfileMetric(metrics,p.team=="retired"?"DERNIER SALAIRE CONTRACTUEL MENSUEL":p.team=="free"?"PRÉTENTIONS / MOIS":"SALAIRE EN JEU / MOIS",Money(Core.Career.MonthlySalary(p.wage)));
            Text(body,"Valeurs de carrière · dates, références et estimations détaillées dans Sources.","footnote");
            if(p.team==Career.club){var state=Career.Person(p.id);ProfileGauge(body,"Condition physique",PlayerFitness(p));ProfileGauge(body,"Moral",state.morale);var actor=Career.match?.actors.FirstOrDefault(a=>a.id==p.id);Text(body,actor?.sentOff==true?"Exclu de la rencontre":actor?.injured==true?"Blessé pendant la rencontre":Career.Available(p.id)?"Disponible pour la sélection":Career.Injury(p.id)!=null?"Blessé · consultez le dossier médical":state.restUntil>Career.life.day?"Repos convenu jusqu’au "+Core.Career.Epoch.AddDays(state.restUntil).ToString("dd MMM",French):state.banUntil>Career.life.day?"Suspendu · indisponible pour la sélection":"Indisponible pour la sélection","profile-availability");if(actor!=null)Text(body,"Condition mesurée pendant le match en cours.","muted");if(Career.Injury(p.id)!=null)Button(body,"Dossier médical",()=>MedicalDetail(p.id));}
            var info=Card(body);Text(info,p.team=="retired"?"Dossier conservé":"Rapport de votre staff","section-title");Text(info,"Connaissance du joueur : "+knowledge+" %","muted");var report=Career.ReportFor(p.id);Text(info,p.team=="retired"?"La fiche conserve les informations connues avant la retraite. Le joueur ne peut plus être observé ni recruté ; les attributs inconnus restent masqués.":report?.advice??(knowledge>=90?"Les étoiles comparent ce joueur à votre onze actuel. Elles peuvent évoluer avec votre effectif. Le potentiel reste une estimation de développement.":"Envoyez un recruteur pour préciser le niveau et les attributs. Les valeurs masquées ne sont pas révélées par cette fiche."));
            if(report!=null){Text(info,(report.scout??"Recruteur")+" · jugement "+(report.judging>0?report.judging.ToString():"non renseigné")+" / 20 · "+(report.confidence<90?"rapport prévu le ":"observation du ")+Core.Career.Epoch.AddDays(report.confidence<90?report.due:report.lastObserved>0?report.lastObserved:report.due).ToString("dd MMM yyyy",French),"footnote");if(p.team!="retired"&&knowledge<report.confidence)Text(info,"Observation ancienne : actualisez le rapport pour réduire l’incertitude.","notice");}
            if(!string.IsNullOrEmpty(p.assessment))Text(body,p.assessment,"footnote");
            var physique=Card(body);physique.name="profile-physique";Text(physique,"Gabarit","section-title");ProfileFact(physique,"Taille",p.heightCm>0?(p.heightCm/100f).ToString("0.00",French)+" m":"Non renseignée");ProfileFact(physique,"Poids",p.weightKg>0?p.weightKg+" kg":"Non renseigné");if(!string.IsNullOrEmpty(p.physiqueSource))Text(physique,"Données annoncées par ESPN · valeurs arrondies à partir du relevé importé.","footnote");
        }
        static void ProfileMetric(VisualElement parent,string title,string value){var card=Card(parent,"profile-metric");Text(card,title,"eyebrow");Text(card,value,"profile-metric-value");}
        static void ProfileGauge(VisualElement parent,string label,float value){var row=Row(parent,"profile-gauge");Text(row,label);Text(row,Mathf.RoundToInt(value)+" %","profile-gauge-value");var bar=new ProgressBar{lowValue=0,highValue=100,value=value};bar.style.width=Length.Percent(100);row.Add(bar);}
        void ProfileContract(VisualElement body,PlayerData p)
        {
            var retirement=Career.world?.contracts?.FirstOrDefault(x=>x.player==p.id&&x.retirement>=0);
            if(p.team!="retired"&&retirement!=null){ProfileFact(body,"Retraite annoncée",Core.Career.Epoch.AddDays(retirement.retirement).ToString("dd MMMM yyyy",French));Text(body,"La carrière du joueur prendra fin à cette date. Une arrivée par précontrat prévue après sa retraite sera annulée ; les frais déjà payés ne sont pas remboursés.","notice");}
            if(p.team=="retired"){
                ProfileFact(body,"Statut","Retraité · aucun salaire actuellement dû");ProfileFact(body,"Dernier salaire contractuel mensuel",Money(Core.Career.MonthlySalary(p.wage)));
                Text(body,"Source : retraite effective dans votre carrière. Le montant conservé est historique ; les clauses de prêt et d’achat ne sont plus exécutables. Les frais déjà payés ne sont pas remboursés.","footnote");
                var archived=Career.life?.retiredPlayers?.FirstOrDefault(x=>x.id==p.id)??Career.previousClubs?.Where(x=>x.life?.retiredPlayers!=null).SelectMany(x=>x.life.retiredPlayers).FirstOrDefault(x=>x.id==p.id);
                if(archived!=null)ProfileFact(body,"Apparitions conservées dans un club dirigé",archived.appearances.ToString());
                else Text(body,"Les apparitions individuelles de ce joueur n’ont pas été enregistrées dans un club que vous dirigiez.","muted");
                return;
            }
            if(p.team=="free"){
                ProfileFact(body,"Statut","Libre · aucun employeur");ProfileFact(body,"Indemnité de transfert","Aucune");ProfileFact(body,"Prétentions mensuelles estimées",Money(Core.Career.MonthlySalary(p.wage)));ProfileFact(body,"Valeur économique estimée",Money(p.value));
                Text(body,"La valeur économique n’est pas une indemnité : négociez salaire, prime à la signature, commission d’agent et rôle dans l’équipe.","notice");
                Text(body,p.salarySource??"Prétentions estimées dans la simulation, à confirmer avec l’agent.","footnote");return;
            }
            var contract=Career.world==null?null:Career.Contract(Database,p.id);
            if(contract!=null&&!string.IsNullOrWhiteSpace(contract.conditionsSource))Text(body,contract.conditionsSource,contract.parentConditionsUnavailable?"notice":"footnote");
            if(contract?.IsLoan==true){int parentEnd=contract.parentConditions?.until??contract.parentUntil;if(parentEnd>0)ProfileFact(body,"Échéance du contrat parent",Core.Career.Epoch.AddDays(parentEnd).ToString("dd MMMM yyyy",French));if(contract.parentConditions==null)Text(body,"Ancienne partie : les conditions personnelles du contrat parent n’ont pas été enregistrées.","notice");if(contract.purchaseConditions!=null&&(contract.terms?.optionFee>0||contract.terms?.obligationFee>0))Text(body,contract.purchaseConditions.source+" · échéance prévue : "+Core.Career.Epoch.AddDays(contract.purchaseConditions.until).ToString("dd MMMM yyyy",French),contract.purchaseConditions.estimated?"notice":"footnote");}

            ProfileFact(body,"Club",ClubName(p.team));ProfileFact(body,"Salaire mensuel",Money(Core.Career.MonthlySalary(contract?.wage??p.wage)));ProfileFact(body,"Valeur de transfert estimée",Money(p.value));
            if(contract!=null){ProfileFact(body,"Fin du contrat",Core.Career.Epoch.AddDays(contract.until).ToString("dd MMMM yyyy",French));ProfileFact(body,"Temps de jeu promis",PlayingTimeRoles.Label(contract.role));Text(body,PlayingTimeRoles.Description(contract.role),"muted").name="profile-playing-time-description";ProfileFact(body,"Prime de présence",Money(contract.appearanceBonus));ProfileFact(body,"Clause libératoire",contract.releaseClause>0?Money(contract.releaseClause):"Aucune");if(contract.IsLoan){ProfileFact(body,"Prêté par",ClubName(contract.parent));ProfileFact(body,"Salaire pris en charge",(contract.terms?.loanWagePercent??50)+" %");ProfileFact(body,"Fin du prêt",Core.Career.Epoch.AddDays(contract.loanUntil).ToString("dd MMMM yyyy",French));ProfileFact(body,"Option d’achat",Money(contract.terms?.optionFee??0));ProfileFact(body,"Obligation d’achat",Money(contract.terms?.obligationFee??0));}}
            if(p.team==Career.club&&contract!=null)Text(body,Career.PlayingTimeProgress(p.id),"notice").name="profile-playing-time-progress";
            if(contract?.parent==Career.club){var path=Career.world.youth.FirstOrDefault(y=>y.player==p.id);ProfileFact(body,"Apparitions de prêt simulées",Math.Max(0,(path?.loanAppearances??0)-contract.appearancesAtSigning).ToString());Text(body,!contract.loanAppearanceTracking||contract.loanAppearanceHistoryEstimated?"Ancienne partie : les apparitions antérieures ne sont pas vérifiées. La clause compte uniquement le nouveau suivi depuis sa reprise.":"Apparitions estimées lors des rencontres réellement jouées du club emprunteur ; sa composition n’est pas simulée en 3D.","footnote");}
            Text(body,contract?.estimated!=false?"Montants et échéance issus de la simulation : ce ne sont pas des données contractuelles privées vérifiées.":"Conditions contractuelles de votre carrière.","footnote");
            var announced=Career.world==null?null:Career.AnnouncedFreeAgentRelease(Database,p.id);if(announced!=null){Text(body,"Libération annoncée · "+(string.IsNullOrEmpty(announced.contractEndsOn)?"échéance contractuelle individuelle non vérifiée, disponibilité appliquée à la borne estivale de simulation":"contrat annoncé jusqu’au "+DateTime.Parse(announced.contractEndsOn).ToString("dd MMM yyyy",French)),"notice");Text(body,"Source : "+announced.sourceUrl+" · publication "+announced.announcedAt,"footnote");if(Career.CanPrecontract(Database,p.id))PlayerManagementButton(body,"Discuter d’un précontrat",()=>TransferDialog(p.id)).AddToClassList("primary");}
            if(p.team==Career.club&&Career.world!=null&&!Career.HasActiveLoan(p.id)){var row=Row(body);PlayerManagementButton(row,"Proposer un prêt",()=>YouthLoanDialog(p.id));PlayerManagementButton(row,"Proposer à la vente",()=>RunDecision(()=>Career.ListForSale(Database,p.id)));}
        }
        static void ProfileFact(VisualElement parent,string label,string value){var row=Row(parent,"profile-fact");Text(row,label,"profile-fact-label");Text(row,value,"profile-fact-value");}
        void ProfileRelations(VisualElement body,PlayerData p)
        {
            if(p.team!=Career.club){Text(body,"Vous ne dirigez pas ce joueur. Discutez avec son agent depuis Négocier pour connaître ses attentes.","notice");return;}
            var person=Career.Person(p.id);Text(body,Career.PlayerSituation(Database,p.id),"notice");if(person.promiseUntil>=0)Text(body,Career.PlayerPromiseStatus(p.id),Career.PlayerPromiseFulfilled(p.id)?"positive":"status-warning");ProfileGauge(body,"Confiance envers vous",person.trust);ProfileGauge(body,"Moral",person.morale);PlayerManagementButton(body,"SMS / Appeler",()=>Conversation(p.id));
            if(person.discussionFocus=="development")PlayerManagementButton(body,"Parcours et travail",()=>DevelopmentConversationPlan(p.id));
            Text(body,"Échanges récents","section-title");var messages=Career.life.messages.Where(m=>m.player==p.id).Reverse().Take(5).ToArray();foreach(var message in messages){var card=Card(body);Text(card,message.sender+" · "+Core.Career.Epoch.AddDays(message.day).ToString("dd MMM",French),"eyebrow");Text(card,message.subject);Button(card,"Lire le message",()=>OpenMessage(message.id));}if(messages.Length==0)Text(body,"Aucun échange enregistré pour le moment.","muted");
        }
        void ProfileAttributes(VisualElement body,PlayerData p,int knowledge)
        {
            bool exact=Career.revealAttributes||p.team==Career.club;Text(body,knowledge<40?"Attributs masqués · observation nécessaire":exact?"Attributs sur 20 · évaluation de simulation":"Rapport du recruteur · fourchettes estimées sur 20","muted");var columns=Row(body,"attribute-columns");
            var groups=new[]{("Technique",new[]{"ballControl|Contrôle","dribbling|Dribble","shortPassing|Passes courtes","longPassing|Passes longues","finishing|Finition","shotPower|Puissance de frappe","longShots|Tirs de loin","crossing|Centres","headingAccuracy|Jeu de tête","standingTackle|Tacle debout","slidingTackle|Tacle glissé","freeKickAccuracy|Coups francs","fkAccuracy|Coups francs","penalties|Penalty","curve|Effet","volleys|Reprises"}),
                ("Mental",new[]{"vision|Vision du jeu","composure|Sang-froid","reactions|Réactivité","positioning|Placement offensif","defensiveAwareness|Placement défensif","interceptions|Anticipation des passes","aggression|Agressivité"}),
                ("Physique",new[]{"acceleration|Accélération","sprintSpeed|Vitesse","agility|Agilité","balance|Équilibre","stamina|Endurance","strength|Puissance","jumping|Détente"}),
                ("Gardien",new[]{"gkDiving|Plongeon","gkHandling|Prise de balle","gkKicking|Relance au pied","gkPositioning|Placement","gkReflexes|Réflexes"})};
            if(p.attributes==null||p.attributes.Length==0){Text(body,"Les attributs détaillés de ce joueur ne sont pas encore disponibles.","notice");return;}
            foreach(var group in groups.OrderBy(g=>p.Goalkeeper&&g.Item1=="Gardien"?0:1)){if(group.Item1=="Gardien"&&!p.Goalkeeper)continue;var card=Card(columns,"attribute-group");Text(card,group.Item1,"section-title");foreach(var spec in group.Item2){var parts=spec.Split('|');if(!(p.attributes?.Any(a=>a.key==parts[0])??false))continue;var row=Row(card,"profile-attribute");Text(row,parts[1],"attribute-name");var assessment=Career.AssessedAttribute(Database,p.id,parts[0]);var text=Text(row,assessment.ToString(),"attribute-score");if(exact&&assessment.known)text.AddToClassList(assessment.low>=15?"attribute-strong":assessment.low>=10?"attribute-average":"attribute-low");}}
            if(!exact&&knowledge>=40)Text(body,"L’incertitude dépend du jugement du recruteur, de la durée et de l’ancienneté de l’observation. Une observation complète ne rend pas le potentiel certain.","footnote");
            Text(body,"Regroupements adaptés aux attributs disponibles dans la base ; les notes ne sont pas celles de Football Manager.","footnote");
        }
    }
}

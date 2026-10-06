using System;
using System.Linq;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;
using Touchline.Core;

namespace Touchline
{
    public sealed partial class TouchlineApp
    {
        static readonly CultureInfo French=CultureInfo.GetCultureInfo("fr-FR");
        bool compact;string inboxFilter="Tous";
        static string Money(long value)=>Math.Abs(value)>=1000000?(value/1000000f).ToString("0.00",French)+" M€":value.ToString("N0",French)+" €";
        static Label Text(VisualElement parent,string text,string cls="body-text"){var l=new Label(text);l.AddToClassList(cls);parent.Add(l);return l;}
        static VisualElement Card(VisualElement parent,string cls=""){var c=new VisualElement();c.AddToClassList("card");if(cls!="")c.AddToClassList(cls);parent.Add(c);return c;}
        void BuildShell()
        {
            root.Clear();modal=null;root.EnableInClassList("reduce-motion",!MotionEnabled);root.EnableInClassList("live-match",page=="Match"&&Career.match!=null);root.EnableInClassList("manager-shell",page!="Match"||Career.match==null);root.EnableInClassList("compact",compact);root.EnableInClassList("short-wide",root.resolvedStyle.width>=1000&&root.resolvedStyle.height<680);
            var header=Row(root,"header");var brand=Row(header,"brand-group");NavigationButtons(brand);var crest=Resources.Load<Texture2D>("Logos/club-"+Career.club);if(crest!=null){var logo=new Image{image=crest,scaleMode=ScaleMode.ScaleToFit};logo.AddToClassList("nav-crest");brand.Add(logo);}var clubTitle=new VisualElement();brand.Add(clubTitle);Text(clubTitle,"TOUCHLINE / "+PageLabel(page).ToUpper(French),"eyebrow");Text(clubTitle,Own.name,"club-title");
            var today=Row(header,"header-actions");Text(today,Career.Date.ToString("ddd dd MMM yyyy",French),"date-label");var go=Button(today,"Continuer  →",ContinueDay);go.AddToClassList("primary");go.name="manager-continue";go.tooltip="Avancer d’un jour. Une rencontre à disputer doit être terminée avant de poursuivre.";
            var shell=new VisualElement();shell.AddToClassList("workspace");root.Add(shell);var nav=new VisualElement();nav.AddToClassList("navigation");shell.Add(nav);
            if(!compact)Text(nav,"ESPACE MANAGER","nav-caption");
            string[] items=compact?new[]{"Club","Effectif","Tactique","Messages","Plus"}:new[]{"Club","Effectif","Tactique","Match","Messages","Santé","Infrastructures","Coulisses"};
            if(!compact&&Career.world!=null)items=new[]{"Club","Effectif","Tactique","Match","Calendrier","Recrutement","Formation","Finances","Messages","Plus"};
            foreach(var item in items){if(!compact&&Career.world!=null&&(item=="Effectif"||item=="Recrutement"))Text(nav,item=="Effectif"?"SPORTIF":"DÉVELOPPEMENT","nav-group");string label=PageLabel(item);var b=Button(nav,label,()=>{if(item=="Plus")MoreMenu();else Navigate(item);});b.name="manager-nav-"+item;b.tooltip=item=="Plus"?"Tous les menus et recherche":DirectoryHint(item);if(item==page||item=="Plus"&&!items.Contains(page))b.AddToClassList("active");if(item=="Messages"&&Career.life.messages.Any(m=>!m.read)){b.AddToClassList("unread-nav");int unread=Career.life.messages.Count(m=>!m.read);b.tooltip=unread+" message(s) non lu(s)";if(!compact){var badge=Text(b,unread>99?"99+":unread.ToString(),"manager-nav-badge");badge.pickingMode=PickingMode.Ignore;}}}
            if(!compact){var spacer=new VisualElement();spacer.style.flexGrow=1;nav.Add(spacer);Text(nav,"CARRIÈRE "+(Career.world?.year??2026),"nav-caption");Button(nav,"Changer de club",()=>Navigate("Changer de club"));Text(nav,"Hors connexion","nav-caption");}
            content=new VisualElement();content.AddToClassList("content");shell.Add(content);
        }
        void MoreMenu()=>ClubDirectory();
        void RunDecision(Action action){try{action();Career.ApplyLife(Database);Save();if(!delegatingMatch)Build();}catch(Exception e){Message(e.Message);}}
        void CloseFinishedMatch(){if(Career.match?.finished!=true)return;Career.RecordMatch(Database);Career.lineup=Career.match.actors.Where(p=>p.side==0).OrderBy(p=>p.slot).Select(p=>p.id).ToArray();Career.match=null;if(arena!=null)Destroy(arena.gameObject);arena=null;}
        void ContinueDay()
        {
            if(Time.unscaledTime<continueAllowedAt)return;continueAllowedAt=Time.unscaledTime+.4f;
            try{CloseFinishedMatch();int before=Career.life.serial;Career.AdvanceDay(Database);newMessageIds=Career.life.messages.Where(m=>m.id>before).Select(m=>m.id).ToArray();page="Club";Save();Build();var go=root.Q<Button>("manager-continue");if(go!=null){go.SetEnabled(false);go.schedule.Execute(()=>go.SetEnabled(true)).StartingIn(400);}}
            catch(Exception e){Message(e.Message);if(Career.life.day>=Career.life.nextFixture)Button(modal.Q(className:"modal-panel"),"Préparer le match",()=>Navigate("Match"));}
        }
        void StartCareerMatch(bool delegated)
        {
            if(Career.life.nextFixture==int.MaxValue)throw new InvalidOperationException("Aucune rencontre programmée. Continuez la préparation depuis Calendrier.");
            if(Career.life.day<Career.life.nextFixture)throw new InvalidOperationException("Le prochain match est prévu le "+Touchline.Core.Career.Epoch.AddDays(Career.life.nextFixture).ToString("dd MMMM",French)+". Utilisez Continuer pour avancer jour par jour.");
            if(Career.life.managerBanUntil>Career.life.day&&!delegated)throw new InvalidOperationException("Votre suspension impose de déléguer cette rencontre à l’adjoint.");
            CloseFinishedMatch();if(Career.world!=null){if(Career.world.managerStatus!="employed")throw new InvalidOperationException("Vous devez retrouver un poste.");var fixture=Career.NextFixture();if(fixture==null||fixture.day>Career.life.day)throw new InvalidOperationException("Aucun match prévu aujourd’hui.");opponent=fixture.home==Career.club?fixture.away:fixture.home;Career.world.activeFixture=fixture.id;}
            Career.PrepareLineup(Database);var simulation=MatchSimulation.Create(Database,Career,opponent,VisualValidation?731:unchecked((uint)DateTime.UtcNow.Ticks),2700);Career.match=simulation.State;Career.ApplyMatchContext(simulation);
            if(delegated){page="Match";CreateArena(simulation);Save();Build();DelegateMatch(simulation);return;}
            else{CreateArena(simulation);page="Match";if(Career.world!=null){arena.Paused=true;presentationPending=true;}}Save();Build();
        }
        void Dashboard()=>ManagerDashboard();
        static void Metric(VisualElement parent,string label,string value,string detail){var c=Card(parent,"metric-card");Text(c,label,"eyebrow");Text(c,value,"metric-value");Text(c,detail,"muted");}
        void Messages()=>InboxWorkspace();
        void OpenMessage(int id)=>OpenMessageThread(id);
        void ContactPicker()=>InboxContactPicker();
        void Conversation(string id)=>PlayerConversationWorkspace(id);
        void Medical()
        {
            var s=Scroll(content);Text(s,"PRENDRE SOIN DU GROUPE","eyebrow");Heading(s,"Centre médical");var active=Career.life.medical.Where(c=>c.closed<0&&string.IsNullOrEmpty(c.responsibilityEndedReason)&&Career.PlayerMedicalResponsibility(c.player)&&Database.Find(c.player)?.team==Career.club).ToArray();Text(s,active.Length+" dossier(s) actif(s) · Pôle médical niveau "+Career.Level("medical"),"muted");
            foreach(var c in active){var card=Card(s);var row=Row(card);Text(row,Database.Find(c.player).name,"section-title");Text(row,c.remaining+" j estimés","status-warning");Text(card,c.diagnosis);Text(card,c.treatment=="pending"?"Le staff attend votre décision":"Protocole : "+Treatment(c.treatment),"muted");Button(card,"Consulter / décider",()=>MedicalDetail(c.player)).AddToClassList("primary");}
            if(active.Length==0)Text(Card(s),"Aucune blessure en cours. Le staff vous écrira dès qu’un joueur a besoin d’un suivi.","empty-state");
            var external=Career.life.medical.Where(c=>c.closed<0&&(!string.IsNullOrEmpty(c.responsibilityEndedReason)||!Career.PlayerMedicalResponsibility(c.player)||Database.Find(c.player)?.team!=Career.club)).Reverse().ToArray();
            if(external.Length>0){Heading(s,"Dossiers conservés hors effectif");foreach(var c in external){var card=Card(s);Text(card,Database.Find(c.player)?.name??"Joueur non disponible","section-title");Text(card,c.diagnosis);Text(card,"Dossier historique : votre staff n’actualise plus ce suivi. Aucune guérison ni date de reprise n’est déduite.","muted");Button(card,"Consulter l’historique",()=>MedicalDetail(c.player));}}
            Heading(s,"Retours récents");foreach(var c in Career.life.medical.Where(c=>c.closed>=0).Reverse().Take(8)){var r=Row(s,"list-row");Text(r,Database.Find(c.player).name+" · "+c.diagnosis);Text(r,"Reprise le "+Touchline.Core.Career.Epoch.AddDays(c.closed).ToString("dd MMM",French),"muted");}
        }
        static string Treatment(string t)=>t=="surgery"?"Intervention et rééducation":t=="injection"?"Antalgie temporaire":t=="conservative"?"Soins conservateurs":"À décider";
        void MedicalDetail(string id)
        {
            var player=Database.Find(id);bool responsible=Career.PlayerMedicalResponsibility(id)&&player?.team==Career.club;
            var c=responsible?Career.Injury(id):Career.life.medical.LastOrDefault(m=>m.player==id);var panel=Modal("Dossier · "+(player?.name??"Joueur non disponible"));panel.name="medical-player-detail";
            if(c==null&&responsible){var previous=Career.life.medical.LastOrDefault(m=>m.player==id&&!string.IsNullOrEmpty(m.responsibilityEndedReason));if(previous!=null){c=previous;responsible=false;}}
            if(!responsible){var history=Scroll(panel);Text(history,"Dossier historique hors effectif","section-title");Text(history,"Votre staff n’actualise plus ce suivi. Aucune guérison ni date de reprise n’est déduite.","notice");if(c!=null){if(!string.IsNullOrEmpty(c.responsibilityEndedReason))Text(history,"Fin de responsabilité constatée le "+Touchline.Core.Career.Epoch.AddDays(c.responsibilityEndedDay).ToString("dd MMM yyyy",French)+" · "+(c.responsibilityEndedReason=="retirement"?"retraite effective":"sortie du suivi club"),"muted");Text(history,c.diagnosis);Text(history,"Dernière estimation conservée : "+c.remaining+" jours, non actualisée par votre club.","muted");Text(history,"Protocole enregistré : "+Treatment(c.treatment),"muted");}else Text(history,"Aucun dossier médical n’a été conservé pour ce joueur.");return;}
            if(c==null){Text(panel,"Le joueur est disponible. Le retour peut nécessiter une reprise progressive.");return;}
            var s=Scroll(panel);Text(s,c.diagnosis,"section-title");Text(s,c.remaining+" jours d’absence estimés","medical-duration");Text(s,"Protocole : "+Treatment(c.treatment)+". Réévaluation possible selon l’évolution.");Text(s,c.consent?"Le joueur accepte les options validées par le staff.":"Le joueur ne consent pas à une intervention. Poursuite des soins conservateurs.","muted");
            if(c.treatment=="pending")PlayerManagementButton(s,"Soins conservateurs",()=>RunDecision(()=>Career.Treat(Database,c.id,"conservative"))).AddToClassList("primary");
            if(c.surgery&&c.treatment!="surgery"&&Career.life.day-c.opened<=10){Text(s,"Avis chirurgical : 55–80 jours de rééducation dans cette simulation. Le retour peut être plus tardif qu’avec les soins déjà commencés. Coût : "+Money(Math.Max(2500,Career.life.revenue/15000)),"notice");PlayerManagementButton(s,"Retenir l’intervention",()=>Confirm("Intervention proposée", "Engager l’intervention avec l’accord du joueur et le suivi du staff ?",()=>RunDecision(()=>Career.Treat(Database,c.id,"surgery")))).SetEnabled(c.consent&&PlayerManagementAvailable);}
            if(c.injection&&c.reliefUntil<0){Text(s,"Option infiltration / antalgie : envisageable uniquement après accord médical, réglementaire et du joueur. Disponibilité temporaire jusqu’au lendemain ; aucune guérison accélérée. Jouer expose à une aggravation (28 % dans le modèle).", "notice");PlayerManagementButton(s,"Demander la prise en charge antalgique",()=>Confirm("Disponibilité temporaire", "Cette décision ne guérit pas la blessure. Confirmer l’option validée par le staff ?",()=>RunDecision(()=>Career.Treat(Database,c.id,"injection")))).SetEnabled(c.consent&&c.remaining<=7&&PlayerManagementAvailable);}
            if(!c.injection)Text(s,"L’infiltration n’est pas proposée pour cette lésion.","muted");Text(s,"Les diagnostics, durées et risques sont des paramètres de simulation, pas des recommandations médicales.","footnote");
        }
        void Facilities()
        {
            var s=Scroll(content);Text(s,"BÂTIR POUR DURER","eyebrow");Heading(s,"Infrastructures");Text(s,"Trésorerie "+Money(Career.life.cash)+" · Deux projets simultanés maximum","muted");var grid=Row(s,"facility-grid");
            foreach(var f in Career.life.facilities){var card=Card(grid,"facility-card");Text(card,f.kind=="training"?"01 / PERFORMANCE":f.kind=="medical"?"02 / SANTÉ":f.kind=="academy"?"03 / AVENIR":"04 / ACCUEIL","eyebrow");Text(card,Core.Career.FacilityName(f.kind),"section-title");Text(card,"NIVEAU "+f.level+" / 5","facility-level");var progress=new ProgressBar{lowValue=0,highValue=5,value=f.level};card.Add(progress);
                Text(card,f.kind=="training"?"Accélère la progression des jeunes joueurs.":f.kind=="medical"?"Améliore la récupération et réduit le risque à l’entraînement.":f.kind=="academy"?"Renforce le développement des joueurs de moins de 24 ans.":"Augmente les recettes d’accueil des rencontres.","muted");
                var project=Career.life.projects.LastOrDefault(p=>p.kind==f.kind&&new[]{"requested","approved","building"}.Contains(p.status));
                if(project!=null){Text(card,project.status=="requested"?"Demande au propriétaire • réponse le "+Touchline.Core.Career.Epoch.AddDays(project.due).ToString("dd MMM",French):project.status=="approved"?"Accord : le propriétaire couvre "+Money(project.contribution):"Travaux en cours • livraison le "+Touchline.Core.Career.Epoch.AddDays(project.due).ToString("dd MMM",French),"status-warning");if(project.status=="approved")Button(card,"Engager ma part · "+Money(project.cost-project.contribution),()=>RunDecision(()=>Career.StartApprovedProject(f.kind))).AddToClassList("primary");}
                else if(f.level<5){Text(card,"Prochain niveau : "+Money(Career.ProjectCost(f.kind)),"project-price");Text(card,(30*(f.level+1)+(f.kind=="stadium"?60:0))+" jours de travaux","muted");Button(card,"Demander au propriétaire",()=>RunDecision(()=>Career.RequestProject(f.kind,true)));Button(card,"Financer avec le club",()=>Confirm("Investissement du club",Core.Career.FacilityName(f.kind)+" : engager "+Money(Career.ProjectCost(f.kind))+" ?",()=>RunDecision(()=>Career.RequestProject(f.kind,false))));}
            }
            Heading(s,"Journal financier");Text(s,"Revenus de référence : "+Money(Career.life.revenue)+" / an · "+Career.life.financeSource+". La trésorerie et les coûts des projets sont des paramètres de carrière estimés.","footnote");foreach(var entry in Career.life.ledger.AsEnumerable().Reverse().Take(16)){var row=Row(s,"list-row");Text(row,Touchline.Core.Career.Epoch.AddDays(entry.day).ToString("dd MMM",French)+" · "+entry.label);Text(row,Money(entry.amount),entry.amount<0?"negative":"positive");}
        }
        void SuspendedPanel(){Heading(content,"Décisions déléguées");Text(content,"Votre suspension court jusqu’au "+Touchline.Core.Career.Epoch.AddDays(Career.life.managerBanUntil).ToString("dd MMM",French)+". Continuer reste disponible ; l’adjoint dispute les rencontres.");}
        void Integrity()
        {
            var s=Scroll(content);Text(s,"CHOIX, PRESSION ET INTÉGRITÉ","eyebrow");Heading(s,"Zone grise");Text(s,"Intrigues fictives : pression d’intermédiaires, alertes internes et responsabilité du manager. Vos décisions ont un coût, modifient la confiance et peuvent conduire à une enquête.","notice");
            var governance=Card(s,"scout-summary");Text(governance,"Gouvernance et prévention","section-title");Text(governance,"Un contrôle indépendant peut réduire progressivement la suspicion et restaurer la confiance. Il ne ferme aucune enquête ni n’annule les sanctions.","muted");Button(governance,"Financer un contrôle indépendant · "+Money(Career.IntegrityReviewCost),()=>RunDecision(()=>Career.ReviewIntegrity())).SetEnabled(Career.life.day>=Career.integrityReviewUntil);
            var toggle=new Toggle("Activer les scénarios de pratiques interdites"){value=Career.life.corruptionEnabled};s.Add(toggle);toggle.RegisterValueChangedCallback(e=>RunDecision(()=>Career.life.corruptionEnabled=e.newValue));Text(s,"Désactiver ferme les nouvelles propositions ; les enquêtes déjà ouvertes continuent.","muted");
            var stats=Row(s,"metric-grid");Metric(stats,"SUSPICION",Career.life.suspicion.ToString("0")+" %","Risque cumulé");Metric(stats,"RÉPUTATION",Career.life.reputation.ToString("0")+" / 100","Image du club");
            if(Career.life.corruptionEnabled){var card=Card(s);Text(card,"Dopage","section-title");Text(card,"Un dossier individuel abstrait : bénéfice physique faible et temporaire, refus possible, confiance dégradée. Une enquête peut entraîner une amende, un problème de santé et une suspension de 90 jours. Aucun produit ou protocole réel n’est détaillé.");var people=Database.Squad(Career.club).Where(p=>Career.Available(p.id)).ToList();if(people.Count>0){var picker=new DropdownField("Joueur",people.Select(p=>p.name).ToList(),0);card.Add(picker);Button(card,"Ouvrir le dossier · "+Money(Math.Max(5000,Career.life.revenue/2000)),()=>Confirm("Scénario de dopage", "Coût immédiat, résultat incertain et enquête possible. Confirmer ce choix de jeu ?",()=>RunDecision(()=>Career.StartScheme("doping",people[picker.index].id))));}
                var fixing=Card(s);Text(fixing,"Achat de match","section-title");Text(fixing,"Un intermédiaire fictif propose une influence limitée sur une seule rencontre. Refus fréquent ; aucune victoire assurée. Les fonds sont perdus même en cas de refus, et le risque disciplinaire demeure.");Button(fixing,"Ouvrir le dossier · "+Money(Math.Max(5000,Career.life.revenue/250)),()=>Confirm("Scénario d’achat de match","Engager ces fonds et accepter les conséquences possibles ?",()=>RunDecision(()=>Career.StartScheme("fixing"))));Text(s,"Un dossier tous les 30 jours au maximum, sans cumul. La probabilité d’une sanction augmente avec la suspicion. Les sanctions sont des règles de jeu simplifiées.","footnote");}
            ExtendedIntegrityCards(s);Heading(s,"Dossiers et enquêtes");foreach(var c in Career.life.investigations.AsEnumerable().Reverse()){var card=Card(s);Text(card,Core.Career.SchemeName(c.kind),"section-title");Text(card,c.status=="pending"?"Enquête possible / dossier en cours":c.status=="sanctioned"?"Sanction prononcée":"Classé sans sanction");Text(card,"Ouvert le "+Touchline.Core.Career.Epoch.AddDays(c.opened).ToString("dd MMM",French),"muted");if(c.status=="pending"&&c.alerted&&string.IsNullOrEmpty(c.response)){Text(card,"Une alerte interne attend votre réponse.","notice");Button(card,"Protéger le témoin · "+Money(Math.Max(1000,Career.life.revenue/10000)),()=>RunDecision(()=>Career.RespondIntegrity(c.opened,"protect")));Button(card,"Reconnaître les faits",()=>RunDecision(()=>Career.RespondIntegrity(c.opened,"cooperate")));Button(card,"Ne pas intervenir",()=>RunDecision(()=>Career.RespondIntegrity(c.opened,"ignore")));}}
        }
    }
}

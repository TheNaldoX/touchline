using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using Touchline.Core;

namespace Touchline
{
    public sealed partial class TouchlineApp
    {
        void ManagerDashboard()
        {
            var s=Scroll(content);s.name="manager-dashboard";s.AddToClassList("manager-dashboard");
            var life=Career.life;var fixture=Career.NextFixture();
            bool employed=Career.world==null||Career.world.managerStatus=="employed";
            var title=Row(s,"manager-dashboard-title");
            var identity=new VisualElement();identity.AddToClassList("manager-title-copy");title.Add(identity);
            Text(identity,"BUREAU / "+Career.Date.ToString("dddd dd MMMM",French).ToUpper(French),"eyebrow");
            Text(identity,employed?"Votre journée au club":"Votre prochaine étape","manager-title");
            Text(identity,Career.manager+" · Saison "+(Career.world?.year??2026)+" / "+((Career.world?.year??2026)+1),"muted");
            Button(title,"Tous les menus",MoreMenu).name="manager-directory";

            var top=Row(s,"manager-top-grid");
            ManagerFixtureCard(top,fixture,employed);
            ManagerDecisionCard(top,employed);
            if(!employed){ManagerInbox(s);return;}

            var metrics=Row(s,"manager-metrics");
            ManagerMetric(metrics,"TRÉSORERIE",Money(life.cash),"Finances et budgets","Finances",life.cash<0);
            ManagerMetric(metrics,"DIRECTION",life.boardTrust.ToString("0")+" %","Confiance du propriétaire","Carrière",life.boardTrust<40);
            int available=life.players.Count(p=>Career.Available(p.id));
            ManagerMetric(metrics,"DISPONIBLES",available+" / "+life.players.Count,"Consulter l’effectif","Effectif",available<18);
            float morale=life.players.Count==0?0:(float)life.players.Average(p=>p.morale);
            ManagerMetric(metrics,"VESTIAIRE",morale.ToString("0")+" %","Moral moyen · contacts","Messages",morale<50);

            var work=Row(s,"manager-work-grid");var training=Card(work,"manager-work-card");
            Text(training,"PERFORMANCE / PROCHAINE JOURNÉE","eyebrow");Text(training,"La charge du groupe","section-title");
            bool delegationRequested=life.staff?.training==true;
            bool delegated=delegationRequested&&Career.Staff("fitness").wage>0;
            Text(training,delegated?"Entraînement délégué au préparateur":delegationRequested?"Délégation inactive · poste de préparateur vacant":"Vous définissez la charge d’entraînement","manager-context");
            float fitness=life.players.Count==0?100:(float)life.players.Average(p=>p.fitness);
            Text(training,"Condition moyenne : "+fitness.ToString("0")+" % · "+life.players.Count(p=>p.fitness<75)+" joueur(s) sous 75 %","muted");
            var choices=new System.Collections.Generic.List<string>{"Récupération","Équilibré","Intensif"};
            var plan=new DropdownField("Charge",choices,life.training=="rest"?0:life.training=="heavy"?2:1){name="manager-training-load"};training.Add(plan);
            plan.SetEnabled(employed&&!delegated&&!(Career.match!=null&&!Career.match.finished)&&life.managerBanUntil<=life.day);
            plan.RegisterValueChangedCallback(_=>RunDecision(()=>{if(Career.match!=null&&!Career.match.finished)throw new InvalidOperationException("Terminez la rencontre.");if(life.managerBanUntil>life.day)throw new InvalidOperationException("L’adjoint gère la charge pendant votre suspension.");life.training=plan.index==0?"rest":plan.index==2?"heavy":"balanced";}));
            Text(training,delegated?"Le staff ajuste la charge selon la condition et le prochain match. Reprenez cette responsabilité dans la délégation.":delegationRequested?"Aucun préparateur ne peut appliquer cette délégation. Vous pouvez régler la charge ici en attendant de recruter un préparateur.":"Récupération : retrouver de la fraîcheur. Intensif : davantage de travail, avec un risque de blessure accru.","muted");
            var trainingActions=Row(training,"manager-card-actions");Button(trainingActions,"Staff et délégation",()=>Navigate("Staff et délégation"));Button(trainingActions,"Santé",()=>Navigate("Santé"));

            var staff=Card(work,"manager-work-card");Text(staff,"DANS LE BUREAU DE L’ADJOINT","eyebrow");Text(staff,"Le point sportif","section-title");
            Text(staff,Career.StaffAdvice(Database),"manager-advice");
            var staffActions=Row(staff,"manager-card-actions");Button(staffActions,"Tactique",()=>Navigate("Tactique"));Button(staffActions,"Formation",()=>Navigate("Formation"));Button(staffActions,"Recrutement",()=>Navigate("Recrutement"));

            ManagerInbox(s);
        }

        void ManagerInbox(VisualElement parent)
        {
            var life=Career.life;var inbox=Card(parent,"manager-inbox");var heading=Row(inbox);Text(heading,"DERNIERS MESSAGES","eyebrow");
            int unread=life.messages.Count(m=>!m.read);Button(heading,"Messagerie · "+unread+" non lu(s)",()=>Navigate("Messages")).name="manager-inbox-open";
            foreach(var msg in life.messages.AsEnumerable().Reverse().Take(3)){
                var button=Button(inbox,"",()=>OpenMessage(msg.id));button.name="manager-message-"+msg.id;button.AddToClassList("manager-message");
                var meta=Text(button,Touchline.Core.Career.Epoch.AddDays(msg.day).ToString("dd MMM",French)+" · "+msg.sender+(msg.read?"":" · NON LU"),"manager-message-meta");meta.name="manager-message-meta-"+msg.id;meta.pickingMode=PickingMode.Ignore;
                Text(button,msg.subject,"manager-message-title").pickingMode=PickingMode.Ignore;
            }
            if(life.messages.Count==0)Text(inbox,"Aucun message pour le moment. Le staff vous écrira au fil de la carrière.","muted");
        }

        void RefreshManagerReadState()
        {
            int unread=Career.life.messages.Count(m=>!m.read);
            var nav=root.Q<Button>("manager-nav-Messages");
            if(nav!=null){nav.EnableInClassList("unread-nav",unread>0);nav.tooltip=unread+" message(s) non lu(s)";var badge=nav.Q<Label>(className:"manager-nav-badge");if(badge!=null){badge.text=unread>99?"99+":unread.ToString();badge.style.display=unread>0?DisplayStyle.Flex:DisplayStyle.None;}}
            var inbox=root.Q<Button>("manager-inbox-open");if(inbox!=null)inbox.text="Messagerie · "+unread+" non lu(s)";
            foreach(var msg in Career.life.messages.AsEnumerable().Reverse().Take(3)){var meta=root.Q<Label>("manager-message-meta-"+msg.id);if(meta!=null)meta.text=Touchline.Core.Career.Epoch.AddDays(msg.day).ToString("dd MMM",French)+" · "+msg.sender+(msg.read?"":" · NON LU");}
        }

        void ManagerFixtureCard(VisualElement parent,Fixture fixture,bool employed)
        {
            var card=Card(parent,"manager-fixture");card.name="manager-next-fixture";var life=Career.life;
            bool active=Career.match!=null&&!Career.match.finished;
            bool scheduled=employed&&(fixture!=null||life.nextFixture!=int.MaxValue);
            int day=fixture?.day??life.nextFixture;
            string timing=!employed?"CARRIÈRE":active?"RENCONTRE EN COURS":!scheduled?"PRÉPARATION":day<=life.day?"JOUR DE MATCH":day-life.day==1?"DEMAIN":"DANS "+(day-life.day)+" JOURS";
            var top=Row(card,"manager-fixture-heading");Text(top,timing,"manager-fixture-tag");
            Text(top,scheduled?Touchline.Core.Career.Epoch.AddDays(day).ToString("dd MMM",French):"", "manager-context");
            if(!employed){Text(card,"Retrouver un banc","manager-fixture-title");Text(card,"Consultez les postes et les approches des clubs pour poursuivre votre carrière.","muted");Button(card,"Voir les opportunités",()=>Navigate("Carrière")).AddToClassList("primary");return;}
            if(!scheduled&&!active){Text(card,"Construire la préparation","manager-fixture-title");Text(card,"Aucune rencontre à venir. Consultez le calendrier pour organiser la suite.","muted");Button(card,"Ouvrir le calendrier",()=>Navigate("Calendrier")).AddToClassList("primary");return;}
            Text(card,fixture==null?"Rencontre de préparation":Career.CompetitionName(Database,fixture.league),"manager-context");
            string home=fixture?.home??Career.club,away=fixture?.away??(active?Career.match.away:opponent);
            int homeSide=home==Career.club?0:1;
            var teams=Row(card,"manager-fixture-teams");ManagerClub(teams,home);Text(teams,active?Career.match.score[homeSide]+" – "+Career.match.score[1-homeSide]:"VS","manager-versus");ManagerClub(teams,away);
            var homeClub=Array.Find(Database.clubs,c=>c.id==home);
            string venue=fixture?.venue;
            if(string.IsNullOrWhiteSpace(venue))venue=homeClub?.stadium;
            Text(card,(fixture?.neutral==true?"Terrain neutre":home==Career.club?"À domicile":"À l’extérieur")+(string.IsNullOrWhiteSpace(venue)?"":" · "+venue),"muted");
            var actions=Row(card,"manager-card-actions");var prepare=Button(actions,active?"Reprendre la rencontre":"Préparer la rencontre",()=>Navigate("Match"));prepare.AddToClassList("primary");prepare.name="manager-prepare-match";
            Button(actions,"Calendrier",()=>Navigate("Calendrier"));
            Text(card,"Dernier résultat : "+life.lastResult,"manager-last-result");
        }

        void ManagerClub(VisualElement parent,string id)
        {
            var club=Button(parent,"",()=>{if(Career.world!=null)ClubProfile(id);});club.AddToClassList("manager-club");club.tooltip="Ouvrir la fiche de "+ClubName(id);
            var crest=Resources.Load<Texture2D>("Logos/club-"+id);
            if(crest!=null){var image=new Image{image=crest,scaleMode=ScaleMode.ScaleToFit,pickingMode=PickingMode.Ignore};image.AddToClassList("manager-fixture-crest");club.Add(image);}
            Text(club,ClubName(id),"manager-club-name").pickingMode=PickingMode.Ignore;
        }

        void ManagerDecisionCard(VisualElement parent,bool employed)
        {
            var card=Card(parent,"manager-decisions");card.name="manager-decisions";
            Text(card,"À TRAITER","eyebrow");Text(card,"Vos décisions","section-title");int count=0;var life=Career.life;
            if(!employed){Text(card,"Votre mandat est terminé. Les dossiers médicaux, les budgets et les négociations restent à la charge du club.","muted");Button(card,"Carrière",()=>Navigate("Carrière"));return;}
            void Decision(string title,string detail,string destination,string key,bool warning=false,Action open=null){count++;var b=Button(card,"",()=>{if(open!=null)open();else if(destination=="Recrutement")OpenRecruitmentAgreements();else Navigate(destination);});b.name="manager-decision-"+key;b.AddToClassList("manager-decision");b.EnableInClassList("manager-decision-warning",warning);Text(b,title+"  ›","manager-decision-title").pickingMode=PickingMode.Ignore;Text(b,detail,"manager-decision-detail").pickingMode=PickingMode.Ignore;}
            int medical=life.medical.Count(c=>c.closed<0&&string.IsNullOrEmpty(c.responsibilityEndedReason)&&c.treatment=="pending"&&Career.PlayerMedicalResponsibility(c.player)&&Database.Find(c.player)?.team==Career.club);
            if(medical>0)Decision("Avis médical attendu",medical+" dossier(s) sans protocole choisi","Santé","medical",true);
            if(life.managerBanUntil>life.day)Decision("Suspension du manager","Décisions confiées au staff jusqu’au "+Touchline.Core.Career.Epoch.AddDays(life.managerBanUntil).ToString("dd MMM",French),"Staff et délégation","suspension",true);
            long payroll=Career.Payroll(Database);if(payroll>Career.WageBudget)Decision("Plafond salarial dépassé",Money(Core.Career.MonthlySalary(payroll))+" / mois · plafond "+Money(Core.Career.MonthlySalary(Career.WageBudget))+" / mois. Réduisez les charges ou demandez une révision du budget avant de recruter.","Finances","payroll",true);
            if(life.cash<0)Decision("Trésorerie négative",Money(life.cash)+" · examiner les charges et recettes","Finances","cash",true);
            int approved=life.projects.Count(p=>p.status=="approved");
            if(approved>0)Decision("Financement accordé",approved+" projet(s) attendent votre engagement","Infrastructures","facilities");
            int offers=Career.world?.offers.Count(o=>o.status=="accepted"&&!Career.PlayerRetirementEffective(o.player)&&Database.Find(o.player)?.team!="retired"&&(o.destination==null||o.destination==Career.club)&&o.due+7>=life.day)??0;
            if(offers>0)Decision("Accord de principe",offers+" négociation(s) à finaliser","Recrutement","offers");
            var loans=Career.outgoingLoans.Where(o=>o.owner==Career.club&&o.status=="accepted"&&!Career.PlayerRetirementEffective(o.player)&&Database.Find(o.player)?.team!="retired"&&o.due+7>=life.day).ToArray();
            if(loans.Length>0)Decision("Prêts sortants à signer",loans.Length+" accord(s) · première échéance "+AgreementDate(loans.Min(o=>o.due+7)),"Recrutement","outgoing",loans.Min(o=>o.due+7)<=life.day+1);
            var staffOffers=Career.staffOffers.Where(o=>o.club==Career.club&&o.status=="accepted"&&o.due+7>=life.day).ToArray();
            if(staffOffers.Length>0)Decision("Contrats staff à signer",staffOffers.Length+" accord(s) · première échéance "+AgreementDate(staffOffers.Min(o=>o.due+7)),"Staff et délégation","staff",staffOffers.Min(o=>o.due+7)<=life.day+1);
            int integrity=life.investigations.Count(i=>i.status=="pending"&&i.alerted&&string.IsNullOrEmpty(i.response));if(integrity>0)Decision("Alertes internes",integrity+" dossier(s) attendent votre réponse","Coulisses","integrity",true);
            int commercial=Career.world?.sponsors.Count(Career.CanSignCommercialQuote)??0;if(commercial>0)Decision("Partenariats à confirmer",commercial+" proposition(s) · aucun délai imposé","Finances","commercial");
            var approaches=Career.approaches.Where(o=>o.status=="open"&&o.club!=Career.club&&o.until>=life.day).ToArray();if(approaches.Length>0)Decision("Approches de clubs",approaches.Length+" offre(s) · première échéance "+AgreementDate(approaches.Min(o=>o.until)),"Carrière","approaches",approaches.Min(o=>o.until)<=life.day+1);
            var promises=life.players.Where(p=>p.promiseUntil>=life.day&&!Career.PlayerPromiseFulfilled(p.id)).OrderBy(p=>p.promiseUntil).ToArray();if(promises.Length>0)Decision("Engagements de temps de jeu",promises.Length+" promesse(s) · première échéance "+AgreementDate(promises[0].promiseUntil),"Messages","promises",promises[0].promiseUntil<=life.day+7,()=>Conversation(promises[0].id));
            if(count==0){Text(card,"Aucune décision urgente détectée","manager-all-clear");Text(card,"Préparez votre groupe et consultez les messages de vos collaborateurs.","muted");}
            if(employed){var links=Row(card,"manager-card-actions");Button(links,"Effectif",()=>Navigate("Effectif"));Button(links,"Infrastructures",()=>Navigate("Infrastructures"));}
        }

        void ManagerMetric(VisualElement parent,string label,string value,string detail,string destination,bool warning)
        {
            var button=Button(parent,"",()=>Navigate(destination));button.AddToClassList("manager-metric");button.EnableInClassList("manager-metric-warning",warning);
            Text(button,label,"eyebrow").pickingMode=PickingMode.Ignore;Text(button,value,"manager-metric-value").pickingMode=PickingMode.Ignore;Text(button,detail+"  ›","manager-metric-detail").pickingMode=PickingMode.Ignore;
        }
    }
}

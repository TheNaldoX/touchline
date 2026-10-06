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
        float continueAllowedAt;
        int[] newMessageIds=Array.Empty<int>();
        string inboxSearch="",contactSearch="",inboxCategory="Toutes";
        readonly HashSet<int> dismissedNotifications=new HashSet<int>();
        ClubLife notificationLife;
        bool conversationPhone;
        static bool MessageContains(string value,string query)=>French.CompareInfo.IndexOf(value??"",query??"",CompareOptions.IgnoreCase|CompareOptions.IgnoreNonSpace)>=0;
        string MessageDate(ClubMessage m)=>Core.Career.Epoch.AddDays(m.day).ToString("ddd dd MMM yyyy",French);
        void ShowManagerNotifications()
        {
            if(launchMenuVisible||Career?.life==null){root?.Q("manager-notification")?.RemoveFromHierarchy();return;}
            if(!ReferenceEquals(notificationLife,Career.life)){notificationLife=Career.life;dismissedNotifications.Clear();newMessageIds=Array.Empty<int>();}
            root.Q("manager-notification")?.RemoveFromHierarchy();
            if(modal!=null||page=="Messages"||page=="Match"&&Career.match!=null)return;
            var candidates=Career.life.messages.Where(m=>!dismissedNotifications.Contains(m.id)&&(!m.read||Career.MessageNeedsDecision(m)));
            var message=Career.OrderedMessages(candidates).FirstOrDefault(m=>Career.MessagePriority(m)>=2||newMessageIds.Contains(m.id));
            if(message==null)return;
            bool decision=Career.MessageNeedsDecision(message);bool today=decision&&Career.MessageDecisionDeadline(message)==Career.life.day;
            var banner=Row(root,"manager-notification");banner.name="manager-notification";banner.EnableInClassList("manager-notification-urgent",today);
            var copy=new VisualElement();copy.AddToClassList("manager-notification-copy");banner.Add(copy);
            Text(copy,today?"ÉCHÉANCE AUJOURD’HUI":decision?"DÉCISION ATTENDUE":"MESSAGE IMPORTANT","eyebrow");
            Text(copy,message.sender+" · "+message.subject,"section-title");
            if(decision)Text(copy,Career.MessageDecisionTiming(message),"manager-notification-timing").name="manager-notification-timing";
            int arrived=Career.life.messages.Count(m=>newMessageIds.Contains(m.id)&&!m.read);Text(copy,(arrived>0?arrived+" nouveau(x) message(s) · ":"")+MessageDate(message),"muted");
            Button(banner,"Lire / agir",()=>{Navigate("Messages");OpenMessage(message.id);}).name="manager-notification-open";
            var dismiss=Button(banner,"×",()=>{dismissedNotifications.Add(message.id);ShowManagerNotifications();});dismiss.tooltip="Masquer cette notification. Le dossier reste dans la messagerie.";
            AnimateEntry(banner,false);
        }
        void InboxWorkspace()
        {
            content.AddToClassList("inbox-page");var title=Row(content,"inbox-titlebar");Heading(title,"Messagerie");
            Text(title,Career.life.messages.Count(m=>!m.read)+" non lu(s) · "+Career.life.messages.Count(Career.MessageNeedsDecision)+" message(s) liés à une décision","inbox-summary").name="inbox-summary";
            var tools=Row(content,"inbox-toolbar");var directActions=Row(tools,"inbox-direct-actions");PlayerManagementButton(directActions,"Contacter un joueur",ContactPicker).AddToClassList("primary");
            var filters=Row(tools,"inbox-filters");filters.name="inbox-primary-filters";
            var searchRow=Row(content,"inbox-search-row");var search=new TextField("Recherche"){value=inboxSearch,name="inbox-search",tooltip="Contact, sujet ou texte du message"};search.AddToClassList("inbox-search-field");searchRow.Add(search);
            var categories=new List<string>{"Toutes","Médical","Joueurs","Direction","Recrutement","Staff et club"};var category=new DropdownField("Catégorie",categories,Math.Max(0,categories.IndexOf(inboxCategory))){name="inbox-category"};category.AddToClassList("inbox-category");searchRow.Add(category);
            var body=Scroll(content);body.name="inbox-workspace";var list=new VisualElement();list.name="inbox-thread-list";body.Add(list);
            if(!new[]{"Tous","Non lus","À traiter","Épinglés"}.Contains(inboxFilter))inboxFilter="Tous";
            foreach(var name in new[]{"Tous","Non lus","À traiter","Épinglés"})
            {
                var button=Button(filters,name,()=>{inboxFilter=name;foreach(var b in filters.Query<Button>().ToList())b.EnableInClassList("active",b.text==name);PopulateInbox(list);body.scrollOffset=Vector2.zero;});button.AddToClassList("inbox-primary-filter");button.EnableInClassList("active",inboxFilter==name);
            }
            var mark=Button(directActions,"Marquer ces messages lus",()=>{var unread=FilteredMessages().Where(m=>!m.read).ToArray();if(unread.Length==0)return;foreach(var m in unread)m.read=true;Save();RefreshManagerReadState();RefreshInboxReadState();});mark.name="inbox-mark-visible-read";mark.tooltip="Marque uniquement les messages correspondant aux filtres d’état, catégorie et recherche actuels. Une décision reste à traiter.";
            tools.Add(filters);IVisualElementScheduledItem pendingSearch=null;
            category.RegisterValueChangedCallback(e=>{inboxCategory=e.newValue;pendingSearch?.Pause();PopulateInbox(list);body.scrollOffset=Vector2.zero;});
            search.RegisterValueChangedCallback(e=>{inboxSearch=e.newValue;pendingSearch?.Pause();mark.SetEnabled(false);pendingSearch=search.schedule.Execute(()=>{PopulateInbox(list);body.scrollOffset=Vector2.zero;}).StartingIn(120);});PopulateInbox(list);
            Text(body,"Lire un message ne résout pas une décision. À traiter reste visible jusqu’à votre choix ou l’expiration du dossier. Les dates sont celles de votre carrière.","footnote");
        }
        ClubMessage[] FilteredMessages()
        {
            var messages=Career.life.messages.AsEnumerable();
            if(inboxFilter=="Non lus")messages=messages.Where(m=>!m.read);else if(inboxFilter=="À traiter")messages=messages.Where(Career.MessageNeedsDecision);else if(inboxFilter=="Épinglés")messages=messages.Where(m=>m.pinned);
            if(inboxCategory!="Toutes")messages=messages.Where(m=>Career.MessageCategory(m)==inboxCategory);
            return messages.Where(m=>MessageContains(m.sender+" "+m.subject+" "+m.text+" "+Database.Find(m.player)?.name,inboxSearch.Trim())).ToArray();
        }
        void PopulateInbox(VisualElement list)
        {
            list.Clear();var filtered=FilteredMessages();var mark=root.Q<Button>("inbox-mark-visible-read");mark?.SetEnabled(filtered.Any(m=>!m.read));
            var groups=Career.OrderedMessages(filtered).GroupBy(Career.MessageThreadKey).ToArray();
            // Index the complete history once; each card still reports its whole conversation.
            var threads=Career.life.messages.ToLookup(Career.MessageThreadKey);
            if(groups.Length==0){Text(Card(list),"Aucune conversation dans cette sélection.","empty-state");return;}
            foreach(var group in groups)
            {
                var latest=group.OrderByDescending(m=>m.id).First();var important=Career.OrderedMessages(group).First();var displayed=group.Any(Career.MessageNeedsDecision)?important:latest;var whole=threads[group.Key].ToArray();int unread=whole.Count(m=>!m.read);
                var card=Card(list,"inbox-thread");card.name="inbox-thread-"+latest.id;card.EnableInClassList("inbox-unread",group.Any(m=>!m.read));
                var row=Row(card,"inbox-thread-heading");Text(row,Database.Find(latest.player)?.name??latest.sender,"section-title");Text(row,Core.Career.Epoch.AddDays(displayed.day).ToString("dd MMM",French),"inbox-date");
                Text(card,displayed.subject,"inbox-subject");string preview=(displayed.text??"").Replace('\r',' ').Replace('\n',' ');var previewLabel=Text(card,preview.Length>140?preview.Substring(0,137)+"…":preview,"inbox-thread-preview");previewLabel.tooltip=displayed.text??"";if(displayed.id!=latest.id)Text(card,"Dernier échange : "+latest.subject+" · "+Core.Career.Epoch.AddDays(latest.day).ToString("dd MMM",French),"inbox-status");
                var meta=Row(card,"inbox-thread-actions");Text(meta,Career.MessageCategory(displayed)+" · "+whole.Length+" échange(s)"+(unread>0?" · "+unread+" non lu(s)":" · lu")+(group.Any(Career.MessageNeedsDecision)?" · À TRAITER":"")+(whole.Any(m=>m.pinned)?" · ÉPINGLÉ":""),"inbox-status");
                if(Career.MessageNeedsDecision(important))Text(card,Career.MessageDecisionTiming(important),"inbox-status");
                Button(meta,group.Any(Career.MessageNeedsDecision)?"Lire et décider":"Lire",()=>OpenMessage(displayed.id));
            }
        }
        void RefreshInboxReadState()
        {
            var workspace=root?.Q("inbox-workspace");if(workspace==null)return;
            var summary=root.Q<Label>("inbox-summary");if(summary!=null)summary.text=Career.life.messages.Count(m=>!m.read)+" non lu(s) · "+Career.life.messages.Count(Career.MessageNeedsDecision)+" message(s) liés à une décision";
            var list=workspace.Q("inbox-thread-list");if(list!=null)PopulateInbox(list);
        }
        void OpenMessageThread(int id)
        {
            var selected=Career.life.messages.FirstOrDefault(m=>m.id==id);if(selected==null){Message("Ce message n’est plus dans l’historique conservé.");return;}
            if(!selected.read){selected.read=true;Save();}RefreshManagerReadState();RefreshInboxReadState();var panel=Modal(Database.Find(selected.player)?.name??selected.sender);panel.name="inbox-conversation";
            var actions=Row(panel,"inbox-thread-actions");var pin=Button(actions,selected.pinned?"Désépingler":"Épingler",()=>{selected.pinned=!selected.pinned;Save();OpenMessageThread(id);});pin.name="inbox-pin";
            Button(actions,"Marquer la conversation lue",()=>{var unread=Career.life.messages.Where(m=>!m.read&&Career.MessageThreadKey(m)==Career.MessageThreadKey(selected)).ToArray();if(unread.Length==0)return;foreach(var m in unread)m.read=true;Save();RefreshManagerReadState();RefreshInboxReadState();OpenMessageThread(id);}).name="inbox-mark-thread-read";
            var scroll=Scroll(panel);scroll.name="inbox-thread-scroll";var thread=Career.life.messages.Where(m=>Career.MessageThreadKey(m)==Career.MessageThreadKey(selected)).OrderBy(m=>m.day).ThenBy(m=>m.id).ToArray();
            var shown=thread.TakeLast(30).ToList();if(!shown.Contains(selected)){shown.RemoveAt(0);shown.Add(selected);shown=shown.OrderBy(m=>m.id).ToList();}
            if(thread.Length>30)Text(scroll,"Les 30 derniers échanges sont affichés, ainsi que le message sélectionné.","footnote");
            VisualElement selectedCard=null;
            foreach(var m in shown)
            {
                var card=Card(scroll,m.sender=="Vous"?"conversation-outgoing":"conversation-incoming");card.name="inbox-message-"+m.id;card.EnableInClassList("conversation-selected",m.id==id);
                Text(card,m.sender+" · "+MessageDate(m),"eyebrow");Text(card,m.subject,"section-title");Text(card,m.text);if(!m.read)Text(card,"Non lu","muted");if(m.id==id)selectedCard=card;
            }
            if(selectedCard!=null)scroll.schedule.Execute(()=>scroll.ScrollTo(selectedCard)).StartingIn(120);
            if(Career.MessageNeedsDecision(selected))Text(panel,Career.MessageDecisionTiming(selected),"status-warning");MessageActions(panel,selected);
        }
        void MessageActions(VisualElement panel,ClubMessage m)
        {
            var actions=Row(panel,"inbox-thread-actions");var p=Database.Find(m.player);bool retired=p!=null&&(p.team=="retired"||Career.PlayerRetirementEffective(p.id));bool own=!retired&&p?.team==Career.club&&Career.life.players.Any(l=>l.id==p.id);
            if(p!=null)Button(actions,"Fiche du joueur",()=>PlayerProfile(p.id,()=>OpenMessageThread(m.id),"Retour au message"));
            if(own&&(m.action=="talk"||m.action=="medical"))PlayerManagementButton(actions,"Discuter avec le joueur",()=>Conversation(p.id)).AddToClassList("primary");
            if(own&&Career.Person(p.id).discussionFocus=="development")PlayerManagementButton(actions,"Parcours / décisions",()=>DevelopmentConversationPlan(p.id)).name="conversation-open-development";
            string target=m.action=="staff"?"Staff et délégation":m.action=="calendar"?"Calendrier":m.action=="facilities"?"Infrastructures":m.action=="integrity"?"Coulisses":m.action=="scout"||m.action=="transfer"?"Recrutement":m.action=="jobs"?"Carrière":m.action=="academy"?"Formation":m.action=="finance"||m.sender=="Direction financière"?"Finances":m.action=="press"?"Presse":m.action=="medical"?"Santé":null;
            if(own&&m.action=="medical")Button(actions,Career.MessageNeedsDecision(m)?"Dossier médical / décider":"Dossier médical actuel",()=>MedicalDetail(p.id)).AddToClassList("primary");
            if(p!=null&&m.action=="medical"&&(!Career.PlayerMedicalResponsibility(p.id)||p.team!=Career.club))Button(actions,"Dossier médical historique",()=>MedicalDetail(p.id)).name="inbox-medical-history";
            if(Career.PendingCommercialAgreement(m)!=null)PlayerManagementButton(actions,"Vérifier le partenariat",()=>CommercialAgreementReview(m)).name="inbox-review-commercial";
            if(Career.PendingJobApproach(m)!=null){var approach=Button(actions,"Examiner l’approche",()=>JobApproachReview(m));approach.name="inbox-review-approach";}
            var transfer=Career.PendingTransferAgreement(m);var outgoing=Career.PendingOutgoingLoanAgreement(m);var staff=Career.PendingStaffAgreement(m);
            if(transfer!=null)PlayerManagementButton(actions,"Vérifier et signer l’accord",()=>TransferAgreementReview(transfer)).name="inbox-sign-transfer";
            else if(outgoing!=null)PlayerManagementButton(actions,"Vérifier et signer le prêt",()=>OutgoingLoanAgreementReview(outgoing)).name="inbox-sign-outgoing";
            else if(staff!=null)PlayerManagementButton(actions,"Vérifier et signer le contrat staff",()=>StaffAgreementReview(staff)).name="inbox-sign-staff";
            else if(p!=null&&!retired&&m.action=="transfer"&&!(m.reference?.StartsWith("outgoing-loan/",StringComparison.Ordinal)??false)&&m.subject!="Accord de prêt"&&m.subject!="Prêt proposé")PlayerManagementButton(actions,"Négociation / contrat",()=>TransferDialog(p.id));
            if(target!=null)Button(actions,"Ouvrir · "+(m.action=="scout"?"Rapports de recrutement":PageLabel(target)),()=>{
                if(m.action=="scout"){recruitmentTab="Rapports";scoutReportFilter="Tous";Navigate("Recrutement");if(p!=null)root.schedule.Execute(()=>{var report=root.Q("scout-report-"+p.id);var scroll=content.Q<ScrollView>();if(report!=null&&scroll!=null){var viewport=scroll.contentViewport.worldBound;scroll.scrollOffset=new Vector2(0,Mathf.Max(0,scroll.scrollOffset.y+report.worldBound.yMin-viewport.yMin));}}).StartingIn(100);}
                else if(target=="Recrutement")OpenRecruitmentAgreements();else Navigate(target);
            }).name="inbox-open-destination";
        }
        void InboxContactPicker()
        {
            var panel=Modal("Contacter un joueur");var search=new TextField("Rechercher dans mon effectif"){value=contactSearch,name="inbox-contact-search"};panel.Add(search);var scroll=Scroll(panel);
            void Populate(){scroll.Clear();foreach(var player in Database.Squad(Career.club).Where(p=>!Career.PlayerRetirementEffective(p.id)&&Career.life.players.Any(l=>l.id==p.id)&&MessageContains(p.name,contactSearch)).OrderBy(p=>p.name)){var state=Career.Person(player.id);var card=Card(scroll,"inbox-contact");Text(card,player.name,"section-title");Text(card,Career.PlayerSituation(Database,player.id),"muted");Text(card,"Moral "+state.morale.ToString("0")+" · confiance "+state.trust.ToString("0"),"footnote");PlayerManagementButton(card,"SMS / appel",()=>Conversation(player.id)).name="inbox-contact-"+player.id;}if(scroll.contentContainer.childCount==0)Text(scroll,"Aucun joueur dans cette recherche.","empty-state");}
            search.RegisterValueChangedCallback(e=>{contactSearch=e.newValue;Populate();});Populate();
        }
        void PlayerConversationWorkspace(string id)=>PlayerDiscussionWorkspace(id);
        void DevelopmentConversationPlan(string id)
        {
            var player=Database.Find(id);if(player?.team!=Career.club||!Career.life.players.Any(p=>p.id==id)){Message("Ce joueur n’appartient plus à votre effectif.");return;}
            var panel=Modal("Parcours · "+player.name);panel.name="conversation-development-plan";panel.AddToClassList("conversation-development-dialog");var body=Scroll(panel);var card=Card(body);Text(card,"Conseil du formateur","conversation-plan-title");Text(card,Career.PlayerDevelopmentAdvice(Database,id),"conversation-plan-advice");var coach=Career.Staff("youth");Text(card,coach.wage>0?coach.name+" · entraînement "+coach.coaching+" / 20 · avis de simulation, sans garantie de résultat.":"Responsable de formation : poste vacant. L’encadrement ne peut pas fournir d’avis individuel précis.","footnote");
            var actions=Row(body,"inbox-thread-actions");Button(actions,"Entraînement",()=>Navigate("Club")).name="conversation-development-training";Button(actions,"Staff / délégation",()=>Navigate("Staff et délégation"));
            if(player.age>=18&&player.age<24)Button(actions,"Proposer un prêt",()=>YouthLoanDialog(id)).name="conversation-development-loan";
            Button(actions,"Tactique",()=>Navigate("Tactique"));
            var path=Career.world?.youth.FirstOrDefault(y=>y.player==id);
            if(path!=null)
            {
                var plan=Card(body);Text(plan,"Parcours de formation","conversation-plan-title");
                var keys=new[]{"balanced","physical","technical","tactical"};var labels=new[]{"Équilibré","Physique","Technique","Tactique"};int focus=Array.IndexOf(keys,path.focus);
                Text(plan,"Axe conservé au dossier : "+(focus>=0?labels[focus]:"Non renseigné"),"muted").name="conversation-development-history";
                Text(plan,"Ce joueur évolue chez les professionnels. Cet axe reste un repère dans son dossier ; il ne constitue pas un programme du centre. Son travail dépend du groupe professionnel, de sa charge et de ses apparitions.","footnote");
                Button(plan,"Ouvrir son parcours",()=>AcademyPlayerPlan(id)).name="conversation-development-path";
            }
            else Text(card,"Ce joueur évolue déjà chez les professionnels. Son entraînement, sa charge et ses sélections sont les leviers disponibles ; aucun programme de centre ne lui est attribué automatiquement.","footnote");
            Button(body,"Retour à l’échange",()=>Conversation(id));
        }
    }
}

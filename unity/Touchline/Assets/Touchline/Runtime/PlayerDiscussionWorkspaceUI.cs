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
        void PlayerDiscussionWorkspace(string id)
        {
            var context=PlayerDiscussionPresentation.From(Career,Database,id);if(!context.own){Message("Ce joueur n’appartient plus à votre effectif.");return;}
            var player=Database.Find(id);var person=Career.Person(id);var panel=Modal(player.name+" · échange privé");panel.name="player-conversation";panel.AddToClassList("discussion-dialog");
            panel.Children().First().AddToClassList("discussion-heading");
            var summary=new VisualElement{name="conversation-summary"};summary.AddToClassList("discussion-summary");panel.Add(summary);
            var icon=new VisualElement{name="conversation-portrait",tooltip="Portrait réel lorsqu’il est disponible, sinon initiales."};icon.AddToClassList("discussion-portrait");summary.Add(icon);Text(icon,string.Concat(player.name.Split(' ').Where(s=>s.Length>0).Take(2).Select(s=>s.Substring(0,1))),"discussion-initials");Text(icon,"Initiales","discussion-initials-caption");
            portraits??=gameObject.AddComponent<PortraitStore>();portraits.Load(id,texture=>{if(texture==null||icon.panel==null)return;icon.Clear();icon.Add(new Image{image=texture,scaleMode=ScaleMode.ScaleToFit,name="conversation-player-photo"});});
            var summaryCopy=new VisualElement();summaryCopy.AddToClassList("discussion-summary-copy");summary.Add(summaryCopy);Text(summaryCopy,Career.PlayerSituation(Database,id),"discussion-situation");Text(summaryCopy,"Moral "+person.morale.ToString("0")+" · confiance "+person.trust.ToString("0")+" · condition "+person.fitness.ToString("0")+" %","discussion-metrics");
            if(person.promiseUntil>=0)Text(summaryCopy,Career.PlayerPromiseStatus(id),Career.PlayerPromiseFulfilled(id)?"positive":"status-warning");
            var tabs=Row(panel,"discussion-tabs");var exchange=new VisualElement{name="conversation-exchange"};exchange.AddToClassList("discussion-exchange");panel.Add(exchange);
            var history=Scroll(panel);history.name="conversation-history";history.AddToClassList("discussion-history");history.style.display=DisplayStyle.None;
            var footer=new VisualElement{name="conversation-footer"};footer.AddToClassList("discussion-footer");panel.Add(footer);
            void Show(bool archive){exchange.style.display=archive?DisplayStyle.None:DisplayStyle.Flex;history.style.display=archive?DisplayStyle.Flex:DisplayStyle.None;footer.style.display=archive?DisplayStyle.None:DisplayStyle.Flex;foreach(var tab in tabs.Query<Button>().ToList())tab.EnableInClassList("active",tab.name==(archive?"conversation-tab-history":"conversation-tab-exchange"));}
            var privateMessages=Career.life.messages.Where(m=>m.player==id&&(m.action=="talk"||m.sender=="Vous")).ToArray();
            Button(tabs,"Échanger",()=>Show(false)).name="conversation-tab-exchange";Button(tabs,"Historique · "+privateMessages.Length,()=>Show(true)).name="conversation-tab-history";
            var layout=new VisualElement{name="conversation-layout"};layout.AddToClassList("discussion-layout");exchange.Add(layout);
            var browser=new VisualElement();browser.AddToClassList("discussion-browser");layout.Add(browser);
            var filter=new Toggle("Tous les sujets"){name="conversation-all-topics"};filter.AddToClassList("discussion-all-topics");browser.Add(filter);
            var choices=Scroll(browser);choices.name="conversation-topic-list";choices.AddToClassList("discussion-topic-list");
            var detail=Scroll(layout);detail.name="conversation-topic-detail";detail.AddToClassList("discussion-detail");
            var dropdown=new DropdownField("Sujet",context.topics.Select(t=>t.title).ToList(),Array.FindIndex(context.topics,t=>t.key==context.defaultTopic)){name="conversation-topic-picker"};dropdown.AddToClassList("discussion-topic-picker");detail.Add(dropdown);
            var shortcuts=Row(detail,"discussion-shortcuts");shortcuts.name="conversation-shortcuts";
            var copy=new VisualElement{name="conversation-topic-copy"};detail.Add(copy);
            var channel=new DropdownField("Canal",new List<string>{"SMS","Appel · compte rendu"},conversationPhone?1:0){name="conversation-channel"};channel.AddToClassList("discussion-channel");
            var cooldown=Text(footer,context.waitDays>0?"Nouvel échange le "+Core.Career.Epoch.AddDays(context.availableDay).ToString("ddd dd MMM",French)+" · encore "+context.waitDays+" jour(s). Vous pouvez consulter l’historique et préparer le sujet.":"Un échange tous les sept jours. Vos décisions et les matchs restent déterminants.","discussion-cooldown");cooldown.name="conversation-cooldown";
            string selected=context.defaultTopic;var actions=Row(footer,"discussion-actions");actions.Add(channel);var send=PlayerManagementButton(actions,conversationPhone?"Aborder pendant l’appel":"Envoyer le SMS",()=>{try{Career.Talk(Database,id,selected,conversationPhone);Save();Build();OpenMessage(Career.life.messages.Last(m=>m.player==id).id);}catch(Exception e){Message(e.Message);}});send.name="conversation-send";send.AddToClassList("primary");
            var blocked=Text(footer,"","discussion-blocked");blocked.name="conversation-send-reason";
            void Select(string key){
                selected=key;var topic=context.topics.First(t=>t.key==key);dropdown.SetValueWithoutNotify(topic.title);copy.Clear();Text(copy,topic.title,"discussion-topic-title");Text(copy,topic.reason,"discussion-topic-context");Text(copy,topic.description,"discussion-topic-description");if(topic.warning!=null)Text(copy,topic.warning,"discussion-topic-warning");
                if(topic.key=="development"&&person.discussionFocus=="development"){Text(copy,Career.PlayerDevelopmentAdvice(Database,id),"discussion-topic-description");PlayerManagementButton(copy,"Concrétiser le parcours",()=>DevelopmentConversationPlan(id)).name="conversation-open-development";}
                if(topic.key=="recovery"&&Career.Injury(id)!=null)Button(copy,"Dossier médical",()=>MedicalDetail(id)).name="conversation-medical";
                Text(copy,"Personnalité de carrière : "+Career.CareerPersonality(id)+". Trait simulé, sans description de la personne réelle.","discussion-personality");
                blocked.text=topic.blocked??(!PlayerManagementAvailable?Career.world?.managerStatus!="employed"?"Vous n’êtes plus en poste.":Career.match?.finished==false?"Ce dialogue sera disponible après le match.":"Votre adjoint gère les décisions pendant votre suspension.":context.waitDays>0?"Laissez sept jours au dernier échange pour produire ses effets.":"");blocked.style.display=string.IsNullOrEmpty(blocked.text)?DisplayStyle.None:DisplayStyle.Flex;
                send.SetEnabled(PlayerManagementAvailable&&context.waitDays==0&&topic.blocked==null);
                foreach(var choice in panel.Query<Button>().ToList().Where(b=>b.name?.StartsWith("conversation-select-",StringComparison.Ordinal)==true))choice.EnableInClassList("active",choice.name=="conversation-select-"+key);
            }
            void Populate(){choices.Clear();var shown=filter.value?context.topics:context.topics.Where(t=>t.recommended).Take(3).ToArray();if(shown.Length==0){Text(choices,"Aucun sujet particulier ne ressort. Vous pouvez clarifier son rôle ou consulter tous les sujets.","discussion-empty");shown=new[]{context.topics.First(t=>t.key=="role")};}foreach(var t in shown){var button=Button(choices,t.title,()=>Select(t.key));button.name="conversation-select-"+t.key;button.AddToClassList("discussion-choice");button.tooltip=t.reason+(t.blocked!=null?" · "+t.blocked:"");button.EnableInClassList("active",selected==t.key);}Text(choices,filter.value?"11 sujets disponibles. Choisir un sujet n’envoie aucun message.":"Sujets suggérés d’après la situation enregistrée, sans nouvelle urgence.","discussion-browser-hint");}
            filter.RegisterValueChangedCallback(e=>Populate());dropdown.RegisterValueChangedCallback(e=>Select(context.topics.First(t=>t.title==e.newValue).key));
            foreach(var topic in context.topics.Where(t=>t.recommended).Take(3)){var quick=Button(shortcuts,topic.title,()=>Select(topic.key));quick.name="conversation-quick-"+topic.key;quick.tooltip=topic.reason;}
            channel.RegisterValueChangedCallback(e=>{conversationPhone=channel.index==1;send.text=conversationPhone?"Aborder pendant l’appel":"Envoyer le SMS";});
            var historyFilter=new DropdownField("Afficher",new List<string>{"Échanges privés","Staff et médical","Toute la relation"},0){name="conversation-history-filter"};historyFilter.AddToClassList("discussion-history-filter");history.Add(historyFilter);var entries=new VisualElement{name="conversation-history-entries"};history.Add(entries);int historyPage=0;
            void PopulateHistory(){entries.Clear();var messages=Career.life.messages.Where(m=>m.player==id&&(historyFilter.index==2||historyFilter.index==0&&(m.action=="talk"||m.sender=="Vous")||historyFilter.index==1&&m.action!="talk"&&m.sender!="Vous")).OrderByDescending(m=>m.id).ToArray();int pages=Math.Max(1,(messages.Length+11)/12);historyPage=Math.Min(historyPage,pages-1);Text(entries,messages.Length+" message(s) conservés · page "+(historyPage+1)+" / "+pages,"discussion-history-count");foreach(var m in messages.Skip(historyPage*12).Take(12)){var card=Card(entries,m.sender=="Vous"?"conversation-outgoing":"conversation-incoming");card.AddToClassList("discussion-history-entry");Text(card,m.sender+" · "+MessageDate(m),"eyebrow");Text(card,m.subject,"discussion-history-subject");Text(card,m.text,"discussion-history-text");Button(card,m.read?"Ouvrir le fil":"Ouvrir · non lu",()=>OpenMessage(m.id)).name="conversation-history-open-"+m.id;}if(messages.Length==0)Text(entries,"Aucun échange conservé dans cette sélection.","discussion-empty");if(pages>1){var nav=Row(entries,"discussion-history-nav");var newer=Button(nav,"Plus récents",()=>{historyPage--;PopulateHistory();history.scrollOffset=Vector2.zero;});newer.name="conversation-history-newer";newer.SetEnabled(historyPage>0);var older=Button(nav,"Précédents",()=>{historyPage++;PopulateHistory();history.scrollOffset=Vector2.zero;});older.name="conversation-history-older";older.SetEnabled(historyPage+1<pages);}}
            historyFilter.RegisterValueChangedCallback(e=>{historyPage=0;PopulateHistory();});PopulateHistory();Populate();Select(selected);Show(false);
        }
    }
}

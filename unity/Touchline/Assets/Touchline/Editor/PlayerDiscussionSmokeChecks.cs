using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;
using Touchline.Core;

namespace Touchline.Editor
{
    // Invoked only by ManagementSmoke's isolated visual-validation session.
    // The player identity is real; medical/relationship/history states below
    // are explicit test fixtures, not observations of the real person.
    public static class PlayerDiscussionSmokeChecks
    {
        static string player,historyIds,selectedTitle;static int historyFilter,channel;
        static VisualElement Root(TouchlineApp app)=>app.GetComponent<UIDocument>().rootVisualElement;
        static void Require(bool b,string why){if(!b)throw new InvalidOperationException(why);}
        static void Click(TouchlineApp app,string name){var b=Root(app).Query<Button>().ToList().First(x=>x.name==name||x.text==name);using(var e=NavigationSubmitEvent.GetPooled()){e.target=b;b.SendEvent(e);}}
        static void Open(TouchlineApp app)=>typeof(TouchlineApp).GetMethod("PlayerConversationWorkspace",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(app,new object[]{player});
        static string KnownState(TouchlineApp app){var l=app.Career.life;var p=app.Career.Person(player);return string.Join("|",l.cash,l.seed,l.messages.Count,app.Career.world.contracts.Count,p.lastTalk,p.appearances,p.promiseUntil,p.promiseStarts,p.restUntil,p.trust,p.morale,p.discussionFocus);}
        public static void AssertThreadReadNoop(TouchlineApp app,int selectedId)
        {
            var c=app.Career;var m=c.life.messages.Single(x=>x.id==selectedId);bool unread=c.life.messages.Any(x=>!x.read&&c.MessageThreadKey(x)==c.MessageThreadKey(m));int before=app.SaveRequestCount;Click(app,"inbox-mark-thread-read");Require(app.SaveRequestCount==before+(unread?1:0),"Marking a thread read did not persist exactly once when needed.");before=app.SaveRequestCount;Click(app,"inbox-mark-thread-read");Require(app.SaveRequestCount==before,"An already-read thread caused a needless save.");
        }
        public static void PrepareAndOpen(TouchlineApp app)
        {
            var c=app.Career;player=app.Database.Squad(c.club).First(p=>c.life.players.Any(x=>x.id==p.id)&&c.Injury(p.id)==null).id;var state=c.Person(player);state.lastTalk=c.life.day-7;state.fitness=64;state.trust=45;state.morale=68;c.OpenInjury(app.Database,player,"bruise");
            for(int i=0;i<14;i++)c.Mail(i%2==0?"Vous":app.Database.Find(player).name,i%2==0?"SMS envoyé":"Réponse","Échange synthétique de validation numéro "+i,player,"talk");
            for(int i=0;i<5;i++)c.Mail("Adjoint","Note de suivi","Note synthétique de validation numéro "+i,player,"club");Open(app);
        }
        public static void AssertChooserAndRememberHistory(TouchlineApp app)
        {
            var root=Root(app);string before=KnownState(app);int saves=app.SaveRequestCount;var picker=root.Q<DropdownField>("conversation-topic-picker");Require(picker.choices.Count==11,"The compact chooser lost a discussion topic.");root.Q<Toggle>("conversation-all-topics").value=true;Require(root.Query<Button>().ToList().Count(b=>b.name?.StartsWith("conversation-select-",StringComparison.Ordinal)==true)==11,"All-topics landscape list is incomplete.");
            foreach(var title in picker.choices.ToArray()){picker.value=title;Require(root.Q("conversation-topic-copy").Query<Label>().ToList().Any(l=>l.text==title),"Selecting a topic did not render its details.");}
            picker.value="Promettre du temps de jeu";Require(!root.Q<Button>("conversation-send").enabledInHierarchy&&root.Q<Label>("conversation-send-reason").text.Contains("blessure"),"An injured player can receive a new playing-time promise.");
            picker.value="Mobiliser un cadre";root.Q<DropdownField>("conversation-channel").index=1;selectedTitle=picker.value;channel=1;Require(root.Q<Button>("conversation-send").text=="Aborder pendant l’appel","Changing channel did not update the single send button.");
            Require(KnownState(app)==before&&app.SaveRequestCount==saves,"Choosing subjects/channel applied Talk effects or saved the career.");Click(app,"conversation-tab-history");root.Q<DropdownField>("conversation-history-filter").index=2;Click(app,"conversation-history-older");historyFilter=2;historyIds=string.Join("|",root.Q("conversation-history-entries").Query<Button>().ToList().Where(b=>b.name?.StartsWith("conversation-history-open-",StringComparison.Ordinal)==true).Select(b=>b.name));Require(!string.IsNullOrEmpty(historyIds),"Older discussion messages are unreachable.");
        }
        public static void AssertHistoryRetainedAfterRotation(TouchlineApp app)
        {
            var root=Root(app);Require(root.Q<ScrollView>("conversation-history").resolvedStyle.display!=DisplayStyle.None&&root.Q<DropdownField>("conversation-history-filter").index==historyFilter,"Rotation discarded active history tab/filter.");string ids=string.Join("|",root.Q("conversation-history-entries").Query<Button>().ToList().Where(b=>b.name?.StartsWith("conversation-history-open-",StringComparison.Ordinal)==true).Select(b=>b.name));Require(ids==historyIds,"Rotation discarded the older history page.");
        }
        public static void ReturnToPreparedTopic(TouchlineApp app)
        {
            var root=Root(app);Click(app,"conversation-tab-exchange");Require(root.Q<DropdownField>("conversation-topic-picker").value==selectedTitle&&root.Q<DropdownField>("conversation-channel").index==channel,"Rotation changed the prepared subject or SMS/call channel.");
        }
        public static void AssertPortraitAndBeginCooldown(TouchlineApp app)
        {
            var root=Root(app);var picker=root.Q<DropdownField>("conversation-topic-picker");Require(root.ClassListContains("narrow")&&picker.resolvedStyle.display!=DisplayStyle.None&&picker.worldBound.height>=43,"Portrait lacks a reachable touch-sized selector for all topics.");AssertBounds(app);app.Career.Talk(app.Database,player,"support",true);Open(app);
        }
        public static void AssertCooldownAndHistory(TouchlineApp app)
        {
            var root=Root(app);var picker=root.Q<DropdownField>("conversation-topic-picker");foreach(var title in picker.choices.ToArray()){picker.value=title;Require(!root.Q<Button>("conversation-send").enabledInHierarchy,"Changing the subject bypassed the seven-day shared cooldown.");}
            Require(root.Q<Label>("conversation-cooldown").text.Contains("7 jour(s)")&&root.Q<Label>("conversation-cooldown").text.Contains(Core.Career.Epoch.AddDays(app.Career.life.day+7).ToString("ddd dd MMM",System.Globalization.CultureInfo.GetCultureInfo("fr-FR"))),"Cooldown lacks the exact next discussion date.");int saves=app.SaveRequestCount;Click(app,"conversation-tab-history");root.Q<DropdownField>("conversation-history-filter").index=1;Require(root.Q("conversation-history-entries").Query<Label>().ToList().Any(l=>l.text.Contains("Note synthétique de validation")),"Staff context became inaccessible during cooldown.");root.Q<DropdownField>("conversation-history-filter").index=0;Require(root.Q("conversation-history-entries").Query<Button>().ToList().Count(b=>b.name?.StartsWith("conversation-history-open-",StringComparison.Ordinal)==true)==12,"Private history does not page its retained messages.");Require(app.SaveRequestCount==saves,"Browsing history marked messages read or caused a save.");Click(app,"conversation-tab-exchange");AssertBounds(app);
        }
        public static void AssertBounds(TouchlineApp app)
        {
            var root=Root(app);var panel=root.Q("player-conversation");var send=root.Q<Button>("conversation-send");var box=root.worldBound;Require(panel!=null&&send!=null&&send.worldBound.height>=43&&send.worldBound.width>=43&&send.worldBound.xMin>=box.xMin-1&&send.worldBound.xMax<=box.xMax+1&&send.worldBound.yMin>=box.yMin-1&&send.worldBound.yMax<=box.yMax+1,"Discussion send footer is clipped or smaller than 44 logical pixels.");
            Require(root.Q("manager-notification")==null,"Rotation restored a notification behind an active discussion.");
            Require(root.Q("conversation-portrait")!=null,"Discussion identity has neither real portrait nor initials fallback.");
        }
    }
}

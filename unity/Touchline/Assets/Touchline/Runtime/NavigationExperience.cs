using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Touchline
{
    public sealed partial class TouchlineApp
    {
        readonly List<string> backPages=new List<string>(),forwardPages=new List<string>(),recentPages=new List<string>();
        readonly Dictionary<string,Vector2[]> pageScroll=new Dictionary<string,Vector2[]>();
        string builtPage,builtScrollKey,historyClub;bool historyTravel;
        string ScrollPageKey=>page+"/"+(page switch{"Tactique"=>tacticalTab,"Recrutement"=>recruitmentTab,"Calendrier"=>calendarTab,"Finances"=>financeTab,_=>""})+"/"+(root.ClassListContains("short-wide")?"short-wide":root.ClassListContains("narrow")?"narrow":"wide");
        static string PageLabel(string id)=>id=="Club"?"Bureau":id=="Coulisses"?"Zone grise":id;
        public static bool DirectoryMatches(string label,string query)=>French.CompareInfo.IndexOf(label??"",query??"",System.Globalization.CompareOptions.IgnoreCase|System.Globalization.CompareOptions.IgnoreNonSpace)>=0;
        bool MotionEnabled=>PlayerPrefs.GetInt("reduce-motion",0)==0;
        public int InterfaceAnimationCount {get;private set;}
        void ReflowInterface()
        {
            // Keep the actual controls: rebuilding a negotiation would discard its unsent offer.
            var retained=modal;
            var focused=root.focusController?.focusedElement as VisualElement;
            bool restoreFocus=retained!=null&&focused!=null&&retained.Contains(focused);
            var scrolls=retained?.Query<ScrollView>().ToList();
            var positions=scrolls?.Select(s=>s.scrollOffset).ToArray();
            retained?.RemoveFromHierarchy();
            Build();
            if(retained==null)return;
            if(modal!=null)return;
            modal=retained;root.Add(retained);if(!launchMenuVisible&&Career?.life!=null)ShowManagerNotifications();
            retained.schedule.Execute(()=>{if(modal!=retained)return;for(int i=0;i<scrolls.Count;i++)scrolls[i].scrollOffset=positions[i];if(restoreFocus)focused.Focus();}).StartingIn(35);
        }
        void RememberPage()
        {
            if(historyClub!=Career.club){lineupUndo=null;backPages.Clear();forwardPages.Clear();recentPages.Clear();pageScroll.Clear();builtPage=null;builtScrollKey=null;historyClub=Career.club;}
            if(content!=null&&builtScrollKey!=null)pageScroll[builtScrollKey]=content.Query<ScrollView>().ToList().Select(s=>s.scrollOffset).ToArray();
        }
        void RecordNavigation(string destination)
        {
            RememberPage();if(destination==page)return;
            if(!historyTravel){backPages.Add(page);if(backPages.Count>40)backPages.RemoveAt(0);forwardPages.Clear();}
            recentPages.Remove(destination);recentPages.Insert(0,destination);if(recentPages.Count>4)recentPages.RemoveAt(4);
        }
        void HistoryStep(bool forward)
        {
            var source=forward?forwardPages:backPages;var target=forward?backPages:forwardPages;if(source.Count==0)return;
            string destination=source[source.Count-1];source.RemoveAt(source.Count-1);target.Add(page);historyTravel=true;
            try{Navigate(destination);}finally{historyTravel=false;}
        }
        void FinishPage()
        {
            bool changed=builtPage!=page;builtPage=page;builtScrollKey=ScrollPageKey;var current=content;
            if(pageScroll.TryGetValue(builtScrollKey,out var positions))current.schedule.Execute(()=>{var scrolls=current.Query<ScrollView>().ToList();for(int i=0;i<Math.Min(scrolls.Count,positions.Length);i++)scrolls[i].scrollOffset=positions[i];}).StartingIn(35);
            if(changed&&page!="Match")AnimateEntry(current);
        }
        void AnimateEntry(VisualElement element,bool drawer=false)
        {
            if(!MotionEnabled)return;InterfaceAnimationCount++;float started=Time.realtimeSinceStartup;float distance=drawer?22:7;var duration=drawer?.2f:.15f;
            element.style.opacity=.35f;element.style.translate=new Translate(0,distance);
            IVisualElementScheduledItem animation=null;
            animation=element.schedule.Execute(()=>{float t=Mathf.Clamp01((Time.realtimeSinceStartup-started)/duration),ease=1-Mathf.Pow(1-t,3);element.style.opacity=Mathf.Lerp(.35f,1,ease);element.style.translate=new Translate(0,distance*(1-ease));if(t>=1)animation?.Pause();}).Every(16);
        }
        void NavigationButtons(VisualElement brand)
        {
            var back=Button(brand,"‹",()=>HistoryStep(false));back.name="history-back";back.tooltip="Écran précédent";back.AddToClassList("history-button");back.SetEnabled(backPages.Count>0);
            var forward=Button(brand,"›",()=>HistoryStep(true));forward.name="history-forward";forward.tooltip="Écran suivant";forward.AddToClassList("history-button");forward.SetEnabled(forwardPages.Count>0);
        }
        void ClubDirectory()
        {
            var panel=Modal("Centre de gestion");panel.AddToClassList("club-directory");var search=new TextField("Rechercher un menu"){name="directory-search"};panel.Add(search);
            var list=Scroll(panel);list.name="directory-results";
            var groups=new[]{
                (title:"ÉQUIPE PREMIÈRE",items:new[]{"Effectif","Tactique","Match","Calendrier","Santé","Staff et délégation"}),
                (title:"CONSTRUIRE LE CLUB",items:new[]{"Recrutement","Formation","Finances","Infrastructures"}),
                (title:"VIE DU MANAGER",items:new[]{"Messages","Presse","Carrière","Coulisses"}),
                (title:"VOTRE PARTIE",items:new[]{"Club","Réglages","Changer de club"})};
            void Entry(VisualElement parent,string id){var button=Button(parent,PageLabel(id),()=>Navigate(id));button.AddToClassList("directory-entry");button.tooltip=DirectoryHint(id);if(id==page)button.AddToClassList("active");}
            void Populate(){list.Clear();string query=search.value.Trim();int count=0;
                if(query.Length==0&&recentPages.Count>0){Text(list,"ACCÈS RÉCENTS","directory-caption");var recent=Row(list,"directory-grid");foreach(var id in recentPages)Entry(recent,id);}
                foreach(var group in groups){var matches=group.items.Where(id=>DirectoryMatches(PageLabel(id),query)||DirectoryMatches(DirectoryHint(id),query)).ToArray();if(matches.Length==0)continue;Text(list,group.title,"directory-caption");var row=Row(list,"directory-grid");foreach(var id in matches){Entry(row,id);count++;}}
                if(count==0)Text(list,"Aucun menu trouvé. Essayez « contrat », « stade » ou « entraînement ».","empty-state");}
            search.RegisterValueChangedCallback(_=>Populate());Populate();Button(panel,"Menu principal",()=>{CloseModal();ReturnToLaunchMenu();}).name="manager-main-menu";AnimateEntry(panel,true);
        }
        static string DirectoryHint(string id)=>id switch{
            "Recrutement"=>"Transferts, prêts, contrats, agents et recruteurs", "Finances"=>"Budget, sponsors, billetterie et naming", "Staff et délégation"=>"Entraînement, adjoint, déléguer les responsabilités", "Santé"=>"Blessures et soins médicaux", "Infrastructures"=>"Stade, installations et projets", "Formation"=>"Centre de formation et jeunes", "Tactique"=>"Composition, onze et consignes", "Calendrier"=>"Compétitions, préparation et amicaux", "Réglages"=>"Graphismes, animations, son et taille", _=>PageLabel(id)};
        void MatchOptions()
        {
            arena.Paused=true;var panel=Modal("Régie du match");panel.name="match-options";panel.AddToClassList("match-options");var settings=Scroll(panel);settings.name="match-settings";settings.AddToClassList("match-settings");Text(settings,"La rencontre est en pause pendant vos réglages.","muted");Text(settings,"Sur le terrain, pincez avec deux doigts pour zoomer. Touchez un joueur pour ouvrir sa fiche.","muted");
            var cameraMode=new DropdownField("Caméra",new List<string>{"Vue TV","Vue tactique"},arena.TacticalCamera?1:0){name="match-camera-choice"};settings.Add(cameraMode);
            cameraMode.RegisterValueChangedCallback(_=>{if((cameraMode.index==1)!=arena.TacticalCamera)arena.CameraMode();arena.ReframeCamera();});
            var camera=Row(settings);Button(camera,"Éloigner",()=>{arena.Zoom(.1f);arena.ReframeCamera();}).name="match-zoom-out";Button(camera,"Rapprocher",()=>{arena.Zoom(-.1f);arena.ReframeCamera();}).name="match-zoom-in";
            var speed=new DropdownField("Vitesse des actions en 3D",new List<string>{"×1 · vitesse naturelle","×2 · accélérée","×3 · rapide","×5 · très rapide","×10 · express"},Math.Max(0,Array.IndexOf(MatchArena.LiveSpeeds,arena.Speed))){name="match-live-speed"};settings.Add(speed);speed.RegisterValueChangedCallback(_=>arena.SetLiveSpeed(MatchArena.LiveSpeeds[speed.index]));
            var names=new DropdownField("Noms sur le terrain",new List<string>{"Désactivés","Autour du ballon · 6 maximum","Tous · masquage des chevauchements"},(int)arena.NameDisplay){name="match-player-names"};settings.Add(names);names.RegisterValueChangedCallback(_=>arena.SetNameDisplay((MatchNameDisplay)names.index));
            var ballLocator=new DropdownField("Repère du ballon",new List<string>{"Désactivé","Discret","Renforcé"},(int)arena.BallDisplay){name="match-ball-locator"};settings.Add(ballLocator);ballLocator.RegisterValueChangedCallback(_=>arena.SetBallDisplay((MatchBallDisplay)ballLocator.index));
            Text(settings,"Seuls les noms de famille sont affichés. Les noms trop proches sont masqués ; touchez le joueur pour ouvrir sa fiche.","muted");
            var overlay=new Toggle("Afficher les repères tactiques"){value=arena.TacticalOverlayVisible,name="match-tactical-overlay"};settings.Add(overlay);overlay.RegisterValueChangedCallback(e=>{if(e.newValue!=arena.TacticalOverlayVisible)arena.ToggleTacticalOverlay();});
            var viewingModes=new[]{MatchViewingMode.KeyMoments,MatchViewingMode.Highlights,MatchViewingMode.Extended,MatchViewingMode.Full};
            var mode=new DropdownField("Visionnage",new List<string>(Array.ConvertAll(viewingModes,MatchBroadcast.ModeName)),Array.IndexOf(viewingModes,arena.Broadcast.Mode)){name="match-viewing-mode"};settings.Add(mode);
            var modeHelp=Text(settings,MatchBroadcast.ModeDescription(arena.Broadcast.Mode),"muted");modeHelp.name="match-viewing-description";
            mode.RegisterValueChangedCallback(_=>{SetBroadcastMode(viewingModes[mode.index]);modeHelp.text=MatchBroadcast.ModeDescription(arena.Broadcast.Mode);});
            bool fullClock=Career.match.HalfDuration==2700;int[] rates=fullClock?new[]{24,60,120}:new[]{8,12,24};var pacing=new DropdownField("Chrono pendant les périodes calmes",new List<string>{"×"+rates[0]+" · plus de temps pour analyser","×"+rates[1]+" · équilibré","×"+rates[2]+" · rapide"},Math.Max(0,Array.IndexOf(rates,arena.Broadcast.QuietSpeed)));settings.Add(pacing);pacing.RegisterValueChangedCallback(e=>{arena.Broadcast.QuietSpeed=rates[pacing.index];PlayerPrefs.SetInt(fullClock?"match-quiet-speed-full":"match-quiet-speed",arena.Broadcast.QuietSpeed);});
            pacing.SetEnabled(arena.Broadcast.Enabled);mode.RegisterValueChangedCallback(_=>pacing.SetEnabled(arena.Broadcast.Enabled));
            Text(settings,"Chaque action reste simulée. Les moments dangereux reviennent automatiquement en 3D. Les fiches et la tactique mettent la rencontre en pause.","muted");
            Button(settings,"Fluidité du match",MatchPerformance);
            Button(settings,"Déléguer la fin",()=>Confirm("Confier la fin à l’adjoint ?","Le résultat sera enregistré dans votre carrière.",()=>DelegateMatch(arena.Simulation)));
            var resume=Button(panel,"Reprendre le match",()=>{CloseModal();if(Career.match.halfTime)arena.Simulation.ResumeHalf();arena.Paused=Career.match.finished;});resume.name="match-options-resume";resume.AddToClassList("primary");
        }
    }
}

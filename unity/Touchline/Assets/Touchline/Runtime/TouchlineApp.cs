using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using Touchline.Core;

namespace Touchline
{
    public sealed partial class TouchlineApp : MonoBehaviour
    {
        public static TouchlineApp Instance {get;private set;}
        public Database Database {get;private set;}
        public Career Career {get;private set;}
        UIDocument document;VisualElement root,content,modal;Label clock,comment;Button playback,matchPending;MatchArena arena;
        string page="Club",opponent="160",clubFilter="",search="";bool attacking,placement;int selectedSlot=9;float refreshAt;Rect lastSafe;int viewportWidth,viewportHeight,interfaceSize=-1;
        bool queuedPreferenceSave;float preferenceSaveAt;
        void QueuePreferenceSave(){queuedPreferenceSave=true;preferenceSaveAt=Time.unscaledTime+.3f;}
        string SavePath=>CareerFilePath;
        void Awake()
        {
            Instance=this;Application.targetFrameRate=60;Screen.sleepTimeout=SleepTimeout.NeverSleep;gameObject.AddComponent<RenderBudget>();
            Database=TouchlineCatalogue.Load();
            Career=new Career();
            document=GetComponent<UIDocument>()??gameObject.AddComponent<UIDocument>();document.panelSettings=Instantiate(Resources.Load<PanelSettings>("TouchlinePanel"));ApplyViewport();root=document.rootVisualElement;
            root.styleSheets.Add(Resources.Load<StyleSheet>("UI/Touchline"));root.AddToClassList("app");root.RegisterCallback<GeometryChangedEvent>(e=>{bool small=!InterfaceViewport.WideLayout(e.newRect.width,e.newRect.height);bool narrow=e.newRect.width<600;bool shortWide=e.newRect.width>=InterfaceViewport.WideLayoutMinWidth&&e.newRect.height<InterfaceViewport.WideLayoutMinHeight;bool changed=small!=compact||narrow!=root.ClassListContains("narrow")||shortWide!=root.ClassListContains("short-wide");compact=small;root.EnableInClassList("narrow",narrow);root.EnableInClassList("short-wide",shortWide);if(changed)root.schedule.Execute(ReflowInterface);});InitializeLaunchExperience();
        }
        void Update()
        {
            if(!launchMenuVisible&&careerSelected&&queuedPreferenceSave&&Time.unscaledTime>=preferenceSaveAt)Save();
            bool resized=ApplyViewport();var safe=Screen.safeArea;
            if(resized||safe!=lastSafe){lastSafe=safe;float scale=1/document.panelSettings.scale;
                if(document.panelSettings.targetTexture!=null)safe=new Rect(0,0,viewportWidth,viewportHeight);
                root.style.paddingLeft=safe.xMin*scale+8;root.style.paddingRight=(viewportWidth-safe.xMax)*scale+8;
                root.style.paddingTop=(viewportHeight-safe.yMax)*scale+6;root.style.paddingBottom=safe.yMin*scale+6;}

            if(Input.GetKeyDown(KeyCode.Escape))LaunchBack();
            if(!launchMenuVisible&&page=="Match"&&arena!=null)RefreshBroadcastVisibility();
            if(!launchMenuVisible&&!delegatingMatch&&page=="Match"&&arena!=null&&Time.unscaledTime-refreshAt>.2f){refreshAt=Time.unscaledTime;var m=Career.match;int medical=Career.life.medical.Count;Career.ProcessMedicalEvents(Database);if(Career.life.medical.Count>medical){arena.Paused=true;Save();if(!m.finished)MatchMedicalAlert(Career.life.medical.Skip(medical).ToArray());}UpdateMatchClock(m);if(playback!=null)playback.text=m.finished?"Retour au club":m.halfTime?"Deuxième mi-temps":arena.Paused?"Reprendre":"Pause";UpdatePendingBanner();if(arena.QuietPresentation)RefreshBroadcastData();comment.text=m.events.LastOrDefault()?.text??"Les équipes se mettent en place.";if(m.finished&&!Career.life.recordedMatch){Career.RecordMatch(Database);Save();}}
        }
        bool ApplyViewport()
        {
            var panel=document.panelSettings;int w=panel.targetTexture!=null?panel.targetTexture.width:Screen.width,h=panel.targetTexture!=null?panel.targetTexture.height:Screen.height;
            int size=PlayerPrefs.GetInt("interface-size",1);if(w==viewportWidth&&h==viewportHeight&&size==interfaceSize)return false;
            viewportWidth=w;viewportHeight=h;interfaceSize=size;InterfaceViewport.Apply(panel,w,h,size);return true;
        }
        void OnApplicationPause(bool pause){PresentationApplicationPause(pause);applicationSuspended=pause;if(pause){if(arena!=null)arena.Paused=true;if(anthem!=null)anthem.Pause();Save();}}
        void OnApplicationQuit(){Save();}
        bool VisualValidation=>Application.isEditor&&Environment.GetCommandLineArgs().Any(a=>a=="-runTests"||a=="Touchline.Editor.MotionContinuityFilm.Run"||a=="Touchline.Editor.ReceptionTransitionReview.Run"||a=="Touchline.Editor.StopStepReview.Run"||a=="Touchline.Editor.LocomotionFacingReview.Run"||a=="Touchline.Editor.PlayerStyleReview.Run"||a=="Touchline.Editor.PlayerEvidenceReview.Run"||a=="Touchline.Editor.KeeperTimelineReview.Run"||a=="Touchline.Editor.GroundedFinishReview.Run"||a=="Touchline.Editor.AcademyWorkspaceSmoke.Run"||a=="Touchline.Editor.PrematchPresentationSmoke.Run"||a=="Touchline.Editor.PrematchProgrammeSmoke.Run"||a=="Touchline.Editor.StaffTrainingWorkspaceSmoke.Run"||a=="Touchline.Editor.MatchFixtureOrderSmoke.Run"||a=="Touchline.Editor.ExitRestartViewportReview.Run"||a=="Touchline.Editor.LaunchMenuSmoke.Run"||a=="Touchline.Editor.LoanNegotiationSmoke.Run"||a=="Touchline.Editor.PlayingTimeWorkspaceSmoke.Run"||a=="Touchline.Editor.SlidingContactReplay.Run"||a=="Touchline.Editor.EditorStaffCatalogSmoke.Run"||a=="Touchline.Editor.GoalReplayUISmoke.Run"||a=="Touchline.Editor.GoalReplaySettingsSmoke.Run"||a=="Touchline.Editor.FullMatchReview.Run"||a=="Touchline.Editor.NavigationPersistenceSmoke.Run"||a=="Touchline.Editor.MatchPlayerLabelsSmoke.Run"||a=="Touchline.Editor.SummerBoundarySmoke.Run"||a=="Touchline.Editor.CalendarWorkspaceSmoke.Run"||a=="Touchline.Editor.MatchDelegationSmoke.Run"||a=="Touchline.Editor.MessageDestinationSmoke.Run"||a=="Touchline.Editor.AcademyDensitySmoke.Run"||a=="Touchline.Editor.StaffDelegationFeedbackSmoke.Run"||a=="Touchline.Editor.ProfileScrollIntegritySmoke.Run"||a=="Touchline.Editor.TacticalBenchSearchSmoke.Run"||a=="Touchline.Editor.InboxWorkspaceSmoke.Run"||a=="Touchline.Editor.RecruitmentDensitySmoke.Run"||a=="Touchline.Editor.PlayerWorkspaceSmoke.Run"||a=="Touchline.Editor.ManagementSmoke.Run"||a=="Touchline.Editor.MatchBroadcastSmoke.Run"||a=="Touchline.Editor.MatchFilmSmoke.Run"||a=="Touchline.Editor.ProfileSmoke.Run"||a=="Touchline.Editor.MatchExperienceSmoke.Run"||a=="Touchline.Editor.NativeSmoke.Run"||a=="Touchline.Editor.MotionCapture.Run"||a=="Touchline.Editor.ProfessionalSmoke.Run"||a=="Touchline.Editor.ErgonomicsSmoke.Run"||a=="Touchline.Editor.TacticsSmoke.Run"||a=="Touchline.Editor.CiUiScreens.Run");
        public void Save()
        {
            if(!careerSelected||launchMenuVisible)return;
            SaveRequestCount++;queuedPreferenceSave=false;lastCareerSaveSucceeded=false;
            if(VisualValidation&&!LaunchMenuValidation){lastCareerSaveSucceeded=true;return;}
            try{
                // Compact format: only what the career changed is written (a few
                // MB instead of 40-90 MB). The live lists are put back right after.
                string json;bool compact=Career.PrepareCompactSave();
                try{json=JsonUtility.ToJson(Career);}finally{if(compact)Career.RestoreAfterSave();}
                LaunchCareerStorage.Write(SavePath,json,validatedPrimary);validatedPrimary=true;primaryValidatedAt=File.GetLastWriteTimeUtc(SavePath);lastCareerSaveSucceeded=true;}
            catch(Exception error){Debug.LogWarning("Échec de sauvegarde : "+error.Message);}
        }
        void Navigate(string destination){if(launchMenuVisible||!careerSelected||delegatingMatch)return;bool hadMatch=Career.match!=null;RecordNavigation(destination);if(arena!=null)arena.Paused=true;if(destination!="Match")CloseFinishedMatch();page=destination;Build();if(hadMatch||queuedPreferenceSave)Save();}
        void Build()
        {
            if(launchMenuVisible){BuildLaunchMenu();return;}
            ObserveContractPersistence();var initializationBefore=InitializationStamp();Career.EnsureLife(Database);RememberPage();
            if(Career.world!=null&&Career.world.managerStatus!="employed"&&!new[]{"Club","Carrière","Messages","Réglages","Changer de club"}.Contains(page))page="Carrière";
            if(page!="Match"&&presentationPending){CancelPresentation();if(anthem!=null)anthem.Stop();}
            BuildShell();
            if(arena!=null)arena.gameObject.SetActive(page=="Match");
            switch(page){case "Staff et délégation":StaffPage();break;case "Calendrier":CalendarPage();break;case "Recrutement":RecruitmentPage();break;case "Formation":AcademyPage();break;case "Finances":FinancePage();break;case "Presse":PressPage();break;case "Carrière":ManagerPage();break;case "Réglages":SettingsPage();break;case "Effectif":Squad();break;case "Tactique":if(Career.life.managerBanUntil>Career.life.day)SuspendedPanel();else Tactics();break;case "Match":Match();break;case "Messages":Messages();break;case "Santé":Medical();break;case "Infrastructures":Facilities();break;case "Coulisses":Integrity();break;case "Changer de club":Club();break;default:Dashboard();break;}
            FinishPage();ShowManagerNotifications();if(initializationBefore!=InitializationStamp())QueuePreferenceSave();
        }
        static VisualElement Row(VisualElement parent,string cls="row"){var row=new VisualElement();row.AddToClassList("row");row.AddToClassList(cls);parent.Add(row);return row;}
        static Button Button(VisualElement parent,string text,Action action){var b=new Button(action){text=text};parent.Add(b);return b;}
        static Label Heading(VisualElement parent,string title){var label=new Label(title);label.AddToClassList("heading");parent.Add(label);return label;}
        static ScrollView Scroll(VisualElement parent){var scroll=new ScrollView(ScrollViewMode.Vertical);scroll.horizontalScrollerVisibility=ScrollerVisibility.Hidden;scroll.contentContainer.style.width=Length.Percent(100);scroll.contentContainer.style.minWidth=0;scroll.AddToClassList("scroll");parent.Add(scroll);return scroll;}
        ClubData Own=>Array.Find(Database.clubs,c=>c.id==Career.club);
        void Club()
        {
            var body=Scroll(content);var hero=Row(body,"hero");var logo=Resources.Load<Texture2D>("Logos/club-"+Career.club);if(logo!=null){var image=new Image{image=logo,scaleMode=ScaleMode.ScaleToFit};image.AddToClassList("club-logo");hero.Add(image);}var copy=Row(hero,"column");Heading(copy,Own.name);copy.Add(new Label("Bienvenue, "+Career.manager+"."));copy.Add(new Label("Saison 2026–2027 · laboratoire de match natif"));
            var actions=Row(body);Button(actions,"Préparer le onze",()=>Navigate("Tactique"));Button(actions,Career.match==null?"Préparer une rencontre":"Revenir au match",()=>Navigate("Match"));
            var info=new Label("Changer de club démarre une nouvelle préparation Unity. Le bureau quotidien, le suivi médical, les relations et les infrastructures sont disponibles. Le calendrier importé, le recrutement, les finances et la progression des saisons sont disponibles.");info.AddToClassList("notice");body.Add(info);
            Heading(body,"Créer une nouvelle carrière");EnsureNewCareerDraft();var scouting=new Toggle("Afficher tous les attributs sans observation"){value=newCareerRevealDraft,name="new-career-reveal-attributes"};body.Add(scouting);scouting.RegisterValueChangedCallback(e=>newCareerRevealDraft=e.newValue);var field=new TextField("Recherche"){value=clubFilter};body.Add(field);var list=Row(body,"column");
            Action populate=()=>{list.Clear();foreach(var club in Database.clubs.Where(c=>c.playable&&c.name.IndexOf(clubFilter,StringComparison.OrdinalIgnoreCase)>=0).Take(35)){var row=Row(list,"list-row");row.Add(new Label(club.name+"  ·  "+club.league));Button(row,"Choisir",()=>Confirm("Prendre les commandes de "+club.name+" ?","Une nouvelle carrière sera créée. Votre progression actuelle sera remplacée. Pour poursuivre votre carrière dans un autre club, utilisez les offres de l’onglet Carrière.",()=>{if(arena!=null)Destroy(arena.gameObject);arena=null;Database=TouchlineCatalogue.Load();Career=new Career{club=club.id,manager=Career.manager,revealAttributes=newCareerRevealDraft,youthGenerationFromYear=Core.Career.ImportedRosterYear+1};Career.lineup=Core.Career.Select(Database,club.id,Career.tactic);Career.EnsureWorld(Database);opponent=Database.clubs.First(c=>c.league==club.league&&c.id!=club.id&&c.playable).id;Save();Build();}));}};
            field.RegisterValueChangedCallback(e=>{clubFilter=e.newValue;populate();});populate();
        }
        string[] Lineup=>Career.match==null?Career.lineup:Career.match.actors.Where(a=>a.side==0).OrderBy(a=>a.slot).Select(a=>a.id).ToArray();
        static void Position(VisualElement token,Slot slot){token.style.left=Length.Percent(slot.x);token.style.top=Length.Percent(100-slot.y);token.style.translate=new Translate(Length.Percent(-50),Length.Percent(-50));}
        static void PitchLines(VisualElement board)
        {
            foreach(var spec in new[]{new[]{0f,50f,100f,.2f},new[]{21f,0f,58f,16f},new[]{21f,84f,58f,16f},new[]{39f,42f,22f,16f}}){var line=new VisualElement();line.pickingMode=PickingMode.Ignore;line.style.position=UnityEngine.UIElements.Position.Absolute;line.style.left=Length.Percent(spec[0]);line.style.top=Length.Percent(spec[1]);line.style.width=Length.Percent(spec[2]);line.style.height=Length.Percent(spec[3]);line.style.borderBottomWidth=line.style.borderTopWidth=line.style.borderLeftWidth=line.style.borderRightWidth=1;var color=new Color(.6f,.75f,.6f,.5f);line.style.borderBottomColor=line.style.borderTopColor=line.style.borderLeftColor=line.style.borderRightColor=color;if(spec[0]==39){line.style.borderBottomLeftRadius=line.style.borderBottomRightRadius=line.style.borderTopLeftRadius=line.style.borderTopRightRadius=50;}board.Add(line);}
        }
        void AddSlider(VisualElement panel,string label,float value,Action<float> apply){var slider=new Slider(label,0,1){value=value};panel.Add(slider);slider.RegisterValueChangedCallback(e=>{apply(e.newValue);QueuePreferenceSave();});}
        void AddToggle(VisualElement panel,string label,bool value,Action<bool> apply){var toggle=new Toggle(label){value=value};panel.Add(toggle);toggle.RegisterValueChangedCallback(e=>{apply(e.newValue);Save();});}
        void Swap(int a,int b)=>Career.AssignTacticalPlayer(Database,a,Lineup[b]);
        void ChoosePlayer(int index){selectedSlot=index;tacticalTab="Composition";Navigate("Tactique");}
        void Match()
        {
            if(Career.world!=null&&Career.match==null){ProfessionalMatchLobby();return;}
            if(Career.match==null){Heading(content,"Rencontre de préparation");content.Add(new Label("Rendez-vous le "+Touchline.Core.Career.Epoch.AddDays(Career.life.nextFixture).ToString("dd MMMM",French)+". Programme de matchs amicaux."));var candidates=Database.clubs.Where(c=>c.id!=Career.club&&c.playable&&c.league==Own.league).ToList();if(candidates.Count==0)candidates=Database.clubs.Where(c=>c.id!=Career.club&&c.playable).Take(30).ToList();if(!candidates.Any(c=>c.id==opponent))opponent=candidates[0].id;
                var picker=new DropdownField("Adversaire",candidates.Select(c=>c.name).ToList(),candidates.FindIndex(c=>c.id==opponent));content.Add(picker);picker.RegisterValueChangedCallback(e=>opponent=candidates.First(c=>c.name==e.newValue).id);Button(content,"Entrer sur le terrain",()=>RunDecision(()=>StartCareerMatch(false)));Button(content,"Déléguer à l’adjoint",()=>RunDecision(()=>StartCareerMatch(true)));return;}
            if(arena==null)CreateArena(new MatchSimulation(Database,Career.match));arena.gameObject.SetActive(true);content.AddToClassList("match-overlay");content.pickingMode=PickingMode.Ignore;
            BuildMatchScoreboard();
            var space=new Image{name="match-viewport",scaleMode=ScaleMode.StretchToFill};space.AddToClassList("match-viewport");content.Add(space);arena.BindViewport(space);BuildBroadcastUI(space);comment=new Label();comment.AddToClassList("commentary");content.Add(comment);
            matchPending=Button(content,"",()=>Navigate("Tactique"));matchPending.name="match-pending-substitutions";matchPending.AddToClassList("pending-match-banner");UpdatePendingBanner();
            MatchMentalityShortcut();
            var controls=Row(content,"match-controls");playback=Button(controls,"Reprendre",()=>{if(Career.match.finished){Navigate("Club");return;}if(Career.match.halfTime){arena.Simulation.ResumeHalf();arena.Paused=false;}else arena.Paused=!arena.Paused;});playback.name="match-playback";playback.AddToClassList("primary");Button(controls,"Consignes",MatchInstructionPanel).name="match-instructions-open";Button(controls,"Banc",MatchBench).name="match-bench-open";Button(controls,"Analyse",MatchAnalysis);Button(controls,"Options du match",MatchOptions);Button(controls,"Bureau",()=>Navigate("Club"));
            if(Career.match.clock==0){var intro=Button(controls,"Avant-match",()=>{presentationPending=true;ShowPresentation();});intro.name="prematch-replay";}
            if(presentationPending)ShowPresentation();
        }
        // Mentalité en une touche, sans quitter le direct ni mettre la rencontre en pause.
        void MatchMentalityShortcut()
        {
            var row=Row(content,"match-mentality");row.name="match-mentality";Text(row,"Mentalité","match-mentality-label");var buttons=new Button[PhoneShortcuts.MentalityLabels.Length];
            void Mark(){int current=PhoneShortcuts.NearestMentality(Career.tactic.mentality);for(int i=0;i<buttons.Length;i++)buttons[i].EnableInClassList("active",i==current);}
            for(int i=0;i<buttons.Length;i++){int level=i;buttons[i]=Button(row,PhoneShortcuts.MentalityLabels[i],()=>{Career.tactic.mentality=PhoneShortcuts.MentalityLevels[level];QueuePreferenceSave();Mark();});buttons[i].name="match-mentality-"+i;}
            Mark();
        }
        void UpdatePendingBanner(){if(matchPending==null)return;int count=Career.match?.pendingSubstitutions?.Count(p=>p.side==0)??0;matchPending.style.display=count>0?DisplayStyle.Flex:DisplayStyle.None;matchPending.text=count+" changement(s) préparé(s) · prochain arrêt de jeu · Modifier";}
        void CreateArena(MatchSimulation sim){matchAnalysisTab="Statistiques";allMatchEvents=false;if(arena!=null)Destroy(arena.gameObject);arena=new GameObject("Native Unity match").AddComponent<MatchArena>();arena.Initialize(Database,sim);arena.Broadcast.SetMode(MatchBroadcast.ResolveMode(PlayerPrefs.GetInt("match-view-mode",-1),PlayerPrefs.GetInt("match-highlights",1)));arena.Broadcast.QuietSpeed=PlayerPrefs.GetInt(sim.State.HalfDuration==2700?"match-quiet-speed-full":"match-quiet-speed",sim.State.HalfDuration==2700?60:12);arena.PlayerSelected=PlayerProfile;arena.SaveRequested=Save;arena.gameObject.SetActive(page=="Match");}
        public bool PointerOverInterface(Vector2 screenPoint){var element=root.panel.Pick(RuntimePanelUtils.ScreenToPanel(root.panel,new Vector2(screenPoint.x,Screen.height-screenPoint.y)));return element!=null&&element!=root&&element!=content;}
        VisualElement Modal(string title){CloseModal();modal=new VisualElement();modal.AddToClassList("modal-backdrop");root.Add(modal);if(!launchMenuVisible&&Career?.life!=null)ShowManagerNotifications();var panel=Row(modal,"modal-panel");var top=Row(panel);Heading(top,title);Button(top,"Fermer",CloseModal);AnimateEntry(panel,true);return panel;}
        void CloseModal(){if(delegatingMatch)return;if(modal?.name=="prematch-presentation")CancelPresentation();modal?.RemoveFromHierarchy();modal=null;if(anthem!=null)anthem.Stop();if(!launchMenuVisible&&root!=null&&Career?.life!=null)ShowManagerNotifications();}
        void Confirm(string title,string text,Action apply){var panel=Modal(title);panel.Add(new Label(text));Button(panel,"Confirmer",()=>{CloseModal();apply();});}
        void Message(string text){Modal("Information").Add(new Label(text));}
    }
}







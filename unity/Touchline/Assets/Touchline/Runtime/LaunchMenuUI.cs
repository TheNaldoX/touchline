using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Touchline.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Touchline
{
    public sealed partial class TouchlineApp
    {
        bool launchMenuVisible=true,careerSelected,validatedPrimary,lastCareerSaveSucceeded,launchBusy;
        string launchTab="Accueil",launchResumePage="Club",launchDraftManager="Victor",launchDraftClub="176",launchClubQuery="",launchLeague="Toutes";
        bool launchDraftReveal;
        Database launchCatalogue;DateTime primaryValidatedAt;
        LaunchCareerEntry[] launchEntries=Array.Empty<LaunchCareerEntry>();
        public bool LaunchMenuVisible=>launchMenuVisible;
        public bool HasSelectedCareer=>careerSelected;
        bool LaunchMenuValidation=>Application.isEditor&&Environment.GetCommandLineArgs().Any(a=>a=="Touchline.Editor.LaunchMenuSmoke.Run");
        string CareerFilePath {
            get {
#if UNITY_EDITOR
                if(LaunchMenuValidation)return Path.Combine(UnityEditor.SessionState.GetString("TouchlineLaunchSaveRoot",""),"touchline-unity-career.json");
#endif
                return Path.Combine(Application.persistentDataPath,"touchline-unity-career.json");
            }
        }
        void RefreshLaunchSaves()
        {
            if(VisualValidation&&!LaunchMenuValidation){launchEntries=Array.Empty<LaunchCareerEntry>();return;}
            launchCatalogue??=Database;
            launchEntries=new[]{LaunchCareerStorage.ReadMetadata(SavePath,"Sauvegarde principale",launchCatalogue),LaunchCareerStorage.ReadMetadata(SavePath+".backup","Sauvegarde de secours",launchCatalogue)};
            if(validatedPrimary&&launchEntries[0].WrittenUtc!=primaryValidatedAt)validatedPrimary=false;
        }
        void InitializeLaunchExperience()
        {
            launchCatalogue=Database;
            if(VisualValidation&&!LaunchMenuValidation){
                // Existing scripted workspaces deliberately begin at the bureau.
                Career=new Career();careerSelected=true;launchMenuVisible=false;
                Career.lineup=Core.Career.Select(Database,Career.club,Career.tactic);Career.EnsureLife(Database);Build();Save();return;
            }
            Career=new Career();RefreshLaunchSaves();BuildLaunchMenu();
        }
        void BuildLaunchMenu()
        {
            root.Clear();modal=null;content=null;
            root.EnableInClassList("live-match",false);root.EnableInClassList("manager-shell",false);
            root.EnableInClassList("compact",compact);root.EnableInClassList("reduce-motion",!MotionEnabled);
            if(arena!=null){arena.Paused=true;arena.gameObject.SetActive(false);}
            var shell=new VisualElement{name="launch-menu"};shell.AddToClassList("launch-menu");root.Add(shell);
            var header=Row(shell,"launch-header");
            var brandTexture=Resources.Load<Texture2D>("UI/Branding/touchline-logo-v1");
            if(brandTexture!=null){var brand=new Image{image=brandTexture,scaleMode=ScaleMode.ScaleToFit,uv=new Rect(.055f,.22f,.89f,.56f),name="launch-brand-image",tooltip="Touchline"};brand.AddToClassList("launch-brand-image");header.Add(brand);}
            else Text(header,"T /  TOUCHLINE","launch-brand");
            Text(header,"VOTRE HISTOIRE COMMENCE ICI","launch-kicker");
            if(launchBusy){var loading=Card(shell,"launch-loading");Heading(loading,"Préparer votre carrière");Text(loading,"Initialisation du club, des effectifs et du calendrier…","muted");return;}
            if(launchTab!="Accueil"){
                var top=Row(shell,"launch-page-heading");Button(top,"‹  Menu principal",()=>{launchTab="Accueil";BuildLaunchMenu();}).name="launch-back";
                Text(top,launchTab,"section-title");content=new VisualElement{name="launch-page-content"};content.AddToClassList("launch-page-content");shell.Add(content);
                if(launchTab=="Réglages")SettingsControls(Scroll(content),true);
                else if(launchTab=="Sauvegardes")LaunchSavesPage(content);
                else LaunchNewCareerPage(content);
            }else{
                var homeScroll=Scroll(shell);homeScroll.AddToClassList("launch-home-scroll");var body=Row(homeScroll,"launch-home");var hero=new VisualElement();hero.AddToClassList("launch-hero");body.Add(hero);
                Text(hero,"ENTREZ DANS LE JEU","eyebrow");Text(hero,"Chaque choix\ncompte.","launch-title");
                Text(hero,"Construisez une équipe. Trouvez votre style. Écrivez la suite.","launch-subtitle");
                var pitch=new VisualElement{pickingMode=PickingMode.Ignore};pitch.AddToClassList("launch-pitch");PitchLines(pitch);hero.Add(pitch);
                var actions=new VisualElement();actions.AddToClassList("launch-actions");body.Add(actions);
                var resume=careerSelected?Career:null;
                var resumeEntry=launchEntries.FirstOrDefault(e=>e.CanAttempt);
                string resumeClub=resume?.club??resumeEntry?.Club;
                if(resumeClub!=null){
                    var card=Card(actions,"launch-resume-card");var club=Database.clubs.FirstOrDefault(c=>c.id==resumeClub);var row=Row(card,"launch-resume-heading");
                    var crest=Resources.Load<Texture2D>("Logos/club-"+resumeClub);if(crest!=null){var image=new Image{image=crest,scaleMode=ScaleMode.ScaleToFit};image.AddToClassList("launch-crest");row.Add(image);}
                    var copy=new VisualElement();copy.style.flexGrow=1;row.Add(copy);Text(copy,club?.name??resumeClub,"section-title");
                    Text(copy,(resume?.manager??resumeEntry?.Manager??"Manager")+" · "+(resume?.Date??Core.Career.Epoch.AddDays(resumeEntry.Day)).ToString("dd MMMM yyyy",French),"muted");Text(card,resume!=null?LaunchMatchSummary(resume):LaunchEntryMatchSummary(resumeEntry),"launch-resume-status");
                    if(!careerSelected&&resumeEntry?.Label=="Sauvegarde de secours")Text(card,"La principale est indisponible. Votre secours peut être récupéré.","status-warning");
                    var play=Button(card,(resume?.match!=null&&!resume.match.finished)||(resume==null&&resumeEntry.HasMatch&&!resumeEntry.MatchFinished)?"Reprendre le match  →":"Reprendre la carrière  →",()=>{if(careerSelected)ResumeLaunchSession();else LoadLaunchCareer(resumeEntry);});play.name="launch-resume";play.AddToClassList("primary");
                }else Text(Card(actions,"launch-empty"),launchEntries.Any(e=>e.Exists)?"Aucune sauvegarde lisible. Les fichiers existants sont conservés ; consultez Sauvegardes avant de créer une carrière.":"Le banc vous attend. Choisissez votre club pour démarrer une carrière.","muted");
                LaunchAction(actions,"Nouvelle carrière","Choisir un club et votre expérience de recrutement","launch-new",()=>{launchTab="Nouvelle carrière";BuildLaunchMenu();});
                LaunchAction(actions,"Sauvegardes","Principale et secours · consulter ou récupérer","launch-saves",()=>{RefreshLaunchSaves();launchTab="Sauvegardes";BuildLaunchMenu();});
                LaunchAction(actions,"Réglages et accessibilité","Interface, graphismes, animations et son","launch-settings",()=>{launchTab="Réglages";BuildLaunchMenu();});
            }
            var footer=Row(shell,"launch-footer");Text(footer,"HORS CONNEXION · ANDROID","eyebrow");Text(footer,"Touchline "+Application.version,"muted");Button(footer,"Quitter",ConfirmLaunchQuit).name="launch-quit";
            AnimateEntry(shell);
        }
        void LaunchAction(VisualElement parent,string title,string subtitle,string name,Action action)
        {
            var button=Button(parent,"",action);button.name=name;button.AddToClassList("launch-action");
            var titleLabel=Text(button,title,"launch-action-title");titleLabel.pickingMode=PickingMode.Ignore;
            var detail=Text(button,subtitle,"launch-action-detail");detail.pickingMode=PickingMode.Ignore;
        }
        string LaunchMatchSummary(Career state)
        {
            if(state.match==null)return "La carrière vous attend au bureau.";
            var m=state.match;var away=Database.clubs.FirstOrDefault(c=>c.id==m.away)?.name??m.away;
            return (m.finished?"Match terminé":m.halfTime?"Mi-temps · rencontre en pause":"Match en pause")+" · "+away+" · "+m.Minute+"′ · "+(m.score?.Length==2?m.score[0]+"–"+m.score[1]:"");
        }
        string LaunchEntryMatchSummary(LaunchCareerEntry entry)
        {
            if(!entry.HasMatch)return "La carrière vous attend au bureau.";
            return (entry.MatchFinished?"Match terminé":entry.MatchHalfTime?"Mi-temps · rencontre en pause":"Match en pause")+" · "+ClubName(entry.MatchAway)+" · "+entry.MatchMinute+"′ · "+(entry.Score?.Length==2?entry.Score[0]+"–"+entry.Score[1]:"");
        }
        void LaunchSavesPage(VisualElement parent)
        {
            var body=Scroll(parent);Text(body,"Deux fichiers de votre carrière sont conservés. Le secours contient la version précédente, pas un emplacement de partie supplémentaire.","muted");
            foreach(var entry in launchEntries){var card=Card(body,"launch-save-card");Text(card,entry.Label,"section-title");
                if(!entry.Exists){Text(card,"Aucun fichier pour le moment.","muted");continue;}
                Text(card,"Enregistrée le "+entry.WrittenUtc.ToLocalTime().ToString("dd MMM yyyy · HH:mm",French),"muted");
                if(!entry.CanAttempt){Text(card,entry.Error,"status-warning");continue;}
                Text(card,ClubName(entry.Club)+" · "+entry.Manager+" · "+Core.Career.Epoch.AddDays(entry.Day).ToString("dd MMM yyyy",French));Text(card,LaunchEntryMatchSummary(entry),"muted");Text(card,"Les données de carrière seront vérifiées au chargement.","muted");
                Button(card,entry.Label=="Sauvegarde principale"?"Charger la principale":"Récupérer le secours",()=>{
                    if(careerSelected)Confirm("Charger cette sauvegarde ?","La version actuellement en mémoire sera remplacée par celle du "+Core.Career.Epoch.AddDays(entry.Day).ToString("dd MMM yyyy",French)+". Les fichiers restent conservés jusqu’à votre prochaine sauvegarde.",()=>LoadLaunchCareer(entry));
                    else LoadLaunchCareer(entry);
                }).name=entry.Label=="Sauvegarde principale"?"launch-load-primary":"launch-load-backup";
            }
            Button(body,"Actualiser la liste",()=>{RefreshLaunchSaves();BuildLaunchMenu();}).name="launch-refresh-saves";
        }
        void LaunchNewCareerPage(VisualElement parent)
        {
            var body=Scroll(parent);var draft=Card(body,"launch-draft");
            var manager=new TextField("Votre nom"){maxLength=64,value=launchDraftManager,name="launch-manager-name"};draft.Add(manager);manager.RegisterValueChangedCallback(e=>launchDraftManager=e.newValue);
            Text(draft,"Visibilité des notes et attributs","section-title");
            var reveal=new Toggle("Tout voir sans observation"){value=launchDraftReveal,name="launch-reveal-attributes"};draft.Add(reveal);
            string RevealDescription()=>launchDraftReveal?"Attributs visibles dès le départ pour tous les joueurs, sans envoyer de recruteur. Niveau et potentiel sont affichés en étoiles, par rapport à votre club.":"Observation nécessaire : les attributs des autres clubs sont masqués, puis estimés par vos recruteurs. Votre effectif est connu ; le niveau et le potentiel restent évalués en étoiles.";
            var revealDescription=Text(draft,RevealDescription(),"muted");revealDescription.name="launch-reveal-description";
            reveal.RegisterValueChangedCallback(e=>{launchDraftReveal=e.newValue;revealDescription.text=RevealDescription();});
            Text(draft,"Ce choix est enregistré pour cette carrière.","footnote");
            var selected=Database.clubs.FirstOrDefault(c=>c.id==launchDraftClub&&c.playable);if(selected==null)selected=Database.clubs.First(c=>c.playable);launchDraftClub=selected.id;
            Text(draft,"Club choisi : "+selected.name,"section-title").name="launch-selected-club";
            var begin=Button(draft,"Démarrer la carrière  →",ConfirmLaunchCareer);begin.name="launch-create-career";begin.AddToClassList("primary");
            var search=new TextField("Rechercher un club"){value=launchClubQuery,name="launch-club-search"};body.Add(search);
            var leagues=Database.leagues.Where(l=>Database.clubs.Any(c=>c.playable&&c.league==l.id)).OrderBy(l=>l.country).ThenBy(l=>l.tier).ToArray();
            var choices=new List<string>{"Toutes"};choices.AddRange(leagues.Select(l=>l.name+" · "+l.id));
            var league=new DropdownField("Championnat",choices,Math.Max(0,choices.IndexOf(launchLeague))){name="launch-league"};body.Add(league);
            var count=Text(body,"","muted");var list=new VisualElement{name="launch-club-list"};body.Add(list);
            void Populate(){list.Clear();string leagueId=league.index==0?null:leagues[league.index-1].id;
                var matches=Database.clubs.Where(c=>c.playable&&(leagueId==null||c.league==leagueId)&&DirectoryMatches(c.name,launchClubQuery)).OrderBy(c=>c.name).ToArray();
                count.text=matches.Length+" clubs · "+(matches.Length>30?"30 affichés, précisez votre recherche":"sélectionnez votre club");
                foreach(var club in matches.Take(30)){var b=Button(list,club.name+"\n"+(Database.leagues.FirstOrDefault(l=>l.id==club.league)?.name??club.league),()=>{launchDraftClub=club.id;root.Q<Label>("launch-selected-club").text="Club choisi : "+club.name;Populate();});b.name="launch-club-"+club.id;b.AddToClassList("launch-club-choice");if(club.id==launchDraftClub)b.AddToClassList("active");}
                if(matches.Length==0)Text(list,"Aucun club trouvé. Modifiez le championnat ou la recherche.","empty-state");}
            search.RegisterValueChangedCallback(e=>{launchClubQuery=e.newValue;Populate();});league.RegisterValueChangedCallback(_=>{launchLeague=league.value;Populate();});Populate();
        }
        void ConfirmLaunchCareer()
        {
            if(string.IsNullOrWhiteSpace(launchDraftManager)){Message("Indiquez le nom de votre manager.");return;}
            var club=Database.clubs.First(c=>c.id==launchDraftClub);
            string existing=careerSelected||launchEntries.Any(e=>e.Exists)?" La carrière principale sera remplacée uniquement après cette confirmation ; sa dernière version lisible restera dans le secours.":"";
            var panel=Modal("Démarrer avec "+club.name+" ?");panel.name="launch-new-confirmation";
            Text(panel,launchDraftManager.Trim()+" · "+(launchDraftReveal?"Attributs connus":"Observations progressives")+" · Préparation estivale 2026–2027."+existing);
            Button(panel,"Annuler",CloseModal).name="launch-new-cancel";
            var confirm=Button(panel,"Créer cette carrière",()=>{CloseModal();launchBusy=true;BuildLaunchMenu();root.schedule.Execute(CreateLaunchCareer).StartingIn(60);});confirm.name="launch-new-confirm";confirm.AddToClassList("primary");
        }
        void CreateLaunchCareer()
        {
            var oldCareer=Career;var oldDatabase=Database;bool oldSelected=careerSelected;
            try{
                if(!validatedPrimary&&File.Exists(SavePath))ValidatePrimaryBeforeReplacement();
                var db=TouchlineCatalogue.Load();
                var state=new Career{club=launchDraftClub,manager=launchDraftManager.Trim(),revealAttributes=launchDraftReveal,saveBaseline=SaveBaseline.From(db)};state.lineup=Core.Career.Select(db,state.club,state.tactic);state.EnsureLife(db);if(!VisualValidation)state.EnsureWorld(db);
                Database=db;Career=state;historyClub=null;careerSelected=true;launchMenuVisible=false;Save();
                if(!lastCareerSaveSucceeded)throw new IOException("La nouvelle carrière n’a pas pu être enregistrée.");
                if(arena!=null)Destroy(arena.gameObject);arena=null;launchCatalogue=null;launchBusy=false;page="Club";launchTab="Accueil";Build();
            }catch(Exception error){Career=oldCareer;Database=oldDatabase;careerSelected=oldSelected;launchMenuVisible=true;launchBusy=false;BuildLaunchMenu();Message(error.Message);}
        }
        void ValidatePrimaryBeforeReplacement()
        {
            var original=launchCatalogue??TouchlineCatalogue.Load();
            var prior=LaunchCareerStorage.Read(SavePath,"Sauvegarde principale",original);validatedPrimary=prior.Valid;primaryValidatedAt=prior.WrittenUtc;
            // prior's full state/world is not cached or retained by the menu.
        }
        void LoadLaunchCareer(LaunchCareerEntry selected)
        {
            if(selected==null)return;
            // Read again at the explicit choice, so stale menu previews are never trusted.
            var pristine=TouchlineCatalogue.Load();
            var entry=LaunchCareerStorage.Read(selected.Path,selected.Label,pristine);
            if(!entry.Valid){RefreshLaunchSaves();BuildLaunchMenu();Message(entry.Error??"La sauvegarde n’est plus disponible.");return;}
            entry.State.saveBaseline=SaveBaseline.From(pristine);
            var oldCareer=Career;var oldDatabase=Database;bool oldSelected=careerSelected;string oldPage=page;
            try{
                if(selected.Path==SavePath){validatedPrimary=true;primaryValidatedAt=entry.WrittenUtc;}
                entry.State.BindMatchTactic();entry.State.EnsureLife(entry.Restored);if(!VisualValidation)entry.State.EnsureWorld(entry.Restored);
                if(arena!=null)Destroy(arena.gameObject);arena=null;Career=entry.State;Database=entry.Restored;historyClub=null;careerSelected=true;launchMenuVisible=false;launchCatalogue=null;
                page=Career.match!=null?"Match":"Club";launchResumePage=page;Build();if(Career.match!=null){arena.Paused=true;}
                // Loading itself does not rotate files. Normal play / an explicit save commits repairs.
            }catch(Exception error){if(arena!=null)Destroy(arena.gameObject);arena=null;Career=oldCareer;Database=oldDatabase;careerSelected=oldSelected;page=oldPage;launchMenuVisible=true;launchCatalogue=null;BuildLaunchMenu();Message("Cette carrière n’a pas pu être initialisée : "+error.Message);}
        }
        void ResumeLaunchSession(){launchMenuVisible=false;page=launchResumePage;Build();if(arena!=null)arena.Paused=true;}
        void ReturnToLaunchMenu()
        {
            if(delegatingMatch||launchBusy)return;
            if(!careerSelected){launchMenuVisible=true;launchTab="Accueil";BuildLaunchMenu();return;}
            if(arena!=null)arena.Paused=true;if(anthem!=null)anthem.Stop();Save();
            if(!lastCareerSaveSucceeded){Message("La sauvegarde a échoué. Votre partie reste ouverte ; réessayez avant de revenir au menu.");return;}
            launchResumePage=page;launchMenuVisible=true;launchTab="Accueil";launchCatalogue=null;RefreshLaunchSaves();BuildLaunchMenu();
        }
        void ConfirmLaunchQuit()
        {
            var panel=Modal("Quitter Touchline ?");panel.name="launch-quit-confirmation";
            Text(panel,"Votre dernière sauvegarde est conservée. Vous pourrez reprendre votre carrière au prochain lancement.");
            Button(panel,"Rester",CloseModal).name="launch-quit-cancel";
            Button(panel,"Quitter le jeu",()=>{CloseModal();if(!Application.isEditor)Application.Quit();}).name="launch-quit-confirm";
        }
        void LaunchBack()
        {
            if(launchBusy)return;
            if(modal!=null){CloseModal();return;}
            if(launchMenuVisible){if(launchTab!="Accueil"){launchTab="Accueil";BuildLaunchMenu();}else ConfirmLaunchQuit();return;}
            if(backPages.Count>0)HistoryStep(false);else Confirm("Revenir au menu principal ?","La rencontre sera mise en pause et votre carrière enregistrée.",ReturnToLaunchMenu);
        }
    }
}

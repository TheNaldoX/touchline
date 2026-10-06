using System;
using System.Linq;
using Touchline.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Touchline
{
    public sealed partial class TouchlineApp
    {
        static readonly string[] IntroChapters={"L’affiche","L’enjeu","Onze à domicile","Onze à l’extérieur","Le coup d’envoi"};
        static readonly float[] IntroDurations={6,9,10,10,0};
        string introFixture,introSelectedPlayer;
        bool introWithBall;
        VisualElement introPlayerDetails;
        int introChapter;
        float introElapsed,introLastTick;
        bool introAutomatic=true,introMuted,introAudioSuspended;
        VisualElement introPanel,introBody,introProgress;
        Label introCounter;
        Button introPrevious,introNext,introAuto;
        IVisualElementScheduledItem introTimer;
        PrematchBriefing introBriefing;

        void ShowPresentation()
        {
            var f=Career.world?.fixtures.FirstOrDefault(x=>x.id==Career.world.activeFixture);
            if(f==null||Career.match==null||Career.match.clock>0){CancelPresentation();return;}
            arena.Paused=true;
            introTimer?.Pause();
            bool fresh=introFixture!=f.id;
            if(fresh){introFixture=f.id;introSelectedPlayer=null;introWithBall=false;introChapter=0;introElapsed=0;introAutomatic=true;introMuted=false;introAudioSuspended=false;}
            introBriefing=PrematchBriefing.Create(Career,Database,f);
            modal=new VisualElement{name="prematch-presentation"};modal.AddToClassList("modal-backdrop");modal.AddToClassList("prematch-backdrop");root.Add(modal);
            introPanel=new VisualElement{name="prematch-panel"};introPanel.AddToClassList("prematch-panel");modal.Add(introPanel);
            var top=Row(introPanel,"prematch-top");
            var branding=Row(top,"prematch-competition");var logo=Resources.Load<Texture2D>("Logos/"+f.league);
            if(logo!=null){var image=new Image{image=logo,scaleMode=ScaleMode.ScaleToFit};image.AddToClassList("prematch-competition-logo");branding.Add(image);}
            Text(branding,Career.CompetitionName(Database,f.league).ToUpper(French),"eyebrow");
            introCounter=Text(top,"","prematch-counter");
            var rail=new VisualElement();rail.AddToClassList("prematch-rail");introPanel.Add(rail);introProgress=new VisualElement();introProgress.AddToClassList("prematch-progress");rail.Add(introProgress);
            var scroll=Scroll(introPanel);scroll.name="prematch-scroll";introBody=new VisualElement();introBody.AddToClassList("prematch-body");scroll.Add(introBody);
            scroll.RegisterCallback<PointerDownEvent>(_=>PauseIntroForExploration(),TrickleDown.TrickleDown);
            scroll.RegisterCallback<WheelEvent>(_=>PauseIntroForExploration(),TrickleDown.TrickleDown);
            var controls=Row(introPanel,"prematch-controls");
            introPrevious=Button(controls,"← Retour",()=>IntroGo(introChapter-1));introPrevious.name="prematch-previous";
            introAuto=Button(controls,"",()=>{introAutomatic=!introAutomatic;UpdateIntroControls();});introAuto.name="prematch-auto";
            introNext=Button(controls,"Suivant →",()=>IntroGo(introChapter+1));introNext.name="prematch-next";
            Button(controls,"Coup d’envoi",StartPresentedMatch).name="prematch-kickoff";controls.Q<Button>("prematch-kickoff").AddToClassList("primary");
            var footer=Row(introPanel,"prematch-footer");Text(footer,"Vous pouvez passer la présentation. Le match attend votre signal.","muted");
            var clip=Resources.Load<AudioClip>("Audio/"+f.league);
            if(clip!=null){
                anthem??=gameObject.AddComponent<AudioSource>();anthem.playOnAwake=false;anthem.loop=false;anthem.spatialBlend=0;
                if(fresh||anthem.clip!=clip){anthem.clip=clip;anthem.volume=PlayerPrefs.GetFloat("music-volume",.55f);anthem.mute=introMuted;if(!applicationSuspended)anthem.Play();else introAudioSuspended=true;}
                var audioButton=Button(top,introMuted?"Activer l’hymne":"Couper l’hymne",()=>{introMuted=!introMuted;anthem.mute=introMuted;top.Q<Button>("prematch-audio").text=introMuted?"Activer l’hymne":"Couper l’hymne";});audioButton.name="prematch-audio";
            }
            RenderIntroChapter();introLastTick=Time.realtimeSinceStartup;
            introTimer=introPanel.schedule.Execute(TickPresentation).Every(33);
            ShowManagerNotifications();
        }
        void PauseIntroForExploration()
        {
            if(!introAutomatic)return;introAutomatic=false;UpdateIntroControls();
        }
        void TickPresentation()
        {
            float now=Time.realtimeSinceStartup,delta=Mathf.Clamp(now-introLastTick,0,.15f);introLastTick=now;
            if(!presentationPending||modal?.name!="prematch-presentation"||applicationSuspended||page!="Match")return;
            arena.Paused=true;
            if(!introAutomatic||introChapter==4)return;
            introElapsed+=delta;
            introProgress.style.width=Length.Percent(100*(introChapter+Mathf.Clamp01(introElapsed/IntroDurations[introChapter]))/5);
            if(introElapsed>=IntroDurations[introChapter])IntroGo(introChapter+1);
        }
        void IntroGo(int chapter)
        {
            introChapter=Mathf.Clamp(chapter,0,4);introElapsed=0;RenderIntroChapter();
        }
        void UpdateIntroControls()
        {
            introCounter.text=(introChapter+1)+" / 5 · "+IntroChapters[introChapter];
            introPrevious.SetEnabled(introChapter>0);introNext.SetEnabled(introChapter<4);
            introAuto.text=introAutomatic?"Pause présentation":"Défilement auto";introAuto.SetEnabled(introChapter<4);
            introProgress.style.width=Length.Percent(introChapter==4?100:100*(introChapter+Mathf.Clamp01(introElapsed/IntroDurations[introChapter]))/5);
        }
        void RenderIntroChapter()
        {
            introBody.Clear();introPlayerDetails=null;UpdateIntroControls();
            var f=introBriefing.fixture;
            if(introChapter==0||introChapter==4){
                Text(introBody,introChapter==0?"JOUR DE MATCH":"LES ÉQUIPES SONT PRÊTES","eyebrow");
                Text(introBody,introChapter==0?"Le rendez-vous.":"À vous de jouer.","prematch-title");
                var bill=Row(introBody,"prematch-bill");IntroClub(bill,f.home,f.neutral?"Terrain neutre":"Domicile");Text(bill,"VS","prematch-versus");IntroClub(bill,f.away,f.neutral?"Terrain neutre":"Extérieur");
                Text(introBody,introBriefing.venue,"prematch-venue");Text(introBody,FixtureDay(f),"muted");
                Text(introBody,introChapter==0?"L’affiche, l’enjeu et les compositions avant d’entrer dans le match.":"Le chronomètre est arrêté. Lancez le coup d’envoi quand vous êtes prêt.","prematch-description");
            }else if(introChapter==1)RenderIntroStakes();
            else RenderIntroLineup(introChapter==2?f.home:f.away);
            AnimateEntry(introBody,true);introPanel.Q<ScrollView>("prematch-scroll").scrollOffset=Vector2.zero;
        }
        void IntroClub(VisualElement parent,string id,string caption)
        {
            var club=new VisualElement();club.AddToClassList("prematch-club");parent.Add(club);
            var texture=Resources.Load<Texture2D>("Logos/club-"+id);
            if(texture!=null){var crest=new Image{image=texture,scaleMode=ScaleMode.ScaleToFit};crest.AddToClassList("prematch-crest");club.Add(crest);}
            Text(club,ClubName(id),"prematch-club-name");Text(club,caption,"muted");
        }
        void RenderIntroStakes()
        {
            var f=introBriefing.fixture;Text(introBody,"CE QUI SE JOUE AUJOURD’HUI","eyebrow");Text(introBody,"L’enjeu.","prematch-title");Text(introBody,introBriefing.context,"prematch-description");
            if(introBriefing.HasResults){
                var table=new VisualElement{name="prematch-standings"};table.AddToClassList("prematch-table");introBody.Add(table);
                var header=Row(table,"prematch-table-row");Text(header,"Rang","prematch-rank");Text(header,"Club","prematch-table-club");Text(header,"J","prematch-stat");Text(header,"Diff.","prematch-stat");Text(header,"Pts","prematch-stat");
                var ranks=introBriefing.table.Select((row,i)=>new{row,i}).Where(x=>x.i<3||x.row.club==f.home||x.row.club==f.away).ToArray();
                int last=-1;foreach(var item in ranks){if(item.i>last+1)Text(table,"…","muted");last=item.i;var row=Row(table,"prematch-table-row");row.EnableInClassList("prematch-featured",item.row.club==f.home||item.row.club==f.away);Text(row,(item.i+1).ToString(),"prematch-rank");Text(row,ClubName(item.row.club),"prematch-table-club");Text(row,item.row.played.ToString(),"prematch-stat");Text(row,(item.row.gf-item.row.ga).ToString("+0;-0;0"),"prematch-stat");Text(row,item.row.points.ToString(),"prematch-stat");}
            }
            var forms=Row(introBody,"prematch-forms");foreach(var club in new[]{f.home,f.away}){var card=Card(forms,"prematch-form");Text(card,ClubName(club),"section-title");Text(card,PrematchBriefing.Form(Career,f,club),"prematch-form-results");
                foreach(var result in PrematchProgramme.RecentResults(Career,f,club))IntroResult(card,result);
            }
            Text(introBody,"5 derniers résultats de la carrière, toutes compétitions · du plus ancien au plus récent. V : victoire · N : nul · D : défaite (score avant tirs au but).","muted");
            var meeting=PrematchProgramme.LastMeeting(Career,f);
            if(meeting!=null){var history=Card(introBody,"prematch-meeting");history.name="prematch-last-meeting";Text(history,"Dernière confrontation dans cette carrière","section-title");IntroResult(history,meeting);}
            Text(introBody,"Détail des trois derniers matchs enregistrés dans cette carrière, du plus récent au plus ancien.","muted");
        }
        void IntroResult(VisualElement parent,Fixture result)
        {
            var row=new VisualElement();row.AddToClassList("prematch-result");parent.Add(row);
            Text(row,FixtureDay(result)+" · "+Career.CompetitionName(Database,result.league),"muted");
            Text(row,ClubName(result.home)+"  "+result.hg+" – "+result.ag+"  "+ClubName(result.away),"prematch-result-score");
            if(result.penHome>0||result.penAway>0)Text(row,"Tirs au but : "+result.penHome+" – "+result.penAway,"muted");
        }
        void RenderIntroLineup(string club)
        {
            var match=Career.match;int side=PrematchBriefing.Side(match,club);var tactic=side==0?match.homeTactic:match.awayTactic;
            Text(introBody,introChapter==2?"LE ONZE À DOMICILE":"LE ONZE À L’EXTÉRIEUR","eyebrow");
            var title=Row(introBody,"prematch-lineup-title");Text(title,ClubName(club)+" · "+tactic.formation,"prematch-title");
            var phases=Row(title,"prematch-phase-controls");
            foreach(bool withBall in new[]{false,true}){bool phase=withBall;var button=Button(phases,phase?"Avec ballon":"Sans ballon",()=>{introAutomatic=false;introWithBall=phase;RenderIntroChapter();});button.name=phase?"prematch-with-ball":"prematch-without-ball";button.EnableInClassList("active",introWithBall==phase);}
            var columns=Row(introBody,"prematch-lineup");var pitch=new VisualElement{name="prematch-pitch"};pitch.AddToClassList("prematch-pitch");columns.Add(pitch);PitchLines(pitch);
            var list=new VisualElement{name="prematch-starters"};list.AddToClassList("prematch-starters");columns.Add(list);
            var starters=PrematchBriefing.Starters(match,club);
            if(!starters.Any(a=>a.id==introSelectedPlayer))introSelectedPlayer=null;
            foreach(var actor in starters){
                var p=Database.Find(actor.id);var slot=PrematchProgramme.Position(match,club,actor.slot,introWithBall);if(p==null||slot==null)continue;
                string playerId=p.id;
                var anchor=new VisualElement{name="prematch-position-"+actor.slot};anchor.AddToClassList("prematch-position");Position(anchor,new Slot(slot.role,Mathf.Lerp(10,90,slot.x/100),Mathf.Lerp(13,88,slot.y/100)));pitch.Add(anchor);
                var token=new Button(()=>SelectIntroPlayer(club,playerId)){name="prematch-player-"+actor.slot,tooltip=p.name,userData=p.id};token.AddToClassList("prematch-player");anchor.Add(token);
                Text(token,(p.number>0?p.number.ToString():"—")+" · "+FrenchFootballPositions.Short(slot.role),"prematch-shirt");Text(token,arena.PlayerSurname(p.id),"prematch-surname");
                var row=new Button(()=>SelectIntroPlayer(club,playerId)){name="prematch-starter-"+actor.slot,userData=p.id,tooltip=p.name};row.AddToClassList("prematch-starter");list.Add(row);Text(row,p.number>0?p.number.ToString():"—","prematch-number");Text(row,p.name,"prematch-fullname");Text(row,FrenchFootballPositions.Short(slot.role),"prematch-role");
                foreach(var label in token.Query<Label>().ToList())label.pickingMode=PickingMode.Ignore;
                foreach(var label in row.Query<Label>().ToList())label.pickingMode=PickingMode.Ignore;
                if(MotionEnabled){token.style.opacity=0;int delay=actor.slot*65;token.schedule.Execute(()=>{token.style.opacity=1;AnimateEntry(token);}).StartingIn(delay);}
            }
            Text(introBody,(introWithBall?"Organisation avec ballon":"Organisation sans ballon")+" · sens de l’attaque ↑ · touchez un joueur pour découvrir sa mission.","muted");
            Text(introBody,PrematchProgramme.Instructions(match,club,introWithBall),"prematch-instructions");
            introPlayerDetails=new VisualElement{name="prematch-player-details"};introPlayerDetails.AddToClassList("prematch-player-details");introBody.Add(introPlayerDetails);
            RenderIntroPlayer(club);
        }
        void SelectIntroPlayer(string club,string id)
        {
            introAutomatic=false;introSelectedPlayer=id;UpdateIntroControls();RenderIntroPlayer(club);
            // Keep the programme and anthem alive; no global modal or simulation mutation.
            var details=introPlayerDetails;introPanel.schedule.Execute(()=>{if(details!=null&&details.panel!=null)introPanel.Q<ScrollView>("prematch-scroll")?.ScrollTo(details);}).StartingIn(1);
        }
        void RenderIntroPlayer(string club)
        {
            if(introPlayerDetails==null)return;
            introPlayerDetails.Clear();
            foreach(var token in introBody.Query<Button>().ToList())if(token.userData is string id)token.EnableInClassList("prematch-selected",id==introSelectedPlayer);
            var actor=PrematchBriefing.Starters(Career.match,club).FirstOrDefault(a=>a.id==introSelectedPlayer);
            var player=actor==null?null:Database.Find(actor.id);introPlayerDetails.style.display=player==null?DisplayStyle.None:DisplayStyle.Flex;if(player==null)return;
            var slot=PrematchProgramme.Position(Career.match,club,actor.slot,introWithBall);
            var identity=Row(introPlayerDetails,"prematch-player-identity");
            var portrait=new VisualElement{name="prematch-player-portrait",tooltip="Portrait réel disponible hors connexion ; initiales si absent."};portrait.AddToClassList("prematch-player-portrait");identity.Add(portrait);
            Text(portrait,string.Concat(player.name.Split(' ').Where(s=>s.Length>0).Take(2).Select(s=>s.Substring(0,1))),"prematch-avatar-initials");Text(portrait,"Initiales","prematch-avatar-caption");
            var copy=new VisualElement{name="prematch-player-copy"};copy.AddToClassList("prematch-player-copy");identity.Add(copy);
            Text(copy,player.name,"section-title");
            Text(copy,(player.age>0?player.age+" ans":"Âge non renseigné")+" · "+(string.IsNullOrEmpty(player.nationality)?"Nationalité non renseignée":player.nationality)+" · Pied : "+PrematchProgramme.PreferredFoot(player),"muted");
            Text(introPlayerDetails,(introWithBall?"Avec ballon":"Sans ballon")+" · "+FrenchFootballPositions.Label(slot?.role)+" · Mission "+PrematchProgramme.Duty(slot),"prematch-player-mission");
            Text(introPlayerDetails,"Position de départ et mission prévues. Les déplacements s’adaptent au ballon et aux consignes collectives.","muted");
            portraits??=gameObject.AddComponent<PortraitStore>();portraits.Load(player.id,texture=>{
                if(texture==null||portrait.panel==null||introSelectedPlayer!=player.id)return;
                portrait.Clear();var photo=new Image{image=texture,scaleMode=ScaleMode.ScaleToFit,name="prematch-player-photo"};photo.AddToClassList("prematch-player-photo");portrait.Add(photo);
            });
        }
        void PresentationApplicationPause(bool paused)
        {
            if(!presentationPending||anthem==null)return;
            if(paused)introAudioSuspended=anthem.isPlaying;
            else if(introAudioSuspended){anthem.UnPause();introAudioSuspended=false;}
            introLastTick=Time.realtimeSinceStartup;
        }
        void CancelPresentation()
        {
            presentationPending=false;introTimer?.Pause();introTimer=null;introFixture=null;introAudioSuspended=false;
        }
        void StartPresentedMatch()
        {
            CloseModal();arena.Paused=false;
            // Reveal the actual paused arena; this transition never teleports actors.
            var viewport=root.Q<Image>("match-viewport");if(viewport!=null)AnimateEntry(viewport);
        }
    }
}

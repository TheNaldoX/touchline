using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Touchline.Core;

namespace Touchline.Editor
{
    // Captures d'écran de l'interface aux deux résolutions du Galaxy Z Fold, rendues
    // par Unity dans GitHub Actions (branche film/ui…, voir match-film.yml).
    // Lance le jeu en mode Play avec une carrière de test, parcourt les écrans
    // fréquents (bureau, effectif, tactique, menus, match, banc, régie) et écrit
    // dans build/film/<nom>/ : <écran>.png et audit.txt (cibles tactiles et tailles
    // de texte converties en dp, éléments qui débordent de l'écran).
    [InitializeOnLoad] public static class CiUiScreens
    {
        const string Flag="CiUiScreens",OutputKey="CiUiScreensOutput";
        // Densité Android du Fold (≈ 420 dpi) : pixels physiques par dp, pour les deux écrans.
        const float DevicePixelsPerDp=2.625f;
        // Cible tactile minimale recommandée par Android (dp) et texte minimal lisible (sp).
        const float MinTouchDp=48,MinTextSp=12;
        // Attente entre deux étapes : images rendues et durée réelle (secondes), le temps que
        // la mise en page et les petites transitions d'entrée se terminent.
        const int StepFrames=12;const float StepSeconds=.6f,BootTimeoutSeconds=180,RunTimeoutSeconds=3300;
        static readonly (string tag,int width,int height)[] Screens={("plie",1080,2520),("deplie",2184,1968)};
        static int stage=-1,frames,failures;static float stepAt,startedAt=-1;static RenderTexture target;static List<Action> steps;
        static readonly StringBuilder audit=new StringBuilder(),log=new StringBuilder();
        static CiUiScreens(){EditorApplication.update+=Tick;}

        public static void Run()
        {
            string name=Environment.GetCommandLineArgs().FirstOrDefault(a=>a.StartsWith("-touchlineFilm="))?.Substring("-touchlineFilm=".Length)??"ui";
            if(!System.Text.RegularExpressions.Regex.IsMatch(name,"^[a-zA-Z0-9.-]+$"))throw new Exception("Nom invalide");
            var output=Path.GetFullPath(Path.Combine("build","film",name));if(Directory.Exists(output))Directory.Delete(output,true);Directory.CreateDirectory(output);
            ProjectBuilder.Configure();
            PlayerPrefs.SetInt("interface-size",1);PlayerPrefs.SetInt("reduce-motion",1); // captures sans transition à mi-course
            SessionState.SetString(OutputKey,output);SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;
        }

        static string Output=>SessionState.GetString(OutputKey,"build/film/ui");
        // Focused PR validation still renders at native Fold resolution. Three frames
        // allow layout/repaint to settle without twelve expensive software-rendered frames.
        static bool FocusedTactics=>Path.GetFileName(Output).StartsWith("ui-tactical-focus-",StringComparison.Ordinal);
        static bool FocusedRecruitment=>Path.GetFileName(Output).StartsWith("ui-recruitment-focus-",StringComparison.Ordinal);
        static bool FocusedLoan=>Path.GetFileName(Output).StartsWith("ui-loan-focus-",StringComparison.Ordinal);
        static bool FocusedNegotiation=>Path.GetFileName(Output).StartsWith("ui-negotiation-focus-",StringComparison.Ordinal);
        static bool FocusedBoard=>Path.GetFileName(Output).StartsWith("ui-board-focus-",StringComparison.Ordinal);
        static bool FocusedDepth=>Path.GetFileName(Output).StartsWith("ui-recruitment-depth",StringComparison.Ordinal);
        static bool FocusedCareer=>Path.GetFileName(Output).StartsWith("ui-career-review-",StringComparison.Ordinal);
        static bool FocusedCell=>Path.GetFileName(Output).StartsWith("ui-recruitment-cell-",StringComparison.Ordinal);
        static bool FocusedImmersion=>Path.GetFileName(Output).StartsWith("ui-immersion-focus-",StringComparison.Ordinal);
        const int FocusedStepFrames=3;
        static TouchlineApp App=>TouchlineApp.Instance;
        static UIDocument Document=>App.GetComponent<UIDocument>();
        static VisualElement Root=>Document.rootVisualElement;
        static object Call(string method,params object[] args)
        {
            var m=typeof(TouchlineApp).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public,null,args.Select(a=>a.GetType()).ToArray(),null);
            if(m==null)throw new Exception("Méthode absente : "+method);return m.Invoke(App,args);
        }
        static void SetField(string field,object value){var f=typeof(TouchlineApp).GetField(field,BindingFlags.Instance|BindingFlags.NonPublic);if(f==null)throw new Exception("Champ absent : "+field);f.SetValue(App,value);}
        static void Click(string nameOrText)
        {
            var button=Root.Query<Button>().ToList().FirstOrDefault(b=>b.name==nameOrText)??Root.Query<Button>().ToList().FirstOrDefault(b=>b.text==nameOrText);
            if(button==null)throw new Exception("Bouton absent : "+nameOrText);using(var e=NavigationSubmitEvent.GetPooled()){e.target=button;button.SendEvent(e);}
        }
        static void Resize(int width,int height)
        {
            var old=target;target=new RenderTexture(width,height,24);target.Create();Document.panelSettings.targetTexture=target;
            if(old!=null){old.Release();UnityEngine.Object.Destroy(old);}
        }
        static MatchArena Arena=>UnityEngine.Object.FindFirstObjectByType<MatchArena>();

        static List<Action> BuildSteps()
        {
            if(Path.GetFileName(Output).StartsWith("ui-recruitment-cache-",StringComparison.Ordinal))return BuildRecruitmentCacheSteps();
            if(Path.GetFileName(Output).StartsWith("ui-scout-snapshot-",StringComparison.Ordinal))return BuildScoutSnapshotSteps();
            if(FocusedImmersion)return BuildFocusedImmersionSteps();
            if(FocusedCareer)return BuildCareerReviewSteps();
            if(FocusedCell)return BuildFocusedCellSteps();
            if(FocusedBoard)return BuildFocusedBoardSteps();
            if(FocusedTactics)return BuildFocusedTacticSteps();
            if(FocusedRecruitment)return BuildFocusedRecruitmentSteps();
            if(FocusedLoan)return BuildFocusedLoanSteps();
            if(FocusedNegotiation)return BuildFocusedNegotiationSteps();
            if(FocusedDepth)return BuildFocusedDepthSteps();
            var list=new List<Action>();
            list.Add(()=>{App.Career.EnsureWorld(App.Database);});
            foreach(var s in Screens){
                var screen=s;
                list.Add(()=>{Resize(screen.width,screen.height);Call("Navigate","Club");});
                list.Add(()=>{Capture(screen.tag+"-bureau");Call("Navigate","Effectif");});
                list.Add(()=>{Capture(screen.tag+"-effectif");SetField("tacticalTab","Composition");Call("Navigate","Tactique");});
                list.Add(()=>{Capture(screen.tag+"-tactique");Click("Avec ballon");});
                list.Add(()=>{Capture(screen.tag+"-tactique-avec-ballon");SetField("tacticalTab","Composition");Call("Navigate","Calendrier");});
                list.Add(()=>{Capture(screen.tag+"-calendrier");Call("Navigate","Messages");});
                list.Add(()=>{Capture(screen.tag+"-messages");Call("ClubDirectory");});
                list.Add(()=>{Capture(screen.tag+"-plus");Call("CloseModal");Call("ReturnToLaunchMenu");});
                list.Add(()=>{Capture(screen.tag+"-accueil");Call("ResumeLaunchSession");});
            }
            // Jour de match : lobby, présentation, puis la rencontre elle-même.
            var first=Screens[0];
            list.Add(()=>{Resize(first.width,first.height);Call("Navigate","Club");});
            // « Jusqu’au match » s’il existe (une touche), sinon saut direct au jour du match.
            list.Add(()=>{if(Root.Q<Button>("manager-advance-to-match")!=null){audit.AppendLine("Jusqu’au match : "+(App.Career.life.nextFixture-App.Career.life.day)+" jour(s) en une touche");Click("manager-advance-to-match");}else{App.Career.life.day=App.Career.life.nextFixture;Call("Navigate","Club");}});
            list.Add(()=>{Capture(first.tag+"-jour-de-match");if(App.Career.life.day<App.Career.life.nextFixture){audit.AppendLine("Arrêt avant le match (décision à prendre) au jour "+App.Career.life.day);App.Career.life.day=App.Career.life.nextFixture;}Call("Navigate","Match");});
            list.Add(()=>{Capture(first.tag+"-avant-match");Click("Entrer sur le terrain");});
            list.Add(()=>{Capture(first.tag+"-presentation");if(Root.Q<Button>("prematch-kickoff")!=null)Click("prematch-kickoff");});
            foreach(var s in Screens){
                var screen=s;
                list.Add(()=>{Resize(screen.width,screen.height);Call("Navigate","Match");});
                list.Add(()=>{var arena=Arena;if(arena!=null)arena.Paused=true;});
                list.Add(()=>{Capture(screen.tag+"-match-pause");var arena=Arena;if(arena!=null)arena.Paused=false;});
                list.Add(()=>{Click("match-instructions-open");});
                list.Add(()=>{Capture(screen.tag+"-consignes-rapides");bool paused=Arena.Paused;float speed=Arena.Speed;Click("quick-tactic-Pressing-2");if(App.Career.tactic.pressing!=.8f||App.Career.match.homeTactic.pressing!=.8f||Arena.Paused!=paused||Arena.Speed!=speed||Root.Q("match-instructions")!=null)throw new Exception("Consigne en deux touches : application/retour/pause incorrects");audit.AppendLine("Pressing intense en deux touches : tactique liée, retour au direct, pause et vitesse préservées.");});
                for(int i=0;i<4;i++)list.Add(()=>{}); // quelques secondes de jeu
                list.Add(()=>{Capture(screen.tag+"-match-direct");if(Root.Q<Button>("match-mentality-2")!=null){Click("match-mentality-2");audit.AppendLine("Mentalité offensive en une touche : "+App.Career.tactic.mentality.ToString(CultureInfo.InvariantCulture)+" · pause "+(Arena!=null&&Arena.Paused));}Call("MatchBench");});
                list.Add(()=>{Capture(screen.tag+"-banc");Call("CloseModal");Call("MatchOptions");});
                list.Add(()=>{Capture(screen.tag+"-regie");Call("CloseModal");SetField("tacticalTab","Composition");Call("Navigate","Tactique");});
                list.Add(()=>{Capture(screen.tag+"-match-tactique");Click("Avec ballon");});
                list.Add(()=>{Capture(screen.tag+"-match-consignes");SetField("tacticalTab","Composition");Call("Navigate","Match");});
            }
            return list;
        }

        static List<Action> BuildRecruitmentCacheSteps()
        {
            var list=new List<Action>();string id=null;
            list.Add(()=>{var c=App.Career;c.EnsureWorld(App.Database);c.revealAttributes=false;var p=App.Database.players.First(x=>x.team!=c.club&&x.team!="retired"&&x.attributes?.Length>0);id=p.id;p.positions=new[]{"RW"};p.position="ATT";c.world.reports.Clear();c.world.reports.Add(new ScoutReport{player=id,club=c.club,confidence=90,judging=15,estimate=70,potential=75,attributesObserved=true,started=c.life.day-20,due=c.life.day});SetField("scoutReportFilter","Tous");SetField("recruitmentTab","Rapports");});
            foreach(var s in Screens){var screen=s;
                list.Add(()=>{Resize(screen.width,screen.height);App.Career.tactic.SetFormation("4-3-3");Call("Navigate","Recrutement");});
                list.Add(()=>{var advice=Root.Q("scout-report-"+id).Q<Label>(className:"scout-report-advice").text;if(advice.Contains("hors postes du système"))throw new Exception("Winger must initially fit the formation");Capture(screen.tag+"-poste-initial");App.Career.tactic.withoutBall[10].role="ST";App.Career.tactic.withBall[10].role="ST";Call("Navigate","Recrutement");});
                list.Add(()=>{Capture(screen.tag+"-poste-modifie");var advice=Root.Q("scout-report-"+id).Q<Label>(className:"scout-report-advice").text;if(!advice.Contains("hors postes du système"))throw new Exception("Recruitment still uses the former winger position after a same-day custom tactic change");});
            }
            audit.AppendLine("Poste AD remplacé par BT sans changer le nom4-3-3 ni le jour ; grade du rapport vérifié avant/après, deux résolutions, profil de test en mémoire.");return list;
        }

        static List<Action> BuildScoutSnapshotSteps()
        {
            var list=new List<Action>();string id=null;
            list.Add(()=>{App.Career.EnsureWorld(App.Database);App.Career.revealAttributes=false;id=App.Database.players.First(p=>p.team!=App.Career.club&&p.team!="retired"&&p.attributes?.Length>0).id;});
            foreach(var s in Screens){var screen=s;
                list.Add(()=>{var c=App.Career;Resize(screen.width,screen.height);c.world.reports.Clear();c.world.reports.Add(new ScoutReport{player=id,club=c.club,confidence=90,judging=15,estimate=70,potential=75,lastObserved=c.life.day,started=c.life.day-20,due=c.life.day,scout="Rapport de sauvegarde ancienne"});SetField("scoutReportFilter","Tous");SetField("recruitmentTab","Rapports");Call("Navigate","Recrutement");});
                list.Add(()=>{if(Root.Q("scout-missing-snapshot-"+id)==null||Root.Q<Button>("scout-refresh-"+id)?.enabledInHierarchy!=true)throw new Exception("Ancien rapport : avertissement ou actualisation absent");Capture(screen.tag+"-ancien-rapport");Root.Q<DropdownField>("scout-report-filter").value="À actualiser";if(Root.Q("scout-report-"+id)==null)throw new Exception("Filtre des anciens rapports incorrect");});
                list.Add(()=>{Capture(screen.tag+"-ancien-detail");Click("scout-refresh-"+id);var r=App.Career.ReportFor(id);if(!r.attributesObserved||r.observedAttributes?.Length==0)throw new Exception("Nouvelle observation non archivée");r.confidence=90;r.lastObserved=App.Career.life.day;SetField("scoutReportFilter","Tous");Call("Navigate","Recrutement");});
                list.Add(()=>{if(Root.Q("scout-missing-snapshot-"+id)!=null||Root.Q("scout-range-ability-"+id)==null)throw new Exception("Nouveau rapport incohérent");Capture(screen.tag+"-rapport-archive");var notice=Root.Q("scout-report-"+id);notice.GetFirstAncestorOfType<ScrollView>()?.ScrollTo(notice);});
                list.Add(()=>Capture(screen.tag+"-rapport-detail"));
            }
            audit.AppendLine("Sauvegarde ancienne et observation renouvelée : deux résolutions, carrière en mémoire ; progression simulée pour la revue UI, vérifiée séparément par CoreTests.");return list;
        }

        // Immersion : préparation, briefing adverse, causeries, cris depuis la touche,
        // vestiaire de mi-temps, lecture de l'adjoint et consigne individuelle.
        static List<Action> BuildFocusedImmersionSteps()
        {
            audit.AppendLine("Parcours ciblé immersion : deux résolutions natives par écran.");
            bool talksOnly=Path.GetFileName(Output).StartsWith("ui-immersion-focus-talks-",StringComparison.Ordinal);
            var list=new List<Action>();var first=Screens[0];
            void Both(string tag,Action prepare){foreach(var s in Screens){var screen=s;list.Add(()=>Resize(screen.width,screen.height));list.Add(()=>prepare?.Invoke());list.Add(()=>Capture(screen.tag+"-"+tag));}}
            list.Add(()=>{App.Career.EnsureWorld(App.Database);Resize(first.width,first.height);Call("Navigate","Club");});
            if(!talksOnly)Both("preparation",()=>{Call("Navigate","Club");var focus=Root.Q("manager-training-focus");if(focus==null)throw new Exception("Thème d'entraînement absent");Root.Q<ScrollView>()?.ScrollTo(focus);});
            list.Add(()=>{Resize(first.width,first.height);App.Career.life.day=App.Career.life.nextFixture;Call("Navigate","Match");});
            list.Add(()=>Click("Entrer sur le terrain"));
            list.Add(()=>{if(App.Career.match?.mindset==null||App.Career.match.mindset.Length!=2)throw new Exception("État mental absent du match de carrière");SetField("introAutomatic",false);Call("IntroGo",1);});
            if(!talksOnly)Both("briefing",()=>{var card=Root.Q("immersion-opponent");if(card==null)throw new Exception("Rapport adverse absent");Root.Q<ScrollView>("prematch-scroll").ScrollTo(card);});
            list.Add(()=>Call("IntroGo",4));
            Both("causerie-avant",()=>{var card=Root.Q("team-talk");if(card==null)throw new Exception("Causerie absente");Root.Q<ScrollView>("prematch-scroll").ScrollTo(card);});
            list.Add(()=>{var b=Root.Q<Button>("team-talk-calm");var view=Root.Q<ScrollView>("prematch-scroll");if(b==null||b.resolvedStyle.whiteSpace!=WhiteSpace.Normal||!b.worldBound.Overlaps(view.contentViewport.worldBound))throw new Exception("Choix de causerie hors champ ou libellé non repliable");});
            list.Add(()=>{float before=App.Career.match.mindset[0].composure.Average();Click("team-talk-calm");float after=App.Career.match.mindset[0].composure.Average();if(after<=before)throw new Exception("Causerie sans effet");audit.AppendLine("Causerie « Rassurer » : sang-froid moyen "+before.ToString("0.00",CultureInfo.InvariantCulture)+" → "+after.ToString("0.00",CultureInfo.InvariantCulture));});
            Both("causerie-reaction",()=>{var card=Root.Q("team-talk");if(card!=null)Root.Q<ScrollView>("prematch-scroll").ScrollTo(card);});
            list.Add(()=>{Resize(first.width,first.height);if(Root.Q<Button>("prematch-kickoff")!=null)Click("prematch-kickoff");});
            if(!talksOnly){
            list.Add(()=>{Arena.Paused=true;Arena.Simulation.Advance(20*App.Career.match.SecondsPerMinute);Click("match-instructions-open");});
            Both("cris-touche",()=>{var card=Root.Q("touchline-shouts");if(card==null)throw new Exception("Cris depuis la touche absents");Root.Q("match-instructions").Q<ScrollView>().ScrollTo(card);});
            list.Add(()=>{Click("shout-press");if(App.Career.match.events.Last().kind!="shout")throw new Exception("Cri non enregistré");audit.AppendLine("Cri « Pressez ! » enregistré, retour au match.");});
            }
            list.Add(()=>{var sim=Arena.Simulation;sim.Advance(sim.State.HalfDuration);if(!sim.State.halfTime)throw new Exception("Mi-temps non atteinte");Call("MatchAnalysis");});
            Both("vestiaire-mi-temps",null);
            list.Add(()=>Click("team-talk-encourage"));
            Both("vestiaire-reaction",null);
            if(talksOnly)return list;
            list.Add(()=>Click("Adjoint"));
            Both("adjoint-lecture",null);
            list.Add(()=>{Call("CloseModal");SetField("tacticalTab","Composition");SetField("selectedSlot",1);Call("Navigate","Tactique");});
            Both("consigne-individuelle",()=>{var field=Root.Q("tactics-player-instruction");if(field==null)throw new Exception("Consigne individuelle absente");});
            return list;
        }

        static List<Action> BuildFocusedBoardSteps()
        {
            var list=new List<Action>();
            list.Add(()=>App.Career.EnsureWorld(App.Database));
            foreach(var s in Screens){var screen=s;
                list.Add(()=>{Resize(screen.width,screen.height);Call("Navigate","Carrière");});
                list.Add(()=>{
                    if(Root.Q("board-season-objective")==null)throw new Exception("Objectif de direction absent");
                    Capture(screen.tag+"-objectif-direction");
                });
            }
            list.Add(()=>{
                var c=App.Career;var objective=c.EnsureBoardObjective(App.Database);c.life.day+=30;int playedDay=c.life.day-6;
                foreach(var f in c.world.fixtures.Where(f=>f.league==objective.division&&(f.home==c.club||f.away==c.club)).Take(6)){
                    f.day=playedDay++;f.played=true;f.hg=f.home==c.club?0:2;f.ag=f.away==c.club?0:2;
                }
                c.ReviewBoardObjective(App.Database);
                audit.AppendLine("Bilan direction : six défaites de championnat synthétiques, carrière de validation uniquement.");
            });
            foreach(var s in Screens){var screen=s;
                list.Add(()=>{Resize(screen.width,screen.height);Call("Navigate","Carrière");});
                list.Add(()=>Capture(screen.tag+"-bilan-direction"));
            }
            return list;
        }

        static List<Action> BuildCareerReviewSteps()
        {
            bool remaining=Path.GetFileName(Output).Contains("-remaining-");
            var list=remaining?new List<Action>():BuildFocusedDepthSteps();if(!remaining)list.AddRange(BuildFocusedBoardSteps());string player=null;
            list.Add(()=>{
                var c=App.Career;c.EnsureWorld(App.Database);var f=c.world.fixtures.First(x=>x.home==c.club||x.away==c.club);
                f.day=c.life.day;f.played=true;f.hg=f.home==c.club?0:2;f.ag=f.away==c.club?0:2;
                player=App.Database.Squad(c.club).First(p=>!p.Goalkeeper&&c.Contract(App.Database,p.id).parent==null).id;
                c.departureRequests.Add(new DepartureRequest{club=c.club,player=player,status="requested",since=c.life.day-28,opened=c.life.day});
                audit.AppendLine("Conférence après défaite et demande de départ synthétiques ; aucune sauvegarde personnelle.");
            });
            foreach(var s in Screens){var screen=s;
                list.Add(()=>{Resize(screen.width,screen.height);Call("Navigate","Presse");});
                list.Add(()=>{Capture(screen.tag+"-presse-resultat");if(Root.Q("press-after-protect")==null)throw new Exception("Conférence contextuelle absente");});
                list.Add(()=>Call("Conversation",player));
                list.Add(()=>{if(Root.Q("departure-allow")==null||Root.Q("departure-refuse")==null)throw new Exception("Choix de départ absents");Capture(screen.tag+"-demande-depart");Call("CloseModal");});
                list.Add(()=>{Call("Navigate","Carrière");});
                list.Add(()=>{var news=Root.Q("club-news");if(news==null)throw new Exception("Actualités absentes");news.GetFirstAncestorOfType<ScrollView>()?.ScrollTo(news);});
                list.Add(()=>Capture(screen.tag+"-actualites-club"));
            }
            return list;
        }

        static List<Action> BuildFocusedNegotiationSteps()
        {
            bool reference=Path.GetFileName(Output).EndsWith("-before",StringComparison.Ordinal);
            var list=new List<Action>();string player=null;long cash=0;int offers=0,ledger=0;VisualElement form=null;
            void AssertUnchanged()
            {
                if(App.Career.life.cash!=cash||App.Career.world.offers.Count!=offers||App.Career.life.ledger.Count!=ledger)throw new Exception("Une offre invalide a modifié les finances ou les dossiers");
            }
            void AssertError()
            {
                var error=Root.Q<Label>("negotiation-error");
                if(Root.Q("transfer-negotiation-form")!=form||error==null||string.IsNullOrEmpty(error.text)||error.resolvedStyle.display==DisplayStyle.None)throw new Exception("Le formulaire ou son erreur persistante a disparu");
                AssertUnchanged();
            }
            list.Add(()=>{
                var safe=typeof(TouchlineApp).GetProperty("VisualValidation",BindingFlags.Instance|BindingFlags.NonPublic);
                if(safe==null||!(bool)safe.GetValue(App))throw new Exception("La validation doit isoler les sauvegardes personnelles");
                App.Career.EnsureWorld(App.Database);
                player=App.Database.players.First(p=>p.team!=App.Career.club&&p.team!="free"&&p.team!="retired"&&!p.team.StartsWith("academy-")&&p.age>=24).id;
                // Isolated UI fixture: abundant budget and a valid parent contract avoid
                // unrelated market restrictions when exercising corrected resubmission.
                App.Career.life.cash=1000000000;App.Career.life.revenue=1000000000;
                App.Career.life.day=(int)(new DateTime(2026,6,20)-Touchline.Core.Career.Epoch).TotalDays;
                var contract=App.Career.Contract(App.Database,player);contract.parent=null;contract.until=App.Career.life.day+730;
                App.Database.Find(player).wage=500;
            });
            foreach(var s in Screens)
            {
                var screen=s;
                list.Add(()=>{
                    Resize(screen.width,screen.height);App.Career.world.offers.RemoveAll(o=>o.player==player);
                    Call("Navigate","Recrutement");Call("TransferDialog",player);
                });
                list.Add(()=>{
                    form=Root.Q("transfer-negotiation-form");if(form==null)throw new Exception("Formulaire de négociation absent");
                    Root.Q<Toggle>("negotiation-loan").value=true;
                    Root.Q<IntegerField>("negotiation-loan-share").value=37;Root.Q<IntegerField>("negotiation-loan-days").value=60;
                    Root.Q<LongField>("negotiation-loan-option").value=120000;Root.Q<LongField>("negotiation-fee").value=9000;Root.Q<LongField>("negotiation-bonus").value=111;
                    Root.Q<Toggle>("negotiation-loan").value=false;
                    Root.Q<LongField>("negotiation-monthly-wage").value=0;Root.Q<LongField>("negotiation-fee").value=50000;
                    Root.Q<LongField>("negotiation-bonus").value=222;Root.Q<IntegerField>("negotiation-years").value=4;
                    Root.Q<DropdownField>("negotiation-role").value=Touchline.Core.PlayingTimeRoles.Label("starter");
                    form.Q<ScrollView>().ScrollTo(Root.Q<Button>("negotiation-send"));
                    cash=App.Career.life.cash;offers=App.Career.world.offers.Count;ledger=App.Career.life.ledger.Count;
                });
                list.Add(()=>{Capture(screen.tag+"-negociation-conditions");Click("negotiation-send");});
                if(reference)
                {
                    list.Add(()=>{AssertUnchanged();if(Root.Q("transfer-negotiation-form")!=null)throw new Exception("Le défaut de référence attendu n'est pas reproduit");Capture(screen.tag+"-negociation-erreur");audit.AppendLine("Référence : salaire nul rejeté, formulaire détruit, finances intactes.");});
                    continue;
                }
                list.Add(()=>{
                    AssertError();
                    if(Root.Q<LongField>("negotiation-monthly-wage").value!=0||Root.Q<LongField>("negotiation-bonus").value!=222||Root.Q<IntegerField>("negotiation-years").value!=4||Root.Q<DropdownField>("negotiation-role").value!=Touchline.Core.PlayingTimeRoles.Label("starter"))throw new Exception("Conditions permanentes perdues");
                    Capture(screen.tag+"-negociation-erreur");
                    Root.Q<Toggle>("negotiation-loan").value=true;
                    if(Root.Q<IntegerField>("negotiation-loan-share").value!=37||Root.Q<IntegerField>("negotiation-loan-days").value!=60||Root.Q<LongField>("negotiation-loan-option").value!=120000||Root.Q<LongField>("negotiation-bonus").value!=111||Root.Q<LongField>("negotiation-fee").value!=9000)throw new Exception("Brouillon de prêt perdu");
                    Root.Q<LongField>("negotiation-fee").value=-1;form.Q<ScrollView>().ScrollTo(Root.Q<Button>("negotiation-send"));
                });
                list.Add(()=>{Click("negotiation-send");});
                list.Add(()=>{
                    AssertError();Capture(screen.tag+"-negociation-pret-erreur");
                    if(Root.Q<IntegerField>("negotiation-loan-share").value!=37||Root.Q<LongField>("negotiation-loan-option").value!=120000)throw new Exception("Conditions de prêt perdues après erreur");
                    Root.Q<LongField>("negotiation-fee").value=9000;Root.Q<Toggle>("negotiation-loan").value=false;
                    if(Root.Q<LongField>("negotiation-fee").value!=50000||Root.Q<LongField>("negotiation-bonus").value!=222||Root.Q<LongField>("negotiation-monthly-wage").value!=0)throw new Exception("Retour au brouillon permanent incorrect");
                    Root.Q<LongField>("negotiation-monthly-wage").value=10000;Click("negotiation-send");
                });
                list.Add(()=>{
                    var offer=App.Career.world.offers.LastOrDefault(o=>o.player==player&&o.status=="pending");
                    if(offer==null||offer.loan||offer.role!="starter"||offer.bonus!=222||offer.fee!=50000||offer.years!=4||offer.wage!=Touchline.Core.Career.WeeklySalary(10000)||Root.Q("transfer-negotiation-form")!=null)throw new Exception("Renvoi corrigé : offre ou sortie du formulaire incorrecte");
                    if(App.Career.life.cash!=cash||App.Career.world.offers.Count!=offers+1)throw new Exception("La proposition doit créer exactement un dossier sans débit de signature");
                    audit.AppendLine(screen.tag+" : salaire nul et indemnité négative refusés, formulaire et deux brouillons préservés, promesse et prime conservées, finances intactes ; offre corrigée créée une seule fois.");
                    SetField("recruitmentTab","Négociations et prêts");Call("Navigate","Recrutement");
                });
                list.Add(()=>{Capture(screen.tag+"-negociation-envoyee");});
            }
            return list;
        }

        static List<Action> BuildFocusedTacticSteps()
        {
            bool reference=Path.GetFileName(Output).EndsWith("-before",StringComparison.Ordinal);
            audit.AppendLine("Parcours ciblé : interface du match, deux résolutions natives. Les autres écrans ne sont pas revalidés par ce parcours.");
            var list=new List<Action>();var first=Screens[0];
            list.Add(()=>{App.Career.EnsureWorld(App.Database);Resize(first.width,first.height);App.Career.life.day=App.Career.life.nextFixture;Call("Navigate","Match");});
            list.Add(()=>Click("Entrer sur le terrain"));
            list.Add(()=>{if(Root.Q<Button>("prematch-kickoff")!=null)Click("prematch-kickoff");});
            foreach(var s in Screens){
                var screen=s;
                list.Add(()=>{Resize(screen.width,screen.height);Call("Navigate","Match");});
                list.Add(()=>{if(Arena==null)throw new Exception("Match absent");Arena.Paused=true;});
                list.Add(()=>{Capture(screen.tag+"-match-pause");if(!reference)Click("match-instructions-open");});
                if(!reference){
                    list.Add(()=>{Capture(screen.tag+"-consignes-rapides");AssertQuickPressing(2,true);});
                    list.Add(()=>{Arena.Paused=false;Arena.Speed=2;Click("match-instructions-open");});
                    list.Add(()=>AssertQuickPressing(1,false));
                    list.Add(()=>{Arena.Paused=true;Click("match-instructions-open");});
                    list.Add(()=>Root.Q("match-instructions").Q<ScrollView>().ScrollTo(Root.Q<Button>("quick-tactic-Directness-2")));
                    list.Add(()=>{
                        Capture(screen.tag+"-consignes-fin");float speed=Arena.Speed;Click("quick-tactic-Directness-2");
                        if(App.Career.tactic.directness!=.8f||App.Career.match.homeTactic.directness!=.8f||!Arena.Paused||Arena.Speed!=speed||Root.Q("match-instructions")!=null)
                            throw new Exception("Dernière consigne après défilement : application/pause/vitesse incorrects");
                        audit.AppendLine("Passes directes après défilement : tactique liée, pause/vitesse conservées, retour au match.");
                        Arena.Paused=false;
                    });
                    list.Add(()=>{Capture(screen.tag+"-match-retour");Arena.Paused=true;});
                }
            }
            return list;
        }

        static List<Action> BuildFocusedRecruitmentSteps()
        {
            bool reference=Path.GetFileName(Output).EndsWith("-before",StringComparison.Ordinal);
            audit.AppendLine("Parcours ciblé recrutement : navigation, postes, missions, joueurs libres et rapports ; deux résolutions natives. Aucune sauvegarde personnelle.");
            var list=new List<Action>();string role=null,searchRole=null,reportedPlayer=null;long cash=0;int missions=0;
            list.Add(()=>{
                var safe=typeof(TouchlineApp).GetProperty("VisualValidation",BindingFlags.Instance|BindingFlags.NonPublic);
                if(safe==null||!(bool)safe.GetValue(App))throw new Exception("La validation doit isoler les sauvegardes personnelles");
                App.Career.EnsureWorld(App.Database);App.Career.revealAttributes=false;
                var p=App.Database.players.First(p=>p.team!=App.Career.club&&p.team!="free"&&p.team!="retired"&&!p.team.StartsWith("academy-")&&p.age>=18);
                reportedPlayer=p.id;
                App.Career.world.reports.Add(new Touchline.Core.ScoutReport{player=p.id,club=App.Career.club,scout="Cellule de test",started=App.Career.life.day-20,due=App.Career.life.day-1,lastObserved=App.Career.life.day,confidence=90,estimate=72,potential=78,judging=15,uncertainty=3,potentialUncertainty=5,advice="Rapport de contrôle : comparer le profil et les conditions financières."});
            });
            foreach(var s in Screens){
                var screen=s;
                list.Add(()=>{Resize(screen.width,screen.height);SetField("recruitmentTab",reference?"Marché":"Synthèse");Call("Navigate","Recrutement");});
                if(!reference){
                    list.Add(()=>{
                        Capture(screen.tag+"-recrutement-synthese");
                        if(Path.GetFileName(Output).Contains("-polish"))foreach(var tab in Root.Query<Button>().ToList().Where(b=>b.name!=null&&b.name.StartsWith("recruit-tab-",StringComparison.Ordinal)))
                            if(tab.worldBound.xMin<Root.worldBound.xMin||tab.worldBound.xMax>Root.worldBound.xMax)throw new Exception("Onglet de recrutement hors écran : "+tab.text);
                        var candidate=Root.Q("recruit-hub-candidate-"+reportedPlayer);
                        if(candidate==null)throw new Exception("Le profil observé doit figurer dans les pistes connues");
                        var scroll=Root.Q<ScrollView>("recruit-hub");
                        var profile=Root.Q<Button>("recruit-hub-profile-"+reportedPlayer);
                        if(profile==null)throw new Exception("Bouton de fiche absent de la piste");
                        // Target the actual action, not the top of a potentially tall card.
                        // Run after the navigation's deferred scroll restoration (35 ms).
                        scroll.schedule.Execute(()=>scroll.ScrollTo(profile)).StartingIn(100);
                    });
                    list.Add(()=>{
                        var scroll=Root.Q<ScrollView>("recruit-hub");var profile=Root.Q<Button>("recruit-hub-profile-"+reportedPlayer);
                        audit.AppendLine("Piste : offset="+scroll.scrollOffset.y+" / "+scroll.verticalScroller.highValue+" viewport="+scroll.contentViewport.worldBound+" contenu="+scroll.contentContainer.worldBound+" fiche="+profile?.worldBound);
                        Capture(screen.tag+"-recrutement-pistes");
                        if(profile==null||!profile.worldBound.Overlaps(scroll.contentViewport.worldBound))throw new Exception("La piste connue reste hors du défilement visible");
                        Root.Q<ScrollView>("recruit-hub").scrollOffset=Vector2.zero;
                    });
                    if(Path.GetFileName(Output).Contains("-context")){
                        list.Add(()=>{var comparison=Root.Q("recruit-hub-comparison-"+reportedPlayer);if(comparison==null||comparison.resolvedStyle.whiteSpace!=WhiteSpace.Normal)throw new Exception("Comparaison absente ou texte non repliable");Root.Q<ScrollView>("recruit-hub").ScrollTo(comparison);});
                        list.Add(()=>Capture(screen.tag+"-comparaison-contextuelle"));
                        if(Path.GetFileName(Output).Contains("-budget")){
                            list.Add(()=>Call("Navigate","Finances"));
                            list.Add(()=>{var budget=Root.Q("finance-budget-explanation");if(budget==null)throw new Exception("Explication du budget absente");budget.GetFirstAncestorOfType<ScrollView>().ScrollTo(budget);});
                            list.Add(()=>Capture(screen.tag+"-budget-explique"));
                        }
                        continue;
                    }
                    if(Path.GetFileName(Output).Contains("-scroll"))continue;
                    list.Add(()=>{
                        var search=Root.Query<Button>().ToList().First(b=>b.name!=null&&b.name.StartsWith("recruit-hub-search-",StringComparison.Ordinal));
                        role=search.name.Substring("recruit-hub-search-".Length);
                        searchRole=role switch{"LM"=>"LW","RM"=>"RW","LWB"=>"LB","RWB"=>"RB","CF"=>"ST","SS"=>"ST",_=>role};Click(search.name);
                    });
                    list.Add(()=>{
                        if(Root.Q<DropdownField>("recruit-role")?.value!=searchRole||Root.Q<ListView>("recruit-list")==null)throw new Exception("Le besoin ne filtre pas le marché au bon poste");
                        Capture(screen.tag+"-recrutement-marche");Click("recruit-tab-Synthèse");
                    });
                    if(Path.GetFileName(Output).Contains("-polish")){
                        list.Add(()=>{Click("recruit-hub-mission-"+role);Root.Q<LongField>("scout-mission-wage").value=0;});
                        list.Add(()=>{
                            var notice=Root.Q("scout-mission-blocked");var send=Root.Q<Button>("scout-mission-send");
                            if(notice==null||notice.resolvedStyle.display==DisplayStyle.None||!Root.worldBound.Contains(notice.worldBound.min)||!Root.worldBound.Contains(notice.worldBound.max)||send.enabledSelf)throw new Exception("Le refus de mission doit être expliqué près du bouton visible");
                            Capture(screen.tag+"-recrutement-mission-plafond");Root.Q<LongField>("scout-mission-wage").value=1000;
                            if(!send.enabledSelf||notice.style.display.value!=DisplayStyle.None)throw new Exception("Le plafond positif doit réactiver la proposition sans envoyer la mission");
                            Call("CloseModal");
                        });
                        list.Add(()=>Click("recruit-tab-Rapports"));
                        list.Add(()=>{Capture(screen.tag+"-recrutement-rapports");var action=Root.Query<Button>().ToList().First(b=>b.text=="Contacter l’agent");action.GetFirstAncestorOfType<ScrollView>().ScrollTo(action);});
                        list.Add(()=>Capture(screen.tag+"-recrutement-rapport-actions"));
                        continue;
                    }
                    list.Add(()=>{cash=App.Career.life.cash;missions=App.Career.world.scoutMissions.Count;Click("recruit-hub-mission-"+role);});
                    list.Add(()=>{
                        if(Root.Q<DropdownField>("scout-mission-role")?.value!=searchRole||App.Career.life.cash!=cash||App.Career.world.scoutMissions.Count!=missions)throw new Exception("Préparation mission : poste ou engagement financier incorrect");
                        Capture(screen.tag+"-recrutement-mission");Call("CloseModal");Click("recruit-hub-free");
                        audit.AppendLine("Besoin → marché au bon poste ; mission préremplie sans dépense avant confirmation : "+screen.tag);
                    });
                    list.Add(()=>{
                        var rows=Root.Q<ListView>("recruit-list")?.itemsSource;
                        if(Root.Q<DropdownField>("recruit-market")?.value!="Libres"||rows==null||rows.Count==0||rows.Cast<Touchline.Core.PlayerData>().Any(p=>p.team!="free"))throw new Exception("Raccourci joueurs libres incohérent");
                        Capture(screen.tag+"-recrutement-libres");Click("recruit-tab-Synthèse");
                    });
                    list.Add(()=>{
                        Call("OpenRecruitmentMarket","Tous","Tous");Root.Q<Foldout>("recruit-advanced").value=true;
                        Root.Q<DropdownField>("recruit-country").value="Norvège";
                    });
                    list.Add(()=>{
                        var rows=Root.Q<ListView>("recruit-list")?.itemsSource;
                        var country=typeof(ClubData).GetField("country");
                        if(rows==null||country==null||!rows.Cast<PlayerData>().Any(p=>p.team=="2980")
                            ||rows.Cast<PlayerData>().Any(p=>!App.Database.clubs.Any(c=>c.id==p.team&&(string)country.GetValue(c)=="Norvège")))
                            throw new Exception("Le territoire Norvège doit inclure Bodø/Glimt sans joueurs d'autres pays");
                        Capture(screen.tag+"-recrutement-norvege");cash=App.Career.life.cash;missions=App.Career.world.scoutMissions.Count;
                        Call("NewScoutingMission");
                    });
                    list.Add(()=>{
                        if(Root.Q<DropdownField>("scout-mission-country")?.value!="Norvège"||App.Career.life.cash!=cash||App.Career.world.scoutMissions.Count!=missions)
                            throw new Exception("La mission doit conserver le territoire sans engagement préalable");
                        Capture(screen.tag+"-recrutement-mission-norvege");Call("CloseModal");Click("recruit-tab-Synthèse");
                        audit.AppendLine("Norvège : joueurs retrouvés, territoire conservé dans la mission sans dépense : "+screen.tag);
                    });
                    list.Add(()=>Click("recruit-hub-reports"));
                }else{
                    list.Add(()=>{Capture(screen.tag+"-recrutement-marche");Click("recruit-tab-Rapports");});
                }
                list.Add(()=>{
                    if(Root.Q("scout-report-"+reportedPlayer)==null)throw new Exception("Rapport connu inaccessible");
                    Capture(screen.tag+"-recrutement-rapports");
                    if(App.Career.revealAttributes)throw new Exception("Le parcours a désactivé le masquage des attributs");
                    audit.AppendLine("Rapport observé accessible, option d'attributs masqués préservée : "+screen.tag);
                });
                if(!reference){
                    list.Add(()=>{
                        var report=Root.Q("scout-report-"+reportedPlayer);
                        var agent=report.Query<Button>().ToList().Single(b=>b.text=="Contacter l’agent");
                        report.GetFirstAncestorOfType<ScrollView>().ScrollTo(agent);
                    });
                    list.Add(()=>Capture(screen.tag+"-recrutement-rapport-actions"));
                }
            }
            return list;
        }

        // film/ui-recruitment-depth… : report cards with ranges/grades, sort, generated league market, comparison.
        static List<Action> BuildFocusedDepthSteps()
        {
            audit.AppendLine("Parcours ciblé profondeur du recrutement : cartes de rapport (fourchettes, note A–E, forces/faiblesses, concurrence), tri, ligue générée Suède, comparaison ; deux résolutions natives, carrière en mémoire.");
            var list=new List<Action>();string generated=null,real=null;
            list.Add(()=>{
                var safe=typeof(TouchlineApp).GetProperty("VisualValidation",BindingFlags.Instance|BindingFlags.NonPublic);
                if(safe==null||!(bool)safe.GetValue(App))throw new Exception("La validation doit isoler les sauvegardes personnelles");
                var c=App.Career;c.EnsureWorld(App.Database);c.revealAttributes=false;
                if(!App.Database.leagues.Any(l=>GeneratedWorld.IsGeneratedLeague(l)))throw new Exception("Ligues générées absentes du catalogue chargé");
                var g=App.Database.players.First(p=>p.team.StartsWith("fic-swe1-",StringComparison.Ordinal)&&p.age>=20&&!p.Goalkeeper);generated=g.id;
                var r=App.Database.players.First(p=>p.team!=c.club&&p.team!="free"&&p.team!="retired"&&!p.team.StartsWith("academy-")&&!GeneratedWorld.IsGenerated(p.team)&&p.age>=18&&!p.Goalkeeper);real=r.id;
                int day=c.life.day;
                c.world.reports.Add(new ScoutReport{player=generated,club=c.club,scout="Cellule de test",started=day-20,due=day-1,lastObserved=day-1,confidence=90,judging=12,depth=1,estimate=60,potential=66,uncertainty=4,potentialUncertainty=7,advice="Rapport de contrôle."});
                c.world.reports.Add(new ScoutReport{player=real,club=c.club,scout="Cellule de test",started=day-200,due=day-190,lastObserved=day-190,confidence=90,judging=16,estimate=70,potential=72,uncertainty=2,potentialUncertainty=5,advice="Rapport ancien de contrôle."});
                if(!c.shortlist.Contains(generated))c.ToggleShortlist(generated);if(!c.shortlist.Contains(real))c.ToggleShortlist(real);
                c.rivalBids.Add(new RivalBid{player=real,club=App.Database.clubs.First(t=>t.id!=c.club&&t.id!=r.team&&t.playable).id,seller=r.team,day=day,decision=day+10,fee=r.value,wage=r.wage});
                audit.AppendLine("Joueur fictif "+generated+" ("+g.team+"), joueur réel "+real+" avec rapport ancien et offre rivale fictive de test.");
            });
            foreach(var s in Screens){
                var screen=s;
                list.Add(()=>{Resize(screen.width,screen.height);SetField("scoutReportFilter","Tous");SetField("recruitmentTab","Rapports");Call("Navigate","Recrutement");});
                list.Add(()=>{
                    foreach(var id in new[]{generated,real}){if(Root.Q("scout-grade-"+id)==null||Root.Q("scout-range-ability-"+id)==null)throw new Exception("Carte de rapport incomplète : "+id);}
                    if(Root.Q("scout-rival-"+real)==null)throw new Exception("Concurrence non affichée");
                    var card=App.Career.ScoutReportCard(App.Database,generated);var old=App.Career.ScoutReportCard(App.Database,real);
                    audit.AppendLine(screen.tag+" : fictif note "+card.grade+" niveau "+card.ability+" potentiel "+card.potential+" familiarité "+card.familiarityLabel+" ; ancien note "+old.grade+" niveau "+old.ability+" à actualiser="+old.stale);
                    Capture(screen.tag+"-profondeur-rapports");
                    var target=Root.Q("scout-report-"+generated);target.GetFirstAncestorOfType<ScrollView>()?.ScrollTo(target);
                });
                list.Add(()=>{Capture(screen.tag+"-profondeur-carte-fictive");Root.Q<DropdownField>("scout-report-sort").value="Note";});
                list.Add(()=>{Capture(screen.tag+"-profondeur-tri-note");Call("OpenRecruitmentMarket","Tous","Tous");Root.Q<Foldout>("recruit-advanced").value=true;Root.Q<DropdownField>("recruit-country").value="Suède";});
                list.Add(()=>{
                    var rows=Root.Q<ListView>("recruit-list")?.itemsSource;
                    if(rows==null||!rows.Cast<PlayerData>().Any(p=>p.team.StartsWith("fic-swe1-",StringComparison.Ordinal)))throw new Exception("Le territoire Suède doit inclure la ligue générée");
                    Root.Q<Foldout>("recruit-advanced").value=false;
                    audit.AppendLine(screen.tag+" : Suède "+rows.Count+" profils, dont "+rows.Cast<PlayerData>().Count(p=>GeneratedWorld.IsGenerated(p.team))+" fictifs.");
                    Capture(screen.tag+"-profondeur-marche-suede");Call("ComparePlayer",generated);
                });
                list.Add(()=>{if(Root.Q("player-comparison")==null)throw new Exception("Comparaison absente");Capture(screen.tag+"-profondeur-comparaison");Call("CloseModal");});
            }
            return list;
        }

        // film/ui-recruitment-cell… : scouting department, assignment, recommendations, report personality/interest,
        // signings' settling and the negotiation's interest line. Synthetic in-memory data, never a personal save.
        static List<Action> BuildFocusedCellSteps()
        {
            audit.AppendLine("Parcours ciblé cellule de recrutement : recruteurs et affectation, recommandations, personnalité/intérêt dans le rapport, intégration des recrues, intérêt en négociation ; deux résolutions natives, données synthétiques en mémoire.");
            var list=new List<Action>();string target=null,local=null,extraKey=null;
            list.Add(()=>{
                var safe=typeof(TouchlineApp).GetProperty("VisualValidation",BindingFlags.Instance|BindingFlags.NonPublic);
                if(safe==null||!(bool)safe.GetValue(App))throw new Exception("La validation doit isoler les sauvegardes personnelles");
                var c=App.Career;var db=App.Database;c.EnsureWorld(db);c.revealAttributes=false;c.match=null;c.world.managerStatus="employed";c.EnsureStaffMarket(db);
                string home=ScoutingGeography.Country(db,db.clubs.First(x=>x.id==c.club));
                string abroad=home=="Espagne"?"Italie":"Espagne";
                var abroadClubs=new HashSet<string>(ScoutingGeography.ClubIds(db,abroad));var homeClubs=new HashSet<string>(ScoutingGeography.ClubIds(db,home));
                bool Usable(PlayerData p)=>p.team!=c.club&&p.age>=20&&p.age<=28&&!p.Goalkeeper&&p.wage>0;
                var t=db.players.Where(p=>abroadClubs.Contains(p.team)&&Usable(p)).OrderByDescending(p=>p.rating).First();target=t.id;
                var l=db.players.Where(p=>homeClubs.Contains(p.team)&&Usable(p)).OrderByDescending(p=>p.rating).First();local=l.id;
                if(c.ScoutSlots<2)throw new Exception("Le club de validation doit pouvoir employer deux recruteurs");
                var extra=c.staffMarket.First(m=>m.role=="scout"&&m.club==null);extra.club=c.club;extra.wage=Math.Max(extra.wage,400);extra.until=c.life.day+700;c.life.staff.members.Add(extra);extraKey=extra.id;
                c.AssignScout(db,c.ScoutingDepartment(db).First(x=>x.chief).key,"needs");c.AssignScout(db,extraKey,"territory",abroad);
                c.scoutRegions.Add(new ScoutRegionKnowledge{staff=extraKey,country=abroad,level=46});
                int day=c.life.day;
                c.world.reports.Add(new ScoutReport{player=target,club=c.club,scout=extra.name,mission="assign-"+extraKey,started=day-25,due=day-2,lastObserved=day-2,confidence=90,judging=extra.judging,depth=1,estimate=t.rating+t.development,potential=Math.Max(t.potential,t.rating),uncertainty=2.4f,potentialUncertainty=6.7f,advice="Rapport de contrôle."});
                c.world.reports.Add(new ScoutReport{player=local,club=c.club,scout=c.Staff("scout").name,started=day-40,due=day-20,lastObserved=day-20,confidence=90,judging=c.Staff("scout").judging,estimate=l.rating+l.development,potential=Math.Max(l.potential,l.rating),uncertainty=2.4f,potentialUncertainty=5.4f,advice="Rapport de contrôle."});
                c.scoutRecommendations.Add(local);c.scoutRecommendations.Add(target);
                var own=db.Squad(c.club).Where(p=>!p.Goalkeeper).OrderByDescending(p=>p.rating).Take(2).ToArray();
                c.signings.Add(new SigningRecord{player=own[0].id,from="test",fromCountry=abroad,day=day-30,settleDays=120,penalty=.05f,knowledge=90,estimate=own[0].rating+own[0].development-2,actual=own[0].rating+own[0].development});
                c.signings.Add(new SigningRecord{player=own[1].id,from="test",fromCountry=home,day=day-200,settleDays=45,penalty=.02f,knowledge=0,actual=own[1].rating+own[1].development,settled=true});
                audit.AppendLine("Cible étrangère "+target+" ("+t.team+", "+abroad+"), cible locale "+local+" ; deux recrues synthétiques ("+own[0].id+", "+own[1].id+") ; recruteur ajouté "+extraKey+".");
            });
            foreach(var s in Screens){
                var screen=s;
                list.Add(()=>{Resize(screen.width,screen.height);SetField("recruitmentTab","Synthèse");Call("Navigate","Recrutement");});
                list.Add(()=>{
                    if(Root.Q("recruit-hub-recommendation-"+target)==null)throw new Exception("Recommandation de la cellule absente");
                    if(Root.Q("recruit-hub-signings")==null)throw new Exception("Intégration des recrues absente");
                    var section=Root.Q("recruit-hub-recommendations");section.GetFirstAncestorOfType<ScrollView>()?.ScrollTo(section);
                });
                list.Add(()=>{Capture(screen.tag+"-cellule-recommandations");var section=Root.Q("recruit-hub-signings");section.GetFirstAncestorOfType<ScrollView>()?.ScrollTo(section);});
                list.Add(()=>{Capture(screen.tag+"-cellule-integration");SetField("recruitmentTab","Missions");Call("Navigate","Recrutement");});
                list.Add(()=>{var panel=Root.Q("scout-department");panel?.GetFirstAncestorOfType<ScrollView>()?.ScrollTo(panel);});
                list.Add(()=>{Capture(screen.tag+"-cellule-chef");var member=Root.Q("scout-member-"+extraKey);member?.GetFirstAncestorOfType<ScrollView>()?.ScrollTo(member);});
                list.Add(()=>{
                    if(Root.Q("scout-department")==null||Root.Q("scout-member-"+extraKey)==null||Root.Q("scout-skill-potential-"+extraKey)==null)throw new Exception("Cellule de recrutement incomplète");
                    var assignment=Root.Q<Label>("scout-assignment-"+extraKey);if(assignment==null||!assignment.text.Contains("Exploration"))throw new Exception("Affectation non affichée");
                    Capture(screen.tag+"-cellule-recruteurs");Click("scout-assign-"+extraKey);
                });
                list.Add(()=>{if(Root.Q("scout-assignment-dialog")==null||Root.Q("scout-assignment-hint")==null)throw new Exception("Dialogue d’affectation absent");Capture(screen.tag+"-cellule-affectation");Call("CloseModal");SetField("scoutReportFilter","Tous");SetField("recruitmentTab","Rapports");Call("Navigate","Recrutement");});
                list.Add(()=>{
                    foreach(var name in new[]{"scout-personality-","scout-interest-","scout-comparable-"})if(Root.Q(name+target)==null)throw new Exception("Rapport incomplet : "+name+target);
                    var card=App.Career.ScoutReportCard(App.Database,target);
                    audit.AppendLine(screen.tag+" : note "+card.grade+" niveau "+card.ability+" ambition "+card.ambition+" adaptabilité "+card.adaptability+" · "+card.comparable+" · intérêt "+card.interest);
                    var report=Root.Q("scout-report-"+target);report.GetFirstAncestorOfType<ScrollView>()?.ScrollTo(report);
                });
                list.Add(()=>{Capture(screen.tag+"-cellule-rapport");Call("TransferDialog",target);});
                list.Add(()=>{if(Root.Q("negotiation-interest")==null)throw new Exception("Intérêt du joueur absent de la négociation");Capture(screen.tag+"-cellule-negociation");Call("CloseModal");});
            }
            return list;
        }

        static List<Action> BuildFocusedLoanSteps()
        {
            audit.AppendLine("Parcours ciblé : droits du prêteur, fiche, contrat de prêt et ancien accord. Carrière de validation en mémoire ; six captures natives, pas de sauvegarde personnelle ni d’achat exécuté.");
            var list=new List<Action>();string playerId=null,ownerName=null;Employment loan=null;TransferOffer legacy=null;long cash=0;
            list.Add(()=>{
                var validation=typeof(TouchlineApp).GetProperty("VisualValidation",BindingFlags.Instance|BindingFlags.NonPublic);
                if(validation==null||!(bool)validation.GetValue(App))throw new Exception("La carrière de validation sans sauvegarde est obligatoire");
                var career=App.Career;career.EnsureWorld(App.Database);career.match=null;career.world.managerStatus="employed";career.life.managerBanUntil=career.life.day;
                var player=App.Database.Squad(career.club).Where(p=>!p.Goalkeeper).OrderBy(p=>p.id,StringComparer.Ordinal).First();playerId=player.id;
                var owner=App.Database.clubs.Where(c=>c.id!=career.club&&c.playable).OrderBy(c=>c.id,StringComparer.Ordinal).First();ownerName=owner.name;
                loan=career.Contract(App.Database,playerId);loan.parent=owner.id;loan.parentUntil=loan.until;loan.club=career.club;loan.loanUntil=career.life.day+90;loan.until=loan.loanUntil;
                // Representative UI fixture only: borrowing does not create ownership.
                loan.terms=new MarketTerms{loanEndDay=loan.loanUntil,loanWagePercent=50,optionFee=100000};
                legacy=new TransferOffer{player=playerId,seller=career.club,destination=career.club,status="accepted",renewal=true,wage=player.wage,years=3,role="starter",due=career.life.day};
                career.world.offers.Add(legacy);cash=career.life.cash;
            });
            foreach(var s in Screens){
                var screen=s;
                list.Add(()=>{Resize(screen.width,screen.height);Call("Navigate","Club");App.PlayerProfile(playerId);});
                list.Add(()=>{
                    if(!Root.Query<Button>().ToList().Any(b=>b.text=="Contrat de prêt")||Root.Query<Button>().ToList().Any(b=>b.text=="Contrat / prolonger"))
                        throw new Exception("La fiche du joueur emprunté propose encore une prolongation");
                    Capture(screen.tag+"-fiche-joueur-prete");Click("Contrat de prêt");
                });
                list.Add(()=>{
                    var panel=Root.Q("loan-contract-ownership");var purchase=panel?.Q<Button>("loan-contract-purchase");
                    if(panel==null||purchase==null||!purchase.enabledSelf||!panel.Query<TextElement>().ToList().Any(t=>t.text==ownerName))
                        throw new Exception("Le contrat de prêt ne montre pas le propriétaire et l’option d’achat disponible");
                    audit.AppendLine(screen.tag+" : fiche sans prolongation, contrat du prêteur et option de 100 000 € accessibles.");
                    Capture(screen.tag+"-contrat-pret");Call("CloseModal");Call("TransferAgreementReview",legacy);
                });
                list.Add(()=>{
                    var sign=Root.Q<Button>("agreement-sign-transfer");
                    if(sign==null||sign.enabledSelf||!App.Career.HasActiveLoan(playerId)||loan.parent==null||App.Career.life.cash!=cash)
                        throw new Exception("L’ancien accord ne protège pas les droits ou la trésorerie du prêt");
                    audit.AppendLine(screen.tag+" : ancien accord affiché, signature désactivée, parent et trésorerie inchangés.");
                    Capture(screen.tag+"-ancien-accord-bloque");Call("CloseModal");
                });
            }
            return list;
        }

        static void AssertQuickPressing(int level,bool paused)
        {
            float expected=level==2?.8f:.5f;float speed=Arena.Speed;
            if(Arena.Paused!=paused)throw new Exception("L'ouverture des consignes a changé la pause");
            Click("quick-tactic-Pressing-"+level);
            if(App.Career.tactic.pressing!=expected||App.Career.match.homeTactic.pressing!=expected||Arena.Paused!=paused||Arena.Speed!=speed||Root.Q("match-instructions")!=null)
                throw new Exception("Consigne en deux touches : application/retour/pause/vitesse incorrects");
            audit.AppendLine("Consigne en deux touches validée : pressing "+expected.ToString(CultureInfo.InvariantCulture)+", pause "+paused+", vitesse "+speed.ToString(CultureInfo.InvariantCulture)+", retour au match.");
        }

        static void Tick()
        {
            if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying)return;
            float now=Time.realtimeSinceStartup;if(startedAt<0){startedAt=now;Application.logMessageReceived+=(m,st,t)=>{if(log.Length<200000)log.AppendLine(t+": "+m+(t==LogType.Exception?"\n"+st:""));};}
            if(TouchlineApp.Instance==null){if(now-startedAt>BootTimeoutSeconds)Finish("TouchlineApp absent");return;}
            if(now-startedAt>RunTimeoutSeconds){Finish("délai dépassé à l'étape "+stage);return;}
            if(++frames<(FocusedTactics||FocusedRecruitment||FocusedLoan||FocusedNegotiation||FocusedBoard||FocusedDepth||FocusedCareer||FocusedImmersion?FocusedStepFrames:StepFrames)||now-stepAt<StepSeconds)return;frames=0;stepAt=now;
            steps??=BuildSteps();
            if(stage<0)stage=0;
            if(stage>=steps.Count){Finish(failures==0?null:failures+" étape(s) en échec");return;}
            File.AppendAllText(Path.Combine(Output,"progress.txt"),DateTime.UtcNow.ToString("O")+" START "+stage+"\n");
            var timer=System.Diagnostics.Stopwatch.StartNew();
            try{steps[stage]();}
            catch(Exception e){failures++;var inner=e is TargetInvocationException t&&t.InnerException!=null?t.InnerException:e;audit.AppendLine("ERREUR étape "+stage+" : "+inner.Message);Debug.LogException(inner);}
            File.AppendAllText(Path.Combine(Output,"progress.txt"),DateTime.UtcNow.ToString("O")+" END "+stage+" "+timer.ElapsedMilliseconds+" ms\n");
            File.WriteAllText(Path.Combine(Output,"audit-progress.txt"),audit.ToString());stage++;
        }

        static void Finish(string failure)
        {
            SessionState.SetBool(Flag,false);
            try{
                File.WriteAllText(Path.Combine(Output,"audit.txt"),"Audit d'interface Touchline · 1 dp = "+DevicePixelsPerDp+" px physiques · seuils : cible tactile "+MinTouchDp+" dp, texte "+MinTextSp+" sp\n"+(failure!=null?"ÉCHEC : "+failure+"\n":"")+"\n"+audit);
                File.WriteAllText(Path.Combine(Output,"unity-messages.txt"),log.ToString());
            }catch(Exception e){Debug.LogException(e);}
            Debug.Log(failure==null?"TOUCHLINE_UI_SCREENS_OK "+Output:"TOUCHLINE_UI_SCREENS_FAILED "+failure);
            EditorApplication.Exit(failure==null?0:1);
        }

        static void Capture(string name)
        {
            var previous=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();File.WriteAllBytes(Path.Combine(Output,name+".png"),image.EncodeToPNG());
            UnityEngine.Object.Destroy(image);RenderTexture.active=previous;Audit(name);
        }

        static bool Visible(VisualElement e)
        {
            for(var v=e;v!=null;v=v.parent)if(v.resolvedStyle.display==DisplayStyle.None||v.resolvedStyle.visibility==Visibility.Hidden||v.resolvedStyle.opacity<.05f)return false;
            return true;
        }
        static string Short(string text){text=(text??"").Replace("\n"," / ");return text.Length>48?text.Substring(0,47)+"…":text;}

        // Mesure ce qui est réellement affiché : boutons trop petits pour un doigt,
        // textes trop petits, éléments coupés par le bord de l'écran.
        public static void Audit(string name)
        {
            var c=CultureInfo.InvariantCulture;var root=Root;var screen=root.worldBound;float dp=Document.panelSettings.scale/DevicePixelsPerDp;
            var buttons=root.Query<Button>().ToList().Where(b=>Visible(b)&&b.worldBound.width>1&&b.worldBound.height>1&&b.worldBound.Overlaps(screen)).ToList();
            var small=buttons.Where(b=>b.worldBound.height*dp<MinTouchDp-.5f||b.worldBound.width*dp<MinTouchDp-.5f).ToList();
            var texts=root.Query<TextElement>().ToList().Where(t=>Visible(t)&&!string.IsNullOrWhiteSpace(t.text)&&t.worldBound.Overlaps(screen)).ToList();
            var tiny=texts.Where(t=>t.resolvedStyle.fontSize*dp<MinTextSp-.05f).ToList();
            var clipped=texts.Where(t=>t.worldBound.xMax>screen.xMax+1||t.worldBound.xMin<screen.xMin-1).ToList();
            float minFont=texts.Count==0?0:texts.Min(t=>t.resolvedStyle.fontSize)*dp;
            audit.AppendLine("## "+name+" · "+target.width+"×"+target.height+" px · échelle "+Document.panelSettings.scale.ToString("0.00",c)+" · 1 unité UI = "+dp.ToString("0.00",c)+" dp · largeur logique "+screen.width.ToString("0",c)+" · hauteur "+screen.height.ToString("0",c));
            audit.AppendLine("   boutons visibles "+buttons.Count+" · < "+MinTouchDp+" dp : "+small.Count+" · textes "+texts.Count+" · < "+MinTextSp+" sp : "+tiny.Count+" · plus petit texte "+minFont.ToString("0.0",c)+" sp · coupés au bord : "+clipped.Count);
            foreach(var g in small.GroupBy(b=>Short(string.IsNullOrEmpty(b.text)?"#"+b.name:b.text)).Take(25)){var b=g.First();audit.AppendLine("   petit bouton ×"+g.Count()+" « "+g.Key+" » "+(b.worldBound.width*dp).ToString("0",c)+"×"+(b.worldBound.height*dp).ToString("0",c)+" dp");}
            foreach(var g in tiny.GroupBy(t=>string.Join(".",t.GetClasses().Take(2))+" "+(t.resolvedStyle.fontSize*dp).ToString("0.0",c)+" sp").OrderBy(g=>g.Key).Take(20))audit.AppendLine("   petit texte ×"+g.Count()+" ["+g.Key+"] ex. « "+Short(g.First().text)+" »");
            foreach(var t in clipped.Take(10))audit.AppendLine("   coupé « "+Short(t.text)+" » x "+t.worldBound.xMin.ToString("0",c)+"–"+t.worldBound.xMax.ToString("0",c));
            audit.AppendLine();
        }
    }
}

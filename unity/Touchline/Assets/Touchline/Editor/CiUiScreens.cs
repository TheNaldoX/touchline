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
            if(FocusedTactics)return BuildFocusedTacticSteps();
            if(FocusedRecruitment)return BuildFocusedRecruitmentSteps();
            if(FocusedLoan)return BuildFocusedLoanSteps();
            if(FocusedNegotiation)return BuildFocusedNegotiationSteps();
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
                        float offset=scroll.scrollOffset.y+candidate.worldBound.yMin-scroll.contentViewport.worldBound.yMin;
                        scroll.scrollOffset=new Vector2(0,Mathf.Clamp(offset,0,scroll.verticalScroller.highValue));
                        audit.AppendLine("Piste : défilement "+scroll.scrollOffset.y+" / "+scroll.verticalScroller.highValue);
                    });
                    list.Add(()=>{
                        var scroll=Root.Q<ScrollView>("recruit-hub");var profile=Root.Q<Button>("recruit-hub-profile-"+reportedPlayer);
                        if(profile==null||!profile.worldBound.Overlaps(scroll.contentViewport.worldBound))throw new Exception("La piste connue reste hors du défilement visible");
                        Capture(screen.tag+"-recrutement-pistes");
                        Root.Q<ScrollView>("recruit-hub").scrollOffset=Vector2.zero;
                    });
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
            if(++frames<(FocusedTactics||FocusedRecruitment||FocusedLoan||FocusedNegotiation?FocusedStepFrames:StepFrames)||now-stepAt<StepSeconds)return;frames=0;stepAt=now;
            steps??=BuildSteps();
            if(stage<0)stage=0;
            if(stage>=steps.Count){Finish(failures==0?null:failures+" étape(s) en échec");return;}
            try{steps[stage]();}
            catch(Exception e){failures++;var inner=e is TargetInvocationException t&&t.InnerException!=null?t.InnerException:e;audit.AppendLine("ERREUR étape "+stage+" : "+inner.Message);Debug.LogException(inner);}
            stage++;
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

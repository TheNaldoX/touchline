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
        static bool FocusedLoan=>Path.GetFileName(Output).StartsWith("ui-loan-focus-",StringComparison.Ordinal);
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
            if(FocusedLoan)return BuildFocusedLoanSteps();
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
            if(++frames<((FocusedTactics||FocusedLoan)?FocusedStepFrames:StepFrames)||now-stepAt<StepSeconds)return;frames=0;stepAt=now;
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

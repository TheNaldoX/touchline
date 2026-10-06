using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Touchline.Editor
{
    [InitializeOnLoad] public static class NativeSmoke
    {
        static int stage,frames,lastFrame=-1;static float profileFitness;static int substitutionStage;static string substituteId,conversationId;static int conversationMessages,facilityDue;static bool facilityReviewOpened;
        static RenderTexture target;
        static string output;
        static NativeSmoke(){EditorApplication.update+=Tick;}
        public static void Run(){ProjectBuilder.Configure();SessionState.SetBool("TouchlineSmoke",true);EditorApplication.isPlaying=true;}
        static void Click(string text){var root=TouchlineApp.Instance.GetComponent<UIDocument>().rootVisualElement;var button=root.Query<Button>().ToList().FirstOrDefault(b=>b.text==text||b.name==text);if(button==null&&text!="Plus"&&root.Query<Button>().ToList().Any(b=>b.text=="Plus")){Click("Plus");button=root.Query<Button>().ToList().FirstOrDefault(b=>b.text==text||b.name==text);}if(button==null)throw new Exception("Bouton absent : "+text);using(var e=NavigationSubmitEvent.GetPooled()){e.target=button;button.SendEvent(e);}}
        static void Capture(string name){var previous=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();
            if(name=="native-match.png"&&image.GetPixels().Count(c=>c.g>c.r*1.25f&&c.g>c.b*1.12f)<target.width*target.height/12)throw new Exception("Final UI does not show the pitch");
            File.WriteAllBytes(Path.Combine(output,name),image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);RenderTexture.active=previous;}
        static void Resize(int width,int height){var old=target;target=new RenderTexture(width,height,24);target.Create();TouchlineApp.Instance.GetComponent<UIDocument>().panelSettings.targetTexture=target;if(old!=null){old.Release();UnityEngine.Object.DestroyImmediate(old);}}
        static void CheckNavigation(){var root=TouchlineApp.Instance.GetComponent<UIDocument>().rootVisualElement;foreach(var button in root.Q(className:"navigation").Query<Button>().ToList()){var bounds=button.worldBound;if(bounds.width<40||bounds.height<30||bounds.xMin<0||bounds.yMin<0||bounds.xMax>root.worldBound.xMax+1||bounds.yMax>root.worldBound.yMax+1)throw new Exception("Navigation hors écran : "+button.text);}}
        static void Tick()
        {
            if(!SessionState.GetBool("TouchlineSmoke",false)||!EditorApplication.isPlaying||TouchlineApp.Instance==null)return;
            if(Time.frameCount==lastFrame)return;lastFrame=Time.frameCount;if(++frames<12)return;frames=0;
            try{
                output=Path.GetFullPath("../../artifacts/unity");Directory.CreateDirectory(output);var app=TouchlineApp.Instance;var document=app.GetComponent<UIDocument>();
                switch(stage++){
                    case 0:document.panelSettings=UnityEngine.Object.Instantiate(document.panelSettings);document.panelSettings.clearColor=true;document.panelSettings.colorClearValue=Color.clear;Resize(1280,900);break;
                    case 1:Capture("native-club.png");Click("Tactique");break;
                    case 2:if(document.rootVisualElement.Query<VisualElement>(className:"token").ToList().Count!=11)throw new Exception("Onze incomplet à l’écran.");Capture("native-tactics.png");Click("Match");break;
                    case 3:if(app.Career.match==null){app.Career.life.day=app.Career.life.nextFixture;Click("Entrer sur le terrain");}var arena=UnityEngine.Object.FindFirstObjectByType<MatchArena>();arena.Broadcast.Enabled=false;Click("match-playback");break;
                    case 4:var view=UnityEngine.Object.FindFirstObjectByType<MatchArena>();if(app.Career.match.clock<20){stage--;return;}if(UnityEngine.Object.FindObjectsByType<PlayerView>(FindObjectsSortMode.None).Length!=22)throw new Exception("22 modèles requis.");view.Paused=true;view.ToggleTacticalOverlay();Capture("native-match.png");profileFitness=app.Career.match.actors[9].fitness;app.Career.match.actors[9].fitness=63;app.PlayerProfile(app.Career.match.actors[9].id);break;
                    case 5:if(!document.rootVisualElement.Q("player-profile").Query<Label>(className:"profile-gauge-value").ToList().Any(l=>l.text=="63 %"))throw new Exception("Profile ignores live match condition");Capture("native-player.png");app.Career.match.actors[9].fitness=profileFitness;app.Career.match.restart=0;app.Career.match.phase="play";Click("Fermer");Click("Tactique");break;
                    case 6:var benchButton=document.rootVisualElement.Query<Button>().ToList().First(b=>b.name.StartsWith("assign-")&&b.enabledInHierarchy);substituteId=benchButton.name;Click(benchButton.name);break;
                    case 7:
                        if(substitutionStage==0){Capture("native-substitution.png");if(app.Career.match.substitutions[0]!=0||app.Career.match.pendingSubstitutions.Count!=1||document.rootVisualElement.Q("pending-changes")==null)throw new Exception("Substitution should be prepared, not applied mid-action");var outgoing=app.Career.match.pendingSubstitutions[0].outgoing;Click("cancel-sub-"+outgoing);if(app.Career.match.pendingSubstitutions.Count!=0)throw new Exception("Pending substitution cancellation failed");Click(substituteId);Click("tactics-return-match");substitutionStage++;stage--;break;}
                        if(document.rootVisualElement.Q<Button>("match-pending-substitutions").resolvedStyle.display==DisplayStyle.None)throw new Exception("Prepared change banner missing");Capture("native-pending-match.png");app.Career.match.phase="throw-in";app.Career.match.restart=2;UnityEngine.Object.FindFirstObjectByType<MatchArena>().Simulation.Advance(.1);if(app.Career.match.substitutions[0]!=1||app.Career.match.pendingSubstitutions.Count!=0)throw new Exception("Substitution not applied at stoppage");Click("Bureau");Resize(900,1600);break;
                    case 8:CheckNavigation();Capture("native-portrait.png");Resize(1600,700);break;
                    case 9:CheckNavigation();Capture("native-landscape.png");Click("Match");Click("Analyse");break;
                    case 10:Capture("native-analysis.png");Click("Fermer");var sim=UnityEngine.Object.FindFirstObjectByType<MatchArena>().Simulation;sim.PlayToEnd();Click("Bureau");int day=app.Career.life.day;Click("Continuer  →");if(app.Career.life.day!=day+1)throw new Exception("Continuer doit avancer d’un jour.");break;
                    case 11:Capture("native-dashboard.png");app.Career.OpenInjury(app.Database,app.Career.lineup[9],"bruise");Click("Santé");break;
                    case 12:Capture("native-medical.png");var medicalButton=document.rootVisualElement.Query<Button>().ToList().Last(b=>b.text=="Consulter / décider");using(var medicalClick=NavigationSubmitEvent.GetPooled()){medicalClick.target=medicalButton;medicalButton.SendEvent(medicalClick);}break;
                    case 13:Capture("native-medical-decision.png");Click("Soins conservateurs");if(app.Career.Injury(app.Career.lineup[9]).treatment!="conservative")throw new Exception("Protocole non enregistré.");Click("Messages");break;
                    case 14:Capture("native-inbox.png");Click("Contacter un joueur");break;
                    case 15:var contact=document.rootVisualElement.Query<Button>().ToList().First(b=>b.name?.StartsWith("inbox-contact-",StringComparison.Ordinal)==true&&b.enabledInHierarchy);conversationId=contact.name.Substring("inbox-contact-".Length);Click(contact.name);break;
                    case 16:
                        var conversation=document.rootVisualElement.Q("player-conversation");if(conversation==null)throw new Exception("Le contact ne mène pas à sa discussion privée.");
                        conversation.Q<DropdownField>("conversation-channel").index=1;conversation.Q<DropdownField>("conversation-topic-picker").value="Écouter et soutenir";
                        var send=conversation.Q<Button>("conversation-send");if(!send.enabledInHierarchy||send.text!="Aborder pendant l’appel")throw new Exception("Le sujet et le canal ne préparent pas un appel de soutien disponible.");
                        conversationMessages=app.Career.life.messages.Count(m=>m.player==conversationId);Capture("native-conversation.png");Click(send.name);
                        var exchange=app.Career.life.messages.Where(m=>m.player==conversationId).TakeLast(2).ToArray();if(app.Career.life.messages.Count(m=>m.player==conversationId)!=conversationMessages+2||app.Career.Person(conversationId).lastTalk!=app.Career.life.day||exchange[0].sender!="Vous"||exchange[0].subject!="Appel • compte rendu"||exchange[1].subject!="Après notre appel")throw new Exception("L’appel ne conserve pas exactement la demande et la réponse du bon joueur.");break;
                    case 17:Capture("native-call.png");Click("Fermer");Click("Infrastructures");break;
                    case 18:Capture("native-facilities.png");Click("Demander au propriétaire");facilityDue=app.Career.life.projects.Single(p=>p.status=="requested").due;break;
                    case 19:
                        // Continue is deliberately debounced; wait for each real
                        // day instead of submitting three clicks in one frame.
                        if(app.Career.life.day<facilityDue){var next=document.rootVisualElement.Q<Button>("manager-continue");if(next.enabledInHierarchy)Click(next.name);stage--;return;}
                        if(!facilityReviewOpened){Click("Infrastructures");facilityReviewOpened=true;stage--;return;}
                        Capture("native-board-reply.png");var approved=document.rootVisualElement.Query<Button>().ToList().FirstOrDefault(b=>b.text.StartsWith("Engager ma part"));if(approved==null)throw new Exception("Le propriétaire n’a pas approuvé le projet à son échéance : "+string.Join(", ",app.Career.life.projects.Select(p=>p.kind+" "+p.status)));Click(approved.text);break;
                    case 20:Capture("native-construction.png");if(!app.Career.life.projects.Any(p=>p.status=="building"))throw new Exception("Travaux non démarrés.");Click("Zone grise");break;
                    case 21:document.rootVisualElement.Query<Toggle>().ToList().First().value=true;break;
                    case 22:Capture("native-integrity.png");File.WriteAllText(Path.Combine(output,"native-smoke.json"),"{\"passed\":true,\"renderedPlayers\":22,\"nativeUI\":true,\"matchAdvanced\":true,\"analysisScreen\":true,\"navigationPortraitAndLandscape\":true,\"dailyContinue\":true,\"medicalDecision\":true,\"playerConversation\":true,\"boardFunding\":true,\"construction\":true,\"integrityTab\":true,\"seededMedicalExample\":true,\"physicalAndroid\":false}");SessionState.SetBool("TouchlineSmoke",false);Debug.Log("TOUCHLINE_NATIVE_SMOKE_OK");EditorApplication.Exit(0);break;
                }
            }catch(Exception e){SessionState.SetBool("TouchlineSmoke",false);Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}


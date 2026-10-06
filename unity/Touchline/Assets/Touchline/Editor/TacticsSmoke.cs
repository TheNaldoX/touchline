using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Touchline.Editor
{
    [InitializeOnLoad] public static class TacticsSmoke
    {
        static string[] formationIds;static int stage,frames,last=-1;static RenderTexture target;static VisualElement drag,instructionContent;static Vector2 destination;static float instructionScroll;static string sourceId,targetId,benchId,output;
        static TacticsSmoke(){EditorApplication.update+=Tick;}
        public static void Run(){var arg=Environment.GetCommandLineArgs().FirstOrDefault(a=>a.StartsWith("-touchlineTacticsOutput="));output=Path.GetFullPath(arg==null?"../../artifacts/unity/tactics-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"):arg.Substring("-touchlineTacticsOutput=".Length));if(Directory.Exists(output))throw new IOException("Preserve previous tactical review: "+output);Directory.CreateDirectory(output);SessionState.SetString("TouchlineTacticsOutput",output);ProjectBuilder.Configure();SessionState.SetBool("TacticsSmoke",true);EditorApplication.isPlaying=true;}
        static VisualElement Root=>TouchlineApp.Instance.GetComponent<UIDocument>().rootVisualElement;
        static void Click(string name){var button=Root.Query<Button>().ToList().FirstOrDefault(b=>b.text==name||b.name==name);if(button==null)throw new Exception("Missing button "+name);using(var e=NavigationSubmitEvent.GetPooled()){e.target=button;button.SendEvent(e);}}
        static void Resize(int w,int h){var old=target;target=new RenderTexture(w,h,24);target.Create();TouchlineApp.Instance.GetComponent<UIDocument>().panelSettings.targetTexture=target;if(old!=null){old.Release();UnityEngine.Object.DestroyImmediate(old);}}
        static void Pointer(EventType type,Vector2 point){var mouse=new Event{type=type,mousePosition=point,button=0};if(type==EventType.MouseDown){using(var e=PointerDownEvent.GetPooled(mouse)){e.target=drag;drag.SendEvent(e);}}else if(type==EventType.MouseUp){using(var e=PointerUpEvent.GetPooled(mouse)){e.target=drag;drag.SendEvent(e);}}else{using(var e=PointerMoveEvent.GetPooled(mouse)){e.target=drag;drag.SendEvent(e);}}}
        static void Capture(string name){var old=RenderTexture.active;RenderTexture.active=target;var texture=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,target.width,target.height),0,0);texture.Apply();File.WriteAllBytes(Path.Combine(output,name+".png"),texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);RenderTexture.active=old;}
        static void Tick(){if(!SessionState.GetBool("TacticsSmoke",false)||!EditorApplication.isPlaying||TouchlineApp.Instance==null||last==Time.frameCount)return;last=Time.frameCount;if(++frames<18)return;frames=0;
            try{var app=TouchlineApp.Instance;output=SessionState.GetString("TouchlineTacticsOutput","");if(string.IsNullOrEmpty(output))throw new IOException("Missing tactical review output");
                switch(stage++){
                    case 0:app.Career.EnsureWorld(app.Database);Resize(1280,966);Click("Tactique");break;
                    case 1:Capture("composition");sourceId=app.Career.lineup[5];targetId=app.Career.lineup[7];drag=Root.Q("tactical-slot-5");destination=Root.Q("tactical-slot-7").worldBound.center;Pointer(EventType.MouseDown,drag.worldBound.center);break;
                    case 2:Pointer(EventType.MouseDrag,destination);break;
                    case 3:if(!Root.Q("tactical-slot-7").ClassListContains("drop-player"))throw new Exception("Drop target not highlighted");Capture("permutation");Pointer(EventType.MouseUp,destination);break;
                    case 4:if(app.Career.lineup[7]!=sourceId||app.Career.lineup[5]!=targetId)throw new Exception("Player swap failed");Click("Annuler");break;
                    case 5:if(app.Career.lineup[5]!=sourceId)throw new Exception("Undo failed");var handle=Root.Query<VisualElement>(className:"bench-handle").ToList().First();benchId=handle.name.Substring("bench-drag-".Length);drag=handle;destination=Root.Q("tactical-slot-9").worldBound.center;Pointer(EventType.MouseDown,drag.worldBound.center);break;
                    case 6:Pointer(EventType.MouseDrag,destination);break;
                    case 7:Pointer(EventType.MouseUp,destination);break;
                    case 8:if(app.Career.lineup[9]!=benchId)throw new Exception("Bench drag failed");Click("Annuler");break;
                    case 9:Click("assign-"+benchId);break;
                    case 10:if(app.Career.lineup[9]!=benchId)throw new Exception("Direct selection failed");Capture("remplacement");Click("Sans ballon");break;
                    case 11:Click("Très haute");break;
                    case 12:if(app.Career.tactic.line!=1)throw new Exception("Instruction not applied");Capture("sans-ballon");Click("Avec ballon");break;
                    case 13:Capture("avec-ballon");Click("Transitions");break;
                    case 14:Capture("transitions");Click("Composition");Resize(1080,2520);break;
                    case 15:Capture("portrait");drag=Root.Q("tactical-slot-8");Pointer(EventType.MouseDown,drag.worldBound.center);break;
                    case 16:Pointer(EventType.MouseUp,drag.worldBound.center);break;
                    case 17:var bench=Root.Q(className:"tactics-bench");if(bench.worldBound.xMax>Root.worldBound.xMax||bench.worldBound.width<300)throw new Exception("Portrait bench overflows");Capture("portrait-banc");if(Root.Q(className:"modal-panel")!=null)throw new Exception("Unexpected selection modal");Resize(1600,700);break;
                    case 18:var keeper=Root.Q("tactical-slot-0");var nav=Root.Q(className:"navigation");if(keeper.worldBound.yMax>nav.worldBound.yMin-8)throw new Exception("Landscape eleven clipped");var viewport=Root.Q(className:"content").Q<ScrollView>().contentViewport.worldBound;for(int slot=0;slot<11;slot++){var bounds=Root.Q("tactical-slot-"+slot).worldBound;if(bounds.yMin<viewport.yMin-2||bounds.yMax>viewport.yMax+2)throw new Exception("Landscape player clipped: "+slot);}Capture("paysage");Click("Avec ballon");Resize(1080,2520);break;
                    case 19:if(Root.Q("tactical-pitch")!=null||Root.Q<Button>("phase-pitch-preview")==null)throw new Exception("Narrow instructions should expose controls before optional pitch");Root.Q(className:"content").Q<ScrollView>().scrollOffset=new Vector2(0,300);break;
                    case 20:instructionContent=Root.Q(className:"content");instructionScroll=instructionContent.Q<ScrollView>().scrollOffset.y;if(instructionScroll<200)throw new Exception("Instruction scroll not exercised");Click("Très rapide");break;
                    case 21:if(instructionContent!=Root.Q(className:"content")||Mathf.Abs(instructionContent.Q<ScrollView>().scrollOffset.y-instructionScroll)>1)throw new Exception("Changing a setting rebuilt or jumped the page");if(app.Career.tactic.tempo!=1)throw new Exception("Tempo not applied");Click("Sans ballon");break;
                    case 22:if(Root.Q(className:"content").Q<ScrollView>().scrollOffset.y>1)throw new Exception("Phase inherited another tab scroll");Click("Avec ballon");break;
                    case 23:if(Mathf.Abs(Root.Q(className:"content").Q<ScrollView>().scrollOffset.y-instructionScroll)>1)throw new Exception("Phase scroll not restored");Capture("portrait-consignes");Click("Composition");Resize(1600,900);break;
                    case 24:formationIds=(string[])app.Career.lineup.Clone();Root.Query<DropdownField>().ToList().First(d=>d.label=="Système").value=app.Career.tactic.formation=="4-2-3-1"?"4-3-3":"4-2-3-1";break;
                    case 25:if(!formationIds.OrderBy(id=>id).SequenceEqual(app.Career.lineup.OrderBy(id=>id))||app.Career.lineup[0]!=formationIds[0])throw new Exception("Formation changed selected eleven or goalkeeper");Capture("formation-remapped");File.WriteAllText(Path.Combine(output,"report.json"),"{\"passed\":true,\"pitchSwap\":true,\"benchDrag\":true,\"directReplacement\":true,\"undo\":true,\"instructionsApplied\":true,\"stableInstructionControls\":true,\"independentPhaseScroll\":true,\"portraitLandscape\":true,\"physicalPhone\":false}");SessionState.SetBool("TacticsSmoke",false);Debug.Log("TOUCHLINE_TACTICS_OK");EditorApplication.Exit(0);break;
                }
            }catch(Exception e){SessionState.SetBool("TacticsSmoke",false);Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}

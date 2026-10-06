using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Touchline.Core;

namespace Touchline.Editor
{
    [InitializeOnLoad] public static class TacticalBenchSearchSmoke
    {
        static int stage,frames,last=-1;static string output,original,starter;static RenderTexture target;static VisualElement drag;static Vector2 destination;
        const string BenchId="324960";
        static TouchlineApp App=>TouchlineApp.Instance;
        static VisualElement Root=>App.GetComponent<UIDocument>().rootVisualElement;
        static TextField Search=>Root.Q<TextField>("tactics-bench-search");
        static TacticalBenchSearchSmoke(){EditorApplication.update+=Tick;}
        static string Output(){var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-touchlineOutput");return Path.GetFullPath(i>=0&&i+1<args.Length?args[i+1]:"../../artifacts/unity/session7-tactical-search");}
        public static void Run(){output=Output();if(Directory.Exists(output)&&Directory.EnumerateFileSystemEntries(output).Any())throw new Exception("Preserve previous tactical proof");Directory.CreateDirectory(output);SessionState.SetInt("TacticalBenchSearchSmoke.oldSize",PlayerPrefs.GetInt("interface-size",1));SessionState.SetBool("TacticalBenchSearchSmoke.hadSize",PlayerPrefs.HasKey("interface-size"));PlayerPrefs.SetInt("interface-size",1);ProjectBuilder.Configure();SessionState.SetBool("TacticalBenchSearchSmoke",true);EditorApplication.isPlaying=true;}
        static void Restore(){if(SessionState.GetBool("TacticalBenchSearchSmoke.hadSize",false))PlayerPrefs.SetInt("interface-size",SessionState.GetInt("TacticalBenchSearchSmoke.oldSize",1));else PlayerPrefs.DeleteKey("interface-size");}
        static object Invoke(string name,params object[] args)=>typeof(TouchlineApp).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(App,args);
        static void Require(bool value,string message){if(!value)throw new Exception(message);}
        static void Submit(VisualElement element){Require(element!=null,"Missing submit target");using(var e=NavigationSubmitEvent.GetPooled()){e.target=element;element.SendEvent(e);}}
        static void Click(string id)=>Submit(Root.Query<Button>().ToList().First(b=>b.name==id||b.text==id));
        static void Resize(int w,int h){var old=target;target=new RenderTexture(w,h,24);target.Create();App.GetComponent<UIDocument>().panelSettings.targetTexture=target;if(old!=null){old.Release();UnityEngine.Object.DestroyImmediate(old);}}
        static void Pointer(EventType type,Vector2 point){var mouse=new Event{type=type,mousePosition=point,button=0};if(type==EventType.MouseDown){using(var e=PointerDownEvent.GetPooled(mouse)){e.target=drag;drag.SendEvent(e);}}else if(type==EventType.MouseUp){using(var e=PointerUpEvent.GetPooled(mouse)){e.target=drag;drag.SendEvent(e);}}else{using(var e=PointerMoveEvent.GetPooled(mouse)){e.target=drag;drag.SendEvent(e);}}}
        static void Capture(string name){var old=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);RenderTexture.active=old;}
        static void Visible(VisualElement element){Require(element!=null,"Missing visible control");var bounds=element.worldBound;Require(bounds.width>=43&&bounds.height>=43,"Tactical target too small: "+element.name+" "+bounds);Require(bounds.xMin>=Root.worldBound.xMin&&bounds.xMax<=Root.worldBound.xMax+1&&bounds.yMin>=Root.worldBound.yMin&&bounds.yMax<=Root.worldBound.yMax+1,"Tactical target outside root: "+element.name+" "+bounds);for(var p=element.parent;p!=null;p=p.parent)if(p is ScrollView view){var viewport=view.Q<VisualElement>(className:"unity-scroll-view__content-viewport").worldBound;Require(bounds.xMin>=viewport.xMin-1&&bounds.xMax<=viewport.xMax+1&&bounds.yMin>=viewport.yMin-1&&bounds.yMax<=viewport.yMax+1,"Tactical target clipped: "+element.name+" "+bounds+" viewport="+viewport);}}
        static void BeginDrag(string player,int slot){drag=Root.Q("bench-drag-"+player);Require(drag!=null,"Filtered drag handle missing");Visible(drag);Visible(Root.Q("tactical-slot-"+slot));destination=Root.Q("tactical-slot-"+slot).worldBound.center;Pointer(EventType.MouseDown,drag.worldBound.center);}
        static void Tick()
        {
            if(!SessionState.GetBool("TacticalBenchSearchSmoke",false)||!EditorApplication.isPlaying||App==null||last==Time.frameCount)return;last=Time.frameCount;if(++frames<24)return;frames=0;
            try{output??=Output();switch(stage++){
                case 0:
                    Require((bool)typeof(TouchlineApp).GetProperty("VisualValidation",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(App),"Personal career access forbidden");App.GetComponent<UIDocument>().panelSettings=UnityEngine.Object.Instantiate(App.GetComponent<UIDocument>().panelSettings);App.Career.EnsureWorld(App.Database);App.Career.PrepareLineup(App.Database);
                    var bench=App.Database.Find(BenchId);Require(bench!=null&&bench.team==App.Career.club&&App.Career.Available(BenchId),"Marseille Bamo Meïté fixture unavailable");int selected=Array.IndexOf(App.Career.lineup,BenchId);if(selected>=0){var replacement=App.Database.Squad(App.Career.club).First(p=>!App.Career.lineup.Contains(p.id)&&App.Career.LineupRestriction(App.Database,selected,p.id)==null);App.Career.AssignTacticalPlayer(App.Database,selected,replacement.id);}original=App.Career.lineup[2];Resize(1280,966);Invoke("Navigate","Tactique");break;
                case 1:Submit(Root.Q("tactical-slot-2"));Search.value="  MEITE  ";break;
                case 2:Require(Root.Query<VisualElement>(className:"bench-player").ToList().Count==1&&Root.Q("bench-player-"+BenchId)!=null,"Case/accent/whitespace search did not isolate Bamo Meïté");Capture("accent-bench-filter");BeginDrag(BenchId,2);break;
                case 3:Pointer(EventType.MouseDrag,destination);break;
                case 4:Require(Root.Q("tactical-slot-2").ClassListContains("drop-player"),"Filtered bench drop not highlighted");Capture("filtered-bench-drag");Pointer(EventType.MouseUp,destination);break;
                case 5:Require(App.Career.lineup[2]==BenchId,"Filtered bench drag did not replace selected player");Click("Annuler");break;
                case 6:Require(App.Career.lineup[2]==original&&Root.Q("bench-player-"+BenchId)!=null,"Undo or search restoration failed");Resize(1080,2520);break;
                case 7:Require(Search.value=="  MEITE  ","Bench search lost on fold");Root.Q(className:"content").Q<ScrollView>().ScrollTo(Root.Q(className:"tactics-bench"));break;
                case 8:Capture("filtered-bench-folded");Require(Root.Q(className:"content").Q<ScrollView>().scrollOffset.y>0,"Portrait fixture did not scroll to bench");Visible(Root.Q("assign-"+BenchId));Click("assign-"+BenchId);break;
                case 9:Require(App.Career.lineup[2]==BenchId,"Direct replacement failed after folded scroll");starter=App.Career.lineup[4];Root.Q<Toggle>("tactics-include-starters").value=true;Search.value=App.Database.Find(starter).name;Resize(1600,700);break;
                case 10:Require(Search.value==App.Database.Find(starter).name&&Root.Q<Toggle>("tactics-include-starters").value,"Search or include-starters lost on unfold");Require(Root.Q("assign-"+starter)?.enabledInHierarchy==true,"Filtered starter cannot be selected");Capture("filtered-starter-landscape");BeginDrag(starter,2);break;
                case 11:Pointer(EventType.MouseDrag,destination);break;
                case 12:Require(Root.Q("tactical-slot-2").ClassListContains("drop-player"),"Starter permutation target not highlighted");Pointer(EventType.MouseUp,destination);break;
                case 13:Require(App.Career.lineup[2]==starter&&App.Career.lineup[4]==BenchId,"Filtered starter permutation after unfolding failed");Capture("filtered-starter-swapped");Click("Annuler");break;
                case 14:Require(App.Career.lineup[2]==BenchId&&App.Career.lineup[4]==starter,"Undo after filtered starter permutation failed");Require(App.Career.lineup.Distinct().Count()==11,"Tactical gestures duplicated a player");Search.value="";break;
                case 15:Capture("tactical-search-cleared");Require(Search.value==""&&Root.Query<VisualElement>(className:"bench-player").ToList().Count>1,"Clearing search did not restore bench candidates");File.WriteAllText(Path.Combine(output,"report.json"),"{\"passed\":true,\"accentCaseWhitespaceSearch\":true,\"filteredBenchDrag\":true,\"foldedDirectReplacement\":true,\"unfoldedStarterPermutation\":true,\"undo\":true,\"elevenUniquePlayers\":true,\"syntheticLineupSetup\":true,\"physicalAndroid\":false}");Restore();SessionState.SetBool("TacticalBenchSearchSmoke",false);Debug.Log("TOUCHLINE_TACTICAL_BENCH_SEARCH_OK");EditorApplication.Exit(0);break;
            }}catch(Exception e){Restore();SessionState.SetBool("TacticalBenchSearchSmoke",false);if(output!=null)File.WriteAllText(Path.Combine(output,"failure.txt"),"Stage "+stage+Environment.NewLine+e);Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}

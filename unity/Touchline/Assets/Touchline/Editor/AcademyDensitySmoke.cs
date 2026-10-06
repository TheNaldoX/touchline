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
    [InitializeOnLoad] public static class AcademyDensitySmoke
    {
        const string Key="AcademyDensitySmoke";
        static int stage,frames,last=-1;static string output,player,query;static RenderTexture target;
        static TouchlineApp App=>TouchlineApp.Instance;
        static VisualElement Root=>App.GetComponent<UIDocument>().rootVisualElement;
        static ScrollView Workspace=>Root.Q<ScrollView>("academy-workspace");
        static AcademyDensitySmoke(){EditorApplication.update+=Tick;}
        static string Output(){var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-touchlineOutput");return Path.GetFullPath(i>=0&&i+1<args.Length?args[i+1]:"../../artifacts/unity/session7-academy-density");}
        public static void Run(){output=Output();if(Directory.Exists(output)&&Directory.EnumerateFileSystemEntries(output).Any())throw new Exception("Preserve previous academy proof");Directory.CreateDirectory(output);SessionState.SetBool(Key+".hadSize",PlayerPrefs.HasKey("interface-size"));SessionState.SetInt(Key+".oldSize",PlayerPrefs.GetInt("interface-size",1));PlayerPrefs.SetInt("interface-size",1);ProjectBuilder.Configure();SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;}
        static void Restore(){if(SessionState.GetBool(Key+".hadSize",false))PlayerPrefs.SetInt("interface-size",SessionState.GetInt(Key+".oldSize",1));else PlayerPrefs.DeleteKey("interface-size");}
        static void Require(bool value,string message){if(!value)throw new Exception(message);}
        static void Call(string name,params object[] args)=>typeof(TouchlineApp).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(App,args);
        static void Click(string name){var b=Root.Query<Button>().ToList().First(x=>x.name==name||x.text==name);using(var e=NavigationSubmitEvent.GetPooled()){e.target=b;b.SendEvent(e);}}
        static void Resize(int w,int h){var old=target;target=new RenderTexture(w,h,24);target.Create();App.GetComponent<UIDocument>().panelSettings.targetTexture=target;if(old!=null){old.Release();UnityEngine.Object.DestroyImmediate(old);}}
        static void Capture(string name){var previous=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);RenderTexture.active=previous;}
        static void CompleteCard(string name)
        {
            Capture(name);var first=Root.Query<VisualElement>(className:"academy-player").ToList().First();var viewport=Workspace.Q<VisualElement>(className:"unity-scroll-view__content-viewport").worldBound;
            File.WriteAllLines(Path.Combine(output,name+"-bounds.txt"),new[]{"root="+Root.worldBound,"viewport="+viewport,"first="+first.worldBound}.Concat(first.Query<Button>().ToList().Select(b=>b.text+"="+b.worldBound)).Concat(first.Query<Label>().ToList().Select(l=>l.text+"="+l.worldBound+" margin="+l.resolvedStyle.marginTop+","+l.resolvedStyle.marginBottom+" padding="+l.resolvedStyle.paddingTop+","+l.resolvedStyle.paddingBottom+" minHeight="+l.resolvedStyle.minHeight)));
            Require(first.worldBound.yMin>=viewport.yMin-1&&first.worldBound.yMax<=viewport.yMax+1,"No complete first academy card: "+first.worldBound+" viewport="+viewport);
            foreach(var button in first.Query<Button>().ToList())Require(button.worldBound.width>=43&&button.worldBound.height>=43&&button.worldBound.xMin>=viewport.xMin-1&&button.worldBound.xMax<=viewport.xMax+1,"Academy action inaccessible: "+button.text+" "+button.worldBound);
            Require(first.Q(className:"academy-advice")!=null&&first.Q(className:"academy-pathway")!=null,"Compact card removed advice or separate counters");
        }
        static void Filters(){Require(Root.Q<TextField>("academy-search").value==query&&Root.Q<DropdownField>("academy-group").value=="U19"&&Root.Query<VisualElement>(className:"academy-player").ToList().Count==1&&Root.Q("academy-player-"+player)!=null,"Academy filters lost on reflow");}
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||App==null||last==Time.frameCount)return;last=Time.frameCount;if(++frames<36)return;frames=0;
            try{output??=Output();switch(stage++){
                case 0:Require((bool)typeof(TouchlineApp).GetProperty("VisualValidation",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(App),"Personal save access forbidden");App.GetComponent<UIDocument>().panelSettings=UnityEngine.Object.Instantiate(App.GetComponent<UIDocument>().panelSettings);App.Career.EnsureWorld(App.Database);var path=App.Career.AcademyPaths(App.Database).First(y=>Career.AcademyGroup(App.Database.Find(y.player),y)=="U19");player=path.player;query=App.Database.Find(player).name;Resize(1600,700);Call("Navigate","Formation");break;
                case 1:CompleteCard("academy-default-short-landscape");Root.Q<DropdownField>("academy-group").value="U19";Root.Q<TextField>("academy-search").value=query;break;
                case 2:Filters();CompleteCard("academy-filtered-short-landscape");Click("academy-plan-"+player);break;
                case 3:Capture("academy-plan-short-landscape");Require(Root.Q("academy-plan")!=null,"First pathway did not open");Click("Fermer");Resize(1080,2520);break;
                case 4:Filters();CompleteCard("academy-filtered-folded");Resize(2160,1856);break;
                case 5:Filters();CompleteCard("academy-filtered-unfolded");Root.Q<DropdownField>("academy-group").value="Tous";Root.Q<TextField>("academy-search").value="";break;
                case 6:CompleteCard("academy-all-unfolded");Require(Root.Query<VisualElement>(className:"academy-player").ToList().Count>1,"Cleared filter did not restore pathways");File.WriteAllText(Path.Combine(output,"report.json"),"{\"passed\":true,\"completeFirstCardShortLandscape\":true,\"minimum44pxActions\":true,\"countersAndAdviceRetained\":true,\"openPlan\":true,\"filtersSurviveFoldUnfold\":true,\"personalSaveWrites\":false,\"physicalAndroid\":false}");Restore();SessionState.SetBool(Key,false);Debug.Log("TOUCHLINE_ACADEMY_DENSITY_OK");EditorApplication.Exit(0);break;
            }}catch(Exception e){Restore();SessionState.SetBool(Key,false);if(output!=null)File.WriteAllText(Path.Combine(output,"failure.txt"),"Stage "+stage+Environment.NewLine+e);Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Touchline.Editor
{
    [InitializeOnLoad] public static class ProfileScrollIntegritySmoke
    {
        const string Key="ProfileScrollIntegritySmoke";
        static int stage,frames,last=-1;static string output,own,foreign;static RenderTexture target;static byte[] tabsPixels,actionsPixels;static Rect tabsRect,actionsRect;
        static TouchlineApp App=>TouchlineApp.Instance;
        static VisualElement Root=>App.GetComponent<UIDocument>().rootVisualElement;
        static VisualElement Panel=>Root.Q("player-profile");
        static ScrollView Body=>Root.Q<ScrollView>("profile-body");
        static VisualElement Tabs=>Panel.Q(className:"profile-tabs");
        static VisualElement Actions=>Panel.Q(className:"profile-actions");
        static ProfileScrollIntegritySmoke(){EditorApplication.update+=Tick;}
        static string Output(){var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-touchlineOutput");return Path.GetFullPath(i>=0&&i+1<args.Length?args[i+1]:"../../artifacts/unity/session7-profile-scroll");}
        public static void Run(){output=Output();if(Directory.Exists(output)&&Directory.EnumerateFileSystemEntries(output).Any())throw new Exception("Preserve previous profile proof");Directory.CreateDirectory(output);SessionState.SetBool(Key+".hadSize",PlayerPrefs.HasKey("interface-size"));SessionState.SetInt(Key+".oldSize",PlayerPrefs.GetInt("interface-size",1));PlayerPrefs.SetInt("interface-size",1);ProjectBuilder.Configure();SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;}
        static void Restore(){if(SessionState.GetBool(Key+".hadSize",false))PlayerPrefs.SetInt("interface-size",SessionState.GetInt(Key+".oldSize",1));else PlayerPrefs.DeleteKey("interface-size");}
        static void Require(bool value,string message){if(!value)throw new Exception(message);}
        static void Click(string text){var b=Panel.Query<Button>().ToList().First(x=>x.text==text);using(var e=NavigationSubmitEvent.GetPooled()){e.target=b;b.SendEvent(e);}}
        static void Resize(int w,int h){var old=target;target=new RenderTexture(w,h,24);target.Create();App.GetComponent<UIDocument>().panelSettings.targetTexture=target;if(old!=null){old.Release();UnityEngine.Object.DestroyImmediate(old);}}
        static byte[] Pixels(Rect logical){float scale=App.GetComponent<UIDocument>().panelSettings.scale;int x=Mathf.Clamp(Mathf.CeilToInt(logical.xMin*scale),0,target.width-1),right=Mathf.Clamp(Mathf.FloorToInt(logical.xMax*scale),x+1,target.width);int top=Mathf.Clamp(Mathf.CeilToInt(logical.yMin*scale),0,target.height-1),bottom=Mathf.Clamp(Mathf.FloorToInt(logical.yMax*scale),top+1,target.height);var previous=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(right-x,bottom-top,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(x,target.height-bottom,right-x,bottom-top),0,0);image.Apply();var bytes=image.EncodeToPNG();UnityEngine.Object.DestroyImmediate(image);RenderTexture.active=previous;return bytes;}
        static void Capture(string name){File.WriteAllBytes(Path.Combine(output,name+".png"),Pixels(Root.worldBound));File.WriteAllLines(Path.Combine(output,name+"-bounds.txt"),new[]{"root="+Root.worldBound,"panel="+Panel.worldBound,"body="+Body.worldBound,"tabs="+Tabs.worldBound,"actions="+Actions.worldBound,"scroll="+Body.scrollOffset,"content="+Body.contentContainer.worldBound}.Concat(Tabs.Query<Button>().ToList().Concat(Actions.Query<Button>().ToList()).Select(b=>b.text+"="+b.worldBound)));}
        static void Bounds(){Require(Panel.worldBound.xMin>=0&&Panel.worldBound.xMax<=Root.worldBound.xMax+1&&Panel.worldBound.yMin>=0&&Panel.worldBound.yMax<=Root.worldBound.yMax+1,"Profile exceeds visible root");Require(Body.worldBound.height>=80,"Profile body too short");Require(Tabs.worldBound.yMax<=Body.worldBound.yMin+1&&Actions.worldBound.yMin>=Body.worldBound.yMax-1,"Fixed profile controls overlap body");foreach(var button in Tabs.Query<Button>().ToList().Concat(Actions.Query<Button>().ToList()))Require(button.worldBound.width>=43&&button.worldBound.height>=43&&button.worldBound.xMin>=Panel.worldBound.xMin&&button.worldBound.xMax<=Panel.worldBound.xMax+1&&button.worldBound.yMax<=Panel.worldBound.yMax+1,"Profile control inaccessible: "+button.text+" "+button.worldBound);}
        static void BeginScroll(string name){Capture(name);Bounds();tabsRect=Tabs.worldBound;actionsRect=Actions.worldBound;tabsPixels=Pixels(tabsRect);actionsPixels=Pixels(actionsRect);Body.scrollOffset=new Vector2(0,700);}
        static void CheckScroll(string name,bool mustScroll=true){Capture(name);Bounds();Require(!mustScroll||Body.scrollOffset.y>0,"Fixture did not scroll profile body");Require(Tabs.worldBound==tabsRect&&Actions.worldBound==actionsRect,"Profile controls moved with scroll");Require(tabsPixels.SequenceEqual(Pixels(tabsRect)),"Scrolled body painted over profile tabs");Require(actionsPixels.SequenceEqual(Pixels(actionsRect)),"Scrolled body painted over profile actions");}
        static void Hidden(){var scores=Body.Query<Label>(className:"attribute-score").ToList();Require(scores.Count>10&&scores.All(s=>s.text=="—")&&!Body.Query<Label>(className:"attribute-strong").ToList().Any(),"Unknown attributes revealed");}
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||App==null||last==Time.frameCount)return;last=Time.frameCount;if(++frames<36)return;frames=0;
            try{output??=Output();switch(stage++){
                case 0:Require((bool)typeof(TouchlineApp).GetProperty("VisualValidation",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(App),"Personal save access forbidden");App.GetComponent<UIDocument>().panelSettings=UnityEngine.Object.Instantiate(App.GetComponent<UIDocument>().panelSettings);App.Career.EnsureWorld(App.Database);App.Career.revealAttributes=false;own=App.Database.Squad(App.Career.club).First(p=>App.Career.Contract(App.Database,p.id)!=null).id;foreign="362150";Require(App.Career.Knowledge(foreign)==0,"Foreign fixture must have unknown attributes");Resize(1600,700);break;
                case 1:App.PlayerProfile(foreign);Click("Attributs");break;
                case 2:Hidden();BeginScroll("unknown-attributes-short-landscape");break;
                case 3:Hidden();CheckScroll("unknown-attributes-short-landscape-scrolled");App.PlayerProfile(own);Click("Contrat");break;
                case 4:BeginScroll("own-contract-short-landscape");break;
                case 5:CheckScroll("own-contract-short-landscape-scrolled");Resize(1080,2520);break;
                case 6:App.PlayerProfile(foreign);Click("Attributs");break;
                case 7:Hidden();BeginScroll("unknown-attributes-folded");break;
                case 8:Hidden();CheckScroll("unknown-attributes-folded-scrolled");App.PlayerProfile(own);Click("Contrat");break;
                case 9:BeginScroll("own-contract-folded");break;
                case 10:CheckScroll("own-contract-folded-scrolled",false);Resize(2160,1856);break;
                case 11:App.PlayerProfile(foreign);Click("Attributs");break;
                case 12:Hidden();BeginScroll("unknown-attributes-unfolded");break;
                case 13:Hidden();CheckScroll("unknown-attributes-unfolded-scrolled",false);File.WriteAllText(Path.Combine(output,"report.json"),"{\"passed\":true,\"fixedTabsAndActionsPixels\":true,\"profileControlsAccessible\":true,\"attributesMasked\":true,\"shortLandscapePortraitUnfolded\":true,\"personalSaveWrites\":false,\"physicalAndroid\":false}");Restore();SessionState.SetBool(Key,false);Debug.Log("TOUCHLINE_PROFILE_SCROLL_INTEGRITY_OK");EditorApplication.Exit(0);break;
            }}catch(Exception e){Restore();SessionState.SetBool(Key,false);if(output!=null)File.WriteAllText(Path.Combine(output,"failure.txt"),"Stage "+stage+Environment.NewLine+e);Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}

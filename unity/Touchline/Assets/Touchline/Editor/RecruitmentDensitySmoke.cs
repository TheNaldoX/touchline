using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Touchline.Core;

namespace Touchline.Editor
{
    [InitializeOnLoad] public static class RecruitmentDensitySmoke
    {
        static int stage,frames,last=-1,oldSize;static string output,player;static RenderTexture target;
        static readonly List<string> observations=new List<string>();
        static TouchlineApp App=>TouchlineApp.Instance;
        static VisualElement Root=>App.GetComponent<UIDocument>().rootVisualElement;
        static ListView List=>Root.Q<ListView>("recruit-list");
        static RecruitmentDensitySmoke(){EditorApplication.update+=Tick;}
        static string Output(){var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-touchlineOutput");return Path.GetFullPath(i>=0&&i+1<args.Length?args[i+1]:"../../artifacts/unity/session7-recruitment-density");}
        public static void Run(){output=Output();if(Directory.Exists(output)&&Directory.EnumerateFileSystemEntries(output).Any())throw new Exception("Review output already exists");Directory.CreateDirectory(output);oldSize=PlayerPrefs.GetInt("interface-size",1);SessionState.SetInt("RecruitmentDensitySmoke.oldSize",oldSize);SessionState.SetBool("RecruitmentDensitySmoke.hadSize",PlayerPrefs.HasKey("interface-size"));PlayerPrefs.SetInt("interface-size",1);ProjectBuilder.Configure();SessionState.SetBool("RecruitmentDensitySmoke",true);EditorApplication.isPlaying=true;}
        static void RestorePreference(){if(SessionState.GetBool("RecruitmentDensitySmoke.hadSize",false))PlayerPrefs.SetInt("interface-size",SessionState.GetInt("RecruitmentDensitySmoke.oldSize",1));else PlayerPrefs.DeleteKey("interface-size");}
        static void Require(bool value,string message){if(!value)throw new Exception(message);}
        static object Invoke(string name,params object[] args)=>typeof(TouchlineApp).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(App,args);
        static void Click(string name){var b=Root.Query<Button>().ToList().First(x=>x.name==name||x.text==name);using(var e=NavigationSubmitEvent.GetPooled()){e.target=b;b.SendEvent(e);}}
        static void Resize(int w,int h){var old=target;target=new RenderTexture(w,h,24);target.Create();App.GetComponent<UIDocument>().panelSettings.targetTexture=target;if(old!=null){old.Release();UnityEngine.Object.DestroyImmediate(old);}}
        static void Capture(string name){var old=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);RenderTexture.active=old;}
        static void Check(string name,int minimumFullRows)
        {
            Capture(name);var viewport=List.Q<ScrollView>().contentViewport.worldBound;
            var rows=List.Query<VisualElement>(className:"recruit-player-row").ToList().Where(v=>v.worldBound.yMin>=viewport.yMin-1&&v.worldBound.yMax<=viewport.yMax+1&&v.worldBound.height>1).ToArray();
            Require(rows.Length>=minimumFullRows,name+": only "+rows.Length+" full rows; viewport="+viewport+", item="+List.fixedItemHeight);
            foreach(var row in rows){foreach(var b in row.Query<Button>().ToList()){Require(b.worldBound.height>=43&&b.worldBound.width>=43,"Touch target too small: "+b.name);Require(b.worldBound.xMin>=viewport.xMin-1&&b.worldBound.xMax<=viewport.xMax+1,"Action clipped: "+b.name);}var copy=row.Q(className:"recruit-copy");var actions=row.Q(className:"recruit-player-actions");Require(copy.worldBound.xMax<=actions.worldBound.xMin+1||copy.worldBound.yMax<=actions.worldBound.yMin+1,"Actions overlap identity");}
            foreach(string id in new[]{"recruit-search","recruit-role","recruit-market","recruit-order","recruit-advanced-button"}){var control=Root.Q(id);Require(control.worldBound.xMin>=Root.worldBound.xMin&&control.worldBound.xMax<=Root.worldBound.xMax,"Search control outside viewport: "+id);}
            observations.Add("{\"viewport\":\""+name+"\",\"fullRows\":"+rows.Length+",\"results\":"+List.itemsSource.Count+"}");
        }
        static void Tick()
        {
            if(!SessionState.GetBool("RecruitmentDensitySmoke",false)||!EditorApplication.isPlaying||App==null||last==Time.frameCount)return;last=Time.frameCount;if(++frames<30)return;frames=0;
            try{output??=Output();switch(stage++){
                case 0:Require((bool)typeof(TouchlineApp).GetProperty("VisualValidation",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(App),"Validation must never read/write personal careers");App.GetComponent<UIDocument>().panelSettings=UnityEngine.Object.Instantiate(App.GetComponent<UIDocument>().panelSettings);App.Career.EnsureWorld(App.Database);App.Career.revealAttributes=false;Resize(1600,700);Invoke("Navigate","Recrutement");break;
                case 1:if(List==null){Click("recruit-tab-Marché");stage--;break;}Check("market-short-landscape",3);Require(Root.Q("recruit-row-362150")?.Q<Label>(className:"recruit-player-name")?.text.Contains("Ailier droit")==true,"Recruitment still shows generic attacker for Yamal");Require(List.itemsSource.Count>100,"Database candidates absent");player=((PlayerData)List.itemsSource[0]).id;Require(App.Career.Knowledge(player)<40,"Fixture must have unknown player");Require(Root.Q("recruit-row-"+player).Q<Label>(className:"recruit-player-detail").text.Contains("Niveau à observer"),"Hidden ability leaked");Click("recruit-shortlist-"+player);Root.Q<DropdownField>("recruit-market").value="Ma sélection";break;
                case 2:Require(List.itemsSource.Count==1,"Shortlist filtering failed");Require(((PlayerData)List.itemsSource[0]).id==player,"Shortlist selected wrong player");Click("recruit-advanced-button");break;
                case 3:Require(Root.Q<Foldout>("recruit-advanced").value,"Advanced button failed to open criteria");Require(List.worldBound.height>=89,"Advanced criteria hide market entirely");Capture("advanced-short-landscape");Click("recruit-advanced-button");Resize(1080,2520);break;
                case 4:Require(Root.Q<DropdownField>("recruit-market").value=="Ma sélection"&&List.itemsSource.Count==1,"Shortlist lost on fold");Check("shortlist-folded-portrait",1);Click("recruit-reset");break;
                case 5:Check("market-folded-portrait",3);Require(App.Career.shortlist.Contains(player),"Reset removed followed player");Root.Q<DropdownField>("recruit-role").value="LW";Resize(2160,1856);break;
                case 6:Require(Root.Q<DropdownField>("recruit-role").value=="LW","Position filter lost on unfold");Require(List.itemsSource.Cast<PlayerData>().All(p=>FootballPositions.Matches(p,"LW")),"Position filtering inconsistent");Check("market-unfolded",4);Resize(1600,700);break;
                case 7:Require(Root.Q<DropdownField>("recruit-role").value=="LW","Position filter lost on landscape return");Check("market-landscape-return",3);Click("recruit-advanced-button");Root.Q<DropdownField>("recruit-country").value=App.Database.leagues.First(l=>!string.IsNullOrWhiteSpace(l.country)).country;Root.Q<DropdownField>("recruit-market").value="Libres";break;
                case 8:Require(List.itemsSource.Count==0&&Root.Q<Label>("recruit-empty").text.Contains("masquent les joueurs libres"),"Empty territory/free-agent state is misleading");Capture("free-agents-territory-explained");Click("recruit-reset");Root.Q<DropdownField>("recruit-market").value="Libres";break;
                case 9:Check("free-agents-short-landscape",3);Require(List.itemsSource.Cast<PlayerData>().All(p=>p.team=="free"),"Free-agent filter failed");App.PlayerProfile("362150");break;
                case 10:Require(Root.Q<Label>("profile-positions").text.StartsWith("Ailier droit / Milieu droit"),"Yamal profile lost precise imported positions");Capture("precise-positions-profile-wide");Resize(1080,2520);App.PlayerProfile("286831");break;
                case 11:Require(Root.Q<Label>("profile-positions").text.StartsWith("Milieu droit / Ailier droit / Milieu offensif"),"Olise position order or CAM translation changed");Capture("precise-positions-profile-portrait");Click("Attributs");break;
                case 12:var scores=Root.Query<Label>(className:"attribute-score").ToList();Require(scores.Count>0&&scores.All(l=>l.text=="—"),"Position presentation revealed unknown attributes");Capture("precise-positions-attributes-hidden");File.WriteAllText(Path.Combine(output,"report.json"),"{\"passed\":true,\"personalSaveWrites\":false,\"hiddenAbilityPreserved\":true,\"precisePlayerPositions\":true,\"shortlistAndFiltersRetained\":true,\"physicalAndroid\":false,\"screens\":["+string.Join(",",observations)+"]}");RestorePreference();SessionState.SetBool("RecruitmentDensitySmoke",false);Debug.Log("TOUCHLINE_RECRUITMENT_DENSITY_OK");EditorApplication.Exit(0);break;
            }}catch(Exception e){RestorePreference();SessionState.SetBool("RecruitmentDensitySmoke",false);if(output!=null)File.WriteAllText(Path.Combine(output,"failure.txt"),"Stage "+stage+Environment.NewLine+e);Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}

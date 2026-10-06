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
    [InitializeOnLoad] public static class InboxWorkspaceSmoke
    {
        static int stage,frames,last=-1,oldSize,medicalId;static string output,injured;static RenderTexture target;static Rect searchBefore;static byte[] fixedHeaderPixels;static ClubMessage accentA,accentB,other;
        static TouchlineApp App=>TouchlineApp.Instance;
        static VisualElement Root=>App.GetComponent<UIDocument>().rootVisualElement;
        static ScrollView History=>Root.Q<ScrollView>("inbox-workspace");
        static InboxWorkspaceSmoke(){EditorApplication.update+=Tick;}
        static string Output(){var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-touchlineOutput");return Path.GetFullPath(i>=0&&i+1<args.Length?args[i+1]:"../../artifacts/unity/session7-inbox-workspace");}
        public static void Run(){output=Output();if(Directory.Exists(output)&&Directory.EnumerateFileSystemEntries(output).Any())throw new Exception("Review output already exists");Directory.CreateDirectory(output);oldSize=PlayerPrefs.GetInt("interface-size",1);SessionState.SetInt("InboxWorkspaceSmoke.oldSize",oldSize);SessionState.SetBool("InboxWorkspaceSmoke.hadSize",PlayerPrefs.HasKey("interface-size"));PlayerPrefs.SetInt("interface-size",1);ProjectBuilder.Configure();SessionState.SetBool("InboxWorkspaceSmoke",true);EditorApplication.isPlaying=true;}
        static void RestorePreference(){if(SessionState.GetBool("InboxWorkspaceSmoke.hadSize",false))PlayerPrefs.SetInt("interface-size",SessionState.GetInt("InboxWorkspaceSmoke.oldSize",1));else PlayerPrefs.DeleteKey("interface-size");}
        static void Require(bool value,string message){if(!value)throw new Exception(message);}
        static object Invoke(string name,params object[] args)=>typeof(TouchlineApp).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(App,args);
        static void Click(string name){var b=Root.Query<Button>().ToList().First(x=>x.name==name||x.text==name);using(var e=NavigationSubmitEvent.GetPooled()){e.target=b;b.SendEvent(e);}}
        static void Resize(int w,int h){var old=target;target=new RenderTexture(w,h,24);target.Create();App.GetComponent<UIDocument>().panelSettings.targetTexture=target;if(old!=null){old.Release();UnityEngine.Object.DestroyImmediate(old);}}
        static void Capture(string name){var old=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);RenderTexture.active=old;}
        static byte[] HeaderPixels(){int height=Mathf.Clamp(Mathf.RoundToInt(History.worldBound.yMin*App.GetComponent<UIDocument>().panelSettings.scale),1,target.height);var old=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(target.width,height,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,target.height-height,target.width,height),0,0);image.Apply();var pixels=image.EncodeToPNG();UnityEngine.Object.DestroyImmediate(image);RenderTexture.active=old;return pixels;}
        static void Check(string name)
        {
            Capture(name);File.WriteAllLines(Path.Combine(output,name+"-layout.txt"),new[]{"root="+Root.worldBound,"history="+History.worldBound}.Concat(new[]{"inbox-search","inbox-category","inbox-primary-filters","inbox-mark-visible-read"}.Select(id=>{var v=Root.Q(id);return id+"="+v.worldBound+" parent="+v.parent.worldBound+" parentDirection="+v.parent.resolvedStyle.flexDirection+" parentWrap="+v.parent.resolvedStyle.flexWrap;})));Require(History.worldBound.height>=120,"History viewport too small: "+History.worldBound);
            foreach(string id in new[]{"inbox-search","inbox-category","inbox-primary-filters","inbox-mark-visible-read"}){var v=Root.Q(id);Require(v!=null&&!History.Contains(v),"Inbox controls still scroll with history: "+id);Require(v.worldBound.xMin>=Root.worldBound.xMin&&v.worldBound.xMax<=Root.worldBound.xMax+1&&v.worldBound.yMax<=History.worldBound.yMin+1,"Inbox control clipped or overlapping history: "+id+" control="+v.worldBound+" history="+History.worldBound+" root="+Root.worldBound);}
            foreach(var b in Root.Q("inbox-primary-filters").Query<Button>().ToList())Require(b.worldBound.height>=43&&b.worldBound.width>=43,"Inbox filter target too small: "+b.text+" "+b.worldBound);
        }
        static void Tick()
        {
            if(!SessionState.GetBool("InboxWorkspaceSmoke",false)||!EditorApplication.isPlaying||App==null||last==Time.frameCount)return;last=Time.frameCount;if(++frames<30)return;frames=0;
            try{output??=Output();switch(stage++){
                case 0:Require((bool)typeof(TouchlineApp).GetProperty("VisualValidation",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(App),"No personal save access allowed");App.GetComponent<UIDocument>().panelSettings=UnityEngine.Object.Instantiate(App.GetComponent<UIDocument>().panelSettings);App.Career.EnsureWorld(App.Database);App.Career.life.messages.Clear();for(int i=0;i<40;i++)App.Career.Mail("Collaborateur "+i,"Compte rendu "+i,"Un échange de suivi du club, conservé dans cette conversation.",null,"staff");accentA=App.Career.Mail("Cellule recrutement","Échéance test7","Premier échange du dossier.",null,"scout");accentB=App.Career.Mail("Cellule recrutement","Échéance test7 — suivi","Deuxième échange du même dossier.",null,"scout");other=App.Career.Mail("Autre contact","À conserver non lu","Un dossier indépendant.",null,"staff");injured=App.Career.lineup.First(id=>App.Career.Injury(id)==null);App.Career.OpenInjury(App.Database,injured,"bruise");medicalId=App.Career.life.messages.Last(m=>m.player==injured&&m.action=="medical").id;App.Career.Mail("Adjoint","Note de routine récente","La note épinglée ne doit pas masquer la décision médicale.",injured,"talk").pinned=true;Resize(1600,700);Invoke("Navigate","Messages");break;
                case 1:Check("inbox-landscape");searchBefore=Root.Q("inbox-search").worldBound;fixedHeaderPixels=HeaderPixels();History.scrollOffset=new Vector2(0,700);break;
                case 2:Require(History.scrollOffset.y>500,"Fixture did not scroll history");Require(Root.Q("inbox-search").worldBound==searchBefore,"Search moved while scrolling");Check("inbox-landscape-scrolled");Require(fixedHeaderPixels.SequenceEqual(HeaderPixels()),"Scrolled history painted over fixed header controls; compare landscape captures");Root.Q<TextField>("inbox-search").value="this should not match";Root.Q<TextField>("inbox-search").value="echeance test7";Require(!Root.Q<Button>("inbox-mark-visible-read").enabledInHierarchy,"Read action remained enabled during pending search");break;
                case 3:Require(Root.Query<VisualElement>(className:"inbox-thread").ToList().Count==1,"Latest accent-insensitive search not applied");Require(Root.Query<Label>(className:"inbox-status").ToList().Any(l=>l.text.Contains("2 échange(s)")),"Preindexed conversation count changed");Check("inbox-search-accent");Click("inbox-mark-visible-read");Require(accentA.read&&accentB.read&&!other.read,"Bulk read escaped selected filters");Root.Q<TextField>("inbox-search").value=App.Database.Find(injured).name;Click("À traiter");break;
                case 4:Require(Root.Query<VisualElement>(className:"inbox-thread").ToList().Count==1,"Medical decision hidden by search");Click("inbox-mark-visible-read");Require(App.Career.MessageNeedsDecision(App.Career.life.messages.First(m=>m.id==medicalId)),"Reading resolved medical decision");Click("Lire et décider");break;
                case 5:Require(Root.Q("inbox-message-"+medicalId)?.ClassListContains("conversation-selected")==true,"Routine pinned note masked important medical action");Capture("inbox-medical-decision");Click("Fermer");Resize(1080,2520);break;
                case 6:Require(Root.Q<TextField>("inbox-search").value==App.Database.Find(injured).name,"Search lost on fold");Check("inbox-folded-decision");Root.Q<TextField>("inbox-search").value="";Click("Non lus");break;
                case 7:Require(Root.Query<VisualElement>(className:"inbox-thread").ToList().Count>30,"Unread conversations disappeared");Check("inbox-folded-unread");Resize(2160,1856);break;
                case 8:Check("inbox-unfolded-unread");File.WriteAllText(Path.Combine(output,"report.json"),"{\"passed\":true,\"fixedControls\":true,\"headerPixelsUnchangedByScroll\":true,\"debouncedLatestQuery\":true,\"accentInsensitiveSearch\":true,\"filteredBulkRead\":true,\"importantDecisionPreserved\":true,\"threadCountsPreserved\":true,\"foldFiltersPreserved\":true,\"syntheticMessages\":true,\"personalSaveWrites\":false,\"physicalAndroid\":false}");RestorePreference();SessionState.SetBool("InboxWorkspaceSmoke",false);Debug.Log("TOUCHLINE_INBOX_WORKSPACE_OK");EditorApplication.Exit(0);break;
            }}catch(Exception e){RestorePreference();SessionState.SetBool("InboxWorkspaceSmoke",false);File.WriteAllText(Path.Combine(output,"failure.txt"),"Stage "+stage+Environment.NewLine+e);Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}

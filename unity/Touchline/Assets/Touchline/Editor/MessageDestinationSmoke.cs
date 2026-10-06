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
    [InitializeOnLoad] public static class MessageDestinationSmoke
    {
        const string Key="MessageDestinationSmoke";const string Foreign="362150";
        static long cashBefore;static int missionsBefore;static int stage,frames,last=-1;static string output,young;static RenderTexture target;
        static TouchlineApp App=>TouchlineApp.Instance;
        static VisualElement Root=>App.GetComponent<UIDocument>().rootVisualElement;
        static MessageDestinationSmoke(){EditorApplication.update+=Tick;}
        static string Output(){var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-touchlineOutput");return Path.GetFullPath(i>=0&&i+1<args.Length?args[i+1]:"../../artifacts/unity/session7-message-destinations");}
        public static void Run(){output=Output();if(Directory.Exists(output)&&Directory.EnumerateFileSystemEntries(output).Any())throw new Exception("Preserve previous message proof");Directory.CreateDirectory(output);SessionState.SetBool(Key+".hadSize",PlayerPrefs.HasKey("interface-size"));SessionState.SetInt(Key+".oldSize",PlayerPrefs.GetInt("interface-size",1));PlayerPrefs.SetInt("interface-size",1);ProjectBuilder.Configure();SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;}
        static void Restore(){if(SessionState.GetBool(Key+".hadSize",false))PlayerPrefs.SetInt("interface-size",SessionState.GetInt(Key+".oldSize",1));else PlayerPrefs.DeleteKey("interface-size");}
        static void Require(bool value,string message){if(!value)throw new Exception(message);}
        static void Call(string name,params object[] args)=>typeof(TouchlineApp).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(App,args);
        static void Click(string name){var b=Root.Query<Button>().ToList().First(x=>x.name==name||x.text==name);using(var e=NavigationSubmitEvent.GetPooled()){e.target=b;b.SendEvent(e);}}
        static void Resize(int w,int h){var old=target;target=new RenderTexture(w,h,24);target.Create();App.GetComponent<UIDocument>().panelSettings.targetTexture=target;if(old!=null){old.Release();UnityEngine.Object.DestroyImmediate(old);}}
        static void Capture(string name){var previous=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);RenderTexture.active=previous;}
        static void ReportIdentityVisible()
        {
            var report=Root.Q("scout-report-"+Foreign);var title=report.Q<Label>(className:"section-title");var viewport=report.GetFirstAncestorOfType<ScrollView>().contentViewport.worldBound;
            Require(title.text.Contains(App.Database.Find(Foreign).name)&&title.worldBound.yMin>=viewport.yMin-1&&title.worldBound.yMax<=viewport.yMax+1,"Report identity clipped: title="+title.worldBound+" viewport="+viewport);
        }
        static void MissionHorizontalBounds()
        {
            var dialog=Root.Q("scout-mission-dialog");var viewport=dialog.Q<ScrollView>().contentViewport.worldBound;var fields=dialog.Q(className:"scout-form").Children().ToArray();
            Require(fields.Length>=9,"Mission criteria missing");
            foreach(var field in fields){var input=field.Q(className:"unity-base-field__input");Require(input!=null&&input.worldBound.xMin>=viewport.xMin-1&&input.worldBound.xMax<=viewport.xMax+1,"Mission input horizontally clipped: "+field.name+" input="+input?.worldBound+" viewport="+viewport);}
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||App==null||last==Time.frameCount)return;last=Time.frameCount;if(++frames<36)return;frames=0;
            try{output??=Output();switch(stage++){
                case 0:Require((bool)typeof(TouchlineApp).GetProperty("VisualValidation",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(App),"Personal save access forbidden");App.GetComponent<UIDocument>().panelSettings=UnityEngine.Object.Instantiate(App.GetComponent<UIDocument>().panelSettings);App.Career.EnsureWorld(App.Database);App.Career.world.reports.Add(new ScoutReport{player=Foreign,club=App.Career.club,confidence=50,started=App.Career.life.day,due=App.Career.life.day+14,scout="Test de navigation",judging=12,advice="Profil suivi dans cette preuve isolée."});typeof(TouchlineApp).GetField("scoutReportFilter",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(App,"Terminés");young=App.Database.Squad(App.Career.club).First(p=>p.age>=18&&p.age<=23).id;var path=App.Career.world.youth.FirstOrDefault(y=>y.player==young);if(path==null)App.Career.world.youth.Add(new YouthPath{player=young,group="senior",focus="technical"});else path.focus="technical";var scout=App.Career.Mail("Recruteur","Profil repéré pour votre mission","Le rapport est en cours.",Foreign,"scout");Resize(1600,700);Call("Navigate","Messages");Call("OpenMessageThread",scout.id);break;
                case 1:Capture("scout-message-landscape");Require(Root.Q<Button>("inbox-open-destination").text.Contains("Rapports"),"Scout destination label is ambiguous");Click("inbox-open-destination");break;
                case 2:Capture("scout-report-destination");Require(Root.Q<Button>("recruit-tab-Rapports").ClassListContains("active")&&Root.Q("scout-report-"+Foreign)!=null,"Scout mail did not open its report, or stale completed filter hid it");ReportIdentityVisible();var transfer=App.Career.Mail("Agent","Échange concernant un joueur","Dossier de transfert pour ce test.",Foreign,"transfer");Call("Navigate","Messages");Call("OpenMessageThread",transfer.id);break;
                case 3:Capture("transfer-message-landscape");Click("inbox-open-destination");break;
                case 4:Capture("transfer-agreements-destination");Require(Root.Q<Button>("recruit-tab-Négociations et prêts").ClassListContains("active"),"Transfer mail no longer opens agreements");Call("DevelopmentConversationPlan",young);break;
                case 5:Capture("senior-development-history");Require(Root.Q("conversation-development-focus")==null&&Root.Q("conversation-development-mentor")==null&&Root.Q("conversation-development-save")==null,"Senior player still offered academy-only plan mutation");Require(Root.Q<Label>("conversation-development-history").text.Contains("Technique"),"Historical academy focus lost");Click("conversation-development-path");break;
                case 6:Capture("senior-pathway-landscape");Require(Root.Q("academy-plan")!=null&&Root.Q("academy-load")==null&&Root.Q("academy-focus")==null&&Root.Q("academy-loan")!=null,"Senior pathway does not offer correct training/loan route");Resize(1080,2520);break;
                case 7:Capture("senior-pathway-folded");Require(Root.Q("academy-plan")!=null&&App.Career.world.youth.First(y=>y.player==young).focus=="technical","Reflow or read-only history changed youth plan");Call("CloseModal");Call("Navigate","Recrutement");Click("recruit-tab-Marché");Root.Q<IntegerField>("recruit-min-age").value=35;Root.Q<IntegerField>("recruit-max-age").value=40;Click("recruit-tab-Missions");Click("scout-new-mission");break;
                case 8:Capture("scout-mission-veterans");MissionHorizontalBounds();Require(Root.Q<IntegerField>("scout-mission-min-age").value==35&&Root.Q<IntegerField>("scout-mission-max-age").value==40,"Market age range inverted or lost in mission");Root.Q<IntegerField>("scout-mission-observations").value=1;cashBefore=App.Career.life.cash;missionsBefore=App.Career.world.scoutMissions.Count;Click("scout-mission-send");break;
                case 9:Capture("scout-mission-veterans-created");Require(Root.Q("scout-mission-dialog")==null&&App.Career.world.scoutMissions.Count==missionsBefore+1,"Valid veteran mission did not submit");var mission=App.Career.world.scoutMissions.Last();Require(mission.minAge==35&&mission.maxAge==40&&mission.budget==App.Career.ObservationCost&&App.Career.life.cash==cashBefore-App.Career.ObservationCost,"Mission criteria or prepaid observation cost changed");File.WriteAllText(Path.Combine(output,"report.json"),"{\"passed\":true,\"missionInputsHorizontallyContained\":true,\"reportIdentityVisible\":true,\"missionAgeRangePreserved\":true,\"missionPrepaidCostVerified\":true,\"scoutMailOpensReports\":true,\"staleReportFilterReset\":true,\"transferMailStillOpensAgreements\":true,\"seniorHistoryReadOnly\":true,\"seniorPathwayOpens\":true,\"foldedPathwayRetained\":true,\"syntheticScoutAndYouthHistory\":true,\"personalSaveWrites\":false,\"physicalAndroid\":false}");Restore();SessionState.SetBool(Key,false);Debug.Log("TOUCHLINE_MESSAGE_DESTINATIONS_OK");EditorApplication.Exit(0);break;
            }}catch(Exception e){Restore();SessionState.SetBool(Key,false);if(output!=null)File.WriteAllText(Path.Combine(output,"failure.txt"),"Stage "+stage+Environment.NewLine+e);Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}

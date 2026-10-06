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
    [InitializeOnLoad] public static class StaffDelegationFeedbackSmoke
    {
        const string Key="StaffDelegationFeedbackSmoke";
        static int stage,frames,last=-1,focusIndex;static string output,young,oldCoach,newCoach,savedFocus;static RenderTexture target;static VisualElement plan;
        static TouchlineApp App=>TouchlineApp.Instance;
        static VisualElement Root=>App.GetComponent<UIDocument>().rootVisualElement;
        static StaffDelegationFeedbackSmoke(){EditorApplication.update+=Tick;}
        static string Output(){var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-touchlineOutput");return Path.GetFullPath(i>=0&&i+1<args.Length?args[i+1]:"../../artifacts/unity/session7-delegation-feedback");}
        public static void Run(){output=Output();if(Directory.Exists(output)&&Directory.EnumerateFileSystemEntries(output).Any())throw new Exception("Preserve previous delegation proof");Directory.CreateDirectory(output);SessionState.SetBool(Key+".hadSize",PlayerPrefs.HasKey("interface-size"));SessionState.SetInt(Key+".oldSize",PlayerPrefs.GetInt("interface-size",1));PlayerPrefs.SetInt("interface-size",1);ProjectBuilder.Configure();SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;}
        static void Restore(){if(SessionState.GetBool(Key+".hadSize",false))PlayerPrefs.SetInt("interface-size",SessionState.GetInt(Key+".oldSize",1));else PlayerPrefs.DeleteKey("interface-size");}
        static void Require(bool value,string message){if(!value)throw new Exception(message);}
        static void Call(string name,params object[] args)=>typeof(TouchlineApp).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(App,args);
        static void Click(string name){var b=Root.Query<Button>().ToList().First(x=>x.name==name||x.text==name);using(var e=NavigationSubmitEvent.GetPooled()){e.target=b;b.SendEvent(e);}}
        static void Resize(int w,int h){var old=target;target=new RenderTexture(w,h,24);target.Create();App.GetComponent<UIDocument>().panelSettings.targetTexture=target;if(old!=null){old.Release();UnityEngine.Object.DestroyImmediate(old);}}
        static void Capture(string name){var previous=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);RenderTexture.active=previous;}
        static void PlanControls(bool manual){Require(Root.Q<DropdownField>("academy-focus").enabledInHierarchy==manual,"Academy focus delegation mismatch");Require(Root.Q<DropdownField>("academy-mentor").enabledInHierarchy==manual,"Academy mentor delegation mismatch");Require(Root.Q<DropdownField>("academy-load").enabledInHierarchy,"Academy load unexpectedly delegated");Require(App.Career.life.staff.youth,"Saved delegation preference silently changed");}
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||App==null||last==Time.frameCount)return;last=Time.frameCount;if(++frames<36)return;frames=0;
            try{output??=Output();switch(stage++){
                case 0:Require((bool)typeof(TouchlineApp).GetProperty("VisualValidation",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(App),"Personal save access forbidden");App.GetComponent<UIDocument>().panelSettings=UnityEngine.Object.Instantiate(App.GetComponent<UIDocument>().panelSettings);App.Career.EnsureWorld(App.Database);App.Career.EnsureStaffMarket(App.Database);var coach=App.Career.Staff("youth");oldCoach=coach.id;coach.wage=0;App.Career.SetDelegation("youth",true);young=App.Career.AcademyPaths(App.Database).First(y=>App.Database.Find(y.player).team=="academy-"+App.Career.club).player;Resize(1600,700);Call("Navigate","Staff et délégation");break;
                case 1:Capture("delegation-requested-staff-landscape");Require(Root.Q<Toggle>("staff-delegation-toggle-youth").value&&Root.Q<Label>("staff-delegation-status-youth").text.StartsWith("En attente"),"Zero-salary staff incorrectly presented as delegated");Require(Root.Query<Toggle>().ToList().Count(t=>t.name!=null&&t.name.StartsWith("staff-delegation-toggle-"))==5,"Delegation controls missing");Root.Q(className:"content").Q<ScrollView>().ScrollTo(Root.Q("staff-delegation-youth"));break;
                case 2:Capture("delegation-vacant-status-landscape");Call("AcademyPlayerPlan",young);break;
                case 3:Capture("academy-manual-vacant-landscape");PlanControls(true);var focus=Root.Q<DropdownField>("academy-focus");focusIndex=focus.index==2?0:2;focus.index=focusIndex;Root.Q<DropdownField>("academy-load").index=1;plan=Root.Q("academy-plan");Resize(1080,2520);break;
                case 4:Capture("academy-manual-vacant-folded");PlanControls(true);Require(Root.Q("academy-plan")==plan&&Root.Q<DropdownField>("academy-focus").index==focusIndex&&Root.Q<DropdownField>("academy-load").index==1,"Folding lost draft plan");Click("academy-save-plan");break;
                case 5:var path=App.Career.world.youth.First(y=>y.player==young);savedFocus=focusIndex==2?"technical":"balanced";Require(path.focus==savedFocus&&path.trainingLoad=="light"&&App.Career.life.staff.youth,"Manual plan did not apply while delegation pending");App.Career.FireStaff(App.Database,oldCoach);var replacement=App.Career.staffMarket.First(s=>s.role=="youth"&&s.club==null&&s.id!=oldCoach);newCoach=replacement.id;App.Career.staffOffers.Add(new StaffOffer{staff=newCoach,club=App.Career.club,employer=null,status="accepted",wage=200,years=2,compensation=0,due=App.Career.life.day});App.Career.SignStaffContract(App.Database,newCoach);Call("Navigate","Formation");Root.Q<Foldout>("academy-staff-options").value=true;break;
                case 6:Capture("academy-new-responsible-folded");Require(Root.Q<Label>("academy-delegation-status").text.Contains(App.Career.Staff("youth").name)&&!Root.Q<Label>("academy-delegation-status").text.StartsWith("En attente"),"New responsible not reflected in delegation status");Call("AcademyPlayerPlan",young);break;
                case 7:Capture("academy-delegated-plan-folded");PlanControls(false);Root.Q<DropdownField>("academy-load").index=0;Click("academy-save-plan");break;
                case 8:var delegatedPath=App.Career.world.youth.First(y=>y.player==young);Require(delegatedPath.focus==savedFocus&&delegatedPath.trainingLoad=="rest","Load change overwrote delegated focus");App.Career.FireStaff(App.Database,newCoach);Call("Navigate","Formation");Root.Q<Foldout>("academy-staff-options").value=true;break;
                case 9:Capture("academy-responsible-departed-folded");Require(Root.Q<Label>(className:"academy-staff").text=="Responsable de formation : poste vacant","Vacant staff displayed fabricated fallback ratings");Require(App.Career.life.staff.youth&&Root.Q<Label>("academy-delegation-status").text.StartsWith("En attente"),"Dismissal changed preference or left stale delegation status");Call("AcademyPlayerPlan",young);Resize(1600,700);break;
                case 10:Capture("academy-manual-restored-landscape");PlanControls(true);var saved=JsonUtility.FromJson<Career>(JsonUtility.ToJson(App.Career));Require(saved.life.staff.youth&&saved.world.youth.First(y=>y.player==young).focus==savedFocus&&saved.world.youth.First(y=>y.player==young).trainingLoad=="rest","Round trip lost preference or youth plan");File.WriteAllText(Path.Combine(output,"report.json"),"{\"passed\":true,\"zeroSalaryStaffManualAccess\":true,\"requestedPreferencePreserved\":true,\"manualPlanSaved\":true,\"foldedDraftPreserved\":true,\"hiringEnablesDelegation\":true,\"dismissalRestoresManualAccess\":true,\"loadRemainsManual\":true,\"serialization\":true,\"syntheticAcceptedStaffOffer\":true,\"personalSaveWrites\":false,\"physicalAndroid\":false}");Restore();SessionState.SetBool(Key,false);Debug.Log("TOUCHLINE_STAFF_DELEGATION_FEEDBACK_OK");EditorApplication.Exit(0);break;
            }}catch(Exception e){Restore();SessionState.SetBool(Key,false);if(output!=null)File.WriteAllText(Path.Combine(output,"failure.txt"),"Stage "+stage+Environment.NewLine+e);Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}

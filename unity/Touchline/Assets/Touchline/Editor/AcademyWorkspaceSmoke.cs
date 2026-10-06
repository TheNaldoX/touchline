using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using Touchline.Core;

namespace Touchline.Editor
{
    [InitializeOnLoad] public static class AcademyWorkspaceSmoke
    {
        static int stage,frames,last=-1;static RenderTexture target;static string output,id,second;static VisualElement plan;static string focus;
        static TouchlineApp App=>TouchlineApp.Instance;
        static VisualElement Root=>App.GetComponent<UIDocument>().rootVisualElement;
        static void Call(string method,params object[] args)=>typeof(TouchlineApp).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(App,args);
        static AcademyWorkspaceSmoke(){EditorApplication.update+=Tick;}
        public static void Run(){ProjectBuilder.Configure();SessionState.SetBool("AcademyWorkspaceSmoke",true);EditorApplication.isPlaying=true;}
        static void Click(string name){var b=Root.Query<Button>().ToList().FirstOrDefault(x=>x.name==name||x.text==name)??throw new Exception("Missing "+name);using(var e=NavigationSubmitEvent.GetPooled()){e.target=b;b.SendEvent(e);}}
        static void Resize(int w,int h){var old=target;target=new RenderTexture(w,h,24);target.Create();App.GetComponent<UIDocument>().panelSettings.targetTexture=target;if(old!=null){old.Release();UnityEngine.Object.DestroyImmediate(old);}}
        static void Capture(string name){var old=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);RenderTexture.active=old;}
        static void Tick()
        {
            if(!SessionState.GetBool("AcademyWorkspaceSmoke",false)||!EditorApplication.isPlaying||App==null||last==Time.frameCount)return;last=Time.frameCount;if(++frames<35)return;frames=0;
            try{
                var requested=Environment.GetCommandLineArgs().FirstOrDefault(a=>a.StartsWith("-touchlineAcademyOutput="))?.Split('=')[1]??"academy-workspace-v2";
                if(!System.Text.RegularExpressions.Regex.IsMatch(requested,"^[a-zA-Z0-9-]+$"))throw new Exception("Invalid proof directory");
                output=Path.GetFullPath("../../artifacts/unity/"+requested);Directory.CreateDirectory(output);
                switch(stage++){
                    case 0:App.Career.EnsureWorld(App.Database);id=App.Career.world.youth[4].player;second=App.Career.world.youth[5].player;App.Database.Find(id).age=20;Resize(1280,966);Call("Navigate","Formation");break;
                    case 1:if(Root.Q("academy-workspace")==null||Root.Query<VisualElement>(className:"academy-player").ToList().Count<14)throw new Exception("Missing academy pathways");if(Root.Q<Foldout>("academy-staff-options").value)throw new Exception("Staff details should start collapsed");Capture("01-centre-open");Click("academy-plan-"+id);break;
                    case 2:Root.Q<DropdownField>("academy-focus").index=2;Root.Q<DropdownField>("academy-load").index=1;plan=Root.Q("academy-plan");Resize(1080,2520);break;
                    case 3:if(Root.Q("academy-plan")!=plan||Root.Q<DropdownField>("academy-load").index!=1||Root.Q<DropdownField>("academy-focus").index!=2)throw new Exception("Fold lost unsaved academy plan");Capture("02-plan-folded");Click("academy-save-plan");break;
                    case 4:var y=App.Career.world.youth.First(x=>x.player==id);if(y.focus!="technical"||y.trainingLoad!="light")throw new Exception("Individual plan not applied");Root.Q<DropdownField>("academy-group").value="Réserve";break;
                    case 5:if(Root.Query<VisualElement>(className:"academy-player").ToList().Count!=1)throw new Exception("Reserve filter wrong");Capture("03-reserve-portrait");Click("academy-plan-"+id);Root.Q("academy-plan").schedule.Execute(()=>Root.Q("academy-plan").Q<ScrollView>().ScrollTo(Root.Q<Button>("academy-promote"))).StartingIn(120);break;
                    case 6:if(Root.Q<Button>("academy-promote").enabledSelf||Root.Q<Label>("academy-promotion-issue")==null)throw new Exception("Unavailable promotion has no upfront reason");Capture("04-blocked-promotion");
                        // This isolated scenario explicitly grants funding to exercise the successful path as well.
                        App.Career.life.revenue*=4;Call("CloseModal");Call("AcademyPlayerPlan",id);Root.Q("academy-plan").schedule.Execute(()=>Root.Q("academy-plan").Q<ScrollView>().ScrollTo(Root.Q<Button>("academy-promote"))).StartingIn(120);break;
                    case 7:var promotion=Root.Q<Button>("academy-promote");if(!promotion.enabledSelf||promotion.worldBound.height<44||promotion.worldBound.yMax>Root.Q("academy-plan").Q<ScrollView>().contentViewport.worldBound.yMax+1)throw new Exception("Promotion cannot be reached");Capture("05-promotion-decision");Click("academy-promote");break;
                    case 8:Click("Confirmer");if(App.Database.Find(id).team!=App.Career.club||App.Career.life.players.Count(p=>p.id==id)!=1)throw new Exception("Promotion lost state or duplicated player");Root.Q<DropdownField>("academy-group").value="Professionnels";Resize(1600,700);break;
                    case 9:Capture("06-pro-path-landscape");Click("academy-plan-"+id);if(Root.Q<Button>("academy-loan")==null||Root.Q<DropdownField>("academy-load")!=null)throw new Exception("Senior pathway still pretends to control academy training");Call("CloseModal");Root.Q<Foldout>("academy-staff-options").value=true;Root.Q<Toggle>("academy-delegation").value=true;Root.Q<DropdownField>("academy-group").value="Tous";Click("academy-plan-"+second);break;
                    case 10:if(Root.Q<DropdownField>("academy-focus").enabledSelf||Root.Q<DropdownField>("academy-mentor").enabledSelf||!Root.Q<DropdownField>("academy-load").enabledSelf)throw new Exception("Delegation controls inconsistent");focus=App.Career.world.youth.First(x=>x.player==second).focus;Root.Q<DropdownField>("academy-load").index=3;Click("academy-save-plan");break;
                    case 11:var delegated=App.Career.world.youth.First(x=>x.player==second);if(delegated.focus!=focus||delegated.trainingLoad!="intensive")throw new Exception("Manual load overwrote delegated focus");Call("Navigate","Formation");Root.Q<DropdownField>("academy-group").value="U19";break;
                    case 12:var firstCard=Root.Query<VisualElement>(className:"academy-player").ToList().First();if(firstCard.worldBound.yMin>Root.Q<ScrollView>("academy-workspace").contentViewport.worldBound.yMax-100)throw new Exception("Landscape header hides the youth list");Capture("07-u19-delegation-landscape");var saved=JsonUtility.FromJson<Career>(JsonUtility.ToJson(App.Career));if(saved.world.youth.First(x=>x.player==second).trainingLoad!="intensive"||!saved.life.staff.youth)throw new Exception("Academy save lost new settings");File.WriteAllText(Path.Combine(output,"report.json"),"{\"passed\":true,\"stages\":13,\"promotionBudgetFeedback\":true,\"groupFilters\":true,\"individualPlan\":true,\"draftSurvivesFold\":true,\"promotion\":true,\"loanPath\":true,\"delegation\":true,\"serialization\":true,\"physicalAndroid\":false}");SessionState.SetBool("AcademyWorkspaceSmoke",false);Debug.Log("TOUCHLINE_ACADEMY_OK");EditorApplication.Exit(0);break;
                }
            }catch(Exception e){SessionState.SetBool("AcademyWorkspaceSmoke",false);Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}

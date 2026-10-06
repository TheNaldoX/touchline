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
    [InitializeOnLoad] public static class StaffTrainingWorkspaceSmoke
    {
        const string Key="TouchlineStaffTrainingSmoke";static int stage,frames,last=-1,saves,oldDue,newDue,oldSkill,newSkill;static long initialCash;static string output,oldId,newId;static RenderTexture target;static string pendingTarget;static int visibleFrames;static double pendingStarted;
        static StaffTrainingWorkspaceSmoke(){EditorApplication.update+=Tick;}
        static VisualElement Root=>TouchlineApp.Instance.GetComponent<UIDocument>().rootVisualElement;
        static void Require(bool value,string message){if(!value)throw new Exception(message);}
        static void Invoke(string name,params object[] args)=>typeof(TouchlineApp).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(TouchlineApp.Instance,args);
        static void Click(string name){var b=Root.Query<Button>().ToList().Single(x=>x.name==name||x.text==name);Require(b.enabledInHierarchy,"Disabled control: "+name);using(var e=NavigationSubmitEvent.GetPooled()){e.target=b;b.SendEvent(e);}}
        static void Resize(int width,int height){var old=target;target=new RenderTexture(width,height,24);target.Create();TouchlineApp.Instance.GetComponent<UIDocument>().panelSettings.targetTexture=target;if(old!=null){old.Release();UnityEngine.Object.DestroyImmediate(old);}}
        static void ScrollTo(string name){pendingTarget=name=="staff-training-progress"?"staff-training-status":name;Require(Root.Q(pendingTarget)!=null,"Missing element: "+pendingTarget);visibleFrames=0;pendingStarted=EditorApplication.timeSinceStartup;}
        static bool EnsurePendingVisible()
        {
            if(pendingTarget==null)return true;
            var element=Root.Q(pendingTarget);Require(element!=null,"Pending scroll target disappeared: "+pendingTarget);
            var scroll=Root.Query<ScrollView>().ToList().First(s=>s.Contains(element));var viewport=scroll.contentViewport.worldBound;var bounds=element.worldBound;
            double age=EditorApplication.timeSinceStartup-pendingStarted;
            if(age>3)throw new Exception("Scroll target could not become visible: "+pendingTarget+" bounds="+bounds+" viewport="+viewport+" offset="+scroll.scrollOffset+" range="+scroll.verticalScroller.lowValue+".."+scroll.verticalScroller.highValue);
            // FinishPage restores its remembered offset after35ms. Never race
            // that documented operation; after it, use measured bounds only.
            if(age<.05||viewport.height<=0||bounds.height<=0)return false;
            float top=viewport.yMin+8,bottom=viewport.yMax-8;
            float correction=bounds.yMin<top?bounds.yMin-top:bounds.yMax>bottom?bounds.yMax-bottom:0;
            if(Mathf.Abs(correction)>.5f){
                float scale=Mathf.Abs(scroll.contentContainer.LocalToWorld(new Vector2(0,1)).y-scroll.contentContainer.LocalToWorld(Vector2.zero).y);Require(scale>.0001f,"Invalid scroll transform.");
                var offset=scroll.scrollOffset;offset.y=Mathf.Clamp(offset.y+correction/scale,scroll.verticalScroller.lowValue,scroll.verticalScroller.highValue);scroll.scrollOffset=offset;visibleFrames=0;return false;
            }
            if(++visibleFrames<3)return false;pendingTarget=null;return true;
        }
        static void Capture(string name){var previous=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);RenderTexture.active=previous;}
        static void StaffTick(Career career,Database db,int day){career.life.day=day;typeof(Career).GetMethod("StaffDay",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(career,new object[]{db});}
        static bool Guarded(TouchlineApp app)=>(bool)typeof(TouchlineApp).GetProperty("VisualValidation",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(app);
        static void StatusBounds(){var label=Root.Q<Label>("staff-training-status");Require(label!=null&&label.resolvedStyle.fontSize>=11&&label.worldBound.width>120&&label.worldBound.height>=16,"Training status is clipped or unreadable.");var viewport=Root.Query<ScrollView>().ToList().First(s=>s.Contains(label)).contentViewport.worldBound;Require(label.worldBound.yMin>=viewport.yMin-1&&label.worldBound.yMax<=viewport.yMax+1,"Training status outside visible scroll viewport: "+label.worldBound+" versus "+viewport);}
        public static void Run()
        {
            var argument=Environment.GetCommandLineArgs().FirstOrDefault(a=>a.StartsWith("-touchlineStaffTrainingOutput="));string name=argument==null?"management-session2-staff-training-v5":argument.Substring(argument.IndexOf('=')+1);
            if(Path.GetFileName(name)!=name||string.IsNullOrWhiteSpace(name))throw new Exception("Simple new output name required.");output=Path.GetFullPath("../../artifacts/unity/"+name);if(Directory.Exists(output)||File.Exists(output))throw new Exception("Previous evidence protected.");Directory.CreateDirectory(output);SessionState.SetString(Key+"Output",output);ProjectBuilder.Configure();SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||TouchlineApp.Instance==null)return;if(last==Time.frameCount)return;last=Time.frameCount;
            try{
                if(!EnsurePendingVisible()||++frames<22)return;frames=0;var app=TouchlineApp.Instance;var career=app.Career;var db=app.Database;
                if(output==null)output=SessionState.GetString(Key+"Output","");Require(Guarded(app),"VisualValidation guard absent; no personal mutation or Save allowed.");
                switch(stage++){
                    case 0:
                        app.GetComponent<UIDocument>().panelSettings=UnityEngine.Object.Instantiate(app.GetComponent<UIDocument>().panelSettings);career.EnsureWorld(db);career.EnsureStaffMarket(db);Resize(1280,960);Invoke("Navigate","Staff et délégation");oldId=career.Staff("assistant").id;oldSkill=career.Staff("assistant").tactics;initialCash=career.life.cash;saves=app.SaveRequestCount;ScrollTo("staff-training-start-assistant");break;
                    case 1:
                        Require(Root.Q<Button>("staff-training-start-assistant").enabledInHierarchy,"Initial training is unavailable.");Capture("01-start-fold");Click("staff-training-start-assistant");break;
                    case 2:
                        Require(career.life.cash==initialCash&&app.SaveRequestCount==saves,"Opening confirmation spent or saved.");Require(Root.Q(className:"modal-panel")!=null,"Confirmation missing.");Capture("02-confirm-cost-fold");Click("Confirmer");oldDue=career.life.staffTrainingUntil;break;
                    case 3:
                        Require(initialCash-career.life.cash==career.StaffTrainingCost&&career.life.staffTrainingStaffId==oldId,"Wrong course cost or beneficiary.");Require(career.life.messages.Count(m=>m.subject=="Formation du staff engagée")==1,"Start mail duplicated.");Require(Root.Query<Button>().ToList().Where(b=>b.name.StartsWith("staff-training-start-")).All(b=>!b.enabledInHierarchy),"Second simultaneous training remains available.");ScrollTo("staff-training-progress");break;
                    case 4:
                        StatusBounds();Capture("03-progress-fold");Resize(1080,2520);Invoke("Build");ScrollTo("staff-training-progress");break;
                    case 5:
                        StatusBounds();Require(Root.Q<Label>("staff-training-status").text.Contains(career.Staff("assistant").name),"Beneficiary lost on rotation.");Capture("04-progress-portrait");career.Staff("assistant").until=career.life.day+1;StaffTick(career,db,career.life.day+1);Require(!career.StaffTrainingInProgress&&career.life.staffTrainingUntil==0,"Expiration left course locked.");Require(career.staffMarket.Single(s=>s.id==oldId).tactics==oldSkill,"Departed coach gained skills.");Invoke("Build");ScrollTo("staff-training-progress");break;
                    case 6:
                        StatusBounds();Capture("05-expired-portrait");var interrupted=career.life.messages.Single(m=>m.subject=="Formation interrompue");Require(!career.MessageNeedsDecision(interrupted)&&interrupted.reference.Contains(oldId),"Interruption mail is stale/actionable or detached.");StaffTick(career,db,career.life.day+1);Require(career.life.messages.Count(m=>m.subject=="Formation interrompue")==1,"Daily interruption spam.");
                        var free=career.staffMarket.First(s=>s.club==null&&s.role=="assistant"&&s.id.StartsWith("staff-free-"));newId=free.id;career.life.reputation=90;career.ProposeStaffContract(db,newId,Career.MonthlySalary(free.wage*2),2);StaffTick(career,db,career.life.day+2);career.SignStaffContract(db,newId);newSkill=free.tactics;career.TrainStaff("assistant");newDue=career.life.staffTrainingUntil;Resize(1280,960);Invoke("Build");ScrollTo("staff-training-progress");break;
                    case 7:
                        StatusBounds();Require(newId!=oldId&&career.life.staffTrainingStaffId==newId,"New course inherited departed identity.");Capture("06-replacement-course-fold");StaffTick(career,db,oldDue);Require(career.Staff("assistant").tactics==newSkill&&career.StaffTrainingInProgress,"Old deadline granted replacement bonus.");Invoke("Build");ScrollTo("staff-training-progress");break;
                    case 8:
                        StatusBounds();Capture("07-old-deadline-fold");StaffTick(career,db,newDue);Require(career.Staff("assistant").tactics==Math.Min(20,newSkill+1)&&!career.StaffTrainingInProgress,"Own course did not complete.");Require(career.life.messages.Count(m=>m.subject=="Formation terminée")==1&&career.life.messages.Single(m=>m.subject=="Formation terminée").reference.Contains(newId),"Completion is duplicated or assigned to predecessor.");Resize(1080,2520);Invoke("Build");ScrollTo("staff-training-progress");break;
                    case 9:
                        StatusBounds();Capture("08-completed-portrait");career.life.managerBanUntil=career.life.day+4;Invoke("Build");ScrollTo("staff-training-start-assistant");break;
                    case 10:
                        Require(!Root.Q<Button>("staff-training-start-assistant").enabledInHierarchy,"Suspended manager can start training.");
                        // Build/rotation can restore its old scroll during reflow.
                        // Re-target the actual disabled control after layout,
                        // then wait a separate phase before taking the picture.
                        ScrollTo("staff-training-start-assistant");break;
                    case 11:
                        var suspended=Root.Q<Button>("staff-training-start-assistant");Require(suspended!=null&&!suspended.enabledInHierarchy,"Suspended training control absent or enabled.");
                        var viewport=Root.Query<ScrollView>().ToList().First(s=>s.Contains(suspended)).contentViewport.worldBound;var bounds=suspended.worldBound;
                        File.WriteAllText(Path.Combine(output,"09-suspension-geometry.txt"),"button="+bounds+"; viewport="+viewport+"; enabled="+suspended.enabledInHierarchy+"; tooltip="+suspended.tooltip);
                        Capture("09-suspended-portrait");
                        Require(bounds.width>=44&&bounds.height>=44&&bounds.xMin>=viewport.xMin-.5f&&bounds.xMax<=viewport.xMax+.5f&&bounds.yMin>=viewport.yMin-.5f&&bounds.yMax<=viewport.yMax+.5f,"Suspended control must be fully visible and at least44px: "+bounds+" versus "+viewport);
                        File.WriteAllText(Path.Combine(output,"report.json"),"{\"passed\":true,\"stages\":12,\"viewportFold\":\"1280x960\",\"viewportPortrait\":\"1080x2520\",\"noPersonalSave\":true,\"physicalAndroid\":false,\"limitation\":\"Isolated staff-day fixture; no full-calendar simulation or measured Android performance. Vacancy and replacement use production expiry and contract negotiations.\"}");Debug.Log("TOUCHLINE_STAFF_TRAINING_WORKSPACE_OK");SessionState.SetBool(Key,false);EditorApplication.isPlaying=false;EditorApplication.delayCall+=()=>EditorApplication.Exit(0);break;
                }
            }catch(Exception ex){output=output??SessionState.GetString(Key+"Output","");if(!string.IsNullOrEmpty(output))File.WriteAllText(Path.Combine(output,"failure.txt"),"stage="+stage+"\n"+ex);Debug.LogException(ex);SessionState.SetBool(Key,false);EditorApplication.isPlaying=false;EditorApplication.delayCall+=()=>EditorApplication.Exit(1);}
        }
    }
}




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
    // Coordinator only. Requires this exact Run method in TouchlineApp.VisualValidation.
    [InitializeOnLoad] public static class EditorStaffCatalogSmoke
    {
        const string Key="TouchlineStaffCatalogSmoke";
        static int step,frames,last=-1,visibleFrames;
        static string output,pendingName;
        static double pendingSince;
        static RenderTexture target;
        static readonly string[] Ids={"real-rene-maric","real-roberto-vitiello","real-marcus-sorg"};
        static readonly int[] Days={0,23,112};
        static readonly List<Sample> samples=new List<Sample>();
        [Serializable] sealed class Sample { public string id,name,date,role,source,biography,contractExpiry,contractEvidence,cardBounds,viewport;public long monthlySalary;public int width,height;public bool cardAndAgentCaptured; }
        [Serializable] sealed class Report { public bool passed,personalSaveWrites,physicalAndroid;public string scope,limitations;public Sample[] samples; }
        static TouchlineApp App=>TouchlineApp.Instance;
        static VisualElement Root=>App.GetComponent<UIDocument>().rootVisualElement;
        static EditorStaffCatalogSmoke(){EditorApplication.update+=Tick;}
        static void Require(bool value,string message){if(!value)throw new Exception(message);}
        static object Call(string method,params object[] args)=>typeof(TouchlineApp).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(App,args);
        static bool Guarded=>(bool)typeof(TouchlineApp).GetProperty("VisualValidation",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(App);
        public static void Run()
        {
            var argument=Environment.GetCommandLineArgs().FirstOrDefault(a=>a.StartsWith("-touchlineStaffCatalogOutput="));
            string name=argument==null?"session8-real-staff-native-v1":argument.Substring(argument.IndexOf('=')+1);
            if(string.IsNullOrWhiteSpace(name)||Path.GetFileName(name)!=name)throw new Exception("A simple new evidence-directory name is required.");
            output=Path.GetFullPath("../../artifacts/unity/"+name);if(Directory.Exists(output)||File.Exists(output))throw new Exception("Previous evidence is protected.");
            Directory.CreateDirectory(output);SessionState.SetString(Key+"Output",output);
            SessionState.SetBool(Key+"HadSize",PlayerPrefs.HasKey("interface-size"));SessionState.SetInt(Key+"OldSize",PlayerPrefs.GetInt("interface-size",1));PlayerPrefs.SetInt("interface-size",1);
            ProjectBuilder.Configure();SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;
        }
        static void RestoreSize(){if(SessionState.GetBool(Key+"HadSize",false))PlayerPrefs.SetInt("interface-size",SessionState.GetInt(Key+"OldSize",1));else PlayerPrefs.DeleteKey("interface-size");}
        static void Resize(int width,int height)
        {
            var old=target;target=new RenderTexture(width,height,24);target.Create();App.GetComponent<UIDocument>().panelSettings.targetTexture=target;
            if(old!=null){old.Release();UnityEngine.Object.DestroyImmediate(old);}
        }
        static void Capture(string name)
        {
            var old=RenderTexture.active;RenderTexture.active=target;var texture=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);
            texture.ReadPixels(new Rect(0,0,target.width,target.height),0,0);texture.Apply();File.WriteAllBytes(Path.Combine(output,name+".png"),texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);RenderTexture.active=old;
        }
        static bool WaitForCard()
        {
            if(pendingName==null)return true;
            var heading=Root.Query<Label>().ToList().FirstOrDefault(l=>l.text.StartsWith(pendingName+" · "));
            Require(heading!=null,"Staff result heading missing: "+pendingName);var card=heading.parent;
            var scroll=Root.Query<ScrollView>().ToList().First(s=>s.Contains(card));var viewport=scroll.contentViewport.worldBound;var bounds=card.worldBound;
            double age=EditorApplication.timeSinceStartup-pendingSince;
            if(age>4)throw new Exception("Staff card cannot be fully reached: "+pendingName+" "+bounds+" viewport "+viewport);
            if(age<.08||bounds.height<=0||viewport.height<=0)return false;
            Require(bounds.height<=viewport.height-8,"Staff card exceeds viewport; inspect its ergonomics: "+bounds);
            float correction=bounds.yMin<viewport.yMin+4?bounds.yMin-viewport.yMin-4:bounds.yMax>viewport.yMax-4?bounds.yMax-viewport.yMax+4:0;
            if(Mathf.Abs(correction)>.5f){float scale=Mathf.Abs(scroll.contentContainer.LocalToWorld(new Vector2(0,1)).y-scroll.contentContainer.LocalToWorld(Vector2.zero).y);Require(scale>.0001f,"Invalid scroll scale");var offset=scroll.scrollOffset;offset.y=Mathf.Clamp(offset.y+correction/scale,scroll.verticalScroller.lowValue,scroll.verticalScroller.highValue);scroll.scrollOffset=offset;visibleFrames=0;return false;}
            if(++visibleFrames<3)return false;
            pendingName=null;return true;
        }
        static VisualElement CurrentCard(StaffMember member)=>Root.Query<Label>().ToList().First(l=>l.text.StartsWith(member.name+" · ")).parent;
        static string Stem(int index)=>(index+1).ToString("00")+"-"+Ids[index/2].Substring(5)+(index%2==0?"-landscape":"-portrait");
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||App==null||last==Time.frameCount)return;last=Time.frameCount;
            try{
                output??=SessionState.GetString(Key+"Output","");Require(Guarded,"Personal career access forbidden: register EditorStaffCatalogSmoke.Run in VisualValidation before launch.");
                if(!WaitForCard()||++frames<30)return;frames=0;
                if(step==0){App.GetComponent<UIDocument>().panelSettings=UnityEngine.Object.Instantiate(App.GetComponent<UIDocument>().panelSettings);App.Career.EnsureWorld(App.Database);}
                int index=step/4,phase=step%4;
                if(index>=6){
                    File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(new Report{passed=true,personalSaveWrites=false,physicalAndroid=false,samples=samples.ToArray(),scope="Actual Staff page filtered to three sourced identities, then actual agent dialog, at 1600x700 and 1080x1920. In-memory fixture dates only, no simulated season.",limitations="Render captures and label/geometry assertions require independent visual review. Native desktop UI, no Android touch/performance claim. Published expiry appears in Bayern biography; unknown expiries remain labelled simulated in other biographies, exact model dates are reported here only."},true));
                    RestoreSize();SessionState.SetBool(Key,false);Debug.Log("TOUCHLINE_REAL_STAFF_CATALOG_OK");EditorApplication.Exit(0);return;
                }
                string id=Ids[index/2];
                if(phase==0){
                    Call("CloseModal");App.Career.life.day=Days[index/2];App.Career.EnsureStaffMarket(App.Database);var member=App.Career.staffMarket.Single(s=>s.id==id);
                    typeof(TouchlineApp).GetField("staffSearch",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(App,member.name);typeof(TouchlineApp).GetField("staffFilter",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(App,"Réels");typeof(TouchlineApp).GetField("staffPage",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(App,0);
                    Resize(index%2==0?1600:1080,index%2==0?700:1920);Call("Navigate","Staff et délégation");
                }else if(phase==1){
                    pendingName=App.Career.staffMarket.Single(s=>s.id==id).name;pendingSince=EditorApplication.timeSinceStartup;visibleFrames=0;
                }else if(phase==2){
                    var member=App.Career.staffMarket.Single(s=>s.id==id);var card=CurrentCard(member);string content=string.Join("\n",card.Query<Label>().ToList().Select(l=>l.text));var viewport=Root.Query<ScrollView>().ToList().First(s=>s.Contains(card)).contentViewport.worldBound;
                    Require(content.Contains(member.biography)&&content.Contains(member.source)&&content.Contains("/ mois")&&content.Contains("Notes et salaire estimés"),"Source, estimate label or monthly salary missing from real staff card");
                    Require(member.id!=Ids[0]||content.Contains("30/06/2029"),"Published Bayern expiry not visible");
                    Require(card.worldBound.xMin>=viewport.xMin-1&&card.worldBound.xMax<=viewport.xMax+1,"Staff card horizontally clipped");
                    Capture(Stem(index)+"-card");samples.Add(new Sample{id=id,name=member.name,role=member.role,date=App.Career.Date.ToString("yyyy-MM-dd"),source=member.source,biography=member.biography,contractExpiry=Career.Epoch.AddDays(member.until).ToString("yyyy-MM-dd"),contractEvidence=id==Ids[0]?"published":"simulated, not verified",monthlySalary=Career.MonthlySalary(member.wage),width=target.width,height=target.height,cardBounds=card.worldBound.ToString(),viewport=viewport.ToString()});Call("StaffDialog",id);
                }else{
                    var panel=Root.Q(className:"modal-panel");Require(panel!=null,"Agent dialog missing");string labels=string.Join("\n",panel.Query<Label>().ToList().Select(l=>l.text));var member=App.Career.staffMarket.Single(s=>s.id==id);Require(labels.Contains(member.name)&&labels.Contains("Salaire souhaité")&&labels.Contains("/ mois"),"Agent monthly range or identity missing");Require(panel.Q<LongField>()!=null&&panel.Q<LongField>().label.Contains("mensuelle"),"Agent offer does not use monthly salary");Capture(Stem(index)+"-agent");samples.Last().cardAndAgentCaptured=true;
                }
                step++;
            }catch(Exception error){RestoreSize();SessionState.SetBool(Key,false);if(!string.IsNullOrEmpty(output))File.WriteAllText(Path.Combine(output,"failure.txt"),"step="+step+Environment.NewLine+error);Debug.LogException(error);EditorApplication.Exit(1);}
        }
    }
}

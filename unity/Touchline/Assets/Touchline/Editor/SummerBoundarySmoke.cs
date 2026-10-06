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
    [InitializeOnLoad] public static class SummerBoundarySmoke
    {
        const string Key="SummerBoundarySmoke";
        static int stage,frames,last=-1;static string output,fixtureId,opponent;static int friendlyDay,campDay;static long campCost;static RenderTexture target;
        static TouchlineApp App=>TouchlineApp.Instance;
        static VisualElement Root=>App.GetComponent<UIDocument>().rootVisualElement;
        static SummerBoundarySmoke(){EditorApplication.update+=Tick;}
        static string Output(){var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-touchlineOutput");return Path.GetFullPath(i>=0&&i+1<args.Length?args[i+1]:"../../artifacts/unity/session7-summer-boundary");}
        public static void Run(){output=Output();if(Directory.Exists(output)&&Directory.EnumerateFileSystemEntries(output).Any())throw new Exception("Preserve previous summer boundary proof");Directory.CreateDirectory(output);SessionState.SetBool(Key+".hadSize",PlayerPrefs.HasKey("interface-size"));SessionState.SetInt(Key+".oldSize",PlayerPrefs.GetInt("interface-size",1));PlayerPrefs.SetInt("interface-size",1);ProjectBuilder.Configure();SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;}
        static void Restore(){if(SessionState.GetBool(Key+".hadSize",false))PlayerPrefs.SetInt("interface-size",SessionState.GetInt(Key+".oldSize",1));else PlayerPrefs.DeleteKey("interface-size");}
        static void Require(bool value,string message){if(!value)throw new Exception(message);}
        static void Call(string name,params object[] args)=>typeof(TouchlineApp).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(App,args);
        static void Click(string name){var b=Root.Query<Button>().ToList().First(x=>x.name==name||x.text==name);Require(b.enabledInHierarchy,"Disabled action "+name);using(var e=NavigationSubmitEvent.GetPooled()){e.target=b;b.SendEvent(e);}}
        static void Resize(int w,int h){var old=target;target=new RenderTexture(w,h,24);target.Create();App.GetComponent<UIDocument>().panelSettings.targetTexture=target;if(old!=null){old.Release();UnityEngine.Object.DestroyImmediate(old);}}
        static void Capture(string name){var previous=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);RenderTexture.active=previous;}
        static void ShowCamp(){Root.schedule.Execute(()=>{var card=Root.Q(className:"calendar-preparation-card");if(card!=null)Root.Q<ScrollView>("calendar-workspace").ScrollTo(card);}).StartingIn(100);}
        static bool TextContains(string text)=>Root.Query<Label>().ToList().Any(l=>l.text!=null&&l.text.Contains(text));
        static void BookingIntegrity(){var c=App.Career;Require(c.world.fixtures.Any(f=>f.id==fixtureId&&f.day==friendlyDay&&!f.played),"Booked friendly ID/date lost");Require(!c.world.history.Any(f=>f.id==fixtureId),"Future friendly incorrectly archived");Require(c.camps.Count==1&&c.camps[0].year==2027&&c.camps[0].start==campDay&&c.camps[0].cost==campCost,"Camp changed on season/rotation");Require(c.life.ledger.Count(e=>e.label=="Garantie de match amical")==1&&c.life.ledger.Count(e=>e.label.StartsWith("Stage de préparation"))==1,"Booking charged twice");}
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||App==null||last==Time.frameCount)return;last=Time.frameCount;if(++frames<36)return;frames=0;
            try{output??=Output();switch(stage++){
                case 0:
                    Require((bool)typeof(TouchlineApp).GetProperty("VisualValidation",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(App),"Personal save access forbidden");
                    App.GetComponent<UIDocument>().panelSettings=UnityEngine.Object.Instantiate(App.GetComponent<UIDocument>().panelSettings);
                    var c=App.Career;c.EnsureWorld(App.Database);foreach(var f in c.world.fixtures){f.played=true;f.winner=f.home;}foreach(var cup in c.world.cups)cup.finished=true;foreach(var pair in c.world.promotionPairs)pair.complete=true;
                    // Synthetic season-completion checkpoint; no personal career is loaded or saved.
                    App.Database.pyramidRules=null;c.life.day=(new DateTime(2027,6,12)-Core.Career.Epoch).Days;c.life.nextFixture=int.MaxValue;c.life.cash=100000000;c.camps.Clear();c.friendlies.Clear();c.life.ledger.Clear();
                    opponent=c.FriendlyRecommendations(App.Database).First().id;campDay=(new DateTime(2027,6,20)-Core.Career.Epoch).Days;friendlyDay=(new DateTime(2027,7,1)-Core.Career.Epoch).Days;
                    c.ScheduleCamp("local",campDay);campCost=c.camps.Single().cost;c.InviteFriendly(App.Database,opponent,friendlyDay,true);c.AdvanceDay(App.Database);c.AdvanceDay(App.Database);
                    fixtureId=c.world.fixtures.Single(f=>f.league=="friendly"&&!f.played&&(f.home==c.club||f.away==c.club)).id;
                    Resize(1600,700);Call("Navigate","Calendrier");Click("calendar-tab-preparation");ShowCamp();break;
                case 1:
                    Capture("before-boundary-preparation-landscape");BookingIntegrity();Require(App.Career.world.year==2026&&App.Career.PreparationYear==2027&&TextContains("PRÉPARATION ESTIVALE · 2027")&&TextContains("Centre du club"),"Upcoming summer not visible before boundary");
                    App.Career.AdvanceDay(App.Database);Call("Navigate","Calendrier");ShowCamp();break;
                case 2:
                    Capture("after-boundary-preparation-landscape");BookingIntegrity();Require(App.Career.world.year==2027,"Season not advanced on June 15");Resize(1080,2520);ShowCamp();break;
                case 3:
                    Capture("after-boundary-preparation-portrait");BookingIntegrity();Require(TextContains("Centre du club"),"Camp lost on folding");Click("calendar-friendly");break;
                case 4:
                    Capture("after-boundary-friendly-dialog-portrait");Require(Root.Q("calendar-friendly-dialog")!=null,"Friendly planning unavailable");Click("Fermer");Resize(1600,700);Click("calendar-tab-month");break;
                case 5:
                    Capture("after-boundary-calendar-landscape");BookingIntegrity();Click("calendar-next");break;
                case 6:
                    Capture("after-boundary-july-friendly");BookingIntegrity();Require(Root.Q("calendar-day-"+friendlyDay)!=null,"Friendly date missing from July");
                    File.WriteAllText(Path.Combine(output,"report.json"),"{\"passed\":true,\"checks\":[\"actual June14-June15 advancement\",\"paid camp visible before and after\",\"friendly identity/date preserved\",\"no premature history\",\"one charge per booking\",\"portrait-landscape state preservation\"],\"physicalAndroid\":false}");Restore();SessionState.SetBool(Key,false);Debug.Log("TOUCHLINE_SUMMER_BOUNDARY_OK");EditorApplication.Exit(0);break;
            }}catch(Exception e){File.WriteAllText(Path.Combine(output??Output(),"failure.txt"),e.ToString());Restore();SessionState.SetBool(Key,false);Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}

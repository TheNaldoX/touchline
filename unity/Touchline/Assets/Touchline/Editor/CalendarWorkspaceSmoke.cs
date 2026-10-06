using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Touchline.Core;

namespace Touchline.Editor
{
    [InitializeOnLoad] public static class CalendarWorkspaceSmoke
    {
        static int stage,frames,last=-1,day;static long cash;static string before,opponent,campDate,friendDate,output;static RenderTexture target;
        static VisualElement Root=>TouchlineApp.Instance.GetComponent<UIDocument>().rootVisualElement;
        static CalendarWorkspaceSmoke(){EditorApplication.update+=Tick;}
        public static void Run(){ProjectBuilder.Configure();SessionState.SetBool("CalendarWorkspaceSmoke",true);EditorApplication.isPlaying=true;}
        static void Click(string name){var button=Root.Query<Button>().ToList().FirstOrDefault(b=>b.name==name||b.text==name);if(button==null&&name=="Calendrier"){Click("Plus");button=Root.Query<Button>().ToList().FirstOrDefault(b=>b.text==name);}if(button==null||!button.enabledInHierarchy)throw new Exception("Calendar button unavailable: "+name);using(var evt=NavigationSubmitEvent.GetPooled()){evt.target=button;button.SendEvent(evt);}}
        static void Resize(int width,int height){var old=target;target=new RenderTexture(width,height,24);target.Create();TouchlineApp.Instance.GetComponent<UIDocument>().panelSettings.targetTexture=target;if(old!=null){old.Release();UnityEngine.Object.DestroyImmediate(old);}}
        static void Capture(string name){var old=RenderTexture.active;RenderTexture.active=target;var texture=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,target.width,target.height),0,0);texture.Apply();File.WriteAllBytes(Path.Combine(output,name+".png"),texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);RenderTexture.active=old;}
        static void Bounds(string name){var targetElement=Root.Q(name);if(targetElement==null)throw new Exception("Calendar element absent: "+name);if(targetElement.worldBound.xMin<Root.worldBound.xMin-1||targetElement.worldBound.xMax>Root.worldBound.xMax+1)throw new Exception("Calendar horizontal overflow: "+name);}
        static void Tick()
        {
            if(!SessionState.GetBool("CalendarWorkspaceSmoke",false)||!EditorApplication.isPlaying||TouchlineApp.Instance==null||last==Time.frameCount)return;last=Time.frameCount;if(++frames<25)return;frames=0;
            try{var app=TouchlineApp.Instance;output=Path.GetFullPath("../../artifacts/unity/calendar-workspace");Directory.CreateDirectory(output);
                switch(stage++){
                    case 0:
                        app.GetComponent<UIDocument>().panelSettings=UnityEngine.Object.Instantiate(app.GetComponent<UIDocument>().panelSettings);app.Career.EnsureWorld(app.Database);day=app.Career.life.day;
                        app.Career.camps.Clear();app.Career.friendlies.Clear();app.Career.life.cash=10000000;
                        app.Career.world.fixtures.RemoveAll(f=>(f.home==app.Career.club||f.away==app.Career.club)&&f.day<day+40);
                        opponent=app.Career.FriendlyRecommendations(app.Database).First().id;
                        app.Career.world.fixtures.Add(new Fixture{id="calendar-smoke-official",home=app.Career.club,away=opponent,day=day+21,league=app.Database.clubs.First(c=>c.id==app.Career.club).league,published=false});
                        app.Career.life.nextFixture=day+21;Resize(1280,900);Click("Calendrier");break;
                    case 1:Bounds("calendar-grid");if(Root.Q("calendar-day-"+day)==null)throw new Exception("Today absent from month grid");before=JsonUtility.ToJson(app.Career);Capture("calendar-wide");Click("calendar-next");break;
                    case 2:if(before!=JsonUtility.ToJson(app.Career))throw new Exception("Month navigation changed career state");Click("calendar-previous");Click("calendar-today");Root.Q<DropdownField>("calendar-status").value="Joués";break;
                    case 3:if(before!=JsonUtility.ToJson(app.Career))throw new Exception("Calendar filtering changed career state");Root.Q<DropdownField>("calendar-status").value="Tous";Click("calendar-camp");break;
                    case 4:
                        Root.Q<DropdownField>("calendar-camp-kind").value="Altitude";
                        var departure=Root.Q<DropdownField>("calendar-camp-date");departure.index=3;campDate=departure.value;
                        cash=app.Career.life.cash;Capture("camp-dialog-wide");Resize(1080,2520);break;
                    case 5:
                        Bounds("calendar-camp-dialog");if(Root.Q<DropdownField>("calendar-camp-kind").value!="Altitude"||Root.Q<DropdownField>("calendar-camp-date").value!=campDate)throw new Exception("Camp draft lost on rotation");
                        Capture("camp-dialog-portrait");Click("calendar-camp-book");break;
                    case 6:
                        if(app.Career.camps.Count!=1||app.Career.camps[0].kind!="altitude")throw new Exception("Camp booking did not use chosen programme");
                        if(app.Career.life.cash!=cash-app.Career.camps[0].cost||app.Career.life.day!=day)throw new Exception("Camp charge/day mismatch");
                        Bounds("calendar-grid");var copy=Root.Q(className:"calendar-heading-copy");if(copy.worldBound.width<Root.worldBound.width*.7f||copy.worldBound.height>180)throw new Exception("Calendar portrait heading collapsed");Capture("calendar-portrait");Click("calendar-tab-preparation");break;
                    case 7:Capture("preparation-portrait");Click("calendar-friendly");break;
                    case 8:
                        var friendly=Root.Q<DropdownField>("calendar-friendly-date");friendly.index=9;friendDate=friendly.value;Root.Q<Toggle>("calendar-friendly-home").value=false;Resize(1600,700);break;
                    case 9:
                        Bounds("calendar-friendly-dialog");if(Root.Q<DropdownField>("calendar-friendly-date").value!=friendDate||Root.Q<Toggle>("calendar-friendly-home").value)throw new Exception("Friendly draft lost on rotation");
                        if(Root.Query<Label>().ToList().Any(l=>l.ClassListContains("calendar-guarantee")&&!l.text.Contains("0 €")))throw new Exception("Away friendly guarantee should be zero");
                        Capture("friendly-dialog-landscape");cash=app.Career.life.cash;Click("calendar-invite-"+opponent);break;
                    case 10:
                        if(app.Career.friendlies.Count!=1||app.Career.friendlies[0].status!="pending"||app.Career.friendlies[0].home||app.Career.friendlies[0].guarantee!=0)throw new Exception("Friendly invitation terms mismatch");
                        if(app.Career.life.cash!=cash||app.Career.life.day!=day)throw new Exception("Invitation charged or advanced time before response");
                        if(!Root.Query<Label>().ToList().Any(l=>l.ClassListContains("calendar-invitation-status")&&l.text.Contains("Réponse attendue")))throw new Exception("Pending invitation status absent");
                        Capture("preparation-pending-landscape");Click("calendar-friendly");break;
                    case 11:
                        if(Root.Q<Button>("calendar-invite-"+opponent).enabledSelf)throw new Exception("Duplicate invitation remains enabled");Click("Fermer");Click("calendar-tab-month");Click("calendar-next");Click("calendar-day-"+(day+21));break;
                    case 12:
                        if(!Root.Q("calendar-day-agenda").Query<Label>().ToList().Any(l=>l.text.Contains("Date simulée")))throw new Exception("Fixture date incorrectly presented as verified");
                        Capture("calendar-selected-landscape");Click("Détails");break;
                    case 13:Bounds("calendar-fixture");if(!Root.Query<Button>().ToList().Any(b=>b.text=="Préparer le match"))throw new Exception("Next fixture preparation action missing");Click("Fermer");
                        File.WriteAllText(Path.Combine(output,"report.json"),"{\"passed\":true,\"monthNavigationReadOnly\":true,\"filtersReadOnly\":true,\"campTermsAndCharge\":true,\"campDraftRotation\":true,\"friendlyTermsAndNoPrematureCharge\":true,\"friendlyDraftRotation\":true,\"pendingStatusAndDuplicateGuard\":true,\"fixtureDateHonesty\":true,\"portraitLandscape\":true,\"physicalAndroid\":false}");
                        SessionState.SetBool("CalendarWorkspaceSmoke",false);Debug.Log("TOUCHLINE_CALENDAR_WORKSPACE_OK");EditorApplication.Exit(0);break;
                }
            }catch(Exception e){SessionState.SetBool("CalendarWorkspaceSmoke",false);Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}

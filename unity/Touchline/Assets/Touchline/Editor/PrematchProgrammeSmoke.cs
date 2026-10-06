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
    [InitializeOnLoad] public static class PrematchProgrammeSmoke
    {
        static int stage,frames,last=-1;static RenderTexture target;static string output,matchBefore,selected;static Fixture fixture;
        static TouchlineApp App=>TouchlineApp.Instance;
        static VisualElement Root=>App.GetComponent<UIDocument>().rootVisualElement;
        static MatchArena Arena=>UnityEngine.Object.FindFirstObjectByType<MatchArena>();
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static void Call(string method,params object[] args)=>typeof(TouchlineApp).GetMethod(method,Private).Invoke(App,args);
        static T Field<T>(string name)=>(T)typeof(TouchlineApp).GetField(name,Private).GetValue(App);
        static PrematchProgrammeSmoke(){EditorApplication.update+=Tick;}
        [Serializable] sealed class Failure { public int stage;public string error; }
        public static void Run()
        {
            const string prefix="-touchlineProgrammeOutput=";
            var choices=Environment.GetCommandLineArgs().Where(a=>a.StartsWith(prefix,StringComparison.Ordinal)).ToArray();
            if(choices.Length>1)throw new ArgumentException("Specify one programme output name.");
            string name=choices.Length==0?"prematch-programme-v1":choices[0].Substring(prefix.Length);
            if(name.Length==0||name.Length>80||name.Any(c=>!(c>='a'&&c<='z'||c>='A'&&c<='Z'||c>='0'&&c<='9'||c=='-'||c=='_')))
                throw new ArgumentException("Programme output must be a basename of 1–80 ASCII letters, digits, hyphens or underscores.");
            output=Path.GetFullPath(Path.Combine("../../artifacts/unity",name));
            if(Directory.Exists(output)||File.Exists(output))throw new IOException("Preserving existing programme proof: "+output);
            Directory.CreateDirectory(output);SessionState.SetString("PrematchProgrammeOutput",output);
            ProjectBuilder.Configure();SessionState.SetBool("PrematchProgrammeSmoke",true);EditorApplication.isPlaying=true;
        }
        static void Click(string name){var button=Root.Q<Button>(name)??throw new Exception("Missing button: "+name);if(!button.enabledInHierarchy)throw new Exception("Disabled button: "+name);using(var e=NavigationSubmitEvent.GetPooled()){e.target=button;button.SendEvent(e);}}
        static void Resize(int width,int height){var old=target;target=new RenderTexture(width,height,24);target.Create();App.GetComponent<UIDocument>().panelSettings.targetTexture=target;if(old!=null){old.Release();UnityEngine.Object.DestroyImmediate(old);}}
        static void Capture(string name){var old=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);RenderTexture.active=old;}
        static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
        static void Frozen(){Check(App.Career.match.clock==0&&Arena.Paused,"Presentation advanced simulation");Check(JsonUtility.ToJson(App.Career.match)==matchBefore,"Read-only programme modified match state");}
        static void Controls(){foreach(var b in Root.Q(className:"prematch-controls").Query<Button>().ToList()){var r=b.worldBound;Check(r.height>=44&&r.xMin>=0&&r.yMin>=0&&r.xMax<=Root.worldBound.xMax+1&&r.yMax<=Root.worldBound.yMax+1,"Inaccessible control: "+b.name);}Frozen();}
        static void Lineup(string club,bool possession)
        {
            var expected=PrematchBriefing.Starters(App.Career.match,club);var shown=Root.Q("prematch-starters").Query<Button>(className:"prematch-starter").ToList();
            Check(expected.Select(a=>a.id).SequenceEqual(shown.Select(r=>(string)r.userData))&&shown.Count==11,"Wrong starting eleven");
            foreach(var actor in expected){var slot=PrematchProgramme.Position(App.Career.match,club,actor.slot,possession);var anchor=Root.Q("prematch-position-"+actor.slot);Check(Mathf.Abs(anchor.style.left.value.value-Mathf.Lerp(10,90,slot.x/100))<.001f,"Incorrect tactical flank");Check(Mathf.Abs(anchor.style.top.value.value-(100-Mathf.Lerp(13,88,slot.y/100)))<.001f,"Incorrect phase depth");}
        }
        static string PublicCopy()=>string.Join("|",Root.Q("prematch-player-details").Query<Label>().ToList().Where(l=>!l.ClassListContains("prematch-avatar-initials")&&!l.ClassListContains("prematch-avatar-caption")).Select(l=>l.text));
        static void Details()
        {
            Check(Field<string>("introSelectedPlayer")==selected&&!Field<bool>("introAutomatic"),"Selection/automatic pause lost");
            var panel=Root.Q("prematch-player-details");Check(panel.resolvedStyle.display!=DisplayStyle.None,"Selected player panel hidden");
            Check(panel.Query<Label>().ToList().Any(l=>l.text==App.Database.Find(selected).name),"Wrong identity");
            var avatar=panel.Q("prematch-player-portrait");Check(avatar!=null,"Portrait container missing");
            Check(avatar.Q<Image>()?.image!=null||!string.IsNullOrEmpty(avatar.Q<Label>(className:"prematch-avatar-initials")?.text),"Offline portrait or explicit initials absent");
            var bounds=panel.worldBound;var viewport=Root.Q<ScrollView>("prematch-scroll").contentViewport.worldBound;Check(bounds.xMin>=viewport.xMin-1&&bounds.xMax<=viewport.xMax+1,"Selected panel overflows horizontally");
            Check(bounds.yMin>=viewport.yMin-1&&bounds.yMax<=viewport.yMax+1,"Selected panel not reachable through scroll");
        }
        static void Tick()
        {
            if(!SessionState.GetBool("PrematchProgrammeSmoke",false)||!EditorApplication.isPlaying||App==null||last==Time.frameCount)return;
            last=Time.frameCount;if(++frames<45)return;frames=0;
            try{
                output=SessionState.GetString("PrematchProgrammeOutput","");if(string.IsNullOrEmpty(output)||!Directory.Exists(output))throw new IOException("Programme proof output was not initialized by Run.");
                switch(stage++){
                    case 0:
                        App.Career.EnsureWorld(App.Database);Resize(1280,966);fixture=App.Career.NextFixture();
                        if(fixture.home==App.Career.club){string other=fixture.away;fixture.away=fixture.home;fixture.home=other;}
                        App.Career.world.fixtures.Add(new Fixture{id="programme-proof-result",league="friendly",home=fixture.away,away=fixture.home,day=fixture.day-1,played=true,hg=2,ag=1});
                        App.Career.life.day=fixture.day;App.Career.life.nextFixture=fixture.day;Call("StartCareerMatch",false);Call("Build");matchBefore=JsonUtility.ToJson(App.Career.match);break;
                    case 1:Controls();Click("prematch-auto");Click("prematch-next");break;
                    case 2:Controls();Check(Root.Q("prematch-last-meeting")!=null,"Last meeting absent");Check(Root.Query<VisualElement>(className:"prematch-result").ToList().Count>=3,"Detailed actual results absent");Capture("01-career-results");Click("prematch-next");break;
                    case 3:Controls();Lineup(fixture.home,false);Capture("02-without-ball");Click("prematch-with-ball");break;
                    case 4:Controls();Lineup(fixture.home,true);Check(!Field<bool>("introAutomatic"),"Exploration failed to pause auto");Capture("03-with-ball");selected=PrematchBriefing.Starters(App.Career.match,fixture.home).First(a=>a.slot==1).id;Click("prematch-player-1");break;
                    case 5:
                        Controls();Details();Capture("04-player-mission");
                        var player=App.Database.Find(selected);float rating=player.rating,potential=player.potential;var attributes=player.attributes;var labels=PublicCopy();
                        player.rating=99;player.potential=99;player.attributes=new[]{new AttributeValue{key="shooting",value=99}};Call("RenderIntroPlayer",fixture.home);
                        Check(labels==PublicCopy(),"Hidden attributes entered public programme");player.rating=rating;player.potential=potential;player.attributes=attributes;
                        Resize(1080,2520);break;
                    case 6:Controls();Lineup(fixture.home,true);Click("prematch-starter-1");break;
                    case 7:Controls();Details();Capture("05-folded-player-mission");Click("prematch-without-ball");break;
                    case 8:Controls();Lineup(fixture.home,false);Check(Field<string>("introSelectedPlayer")==selected,"Phase change discarded selection");Capture("06-folded-without-ball");Click("prematch-next");Resize(1600,700);break;
                    case 9:Controls();Lineup(fixture.away,false);Check(Field<string>("introSelectedPlayer")==null,"Selection leaked across teams");Capture("07-away-landscape");selected=PrematchBriefing.Starters(App.Career.match,fixture.away).First(a=>a.slot==4).id;Click("prematch-starter-4");break;
                    case 10:Controls();Details();Capture("08-landscape-player-mission");Call("LaunchBack");break;
                    case 11:Check(Root.Q("prematch-presentation")==null&&!Field<bool>("presentationPending"),"Android back failed cleanup");Check(Field<AudioSource>("anthem")==null||!Field<AudioSource>("anthem").isPlaying,"Audio continues after exit");Click("prematch-replay");break;
                    case 12:Controls();Click("prematch-kickoff");break;
                    case 13:
                        Check(App.Career.match.clock>0&&!Arena.Paused&&Root.Q("prematch-presentation")==null&&!Field<bool>("presentationPending"),"Kickoff failed");
                        File.WriteAllText(Path.Combine(output,"report.json"),"{\"passed\":true,\"stages\":14,\"syntheticFixture\":true,\"actualAwayManagerPhases\":true,\"playerSelection\":true,\"attributePrivacy\":true,\"careerResults\":true,\"frozenUntilKickoff\":true,\"portraitAndLandscapeScroll\":true,\"androidBackPath\":true,\"controls44px\":true,\"physicalAndroid\":false}");
                        SessionState.SetBool("PrematchProgrammeSmoke",false);Debug.Log("TOUCHLINE_PROGRAMME_OK");EditorApplication.Exit(0);break;
                }
            }catch(Exception e){SessionState.SetBool("PrematchProgrammeSmoke",false);if(!string.IsNullOrEmpty(output)&&Directory.Exists(output))File.WriteAllText(Path.Combine(output,"failure.json"),JsonUtility.ToJson(new Failure{stage=stage,error=e.ToString()},true));Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}

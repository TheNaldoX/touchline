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
    [InitializeOnLoad] public static class PrematchPresentationSmoke
    {
        static int stage,frames,last=-1,oldMotion,animations;static RenderTexture target;static string output;static Fixture fixture;static float audioTime;
        static TouchlineApp App=>TouchlineApp.Instance;
        static VisualElement Root=>App.GetComponent<UIDocument>().rootVisualElement;
        static MatchArena Arena=>UnityEngine.Object.FindFirstObjectByType<MatchArena>();
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static void Call(string method,params object[] args)=>typeof(TouchlineApp).GetMethod(method,Private).Invoke(App,args);
        static T Field<T>(string name)=>(T)typeof(TouchlineApp).GetField(name,Private).GetValue(App);
        static void Set(string name,object value)=>typeof(TouchlineApp).GetField(name,Private).SetValue(App,value);
        static PrematchPresentationSmoke(){EditorApplication.update+=Tick;}
        public static void Run(){ProjectBuilder.Configure();SessionState.SetBool("PrematchPresentationSmoke",true);EditorApplication.isPlaying=true;}
        static void Click(string name){var button=Root.Q<Button>(name)??throw new Exception("Missing button: "+name);if(!button.enabledInHierarchy)throw new Exception("Disabled button: "+name);using(var e=NavigationSubmitEvent.GetPooled()){e.target=button;button.SendEvent(e);}}
        static void Resize(int width,int height){var old=target;target=new RenderTexture(width,height,24);target.Create();App.GetComponent<UIDocument>().panelSettings.targetTexture=target;if(old!=null){old.Release();UnityEngine.Object.DestroyImmediate(old);}}
        static void Capture(string name){var old=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);RenderTexture.active=old;}
        static void Frozen(){if(App.Career.match.clock!=0||!Arena.Paused)throw new Exception("Presentation advanced the simulation");}
        static void Controls(){var panel=Root.Q("prematch-panel")??throw new Exception("Introduction lost");foreach(var b in panel.Q(className:"prematch-controls").Query<Button>().ToList()){var r=b.worldBound;if(r.height<44||r.xMin<0||r.yMin<0||r.xMax>Root.worldBound.xMax+1||r.yMax>Root.worldBound.yMax+1)throw new Exception("Inaccessible intro control "+b.name+" "+r);}Frozen();}
        static void Lineup(string club){var expected=PrematchBriefing.Starters(App.Career.match,club).Select(a=>a.id).ToArray();var shown=Root.Q("prematch-starters").Query<VisualElement>(className:"prematch-starter").ToList().Select(r=>(string)r.userData).ToArray();if(!expected.SequenceEqual(shown)||shown.Length!=11)throw new Exception("Wrong actual eleven for "+club);}
        static void Tick()
        {
            if(!SessionState.GetBool("PrematchPresentationSmoke",false)||!EditorApplication.isPlaying||App==null||last==Time.frameCount)return;
            last=Time.frameCount;if(++frames<90)return;frames=0;
            try{
                output=Path.GetFullPath("../../artifacts/unity/prematch-presentation-v3");Directory.CreateDirectory(output);
                switch(stage++){
                    case 0:
                        oldMotion=PlayerPrefs.GetInt("reduce-motion",0);PlayerPrefs.SetInt("reduce-motion",0);App.Career.EnsureWorld(App.Database);Resize(1280,966);
                        fixture=App.Career.NextFixture();if(fixture.home==App.Career.club){string other=fixture.away;fixture.away=fixture.home;fixture.home=other;}
                        // Isolated synthetic European fixture tests anthem and away-manager mapping.
                        fixture.league="ucl";fixture.knockout=false;fixture.venue=App.Database.clubs.First(c=>c.id==fixture.home).stadium;var cup=App.Career.world.cups.First(c=>c.id=="ucl");cup.entrants=new System.Collections.Generic.List<string>{fixture.home,fixture.away};
                        App.Career.world.fixtures.Add(new Fixture{id="intro-proof-result",league="ucl",home=fixture.away,away=fixture.home,day=fixture.day-1,played=true,hg=2,ag=1});
                        App.Career.life.day=fixture.day;App.Career.life.nextFixture=fixture.day;Call("StartCareerMatch",false);Call("Build");break;
                    case 1:Controls();Capture("01-affiche-fold-open");if(Field<AudioSource>("anthem")?.clip?.name!="ucl")throw new Exception("Competition anthem not bound");audioTime=Field<AudioSource>("anthem").time;Click("prematch-auto");Call("Build");break;
                    case 2:Controls();if(Field<bool>("introAutomatic"))throw new Exception("Rebuild lost presentation pause");if(Field<AudioSource>("anthem").time+0.1f<audioTime)throw new Exception("Rebuild restarted anthem");Click("prematch-next");break;
                    case 3:Controls();if(Root.Q("prematch-standings")==null)throw new Exception("Career standings absent");Capture("02-enjeu");Click("prematch-next");break;
                    case 4:Controls();Lineup(fixture.home);Capture("03-home-lineup");Resize(1080,2520);break;
                    case 5:Controls();Lineup(fixture.home);if(Field<int>("introChapter")!=2)throw new Exception("Fold reset chapter");Capture("04-lineup-folded");Click("prematch-next");break;
                    case 6:Controls();Lineup(fixture.away);Capture("05-away-lineup-folded");Resize(1600,700);break;
                    case 7:Controls();Lineup(fixture.away);var pitch=Root.Q("prematch-pitch");var scroll=Root.Q<ScrollView>("prematch-scroll");if(pitch.worldBound.yMax>scroll.contentViewport.worldBound.yMax+1)throw new Exception("Full lineup pitch must fit landscape without scrolling");if(Root.Q("prematch-starters").worldBound.yMax>scroll.contentViewport.worldBound.yMax+1)throw new Exception("All eleven names must fit landscape without scrolling");Capture("06-lineup-landscape");Click("prematch-audio");if(!Field<AudioSource>("anthem").mute)throw new Exception("Audio toggle failed");Click("prematch-audio");Call("OnApplicationPause",true);audioTime=Field<float>("introElapsed");break;
                    case 8:Frozen();if(Field<float>("introElapsed")!=audioTime)throw new Exception("Presentation advances in background");if(Field<AudioSource>("anthem").isPlaying)throw new Exception("Anthem continues in background");Call("OnApplicationPause",false);Click("prematch-next");break;
                    case 9:Controls();Capture("07-ready");if(Root.Q<Button>("prematch-next").enabledSelf)throw new Exception("Ready must wait for explicit kickoff");Call("LaunchBack");break;
                    case 10:Frozen();if(Root.Q("prematch-presentation")!=null||Field<bool>("presentationPending")||Field<AudioSource>("anthem").isPlaying)throw new Exception("Back did not clean up presentation");Click("prematch-replay");PlayerPrefs.SetInt("reduce-motion",1);animations=App.InterfaceAnimationCount;Click("prematch-next");break;
                    case 11:Controls();if(App.InterfaceAnimationCount!=animations)throw new Exception("Reduced motion ignored");Set("introElapsed",8.98f);break;
                    case 12:Controls();if(Field<int>("introChapter")!=2)throw new Exception("Automatic chapter did not advance");if(App.InterfaceAnimationCount!=animations)throw new Exception("Lineup ignored reduced motion");Capture("08-reduced-motion");Click("prematch-kickoff");break;
                    case 13:
                        if(App.Career.match.clock<=0||Arena.Paused||Field<bool>("presentationPending")||Root.Q("prematch-presentation")!=null||Field<AudioSource>("anthem").isPlaying)throw new Exception("Kickoff or presentation cleanup failed");
                        Capture("09-live-match");PlayerPrefs.SetInt("reduce-motion",oldMotion);
                        File.WriteAllText(Path.Combine(output,"report.json"),"{\"passed\":true,\"stages\":14,\"syntheticFixture\":true,\"actualAwayManagerLineups\":true,\"careerStandings\":true,\"anthemBound\":true,\"audioLifecycle\":true,\"frozenUntilKickoff\":true,\"automaticAndManualChapters\":true,\"rebuildAndFold\":true,\"reducedMotion\":true,\"controls44px\":true,\"physicalAndroid\":false}");
                        SessionState.SetBool("PrematchPresentationSmoke",false);Debug.Log("TOUCHLINE_PREMATCH_OK");EditorApplication.Exit(0);break;
                }
            }catch(Exception e){PlayerPrefs.SetInt("reduce-motion",oldMotion);SessionState.SetBool("PrematchPresentationSmoke",false);Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}

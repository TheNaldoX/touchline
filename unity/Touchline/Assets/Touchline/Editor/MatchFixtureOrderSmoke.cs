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
    [InitializeOnLoad] public static class MatchFixtureOrderSmoke
    {
        static int stage,frames,last=-1;static RenderTexture target;static string output,selected;static Fixture fixture;static float expectedClock;
        static TouchlineApp App=>TouchlineApp.Instance;
        static VisualElement Root=>App.GetComponent<UIDocument>().rootVisualElement;
        static MatchArena Arena=>UnityEngine.Object.FindFirstObjectByType<MatchArena>();
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static void Call(string method,params object[] args)=>typeof(TouchlineApp).GetMethod(method,Private).Invoke(App,args);
        static T Field<T>(string name)=>(T)typeof(TouchlineApp).GetField(name,Private).GetValue(App);
        static MatchFixtureOrderSmoke(){EditorApplication.update+=Tick;}
        [Serializable] sealed class Failure { public int stage;public string error; }
        public static void Run()
        {
            const string prefix="-touchlineFixtureOrderOutput=";
            var choices=Environment.GetCommandLineArgs().Where(a=>a.StartsWith(prefix,StringComparison.Ordinal)).ToArray();
            if(choices.Length>1)throw new ArgumentException("Specify one programme output name.");
            string name=choices.Length==0?"match-fixture-order-v1":choices[0].Substring(prefix.Length);
            if(name.Length==0||name.Length>80||name.Any(c=>!(c>='a'&&c<='z'||c>='A'&&c<='Z'||c>='0'&&c<='9'||c=='-'||c=='_')))
                throw new ArgumentException("Programme output must be a basename of 1–80 ASCII letters, digits, hyphens or underscores.");
            output=Path.GetFullPath(Path.Combine("../../artifacts/unity",name));
            if(Directory.Exists(output)||File.Exists(output))throw new IOException("Preserving existing programme proof: "+output);
            Directory.CreateDirectory(output);SessionState.SetString("MatchFixtureOrderOutput",output);
            ProjectBuilder.Configure();SessionState.SetBool("MatchFixtureOrderSmoke",true);EditorApplication.isPlaying=true;
        }
        static void Click(string name){var button=Root.Query<Button>().ToList().FirstOrDefault(b=>b.name==name||b.text==name)??throw new Exception("Missing button: "+name);if(!button.enabledInHierarchy)throw new Exception("Disabled button: "+name);using(var e=NavigationSubmitEvent.GetPooled()){e.target=button;button.SendEvent(e);}}
        static void Resize(int width,int height){var old=target;target=new RenderTexture(width,height,24);target.Create();App.GetComponent<UIDocument>().panelSettings.targetTexture=target;if(old!=null){old.Release();UnityEngine.Object.DestroyImmediate(old);}}
        static void Capture(string name){var old=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);RenderTexture.active=old;}
        static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
        static void Frozen(){Check(Arena.Paused&&App.Career.match.clock==expectedClock,"UI changed simulation clock or pause");}
        static void Hud(string score)
        {
            Check(Root.Q<Label>("match-home-club").text==App.Database.clubs.First(c=>c.id==fixture.home).name,"Home label wrong");
            Check(Root.Q<Label>("match-away-club").text==App.Database.clubs.First(c=>c.id==fixture.away).name,"Away label wrong");
            Check(Root.Q<Label>("match-score").text.StartsWith(score,StringComparison.Ordinal),"Home-away score wrong");Frozen();
        }
        static void Goal(int side,int homeScore,int awayScore)
        {
            var m=App.Career.match;m.score[0]=homeScore;m.score[1]=awayScore;
            m.events.Add(new MatchEvent{kind="goal",side=side,player=m.actors[side*11+9].id,text=side==0?"But de l’équipe du manager":"But de l’adversaire",time=m.clock});
            Call("UpdateMatchClock",m);Call("RefreshMatchFeedback");
        }
        static void Dashboard()
        {
            var home=Root.Q("interlude-team-1");var away=Root.Q("interlude-team-0");
            Check(home!=null&&away!=null&&home.parent.IndexOf(home)<away.parent.IndexOf(away),"Dashboard did not order actual home first");
            var rows=Root.Query<VisualElement>(className:"interlude-stat").ToList();var possession=rows[0].Query<Label>(className:"interlude-value").ToList();var shots=rows[1].Query<Label>(className:"interlude-value").ToList();
            Check(possession[0].text=="30 %"&&possession[1].text=="70 %","Possession columns reversed");
            Check(shots[0].text=="2 / 1"&&shots[1].text=="7 / 5","Shot columns reversed");Frozen();
        }
        static void Tick()
        {
            if(!SessionState.GetBool("MatchFixtureOrderSmoke",false)||!EditorApplication.isPlaying||App==null||last==Time.frameCount)return;
            last=Time.frameCount;if(++frames<45)return;frames=0;
            try{
                output=SessionState.GetString("MatchFixtureOrderOutput","");if(string.IsNullOrEmpty(output)||!Directory.Exists(output))throw new IOException("Output not initialized by Run.");
                var m=App.Career.match;
                switch(stage++){
                    case 0:
                        App.Career.EnsureWorld(App.Database);Resize(1280,966);fixture=App.Career.NextFixture();
                        if(fixture.home==App.Career.club){string other=fixture.away;fixture.away=fixture.home;fixture.home=other;}
                        App.Career.life.day=fixture.day;App.Career.life.nextFixture=fixture.day;Call("StartCareerMatch",false);Call("Build");Call("LaunchBack");
                        Arena.enabled=false;Arena.Paused=true;Arena.Broadcast.SetMode(MatchViewingMode.Full);m=App.Career.match;m.clock=300;expectedClock=m.clock;m.events.Clear();m.score[0]=m.score[1]=0;
                        m.shots[0]=7;m.shots[1]=2;m.metrics[0].shotsOnTarget=5;m.metrics[1].shotsOnTarget=1;m.metrics[0].possessionSeconds=70;m.metrics[1].possessionSeconds=30;Call("Build");break;
                    case 1:Call("UpdateMatchClock",m);Hud("0 – 0");Goal(0,1,0);break;
                    case 2:Hud("0 – 1");Check(Field<Label>("matchSignalTitle").text.Contains("0 – 1"),"Manager goal banner reversed");Check(Field<Label>("matchSignalDetail").text.Contains(App.Database.clubs.First(c=>c.id==m.home).name),"Manager goal attributed to wrong club");Capture("01-away-manager-goal");Goal(1,1,3);break;
                    case 3:
                        Hud("3 – 1");Check(Field<Label>("matchSignalTitle").text.Contains("3 – 1"),"Opponent goal banner reversed");Capture("02-home-opponent-goal");
                        m.phase="throw-in";m.restart=20;m.ball=new BallState{position=new Point(0,34),previous=new Point(0,34)};
                        // SetMode intentionally opens a four-second live window and observes
                        // the synthetic goals at the current clock. Move the fixture clock
                        // only after that setup, beyond these protected live windows.
                        Arena.Broadcast.SetMode(MatchViewingMode.Highlights);m.clock=600;expectedClock=m.clock;Arena.Broadcast.Refresh();Arena.RenderFrame(.02f);Call("RefreshBroadcastVisibility");Call("RefreshBroadcastData");break;
                    case 4:Hud("3 – 1");Dashboard();Check(Arena.QuietPresentation,"Fixture did not enter quiet presentation");Capture("03-quiet-stats-lineups");Call("MatchAnalysis");break;
                    case 5:
                        Frozen();var header=Root.Q(className:"analysis-clubs").Query<Label>().ToList();Check(header[0].text==App.Database.clubs.First(c=>c.id==fixture.home).name&&header[1].text.StartsWith("3 – 1",StringComparison.Ordinal),"Analysis header reversed");
                        var stats=Root.Query<VisualElement>(className:"match-stat").ToList();var shot=stats.First(r=>r.Query<Label>().ToList().Any(l=>l.text=="Tirs / cadrés")).Query<Label>(className:"match-stat-value").ToList();Check(shot[0].text=="2 / 1"&&shot[1].text=="7 / 5","Analysis statistics reversed");Capture("04-analysis-home-away");Click("Temps forts");break;
                    case 6:
                        Frozen();var moments=Root.Query<VisualElement>(className:"match-moment").ToList();Check(moments.Count==2&&moments[0].ClassListContains("moment-home")&&moments[1].ClassListContains("moment-away"),"Goal timeline colours reversed");Capture("05-goal-timeline-colours");Call("CloseModal");selected=m.actors[9].id;Click("interlude-player-9");break;
                    case 7:
                        Frozen();Check(Root.Q("player-profile")!=null&&Root.Query<Label>().ToList().Any(l=>l.text==App.Database.Find(selected).name),"Lineup click changed simulation actor index");Call("CloseModal");Resize(1080,2520);App.Career.world.activeFixture=null;Call("Build");break;
                    case 8:Hud("3 – 1");Dashboard();Capture("06-portrait-retained-order");
                        File.WriteAllText(Path.Combine(output,"report.json"),"{\"passed\":true,\"stages\":9,\"syntheticFixture\":true,\"awayManager\":true,\"goalBothSides\":true,\"statsAndLineups\":true,\"analysis\":true,\"timelineColours\":true,\"actorClickIndexPreserved\":true,\"orderRetainedAfterActiveClear\":true,\"physicalAndroid\":false}");
                        SessionState.SetBool("MatchFixtureOrderSmoke",false);Debug.Log("TOUCHLINE_FIXTURE_ORDER_OK");EditorApplication.Exit(0);break;
                }
            }catch(Exception e){SessionState.SetBool("MatchFixtureOrderSmoke",false);if(!string.IsNullOrEmpty(output)&&Directory.Exists(output))File.WriteAllText(Path.Combine(output,"failure.json"),JsonUtility.ToJson(new Failure{stage=stage,error=e.ToString()},true));Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}


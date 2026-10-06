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
    [InitializeOnLoad] public static class MatchDelegationSmoke
    {
        static int stage,frames,last=-1,matches;static float stopped,started;static MatchState match;static string output,expected,injuredPlayer;static RenderTexture target;
        static VisualElement Root=>TouchlineApp.Instance.GetComponent<UIDocument>().rootVisualElement;
        static MatchArena Arena=>UnityEngine.Object.FindFirstObjectByType<MatchArena>();
        static MatchDelegationSmoke(){EditorApplication.update+=Tick;}
        [Serializable] sealed class Failure { public int stage;public string error; }
        public static void Run()
        {
            const string prefix="-touchlineDelegationOutput=";
            var choices=Environment.GetCommandLineArgs().Where(a=>a.StartsWith(prefix,StringComparison.Ordinal)).ToArray();
            if(choices.Length>1)throw new ArgumentException("Specify one delegation output name.");
            string name=choices.Length==0?"match-delegation-v2":choices[0].Substring(prefix.Length);
            if(name.Length==0||name.Length>80||name.Any(c=>!(c>='a'&&c<='z'||c>='A'&&c<='Z'||c>='0'&&c<='9'||c=='-'||c=='_')))
                throw new ArgumentException("Delegation output must be a basename of 1-80 ASCII letters, digits, hyphens or underscores.");
            output=Path.GetFullPath(Path.Combine("../../artifacts/unity",name));
            if(Directory.Exists(output)||File.Exists(output))throw new IOException("Preserving existing delegation proof: "+output);
            Directory.CreateDirectory(output);SessionState.SetString("MatchDelegationOutput",output);
            ProjectBuilder.Configure();SessionState.SetBool("MatchDelegationSmoke",true);EditorApplication.isPlaying=true;
        }
        static void Click(string name){var b=Root.Query<Button>().ToList().FirstOrDefault(x=>x.name==name||x.text==name);if(b==null)throw new Exception("Missing delegation control: "+name);using(var e=NavigationSubmitEvent.GetPooled()){e.target=b;b.SendEvent(e);}}
        static void Suspend(bool value)=>typeof(TouchlineApp).GetMethod("OnApplicationPause",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(TouchlineApp.Instance,new object[]{value});
        static void Resize(int w,int h){var old=target;target=new RenderTexture(w,h,24);target.Create();TouchlineApp.Instance.GetComponent<UIDocument>().panelSettings.targetTexture=target;if(old!=null){old.Release();UnityEngine.Object.DestroyImmediate(old);}}
        static void Capture(string name){var old=RenderTexture.active;RenderTexture.active=target;var t=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);t.ReadPixels(new Rect(0,0,target.width,target.height),0,0);t.Apply();File.WriteAllBytes(Path.Combine(output,name+".png"),t.EncodeToPNG());UnityEngine.Object.DestroyImmediate(t);RenderTexture.active=old;}
        static void CheckPanel(){var panel=Root.Q("match-delegation");var cancel=panel?.Query<Button>().ToList().FirstOrDefault(b=>b.text=="Revenir au match");if(panel==null||cancel==null||cancel.resolvedStyle.display==DisplayStyle.None||cancel.worldBound.height<43||cancel.worldBound.xMin<0||cancel.worldBound.xMax>Root.worldBound.xMax+1||cancel.worldBound.yMax>Root.worldBound.yMax+1)throw new Exception("Delegation/cancel panel vanished or overflowed");}
        static void Tick()
        {
            if(!SessionState.GetBool("MatchDelegationSmoke",false)||!EditorApplication.isPlaying||TouchlineApp.Instance==null||last==Time.frameCount)return;last=Time.frameCount;if(++frames<20)return;frames=0;
            try{var app=TouchlineApp.Instance;output=SessionState.GetString("MatchDelegationOutput","");if(string.IsNullOrEmpty(output)||!Directory.Exists(output))throw new IOException("Delegation output not initialized by Run.");
                switch(stage++){
                    case 0:Resize(1280,966);app.Career.life.day=app.Career.life.nextFixture;matches=app.Career.life.matches;Suspend(true);Click("Match");Click("Déléguer à l’adjoint");match=app.Career.match;stopped=match.clock;break;
                    case 1:CheckPanel();if(match.clock!=stopped)throw new Exception("Background delegation advanced");Capture("delegation-fold");Resize(1080,2520);break;
                    case 2:CheckPanel();if(match.clock!=stopped)throw new Exception("Rotation resumed suspended delegation");Capture("delegation-portrait");Suspend(false);break;
                    case 3:CheckPanel();if(match.clock<=stopped||Root.Q<ProgressBar>().value!=match.Minute)throw new Exception("Delegation made no progress");Suspend(true);stopped=match.clock;Click("Revenir au match");break;
                    case 4:if(Root.Q("match-delegation")!=null||Arena==null||!Arena.Paused||match.clock!=stopped||app.Career.life.matches!=matches)throw new Exception("Cancel must return to the same paused match without recording it");Suspend(false);Resize(1600,700);break;
                    case 5:
                        if(match.clock!=stopped)throw new Exception("Cancelled match resumed itself");Capture("cancelled-match");
                        // Controlled injury fixture at the paused resumption point. This is
                        // explicitly injected evidence, not a claimed random medical event.
                        if(match.substitutions[0]>=5||match.homeWindows.Count>=3)throw new Exception("Fixture has no substitution capacity left.");
                        var injured=match.actors.FirstOrDefault(a=>a.side==0&&a.slot>0&&!a.sentOff&&app.Database.players.Any(p=>p.team==match.home&&!match.used.Contains(p.id)&&p.unavailableDays<=0&&!p.Goalkeeper&&p.Fit(match.homeTactic.withoutBall[a.slot].role)>=.65f));
                        if(injured==null)throw new Exception("No eligible injury replacement fixture.");
                        injuredPlayer=injured.id;injured.injured=true;
                        match.events.Add(new MatchEvent{kind="injury",side=0,player=injuredPlayer,time=match.clock,text="Fixture de validation : blessure à la reprise de la délégation."});
                        var oracle=JsonUtility.FromJson<Career>(JsonUtility.ToJson(app.Career));
                        var oracleDb=JsonUtility.FromJson<Database>(JsonUtility.ToJson(app.Database));
                        var copy=oracle.match;var direct=new MatchSimulation(oracleDb,copy);int steps=0;
                        while(!copy.finished){direct.AdvanceDelegatedStep();if(++steps>54001)throw new Exception("Direct delegated oracle failed to finish.");}
                        oracle.ProcessMedicalEvents(oracleDb);oracle.RecordMatch(oracleDb);expected=JsonUtility.ToJson(copy);
                        if(!copy.events.Any(e=>e.kind=="substitution"&&e.side==0&&e.receiver==injuredPlayer))throw new Exception("Direct delegated oracle did not replace fixture injury.");
                        Click("Options du match");Click("Déléguer la fin");Click("Confirmer");started=Time.realtimeSinceStartup;break;
                    case 6:
                        if(!match.finished){CheckPanel();if(Time.realtimeSinceStartup-started>180)throw new Exception("Delegation did not finish within 180 seconds");stage--;break;}
                        if(Root.Q("match-delegation")!=null){stage--;break;}
                        if(match.clock!=5400||!app.Career.life.recordedMatch||app.Career.life.matches!=matches+1||app.Career.match!=null)throw new Exception("Delegated full match must be recorded once, then return to club");
                        if(JsonUtility.ToJson(match)!=expected)throw new Exception("Incremental delegation differs from the same delegated-step oracle");
                        if(!match.events.Any(e=>e.kind=="substitution"&&e.side==0&&e.receiver==injuredPlayer))throw new Exception("Delegated assistant did not replace the injured fixture player" );
                        app.Career.RecordMatch(app.Database);if(app.Career.life.matches!=matches+1)throw new Exception("Repeated recording duplicated the match" );Capture("delegation-complete" );
                        File.WriteAllText(Path.Combine(output,"report.json"),"{\"passed\":true,\"startFromLobby\":true,\"cancelAndResume\":true,\"suspendedClockStops\":true,\"rotationPreservesControls\":true,\"full5400Seconds\":true,\"sameStateAsDirectDelegatedSimulation\":true,\"injectedInjuryReplaced\":true,\"recordedOnce\":true,\"physicalAndroid\":false}");
                        SessionState.SetBool("MatchDelegationSmoke",false);Debug.Log("TOUCHLINE_DELEGATION_OK");EditorApplication.Exit(0);break;
                }
            }catch(Exception e){SessionState.SetBool("MatchDelegationSmoke",false);if(!string.IsNullOrEmpty(output)&&Directory.Exists(output))File.WriteAllText(Path.Combine(output,"failure.json"),JsonUtility.ToJson(new Failure{stage=stage,error=e.ToString()},true));Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}

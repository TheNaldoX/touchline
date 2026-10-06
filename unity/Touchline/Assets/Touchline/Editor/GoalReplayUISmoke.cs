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
    [InitializeOnLoad] public static class GoalReplayUISmoke
    {
        const string Key="GoalReplayUISmoke";static int stage,frames,last=-1;static string output;static RenderTexture target;static MatchArena arena;static float clock;static int home,away;static bool seekingGoal;static int renderedFrames;static long renderTicks;static string liveJson;static Transform[] liveTransforms;static Vector3[] livePositions;static Quaternion[] liveRotations;
        static TouchlineApp App=>TouchlineApp.Instance;static VisualElement Root=>App.GetComponent<UIDocument>().rootVisualElement;
        static GoalReplayUISmoke(){EditorApplication.update+=Tick;}
        static string Output(){var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-touchlineOutput");return Path.GetFullPath(i>=0&&i+1<args.Length?args[i+1]:"../../artifacts/unity/session8-goal-replay-ui");}
        public static void Run(){output=Output();if(Directory.Exists(output)&&Directory.EnumerateFileSystemEntries(output).Any())throw new Exception("Preserve prior replay UI proof");Directory.CreateDirectory(output);SessionState.SetBool(Key+".hadSize",PlayerPrefs.HasKey("interface-size"));SessionState.SetInt(Key+".oldSize",PlayerPrefs.GetInt("interface-size",1));PlayerPrefs.SetInt("interface-size",1);ProjectBuilder.Configure();SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;}
        static void Restore(){if(SessionState.GetBool(Key+".hadSize",false))PlayerPrefs.SetInt("interface-size",SessionState.GetInt(Key+".oldSize",1));else PlayerPrefs.DeleteKey("interface-size");}
        static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
        static void Call(string name,params object[] values)=>typeof(TouchlineApp).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(App,values);
        static void Resize(int w,int h){var old=target;target=new RenderTexture(w,h,24);target.Create();App.GetComponent<UIDocument>().panelSettings.targetTexture=target;if(old!=null){old.Release();UnityEngine.Object.DestroyImmediate(old);}}
        static void Capture(string name){var prior=RenderTexture.active;RenderTexture.active=target;var tex=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);tex.ReadPixels(new Rect(0,0,target.width,target.height),0,0);tex.Apply();File.WriteAllBytes(Path.Combine(output,name+".png"),tex.EncodeToPNG());UnityEngine.Object.DestroyImmediate(tex);RenderTexture.active=prior;}
        static void CheckReplay(){Call("RefreshBroadcastVisibility");var b=Root.Q<Button>("match-replay-skip");Require(arena.GoalReplayActive&&b!=null&&b.enabledInHierarchy&&b.resolvedStyle.display==DisplayStyle.Flex,"Skip button unavailable during replay");Require(b.worldBound.height>=44&&b.worldBound.xMin>=Root.worldBound.xMin-1&&b.worldBound.xMax<=Root.worldBound.xMax+1,"Skip button clipped or too small: "+b.worldBound);Require(Root.Q<Button>("match-speed-cycle").resolvedStyle.display==DisplayStyle.None&&Root.Q<Button>("match-broadcast-mode").resolvedStyle.display==DisplayStyle.None,"Live speed/view controls visible during replay");Require(Root.Query<Label>().ToList().Any(l=>l.text!=null&&l.text.Contains("REPLAY DU BUT")),"Replay status missing");}
        static void Unchanged(){Require(arena.Simulation.State.clock==clock&&arena.Simulation.State.score[0]==home&&arena.Simulation.State.score[1]==away&&JsonUtility.ToJson(arena.Simulation.State)==liveJson,"Replay UI changed authoritative match state");}
                static void SeekNaturalGoal(){
            for(int i=0;i<64&&!arena.GoalReplayActive;i++){
                var m=arena.Simulation.State;Require(!m.finished,"No natural goal available in the seeded match");if(m.halfTime)arena.Simulation.ResumeHalf();arena.Paused=false;
                long before=System.Diagnostics.Stopwatch.GetTimestamp();arena.RenderFrame(.1f);renderTicks+=System.Diagnostics.Stopwatch.GetTimestamp()-before;renderedFrames++;
            }
            if(!arena.GoalReplayActive)return;
            Require(arena.Simulation.State.events.Any(e=>e.kind=="goal"),"Replay must follow a natural simulation goal");
            var state=arena.Simulation.State;clock=state.clock;home=state.score[0];away=state.score[1];liveJson=JsonUtility.ToJson(state);
            liveTransforms=Enumerable.Range(0,22).SelectMany(i=>arena.PlayerVisual(i).GetComponentsInChildren<Transform>(true)).ToArray();livePositions=liveTransforms.Select(t=>t.localPosition).ToArray();liveRotations=liveTransforms.Select(t=>t.localRotation).ToArray();
            File.WriteAllText(Path.Combine(output,"natural-goal-state.json"),liveJson);
            for(int i=0;i<12;i++)arena.RenderFrame(1f/30);Unchanged();arena.Paused=true;seekingGoal=false;Call("RefreshBroadcastVisibility");
        }
        static void Tick(){if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||App==null||last==Time.frameCount)return;last=Time.frameCount;if(!seekingGoal&&++frames<36)return;if(!seekingGoal)frames=0;
            try{output??=Output();if(seekingGoal){SeekNaturalGoal();return;}switch(stage++){
                case 0:
                    Require((bool)typeof(TouchlineApp).GetProperty("VisualValidation",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(App),"Personal career access forbidden");App.GetComponent<UIDocument>().panelSettings=UnityEngine.Object.Instantiate(App.GetComponent<UIDocument>().panelSettings);App.Career.EnsureWorld(App.Database);
                    var opponent=App.Database.clubs.First(c=>c.playable&&c.id!=App.Career.club&&c.league==App.Database.clubs.First(o=>o.id==App.Career.club).league);
                    App.Career.match=MatchSimulation.Create(App.Database,App.Career,opponent.id,9013).State;Resize(1600,700);Call("Navigate","Match");break;
                case 1:
                    arena=(MatchArena)typeof(TouchlineApp).GetField("arena",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(App);arena.enabled=false;arena.Broadcast.SetMode(MatchViewingMode.Full);arena.Paused=false;arena.Speed=1;
                    seekingGoal=true;SeekNaturalGoal();break;
                case 2:Capture("replay-landscape");CheckReplay();Unchanged();Resize(1080,2520);break;
                case 3:Capture("replay-portrait");CheckReplay();Unchanged();var button=Root.Q<Button>("match-replay-skip");using(var e=NavigationSubmitEvent.GetPooled()){e.target=button;button.SendEvent(e);}break;
                case 4:Capture("skipped-portrait");for(int i=0;i<liveTransforms.Length;i++){Require(Vector3.Distance(liveTransforms[i].localPosition,livePositions[i])<.00001f,"Skip failed to restore live bone position");Require(Quaternion.Angle(liveTransforms[i].localRotation,liveRotations[i])<.05f,"Skip failed to restore live bone rotation");}Require(!arena.GoalReplayActive&&Root.Q<Button>("match-replay-skip").resolvedStyle.display==DisplayStyle.None,"Skip did not restore live UI");Unchanged();Require(Root.Q<Button>("match-speed-cycle").resolvedStyle.display==DisplayStyle.Flex,"Live speed not restored");Resize(1600,700);break;
                case 5:Capture("skipped-landscape");Unchanged();File.WriteAllText(Path.Combine(output,"report.json"),"{\"passed\":true,\"naturalGoalFromSimulation\":true,\"authoritativeStateUnchanged\":true,\"liveBonesRestored\":true,\"skipAccessible44px\":true,\"portraitLandscape\":true,\"clockAndScoreUnchanged\":true,\"liveControlsRestored\":true,\"physicalAndroid\":false}");File.WriteAllText(Path.Combine(output,"editor-cost.txt"),"Rendered frames while seeking natural goal: "+renderedFrames+"\nMeasured Editor RenderFrame CPU seconds: "+(renderTicks/(double)System.Diagnostics.Stopwatch.Frequency)+"\nAllocated replay payload bytes: "+arena.GoalReplayPayloadBytes+"\nThis is an Editor measurement, not a physical Android performance benchmark.");Restore();SessionState.SetBool(Key,false);Debug.Log("TOUCHLINE_GOAL_REPLAY_UI_OK");EditorApplication.Exit(0);break;
            }}catch(Exception e){Restore();SessionState.SetBool(Key,false);File.WriteAllText(Path.Combine(output??Output(),"failure.txt"),e.ToString());Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}

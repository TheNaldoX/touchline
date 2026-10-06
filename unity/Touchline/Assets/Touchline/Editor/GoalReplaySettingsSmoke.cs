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
    [InitializeOnLoad] public static class GoalReplaySettingsSmoke
    {
        const string Key="GoalReplaySettingsSmoke";static int stage,frames,last=-1;static string output;static RenderTexture target;static MatchArena arena;static float clock;static int home,away;static bool seekingGoal;static int renderedFrames;static long renderTicks;static string liveJson;static Transform[] liveTransforms;static Vector3[] livePositions;static Quaternion[] liveRotations;
        static TouchlineApp App=>TouchlineApp.Instance;static VisualElement Root=>App.GetComponent<UIDocument>().rootVisualElement;
        static GoalReplaySettingsSmoke(){EditorApplication.update+=Tick;}
        static string Output(){var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-touchlineOutput");return Path.GetFullPath(i>=0&&i+1<args.Length?args[i+1]:"../../artifacts/unity/session9-replay-settings-ui");}
        public static void Run(){output=Output();if(Directory.Exists(output)&&Directory.EnumerateFileSystemEntries(output).Any())throw new Exception("Preserve prior replay UI proof");Directory.CreateDirectory(output);SessionState.SetBool(Key+".hadSize",PlayerPrefs.HasKey("interface-size"));SessionState.SetInt(Key+".oldSize",PlayerPrefs.GetInt("interface-size",1));PlayerPrefs.SetInt("interface-size",1);SessionState.SetBool(Key+".hadReplay",PlayerPrefs.HasKey("match-goal-replays"));SessionState.SetInt(Key+".oldReplay",PlayerPrefs.GetInt("match-goal-replays",1));PlayerPrefs.SetInt("match-goal-replays",1);ProjectBuilder.Configure();SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;}
        static void Restore(){if(SessionState.GetBool(Key+".hadReplay",false))PlayerPrefs.SetInt("match-goal-replays",SessionState.GetInt(Key+".oldReplay",1));else PlayerPrefs.DeleteKey("match-goal-replays");PlayerPrefs.Save();if(SessionState.GetBool(Key+".hadSize",false))PlayerPrefs.SetInt("interface-size",SessionState.GetInt(Key+".oldSize",1));else PlayerPrefs.DeleteKey("interface-size");}
        static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
        static void Call(string name,params object[] values)=>typeof(TouchlineApp).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(App,values);
        static void Resize(int w,int h){var old=target;target=new RenderTexture(w,h,24);target.Create();App.GetComponent<UIDocument>().panelSettings.targetTexture=target;if(old!=null){old.Release();UnityEngine.Object.DestroyImmediate(old);}}
        static void Capture(string name){var prior=RenderTexture.active;RenderTexture.active=target;var tex=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);tex.ReadPixels(new Rect(0,0,target.width,target.height),0,0);tex.Apply();File.WriteAllBytes(Path.Combine(output,name+".png"),tex.EncodeToPNG());UnityEngine.Object.DestroyImmediate(tex);RenderTexture.active=prior;}
        static void CheckReplay(){Call("RefreshBroadcastVisibility");CheckControl("match-replay-speed");CheckControl("match-replay-options");var b=Root.Q<Button>("match-replay-skip");Require(arena.GoalReplayActive&&b!=null&&b.enabledInHierarchy&&b.resolvedStyle.display==DisplayStyle.Flex,"Skip button unavailable during replay");Require(b.worldBound.height>=44&&b.worldBound.xMin>=Root.worldBound.xMin-1&&b.worldBound.xMax<=Root.worldBound.xMax+1,"Skip button clipped or too small: "+b.worldBound);Require(Root.Q<Button>("match-speed-cycle").resolvedStyle.display==DisplayStyle.None&&Root.Q<Button>("match-broadcast-mode").resolvedStyle.display==DisplayStyle.None,"Live speed/view controls visible during replay");Require(Root.Query<Label>().ToList().Any(l=>l.text!=null&&l.text.Contains("REPLAY DU BUT")),"Replay status missing");}
        static void Click(string name){var button=Root.Q<Button>(name);Require(button!=null&&button.enabledInHierarchy,"Missing active button "+name);using(var e=NavigationSubmitEvent.GetPooled()){e.target=button;button.SendEvent(e);}}
        static void CheckControl(string name){var button=Root.Q<Button>(name);Require(button!=null&&button.resolvedStyle.display==DisplayStyle.Flex&&button.worldBound.height>=44&&button.worldBound.width>=44&&button.worldBound.xMin>=Root.worldBound.xMin-1&&button.worldBound.xMax<=Root.worldBound.xMax+1,"Control clipped or too small: "+name);}
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
                case 2:
                    Capture("replay-landscape");CheckReplay();Unchanged();arena.Speed=3;arena.Paused=false;
                    Click("match-replay-speed");Require(arena.GoalReplaySpeed==.5f&&arena.Speed==3,"Replay speed affected live speed");float start=arena.GoalReplayProgress;arena.RenderFrame(.1f);float slow=arena.GoalReplayProgress-start;
                    Click("match-replay-speed");start=arena.GoalReplayProgress;arena.RenderFrame(.1f);float normal=arena.GoalReplayProgress-start;Require(slow>0&&Mathf.Abs(normal-2*slow)<.002f,"Replay speeds do not advance at 0.5 / 1");
                    arena.Paused=true;start=arena.GoalReplayProgress;arena.RenderFrame(.1f);Require(arena.GoalReplayProgress==start,"Paused replay advanced");Unchanged();Resize(1080,2520);break;
                case 3:Capture("replay-portrait");CheckReplay();Unchanged();var button=Root.Q<Button>("match-replay-skip");using(var e=NavigationSubmitEvent.GetPooled()){e.target=button;button.SendEvent(e);}break;
                case 4:Capture("skipped-portrait");for(int i=0;i<liveTransforms.Length;i++){Require(Vector3.Distance(liveTransforms[i].localPosition,livePositions[i])<.00001f,"Skip failed to restore live bone position");Require(Quaternion.Angle(liveTransforms[i].localRotation,liveRotations[i])<.05f,"Skip failed to restore live bone rotation");}Require(!arena.GoalReplayActive&&Root.Q<Button>("match-replay-skip").resolvedStyle.display==DisplayStyle.None,"Skip did not restore live UI");Unchanged();Require(Root.Q<Button>("match-speed-cycle").resolvedStyle.display==DisplayStyle.Flex,"Live speed not restored");Resize(1600,700);break;
                case 5:
                    Capture("skipped-landscape");Unchanged();Require(arena.Paused&&arena.Speed==3,"Skip changed live pause or speed");
                    var opponent2=App.Database.clubs.First(c=>c.playable&&c.id!=App.Career.club&&c.league==App.Database.clubs.First(o=>o.id==App.Career.club).league);
                    App.Career.match=MatchSimulation.Create(App.Database,App.Career,opponent2.id,9013).State;Call("CreateArena",new MatchSimulation(App.Database,App.Career.match));Call("Navigate","Match");break;
                case 6:
                    arena=(MatchArena)typeof(TouchlineApp).GetField("arena",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(App);arena.enabled=false;arena.Broadcast.SetMode(MatchViewingMode.Full);arena.Paused=false;arena.Speed=1;seekingGoal=true;SeekNaturalGoal();break;
                case 7:CheckReplay();arena.Paused=false;arena.Speed=3;Click("match-replay-options");Require(!arena.Paused,"Replay options altered live pause preference");break;
                case 8:
                    CheckControl("match-auto-goal-replays");Capture("options-landscape");Resize(1080,2520);break;
                case 9:
                    CheckControl("match-auto-goal-replays");Capture("options-portrait");Click("match-auto-goal-replays");
                    Require(!arena.GoalReplayActive&&!arena.AutomaticGoalReplays&&PlayerPrefs.GetInt("match-goal-replays",1)==0,"Disabling replay did not stop and persist");Require(!arena.Paused&&arena.Speed==3,"Disabling replay altered live pause or speed");Unchanged();
                    for(int i=0;i<liveTransforms.Length;i++){Require(Vector3.Distance(liveTransforms[i].localPosition,livePositions[i])<.00001f,"Disable failed to restore live position");Require(Quaternion.Angle(liveTransforms[i].localRotation,liveRotations[i])<.05f,"Disable failed to restore live rotation");}
                    Click("match-auto-goal-replays");Require(arena.AutomaticGoalReplays&&PlayerPrefs.GetInt("match-goal-replays",0)==1,"Enabling replay did not persist");Require(!arena.GoalReplayActive,"Enabling replay restarted stale goal");Call("CloseModal");Call("RefreshBroadcastVisibility");break;
                case 10:Capture("disabled-return-portrait");Unchanged();Require(Root.Q<Button>("match-replay-speed").resolvedStyle.display==DisplayStyle.None&&Root.Q<Button>("match-speed-cycle").resolvedStyle.display==DisplayStyle.Flex,"Replay controls leaked into live play");File.WriteAllText(Path.Combine(output,"report.json"),"{\"passed\":true,\"automaticReplayToggleUI\":true,\"preferencePersisted\":true,\"disableRestoresExactPose\":true,\"livePauseAndSpeedPreserved\":true,\"replaySpeedHalfAndNormal\":true,\"naturalGoalFromSimulation\":true,\"authoritativeStateUnchanged\":true,\"liveBonesRestored\":true,\"skipAccessible44px\":true,\"portraitLandscape\":true,\"clockAndScoreUnchanged\":true,\"liveControlsRestored\":true,\"physicalAndroid\":false}");File.WriteAllText(Path.Combine(output,"editor-cost.txt"),"Rendered frames while seeking natural goal: "+renderedFrames+"\nMeasured Editor RenderFrame CPU seconds: "+(renderTicks/(double)System.Diagnostics.Stopwatch.Frequency)+"\nAllocated replay payload bytes: "+arena.GoalReplayPayloadBytes+"\nThis is an Editor measurement, not a physical Android performance benchmark.");Restore();SessionState.SetBool(Key,false);Debug.Log("TOUCHLINE_GOAL_REPLAY_SETTINGS_UI_OK");EditorApplication.Exit(0);break;
            }}catch(Exception e){Restore();SessionState.SetBool(Key,false);File.WriteAllText(Path.Combine(output??Output(),"failure.txt"),e.ToString());Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}

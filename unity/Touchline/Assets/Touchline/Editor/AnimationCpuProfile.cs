using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Touchline.Core;
using Debug=UnityEngine.Debug;

namespace Touchline.Editor
{
    // CPU pose/simulation profiling only: no Camera.Render, GPU timing, device
    // or thermal claim. Every run uses saved natural states, never user saves.
    public static class AnimationCpuProfile
    {
        [Serializable] class Clip { public string id,statePath; public float durationSeconds; }
        [Serializable] class Index { public Clip[] clips; }
        [Serializable] class Result { public string clip; public int speed,fps,frames,poseEvaluations; public float p50Ms,p95Ms,p99Ms,maxMs,totalMs,clock; public bool stateEquivalent; }
        [Serializable] class Report { public bool passed,physicalAndroid=false,gpuMeasured=false; public string limitation="Windows Unity Editor CPU only, without camera/GPU rendering. End-state comparison normalizes only negligible accumulator residue on a cloned state; authoritative states are untouched."; public string processor; public Result[] results; }
        static string Arg(string key,string fallback)=>Environment.GetCommandLineArgs().FirstOrDefault(a=>a.StartsWith(key+"="))?.Substring(key.Length+1)??fallback;
        static string Normalized(MatchState state)
        {
            if(Math.Abs(state.remainder)>1e-5)throw new Exception("Unexpected partial step at clip end");
            var clone=JsonUtility.FromJson<MatchState>(JsonUtility.ToJson(state));clone.remainder=0;return JsonUtility.ToJson(clone);
        }
        public static void Run()
        {
            try{
                string name=Arg("-touchlineCpuOutput","animation-cpu-profile-v1"),pack=Arg("-touchlineNaturalPack","natural-match-sequences-20261005-v4");
                if(!System.Text.RegularExpressions.Regex.IsMatch(name,"^[a-zA-Z0-9-]+$")||!System.Text.RegularExpressions.Regex.IsMatch(pack,"^[a-zA-Z0-9-]+$"))throw new Exception("Invalid output or pack name");
                string input=Path.GetFullPath("../../artifacts/research/"+pack),output=Path.GetFullPath("../../artifacts/unity/"+name);if(Directory.Exists(output))throw new Exception("Preserve previous proof");Directory.CreateDirectory(output);
                var index=JsonUtility.FromJson<Index>(File.ReadAllText(Path.Combine(input,"index.json")));var db=JsonUtility.FromJson<Database>(File.ReadAllText(Path.Combine(input,"database.json")));var results=new List<Result>();
                foreach(var clip in index.clips){
                    string saved=File.ReadAllText(Path.Combine(input,clip.statePath));var expected=new MatchSimulation(db,JsonUtility.FromJson<MatchState>(saved));expected.Advance(clip.durationSeconds);string end=Normalized(expected.State);
                    foreach(int fps in new[]{30,60,120})foreach(int speed in new[]{1,2,10}){
                        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                        var simulation=new MatchSimulation(db,JsonUtility.FromJson<MatchState>(saved));var root=new GameObject("CPU natural replay");var arena=root.AddComponent<MatchArena>();arena.Initialize(db,simulation);arena.enabled=false;arena.MatchCamera.enabled=false;arena.MatchCamera.aspect=1.5f;arena.Speed=speed;arena.Broadcast.SetMode(MatchViewingMode.Full);
                        // Warm resources and the initial pose outside timed frames.
                        arena.Paused=true;arena.RenderFrame(1f/fps);arena.Paused=false;
                        int frames=Mathf.RoundToInt(clip.durationSeconds*fps/speed);var times=new double[frames];int initialPoses=arena.PoseEvaluations;long total=0;
                        for(int i=0;i<frames;i++){
                            long start=Stopwatch.GetTimestamp();arena.RenderFrame(1f/fps);long elapsed=Stopwatch.GetTimestamp()-start;total+=elapsed;times[i]=elapsed*1000.0/Stopwatch.Frequency;
                        }
                        Array.Sort(times);bool equivalent=end==Normalized(simulation.State);
                        results.Add(new Result{clip=clip.id,speed=speed,fps=fps,frames=frames,poseEvaluations=arena.PoseEvaluations-initialPoses,p50Ms=(float)times[(frames-1)/2],p95Ms=(float)times[(int)((frames-1)*.95)],p99Ms=(float)times[(int)((frames-1)*.99)],maxMs=(float)times[frames-1],totalMs=(float)(total*1000.0/Stopwatch.Frequency),clock=simulation.State.clock,stateEquivalent=equivalent});
                        UnityEngine.Object.DestroyImmediate(root);
                        File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(new Report{passed=false,processor=SystemInfo.processorType,results=results.ToArray()},true));
                        if(!equivalent)throw new Exception("Rendered replay changed authoritative state: "+clip.id+" x"+speed+" at "+fps);
                    }
                }
                File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(new Report{passed=true,processor=SystemInfo.processorType,results=results.ToArray()},true));Debug.Log("TOUCHLINE_ANIMATION_CPU_PROFILE_OK");EditorApplication.Exit(0);
            }catch(Exception error){Debug.LogException(error);EditorApplication.Exit(1);}
        }
    }
}

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.Media;
using UnityEngine;
using UnityEngine.UIElements;
using Touchline.Core;
namespace Touchline.Editor
{
    // Playback of native .1s snapshots through the unpaused production arena.
    // No simulation tick is permitted: only the recorded interpolation interval is rendered.
    [InitializeOnLoad] public static class SlidingContactReplay
    {
        const string Key="SlidingContactReplay",Player="279155";
        [Serializable] class Sample {public int fps,frame,stateIndex;public float time,clock,alpha,remaining,contactDistance,leftKneeHeight,rightKneeHeight,leftSoleLow,rightSoleLow,requestedHipDistance,boneReach;public string action;public Vector3 root,leftBoot,rightBoot,leftKnee,rightKnee,leftHip,rightHip,requestedAnkle,impact,rig;}
        [Serializable] class Result {public int fps,frames;public bool immutableSnapshots,contactGatePassed;public float contactDistance,contactTimeError,minKneeHeight,minSoleHeight,maxRootStep,maxRigStep;public string source;}
        [Serializable] class Report {public bool passed,qualityGatePassed,physicalAndroid=false,performanceBenchmark=false;public string method;public Result[] results;}
        [Serializable] class Samples {public Sample[] samples;}
        static string output,input;static string[] json;static MatchState[] snapshots;static Database db;static MatchArena arena;static Camera close;static RenderTexture target;static Texture2D pixels;static MediaEncoder wideEncoder,closeEncoder;static int phase,frame,last=-1;static readonly int[] rates={30,60,120};static readonly List<Result> results=new List<Result>();static readonly List<Sample> samples=new List<Sample>();static Result result;static Vector3 impact,lastRoot,lastRig;static float contactAt=463.1f;static bool hasPrevious;
        static SlidingContactReplay(){EditorApplication.update+=Tick;}
        static string Arg(string key,string fallback){var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,key);return Path.GetFullPath(i>=0&&i+1<args.Length?args[i+1]:fallback);}
        public static void Run(){var path=Arg("-touchlineOutput","../../artifacts/unity/session8-slide-replay");if(Directory.Exists(path)&&Directory.EnumerateFileSystemEntries(path).Any())throw new Exception("Preserve prior replay proof");ProjectBuilder.Configure();SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;}
        static void Require(bool value,string message){if(!value)throw new Exception(message);}
        static void Initialize(){
            var app=TouchlineApp.Instance;Require((bool)typeof(TouchlineApp).GetProperty("VisualValidation",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(app),"Personal career access forbidden");
            output=Arg("-touchlineOutput","../../artifacts/unity/session8-slide-replay");input=Arg("-touchlineReplayInput","../../artifacts/unity/session7-slide-native-states");Directory.CreateDirectory(output);
            var files=Directory.GetFiles(input,"state-*.json").OrderBy(x=>x,StringComparer.Ordinal).ToArray();Require(files.Length>20,"Native snapshot interval too short");json=files.Select(File.ReadAllText).ToArray();snapshots=json.Select(JsonUtility.FromJson<MatchState>).ToArray();
            for(int i=1;i<snapshots.Length;i++)Require(Mathf.Abs(snapshots[i].clock-snapshots[i-1].clock-.1f)<.001f,"Non-contiguous snapshot clocks");
            Require(snapshots[0].clock<462&&snapshots.Last().clock>464,"Replay must include run, entry, impact and recovery");
            db=File.Exists(Path.Combine(input,"database.json"))?JsonUtility.FromJson<Database>(File.ReadAllText(Path.Combine(input,"database.json"))):app.Database;
            var contact=snapshots.OrderBy(m=>Mathf.Abs(m.clock-contactAt)).First().actors.First(a=>a.id==Player);Require(contact.action=="slide"&&contact.actionKind==MatchSimulation.SlidingDuel,"Reference impact is not the intended natural slide");impact=new Vector3(contact.actionTarget.x,contact.actionHeight,contact.actionTarget.z);
            app.GetComponent<UIDocument>().rootVisualElement.style.display=DisplayStyle.None;BeginRate();
        }
        static void BeginRate(){
            int fps=rates[phase];frame=0;hasPrevious=false;samples.Clear();result=new Result{fps=fps,immutableSnapshots=true,contactDistance=float.MaxValue,contactTimeError=float.MaxValue,minKneeHeight=float.MaxValue,minSoleHeight=float.MaxValue,source=input};
            var state=JsonUtility.FromJson<MatchState>(json[0]);arena=new GameObject("Continuous recorded slide replay "+fps).AddComponent<MatchArena>();arena.Initialize(db,new MatchSimulation(db,state));arena.enabled=false;arena.Broadcast.SetMode(MatchViewingMode.Full);arena.Speed=1;arena.Paused=false;
            target=new RenderTexture(1280,720,24);target.Create();arena.MatchCamera.targetTexture=target;arena.MatchCamera.aspect=1280f/720;
            close=new GameObject("Diagnostic close camera").AddComponent<Camera>();close.enabled=false;close.fieldOfView=40;close.nearClipPlane=.05f;close.farClipPlane=120;close.targetTexture=target;close.aspect=1280f/720;close.clearFlags=CameraClearFlags.Skybox;close.backgroundColor=arena.MatchCamera.backgroundColor;
            pixels=new Texture2D(1280,720,TextureFormat.RGBA32,false);
            var attrs=new VideoTrackAttributes{frameRate=new MediaRational(fps),width=1280,height=720,includeAlpha=false,bitRateMode=VideoBitrateMode.High};wideEncoder=new MediaEncoder(Path.Combine(output,"broadcast-"+fps+"fps.mp4"),attrs,Array.Empty<AudioTrackAttributes>());closeEncoder=new MediaEncoder(Path.Combine(output,"close-"+fps+"fps.mp4"),attrs,Array.Empty<AudioTrackAttributes>());
        }
        static Transform Bone(PlayerView view,string name)=>view.GetComponentsInChildren<Transform>().First(t=>t.name==name);
        static float SoleLow(Transform foot,float scale){var r=foot.rotation;var center=r*new Vector3(0,.02f,.075f);float extent=Mathf.Abs((r*Vector3.right).y)*.057f+Mathf.Abs((r*Vector3.up).y)*.085f+Mathf.Abs((r*Vector3.forward).y)*.145f;return foot.position.y+(center.y-extent)*scale;}
        static void Capture(Camera camera,MediaEncoder encoder,string still){var before=RenderTexture.active;camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();RenderTexture.active=before;Require(encoder.AddFrame(pixels),"Frame encoding failed");if(still!=null)File.WriteAllBytes(Path.Combine(output,still+".png"),pixels.EncodeToPNG());}
        static bool RenderNext(){
            int fps=rates[phase];double elapsed=frame/(double)fps;int index=(int)Math.Floor(elapsed/.1+1e-7);if(index>=snapshots.Length)return false;float alpha=(float)(elapsed/.1-index);alpha=Mathf.Clamp(alpha,0,.999999f);float dt=1f/fps;
            var m=arena.Simulation.State;JsonUtility.FromJsonOverwrite(json[index],m);m.remainder=0;string expected=JsonUtility.ToJson(m);m.remainder=alpha*.1-dt;
            // Broadcast consumes exactly dt; the accumulator remains below .1,
            // so the production animation path receives dt without a Core tick.
            arena.Paused=false;arena.RenderFrame(dt);m.remainder=0;Require(expected==JsonUtility.ToJson(m),"Replay advanced or mutated a recorded Core state");
            int actorIndex=Array.FindIndex(m.actors,a=>a.id==Player);var actor=m.actors[actorIndex];var view=arena.PlayerVisual(actorIndex);var lk=Bone(view,"lowerleg01.L");var rk=Bone(view,"lowerleg01.R");var lf=Bone(view,"foot.L");var rf=Bone(view,"foot.R");var rig=Bone(view,"Rig");var lh=Bone(view,"upperleg01.L");var rh=Bone(view,"upperleg01.R");
            var sample=new Sample{fps=fps,frame=frame,stateIndex=index,time=snapshots[0].clock-.1f+(float)elapsed,clock=m.clock,alpha=alpha,remaining=actor.actionTime+(1-alpha)*.1f,action=actor.action,root=view.transform.position,leftBoot=view.BootContactPosition(true),rightBoot=view.BootContactPosition(false),leftKnee=lk.position,rightKnee=rk.position,leftKneeHeight=lk.position.y,rightKneeHeight=rk.position.y,leftSoleLow=SoleLow(lf,view.transform.localScale.y),rightSoleLow=SoleLow(rf,view.transform.localScale.y),rig=rig.position,impact=impact};
            sample.leftHip=lh.position;sample.rightHip=rh.position;
            var actorPoint=new Vector3(actor.actionTarget.x,actor.actionHeight,actor.actionTarget.z);var ankle=actorPoint-view.transform.forward*(.22f*view.transform.localScale.y+.11f);ankle.y=Mathf.Max(.08f,ankle.y);var local=view.transform.InverseTransformPoint(ankle);local.x=Mathf.Clamp(local.x,-.55f,.55f);local.z=Mathf.Clamp(local.z,-(.22f+.11f/view.transform.localScale.y),.76f);local.y=Mathf.Clamp(local.y,.08f,.22f);sample.requestedAnkle=view.transform.TransformPoint(local);var hip=actor.diveSide>=0?lh:rh;var knee=actor.diveSide>=0?lk:rk;var foot=actor.diveSide>=0?lf:rf;sample.requestedHipDistance=Vector3.Distance(hip.position,sample.requestedAnkle);sample.boneReach=Vector3.Distance(hip.position,knee.position)+Vector3.Distance(knee.position,foot.position)-.015f;
            sample.contactDistance=Mathf.Min(Vector3.Distance(sample.leftBoot,impact),Vector3.Distance(sample.rightBoot,impact));samples.Add(sample);
            if(Mathf.Abs(sample.time-contactAt)<result.contactTimeError){result.contactTimeError=Mathf.Abs(sample.time-contactAt);result.contactDistance=sample.contactDistance;}
            result.minKneeHeight=Mathf.Min(result.minKneeHeight,Mathf.Min(lk.position.y,rk.position.y));result.minSoleHeight=Mathf.Min(result.minSoleHeight,Mathf.Min(sample.leftSoleLow,sample.rightSoleLow));if(hasPrevious){result.maxRootStep=Mathf.Max(result.maxRootStep,Vector3.Distance(lastRoot,sample.root));result.maxRigStep=Mathf.Max(result.maxRigStep,Vector3.Distance(lastRig,sample.rig));}lastRoot=sample.root;lastRig=sample.rig;hasPrevious=true;
            close.transform.position=view.transform.position+new Vector3(-2.7f,1.75f,-3.5f);close.transform.LookAt(view.transform.position+Vector3.up*.52f);
            bool still=sample.time>=462.7f&&sample.time<=463.9f&&frame%Math.Max(1,fps/10)==0;string tag=fps+"fps-"+frame.ToString("D4");Capture(arena.MatchCamera,wideEncoder,still?"broadcast-"+tag:null);Capture(close,closeEncoder,still?"close-"+tag:null);frame++;result.frames=frame;return true;
        }
        static void FinishRate(){wideEncoder?.Dispose();closeEncoder?.Dispose();wideEncoder=closeEncoder=null;result.contactGatePassed=result.contactDistance<.18f&&result.minKneeHeight>=0&&result.minSoleHeight>=-.002f;results.Add(result);File.WriteAllText(Path.Combine(output,"samples-"+rates[phase]+"fps.json"),JsonUtility.ToJson(new Samples{samples=samples.ToArray()},true));File.WriteAllText(Path.Combine(output,"result-"+rates[phase]+"fps.json"),JsonUtility.ToJson(result,true));UnityEngine.Object.DestroyImmediate(close.gameObject);UnityEngine.Object.DestroyImmediate(arena.gameObject);arena=null;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(pixels);}
        static void Tick(){if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||TouchlineApp.Instance==null||last==Time.frameCount)return;last=Time.frameCount;
            try{if(arena==null&&phase==0)Initialize();for(int batch=0;batch<4;batch++){if(RenderNext())continue;FinishRate();phase++;if(phase<rates.Length){BeginRate();continue;}File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(new Report{passed=true,qualityGatePassed=results.All(r=>r.contactGatePassed),method="Recorded native states through unpaused production MatchArena at x1, no Core tick, native fixed camera plus diagnostic close camera",results=results.ToArray()},true));SessionState.SetBool(Key,false);Debug.Log("TOUCHLINE_SLIDE_REPLAY_OK");EditorApplication.Exit(0);return;}}
            catch(Exception e){wideEncoder?.Dispose();closeEncoder?.Dispose();SessionState.SetBool(Key,false);if(output!=null)File.WriteAllText(Path.Combine(output,"failure.txt"),e.ToString());Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}

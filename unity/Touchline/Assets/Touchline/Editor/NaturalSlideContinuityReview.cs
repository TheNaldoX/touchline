using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Touchline.Core;
namespace Touchline.Editor {
 public static class NaturalSlideContinuityReview {
  [Serializable] class Clip {public string id,statePath,category,targetPlayer,eventKind;public int targetSequence,eventIndex;public float startClock,eventClock,durationSeconds;}
  [Serializable] class Index {public Clip[] clips;}
  [Serializable] class Frame {public string clip,action,phase,flightKind,receiver;public int frame;public float clock,height,previewWeight,contactDistance,actionTime,hipSpeed,kneeSpeed,rightKneeSpeed,targetStep,hipLocalSpeed,kneeLocalSpeed,rightKneeLocalSpeed,rootStep,rootTurn;public bool receivingLeft;public Vector3 ball,actor,leftFoot,rightFoot,hip,knee,rightKnee,committedTarget,hipLocal,kneeLocal,rightKneeLocal;}
  [Serializable] class Result {public string id;public bool eventObserved;public float startClock,endClock,firstObservedClock;public int frames;}
  [Serializable] class Report {public bool passed,physicalAndroid=false,synthetic=false,sparseCaptures=true;public int fps=30;public string limitation="Actual saved Core sequences, production MatchArena/PlayerView at x1. Additional tracking review camera; broadcast snapshots are separate. Offline native renders, not Android performance or real-world match footage. No generated frames.";public Result[] clips;public Frame[] samples;}
  static void Capture(Camera camera,RenderTexture target,GameObject root,string path){
   var skins=new List<SkinnedMeshRenderer>();var proxies=new List<GameObject>();var meshes=new List<Mesh>();var previous=RenderTexture.active;Texture2D image=null;
   try {foreach(var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>()){if(!skin.enabled||!skin.gameObject.activeInHierarchy)continue;var mesh=new Mesh();skin.BakeMesh(mesh);meshes.Add(mesh);var proxy=new GameObject("Native current skin");proxy.layer=skin.gameObject.layer;proxy.transform.SetParent(skin.transform,false);proxy.AddComponent<MeshFilter>().sharedMesh=mesh;proxy.AddComponent<MeshRenderer>().sharedMaterials=skin.sharedMaterials;proxies.Add(proxy);skins.Add(skin);skin.enabled=false;}
    camera.Render();RenderTexture.active=target;image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());
   } finally {foreach(var skin in skins)skin.enabled=true;foreach(var proxy in proxies)UnityEngine.Object.DestroyImmediate(proxy);foreach(var mesh in meshes)UnityEngine.Object.DestroyImmediate(mesh);if(image!=null)UnityEngine.Object.DestroyImmediate(image);RenderTexture.active=previous;}
  }
  static string Arg(string key,string fallback)=>Environment.GetCommandLineArgs().FirstOrDefault(a=>a.StartsWith(key+"="))?.Substring(key.Length+1)??fallback;
  public static void Run(){try{
   int fps=int.Parse(Arg("-touchlineNaturalFps","30"));if(fps!=30&&fps!=60&&fps!=120)throw new Exception("Invalid rate");string only=Arg("-touchlineNaturalOnly","03-sliding-duel");
   var name=Arg("-touchlineNaturalOutput","natural-body-reception-v1");var pack=Arg("-touchlineNaturalPack","natural-match-sequences-20261005-v3");
   if(!System.Text.RegularExpressions.Regex.IsMatch(name,"^[a-zA-Z0-9-]+$")||!System.Text.RegularExpressions.Regex.IsMatch(pack,"^[a-zA-Z0-9-]+$"))throw new Exception("Invalid proof names");
   var input=Path.GetFullPath("../../artifacts/research/"+pack);var output=Path.GetFullPath("../../artifacts/unity/"+name);if(Directory.Exists(output))throw new Exception("Preserve previous proofs");Directory.CreateDirectory(output);
   var index=JsonUtility.FromJson<Index>(File.ReadAllText(Path.Combine(input,"index.json")));var db=JsonUtility.FromJson<Database>(File.ReadAllText(Path.Combine(input,"database.json")));
   if(index?.clips==null||index.clips.Length==0||db==null)throw new Exception("Invalid or empty replay pack");
   var results=new List<Result>();var samples=new List<Frame>();
   var flag=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
   var weightField=typeof(PlayerView).GetField("receptionPreparationWeight",flag);var sideField=typeof(PlayerView).GetField("receivingLeft",flag);
   foreach(var clip in index.clips){
    if(clip.id!=only)continue;
    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
    if(!System.Text.RegularExpressions.Regex.IsMatch(clip.id,"^[a-zA-Z0-9-]+$")||clip.durationSeconds<=0||clip.durationSeconds>30)throw new Exception("Invalid clip");
    var state=JsonUtility.FromJson<MatchState>(File.ReadAllText(Path.Combine(input,clip.statePath)));var sim=new MatchSimulation(db,state);var root=new GameObject("Native natural match");var arena=root.AddComponent<MatchArena>();arena.Initialize(db,sim);arena.Speed=1;arena.Paused=false;arena.Broadcast.SetMode(MatchViewingMode.Full);
    var camera=new GameObject("Animation inspection camera").AddComponent<Camera>();camera.enabled=false;camera.fieldOfView=43;camera.nearClipPlane=.15f;camera.farClipPlane=270;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=RenderSettings.fogColor;
    var target=new RenderTexture(960,640,24){antiAliasing=2};target.Create();camera.targetTexture=target;camera.aspect=1.5f;arena.MatchCamera.targetTexture=target;arena.MatchCamera.aspect=1.5f;
    string dir=Path.Combine(output,clip.id);Directory.CreateDirectory(dir);int count=Mathf.RoundToInt(clip.durationSeconds*fps);bool observed=false;float firstObserved=-1;Vector3 focus=Vector3.zero,velocity=Vector3.zero;
    float slideStart=-1;
    int ai=Array.FindIndex(state.actors,a=>a.id==clip.targetPlayer);if(ai<0)throw new Exception("Target not found: "+clip.id);
    var joints=arena.PlayerVisual(ai).GetComponentsInChildren<Transform>();var hip=joints.Single(t=>t.name=="upperleg01.L");var knee=joints.Single(t=>t.name=="lowerleg01.L");var rightKnee=joints.Single(t=>t.name=="lowerleg01.R");Vector3 previousHip=Vector3.zero,previousKnee=Vector3.zero,previousRightKnee=Vector3.zero,previousTarget=Vector3.zero,previousHipLocal=Vector3.zero,previousKneeLocal=Vector3.zero,previousRightKneeLocal=Vector3.zero,previousRoot=Vector3.zero;Quaternion previousRotation=Quaternion.identity;
    for(int i=0;i<count;i++){
     arena.RenderFrame(1f/fps);var actor=state.actors[ai];if(slideStart<0&&actor.action=="slide")slideStart=state.clock;var visual=arena.PlayerVisual(ai);var p=visual.transform.position;
     Vector3 desired=Vector3.Lerp(p,arena.BallDisplayPosition,.3f);desired.y=1;
     if(i==0)focus=desired;else focus=Vector3.SmoothDamp(focus,desired,ref velocity,.22f,50,1f/fps);
     camera.transform.position=focus+new Vector3(4.5f,3.1f,4.5f);camera.transform.LookAt(focus);if(i==0||Math.Abs(state.clock-clip.eventClock)<.051f||slideStart>=0&&state.clock>=slideStart+1.6f&&state.clock<=slideStart+2.2f&&i%Mathf.Max(1,fps/10)==0||actor.action=="slide"&&(Mathf.Abs(actor.actionTime-(MatchSimulation.SlidingDuelDuration-MatchSimulation.SlidingDuelContact))<1.1f/fps||Mathf.Abs(actor.actionTime-.1f)<1.1f/fps))Capture(camera,target,root,Path.Combine(dir,"frame-"+i.ToString("D4")+".png"));
     if(i==0||Math.Abs(state.clock-clip.eventClock)<.051f)Capture(arena.MatchCamera,target,root,Path.Combine(dir,"broadcast-"+i.ToString("D4")+".png"));
     bool action=clip.eventIndex<0&&actor.actionSequence==clip.targetSequence&&actor.actionKind==clip.eventKind&&Math.Abs(state.clock-clip.eventClock)<.151f;
     bool ev=clip.eventIndex>=0&&state.events.Count>clip.eventIndex&&state.events[clip.eventIndex].kind==clip.eventKind&&state.events[clip.eventIndex].player==clip.targetPlayer&&Math.Abs(state.events[clip.eventIndex].time-clip.eventClock)<.051f;
     if(!observed&&state.clock>=clip.eventClock-.05f&&(action||ev)){observed=true;firstObserved=state.clock;}
     float preview=weightField==null?-1:(float)weightField.GetValue(visual);
     bool receiveLeft=(bool)sideField.GetValue(visual);
     float contactDistance=MatchSimulation.IsBodyControl(actor)?Vector3.Distance(actor.actionKind==MatchSimulation.ChestControl?visual.ChestContactPosition:visual.ThighContactPosition(receiveLeft),arena.BallDisplayPosition):-1;
     var targetNow=new Vector3(actor.actionTarget.x,actor.actionHeight,actor.actionTarget.z);var hipLocal=visual.transform.InverseTransformPoint(hip.position);var kneeLocal=visual.transform.InverseTransformPoint(knee.position);var rightKneeLocal=visual.transform.InverseTransformPoint(rightKnee.position);float metricScale=visual.transform.localScale.y;
     samples.Add(new Frame{hipLocal=hipLocal,kneeLocal=kneeLocal,rightKneeLocal=rightKneeLocal,hipLocalSpeed=i>0?Vector3.Distance(hipLocal,previousHipLocal)*fps*metricScale:0,kneeLocalSpeed=i>0?Vector3.Distance(kneeLocal,previousKneeLocal)*fps*metricScale:0,rightKneeLocalSpeed=i>0?Vector3.Distance(rightKneeLocal,previousRightKneeLocal)*fps*metricScale:0,rootStep=i>0?Vector3.Distance(p,previousRoot):0,rootTurn=i>0?Quaternion.Angle(previousRotation,visual.transform.rotation):0,actionTime=actor.actionTime,hip=hip.position,knee=knee.position,rightKnee=rightKnee.position,rightKneeSpeed=i>0?Vector3.Distance(rightKnee.position,previousRightKnee)*fps:0,committedTarget=targetNow,hipSpeed=i>0?Vector3.Distance(hip.position,previousHip)*fps:0,kneeSpeed=i>0?Vector3.Distance(knee.position,previousKnee)*fps:0,targetStep=i>0?Vector3.Distance(targetNow,previousTarget):0,previewWeight=preview,contactDistance=contactDistance,receivingLeft=receiveLeft,leftFoot=visual.FootPosition(true),rightFoot=visual.FootPosition(false),flightKind=state.ball.kind,receiver=state.ball.to,clip=clip.id,frame=i,clock=state.clock,action=actor.action+"/"+actor.actionKind,phase=state.phase,height=state.ball.height,ball=arena.BallDisplayPosition,actor=p});
     previousHip=hip.position;previousKnee=knee.position;previousRightKnee=rightKnee.position;previousTarget=targetNow;previousHipLocal=hipLocal;previousKneeLocal=kneeLocal;previousRightKneeLocal=rightKneeLocal;previousRoot=p;previousRotation=visual.transform.rotation;
    }
    if(Math.Abs(state.clock-clip.startClock-clip.durationSeconds)>.151f)throw new Exception("Replay pacing changed or paused: "+clip.id);
    results.Add(new Result{id=clip.id,eventObserved=observed,startClock=clip.startClock,endClock=state.clock,firstObservedClock=firstObserved,frames=count});
    camera.targetTexture=null;arena.MatchCamera.targetTexture=null;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(camera.gameObject);UnityEngine.Object.DestroyImmediate(root);
    File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(new Report{fps=fps,passed=false,clips=results.ToArray(),samples=samples.ToArray()},true));
    if(!observed)throw new Exception("Natural target event not reproduced: "+clip.id);
   }
   if(results.Count!=1)throw new Exception("Requested clip was not found");
   File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(new Report{fps=fps,passed=true,clips=results.ToArray(),samples=samples.ToArray()},true));Debug.Log("TOUCHLINE_NATURAL_SLIDE_CONTINUITY_OK");EditorApplication.Exit(0);
  }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
 }
}





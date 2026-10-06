using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Touchline.Core;
namespace Touchline.Editor
{
 public static class NaturalReceptionExitDetailedAudit
 {
  [Serializable] class Clip {public string id,statePath,targetPlayer,eventKind;public int targetSequence;public float durationSeconds,eventClock;}
  [Serializable] class Index {public Clip[] clips;}
  [Serializable] class Sample {public int fps,frame;public string action;public float clock,alpha,actionTime,yaw,physicalSpeed,renderedSpeed,leftSpeed,rightSpeed;public Vector3 root,left,right,leftAnchor,rightAnchor;public bool leftStance,rightStance;public float transitionRemaining,transitionDuration,leftPitch,rightPitch,leftSupportPlanarError,rightSupportPlanarError;}
  [Serializable] class RunResult {public int fps;public bool controlObserved,exitObserved;public int exitFrame;public float peakLeftSpeed,peakRightSpeed;}
  [Serializable] class Report {public bool passed,synthetic=false,physicalAndroid=false;public string limitation="Numerical production MatchArena replay; no image or physical-device performance measurement. Root-relative feet speeds include body yaw. This is an audit, with no proposed animation threshold.";public RunResult[] runs;public Sample[] samples;}
  static string Arg(string key,string fallback)=>Environment.GetCommandLineArgs().FirstOrDefault(a=>a.StartsWith(key+"="))?.Substring(key.Length+1)??fallback;
  public static void Run(){try{
   string name=Arg("-touchlineReceptionExitOutput","reception-exit-detailed-v1"),pack=Arg("-touchlineNaturalPack","natural-match-sequences-20261005-v4");
   if(!System.Text.RegularExpressions.Regex.IsMatch(name,"^[a-zA-Z0-9-]+$")||!System.Text.RegularExpressions.Regex.IsMatch(pack,"^[a-zA-Z0-9-]+$"))throw new Exception("Invalid output/pack");
   string input=Path.GetFullPath("../../artifacts/research/"+pack),output=Path.GetFullPath("../../artifacts/unity/"+name);if(Directory.Exists(output))throw new Exception("Preserve previous proofs");Directory.CreateDirectory(output);
   var index=JsonUtility.FromJson<Index>(File.ReadAllText(Path.Combine(input,"index.json")));var clip=index.clips.Single(c=>c.id=="02-high-control");var db=JsonUtility.FromJson<Database>(File.ReadAllText(Path.Combine(input,"database.json")));var samples=new List<Sample>();var results=new List<RunResult>();
   var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;var stanceField=typeof(PlayerView).GetField("captureStance",flags);var anchorField=typeof(PlayerView).GetField("capturePlant",flags);var pitchField=typeof(PlayerView).GetField("capturedAnklePitch",flags);var remainField=typeof(PlayerView).GetField("transitionRemaining",flags);var durationField=typeof(PlayerView).GetField("transitionDuration",flags);
   foreach(int fps in new[]{30,60,120}){
    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var state=JsonUtility.FromJson<MatchState>(File.ReadAllText(Path.Combine(input,clip.statePath)));var simulation=new MatchSimulation(db,state);var root=new GameObject("Natural reception exit audit");var arena=root.AddComponent<MatchArena>();arena.Initialize(db,simulation);arena.Speed=1;arena.Paused=false;arena.Broadcast.SetMode(MatchViewingMode.Full);
    int ai=Array.FindIndex(state.actors,a=>a.id==clip.targetPlayer);if(ai<0)throw new Exception("Missing target");bool observed=false,exit=false,previousControl=false;int exitFrame=-1;float maxLeft=0,maxRight=0;Vector3 previousLeft=Vector3.zero,previousRight=Vector3.zero,previousRoot=Vector3.zero;
    for(int frame=0;frame<Mathf.RoundToInt(clip.durationSeconds*fps);frame++){
     arena.RenderFrame(1f/fps);var actor=state.actors[ai];var visual=arena.PlayerVisual(ai);var position=visual.transform.position;var left=visual.FootPosition(true)-position;var right=visual.FootPosition(false)-position;bool control=MatchSimulation.IsBodyControl(actor);
     if(control&&actor.actionSequence==clip.targetSequence)observed=true;
     if(!exit&&observed&&previousControl&&!control){exit=true;exitFrame=frame;}
     float ls=frame>0?Vector3.Distance(left,previousLeft)*fps:0,rs=frame>0?Vector3.Distance(right,previousRight)*fps:0;
     if(exit&&frame-exitFrame<=Mathf.CeilToInt(.2f*fps)){maxLeft=Mathf.Max(maxLeft,ls);maxRight=Mathf.Max(maxRight,rs);}
     var stance=(bool[])stanceField.GetValue(visual);var anchors=(Vector3[])anchorField.GetValue(visual);var pitches=(float[])pitchField.GetValue(visual);
     samples.Add(new Sample{leftStance=stance[0],rightStance=stance[1],leftAnchor=anchors[0],rightAnchor=anchors[1],leftPitch=pitches[0],rightPitch=pitches[1],transitionRemaining=(float)remainField.GetValue(visual),transitionDuration=(float)durationField.GetValue(visual),leftSupportPlanarError=Vector3.ProjectOnPlane(position+left-anchors[0],Vector3.up).magnitude,rightSupportPlanarError=Vector3.ProjectOnPlane(position+right-anchors[1],Vector3.up).magnitude,fps=fps,frame=frame,action=actor.action+"/"+actor.actionKind,clock=state.clock,alpha=Mathf.Clamp01((float)(state.remainder/MatchSimulation.Step)),actionTime=actor.actionTime,yaw=visual.transform.eulerAngles.y,physicalSpeed=actor.velocity.Length,renderedSpeed=frame>0?Vector3.Distance(position,previousRoot)*fps:0,leftSpeed=ls,rightSpeed=rs,root=position,left=left,right=right});
     previousControl=control;previousRoot=position;previousLeft=left;previousRight=right;
     if(exit&&frame-exitFrame>Mathf.CeilToInt(.35f*fps))break;
    }
    results.Add(new RunResult{fps=fps,controlObserved=observed,exitObserved=exit,exitFrame=exitFrame,peakLeftSpeed=maxLeft,peakRightSpeed=maxRight});UnityEngine.Object.DestroyImmediate(root);
   }
   bool passed=results.All(r=>r.controlObserved&&r.exitObserved);File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(new Report{passed=passed,runs=results.ToArray(),samples=samples.ToArray()},true));if(!passed)throw new Exception("Natural control/exit not reproduced");Debug.Log("TOUCHLINE_RECEPTION_EXIT_AUDIT_OK");EditorApplication.Exit(0);
  }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
 }
}

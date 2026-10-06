using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Touchline.Core;

namespace Touchline.Editor {
 public static class CloseMarkReview {
  [Serializable] class Frame {public int frame;public float clock,markDistance;public string owner,defenderIntent;public Vector3 defender,striker,winger,rightBack,ball;public Point defenderTarget;}
  [Serializable] class Report {public bool passed,physicalAndroid=false;public int fps;public float startClock,endClock;public string note="Native3s replay of the same531.4s cutback state. Camera is an inspection view, not the game broadcast camera. This proves a local coverage sequence, not global balance or physical Android FPS.";public Frame[] frames;public MatchEvent[] events;}
  static string Arg(string key,string fallback)=>Environment.GetCommandLineArgs().FirstOrDefault(x=>x.StartsWith(key+"="))?.Substring(key.Length+1)??fallback;
  public static void Run(){try{
   string name=Arg("-touchlineCloseMarkOutput","close-mark-review");if(!System.Text.RegularExpressions.Regex.IsMatch(name,"^[a-zA-Z0-9-]+$"))throw new Exception("Invalid review output");
   string source=Path.GetFullPath("../../artifacts/staging/session7-score-outlier"),output=Path.GetFullPath("../../artifacts/unity/"+name);if(Directory.Exists(output))throw new Exception("Preserve prior evidence");Directory.CreateDirectory(output);
   var db=JsonUtility.FromJson<Database>(File.ReadAllText(Path.Combine(source,"../session7-kick-turn/database.json")));var state=JsonUtility.FromJson<MatchState>(File.ReadAllText(Path.Combine(source,"coverage-handoff-531.4.json")));var sim=new MatchSimulation(db,state);float start=state.clock;int events=state.events.Count;
   EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var root=new GameObject("Close marking natural replay");var arena=root.AddComponent<MatchArena>();arena.Initialize(db,sim);arena.enabled=false;arena.Speed=1;arena.Broadcast.SetMode(MatchViewingMode.Full);arena.Paused=false;
   var camera=new GameObject("Close marking inspection camera").AddComponent<Camera>();camera.enabled=false;camera.fieldOfView=48;camera.nearClipPlane=.15f;camera.farClipPlane=270;camera.backgroundColor=RenderSettings.fogColor;camera.transform.position=new Vector3(42,23,-23);camera.transform.LookAt(new Vector3(44,0,-3));
   var texture=new RenderTexture(960,640,24){antiAliasing=2};texture.Create();camera.targetTexture=texture;camera.aspect=1.5f;var capture=typeof(KeeperDistributionTurnReview).GetMethod("Capture",BindingFlags.NonPublic|BindingFlags.Static);var rows=new List<Frame>();
   for(int i=0;i<90;i++){arena.RenderFrame(1f/30);capture.Invoke(null,new object[]{camera,texture,root,Path.Combine(output,"frame-"+i.ToString("D4")+".png")});rows.Add(new Frame{frame=i,clock=state.clock,markDistance=Point.Distance(state.actors[14].position,state.actors[9].position),owner=state.ball.owner,defenderIntent=state.actors[14].intent,defender=arena.PlayerVisual(14).transform.position,striker=arena.PlayerVisual(9).transform.position,winger=arena.PlayerVisual(8).transform.position,rightBack=arena.PlayerVisual(15).transform.position,ball=arena.BallDisplayPosition,defenderTarget=sim.MovementTarget(14)});}
   if(Math.Abs(state.clock-start-3)>.15f)throw new Exception("Replay did not advance3seconds");var report=new Report{passed=true,fps=30,startClock=start,endClock=state.clock,frames=rows.ToArray(),events=state.events.Skip(events).ToArray()};File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(report,true));camera.targetTexture=null;texture.Release();UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(camera.gameObject);UnityEngine.Object.DestroyImmediate(root);Debug.Log("TOUCHLINE_CLOSE_MARK_REVIEW_OK");EditorApplication.Exit(0);
  }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
 }
}

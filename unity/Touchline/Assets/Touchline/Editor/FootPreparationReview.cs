using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Touchline.Core;

namespace Touchline.Editor
{
    public static class FootPreparationReview
    {
        [Serializable] class Frame { public int frame;public float clock,remaining,elapsed,angle,yawRate,speed,contactDistance;public string action,kind,owner;public Vector3 root,ball,leftFoot,rightFoot; }
        [Serializable] class Report {public bool passed,physicalAndroid=false;public int fps,preparationFrames,kickFrames;public float maxPreparationYawRate,maxKickYawRate,maxContactDistance;public string finalStateSha256,limitation="Native x1 replay from a real saved simulation state. Core changes may legitimately alter subsequent decisions; this is not a physical Android FPS or motion-capture validation.";public Frame[] frames;}
        static string Arg(string key,string fallback)=>Environment.GetCommandLineArgs().FirstOrDefault(x=>x.StartsWith(key+"="))?.Substring(key.Length+1)??fallback;
        public static void Run()
        {
            try{
                string name=Arg("-touchlineFootOutput","foot-preparation-review"),file=Arg("-touchlineFootState","natural-turn-2891.5.json");int fps=int.Parse(Arg("-touchlineFootFps","30"));
                if(!System.Text.RegularExpressions.Regex.IsMatch(name,"^[a-zA-Z0-9-]+$")||Path.GetFileName(file)!=file||!new[]{30,60,120}.Contains(fps))throw new Exception("Invalid review arguments");
                string input=Path.GetFullPath("../../artifacts/staging/session7-kick-turn"),output=Path.GetFullPath("../../artifacts/unity/"+name);if(Directory.Exists(output))throw new Exception("Preserve previous proof");Directory.CreateDirectory(output);
                var db=JsonUtility.FromJson<Database>(File.ReadAllText(Path.Combine(input,"database.json")));var state=JsonUtility.FromJson<MatchState>(File.ReadAllText(Path.Combine(input,file)));float startClock=state.clock;
                var sim=new MatchSimulation(db,state);EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var root=new GameObject("Foot preparation native replay");var arena=root.AddComponent<MatchArena>();arena.Initialize(db,sim);arena.enabled=false;arena.Speed=1;arena.Broadcast.SetMode(MatchViewingMode.Full);arena.Paused=false;
                int index=Array.FindIndex(state.actors,a=>a.id=="264211");if(index<0)throw new Exception("Natural target missing");var data=db.Find("264211");bool left=data.preferredFoot=="Left"||data.preferredFoot=="Gauche";
                var camera=new GameObject("Foot preparation inspection camera").AddComponent<Camera>();camera.enabled=false;camera.fieldOfView=43;camera.nearClipPlane=.15f;camera.farClipPlane=270;camera.backgroundColor=RenderSettings.fogColor;
                var texture=new RenderTexture(960,640,24){antiAliasing=2};texture.Create();camera.targetTexture=texture;camera.aspect=1.5f;
                var capture=typeof(KeeperDistributionTurnReview).GetMethod("Capture",BindingFlags.NonPublic|BindingFlags.Static);
                var rows=new List<Frame>();var report=new Report{fps=fps};Vector3 focus=Vector3.zero,focusVelocity=Vector3.zero;float previousYaw=0;
                for(int frame=0;frame<8*fps;frame++){
                    arena.RenderFrame(1f/fps);var actor=state.actors[index];var view=arena.PlayerVisual(index);
                    var desired=view.transform.position+Vector3.up;if(frame==0)focus=desired;else focus=Vector3.SmoothDamp(focus,desired,ref focusVelocity,.22f,50,1f/fps);
                    camera.transform.position=focus+new Vector3(4,2.5f,-6);camera.transform.LookAt(focus);capture.Invoke(null,new object[]{camera,texture,root,Path.Combine(output,"frame-"+frame.ToString("D4")+".png")});
                    float yaw=view.transform.eulerAngles.y,rate=frame==0?0:Mathf.Abs(Mathf.DeltaAngle(previousYaw,yaw))*fps;previousYaw=yaw;
                    float alpha=Mathf.Clamp01((float)(state.remainder/MatchSimulation.Step));float remaining=actor.actionTime+(1-alpha)*MatchSimulation.Step;
                    float elapsed=actor.action=="prepare-kick"?Mathf.Max(0,actor.actionContactTime-remaining):actor.action=="kick"?Mathf.Max(0,.64f-remaining):-1;
                    float distance=-1;
                    if(actor.action=="prepare-kick"){report.preparationFrames++;report.maxPreparationYawRate=Mathf.Max(report.maxPreparationYawRate,rate);}
                    if(actor.action=="kick"){
                        report.kickFrames++;report.maxKickYawRate=Mathf.Max(report.maxKickYawRate,rate);
                        if(Mathf.Abs(elapsed-actor.actionContactTime)<=.51f/fps){bool inside=actor.actionKind=="pass"||actor.actionKind=="through"||actor.actionKind=="cutback";distance=Vector3.Distance(inside?view.InsideFootContactPosition(left):view.BootContactPosition(left),arena.BallDisplayPosition);report.maxContactDistance=Mathf.Max(report.maxContactDistance,distance);}
                    }
                    rows.Add(new Frame{frame=frame,clock=state.clock,action=actor.action,kind=actor.actionKind,remaining=remaining,elapsed=elapsed,angle=yaw,yawRate=rate,speed=actor.velocity.Length,contactDistance=distance,owner=state.ball.owner,root=view.transform.position,ball=arena.BallDisplayPosition,leftFoot=view.FootPosition(true),rightFoot=view.FootPosition(false)});
                }
                if(Math.Abs(state.clock-startClock-8)>.15f)throw new Exception("Replay clock did not advance8seconds");
                if(report.kickFrames==0&&report.preparationFrames==0)throw new Exception("No target action observed; cannot assess requested natural situation");
                using(var sha=System.Security.Cryptography.SHA256.Create())report.finalStateSha256=BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(state)))).Replace("-","");
                report.frames=rows.ToArray();report.passed=true;File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(report,true));
                camera.targetTexture=null;texture.Release();UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(camera.gameObject);UnityEngine.Object.DestroyImmediate(root);Debug.Log("TOUCHLINE_FOOT_PREPARATION_REVIEW_OK");EditorApplication.Exit(0);
            }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}

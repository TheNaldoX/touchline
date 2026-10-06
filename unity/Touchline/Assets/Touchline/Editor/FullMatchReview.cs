using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Touchline.Core;
using UnityEditor;
using UnityEditor.Media;
using UnityEngine;
using UnityEngine.UIElements;

namespace Touchline.Editor
{
    // A whole physical match, reviewed through the production broadcast and camera.
    // Footage contains selected natural events; it is not an Android benchmark.
    [InitializeOnLoad] public static class FullMatchReview
    {
        [Serializable] sealed class ReviewEvent { public string kind,player;public float time,observedAt;public int side; }
        [Serializable] sealed class ReviewActor { public string id,action,intent;public float actionTime;public Vector3 root,head,leftBoot,rightBoot; }
        [Serializable] sealed class ReviewStill {
            public string file,phase,ballKind,ballOwner;public int frame,movieFrame,eventCursor;
            public float simulationSeconds,aspect,fieldOfView;public Vector3 ballDisplay,ballModel,ballViewport,cameraPosition,cameraEuler;public ReviewActor[] actors;
        }
        [Serializable] sealed class StillIndex { public ReviewStill[] stills; }
        [Serializable] sealed class ReviewReport {
            public bool passed,physicalAndroid,performanceBenchmark;
            public int seed=20261007,renderedFrames,quietFrames,liveFrames,shotEvents,goalEvents,saveEvents,framingSamples,multisampleCount;
            public float simulationSeconds,secondsAdvancedFromQuietFrames;public string home,away;
            public int[] score;public TeamMetrics[] metrics;public ReviewEvent[] events;public string[] actions;
        }
        static MatchArena arena;static RenderTexture target;static Texture2D pixels;static MediaEncoder encoder;
        static readonly List<ReviewEvent> events=new List<ReviewEvent>();
        static readonly List<ReviewStill> stills=new List<ReviewStill>();
        static readonly HashSet<string> actions=new HashSet<string>();
        static ReviewReport report;static int last=-1,cursor,frame;static float captureUntil=-1,nextStill;
        static string output;static bool capturedStart;
        static FullMatchReview(){EditorApplication.update+=Tick;}
        static string OutputPath(){string name=Environment.GetCommandLineArgs().FirstOrDefault(a=>a.StartsWith("-touchlineFullMatchOutput="))?.Substring("-touchlineFullMatchOutput=".Length)??"full-match-review";if(!System.Text.RegularExpressions.Regex.IsMatch(name,"^[a-zA-Z0-9-]+$"))throw new Exception("Invalid review output");return Path.GetFullPath("../../artifacts/unity/"+name);}
        public static void Run(){if(Directory.Exists(OutputPath()))throw new Exception("Preserve previous full-match review");ProjectBuilder.Configure();SessionState.SetBool("FullMatchReview",true);EditorApplication.isPlaying=true;}
        static void Initialize(){
            var app=TouchlineApp.Instance;app.GetComponent<UIDocument>().rootVisualElement.style.display=DisplayStyle.None;
            var own=app.Database.clubs.First(c=>c.id==app.Career.club);
            var opponent=app.Database.clubs.First(c=>c.id!=own.id&&c.league==own.league&&c.playable&&app.Database.Squad(c.id).Count>=18);
            var sim=MatchSimulation.Create(app.Database,app.Career,opponent.id,20261007,2700);
            arena=new GameObject("Full natural match review").AddComponent<MatchArena>();arena.Initialize(app.Database,sim);arena.enabled=false;
            arena.Broadcast.SetMode(MatchViewingMode.Highlights);arena.Broadcast.QuietSpeed=120;arena.Paused=false;
            target=new RenderTexture(1280,720,24);var descriptor=target.descriptor;descriptor.msaaSamples=2;target.antiAliasing=Math.Max(1,SystemInfo.GetRenderTextureSupportedMSAASampleCount(descriptor));target.Create();arena.MatchCamera.targetTexture=target;arena.MatchCamera.aspect=1280f/720;
            pixels=new Texture2D(1280,720,TextureFormat.RGBA32,false);
            output=OutputPath();if(Directory.Exists(output))throw new Exception("Preserve previous full-match review");Directory.CreateDirectory(output);
            encoder=new MediaEncoder(Path.Combine(output,"natural-event-excerpts.mp4"),new VideoTrackAttributes{frameRate=new MediaRational(30),width=1280,height=720,includeAlpha=false,bitRateMode=VideoBitrateMode.High},Array.Empty<AudioTrackAttributes>());
            report=new ReviewReport{home=own.id,away=opponent.id,multisampleCount=target.antiAliasing};
        }
        static void ReadEvents(){
            var m=arena.Simulation.State;
            while(cursor<m.events.Count){var e=m.events[cursor++];events.Add(new ReviewEvent{kind=e.kind,player=e.player,time=e.time,observedAt=m.clock,side=e.side});
                if(e.kind=="shot")report.shotEvents++;if(e.kind=="goal")report.goalEvents++;if(e.kind=="save")report.saveEvents++;
                if(new[]{"shot","goal","save","woodwork","red","injury","penalty"}.Contains(e.kind)){
                    if(arena.QuietPresentation)throw new Exception("Important event hidden by quiet presentation: "+e.kind);
                    captureUntil=Math.Max(captureUntil,m.clock+3);
                    File.WriteAllText(Path.Combine(output,"event-"+cursor.ToString("D4")+".json"),JsonUtility.ToJson(events[events.Count-1],true));
                }
            }
        }
        static void Capture(){
            var previous=RenderTexture.active;arena.MatchCamera.Render();RenderTexture.active=target;
            pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();RenderTexture.active=previous;
            if(!encoder.AddFrame(pixels))throw new Exception("Native review frame encoding failed");report.renderedFrames++;
            var m=arena.Simulation.State;
            var point=arena.MatchCamera.WorldToViewportPoint(arena.BallDisplayPosition);
            if(point.z<=0||point.x<0||point.x>1||point.y<0||point.y>1)throw new Exception("Natural ball outside frame at "+m.clock+": "+point);
            report.framingSamples++;
            if(m.clock>=nextStill){
                string file="natural-"+frame.ToString("D6")+".png";File.WriteAllBytes(Path.Combine(output,file),pixels.EncodeToPNG());nextStill=m.clock+1;
                var poses=new List<ReviewActor>();for(int i=0;i<m.actors.Length;i++){
                    var actor=m.actors[i];var view=arena.PlayerVisual(i);if(actor.sentOff||view==null)continue;
                    poses.Add(new ReviewActor{id=actor.id,action=actor.action,intent=actor.intent,actionTime=actor.actionTime,root=view.transform.position,head=view.LabelHeadPosition,leftBoot=view.BootContactPosition(true),rightBoot=view.BootContactPosition(false)});
                }
                stills.Add(new ReviewStill{file=file,frame=frame,movieFrame=report.renderedFrames-1,eventCursor=cursor,simulationSeconds=m.clock,phase=m.phase,ballKind=m.ball.kind,ballOwner=m.ball.owner,ballDisplay=arena.BallDisplayPosition,ballModel=new Vector3(m.ball.position.x,m.ball.height,m.ball.position.z),ballViewport=arena.MatchCamera.WorldToViewportPoint(arena.BallDisplayPosition),cameraPosition=arena.MatchCamera.transform.position,cameraEuler=arena.MatchCamera.transform.eulerAngles,aspect=arena.MatchCamera.aspect,fieldOfView=arena.MatchCamera.fieldOfView,actors=poses.ToArray()});
            }
        }
        static void Tick(){
            if(!SessionState.GetBool("FullMatchReview",false)||!EditorApplication.isPlaying||TouchlineApp.Instance==null||last==Time.frameCount)return;last=Time.frameCount;
            try{
                if(arena==null)Initialize();
                for(int batch=0;batch<8;batch++){
                    var m=arena.Simulation.State;
                    if(m.finished){Finish();return;}
                    if(m.halfTime)arena.Simulation.ResumeHalf();
                    arena.Paused=false;
                    bool goalThreat=!arena.QuietPresentation&&(arena.Broadcast.Reason=="Possibilité de frappe"||arena.Broadcast.Reason=="Percée vers la surface"||arena.Broadcast.Reason=="Ballon vers la surface");
                    if(goalThreat)captureUntil=Math.Max(captureUntil,m.clock+1);
                    bool filming=m.clock<captureUntil;
                    arena.Speed=1;bool quietBefore=arena.QuietPresentation;float before=m.clock;
                    arena.RenderFrame(filming?1f/30:.1f);ReadEvents();
                    if(quietBefore){report.quietFrames++;report.secondsAdvancedFromQuietFrames+=m.clock-before;}else report.liveFrames++;
                    // Corner retrieval is intentionally quiet. Review its natural
                    // return to live for the preparation, delivery and first duel.
                    if(m.phase=="corner"&&m.restart>0&&m.restart<=6&&!arena.QuietPresentation)captureUntil=Math.Max(captureUntil,m.clock+5);
                    foreach(var actor in m.actors){
                        if(float.IsNaN(actor.position.x+actor.position.z)||float.IsInfinity(actor.position.x+actor.position.z))throw new Exception("Invalid actor position");
                        actions.Add(actor.action??"unknown");
                    }
                    frame++;
                    if(!arena.QuietPresentation&&(filming||!capturedStart)){Capture();capturedStart=true;break;}
                    if(frame>100000)throw new Exception("Match review exceeded frame budget");
                }
            }catch(Exception error){encoder?.Dispose();encoder=null;SessionState.SetBool("FullMatchReview",false);Debug.LogException(error);EditorApplication.Exit(1);}
        }
        static void Finish(){
            var m=arena.Simulation.State;
            if(m.clock!=5400||m.period!=2||!m.finished)throw new Exception("Review did not simulate the whole 90 minutes");
            if(report.shotEvents!=m.shots.Sum()||report.goalEvents!=m.score.Sum())throw new Exception("Visible events and final scoreboard disagree");
            encoder.Dispose();encoder=null;report.passed=true;report.simulationSeconds=m.clock;report.score=m.score;report.metrics=m.metrics;report.events=events.ToArray();report.actions=actions.OrderBy(x=>x).ToArray();
            File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(report,true));
            File.WriteAllText(Path.Combine(output,"frame-index.json"),JsonUtility.ToJson(new StillIndex{stills=stills.ToArray()},true));
            SessionState.SetBool("FullMatchReview",false);Debug.Log("TOUCHLINE_FULL_MATCH_REVIEW_OK");EditorApplication.Exit(0);
        }
    }
}

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Touchline.Core;

namespace Touchline.Editor
{
    // Deliberately constructed decision/pose regressions, not footage of a real match.
    [InitializeOnLoad] public static class GroundedFinishReview
    {
        [Serializable] class Sample { public int side,period,frame;public float clock,recovery;public string file,ballKind,defenderAction;public Vector3 ball,defender; }
        [Serializable] class Report { public bool passed,synthetic=true,physicalAndroid=false;public int scenarios;public Sample[] samples; }
        static MatchArena arena;static RenderTexture target;static string output;static int scene,frame,last=-1;static Actor striker,defender;static readonly List<Sample> samples=new List<Sample>();
        static GroundedFinishReview(){EditorApplication.update+=Tick;}
        public static void Run(){ProjectBuilder.Configure();SessionState.SetBool("GroundedFinishReview",true);EditorApplication.isPlaying=true;}
        static void Setup()
        {
            var app=TouchlineApp.Instance;app.GetComponent<UIDocument>().rootVisualElement.style.display=DisplayStyle.None;
            output=Path.GetFullPath("../../artifacts/unity/grounded-finish-review-v1");Directory.CreateDirectory(output);
            string away=app.Database.clubs.First(c=>c.id!=app.Career.club&&c.playable&&app.Database.Squad(c.id).Count>=18).id;
            var sim=MatchSimulation.Create(app.Database,app.Career,away,73,2700);var m=sim.State;int side=scene/2,period=scene%2+1;
            m.period=period;m.restart=0;m.phase="play";m.clock=100;sim.Tactic(side).workIntoBox=true;
            foreach(var a in m.actors){a.sentOff=true;a.position=a.previous=new Point(0,30);}
            int dir=sim.Direction(side);striker=m.actors[side*11+9];defender=m.actors[(1-side)*11+2];var chaser=m.actors[(1-side)*11+4];
            striker.sentOff=defender.sentOff=chaser.sentOff=false;m.actors[(1-side)*11].position=new Point(dir*51,0);
            striker.position=striker.previous=striker.carryTarget=new Point(dir*43,0);striker.angle=dir*Mathf.PI*.5f;
            defender.position=defender.previous=new Point(dir*45,0);defender.action="fall";defender.actionTime=1.5f;defender.actionKind="contact-fall";defender.actionSequence=1;defender.angle=-dir*Mathf.PI*.5f;
            chaser.position=chaser.previous=new Point(dir*41,0);chaser.angle=dir*Mathf.PI*.5f;
            m.possessionSide=side;m.ball=new BallState{owner=striker.id,side=side,lastTouch=side,lastTouchId=striker.id,position=striker.position+new Point(dir*.43f,0),previous=striker.position+new Point(dir*.43f,0),height=.11f,previousHeight=.11f,controlOrigin=striker.position,kind="none"};
            if(sim.Decide(striker)!="shot")throw new Exception("Native falling-defender scenario did not choose its available finish");
            arena=new GameObject("Synthetic finish review").AddComponent<MatchArena>();arena.Initialize(app.Database,sim);arena.enabled=false;arena.Speed=1;arena.Broadcast.Enabled=false;arena.Paused=false;
            target=new RenderTexture(1280,720,24);target.antiAliasing=2;target.Create();arena.MatchCamera.targetTexture=target;arena.MatchCamera.aspect=1280f/720;
        }
        static void Tick()
        {
            if(!SessionState.GetBool("GroundedFinishReview",false)||!EditorApplication.isPlaying||TouchlineApp.Instance==null||last==Time.frameCount)return;last=Time.frameCount;
            try{
                if(arena==null){Setup();return;}
                arena.RenderFrame(1f/30);frame++;var m=arena.Simulation.State;int dir=arena.Simulation.Direction(striker.side);
                if(m.events.Any(e=>e.kind=="block"&&e.player==defender.id))throw new Exception("Grounded defender blocked the selected shot");
                var center=new Vector3(dir*47,1,0);arena.MatchCamera.transform.position=center+new Vector3(-dir*6,7,dir*13);arena.MatchCamera.transform.LookAt(center);arena.MatchCamera.fieldOfView=49;
                if(frame==4||frame==10||frame==18){
                    string name="side-"+striker.side+"-half-"+m.period+"-frame-"+frame.ToString("D2")+".png";
                    var previous=RenderTexture.active;arena.MatchCamera.Render();RenderTexture.active=target;var image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes(Path.Combine(output,name),image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);RenderTexture.active=previous;
                    samples.Add(new Sample{side=striker.side,period=m.period,frame=frame,clock=m.clock,recovery=defender.actionTime,file=name,ballKind=m.ball.kind,defenderAction=defender.action,ball=arena.BallDisplayPosition,defender=arena.PlayerVisual((1-striker.side)*11+2).transform.position});
                }
                if(frame<22)return;
                UnityEngine.Object.DestroyImmediate(arena.gameObject);arena=null;target.Release();UnityEngine.Object.DestroyImmediate(target);frame=0;scene++;
                if(scene==4){File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(new Report{passed=true,scenarios=4,samples=samples.ToArray()},true));SessionState.SetBool("GroundedFinishReview",false);Debug.Log("TOUCHLINE_GROUNDED_REVIEW_OK");EditorApplication.Exit(0);}
            }catch(Exception e){SessionState.SetBool("GroundedFinishReview",false);Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}

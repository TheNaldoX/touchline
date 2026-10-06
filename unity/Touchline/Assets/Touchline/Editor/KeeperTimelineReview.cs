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
    [InitializeOnLoad] public static class KeeperTimelineReview
    {
        [Serializable] class Sample { public string file,scenario,action,ballKind;public int side,frame,saves;public float pose,contact,alpha,contactFraction;public bool contactStep,beforeContact;public Vector3 ball,hand; }
        [Serializable] class Report { public bool passed,synthetic=true,physicalAndroid=false;public int scenarios;public Sample[] samples; }
        static MatchArena arena;static RenderTexture target;static string output;static Actor keeper;static int scene,frame,last=-1;static readonly List<Sample> samples=new List<Sample>();
        static KeeperTimelineReview(){EditorApplication.update+=Tick;}
        public static void Run(){ProjectBuilder.Configure();SessionState.SetBool("KeeperTimelineReview",true);EditorApplication.isPlaying=true;}
        static void Setup()
        {
            var app=TouchlineApp.Instance;app.GetComponent<UIDocument>().rootVisualElement.style.display=DisplayStyle.None;
            string name=Environment.GetCommandLineArgs().FirstOrDefault(a=>a.StartsWith("-touchlineKeeperOutput="))?.Split('=')[1]??"keeper-timeline-review-v2";
            if(!System.Text.RegularExpressions.Regex.IsMatch(name,"^[a-zA-Z0-9-]+$"))throw new Exception("Invalid review output name");
            output=Path.GetFullPath("../../artifacts/unity/"+name);Directory.CreateDirectory(output);
            var away=app.Database.clubs.First(c=>c.id!=app.Career.club&&c.playable&&app.Database.Squad(c.id).Count>=18).id;
            var sim=MatchSimulation.Create(app.Database,app.Career,away,1,2700);var m=sim.State;m.restart=0;m.phase="play";m.clock=100;m.decision=10;
            int side=scene/3,mode=scene%3,dir=-sim.Direction(side);
            foreach(var p in m.actors){p.sentOff=true;p.position=p.previous=new Point(0,30);}
            keeper=m.actors[side*11];keeper.sentOff=false;keeper.position=keeper.previous=new Point(dir*49,0);keeper.angle=-dir*Mathf.PI*.5f;
            keeper.action=mode==1?"dive":"idle";keeper.actionTime=mode==1?1.02f:0;keeper.actionSequence=1;keeper.actionKind=mode==1?"save-attempt":"";keeper.diveSide=dir;
            float elapsed=mode==2?.12f:.5f,speed=mode==2?40:30,lateral=mode==0?.2f:mode==1?1.2f:.7f,x=mode==2?47.5f:47;
            var start=new Point(dir*(x-speed*elapsed),lateral);
            keeper.actionTarget=new Point(dir*49,lateral);keeper.actionHeight=.6f;keeper.actionContactTime=.27f;
            m.ball=new BallState{kind="shot",side=1-side,lastTouch=1-side,from=m.actors[(1-side)*11+9].id,start=start,end=start+new Point(dir*speed,0),duration=1,elapsed=elapsed,position=new Point(dir*x,lateral),previous=new Point(dir*x,lateral),height=.6f,previousHeight=.6f,startHeight=.6f,endHeight=.6f};
            arena=new GameObject("Synthetic keeper timeline").AddComponent<MatchArena>();arena.Initialize(app.Database,sim);arena.enabled=false;arena.Speed=1;arena.Broadcast.Enabled=false;arena.Paused=false;
            target=new RenderTexture(1280,720,24);target.antiAliasing=2;target.Create();arena.MatchCamera.targetTexture=target;arena.MatchCamera.aspect=1280f/720;
        }
        static void Tick()
        {
            if(!SessionState.GetBool("KeeperTimelineReview",false)||!EditorApplication.isPlaying||TouchlineApp.Instance==null||last==Time.frameCount)return;last=Time.frameCount;
            try{
                if(arena==null){Setup();return;}
                arena.RenderFrame(.01f);frame++;var m=arena.Simulation.State;int dir=-arena.Simulation.Direction(keeper.side);string scenario=scene%3==0?"catch":scene%3==1?"parry":"unreachable";
                var center=new Vector3(dir*49,1,.5f);arena.MatchCamera.transform.position=center+new Vector3(-dir*5,3.2f,7);arena.MatchCamera.transform.LookAt(center);arena.MatchCamera.fieldOfView=44;
                if(frame==10||frame==12||frame==14||frame==16||frame==17||frame==19||frame==20||frame==24||frame==60||frame==90||frame==120||frame==140){
                    string name=scenario+"-side-"+keeper.side+"-frame-"+frame.ToString("D2")+".png";
                    var previous=RenderTexture.active;arena.MatchCamera.Render();RenderTexture.active=target;var image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes(Path.Combine(output,name),image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);RenderTexture.active=previous;
                    var contact=arena.Simulation.KeeperContact;float alpha=Mathf.Clamp01((float)(m.remainder/.1));bool active=KeeperBallPresentation.Current(contact,m),before=active&&alpha<contact.fraction;
                    if(before){var start=new Vector3(contact.start.x,contact.startHeight,contact.start.z);var impact=new Vector3(contact.impact.x,contact.height,contact.impact.z);var expected=Vector3.Lerp(start,impact,alpha/contact.fraction);if(Vector3.Distance(expected,arena.BallDisplayPosition)>.0001f)throw new Exception("Ball attached or slowed before contact");}
                    samples.Add(new Sample{file=name,scenario=scenario,side=keeper.side,frame=frame,saves=m.metrics[keeper.side].saves,action=keeper.actionKind,ballKind=m.ball.kind,pose=1.2f-keeper.actionTime,contact=keeper.actionContactTime,alpha=alpha,contactFraction=contact.fraction,contactStep=active,beforeContact=before,ball=arena.BallDisplayPosition,hand=arena.PlayerVisual(keeper.side*11).HeldBallPosition});
                }
                if(frame==19){bool save=m.metrics[keeper.side].saves==1;if(save!=(scene%3!=2))throw new Exception("Unexpected keeper reach outcome: "+scenario);if(save&&keeper.actionKind!="save-"+scenario)throw new Exception("Unexpected save outcome: "+keeper.actionKind);}
                // The unreachable shot can score. This isolated scene has no
                // outfield players to take the subsequent kickoff; only the
                // catch/parry scenes need the extended recovery timeline.
                if(frame<(scene%3==2?25:140))return;
                UnityEngine.Object.DestroyImmediate(arena.gameObject);arena=null;target.Release();UnityEngine.Object.DestroyImmediate(target);frame=0;scene++;
                if(scene==6){foreach(int side in new[]{0,1})foreach(string kind in new[]{"catch","parry"})if(!samples.Any(s=>s.side==side&&s.scenario==kind&&s.beforeContact))throw new Exception("Missing pre-contact sample: "+kind+" / "+side);File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(new Report{passed=true,scenarios=6,samples=samples.ToArray()},true));SessionState.SetBool("KeeperTimelineReview",false);Debug.Log("TOUCHLINE_KEEPER_TIMELINE_OK");EditorApplication.Exit(0);}
            }catch(Exception e){SessionState.SetBool("KeeperTimelineReview",false);Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}

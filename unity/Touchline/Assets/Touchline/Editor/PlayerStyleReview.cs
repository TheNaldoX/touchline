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
    // Constructed contrasting profiles, not real-player footage or a balance claim.
    [InitializeOnLoad] public static class PlayerStyleReview
    {
        [Serializable] class Sample { public string file,profile,decision,action,ballKind,owner;public int period,frame;public float clock,actorDistance;public Vector3 ball,actor; }
        [Serializable] class Report { public bool passed,synthetic=true,physicalAndroid=false;public int scenarios;public float overallRating=75,x,ahead,lateral,pressure;public string limitation="Constructed contrasting profiles at equal overall rating and identical tactics. The geometry is selected to expose a different decision; this is not representative match footage or a balance proof.";public Sample[] samples; }
        static readonly List<Sample> samples=new List<Sample>();
        static MatchArena arena;static RenderTexture target;static string output,decision;static Actor player;static Point origin;
        static int scene,frame,last=-1;static float x,ahead,lateral,pressure;
        static PlayerStyleReview(){EditorApplication.update+=Tick;}
        public static void Run()
        {
            string name=Environment.GetCommandLineArgs().FirstOrDefault(a=>a.StartsWith("-touchlineStyleOutput="))?.Substring("-touchlineStyleOutput=".Length)??"player-style-review-v1";
            if(!System.Text.RegularExpressions.Regex.IsMatch(name,"^[a-zA-Z0-9-]+$"))throw new Exception("Invalid review output name");
            output=Path.GetFullPath("../../artifacts/unity/"+name);
            if(Directory.Exists(output)||File.Exists(output))throw new Exception("Use a new output name; existing proof must be preserved");
            SessionState.SetString("PlayerStyleReviewOutput",output);ProjectBuilder.Configure();SessionState.SetBool("PlayerStyleReview",true);EditorApplication.isPlaying=true;
        }
        static MatchSimulation Create(int profile,int period,float startX,float forward,float flank,float markerGap,out Database database,out Actor actor)
        {
            database=new Database{clubs=new[]{new ClubData{id="style-a",name="Synthetic blue",color="#247daa"},new ClubData{id="style-b",name="Synthetic red",color="#bf432c"}},players=Enumerable.Range(0,40).Select(i=>new PlayerData{id="style-"+i,name="Synthetic "+i,team=i<20?"style-a":"style-b",position=i%20==0?"GB":i%20<5?"DEF":i%20<9?"MIL":"ATT",rating=75,heightCm=182,weightKg=76}).ToArray()};
            var career=new Career{club="style-a"};career.lineup=Career.Select(database,career.club,career.tactic);var sim=MatchSimulation.Create(database,career,"style-b",701,2700);
            var state=sim.State;state.restart=0;state.phase="play";state.period=period;state.clock=100;state.decision=10;state.carryTime=1;
            foreach(var a in state.actors){a.sentOff=true;a.position=a.previous=new Point(0,30);}
            actor=state.actors[9];actor.sentOff=false;int dir=sim.Direction(0);actor.position=actor.previous=new Point(dir*startX,0);actor.angle=dir*Mathf.PI*.5f;
            var data=database.Find(actor.id);data.name=profile==0?"Synthetic Distributor":"Synthetic Carrier";
            data.attributes=new[]{"shortPassing","longPassing","vision","dribbling","agility"}.Select(key=>new AttributeValue{key=key,value=(key=="dribbling"||key=="agility")?(profile==0?48:92):(profile==0?92:48)}).ToArray();
            var keeper=state.actors[11];keeper.sentOff=false;keeper.position=keeper.previous=new Point(dir*50,0);
            var mate=state.actors[7];mate.sentOff=false;mate.position=mate.previous=new Point(dir*(startX+forward),flank);
            var cover=state.actors[13];cover.sentOff=false;cover.position=cover.previous=new Point(dir*46,28);
            var marker=state.actors[14];marker.sentOff=false;marker.position=marker.previous=actor.position-new Point(dir*markerGap,0);
            state.possessionSide=0;state.ball=new BallState{owner=actor.id,from=actor.id,side=0,lastTouch=0,lastTouchId=actor.id,position=actor.position,previous=actor.position,height=.11f,previousHeight=.11f,kind="none"};
            return sim;
        }
        static void SelectGeometry()
        {
            foreach(float sx in new[]{12f,0f,25f})foreach(float f in new[]{10f,5f,16f})foreach(float z in new[]{10f,5f,16f})foreach(float gap in new[]{7f,3f}){
                bool contrast=true;
                foreach(int half in new[]{1,2})for(int profile=0;profile<2;profile++){
                    var sim=Create(profile,half,sx,f,z,gap,out _,out var actor);string choice=sim.Decide(actor);
                    if(profile==0?(choice=="carry"||choice=="shot"||choice=="hold"):choice!="carry")contrast=false;
                }
                if(!contrast)continue;x=sx;ahead=f;lateral=z;pressure=gap;return;
            }
            throw new Exception("No mirrored contrasting profile scenario exists in the review grid");
        }
        static void Setup()
        {
            var app=TouchlineApp.Instance;app.GetComponent<UIDocument>().rootVisualElement.style.display=DisplayStyle.None;
            if(scene==0){output=SessionState.GetString("PlayerStyleReviewOutput","");if(string.IsNullOrEmpty(output)||Directory.Exists(output))throw new Exception("Output already exists or review path was lost");Directory.CreateDirectory(output);SelectGeometry();}
            int profile=scene%2,period=scene/2+1;var sim=Create(profile,period,x,ahead,lateral,pressure,out var database,out player);origin=player.position;
            decision=sim.Decide(player);
            if(profile==0?(decision=="carry"||decision=="shot"||decision=="hold"):decision!="carry")throw new Exception("Unexpected profile decision: "+decision);
            arena=new GameObject("Synthetic player style review").AddComponent<MatchArena>();arena.Initialize(database,sim);arena.enabled=false;arena.Speed=1;arena.Broadcast.Enabled=false;arena.Paused=false;
            target=new RenderTexture(1280,720,24){antiAliasing=2};target.Create();arena.MatchCamera.targetTexture=target;arena.MatchCamera.aspect=1280f/720;
        }
        static void Tick()
        {
            if(!SessionState.GetBool("PlayerStyleReview",false)||!EditorApplication.isPlaying||TouchlineApp.Instance==null||last==Time.frameCount)return;last=Time.frameCount;
            try{
                if(arena==null){Setup();return;}
                arena.RenderFrame(1f/30);frame++;var state=arena.Simulation.State;int dir=arena.Simulation.Direction(0);string profile=scene%2==0?"distributor":"carrier";
                var center=new Vector3(dir*(x+ahead*.35f),.5f,lateral*.3f);arena.MatchCamera.transform.position=center+new Vector3(-dir*6,10,17);arena.MatchCamera.transform.LookAt(center);arena.MatchCamera.fieldOfView=46;
                if(frame==4||frame==12||frame==30){
                    string name=profile+"-half-"+state.period+"-frame-"+frame.ToString("D2")+".png";string file=Path.Combine(output,name);if(File.Exists(file))throw new Exception("Capture must not be overwritten");
                    var previous=RenderTexture.active;Texture2D image=null;
                    try{arena.MatchCamera.Render();RenderTexture.active=target;image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes(file,image.EncodeToPNG());}
                    finally{if(image!=null)UnityEngine.Object.DestroyImmediate(image);RenderTexture.active=previous;}
                    samples.Add(new Sample{file=name,profile=profile,decision=decision,period=state.period,frame=frame,clock=state.clock,actorDistance=Point.Distance(origin,player.position),action=player.action,ballKind=state.ball.kind,owner=state.ball.owner,ball=arena.BallDisplayPosition,actor=arena.PlayerVisual(9).transform.position});
                }
                if(frame<32)return;
                UnityEngine.Object.DestroyImmediate(arena.gameObject);arena=null;target.Release();UnityEngine.Object.DestroyImmediate(target);frame=0;scene++;
                if(scene==4){File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(new Report{passed=true,scenarios=4,x=x,ahead=ahead,lateral=lateral,pressure=pressure,samples=samples.ToArray()},true));SessionState.SetBool("PlayerStyleReview",false);Debug.Log("TOUCHLINE_PLAYER_STYLE_REVIEW_OK");EditorApplication.Exit(0);}
            }catch(Exception e){SessionState.SetBool("PlayerStyleReview",false);Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}

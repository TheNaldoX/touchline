using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Media;
using UnityEngine;
using UnityEngine.UIElements;
using Touchline.Core;

namespace Touchline.Editor
{
    [InitializeOnLoad] public static class MatchFilmSmoke
    {
        static int frame,last=-1;static MatchArena arena;static RenderTexture target;static Texture2D texture;static MediaEncoder encoder;static string output;
        static readonly Dictionary<string,int> actions=new Dictionary<string,int>();
        static readonly HashSet<string> intents=new HashSet<string>();
        static int eventCursor,saveEvents,duelPreparations,closeUntil;static Actor closeKeeper;static float normalFieldOfView;static string closeKind="save";
        static readonly Dictionary<string,int> duelSequences=new Dictionary<string,int>();
        static MatchFilmSmoke(){EditorApplication.update+=Tick;}
        public static void Run(){ProjectBuilder.Configure();SessionState.SetBool("MatchFilmSmoke",true);EditorApplication.isPlaying=true;}
        static void Tick()
        {
            if(!SessionState.GetBool("MatchFilmSmoke",false)||!EditorApplication.isPlaying||TouchlineApp.Instance==null||last==Time.frameCount)return;last=Time.frameCount;
            try{
                if(arena==null){
                    var app=TouchlineApp.Instance;app.GetComponent<UIDocument>().rootVisualElement.style.display=DisplayStyle.None;
                    var opponent=app.Database.clubs.First(c=>c.id!=app.Career.club&&c.playable&&app.Database.players.Count(p=>p.team==c.id)>=18);
                    var sim=MatchSimulation.Create(app.Database,app.Career,opponent.id,20261007,2700);
                    arena=new GameObject("Recorded native match").AddComponent<MatchArena>();arena.Initialize(app.Database,sim);arena.enabled=false;arena.Paused=false;
                    target=new RenderTexture(1280,720,24);target.Create();arena.MatchCamera.targetTexture=target;arena.Zoom(-.2f);
                    normalFieldOfView=arena.MatchCamera.fieldOfView;
                    texture=new Texture2D(1280,720,TextureFormat.RGBA32,false);
                    output=Path.GetFullPath("../../artifacts/unity/match-film");Directory.CreateDirectory(output);
                    encoder=new MediaEncoder(Path.Combine(output,"simulation-sequence.mp4"),new VideoTrackAttributes{frameRate=new MediaRational(30),width=1280,height=720,includeAlpha=false},Array.Empty<AudioTrackAttributes>());
                }else{
                    var previous=RenderTexture.active;RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();RenderTexture.active=previous;
                    if(!encoder.AddFrame(texture))throw new Exception("Match frame encoding failed");
                    if(frame%150==0)File.WriteAllBytes(Path.Combine(output,"match-"+frame.ToString("D4")+".png"),texture.EncodeToPNG());
                    if(closeKeeper!=null&&frame<=closeUntil&&frame%3==0)File.WriteAllBytes(Path.Combine(output,closeKind+"-"+frame.ToString("D4")+".png"),texture.EncodeToPNG());
                }
                if(frame>=2700){
                    encoder.Dispose();encoder=null;var m=arena.Simulation.State;
                    File.WriteAllText(Path.Combine(output,"report.json"),"{\"passed\":true,\"frames\":2700,\"fps\":30,\"simulationSeconds\":"+m.clock.ToString(System.Globalization.CultureInfo.InvariantCulture)+",\"seed\":20261007,\"physicalAndroid\":false,\"performanceBenchmark\":false,\"actions\":["+string.Join(",",actions.Keys.OrderBy(x=>x).Select(x=>"\""+x+"\""))+"],\"intents\":["+string.Join(",",intents.OrderBy(x=>x).Select(x=>"\""+x+"\""))+"],\"saveEvents\":"+saveEvents+",\"duelPreparations\":"+duelPreparations+",\"corners\":"+m.metrics.Sum(x=>x.corners)+",\"events\":"+m.events.Count+"}");
                    SessionState.SetBool("MatchFilmSmoke",false);Debug.Log("TOUCHLINE_MATCH_FILM_OK");EditorApplication.Exit(0);return;
                }
                arena.RenderFrame(1f/30);
                var state=arena.Simulation.State;
                while(eventCursor<state.events.Count){var e=state.events[eventCursor++];if(e.kind=="save"){saveEvents++;closeKind="save";closeKeeper=state.actors.FirstOrDefault(a=>a.id==e.player);closeUntil=frame+30;}}
                foreach(var actor in state.actors)if(actor.action=="tackle"&&actor.actionKind==MatchSimulation.StandingDuel&&!string.IsNullOrEmpty(actor.tackleOpponent)&&(!duelSequences.TryGetValue(actor.id,out int sequence)||sequence!=actor.actionSequence)){
                    duelSequences[actor.id]=actor.actionSequence;duelPreparations++;
                    if(frame>closeUntil){closeKind="duel";closeKeeper=actor;closeUntil=frame+36;}
                }
                if(closeKeeper!=null&&frame<=closeUntil){
                    var center=new Vector3(closeKeeper.position.x,.7f,closeKeeper.position.z);int direction=arena.Simulation.Direction(closeKeeper.side);
                    arena.MatchCamera.transform.position=center+(closeKind=="duel"?new Vector3(direction*4,2.3f,5.3f):new Vector3(direction*6,3,8));arena.MatchCamera.transform.LookAt(center);arena.MatchCamera.fieldOfView=48;
                }else arena.MatchCamera.fieldOfView=normalFieldOfView;
                foreach(var a in arena.Simulation.State.actors){if(float.IsNaN(a.position.x+a.position.z))throw new Exception("Invalid actor position");if(!actions.ContainsKey(a.action))actions[a.action]=0;actions[a.action]++;if(!string.IsNullOrEmpty(a.intent))intents.Add(a.intent);}
                frame++;
            }catch(Exception e){encoder?.Dispose();SessionState.SetBool("MatchFilmSmoke",false);Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}


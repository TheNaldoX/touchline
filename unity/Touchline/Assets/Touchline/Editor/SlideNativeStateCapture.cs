using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using Touchline.Core;

namespace Touchline.Editor
{
    // Instrumentation only: no rendering or mutation of the simulated actors.
    // The native JsonUtility database/replay deliberately matches the failing test.
    public static class SlideNativeStateCapture
    {
        [Serializable] public class Source { public string path,sha256; }
        [Serializable] public class Sample
        {
            public string file,sha256,action,actionKind,owner;
            public int tick,actorIndex,actionSequence;
            public float clock,remaining,angle,diveSide,rootToTarget;
            public Point position,previous,velocity,target;
            public MatchEvent[] newEvents;
        }
        [Serializable] public class Index
        {
            public string status="captured",note="Native 0.1s states, not video or Android performance evidence. Consume snapshots in order for continuous visual replay; do not re-simulate under another Core.",databaseFile="database.json",databaseSha256,player="279155";
            public uint initialSeed=31676;
            public string home,away;
            public float step=.1f,startClock,endClock;
            public bool reproducedContact;
            public Sample[] states;
            public Source[] coreSources;
        }
        static string Arg(string key,string fallback)=>Environment.GetCommandLineArgs().FirstOrDefault(x=>x.StartsWith(key+"="))?.Substring(key.Length+1)??fallback;
        static string Hash(byte[] bytes){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
        static string FileHash(string path)=>Hash(File.ReadAllBytes(path));
        public static void Run()
        {
            try {
                string name=Arg("-touchlineSlideStateOutput","session7-slide-native-states");
                if(!System.Text.RegularExpressions.Regex.IsMatch(name,"^[a-zA-Z0-9-]+$"))throw new Exception("Invalid unique output name");
                string output=Path.GetFullPath("../../artifacts/unity/"+name);
                if(Directory.Exists(output))throw new Exception("Preserve existing native evidence: output exists");
                string core=Path.Combine(Application.dataPath,"Touchline/Core");
                const string requiredMovement="25e69139bdd6e6079e1897ce7de3652ca8ccef279ef006d673021a0789943a57";
                if(FileHash(Path.Combine(core,"MatchMovement.cs"))!=requiredMovement)throw new Exception("Capture requires unchanged close-mark V3 movement; coordinator must choose the native source bundle explicitly");
                Directory.CreateDirectory(output);
                var text=Resources.Load<TextAsset>("Data/database");
                if(text==null)throw new Exception("Missing native resource database");
                File.WriteAllText(Path.Combine(output,"database.json"),text.text);
                var db=JsonUtility.FromJson<Database>(text.text);
                var clubs=db.clubs.Where(c=>c.playable&&c.league=="eng.1").Take(2).ToArray();
                var career=new Career{club=clubs[0].id};career.lineup=Career.Select(db,career.club,career.tactic);
                var sim=MatchSimulation.Create(db,career,clubs[1].id,31676);
                var samples=new List<Sample>();int previousEvents=0;bool reproduced=false;
                for(int tick=0;tick<5000;tick++) {
                    if(sim.State.halfTime)sim.ResumeHalf();sim.Advance(.1);var state=sim.State;
                    var newEvents=state.events.Skip(previousEvents).ToArray();previousEvents=state.events.Count;
                    if(state.clock>=459.85f&&state.clock<=465.15f) {
                        int actorIndex=Array.FindIndex(state.actors,a=>a.id=="279155");
                        if(actorIndex<0)throw new Exception("Expected defender missing");
                        var actor=state.actors[actorIndex];
                        string file="state-"+samples.Count.ToString("D4")+".json",path=Path.Combine(output,file);
                        File.WriteAllText(path,JsonUtility.ToJson(state));
                        samples.Add(new Sample{file=file,sha256=FileHash(path),tick=tick,clock=state.clock,actorIndex=actorIndex,action=actor.action,actionKind=actor.actionKind,actionSequence=actor.actionSequence,remaining=actor.actionTime,angle=actor.angle,diveSide=actor.diveSide,rootToTarget=Point.Distance(actor.position,actor.actionTarget),position=actor.position,previous=actor.previous,velocity=actor.velocity,target=actor.actionTarget,owner=state.ball.owner,newEvents=newEvents});
                        if(Math.Abs(state.clock-463.1f)<.025f) {
                            reproduced=actor.action=="slide"&&actor.actionKind==MatchSimulation.SlidingDuel&&Math.Abs(actor.actionTime-1.4f)<.001f&&Point.Distance(actor.position,new Point(41.3191947937f,1.7581845522f))<.001f&&Point.Distance(actor.actionTarget,new Point(42.3366775513f,1.7433052063f))<.001f&&newEvents.Any(e=>e.kind=="tackle"&&e.player==actor.id);
                            File.WriteAllText(Path.Combine(output,"impact-state.json"),JsonUtility.ToJson(state));
                        }
                    }
                    if(state.clock>465.15f)break;
                }
                var report=new Index{status=reproduced?"exact-native-contact-reproduced":"trajectory-mismatch",home=clubs[0].id,away=clubs[1].id,databaseSha256=FileHash(Path.Combine(output,"database.json")),reproducedContact=reproduced,states=samples.ToArray(),startClock=samples.Count==0?0:samples[0].clock,endClock=samples.Count==0?0:samples[samples.Count-1].clock,coreSources=Directory.GetFiles(core,"*.cs").OrderBy(p=>p).Select(p=>new Source{path=Path.GetFileName(p),sha256=FileHash(p)}).ToArray()};
                File.WriteAllText(Path.Combine(output,"index.json"),JsonUtility.ToJson(report,true));
                if(!reproduced||samples.Count<50)throw new Exception("The exact failing trajectory was not reproduced; preserve evidence and do not label it the original slide");
                Debug.Log("TOUCHLINE_SLIDE_NATIVE_STATES_OK "+output);EditorApplication.Exit(0);
            } catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}

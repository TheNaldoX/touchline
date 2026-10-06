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
    // STAGED diagnostic: root must copy this into Editor only with Unity stopped.
    // Real simulation and MatchArena interpolation, synthetic player data. No saves.
    public static class KeeperTransitionReview
    {
        [Serializable] class Frame
        {
            public int scenario,fps,height,frame;public string route,action,kind;
            public bool transition,held;public float time,handSpeed,ballSpeed,rootError,handBallError,leftWristSpeed,rightWristSpeed,contactSpeed,contactBallError;
            public Vector3 hand,ball,root,leftWrist,rightWrist,contact;
        }
        [Serializable] class SceneResult
        {
            public int scenario,fps,height;public string route;public string[] actions;
            public float maxHandSpeed,maxBallSpeed,maxTransitionHandSpeed,maxTransitionBallSpeed,maxRootError,maxTransitionWristSpeed,maxTransitionContactSpeed;
        }
        [Serializable] class Report
        {
            public bool passed,synthetic=true,physicalAndroid=false;
            public string limitation="Native presentation diagnostic over constructed catches. hand means the two-hand grip marker (not a joint); its grip offset changes for one-handed distribution. Separate left/right wrist speeds measure actual bones. contact follows the right distribution hand during roll/throw and the two-hand marker otherwise. Speed maxima are observations, not biomechanical validation or claims about actual hardware FPS.";
            public SceneResult[] scenes;public Frame[] frames;
        }
        static MatchSimulation Create(int height,int mode,out Database db,out Actor keeper)
        {
            db=new Database{
                clubs=new[]{new ClubData{id="transition-a",name="Synthetic blue",color="#247daa"},new ClubData{id="transition-b",name="Synthetic red",color="#bf432c"}},
                players=Enumerable.Range(0,40).Select(i=>new PlayerData{id="transition-"+i,name="Synthetic "+i,team=i<20?"transition-a":"transition-b",position=i%20==0?"GB":i%20<5?"DEF":i%20<9?"MIL":"ATT",rating=75,heightCm=i==0?height:182,weightKg=76}).ToArray()
            };
            var career=new Career{club="transition-a"};career.lineup=Career.Select(db,career.club,career.tactic);
            var sim=MatchSimulation.Create(db,career,"transition-b",701,2700);var m=sim.State;
            m.restart=0;m.phase="play";m.clock=100;m.decision=10;
            foreach(var actor in m.actors){actor.sentOff=true;actor.position=actor.previous=new Point(0,30);}
            keeper=m.actors[0];keeper.sentOff=false;keeper.position=keeper.previous=new Point(-49,0);keeper.angle=Mathf.PI*.5f;
            keeper.action="idle";keeper.actionTime=0;keeper.actionSequence=1;keeper.actionTarget=new Point(-49,.2f);keeper.actionHeight=.6f;keeper.actionContactTime=.27f;
            if(mode>0){
                var mate=m.actors[2];mate.sentOff=false;mate.position=mate.previous=new Point(mode==1?-36:-21,5);
                // Freeze the selected receiver for this diagnostic. The keeper
                // chooses a genuine safe distribution through the production AI.
                mate.action="kick";mate.actionTime=10;
            }
            m.ball=new BallState{kind="shot",side=1,lastTouch=1,from=m.actors[20].id,start=new Point(-32,.2f),end=new Point(-62,.2f),duration=1,elapsed=.5f,position=new Point(-47,.2f),previous=new Point(-47,.2f),height=.6f,previousHeight=.6f,startHeight=.6f,endHeight=.6f};
            return sim;
        }
        static void Capture(Camera camera,RenderTexture target,string path)
        {
            if(File.Exists(path))throw new Exception("Preserve existing capture");
            var previous=RenderTexture.active;Texture2D image=null;
            var skins=new List<SkinnedMeshRenderer>();var proxies=new List<GameObject>();var meshes=new List<Mesh>();
            try{
                // Multiple diagnostic poses are rendered inside one Editor
                // update. Bake the current CPU bones so a cached GPU skin from
                // an earlier sample cannot make the still disagree with them.
                foreach(var skin in camera.transform.parent.GetComponentsInChildren<SkinnedMeshRenderer>()){
                    if(!skin.enabled||!skin.gameObject.activeInHierarchy)continue;
                    var mesh=new Mesh();skin.BakeMesh(mesh);meshes.Add(mesh);
                    var proxy=new GameObject("Current diagnostic skin");proxy.transform.SetParent(skin.transform,false);proxy.AddComponent<MeshFilter>().sharedMesh=mesh;proxy.AddComponent<MeshRenderer>().sharedMaterials=skin.sharedMaterials;
                    proxies.Add(proxy);skins.Add(skin);skin.enabled=false;
                }
                camera.Render();RenderTexture.active=target;image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());
            }
            finally{
                foreach(var skin in skins)skin.enabled=true;foreach(var proxy in proxies)UnityEngine.Object.DestroyImmediate(proxy);foreach(var mesh in meshes)UnityEngine.Object.DestroyImmediate(mesh);
                if(image!=null)UnityEngine.Object.DestroyImmediate(image);RenderTexture.active=previous;
            }
        }
        public static void Run()
        {
            try{
                string name=Environment.GetCommandLineArgs().FirstOrDefault(a=>a.StartsWith("-touchlineKeeperTransitionOutput="))?.Substring("-touchlineKeeperTransitionOutput=".Length)??"keeper-transition-review-v1";
                if(!System.Text.RegularExpressions.Regex.IsMatch(name,"^[a-zA-Z0-9-]+$"))throw new Exception("Invalid review output name");
                var output=Path.GetFullPath("../../artifacts/unity/"+name);
                if(Directory.Exists(output))throw new Exception("Existing evidence folder must not be overwritten");
                Directory.CreateDirectory(output);EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var results=new List<SceneResult>();var frames=new List<Frame>();int scenario=0;
                foreach(int height in new[]{160,182,205})foreach(int fps in new[]{30,60,120})for(int mode=0;mode<3;mode++){
                    var sim=Create(height,mode,out var db,out var keeper);
                    var arena=new GameObject("Keeper transition diagnostic").AddComponent<MatchArena>();arena.Initialize(db,sim);arena.enabled=false;arena.Speed=1;arena.Broadcast.Enabled=false;arena.Paused=false;
                    var target=new RenderTexture(960,640,24){antiAliasing=2};target.Create();arena.MatchCamera.targetTexture=target;arena.MatchCamera.aspect=1.5f;
                    var result=new SceneResult{scenario=scenario,fps=fps,height=height,route=mode==0?"place-rise":mode==1?"roll":"throw"};
                    var actions=new HashSet<string>();string oldAction=null;Vector3 previousHand=Vector3.zero,previousBall=Vector3.zero,previousLeft=Vector3.zero,previousRight=Vector3.zero,previousContact=Vector3.zero;int transitionFrame=-1000;
                    var keeperView=arena.PlayerVisual(0);var joints=keeperView.GetComponentsInChildren<Transform>();var left=joints.First(t=>t.name=="wrist.L");var right=joints.First(t=>t.name=="wrist.R");
                    for(int frame=1;frame<=Mathf.CeilToInt(3.5f*fps);frame++){
                        arena.RenderFrame(1f/fps);var m=sim.State;var view=arena.PlayerVisual(0);float alpha=Mathf.Clamp01((float)(m.remainder/.1));
                        var expected=Point.Lerp(keeper.previous,keeper.position,alpha);var root=view.transform.position;
                        bool transition=oldAction!=null&&oldAction!=keeper.action;if(transition)transitionFrame=frame;
                        var hand=view.HeldBallPosition;var ball=arena.BallDisplayPosition;
                        float handSpeed=frame>1?Vector3.Distance(hand,previousHand)*fps:0,ballSpeed=frame>1?Vector3.Distance(ball,previousBall)*fps:0;
                        var contact=MatchSimulation.HandDistribution(keeper.action)?view.DistributionHandPosition:hand;
                        float leftSpeed=frame>1?Vector3.Distance(left.position,previousLeft)*fps:0,rightSpeed=frame>1?Vector3.Distance(right.position,previousRight)*fps:0,contactSpeed=frame>1?Vector3.Distance(contact,previousContact)*fps:0;
                        float rootError=Vector3.Distance(root,new Vector3(expected.x,0,expected.z));
                        if(float.IsNaN(handSpeed)||float.IsInfinity(handSpeed)||float.IsNaN(ballSpeed)||float.IsInfinity(ballSpeed)||rootError>.0001f)throw new Exception("Non-finite pose or incorrect root interpolation");
                        var item=new Frame{scenario=scenario,fps=fps,height=height,frame=frame,route=result.route,action=keeper.action,kind=m.ball.kind,transition=transition,held=m.ball.held,time=frame/(float)fps,handSpeed=handSpeed,ballSpeed=ballSpeed,rootError=rootError,handBallError=Vector3.Distance(hand,ball),hand=hand,ball=ball,root=root,leftWrist=left.position,rightWrist=right.position,contact=contact,leftWristSpeed=leftSpeed,rightWristSpeed=rightSpeed,contactSpeed=contactSpeed,contactBallError=Vector3.Distance(contact,ball)};
                        frames.Add(item);result.maxHandSpeed=Mathf.Max(result.maxHandSpeed,handSpeed);result.maxBallSpeed=Mathf.Max(result.maxBallSpeed,ballSpeed);result.maxRootError=Mathf.Max(result.maxRootError,rootError);
                        if(frame-transitionFrame<=Mathf.CeilToInt(.2f*fps)){result.maxTransitionHandSpeed=Mathf.Max(result.maxTransitionHandSpeed,handSpeed);result.maxTransitionBallSpeed=Mathf.Max(result.maxTransitionBallSpeed,ballSpeed);result.maxTransitionWristSpeed=Mathf.Max(result.maxTransitionWristSpeed,Mathf.Max(leftSpeed,rightSpeed));result.maxTransitionContactSpeed=Mathf.Max(result.maxTransitionContactSpeed,contactSpeed);}
                        actions.Add(keeper.action);
                        // At most five meaningful stills per route, only the
                        // reference height/FPS. The full numeric timeline covers all27.
                        if(height==182&&fps==60&&transition&&keeper.action!="dive"){
                            var center=new Vector3(-49,.9f,.3f);arena.MatchCamera.transform.position=center+new Vector3(4,2.8f,6);arena.MatchCamera.transform.LookAt(center);arena.MatchCamera.fieldOfView=38;
                            Capture(arena.MatchCamera,target,Path.Combine(output,result.route+"-"+keeper.action+"-"+frame+".png"));
                        }
                        oldAction=keeper.action;previousHand=hand;previousBall=ball;previousLeft=left.position;previousRight=right.position;previousContact=contact;
                    }
                    if(!actions.Contains("keeper-hold")||!actions.Contains(mode==0?"place-ball":mode==1?"keeper-roll":"keeper-throw"))throw new Exception("Missing expected route: "+result.route+" / "+string.Join(",",actions));
                    if(mode==0&&!actions.Contains("keeper-rise"))throw new Exception("No recovery from placement");
                    result.actions=actions.ToArray();results.Add(result);scenario++;
                    UnityEngine.Object.DestroyImmediate(arena.gameObject);target.Release();UnityEngine.Object.DestroyImmediate(target);
                }
                File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(new Report{passed=true,scenes=results.ToArray(),frames=frames.ToArray()},true));
                Debug.Log("TOUCHLINE_KEEPER_TRANSITION_OK");EditorApplication.Exit(0);
            }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}

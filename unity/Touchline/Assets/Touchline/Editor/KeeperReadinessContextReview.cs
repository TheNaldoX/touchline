using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Touchline.Core;

namespace Touchline.Editor
{
    // Staged native diagnostic only. No simulation advancement or Save calls;
    // constructed input states exercise the complete production presentation.
    public static class KeeperReadinessContextReview
    {
        const int Width=640,Height=480,Layer=30;
        const float Duration=2;
        static readonly float[] CaptureTimes={0,.25f,.5f,.75f,1,1.5f,2};
        [Serializable] sealed class Sample {
            public string scenario,context,file,action,ballKind;public int fps,frame;
            public float time,clock,rootError,rigY,leftSole,rightSole,leftWristSpeed,rightWristSpeed,leftSlip,rightSlip,ballRange;
            public bool defending,carrying,claimAnticipation,readinessActive;public float readinessTarget;public Vector3 rig,root,leftWrist,rightWrist,leftFoot,rightFoot;
        }
        [Serializable] sealed class Result {
            public string scenario;public int fps;
            public float minSole=100,maxLeftWristSpeed,maxRightWristSpeed,maxRootError,totalLeftSlip,totalRightSlip,finalRigY;
            public Vector3 finalLeftWrist,finalRightWrist;
        }
        [Serializable] sealed class RateComparison {
            public string scenario;public int fps,referenceFps=60;
            public float rigYDifference,leftWristDifference,rightWristDifference;
        }
        [Serializable] sealed class Source {public string file,sha256;}
        [Serializable] sealed class Report {
            public bool structuralChecksPassed,syntheticInputs=true,productionPresentation=true,physicalAndroid=false;
            public string observedAt;
            public string limitation="Constructed stationary goalkeeper contexts; no Core match or real FIFA video analysed. Sample FPS is invocation cadence, not device performance. This diagnostic does not change readiness, save, interception or ball physics; it renders the installed Runtime candidate. Rig/limb metrics describe the complete production pose and must be reviewed visually before changing behavior.";
            public Result[] results;public Sample[] samples;public RateComparison[] rateComparisons;public Source[] sources;
        }
        static string Hash(string file) {
            using(var algorithm=SHA256.Create())using(var stream=File.OpenRead(file))
                return BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-","");
        }
        static Source[] Sources() {
            var relative=new[]{"Runtime/PlayerView.cs","Runtime/PlayerActionMotion.cs","Runtime/PlayerKeeperMotion.cs","Runtime/KeeperClaimAnticipation.cs","Runtime/KeeperCompactCatch.cs","Runtime/KeeperReadiness.cs","Core/Models.cs"};
            return relative.Select(path=>new Source{file=path,sha256=Hash(Path.Combine(Application.dataPath,"Touchline",path))}).ToArray();
        }
        static void SetLayer(GameObject root) {root.layer=Layer;foreach(Transform child in root.transform)SetLayer(child.gameObject);}
        static MatchState State(out Actor keeper) {
            keeper=new Actor{id="keeper-readiness-review",side=0,slot=0,action="idle",intent="keeper",fitness=100,
                position=new Point(-48,0),previous=new Point(-48,0),angle=Mathf.PI*.5f};
            return new MatchState {home="diagnostic-home",away="diagnostic-away",clock=100,phase="play",restart=0,periodSeconds=2700,engineVersion=4,
                homeTactic=new Tactic(),awayTactic=new Tactic(),actors=new[]{keeper,
                    new Actor{id="attacker",side=1,slot=9,action="idle",position=new Point(-40,0),previous=new Point(-40,0)},
                    new Actor{id="defender",side=0,slot=2,action="idle",position=new Point(35,0),previous=new Point(35,0)}}};
        }
        static string SetContext(MatchState state,int scenario,float time) {
            int mode=scenario==3?(time<.75f?0:time<1.5f?2:0):scenario;
            var attacker=state.actors[1];attacker.action=mode==2?"kick":"idle";attacker.actionKind=mode==2?"shot":null;attacker.actionTime=mode==2?.64f:0;
            state.ball=mode==0?new BallState {kind="none",side=0,owner="defender",position=new Point(35,0),previous=new Point(35,0)}:
                mode==1?new BallState{kind="none",side=1,owner="attacker",position=new Point(-40,0),previous=new Point(-40,0)}:
                new BallState{kind="shot",side=1,from="attacker",elapsed=-.18f,releaseDelay=.18f,duration=.4f,
                    position=new Point(-40,0),previous=new Point(-40,0),start=new Point(-40,0),end=new Point(-53,0)};
            // These are held context probes, not a fake simulated release.
            return mode==0?"far-own-possession":mode==1?"near-opponent-possession":"near-shot-preparation-held";
        }
        static Transform Bone(PlayerView view,string name) => view.GetComponentsInChildren<Transform>().First(t=>t.name==name);
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        static bool Finite(Vector3 value)=>Finite(value.x)&&Finite(value.y)&&Finite(value.z);
        static float Sole(Transform foot,float scale) {
            var q=foot.rotation;var centre=q*new Vector3(0,.02f,.075f);
            float extent=Mathf.Abs((q*Vector3.right).y)*.057f+Mathf.Abs((q*Vector3.up).y)*.085f+Mathf.Abs((q*Vector3.forward).y)*.145f;
            return foot.position.y+(centre.y-extent)*scale;
        }
        static void Capture(GameObject root,Camera camera,RenderTexture target,string file) {
            if(File.Exists(file))throw new Exception("Unique capture names required; preserve previous evidence.");
            var previous=RenderTexture.active;Texture2D image=null;
            var skins=new List<SkinnedMeshRenderer>();var proxies=new List<GameObject>();var meshes=new List<Mesh>();
            try {
                // Several poses are captured synchronously in one Editor update.
                // CPU bake the current bones to avoid stale GPU skinning data.
                foreach(var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>()) {
                    if(!skin.enabled||!skin.gameObject.activeInHierarchy)continue;
                    var mesh=new Mesh();skin.BakeMesh(mesh);meshes.Add(mesh);
                    var proxy=new GameObject("Current readiness skin");proxy.layer=skin.gameObject.layer;proxy.transform.SetParent(skin.transform,false);
                    proxy.AddComponent<MeshFilter>().sharedMesh=mesh;proxy.AddComponent<MeshRenderer>().sharedMaterials=skin.sharedMaterials;
                    proxies.Add(proxy);skins.Add(skin);skin.enabled=false;
                }
                camera.Render();RenderTexture.active=target;image=new Texture2D(Width,Height,TextureFormat.RGB24,false);
                image.ReadPixels(new Rect(0,0,Width,Height),0,0);image.Apply();File.WriteAllBytes(file,image.EncodeToPNG());
            }
            finally {
                foreach(var skin in skins)if(skin!=null)skin.enabled=true;
                foreach(var proxy in proxies)UnityEngine.Object.DestroyImmediate(proxy);
                foreach(var mesh in meshes)UnityEngine.Object.DestroyImmediate(mesh);
                if(image!=null)UnityEngine.Object.DestroyImmediate(image);RenderTexture.active=previous;
            }
        }
        public static void Run() {
            string output=null;
            try {
                string prefix="-touchlineKeeperReadinessOutput=";
                string name=Environment.GetCommandLineArgs().FirstOrDefault(a=>a.StartsWith(prefix))?.Substring(prefix.Length)??"keeper-readiness-context-v1";
                if(!System.Text.RegularExpressions.Regex.IsMatch(name,"^[A-Za-z0-9-]+$"))throw new Exception("Simple output folder name required.");
                output=Path.GetFullPath("../../artifacts/unity/"+name);
                if(Directory.Exists(output)||File.Exists(output))throw new Exception("Preserve existing evidence; choose a new output name.");
                Directory.CreateDirectory(output);var sources=Sources();
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var results=new List<Result>();var samples=new List<Sample>();
                string[] names={"far-own","near-opponent","shot-preparation","context-switch"};
                foreach(int fps in new[]{30,60,120})for(int scenario=0;scenario<4;scenario++) {
                    var state=State(out var keeper);var root=new GameObject("Readiness context diagnostic");RenderTexture target=null;
                    try {
                        var player=new GameObject("Production goalkeeper");player.transform.SetParent(root.transform,false);
                        var view=player.AddComponent<PlayerView>();view.Build(new PlayerData{id=keeper.id,heightCm=182,preferredFoot="Right"},0,0,Color.yellow);
                        var rig=player.transform.Find("Rig");var wrists=new[]{Bone(view,"wrist.L"),Bone(view,"wrist.R")};var feet=new[]{Bone(view,"foot.L"),Bone(view,"foot.R")};
                        var ground=GameObject.CreatePrimitive(PrimitiveType.Plane);ground.transform.SetParent(root.transform,false);ground.transform.position=new Vector3(-48,-.02f,0);
                        ground.transform.localScale=new Vector3(2,1,2);ground.GetComponent<Renderer>().sharedMaterial=PlayerView.Material(new Color(.13f,.24f,.12f));
                        var camera=new GameObject("Readiness diagnostic camera").AddComponent<Camera>();camera.transform.SetParent(root.transform,false);
                        camera.enabled=false;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.055f,.08f,.11f);camera.cullingMask=1<<Layer;
                        camera.fieldOfView=40;camera.nearClipPlane=.05f;camera.farClipPlane=40;
                        camera.transform.position=new Vector3(-48,0,0)+new Vector3(3,2.2f,3.4f);camera.transform.LookAt(new Vector3(-48,.95f,0));
                        var light=new GameObject("Readiness diagnostic light").AddComponent<Light>();light.transform.SetParent(root.transform,false);
                        light.type=LightType.Directional;light.intensity=1.25f;light.cullingMask=1<<Layer;light.transform.rotation=Quaternion.Euler(40,-25,0);
                        SetLayer(root);target=new RenderTexture(Width,Height,24){antiAliasing=2};target.Create();camera.targetTexture=target;camera.aspect=Width/(float)Height;
                        var result=new Result{scenario=names[scenario],fps=fps};Vector3[] lastWrist=null,lastFeet=null;float[] lastSole=null;int capture=0;
                        for(int frame=0;frame<=fps*Duration;frame++) {
                            float time=frame/(float)fps,dt=frame==0?0:1f/fps;
                            state.clock=100+(float)(Math.Floor(time*10+.0001)/10);string contextName=SetContext(state,scenario,time);
                            var context=PlayerMotionContext.From(state,keeper,1);string before=JsonUtility.ToJson(state);
                            view.Render(keeper,1,dt,new Vector3(state.ball.position.x,state.ball.height,state.ball.position.z),context);
                            if(before!=JsonUtility.ToJson(state))throw new Exception("Production presentation mutated diagnostic input state/RNG.");
                            var currentWrist=new[]{wrists[0].position,wrists[1].position};var currentFeet=new[]{feet[0].position,feet[1].position};
                            var soles=new[]{Sole(feet[0],view.transform.localScale.y),Sole(feet[1],view.transform.localScale.y)};
                            float rootError=Vector3.Distance(view.transform.position,new Vector3(keeper.position.x,0,keeper.position.z));
                            if(!Finite(rig.localPosition)||!Finite(currentWrist[0])||!Finite(currentWrist[1])||!Finite(currentFeet[0])||!Finite(currentFeet[1])||rootError>.0001f)throw new Exception("Nonfinite pose or incorrect root.");
                            float leftSpeed=lastWrist==null?0:Vector3.Distance(currentWrist[0],lastWrist[0])/dt,rightSpeed=lastWrist==null?0:Vector3.Distance(currentWrist[1],lastWrist[1])/dt;
                            float leftSlip=0,rightSlip=0;
                            if(lastFeet!=null) {
                                if(soles[0]<=.025f&&lastSole[0]<=.025f)leftSlip=new Vector2(currentFeet[0].x-lastFeet[0].x,currentFeet[0].z-lastFeet[0].z).magnitude;
                                if(soles[1]<=.025f&&lastSole[1]<=.025f)rightSlip=new Vector2(currentFeet[1].x-lastFeet[1].x,currentFeet[1].z-lastFeet[1].z).magnitude;
                            }
                            string file=null;
                            if(capture<CaptureTimes.Length&&time>=CaptureTimes[capture]-.0001f) {
                                file=names[scenario]+"-"+fps+"fps-f"+frame.ToString("D4")+"-t"+Mathf.RoundToInt(time*1000).ToString("D4")+".png";
                                Capture(root,camera,target,Path.Combine(output,file));capture++;
                            }
                            samples.Add(new Sample{scenario=names[scenario],context=contextName,fps=fps,frame=frame,time=time,clock=state.clock,file=file,
                                action=keeper.action,ballKind=state.ball.kind,rootError=rootError,rigY=rig.localPosition.y,rig=rig.localPosition,root=view.transform.position,
                                leftWrist=currentWrist[0],rightWrist=currentWrist[1],leftFoot=currentFeet[0],rightFoot=currentFeet[1],leftSole=soles[0],rightSole=soles[1],
                                leftWristSpeed=leftSpeed,rightWristSpeed=rightSpeed,leftSlip=leftSlip,rightSlip=rightSlip,defending=context.defending,carrying=context.carrying,
                                claimAnticipation=context.keeperClaim.active,readinessActive=context.keeperReadiness.active,readinessTarget=context.keeperReadiness.weight,ballRange=Point.Distance(state.ball.position,keeper.position)});
                            result.minSole=Mathf.Min(result.minSole,Mathf.Min(soles[0],soles[1]));result.maxRootError=Mathf.Max(result.maxRootError,rootError);
                            result.maxLeftWristSpeed=Mathf.Max(result.maxLeftWristSpeed,leftSpeed);result.maxRightWristSpeed=Mathf.Max(result.maxRightWristSpeed,rightSpeed);
                            result.totalLeftSlip+=leftSlip;result.totalRightSlip+=rightSlip;
                            result.finalRigY=rig.localPosition.y;result.finalLeftWrist=currentWrist[0];result.finalRightWrist=currentWrist[1];
                            lastWrist=currentWrist;lastFeet=currentFeet;lastSole=soles;
                        }
                        results.Add(result);
                    }
                    finally {if(target!=null){target.Release();UnityEngine.Object.DestroyImmediate(target);}UnityEngine.Object.DestroyImmediate(root);}
                }
                var comparisons=results.Where(r=>r.fps!=60).Select(r=>{var reference=results.First(other=>other.scenario==r.scenario&&other.fps==60);
                    return new RateComparison{scenario=r.scenario,fps=r.fps,rigYDifference=Mathf.Abs(r.finalRigY-reference.finalRigY),
                        leftWristDifference=Vector3.Distance(r.finalLeftWrist,reference.finalLeftWrist),rightWristDifference=Vector3.Distance(r.finalRightWrist,reference.finalRightWrist)};}).ToArray();
                var after=Sources();if(sources.Where((source,i)=>source.sha256!=after[i].sha256).Any())throw new Exception("Sources changed during native diagnostic.");
                File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(new Report{structuralChecksPassed=true,observedAt=DateTime.UtcNow.ToString("o"),
                    results=results.ToArray(),samples=samples.ToArray(),rateComparisons=comparisons,sources=sources},true));
                Debug.Log("TOUCHLINE_KEEPER_READINESS_CONTEXT_OK");EditorApplication.Exit(0);
            }
            catch(Exception e) {if(output!=null&&Directory.Exists(output))File.WriteAllText(Path.Combine(output,"failure.txt"),e.ToString());Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Touchline.Core;
namespace Touchline.Editor {
 public static class KeeperDistributionTurnReview {
  [Serializable] class Clip {public string id,statePath,category,targetPlayer,eventKind;public int targetSequence,eventIndex;public float startClock,eventClock,durationSeconds;}
  [Serializable] class Index {public Clip[] clips;}
  [Serializable] class Frame {public string clip,action,phase,flightKind,receiver;public int frame;public float clock,height,previewWeight,contactDistance,alpha,physicalElapsed,contactDeadline,targetDistance,previewEta,actorYaw,renderedYaw,yawRate,shoulderBallDistance,armReach,wristTargetReach,reachExcess,leftFootHorizontalSpeed,rightFootHorizontalSpeed;public bool heldTurn;public float heldContactDistance,nearestBodyDistance;public bool distributionPreparation,trailingFootExpectedSupport;public Vector3 shoulder,elbow,wrist;public bool previewActive;public Vector3 contactPoint,actionTarget;public bool receivingLeft;public Vector3 ball,actor,leftFoot,rightFoot;}
  [Serializable] class Result {public string id;public bool eventObserved;public float startClock,endClock,firstObservedClock;public int frames;}
  [Serializable] class Report {public bool passed,physicalAndroid=false,synthetic=false;public int fps=30;public string finalStateSha256;public float maxHeldTurnContactDistance,maxHeldTurnYawRate,maxDistributionYawRate,minDistributionBodyDistance;public float maxPreparationContactDistance,maxPreparationReachExcess,maxDistributionLeftFootSpeed,maxDistributionRightFootSpeed;public bool preparationContactWithinOriginalReleaseTolerance;public string reviewStatus="Measurements only: requires before/after foot-support and visual review, not whole-animation acceptance.";public string limitation="Actual saved Core sequences, production MatchArena/PlayerView at x1. Additional tracking review camera; broadcast snapshots are separate. Offline native renders, not Android performance or real-world match footage. No generated frames.";public Result[] clips;public Frame[] samples;}
  static void Capture(Camera camera,RenderTexture target,GameObject root,string path){
   var skins=new List<SkinnedMeshRenderer>();var proxies=new List<GameObject>();var meshes=new List<Mesh>();var previous=RenderTexture.active;Texture2D image=null;
   try {foreach(var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>()){if(!skin.enabled||!skin.gameObject.activeInHierarchy)continue;var mesh=new Mesh();skin.BakeMesh(mesh);meshes.Add(mesh);var proxy=new GameObject("Native current skin");proxy.layer=skin.gameObject.layer;proxy.transform.SetParent(skin.transform,false);proxy.AddComponent<MeshFilter>().sharedMesh=mesh;proxy.AddComponent<MeshRenderer>().sharedMaterials=skin.sharedMaterials;proxies.Add(proxy);skins.Add(skin);skin.enabled=false;}
    camera.Render();RenderTexture.active=target;image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());
   } finally {foreach(var skin in skins)skin.enabled=true;foreach(var proxy in proxies)UnityEngine.Object.DestroyImmediate(proxy);foreach(var mesh in meshes)UnityEngine.Object.DestroyImmediate(mesh);if(image!=null)UnityEngine.Object.DestroyImmediate(image);RenderTexture.active=previous;}
  }
  static string Arg(string key,string fallback)=>Environment.GetCommandLineArgs().FirstOrDefault(a=>a.StartsWith(key+"="))?.Substring(key.Length+1)??fallback;
  public static void Run(){try{
   int fps=int.Parse(Arg("-touchlineNaturalFps","30"));if(fps!=30&&fps!=60&&fps!=120)throw new Exception("Invalid rate");string only=Arg("-touchlineNaturalOnly","07-save");
   var name=Arg("-touchlineNaturalOutput","keeper-distribution-turn-v1");var pack=Arg("-touchlineNaturalPack","natural-match-sequences-20261005-v6");
   if(!System.Text.RegularExpressions.Regex.IsMatch(name,"^[a-zA-Z0-9-]+$")||!System.Text.RegularExpressions.Regex.IsMatch(pack,"^[a-zA-Z0-9-]+$"))throw new Exception("Invalid proof names");
   var input=Path.GetFullPath("../../artifacts/research/"+pack);var output=Path.GetFullPath("../../artifacts/unity/"+name);if(Directory.Exists(output))throw new Exception("Preserve previous proofs");Directory.CreateDirectory(output);
   var index=JsonUtility.FromJson<Index>(File.ReadAllText(Path.Combine(input,"index.json")));var db=JsonUtility.FromJson<Database>(File.ReadAllText(Path.Combine(input,"database.json")));
   if(index?.clips==null||index.clips.Length==0||db==null)throw new Exception("Invalid or empty replay pack");
   var results=new List<Result>();var samples=new List<Frame>();
   var flag=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
   var weightField=typeof(PlayerView).GetField("receptionPreparationWeight",flag);var sideField=typeof(PlayerView).GetField("receivingLeft",flag);
   foreach(var clip in index.clips){
    if(clip.id!=only)continue;
    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
    if(!System.Text.RegularExpressions.Regex.IsMatch(clip.id,"^[a-zA-Z0-9-]+$")||clip.durationSeconds<=0||clip.durationSeconds>30)throw new Exception("Invalid clip");
    var state=JsonUtility.FromJson<MatchState>(File.ReadAllText(Path.Combine(input,clip.statePath)));var sim=new MatchSimulation(db,state);var root=new GameObject("Native natural match");var arena=root.AddComponent<MatchArena>();arena.Initialize(db,sim);arena.Speed=1;arena.Paused=false;arena.Broadcast.SetMode(MatchViewingMode.Full);
    var camera=new GameObject("Animation inspection camera").AddComponent<Camera>();camera.enabled=false;camera.fieldOfView=43;camera.nearClipPlane=.15f;camera.farClipPlane=270;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=RenderSettings.fogColor;
    var target=new RenderTexture(960,640,24){antiAliasing=2};target.Create();camera.targetTexture=target;camera.aspect=1.5f;arena.MatchCamera.targetTexture=target;arena.MatchCamera.aspect=1.5f;
    string dir=Path.Combine(output,clip.id);Directory.CreateDirectory(dir);int count=Mathf.RoundToInt(clip.durationSeconds*fps);bool observed=false;float previousYaw=0;Vector3 previousLeftFoot=Vector3.zero,previousRightFoot=Vector3.zero;float firstObserved=-1;Vector3 focus=Vector3.zero,velocity=Vector3.zero;
    int ai=Array.FindIndex(state.actors,a=>a.id==clip.targetPlayer);if(ai<0)throw new Exception("Target not found: "+clip.id);
    for(int i=0;i<count;i++){
     arena.RenderFrame(1f/fps);var actor=state.actors[ai];var visual=arena.PlayerVisual(ai);var p=visual.transform.position;
     Vector3 desired=Vector3.Lerp(p,arena.BallDisplayPosition,.3f);desired.y=1;
     if(i==0)focus=desired;else focus=Vector3.SmoothDamp(focus,desired,ref velocity,.22f,50,1f/fps);
     bool oppositeSide=Arg("-touchlineKeeperCamera","side")=="side";
     camera.transform.position=focus+(oppositeSide?new Vector3(p.x>=0?-2.5f:2.5f,2.6f,-6):new Vector3(p.x>=0?-6:6,3.2f,5));camera.transform.LookAt(focus);Capture(camera,target,root,Path.Combine(dir,"frame-"+i.ToString("D4")+".png"));
     if(i==0||Math.Abs(state.clock-clip.eventClock)<.051f)Capture(arena.MatchCamera,target,root,Path.Combine(dir,"broadcast-"+i.ToString("D4")+".png"));
     bool action=clip.eventIndex<0&&actor.actionSequence==clip.targetSequence&&actor.actionKind==clip.eventKind&&Math.Abs(state.clock-clip.eventClock)<.151f;
     bool ev=clip.eventIndex>=0&&state.events.Count>clip.eventIndex&&state.events[clip.eventIndex].kind==clip.eventKind&&state.events[clip.eventIndex].player==clip.targetPlayer&&Math.Abs(state.events[clip.eventIndex].time-clip.eventClock)<.051f;
     if(!observed&&state.clock>=clip.eventClock-.05f&&(action||ev)){observed=true;firstObserved=state.clock;}
     float preview=weightField==null?-1:(float)weightField.GetValue(visual);
     bool receiveLeft=(bool)sideField.GetValue(visual);
     bool bodyControl=MatchSimulation.IsBodyControl(actor),header=actor.action=="header",distribution=MatchSimulation.HandDistribution(actor.action);
     float alpha=Mathf.Clamp01((float)(state.remainder/MatchSimulation.Step));
     float duration=distribution?MatchSimulation.KeeperDistributionDuration:bodyControl?MatchSimulation.BodyControlDuration(actor.actionKind):.64f;
     float physicalElapsed=bodyControl||header||distribution?Mathf.Max(0,duration-(actor.actionTime+(1-alpha)*MatchSimulation.Step)):-1;
     var contactPoint=bodyControl?(actor.actionKind==MatchSimulation.ChestControl?visual.ChestContactPosition:visual.ThighContactPosition(receiveLeft)):header?visual.HeaderContactPosition:distribution?visual.DistributionHandPosition:Vector3.zero;
     var actionTarget=new Vector3(actor.actionTarget.x,actor.actionHeight,actor.actionTarget.z);
     float contactDistance=bodyControl||header||distribution?Vector3.Distance(contactPoint,arena.BallDisplayPosition):-1;
     var preparation=BodyReceptionAnticipationSample.From(state,actor,alpha,visual.MotionStature);
     var joints=visual.GetComponentsInChildren<Transform>();var shoulder=joints.First(x=>x.name=="upperarm01.R").position;var elbow=joints.First(x=>x.name=="lowerarm01.R").position;var wrist=joints.First(x=>x.name=="wrist.R").position;
     float yaw=visual.transform.eulerAngles.y;float yawRate=i==0?0:Mathf.DeltaAngle(previousYaw,yaw)*fps;previousYaw=yaw;
     var leftFoot=visual.FootPosition(true);var rightFoot=visual.FootPosition(false);
     float leftFootSpeed=i==0?0:Vector2.Distance(new Vector2(leftFoot.x,leftFoot.z),new Vector2(previousLeftFoot.x,previousLeftFoot.z))*fps;
     float rightFootSpeed=i==0?0:Vector2.Distance(new Vector2(rightFoot.x,rightFoot.z),new Vector2(previousRightFoot.x,previousRightFoot.z))*fps;
     previousLeftFoot=leftFoot;previousRightFoot=rightFoot;
     float reach=Vector3.Distance(shoulder,elbow)+Vector3.Distance(elbow,wrist);
     var wristTarget=arena.BallDisplayPosition-(contactPoint-wrist);
     float targetReach=distribution?Vector3.Distance(shoulder,wristTarget):0;
     bool inPreparation=distribution&&physicalElapsed>=0&&physicalElapsed<=actor.actionContactTime+.0001f;
     samples.Add(new Frame{heldTurn=actor.action=="keeper-hold"&&actor.actionKind==MatchSimulation.KeeperDistributionTurn,heldContactDistance=Vector3.Distance(visual.HeldBallPosition,arena.BallDisplayPosition),nearestBodyDistance=state.actors.Where(x=>x!=actor&&!x.sentOff).Select(x=>Point.Distance(actor.position,x.position)).DefaultIfEmpty(100).Min(),distributionPreparation=inPreparation,trailingFootExpectedSupport=distribution,wristTargetReach=targetReach,reachExcess=distribution?Mathf.Max(0,targetReach-reach):0,leftFootHorizontalSpeed=leftFootSpeed,rightFootHorizontalSpeed=rightFootSpeed,shoulder=shoulder,elbow=elbow,wrist=wrist,shoulderBallDistance=Vector3.Distance(shoulder,arena.BallDisplayPosition),armReach=Vector3.Distance(shoulder,elbow)+Vector3.Distance(elbow,wrist),yawRate=yawRate,alpha=alpha,physicalElapsed=physicalElapsed,contactDeadline=bodyControl?MatchSimulation.BodyControlContactTime:actor.actionContactTime,targetDistance=bodyControl||header||distribution?Vector3.Distance(contactPoint,actionTarget):-1,contactPoint=contactPoint,actionTarget=actionTarget,previewActive=preparation.active,previewEta=preparation.eta,actorYaw=actor.angle*Mathf.Rad2Deg,renderedYaw=visual.transform.eulerAngles.y,previewWeight=preview,contactDistance=contactDistance,receivingLeft=receiveLeft,leftFoot=visual.FootPosition(true),rightFoot=visual.FootPosition(false),flightKind=state.ball.kind,receiver=state.ball.to,clip=clip.id,frame=i,clock=state.clock,action=actor.action+"/"+actor.actionKind,phase=state.phase,height=state.ball.height,ball=arena.BallDisplayPosition,actor=p});
    }
    if(Math.Abs(state.clock-clip.startClock-clip.durationSeconds)>.151f)throw new Exception("Replay pacing changed or paused: "+clip.id);
    using(var sha=System.Security.Cryptography.SHA256.Create())File.WriteAllText(Path.Combine(output,"final-state-sha256.txt"),BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(state)))).Replace("-",""));
    results.Add(new Result{id=clip.id,eventObserved=observed,startClock=clip.startClock,endClock=state.clock,firstObservedClock=firstObserved,frames=count});
    camera.targetTexture=null;arena.MatchCamera.targetTexture=null;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(camera.gameObject);UnityEngine.Object.DestroyImmediate(root);
    File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(new Report{fps=fps,passed=false,clips=results.ToArray(),samples=samples.ToArray()},true));
    if(!observed)throw new Exception("Natural target event not reproduced: "+clip.id);
   }
   if(results.Count!=1)throw new Exception("Requested clip was not found");
   File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(new Report{fps=fps,passed=true,maxHeldTurnContactDistance=samples.Where(x=>x.heldTurn).Select(x=>x.heldContactDistance).DefaultIfEmpty(-1).Max(),maxHeldTurnYawRate=samples.Where(x=>x.heldTurn).Select(x=>Mathf.Abs(x.yawRate)).DefaultIfEmpty(-1).Max(),maxDistributionYawRate=samples.Where(x=>x.distributionPreparation).Select(x=>Mathf.Abs(x.yawRate)).DefaultIfEmpty(-1).Max(),minDistributionBodyDistance=samples.Where(x=>x.trailingFootExpectedSupport).Select(x=>x.nearestBodyDistance).DefaultIfEmpty(-1).Min(),clips=results.ToArray(),samples=samples.ToArray(),maxPreparationContactDistance=samples.Where(x=>x.distributionPreparation).Select(x=>x.contactDistance).DefaultIfEmpty(-1).Max(),maxPreparationReachExcess=samples.Where(x=>x.distributionPreparation).Select(x=>x.reachExcess).DefaultIfEmpty(-1).Max(),maxDistributionLeftFootSpeed=samples.Where(x=>x.trailingFootExpectedSupport).Select(x=>x.leftFootHorizontalSpeed).DefaultIfEmpty(-1).Max(),maxDistributionRightFootSpeed=samples.Where(x=>x.trailingFootExpectedSupport).Select(x=>x.rightFootHorizontalSpeed).DefaultIfEmpty(-1).Max(),preparationContactWithinOriginalReleaseTolerance=samples.Any(x=>x.distributionPreparation)&&samples.Where(x=>x.distributionPreparation).All(x=>x.contactDistance<.14f)},true));Debug.Log("TOUCHLINE_KEEPER_DISTRIBUTION_TURN_OK");EditorApplication.Exit(0);
  }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
 }
}

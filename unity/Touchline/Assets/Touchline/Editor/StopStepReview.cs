using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Touchline.Core;
namespace Touchline.Editor
{
 [InitializeOnLoad] public static class StopStepReview
 {
  [Serializable] class Sample { public string file,scenario;public bool candidate,plannerActive;public int fps,playback,frame,movingFoot;public float time,leftSole,rightSole;public Vector3 root,left,right; }
  [Serializable] class Result { public string scenario;public bool candidate;public int fps,playback,activeFrames;public float leftSlip,rightSlip,minimumSole=1; }
  [Serializable] class Report { public bool passed,synthetic=true,physicalAndroid=false;public string limitation="Constructed identical braking trajectories with and without the presentation-only settling layer. Ground slip is integrated only when the actual rotated sole is near the turf in both consecutive frames. No mocap or physical-device performance claim.";public Result[] results;public Sample[] samples; }
  static readonly List<Sample> samples=new List<Sample>();static readonly List<Result> results=new List<Result>();
  static readonly int[] rates={30,60,120};static readonly FieldInfo plannerField=typeof(PlayerView).GetField("stoppingSteps",BindingFlags.NonPublic|BindingFlags.Instance),movedField=typeof(PlayerView).GetField("movedBeforeStop",BindingFlags.NonPublic|BindingFlags.Instance);
  static GameObject sceneRoot;static PlayerView view;static Transform[] feet;static Camera camera;static RenderTexture target;static string output;static Result result;
  static int scene,frame,last=-1,captureIndex;static float previousTime;static Vector3[] previousFeet;static float[] previousSoles;
  static readonly float[] captureTimes={1f,1.1f,1.2f,1.4f,1.8f};
  static StopStepReview(){EditorApplication.update+=Tick;}
  public static void Run(){
   string name=Environment.GetCommandLineArgs().FirstOrDefault(a=>a.StartsWith("-touchlineStopOutput="))?.Substring("-touchlineStopOutput=".Length)??"stop-step-review-v1";
   if(!System.Text.RegularExpressions.Regex.IsMatch(name,"^[a-zA-Z0-9-]+$"))throw new Exception("Invalid output name");output=Path.GetFullPath("../../artifacts/unity/"+name);
   if(Directory.Exists(output)||File.Exists(output))throw new Exception("Preserve existing proof");SessionState.SetString("StopStepReviewOutput",output);ProjectBuilder.Configure();SessionState.SetBool("StopStepReview",true);EditorApplication.isPlaying=true;
  }
  static void Layer(GameObject o){o.layer=30;foreach(Transform c in o.transform)Layer(c.gameObject);}
  static void Setup(){
   TouchlineApp.Instance.GetComponent<UIDocument>().rootVisualElement.style.display=DisplayStyle.None;
   if(scene==0){output=SessionState.GetString("StopStepReviewOutput","");if(Directory.Exists(output))throw new Exception("Proof already exists");Directory.CreateDirectory(output);}
   result=new Result{candidate=scene%2==1,scenario=scene/2%2==0?"free-stop":"press-ready-stop",fps=rates[scene/4%3],playback=scene/12==0?1:10};
   sceneRoot=new GameObject("Synthetic grounded stop");var player=new GameObject("Player");player.transform.SetParent(sceneRoot.transform);view=player.AddComponent<PlayerView>();view.Build(new PlayerData{id="stop-review",heightCm=182,preferredFoot="Right"},0,9,new Color(.08f,.45f,.76f));
   feet=new[]{player.GetComponentsInChildren<Transform>().First(t=>t.name=="foot.L"),player.GetComponentsInChildren<Transform>().First(t=>t.name=="foot.R")};
   var ground=GameObject.CreatePrimitive(PrimitiveType.Plane);ground.transform.SetParent(sceneRoot.transform);ground.transform.localScale=Vector3.one*2;ground.GetComponent<Renderer>().sharedMaterial=PlayerView.Material(new Color(.16f,.28f,.13f));
   camera=new GameObject("Stop review camera").AddComponent<Camera>();camera.transform.SetParent(sceneRoot.transform);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.05f,.08f,.10f);camera.cullingMask=1<<30;camera.fieldOfView=38;camera.enabled=false;
   var light=new GameObject("Stop review light").AddComponent<Light>();light.transform.SetParent(sceneRoot.transform);light.type=LightType.Directional;light.intensity=1.2f;light.transform.rotation=Quaternion.Euler(45,-25,0);light.cullingMask=1<<30;
   Layer(sceneRoot);target=new RenderTexture(960,720,24){antiAliasing=2};target.Create();camera.targetTexture=target;camera.aspect=4f/3;previousFeet=null;previousSoles=null;previousTime=0;captureIndex=0;
  }
  static float Sole(Transform f){var q=f.rotation;var center=q*new Vector3(0,.02f,.075f);float extent=Mathf.Abs((q*Vector3.right).y)*.057f+Mathf.Abs((q*Vector3.up).y)*.085f+Mathf.Abs((q*Vector3.forward).y)*.145f;return f.position.y+(center.y-extent)*view.transform.localScale.y;}
  static void Capture(string name){
   var old=RenderTexture.active;Texture2D image=null;var skins=new List<SkinnedMeshRenderer>();var proxies=new List<GameObject>();var meshes=new List<Mesh>();
   try{
    foreach(var skin in view.GetComponentsInChildren<SkinnedMeshRenderer>()){if(!skin.enabled)continue;var mesh=new Mesh();skin.BakeMesh(mesh);meshes.Add(mesh);var proxy=new GameObject("Current measured stop skin");proxy.layer=skin.gameObject.layer;proxy.transform.SetParent(skin.transform,false);proxy.AddComponent<MeshFilter>().sharedMesh=mesh;proxy.AddComponent<MeshRenderer>().sharedMaterials=skin.sharedMaterials;proxies.Add(proxy);skins.Add(skin);skin.enabled=false;}
    camera.Render();RenderTexture.active=target;image=new Texture2D(960,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,960,720),0,0);image.Apply();File.WriteAllBytes(Path.Combine(output,name),image.EncodeToPNG());
   }finally{foreach(var skin in skins)skin.enabled=true;foreach(var proxy in proxies)UnityEngine.Object.DestroyImmediate(proxy);foreach(var mesh in meshes)UnityEngine.Object.DestroyImmediate(mesh);if(image!=null)UnityEngine.Object.DestroyImmediate(image);RenderTexture.active=old;}
  }
  static void Tick(){
   if(!SessionState.GetBool("StopStepReview",false)||!EditorApplication.isPlaying||TouchlineApp.Instance==null||last==Time.frameCount)return;last=Time.frameCount;
   try{
    if(view==null){Setup();return;}float dt=(float)result.playback/result.fps,time=frame*dt,u=time-.5f,speed=time<.5f?3:time<1?3-6*u:0,z=time<.5f?3*time:time<1?1.5f+3*u-3*u*u:2.25f;
    var actor=new Actor{id="stop-review",slot=9,action="run",intent=result.scenario=="free-stop"?"shape":"press",angle=0,position=new Point(0,z),previous=new Point(0,z),velocity=new Point(0,speed)};
    var planner=(StopStepMotion)plannerField.GetValue(view);
    // Baseline disables only this new presentation layer. Physics, actor pose,
    // other animation layers and render trajectory are identical.
    if(!result.candidate){planner.Cancel();movedField.SetValue(view,false);}
    string before=JsonUtility.ToJson(actor);view.Render(actor,1,frame==0?0:dt,new Vector3(0,.11f,4),new PlayerMotionContext{defending=result.scenario!="free-stop",hasSimulationClock=true,simulationClock=(float)Math.Floor(time*10+.0001f)/10});
    if(before!=JsonUtility.ToJson(actor)||Vector3.Distance(view.transform.position,new Vector3(0,0,z))>.0001f)throw new Exception("Rendering changed simulation");
    var positions=new[]{feet[0].position,feet[1].position};var soles=new[]{Sole(feet[0]),Sole(feet[1])};if(planner.Active)result.activeFrames++;
    if(time>=1&&previousTime>=1&&previousFeet!=null)for(int i=0;i<2;i++){result.minimumSole=Mathf.Min(result.minimumSole,soles[i]);if(soles[i]<=.025f&&previousSoles[i]<=.025f){float slip=new Vector2(positions[i].x-previousFeet[i].x,positions[i].z-previousFeet[i].z).magnitude;if(i==0)result.leftSlip+=slip;else result.rightSlip+=slip;}}
    string file=null;camera.transform.position=view.transform.position+new Vector3(4,2.6f,4);camera.transform.LookAt(view.transform.position+Vector3.up*.95f);
    if(captureIndex<captureTimes.Length&&time>=captureTimes[captureIndex]-.0001f){file=result.scenario+"-"+(result.candidate?"candidate":"baseline")+"-"+result.fps+"fps-x"+result.playback+"-"+frame.ToString("D3")+".png";Capture(file);while(captureIndex<captureTimes.Length&&time>=captureTimes[captureIndex]-.0001f)captureIndex++;}
    samples.Add(new Sample{file=file,scenario=result.scenario,candidate=result.candidate,fps=result.fps,playback=result.playback,frame=frame,time=time,plannerActive=planner.Active,movingFoot=planner.MovingFoot,root=view.transform.position,left=positions[0],right=positions[1],leftSole=soles[0],rightSole=soles[1]});
    previousFeet=positions;previousSoles=soles;previousTime=time;frame++;if(time<2)return;
    results.Add(result);UnityEngine.Object.DestroyImmediate(sceneRoot);view=null;target.Release();UnityEngine.Object.DestroyImmediate(target);frame=0;scene++;
    if(scene==24){
     File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(new Report{passed=true,results=results.ToArray(),samples=samples.ToArray()},true));SessionState.SetBool("StopStepReview",false);Debug.Log("TOUCHLINE_STOP_STEP_REVIEW_OK");EditorApplication.Exit(0);
    }
   }catch(Exception e){SessionState.SetBool("StopStepReview",false);Debug.LogException(e);EditorApplication.Exit(1);}
  }
 }
}

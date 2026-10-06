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
 [InitializeOnLoad] public static class MotionContinuityFilm
 {
  [Serializable] class Frame { public string file,kind,stage;public int frame;public float time;public Vector3 root,left,right,ball; }
  [Serializable] class Report { public bool passed,synthetic=true,physicalAndroid=false;public int fps,scenes,frames;public string limitation="Offline native pose/render sequence sampled at the stated rate, not a device performance test. Constructed run/brake/pivot trajectories and incoming ball approach; Core.Control creates the reception event, then its saved timer and ControlledBallHeight advance in isolation. Every image is rendered from the current baked bones; no interpolated or AI-generated frames.";public Frame[] samples; }
  static readonly List<Frame> samples=new List<Frame>();static readonly MethodInfo control=typeof(MatchSimulation).GetMethod("Control",BindingFlags.Instance|BindingFlags.NonPublic);
  static GameObject sceneRoot,sphere;static PlayerView view;static Actor actor;static MatchState state;static MatchSimulation sim;static Camera camera;static RenderTexture target;static Material groundMaterial,lineMaterial,ballMaterial;static string output,kind;static int fps,scene,frame,last=-1;static bool controlled;
  static MotionContinuityFilm(){EditorApplication.update+=Tick;}
  public static void Run(){
   string name=Environment.GetCommandLineArgs().FirstOrDefault(a=>a.StartsWith("-touchlineFilmOutput="))?.Substring("-touchlineFilmOutput=".Length)??"motion-continuity-film-v1";
   if(!System.Text.RegularExpressions.Regex.IsMatch(name,"^[a-zA-Z0-9-]+$"))throw new Exception("Invalid output name");string rate=Environment.GetCommandLineArgs().FirstOrDefault(a=>a.StartsWith("-touchlineFilmFps="))?.Substring("-touchlineFilmFps=".Length)??"30";if(rate!="30"&&rate!="60")throw new Exception("Use30 or60fps");
   output=Path.GetFullPath("../../artifacts/unity/"+name);if(Directory.Exists(output)||File.Exists(output))throw new Exception("Preserve previous film proof");SessionState.SetString("MotionContinuityOutput",output);SessionState.SetInt("MotionContinuityFps",int.Parse(rate));ProjectBuilder.Configure();SessionState.SetBool("MotionContinuityFilm",true);EditorApplication.isPlaying=true;
  }
  static void Layer(GameObject go){go.layer=30;foreach(Transform c in go.transform)Layer(c.gameObject);}
  static void Setup(){
   TouchlineApp.Instance.GetComponent<UIDocument>().rootVisualElement.style.display=DisplayStyle.None;
   if(scene==0){output=SessionState.GetString("MotionContinuityOutput","");fps=SessionState.GetInt("MotionContinuityFps",30);if(Directory.Exists(output))throw new Exception("Proof exists");Directory.CreateDirectory(output);}
   kind=scene==0?MatchSimulation.ChestControl:MatchSimulation.ThighControl;Directory.CreateDirectory(Path.Combine(output,kind));sceneRoot=new GameObject("Native continuity diagnostic film");
   var player=new PlayerData{id="film-native",heightCm=182,rating=75};var playerObject=new GameObject("Player");playerObject.transform.SetParent(sceneRoot.transform);view=playerObject.AddComponent<PlayerView>();view.Build(player,0,9,new Color(.08f,.45f,.76f));actor=new Actor{id=player.id,slot=9,action="run",angle=0};
   state=new MatchState{home="h",away="a",restart=0,phase="play",engineVersion=4,homeTactic=new Tactic(),awayTactic=new Tactic(),actors=new[]{actor},ball=new BallState{kind="pass"}};sim=new MatchSimulation(new Database{players=new[]{player}},state);
   groundMaterial=PlayerView.Material(new Color(.16f,.29f,.13f));lineMaterial=PlayerView.Material(new Color(.6f,.68f,.57f));ballMaterial=PlayerView.Material(Color.white);
   var ground=GameObject.CreatePrimitive(PrimitiveType.Plane);ground.transform.SetParent(sceneRoot.transform);ground.transform.localScale=Vector3.one*3;ground.GetComponent<Renderer>().sharedMaterial=groundMaterial;
   for(int i=-4;i<=8;i+=2){var line=GameObject.CreatePrimitive(PrimitiveType.Cube);line.transform.SetParent(sceneRoot.transform);line.transform.position=new Vector3(i,.005f,0);line.transform.localScale=new Vector3(.025f,.005f,14);line.GetComponent<Renderer>().sharedMaterial=lineMaterial;}
   sphere=GameObject.CreatePrimitive(PrimitiveType.Sphere);sphere.transform.SetParent(sceneRoot.transform);sphere.transform.localScale=Vector3.one*.22f;sphere.GetComponent<Renderer>().sharedMaterial=ballMaterial;sphere.SetActive(false);
   camera=new GameObject("Continuity camera").AddComponent<Camera>();camera.transform.SetParent(sceneRoot.transform);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.05f,.08f,.10f);camera.cullingMask=1<<30;camera.fieldOfView=38;camera.enabled=false;
   var light=new GameObject("Film light").AddComponent<Light>();light.transform.SetParent(sceneRoot.transform);light.type=LightType.Directional;light.intensity=1.2f;light.transform.rotation=Quaternion.Euler(45,-25,0);light.cullingMask=1<<30;Layer(sceneRoot);
   target=new RenderTexture(1280,720,24){antiAliasing=2};target.Create();camera.targetTexture=target;camera.aspect=16f/9;controlled=false;
  }
  static string Trajectory(float t){
   float x=0,z=0,speed=0,angle=0;string stage;
   if(t<1.2f){speed=3.5f;z=-3+3.5f*t;stage="course";}
   else if(t<1.8f){float u=t-1.2f;speed=3.5f*(1-u/.6f);z=1.2f+3.5f*u-3.5f/(2*.6f)*u*u;stage="freinage";}
   else if(t<2.7f){z=2.25f;stage="derniers appuis";}
   else if(t<3.5f){z=2.25f;angle=Mathf.Min(Mathf.PI*.5f,(t-2.7f)*5);stage="pivot";}
   else {
    z=2.25f;angle=Mathf.PI*.5f;float u=t-3.5f;
    if(u<.7f){speed=5*u;x=2.5f*u*u;stage="reprise";}
    else if(u<.9f){speed=3.5f;x=1.225f+3.5f*(u-.7f);stage="approche";}
    else if(u<1.5f){float v=u-.9f;speed=3.5f*(1-v/.6f);x=1.925f+3.5f*v-3.5f/(2*.6f)*v*v;stage="preparation du controle";}
    else{x=2.975f;stage=controlled&&t<5+MatchSimulation.BodyControlDuration(kind)?"controle":"recuperation";}
   }
   actor.previous=actor.position;actor.position=new Point(x,z);actor.angle=angle;actor.velocity=new Point(Mathf.Sin(angle)*Mathf.Max(0,speed),Mathf.Cos(angle)*Mathf.Max(0,speed));return stage;
  }
  static void Capture(string name){
   var previous=RenderTexture.active;Texture2D image=null;var skins=new List<SkinnedMeshRenderer>();var proxies=new List<GameObject>();var meshes=new List<Mesh>();
   try{
    foreach(var skin in view.GetComponentsInChildren<SkinnedMeshRenderer>()){if(!skin.enabled)continue;var mesh=new Mesh();skin.BakeMesh(mesh);meshes.Add(mesh);var proxy=new GameObject("Current film pose skin");proxy.layer=skin.gameObject.layer;proxy.transform.SetParent(skin.transform,false);proxy.AddComponent<MeshFilter>().sharedMesh=mesh;proxy.AddComponent<MeshRenderer>().sharedMaterials=skin.sharedMaterials;proxies.Add(proxy);skins.Add(skin);skin.enabled=false;}
    camera.Render();RenderTexture.active=target;image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes(Path.Combine(output,name),image.EncodeToPNG());
   }finally{foreach(var skin in skins)skin.enabled=true;foreach(var proxy in proxies)UnityEngine.Object.DestroyImmediate(proxy);foreach(var mesh in meshes)UnityEngine.Object.DestroyImmediate(mesh);if(image!=null)UnityEngine.Object.DestroyImmediate(image);RenderTexture.active=previous;}
  }
  static void Tick(){
   if(!SessionState.GetBool("MotionContinuityFilm",false)||!EditorApplication.isPlaying||TouchlineApp.Instance==null||last==Time.frameCount)return;last=Time.frameCount;
   try{
    if(view==null){Setup();return;}float time=(float)frame/fps,dt=1f/fps;string stage=Trajectory(time);float height=kind==MatchSimulation.ChestControl?1.35f:.95f;var impact=new Point(3.275f,2.02f);
    if(time>=4.4f){sphere.SetActive(true);float approach=Mathf.Clamp01((time-4.4f)/.6f);sphere.transform.position=new Vector3(Mathf.Lerp(8,impact.x,approach),Mathf.Lerp(1.8f,height,approach)+Mathf.Sin(approach*Mathf.PI)*.2f,impact.z);}
    if(time>=5&&!controlled){state.ball.position=impact;state.ball.height=height;control.Invoke(sim,new object[]{actor});if(actor.actionKind!=kind)throw new Exception("Wrong Core control event");controlled=true;}
    if(controlled){float elapsed=time-5,duration=MatchSimulation.BodyControlDuration(kind);stage=elapsed<duration?"controle":"recuperation";actor.actionTime=Mathf.Max(0,duration-elapsed);state.ball.controlElapsed=elapsed;if(elapsed>=duration){actor.action="idle";actor.actionTime=0;}state.ball.height=MatchSimulation.ControlledBallHeight(actor,state.ball);sphere.transform.position=new Vector3(impact.x,state.ball.height,impact.z);}
    string actorBefore=JsonUtility.ToJson(actor);view.Render(actor,1,frame==0?0:dt,sphere.activeSelf?sphere.transform.position:new Vector3(0,.11f,6),new PlayerMotionContext{carrying=controlled,hasSimulationClock=true,simulationClock=(float)Math.Floor(time*10+.0001f)/10});
    if(actorBefore!=JsonUtility.ToJson(actor)||Vector3.Distance(view.transform.position,new Vector3(actor.position.x,0,actor.position.z))>.0001f)throw new Exception("Renderer altered authoritative actor");
    var desired=view.transform.position+new Vector3(4,2.6f,4);camera.transform.position=frame==0?desired:Vector3.Lerp(camera.transform.position,desired,1-Mathf.Exp(-dt*7));camera.transform.LookAt(view.transform.position+Vector3.up*.95f);
    string file=kind+"/frame-"+frame.ToString("D4")+".png";Capture(file);samples.Add(new Frame{file=file,kind=kind,stage=stage,frame=frame,time=time,root=view.transform.position,left=view.FootPosition(true),right=view.FootPosition(false),ball=sphere.transform.position});
    frame++;if(time<7)return;UnityEngine.Object.DestroyImmediate(sceneRoot);UnityEngine.Object.DestroyImmediate(groundMaterial);UnityEngine.Object.DestroyImmediate(lineMaterial);UnityEngine.Object.DestroyImmediate(ballMaterial);target.Release();UnityEngine.Object.DestroyImmediate(target);view=null;frame=0;scene++;
    if(scene==2){File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(new Report{passed=true,fps=fps,scenes=scene,frames=samples.Count,samples=samples.ToArray()},true));SessionState.SetBool("MotionContinuityFilm",false);Debug.Log("TOUCHLINE_MOTION_CONTINUITY_FILM_OK");EditorApplication.Exit(0);}
   }catch(Exception e){SessionState.SetBool("MotionContinuityFilm",false);Debug.LogException(e);EditorApplication.Exit(1);}
  }
 }
}

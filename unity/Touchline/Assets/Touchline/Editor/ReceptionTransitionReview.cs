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
 [InitializeOnLoad] public static class ReceptionTransitionReview
 {
  [Serializable] class Sample { public string file,kind,stage;public int fps;public float elapsed,leftEntryDelta,rightEntryDelta,elbowEntryDelta,contactDistance;public Vector3 left,right,ball,root; }
  [Serializable] class Report { public bool passed,synthetic=true,physicalAndroid=false;public string limitation="Core.Control produces the actual reception event after a constructed run. Recovery then advances its recorded action timer and Core.ControlledBallHeight in isolation; this is not a full match. Capture intervals are split at exact contact boundaries. Skin snapshots are baked from the measured bones.";public Sample[] samples; }
  static readonly List<Sample> samples=new List<Sample>();static readonly MethodInfo control=typeof(MatchSimulation).GetMethod("Control",BindingFlags.Instance|BindingFlags.NonPublic);
  static int last=-1;static string output;
  static ReceptionTransitionReview(){EditorApplication.update+=Tick;}
  public static void Run(){
   string name=Environment.GetCommandLineArgs().FirstOrDefault(a=>a.StartsWith("-touchlineReceptionOutput="))?.Substring("-touchlineReceptionOutput=".Length)??"reception-transition-review-v1";
   if(!System.Text.RegularExpressions.Regex.IsMatch(name,"^[a-zA-Z0-9-]+$"))throw new Exception("Invalid output name");output=Path.GetFullPath("../../artifacts/unity/"+name);if(Directory.Exists(output)||File.Exists(output))throw new Exception("Preserve existing proof");SessionState.SetString("ReceptionTransitionOutput",output);ProjectBuilder.Configure();SessionState.SetBool("ReceptionTransitionReview",true);EditorApplication.isPlaying=true;
  }
  static void Layer(GameObject go){go.layer=30;foreach(Transform c in go.transform)Layer(c.gameObject);}
  static void Capture(PlayerView view,Camera camera,RenderTexture target,string name){
   var previous=RenderTexture.active;Texture2D image=null;var skins=new List<SkinnedMeshRenderer>();var proxies=new List<GameObject>();var meshes=new List<Mesh>();
   try{
    foreach(var skin in view.GetComponentsInChildren<SkinnedMeshRenderer>()){if(!skin.enabled)continue;var mesh=new Mesh();skin.BakeMesh(mesh);meshes.Add(mesh);var proxy=new GameObject("Current reception proof skin");proxy.layer=skin.gameObject.layer;proxy.transform.SetParent(skin.transform,false);proxy.AddComponent<MeshFilter>().sharedMesh=mesh;proxy.AddComponent<MeshRenderer>().sharedMaterials=skin.sharedMaterials;proxies.Add(proxy);skins.Add(skin);skin.enabled=false;}
    camera.Render();RenderTexture.active=target;image=new Texture2D(960,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,960,720),0,0);image.Apply();File.WriteAllBytes(Path.Combine(output,name),image.EncodeToPNG());
   }finally{foreach(var skin in skins)skin.enabled=true;foreach(var proxy in proxies)UnityEngine.Object.DestroyImmediate(proxy);foreach(var mesh in meshes)UnityEngine.Object.DestroyImmediate(mesh);if(image!=null)UnityEngine.Object.DestroyImmediate(image);RenderTexture.active=previous;}
  }
  static void Scene(string kind,int fps){
   GameObject scene=null;RenderTexture target=null;Material groundMaterial=null,ballMaterial=null;
   try{
    scene=new GameObject("Core reception synthetic scene");var player=new PlayerData{id="reception-native",heightCm=182,rating=75};var playerObject=new GameObject("Player");playerObject.transform.SetParent(scene.transform);var view=playerObject.AddComponent<PlayerView>();view.Build(player,0,9,new Color(.08f,.45f,.76f));
    var actor=new Actor{id=player.id,slot=9,action="run",angle=0,velocity=new Point(0,3.5f)};
    for(int frame=0;frame<fps;frame++){actor.previous=actor.position;actor.position+=actor.velocity/fps;view.Render(actor,1,1f/fps,new Vector3(.23f,1.2f,actor.position.z+.3f));}
    var before=new[]{view.FootPosition(true),view.FootPosition(false)};var elbow=playerObject.GetComponentsInChildren<Transform>().First(t=>t.name=="lowerarm01.L");var elbowBefore=elbow.localRotation;
    float height=kind==MatchSimulation.ChestControl?1.35f:.95f;var impact=new Point(actor.position.x+.23f,actor.position.z+.30f);var ball=new BallState{kind="pass",position=impact,height=height};
    var state=new MatchState{home="h",away="a",restart=0,phase="play",engineVersion=4,homeTactic=new Tactic(),awayTactic=new Tactic(),actors=new[]{actor},ball=ball};var sim=new MatchSimulation(new Database{players=new[]{player}},state);
    var ground=GameObject.CreatePrimitive(PrimitiveType.Plane);ground.transform.SetParent(scene.transform);ground.transform.localScale=Vector3.one*2;groundMaterial=PlayerView.Material(new Color(.16f,.28f,.13f));ground.GetComponent<Renderer>().sharedMaterial=groundMaterial;
    var sphere=GameObject.CreatePrimitive(PrimitiveType.Sphere);sphere.transform.SetParent(scene.transform);sphere.transform.localScale=Vector3.one*.22f;ballMaterial=PlayerView.Material(Color.white);sphere.GetComponent<Renderer>().sharedMaterial=ballMaterial;
    var camera=new GameObject("Reception camera").AddComponent<Camera>();camera.transform.SetParent(scene.transform);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.05f,.08f,.10f);camera.cullingMask=1<<30;camera.fieldOfView=38;camera.enabled=false;
    camera.transform.position=view.transform.position+new Vector3(4,2.2f,4);camera.transform.LookAt(view.transform.position+Vector3.up*.95f);
    var light=new GameObject("Reception light").AddComponent<Light>();light.transform.SetParent(scene.transform);light.type=LightType.Directional;light.intensity=1.2f;light.transform.rotation=Quaternion.Euler(45,-25,0);light.cullingMask=1<<30;Layer(scene);
    target=new RenderTexture(960,720,24){antiAliasing=2};target.Create();camera.targetTexture=target;camera.aspect=4f/3;
    sphere.transform.position=new Vector3(impact.x,height,impact.z);string prefix=kind+"-"+fps+"fps-";Capture(view,camera,target,prefix+"outgoing.png");
    samples.Add(new Sample{file=prefix+"outgoing.png",kind=kind,stage="outgoing",fps=fps,elapsed=-1,left=before[0],right=before[1],ball=sphere.transform.position,root=view.transform.position});
    control.Invoke(sim,new object[]{actor});if(actor.actionKind!=kind)throw new Exception("Core returned wrong reception kind");
    float duration=MatchSimulation.BodyControlDuration(kind),elapsed=0;
    foreach(float requested in new[]{0f,.04f,.08f,.16f,.32f,.46f}){
     do{
      float dt=Mathf.Min(1f/fps,Mathf.Max(0,requested-elapsed));elapsed+=dt;actor.actionTime=duration-elapsed;ball.controlElapsed=elapsed;ball.height=MatchSimulation.ControlledBallHeight(actor,ball);sphere.transform.position=new Vector3(impact.x,ball.height,impact.z);
      view.Render(actor,1,requested==0?1f/fps:dt,sphere.transform.position);
      if(Vector3.Distance(view.transform.position,new Vector3(actor.position.x,0,actor.position.z))>.0001f)throw new Exception("Presentation moved Core root");
     }while(elapsed<requested-.000001f);
     string name=prefix+(requested*1000).ToString("000")+"ms.png";Capture(view,camera,target,name);
     bool left=(bool)typeof(PlayerView).GetField("receivingLeft",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(view);var contact=kind==MatchSimulation.ChestControl?view.ChestContactPosition:view.ThighContactPosition(left);
     samples.Add(new Sample{file=name,kind=kind,stage="reception",fps=fps,elapsed=elapsed,left=view.FootPosition(true),right=view.FootPosition(false),ball=sphere.transform.position,root=view.transform.position,leftEntryDelta=requested==0?Vector3.Distance(before[0],view.FootPosition(true)):0,rightEntryDelta=requested==0?Vector3.Distance(before[1],view.FootPosition(false)):0,elbowEntryDelta=requested==0?Quaternion.Angle(elbowBefore,elbow.localRotation):0,contactDistance=Vector3.Distance(contact,sphere.transform.position)});
    }
   }finally{if(scene!=null)UnityEngine.Object.DestroyImmediate(scene);if(target!=null){target.Release();UnityEngine.Object.DestroyImmediate(target);}if(groundMaterial!=null)UnityEngine.Object.DestroyImmediate(groundMaterial);if(ballMaterial!=null)UnityEngine.Object.DestroyImmediate(ballMaterial);}
  }
  static void Tick(){
   if(!SessionState.GetBool("ReceptionTransitionReview",false)||!EditorApplication.isPlaying||TouchlineApp.Instance==null||last==Time.frameCount)return;last=Time.frameCount;
   try{
    output=SessionState.GetString("ReceptionTransitionOutput","");if(Directory.Exists(output))throw new Exception("Proof exists");Directory.CreateDirectory(output);TouchlineApp.Instance.GetComponent<UIDocument>().rootVisualElement.style.display=DisplayStyle.None;
    foreach(string kind in new[]{MatchSimulation.ChestControl,MatchSimulation.ThighControl})foreach(int fps in new[]{30,60,120})Scene(kind,fps);
    File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(new Report{passed=true,samples=samples.ToArray()},true));SessionState.SetBool("ReceptionTransitionReview",false);Debug.Log("TOUCHLINE_RECEPTION_TRANSITION_REVIEW_OK");EditorApplication.Exit(0);
   }catch(Exception e){SessionState.SetBool("ReceptionTransitionReview",false);Debug.LogException(e);EditorApplication.Exit(1);}
  }
 }
}

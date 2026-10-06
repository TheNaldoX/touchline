using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Touchline.Core;
namespace Touchline.Editor
{
 public static class ReceptionCancellationReview
 {
  [Serializable]class Sample{public int fps,frame;public float time;public Vector3 left,right;public string file;}
  [Serializable]class Report{public bool passed,synthetic=true,physicalAndroid=false;public string limitation="Native diagnostic of a prepared thigh followed by a synthetic shoulder-contact context. Not a Core-simulated duel or real-world footage. Exact baked current bones, no generated frames.";public Sample[] samples;}
  static void Capture(Camera camera,RenderTexture target,PlayerView view,string path){
   var skins=new List<SkinnedMeshRenderer>();var proxies=new List<GameObject>();var meshes=new List<Mesh>();var previous=RenderTexture.active;Texture2D image=null;
   try{foreach(var skin in view.GetComponentsInChildren<SkinnedMeshRenderer>()){if(!skin.enabled)continue;var mesh=new Mesh();skin.BakeMesh(mesh);meshes.Add(mesh);var proxy=new GameObject("Current cancellation skin");proxy.transform.SetParent(skin.transform,false);proxy.AddComponent<MeshFilter>().sharedMesh=mesh;proxy.AddComponent<MeshRenderer>().sharedMaterials=skin.sharedMaterials;proxies.Add(proxy);skins.Add(skin);skin.enabled=false;}
    camera.Render();RenderTexture.active=target;image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());
   }finally{foreach(var skin in skins)skin.enabled=true;foreach(var proxy in proxies)UnityEngine.Object.DestroyImmediate(proxy);foreach(var mesh in meshes)UnityEngine.Object.DestroyImmediate(mesh);if(image!=null)UnityEngine.Object.DestroyImmediate(image);RenderTexture.active=previous;}
  }
  public static void Run(){try{
   string name=Environment.GetCommandLineArgs().FirstOrDefault(a=>a.StartsWith("-touchlineCancelOutput="))?.Substring("-touchlineCancelOutput=".Length)??"reception-cancellation-native-v1";
   if(!System.Text.RegularExpressions.Regex.IsMatch(name,"^[a-zA-Z0-9-]+$"))throw new Exception("Invalid output name");
   var output=Path.GetFullPath("../../artifacts/unity/"+name);if(Directory.Exists(output)||File.Exists(output))throw new Exception("Preserve existing proof");Directory.CreateDirectory(output);var samples=new List<Sample>();
   foreach(int fps in new[]{30,60}){
    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.5f,.5f,.5f);
    var root=new GameObject("Synthetic cancellation review");var view=new GameObject("Receiver").AddComponent<PlayerView>();view.transform.SetParent(root.transform);var data=new PlayerData{id="r",heightCm=182,rating=75};view.Build(data,0,9,new Color(.08f,.40f,.8f));
    var ground=GameObject.CreatePrimitive(PrimitiveType.Plane);ground.transform.SetParent(root.transform);var groundMaterial=PlayerView.Material(new Color(.14f,.28f,.16f));ground.GetComponent<Renderer>().sharedMaterial=groundMaterial;
    var light=new GameObject("Review sunlight").AddComponent<Light>();light.transform.SetParent(root.transform);light.type=LightType.Directional;light.intensity=1.1f;light.transform.rotation=Quaternion.Euler(40,-30,0);
    var camera=new GameObject("Cancellation side camera").AddComponent<Camera>();camera.transform.SetParent(root.transform);camera.enabled=false;camera.fieldOfView=35;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.05f,.08f,.10f);camera.transform.position=new Vector3(3,1.9f,3);camera.transform.LookAt(new Vector3(0,.95f,0));camera.aspect=4f/3;
    var target=new RenderTexture(800,600,24){antiAliasing=2};target.Create();camera.targetTexture=target;
    var actor=new Actor{id="r",slot=9,side=0,action="idle",angle=0};var m=new MatchState{phase="play",restart=0,actors=new[]{actor},ball=new BallState{kind="pass",from="s",to="r",side=0,start=new Point(.23f,2.8f),end=new Point(.23f,.3f),startHeight=.95f,endHeight=.95f,height=.95f,duration=1}};
    view.Render(actor,1,0);for(int f=1;f<=Mathf.CeilToInt(.36f*fps);f++){m.ball.elapsed=.60f+Mathf.Min(.36f,(float)f/fps);m.ball.position=Point.Lerp(m.ball.start,m.ball.end,m.ball.elapsed);view.Render(actor,1,1f/fps,new Vector3(m.ball.position.x,.95f,m.ball.position.z),PlayerMotionContext.From(m,actor,1,1.82f));}
    string prepared=$"{fps}-prepared.png";Capture(camera,target,view,Path.Combine(output,prepared));samples.Add(new Sample{fps=fps,frame=-1,time=-1f/fps,left=view.FootPosition(true),right=view.FootPosition(false),file=prepared});
    m.ball.kind="loose";m.ball.to=null;m.actors=new[]{actor,new Actor{id="opponent",side=1,slot=5,position=new Point(0,.8f),velocity=new Point(0,-2)}};
    var context=PlayerMotionContext.From(m,actor,1,1.82f);var captureFrames=new HashSet<int>{0,1,Mathf.RoundToInt(.0666667f*fps),Mathf.RoundToInt(.1333333f*fps),Mathf.RoundToInt(.2f*fps),Mathf.CeilToInt(.35f*fps)};
    for(int f=0;f<=Mathf.CeilToInt(.35f*fps);f++){string before=JsonUtility.ToJson(m);view.Render(actor,1,1f/fps,new Vector3(.23f,.95f,.3f),context);if(before!=JsonUtility.ToJson(m))throw new Exception("Renderer changed state");if(!captureFrames.Contains(f))continue;
     string file=$"{fps}-{f:D3}.png";Capture(camera,target,view,Path.Combine(output,file));samples.Add(new Sample{fps=fps,frame=f,time=(float)f/fps,left=view.FootPosition(true),right=view.FootPosition(false),file=file});
    }
    camera.targetTexture=null;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(groundMaterial);
   }
   File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(new Report{passed=true,samples=samples.ToArray()},true));Debug.Log("TOUCHLINE_RECEPTION_CANCELLATION_REVIEW_OK");EditorApplication.Exit(0);
  }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
 }
}

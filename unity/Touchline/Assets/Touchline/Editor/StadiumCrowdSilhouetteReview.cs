using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace Touchline.Editor
{
 public static class StadiumCrowdSilhouetteReview
 {
  [Serializable] class Geometry {public int vertices,triangles,submeshes;public string indexFormat;public Vector3 bounds;}
  [Serializable] class View {public string name,before,after;public Vector3 camera,target;}
  [Serializable] class Report {public bool passed,physicalAndroid=false;public string limitation="Static native comparison with identical occupancy, deterministic seed, camera, lighting and seven opaque materials. No physical-device FPS/thermal claim. Front two rows use more detailed silhouettes; further rows use opaque two-sided cards.";public Geometry baseline,candidate,fullBaseline,fullCandidate;public View[] views;}
  static Geometry Measure(Mesh m)=>new Geometry{vertices=m.vertexCount,triangles=m.triangles.Length/3,submeshes=m.subMeshCount,indexFormat=m.indexFormat.ToString(),bounds=m.bounds.size};
  static MeshRenderer ObjectFor(GameObject root,Mesh mesh,Material[] materials,string name){var go=new GameObject(name);go.transform.SetParent(root.transform,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterials=materials;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;return renderer;}
  static void Capture(Camera c,RenderTexture rt,string file){var old=RenderTexture.active;Texture2D image=null;try{c.Render();RenderTexture.active=rt;image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();File.WriteAllBytes(file,image.EncodeToPNG());}finally{RenderTexture.active=old;if(image!=null)UnityEngine.Object.DestroyImmediate(image);}}
  public static void Run(){try{
   var argument=Environment.GetCommandLineArgs().FirstOrDefault(x=>x.StartsWith("-touchlineCrowdSilhouetteOutput="));string name=argument==null?"stadium-crowd-silhouettes-v1":argument.Substring(argument.IndexOf('=')+1);if(!System.Text.RegularExpressions.Regex.IsMatch(name,"^[a-zA-Z0-9-]+$"))throw new Exception("Invalid output name");string output=Path.GetFullPath("../../artifacts/unity/"+name);if(Directory.Exists(output))throw new Exception("Preserve previous proofs");Directory.CreateDirectory(output);
   EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var root=new GameObject("Batched crowd native comparison");RenderSettings.fog=false;RenderSettings.ambientLight=new Color(.48f,.57f,.67f);var sun=new GameObject("Comparable stadium sun").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.5f;sun.color=new Color(1,.94f,.84f);sun.transform.rotation=Quaternion.Euler(48,-35,0);
   var materials=StadiumAtmosphere.Palette(new Color(.16f,.4f,.58f),new Color(.68f,.18f,.12f)).Select(PlayerView.Material).ToArray();var concrete=PlayerView.Material(new Color(.18f,.23f,.28f));var seats=PlayerView.Material(new Color(.23f,.31f,.36f));var white=PlayerView.Material(new Color(.88f,.89f,.81f));
   for(int side=-1;side<=1;side+=2){ObjectFor(root,StadiumGeometry.Stand(side,false),new[]{concrete},"Concrete stand");ObjectFor(root,StadiumGeometry.Stand(side,true),new[]{seats},"Seats");ObjectFor(root,StadiumGeometry.EndStand(side),new[]{seats},"End stand");ObjectFor(root,StadiumGeometry.GoalNet(side),new[]{white},"Net");}
   var baseline=StadiumAtmosphereBaseline.Crowd("176","160");var candidate=StadiumAtmosphere.Crowd("176","160");var oldRenderer=ObjectFor(root,baseline,materials,"Baseline crowd");var newRenderer=ObjectFor(root,candidate,materials,"Candidate crowd");var fullOld=StadiumAtmosphereBaseline.Crowd("176","160",1);var fullNew=StadiumAtmosphere.Crowd("176","160",1);
   var rt=new RenderTexture(960,640,24){antiAliasing=2};rt.Create();var camera=new GameObject("Crowd comparison camera").AddComponent<Camera>();camera.enabled=false;camera.targetTexture=rt;camera.aspect=1.5f;camera.fieldOfView=43;camera.nearClipPlane=.1f;camera.farClipPlane=250;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.35f,.43f,.5f);
   var views=new[]{new View{name="end-stand-from-field",camera=new Vector3(44,3.8f,0),target=new Vector3(60,3.6f,0)},new View{name="first-rows-close",camera=new Vector3(55,1.6f,-10),target=new Vector3(60.4f,1.2f,-10)},new View{name="side-stand",camera=new Vector3(5,3.2f,32),target=new Vector3(5,2.2f,41)},new View{name="broadcast-distance",camera=new Vector3(0,30,35),target=new Vector3(0,3,-44)}};
   foreach(var view in views){camera.transform.position=view.camera;camera.transform.LookAt(view.target);view.before=view.name+"-before.png";view.after=view.name+"-after.png";oldRenderer.enabled=true;newRenderer.enabled=false;Capture(camera,rt,Path.Combine(output,view.before));oldRenderer.enabled=false;newRenderer.enabled=true;Capture(camera,rt,Path.Combine(output,view.after));}
   var report=new Report{passed=fullNew.vertexCount<65535&&fullNew.triangles.Length/3<53000&&fullNew.subMeshCount==7,baseline=Measure(baseline),candidate=Measure(candidate),fullBaseline=Measure(fullOld),fullCandidate=Measure(fullNew),views=views};File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(report,true));if(!report.passed)throw new Exception("Crowd geometry budget failed");Debug.Log("TOUCHLINE_CROWD_SILHOUETTE_REVIEW_OK");EditorApplication.Exit(0);
  }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
 }
}

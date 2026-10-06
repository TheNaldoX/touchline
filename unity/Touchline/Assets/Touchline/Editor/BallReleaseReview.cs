using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Touchline.Core;
namespace Touchline.Editor {
 public static class BallReleaseReview {
  [Serializable] class Sample { public string scenario,file;public float time,alpha,releaseFraction,releaseError;public Vector3 ball,contact; }
  [Serializable] class Report {public bool passed,synthetic=true,physicalAndroid=false;public string limitation="Constructed isolated releases, current production PlayerView and Core UpdateBall; no acquired motion capture.";public Sample[] samples;}
  static MatchSimulation Create(string spec,out Actor actor) {
   bool header=spec=="header";float delay=header?.12f:.18f;
   var p=new PlayerData{id="release-review",heightCm=182,rating=75,preferredFoot="Right",positions=new[]{"ST"}};
   actor=new Actor{id=p.id,slot=9,side=0,controlTime=100,action=header?"header":"kick",actionKind=spec,actionSequence=1,actionContactTime=delay,actionTime=.64f,actionTarget=new Point(0,.42f),actionHeight=header?1.6f:.11f};
   var b=new BallState{kind=header?"shot":spec,from=p.id,setupStart=new Point(0,.3f),position=new Point(0,.3f),previous=new Point(0,.3f),start=new Point(0,.42f),end=new Point(0,10),elapsed=-delay,releaseDelay=delay,duration=.5f,startHeight=header?1.6f:.11f,setupHeight=header?1.6f:.11f,endHeight=.11f,height=header?1.6f:.11f,previousHeight=header?1.6f:.11f};
   return new MatchSimulation(new Database{players=new[]{p}},new MatchState{home="h",away="a",phase="play",restart=0,engineVersion=4,actors=new[]{actor},ball=b});
  }
  public static void Run(){try{
   var name=Environment.GetCommandLineArgs().FirstOrDefault(a=>a.StartsWith("-touchlineReleaseOutput="))?.Split('=')[1]??"ball-release-review-v1";
   if(!System.Text.RegularExpressions.Regex.IsMatch(name,"^[a-zA-Z0-9-]+$"))throw new Exception("Invalid proof name");
   var output=Path.GetFullPath("../../artifacts/unity/"+name);if(Directory.Exists(output))throw new Exception("Do not overwrite proof");Directory.CreateDirectory(output);
   EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);RenderSettings.ambientLight=new Color(.65f,.65f,.65f);
   var light=new GameObject("Light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.5f;light.transform.rotation=Quaternion.Euler(35,-25,0);
   var floor=GameObject.CreatePrimitive(PrimitiveType.Plane);floor.GetComponent<Renderer>().sharedMaterial=PlayerView.Material(new Color(.10f,.24f,.15f));
   var camera=new GameObject("Release camera").AddComponent<Camera>();camera.enabled=false;camera.transform.position=new Vector3(3,2.1f,4);camera.transform.LookAt(new Vector3(0,.85f,.4f));camera.fieldOfView=38;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.05f,.07f,.09f);
   var target=new RenderTexture(1000,800,24){antiAliasing=2};target.Create();camera.targetTexture=target;camera.aspect=1.25f;
   var ball=GameObject.CreatePrimitive(PrimitiveType.Sphere);ball.transform.localScale=Vector3.one*.22f;ball.GetComponent<Renderer>().sharedMaterial=PlayerView.Material(Color.white);
   var update=typeof(MatchSimulation).GetMethod("UpdateBall",BindingFlags.NonPublic|BindingFlags.Instance);
   var capture=typeof(KeeperCompactReview).GetMethod("Capture",BindingFlags.NonPublic|BindingFlags.Static);
   var samples=new List<Sample>();
   foreach(var spec in new[]{"pass","shot","header"}){
    var sim=Create(spec,out var actor);var m=sim.State;var b=m.ball;var go=new GameObject(spec);var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id=actor.id,heightCm=182,preferredFoot="Right"},0,9,new Color(.08f,.44f,.77f));
    var warm=new Actor{id=actor.id,slot=9,action="idle"};view.Render(warm,1,.01f);
    for(int step=0;step<4;step++){
     b.previous=b.position;b.previousHeight=b.height;m.clock+=.1f;actor.actionTime-=.1f;update.Invoke(sim,null);
     for(int frame=0;frame<=10;frame++){
      float alpha=frame*.1f;var pos=BallReleasePresentation.Position(sim.ReleaseContact,m,alpha);view.Render(actor,alpha,frame==0?0:.01f,pos);ball.transform.position=pos;
      float time=step*.1f+alpha*.1f;
      if(!(Mathf.Abs(time-.10f)<.001f||Mathf.Abs(time-.12f)<.001f||Mathf.Abs(time-.16f)<.001f||Mathf.Abs(time-.18f)<.001f||Mathf.Abs(time-.22f)<.001f||Mathf.Abs(time-.3f)<.001f))continue;
      string file=spec+"-"+Mathf.RoundToInt(time*1000).ToString("D3")+".png";if(File.Exists(Path.Combine(output,file)))continue;
      capture.Invoke(null,new object[]{camera,target,view,Path.Combine(output,file)});
      var contact=spec=="header"?view.HeaderContactPosition:spec=="pass"?view.InsideFootContactPosition(false):view.BootContactPosition(false);
      float error=Vector3.Distance(contact,pos);samples.Add(new Sample{scenario=spec,file=file,time=time,alpha=alpha,releaseFraction=sim.ReleaseContact.fraction,releaseError=error,ball=pos,contact=contact});
      if(Mathf.Abs(time-actor.actionContactTime)<.001f&&error>.15f)throw new Exception(spec+" contact misses release by "+error);
     }
    }
    UnityEngine.Object.DestroyImmediate(go);
   }
   File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(new Report{passed=true,samples=samples.ToArray()},true));Debug.Log("TOUCHLINE_BALL_RELEASE_REVIEW_OK");EditorApplication.Exit(0);
  }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
 }
}

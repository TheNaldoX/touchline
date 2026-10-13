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
  static GameObject sceneRoot,sphere;static PlayerView view;static Actor actor;static MatchState state;static MatchSimulation sim;static Camera camera;static RenderTexture target;static Material groundMaterial,lineMaterial,ballMaterial;static string output,kind;static int fps,scene,frame,last=-1;static bool controlled,slides,signals,feet,falls,braces,celebrations,headers,withdrawals;
  static MotionContinuityFilm(){EditorApplication.update+=Tick;}
  public static void Run(){
   string name=Environment.GetCommandLineArgs().FirstOrDefault(a=>a.StartsWith("-touchlineFilmOutput="))?.Substring("-touchlineFilmOutput=".Length)??"motion-continuity-film-v1";
   if(!System.Text.RegularExpressions.Regex.IsMatch(name,"^[a-zA-Z0-9-]+$"))throw new Exception("Invalid output name");string rate=Environment.GetCommandLineArgs().FirstOrDefault(a=>a.StartsWith("-touchlineFilmFps="))?.Substring("-touchlineFilmFps=".Length)??"30";if(rate!="30"&&rate!="60")throw new Exception("Use30 or60fps");
   output=Path.GetFullPath("../../artifacts/unity/"+name);if(Directory.Exists(output)||File.Exists(output))throw new Exception("Preserve previous film proof");SessionState.SetString("MotionContinuityOutput",output);SessionState.SetInt("MotionContinuityFps",int.Parse(rate));SessionState.SetBool("MotionContinuitySlides",Environment.GetCommandLineArgs().Contains("-touchlineFilmMotion=slide"));SessionState.SetBool("MotionContinuitySignals",Environment.GetCommandLineArgs().Contains("-touchlineFilmMotion=signals"));SessionState.SetBool("MotionContinuityFeet",Environment.GetCommandLineArgs().Contains("-touchlineFilmMotion=foot"));SessionState.SetBool("MotionContinuityFalls",Environment.GetCommandLineArgs().Contains("-touchlineFilmMotion=fall"));SessionState.SetBool("MotionContinuityBraces",Environment.GetCommandLineArgs().Contains("-touchlineFilmMotion=brace"));SessionState.SetBool("MotionContinuityCelebrations",Environment.GetCommandLineArgs().Contains("-touchlineFilmMotion=celebration"));SessionState.SetBool("MotionContinuityHeaders",Environment.GetCommandLineArgs().Contains("-touchlineFilmMotion=header"));SessionState.SetBool("MotionContinuityWithdrawals",Environment.GetCommandLineArgs().Contains("-touchlineFilmMotion=withdraw"));ProjectBuilder.Configure();SessionState.SetBool("MotionContinuityFilm",true);EditorApplication.isPlaying=true;
  }
  static void Layer(GameObject go){go.layer=30;foreach(Transform c in go.transform)Layer(c.gameObject);}
  static void Setup(){
   TouchlineApp.Instance.GetComponent<UIDocument>().rootVisualElement.style.display=DisplayStyle.None;
   if(scene==0){output=SessionState.GetString("MotionContinuityOutput","");fps=SessionState.GetInt("MotionContinuityFps",30);if(Directory.Exists(output))throw new Exception("Proof exists");Directory.CreateDirectory(output);}
   slides=SessionState.GetBool("MotionContinuitySlides",false);signals=SessionState.GetBool("MotionContinuitySignals",false);feet=SessionState.GetBool("MotionContinuityFeet",false);falls=SessionState.GetBool("MotionContinuityFalls",false);braces=SessionState.GetBool("MotionContinuityBraces",false);celebrations=SessionState.GetBool("MotionContinuityCelebrations",false);headers=SessionState.GetBool("MotionContinuityHeaders",false);withdrawals=SessionState.GetBool("MotionContinuityWithdrawals",false);kind=withdrawals?(scene==0?"withdraw-left":"withdraw-right"):headers?(scene==0?"header-30-start":"header-30-recovery"):celebrations?(scene==0?"goal-standing":"goal-turning"):braces?(scene==0?"brace-left":"brace-right"):falls?(scene==0?"fall-left":"fall-right"):feet?(scene==0?"foot-left":"foot-right"):signals?(scene==0?"request-ball":"applause"):slides?(scene==0?"slide-left":"slide-right"):(scene==0?MatchSimulation.ChestControl:MatchSimulation.ThighControl);Directory.CreateDirectory(Path.Combine(output,kind));sceneRoot=new GameObject("Native continuity diagnostic film");
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
   if(withdrawals)return WithdrawalTrajectory(t);
   if(headers)return HeaderTrajectory(t);
   if(falls){float elapsed=t-1;actor.position=actor.previous=new Point();actor.velocity=new Point();actor.action=elapsed>=0&&elapsed<MatchSimulation.ContactFallDuration?"fall":"idle";actor.actionKind="tackled-fall";actor.actionSequence=1;actor.actionTime=Mathf.Clamp(MatchSimulation.ContactFallDuration-elapsed,0,MatchSimulation.ContactFallDuration);actor.diveSide=scene==0?1:-1;return elapsed<0?"repos":actor.action=="fall"?"chute":"releve";}
   if(celebrations)actor.actionSequence=scene;
   if(signals||braces||celebrations){actor.action="idle";actor.position=actor.previous=new Point();actor.velocity=new Point();return t<1?"repos":t<4?"signal":"retour au repos";}
   if(slides)return SlideTrajectory(t);
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
  static string WithdrawalTrajectory(float time){
   float elapsed=time-1;actor.position=actor.previous=new Point();actor.velocity=new Point();
   actor.actionKind=MatchSimulation.StandingDuel;actor.actionSequence=1;actor.actionTarget=new Point((scene==0?1:-1)*.85f,.42f);actor.actionHeight=.11f;actor.actionContactTime=.2f;
   if(elapsed<0){actor.action="idle";return "repos";}
   if(elapsed<.1f){actor.action="tackle";actor.actionTime=.75f-elapsed;return "preparation";}
   actor.tackleWithdrawFrom=.65f;actor.actionTime=Mathf.Max(0,MatchSimulation.TackleRecovery*.35f-(elapsed-.1f));actor.action=actor.actionTime>0?"tackle":"idle";
   return actor.action=="tackle"?"retrait":"retour appuis";
  }
  static string HeaderTrajectory(float time){
   if(time<1){actor.previous=actor.position;actor.position=new Point(0,-3.5f+time*3.5f);actor.velocity=new Point(0,3.5f);actor.action="run";return "course";}
   if(!controlled){
    actor.previous=actor.position;actor.velocity=new Point();var impact=view.HeaderContactPosition+Vector3.up*(scene==0?.15f:.25f);
    state.ball.position=state.ball.previous=new Point(impact.x,impact.z);state.ball.height=state.ball.previousHeight=impact.y;state.ball.owner=actor.id;state.ball.kind="cross";
    typeof(MatchSimulation).GetMethod("DistributeHeader",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(sim,new object[]{actor,null});controlled=true;
   }
   actor.actionTime=Mathf.Max(0,MatchSimulation.AerialContestDuration-(time-1));if(actor.actionTime<=0)actor.action="idle";
   return time==1?"entree sans progression":actor.action=="header"?"tete":"reception au sol";
  }
  static string SlideTrajectory(float time){
   // Constructed diagnostic, using the three committed travel segments from
   // the slide regression fixture. This is not a newly simulated match.
   const float travel=.48f+.3744f+.292032f;float t=time-1,speed=0,z;string stage;
   actor.previous=actor.position;
   if(t<0){speed=4.8f;z=-4.8f+4.8f*time;actor.action="run";stage="course";}
   else if(t<MatchSimulation.SlidingDuelDuration){
    z=t<.1f?4.8f*t:t<.2f?.48f+3.744f*(t-.1f):t<.3f?.8544f+2.92032f*(t-.2f):travel;
    speed=t<.1f?4.8f:t<.2f?3.744f:t<.3f?2.92032f:0;
    actor.action="slide";actor.actionKind=MatchSimulation.SlidingDuel;actor.actionSequence=1;actor.actionTime=MatchSimulation.SlidingDuelDuration-t;actor.actionContactTime=MatchSimulation.SlidingDuelContact;actor.diveSide=scene==0?1:-1;actor.actionHeight=.11f;actor.actionTarget=new Point(actor.diveSide*.12f,travel+.14f);stage=t<.3f?"engagement":t<.78f?"contact":"releve";
   }else{float running=Mathf.Max(0,t-2);speed=Mathf.Min(4.8f,running*6);z=travel+(running<.8f?3*running*running:1.92f+(running-.8f)*4.8f);actor.action=running>0?"run":"idle";actor.actionTime=0;stage="reprise";}
   actor.position=new Point(0,z);actor.velocity=new Point(0,speed);return stage;
  }
  static void Capture(string name){
   var previous=RenderTexture.active;Texture2D image=null;var skins=new List<SkinnedMeshRenderer>();var proxies=new List<GameObject>();var meshes=new List<Mesh>();
   try{
    foreach(var skin in view.GetComponentsInChildren<SkinnedMeshRenderer>()){if(!skin.enabled)continue;var mesh=new Mesh();skin.BakeMesh(mesh,true);meshes.Add(mesh);var proxy=new GameObject("Current film pose skin");proxy.layer=skin.gameObject.layer;proxy.transform.SetParent(skin.transform,false);proxy.AddComponent<MeshFilter>().sharedMesh=mesh;proxy.AddComponent<MeshRenderer>().sharedMaterials=skin.sharedMaterials;proxies.Add(proxy);skins.Add(skin);skin.enabled=false;}
    camera.Render();RenderTexture.active=target;image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes(Path.Combine(output,name),image.EncodeToPNG());
   }finally{foreach(var skin in skins)skin.enabled=true;foreach(var proxy in proxies)UnityEngine.Object.DestroyImmediate(proxy);foreach(var mesh in meshes)UnityEngine.Object.DestroyImmediate(mesh);if(image!=null)UnityEngine.Object.DestroyImmediate(image);RenderTexture.active=previous;}
  }
  static void Tick(){
   if(!SessionState.GetBool("MotionContinuityFilm",false)||!EditorApplication.isPlaying||TouchlineApp.Instance==null||last==Time.frameCount)return;last=Time.frameCount;
   try{
    if(view==null){Setup();return;}float time=(float)frame/fps,dt=1f/fps;string stage=Trajectory(time);float height=feet?.11f:kind==MatchSimulation.ChestControl?1.35f:.95f;var impact=new Point(3.275f,feet?2.25f+(scene==0?-.23f:.23f):2.02f);
    if(slides){sphere.SetActive(true);sphere.transform.position=new Vector3((scene==0?1:-1)*.12f,.11f,.48f+.3744f+.292032f+.14f);}
    if(!slides&&!signals&&!falls&&!braces&&!celebrations&&!headers&&!withdrawals&&time>=4.4f){sphere.SetActive(true);float approach=Mathf.Clamp01((time-4.4f)/.6f);sphere.transform.position=new Vector3(Mathf.Lerp(8,impact.x,approach),Mathf.Lerp(1.8f,height,approach)+Mathf.Sin(approach*Mathf.PI)*.2f,impact.z);
     if(!controlled){var b=state.ball;b.from="incoming-pass";b.to=actor.id;b.side=actor.side;b.start=new Point(8,impact.z);b.end=impact;b.duration=.6f;b.elapsed=time-4.4f;b.startHeight=1.8f;b.endHeight=height;b.loft=.2f;b.position=new Point(sphere.transform.position.x,sphere.transform.position.z);b.height=sphere.transform.position.y;}
    }
    if(!slides&&!signals&&!falls&&!braces&&!celebrations&&!headers&&!withdrawals&&time>=5&&!controlled){state.ball.position=impact;state.ball.height=height;control.Invoke(sim,new object[]{actor});if(actor.actionKind!=(feet?"control-foot":kind))throw new Exception("Wrong Core control event");controlled=true;}
    if(controlled&&!headers){float elapsed=time-5,duration=feet?.3f:MatchSimulation.BodyControlDuration(kind);stage=elapsed<duration?"controle":"recuperation";actor.actionTime=Mathf.Max(0,duration-elapsed);state.ball.controlElapsed=elapsed;if(elapsed>=duration){actor.action="idle";actor.actionTime=0;}state.ball.height=MatchSimulation.ControlledBallHeight(actor,state.ball);sphere.transform.position=new Vector3(impact.x,state.ball.height,impact.z);}
    var context=new PlayerMotionContext{carrying=controlled&&!headers,bodyReception=BodyReceptionAnticipationSample.From(state,actor,1,view.MotionStature),hasSimulationClock=true,simulationClock=(float)Math.Floor(time*10+.0001f)/10};
    if(withdrawals){sphere.SetActive(true);sphere.transform.position=new Vector3((scene==0?1:-1)*.85f,.11f,.42f);}
    if(headers&&controlled){sphere.SetActive(true);sphere.transform.position=new Vector3(actor.actionTarget.x,actor.actionHeight,actor.actionTarget.z);}
    if(celebrations&&time>=1&&time<4){context.reaction=2;context.reactionTime=time-1;}
    if(braces&&time>=1&&time<4){context.contactWeight=1;context.contactDirection=new Point(scene==0?1:-1,0);}
    if(signals&&time>=1&&time<4){if(scene==0){context.requestSide=1;context.requestWeight=1;}else{context.reaction=1;context.reactionTime=time-1;}}
    string actorBefore=JsonUtility.ToJson(actor);view.Render(actor,1,frame==0?0:dt,sphere.activeSelf?sphere.transform.position:new Vector3(0,.11f,6),context);
    if(actorBefore!=JsonUtility.ToJson(actor)||Vector3.Distance(view.transform.position,new Vector3(actor.position.x,0,actor.position.z))>.0001f)throw new Exception("Renderer altered authoritative actor");
    var desired=view.transform.position+new Vector3(4,2.6f,4);camera.transform.position=frame==0?desired:Vector3.Lerp(camera.transform.position,desired,1-Mathf.Exp(-dt*7));camera.transform.LookAt(view.transform.position+Vector3.up*.95f);
    string file=kind+"/frame-"+frame.ToString("D4")+".png";Capture(file);samples.Add(new Frame{file=file,kind=kind,stage=stage,frame=frame,time=time,root=view.transform.position,left=view.FootPosition(true),right=view.FootPosition(false),ball=sphere.transform.position});
    frame++;if(time<7)return;UnityEngine.Object.DestroyImmediate(sceneRoot);UnityEngine.Object.DestroyImmediate(groundMaterial);UnityEngine.Object.DestroyImmediate(lineMaterial);UnityEngine.Object.DestroyImmediate(ballMaterial);target.Release();UnityEngine.Object.DestroyImmediate(target);view=null;frame=0;scene++;
    if(scene==2){var report=new Report{passed=true,fps=fps,scenes=scene,frames=samples.Count,samples=samples.ToArray()};if(withdrawals)report.limitation="Native constructed standing poke cancelled halfway through preparation, then recovery to idle. No new mocap, match replay or Android performance claim.";if(headers)report.limitation="Native constructed running-to-header transition initiated by Core.DistributeHeader, then its recorded action timer replayed at 30Hz. No match replay or Android performance claim.";if(celebrations)report.limitation="Native constructed three-second goal reactions, existing licensed clips 13 and 14 with entry and exit blends. No new mocap, match replay or Android fluidity claim.";if(braces)report.limitation="Native constructed upright shoulder-pressure response and release, captured idle plus bounded torso/arm overlay. No simulated collision, new mocap or Android performance claim.";if(falls)report.limitation="Native constructed fall/recovery sequence driven by the existing Core fall duration. Captured clips with hand-floor clearance; no new mocap, match replay or physical Android test.";if(feet)report.limitation="Native constructed foot receptions initiated by Core.Control, saved 0.3s control timer replayed in isolation. Existing captured clip and procedural foot correction. Not a full match or Android performance test.";if(signals)report.limitation="Native constructed request/applause sequences: captured locomotion with procedural upper-body signals. Not a match replay, new mocap or device performance test.";if(slides)report.limitation="Offline native pose/render sequence, not a device performance test or a newly simulated match. Constructed run, three committed slide travel segments, fixed accepted impact, recovery and running restart. Existing captured clips plus procedural contact adaptation; no newly acquired motion capture. Every image uses the current baked bones.";File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(report,true));SessionState.SetBool("MotionContinuityFilm",false);Debug.Log("TOUCHLINE_MOTION_CONTINUITY_FILM_OK");EditorApplication.Exit(0);}
   }catch(Exception e){SessionState.SetBool("MotionContinuityFilm",false);Debug.LogException(e);EditorApplication.Exit(1);}
  }
 }
}

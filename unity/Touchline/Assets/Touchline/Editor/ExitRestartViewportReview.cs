using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Touchline.Core;
namespace Touchline.Editor {
 [InitializeOnLoad]public static class ExitRestartViewportReview {
  [Serializable]class Clip{public string id,statePath;public float startClock,eventClock;}
  [Serializable]class Index{public Clip[] clips;}
  [Serializable]class Row{public string clip,tag,phase,caption;public int fps,speed,actualSpeed,frame;public float clock,alpha,opacity,restart;public bool paused,outgoing;public Vector3 physicalBall,displayBall,camera,cutFocus,cutExpected;}
  [Serializable]class Report{public bool passed,physicalAndroid=false,synthetic=false,realUIToolkitViewport=true;public string limitation="Native saved replay through actual MatchViewport and opaque UI Toolkit overlay. Selected sparse screenshots after UI frames; no generated frames or simulated ball retrieval. Offline render, not phone performance. At x10, entry is first sampled at x1 until the accepted outgoing boundary is displayed, then the requested pace is applied; this isolates skipped-cut coverage and is not an uninterrupted x10 replay.";public Row[] samples;public int cases;}
  static string Arg(string key,string fallback)=>Environment.GetCommandLineArgs().FirstOrDefault(s=>s.StartsWith(key+"="))?.Substring(key.Length+1)??fallback;
  static int last=-1,warm,settle,caseIndex,sampleFrame,pauseRemaining,completed;static string output,input,dir,pendingTag;static Clip[] clips;static Database db;static MatchArena arena;static UIDocument doc;static RenderTexture uiTarget;static Image viewport;static float exitClock,firstClock,pauseClock,pauseOpacity;static Vector3 pauseBall,cutFocus,cutExpected;static bool sawOutgoing,sawOpaque,sawReveal,sawFade,sawReposition,pauseDone;static int fps,speed;static readonly List<Row> rows=new List<Row>();
  static ExitRestartViewportReview(){EditorApplication.update+=Tick;}
  public static void Run(){ProjectBuilder.Configure();SessionState.SetBool("ExitRestartViewportReview",true);EditorApplication.isPlaying=true;}
  static void Check(bool c,string message){if(!c)throw new Exception(message);}
  static void Initialize(){
   var app=TouchlineApp.Instance;Check((bool)typeof(TouchlineApp).GetProperty("VisualValidation",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(app),"Save protection must be registered before native review");app.enabled=false;app.GetComponent<UIDocument>().rootVisualElement.style.display=DisplayStyle.None;app.GetComponent<UIDocument>().panelSettings.scale=1;
   string name=Arg("-touchlineExitOutput","exit-restart-viewport-v1"),pack=Arg("-touchlineExitPack","natural-match-sequences-20261005-v5");
   Check(System.Text.RegularExpressions.Regex.IsMatch(name,"^[a-zA-Z0-9-]+$")&&System.Text.RegularExpressions.Regex.IsMatch(pack,"^[a-zA-Z0-9-]+$"),"Invalid proof name");
   input=Path.GetFullPath("../../artifacts/research/"+pack);output=Path.GetFullPath("../../artifacts/unity/"+name);Check(!Directory.Exists(output),"Preserve prior proofs");Directory.CreateDirectory(output);
   var index=JsonUtility.FromJson<Index>(File.ReadAllText(Path.Combine(input,"index.json")));string only=Arg("-touchlineExitOnly","all");clips=index.clips.Where(c=>(c.id=="04-shot"||c.id=="08-header")&&(only=="all"||c.id==only)).ToArray();Check(clips.Length>0,"No requested exit clips");db=JsonUtility.FromJson<Database>(File.ReadAllText(Path.Combine(input,"database.json")));
   var panel=UnityEngine.Object.Instantiate(app.GetComponent<UIDocument>().panelSettings);panel.scaleMode=PanelScaleMode.ConstantPixelSize;panel.scale=1;uiTarget=new RenderTexture(960,640,24);uiTarget.Create();panel.targetTexture=uiTarget;
   doc=new GameObject("Exit UI viewport review").AddComponent<UIDocument>();doc.panelSettings=panel;doc.sortingOrder=100;
   StartCase();
  }
  static void StartCase(){
   if(arena!=null)UnityEngine.Object.DestroyImmediate(arena.gameObject);
   var clip=clips[caseIndex/6];int combination=caseIndex%6;fps=new[]{30,60,120}[combination/2];speed=combination%2==0?1:10;
   string json=File.ReadAllText(Path.Combine(input,clip.statePath));var seek=new MatchSimulation(db,JsonUtility.FromJson<MatchState>(json));int cap=0;
   while(!seek.ExitContact.valid&&cap++<150)seek.Advance(.1);
   Check(seek.ExitContact.valid,"No descriptive exit in natural clip "+clip.id);exitClock=seek.ExitContact.clock;
   var sim=new MatchSimulation(db,JsonUtility.FromJson<MatchState>(json));double pre=Math.Max(0,exitClock-.4-sim.State.clock);sim.Advance(pre);firstClock=sim.State.clock;
   var root=doc.rootVisualElement;root.Clear();root.style.flexDirection=FlexDirection.Column;root.style.backgroundColor=new Color(.055f,.09f,.125f);root.style.width=960;root.style.height=640;
   var title=new Label(clip.id+" Ãƒâ€šÃ‚Â· sortie et remise Ãƒâ€šÃ‚Â· "+fps+" FPS Ãƒâ€šÃ‚Â· x"+speed);title.style.height=40;title.style.color=Color.white;title.style.fontSize=18;root.Add(title);
   viewport=new Image{name="match-viewport",scaleMode=ScaleMode.StretchToFill};viewport.style.height=560;viewport.style.width=960;root.Add(viewport);
   var controls=new VisualElement();controls.style.flexDirection=FlexDirection.Row;controls.style.height=40;var pause=new Button(()=>arena.Paused=!arena.Paused){text="Pause / Reprendre"};pause.name="exit-review-pause";controls.Add(pause);controls.Add(new Button(){text="Options"});root.Add(controls);
   arena=new GameObject("Exit natural arena").AddComponent<MatchArena>();arena.Initialize(db,sim);arena.Speed=1;arena.Paused=false;arena.Broadcast.SetMode(MatchViewingMode.Full);arena.BindViewport(viewport);arena.enabled=false;
   cutFocus=cutExpected=Vector3.zero;sampleFrame=0;sawOutgoing=sawOpaque=sawReveal=sawFade=sawReposition=pauseDone=false;pauseRemaining=0;pendingTag=null;settle=4;
   dir=Path.Combine(output,clip.id+"-"+fps+"fps-x"+speed);Directory.CreateDirectory(dir);
  }
  static void Sample(){
   arena.Speed=sawOutgoing?speed:1;arena.RenderFrame(1f/fps);var m=arena.Simulation.State;string tag=null;
   if(sampleFrame==0)tag="before-boundary";
   if(arena.ExitBallIsOutgoing&&!sawOutgoing){sawOutgoing=true;tag="outgoing";}
   if(arena.RestartCutOpacity>0&&arena.RestartCutOpacity<.999f&&!sawFade){sawFade=true;tag="fade";}
   if(arena.RestartCutOpacity>=.999f&&!sawOpaque){sawOpaque=true;tag="opaque";}
   if(sawOpaque&&arena.RestartCutOpacity<=0&&!sawReveal){sawReveal=true;tag="replacement";}
   if(sawOutgoing&&!arena.ExitBallIsOutgoing&&arena.Simulation.ExitContact.valid&&!sawReposition){
    sawReposition=true;tag="opaque-reposition";
    Check(arena.RestartCutOpacity>=.999f,"Actual change of ball location must be opaque");
    Check(Vector3.Distance(arena.BallDisplayPosition,new Vector3(m.ball.position.x,m.ball.height,m.ball.position.z))<.0001f,"Displayed replacement must match physical spot under curtain");
    var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
    var focus=(Vector3)typeof(MatchArena).GetField("focus",flags).GetValue(arena);
    bool tactical=(bool)typeof(MatchArena).GetField("tactical",flags).GetValue(arena);var bp=arena.BallDisplayPosition;bool portrait=arena.MatchCamera.aspect<.8f;
    bool setPiece=m.restart>0&&(m.phase=="penalty"||m.phase=="corner"||m.phase=="free-kick"&&bp.x*arena.Simulation.Direction(m.restartSide)>20);
    var expected=tactical?new Vector3(0,.6f,0):new Vector3(Mathf.Clamp(bp.x*.90f,-46,46),.6f,Mathf.Clamp(bp.z*(portrait?.72f:setPiece?.46f:.58f),portrait?-25:-19,portrait?25:19));
    // MatchArena constrains the tracking centre to retain the ball after
    // resetting it, especially for a throw-in beyond the +/-19 m centre clamp.
    if(!tactical){expected.x=Mathf.Clamp(expected.x,bp.x-20,bp.x+20);expected.z=Mathf.Clamp(expected.z,bp.z-13,bp.z+13);}
    cutFocus=focus;cutExpected=expected;
    Debug.Log("EXIT_CUT_FOCUS clip="+clips[caseIndex/6].id+" fps="+fps+" speed="+speed+" actual="+focus.ToString("F5")+" expected="+expected.ToString("F5"));
    Check(Vector3.Distance(focus,expected)<.001f,"Camera still eases from old point behind reposition cut: actual="+focus+" expected="+expected);
   }
   if(sawOpaque&&!pauseDone&&arena.RestartCutOpacity>=.999f){arena.Paused=true;pauseRemaining=2;pauseClock=m.clock;pauseOpacity=arena.RestartCutOpacity;pauseBall=arena.BallDisplayPosition;pauseDone=true;}
   else if(pauseRemaining>0){Check(m.clock==pauseClock&&arena.RestartCutOpacity==pauseOpacity&&Vector3.Distance(pauseBall,arena.BallDisplayPosition)<.00001f,"Pause advanced the exit cut");tag="paused-"+pauseRemaining;pauseRemaining--;if(pauseRemaining==0)arena.Paused=false;}
   rows.Add(new Row{clip=clips[caseIndex/6].id,fps=fps,speed=speed,actualSpeed=arena.Speed,frame=sampleFrame,clock=m.clock,alpha=(float)(m.remainder/.1),phase=m.phase,restart=m.restart,caption=arena.RestartCutCaption,opacity=arena.RestartCutOpacity,outgoing=arena.ExitBallIsOutgoing,paused=arena.Paused,physicalBall=new Vector3(m.ball.position.x,m.ball.height,m.ball.position.z),displayBall=arena.BallDisplayPosition,camera=arena.MatchCamera.transform.position,cutFocus=cutFocus,cutExpected=cutExpected,tag=tag});
   pendingTag=tag;sampleFrame++;settle=2;
  }
  static void Capture(){
   if(pendingTag==null)return;var previous=RenderTexture.active;RenderTexture.active=uiTarget;var image=new Texture2D(uiTarget.width,uiTarget.height,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,uiTarget.width,uiTarget.height),0,0);image.Apply();RenderTexture.active=previous;
   try{
    if(pendingTag=="opaque"||pendingTag=="opaque-reposition"){
     var cut=viewport.Q("restart-broadcast-cut");Check(cut!=null&&cut.resolvedStyle.opacity>=.999f&&cut.resolvedStyle.display!=DisplayStyle.None,"Reposition did not produce an actual opaque viewport element");
     var points=new[]{new Vector2Int(30,70),new Vector2Int(930,70),new Vector2Int(30,560),new Vector2Int(930,560)};var first=image.GetPixel(points[0].x,points[0].y);
     foreach(var p in points){var c=image.GetPixel(p.x,p.y);Check(c.b>c.g&&c.g>c.r&&c.b>.03f&&Math.Abs(c.r-first.r)<.02f&&Math.Abs(c.g-first.g)<.02f&&Math.Abs(c.b-first.b)<.02f,"Opaque viewport screenshot still reveals the pitch or a black hole");}
     Check(doc.rootVisualElement.Q<Button>("exit-review-pause").resolvedStyle.display!=DisplayStyle.None,"Pause outside viewport remains accessible");
    }
    File.WriteAllBytes(Path.Combine(dir,sampleFrame.ToString("D4")+"-"+pendingTag+".png"),image.EncodeToPNG());
   }finally{UnityEngine.Object.DestroyImmediate(image);pendingTag=null;}
  }
  static void Save(bool passed){if(output!=null)File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(new Report{passed=passed,cases=completed,samples=rows.ToArray()},true));}
  static void Tick(){
   if(!SessionState.GetBool("ExitRestartViewportReview",false)||!EditorApplication.isPlaying||TouchlineApp.Instance==null||last==Time.frameCount)return;last=Time.frameCount;
   try{
    if(doc==null){if(++warm<20)return;Initialize();return;}
    if(settle-->0)return;Capture();
    if(sawReveal&&arena.Simulation.State.clock>exitClock+1.1f){
     Check(sawOutgoing&&sawOpaque&&sawReposition&&pauseDone,"Natural exit did not traverse required visible stages");completed++;Save(false);caseIndex++;
     if(caseIndex>=clips.Length*6){Save(true);SessionState.SetBool("ExitRestartViewportReview",false);Debug.Log("TOUCHLINE_EXIT_VIEWPORT_OK");EditorApplication.Exit(0);return;}
     StartCase();return;
    }
    Check(sampleFrame<fps*5,"Exit review did not complete");Sample();
   }catch(Exception e){Save(false);SessionState.SetBool("ExitRestartViewportReview",false);Debug.LogException(e);EditorApplication.Exit(1);}
  }
 }
}

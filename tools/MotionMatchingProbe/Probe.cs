// Editor-only experiment deployed into the pinned upstream project by Run.ps1.
// No dependency, animation or simulation code is installed in Touchline.
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using MotionMatching;

[InitializeOnLoad]
public static class TouchlineMotionMatchingProbe
{
    const int WarmupFrames=120, MeasuredFrames=600;
    static int actors,hz,lastFrame=-1,frames,errors,editorSearchErrors,stationaryRootSamples;
    static string output;
    static double started,previousCost;
    static MotionMatchingController[] controllers;
    static Vector3[] previous;
    static float maxStep;
    static readonly List<double> costs=new List<double>();
    static TouchlineMotionMatchingProbe(){if(SessionState.GetBool("TouchlineMMProbe",false))Attach();}
    static string Arg(string name){var a=Environment.GetCommandLineArgs();return a[Array.IndexOf(a,name)+1];}
    static void Attach(){
        actors=int.Parse(Arg("-probeActors"));hz=int.Parse(Arg("-probeHz"));output=Arg("-probeOutput");
        started=EditorApplication.timeSinceStartup;
        EditorApplication.update-=Tick;EditorApplication.update+=Tick;
        Application.logMessageReceived+=(m,s,t)=>{if(t==LogType.Error||t==LogType.Exception||t==LogType.Assert){
            if(s.Contains("UnityEditor.Search."))editorSearchErrors++;else errors++;
            File.AppendAllText(output+"/errors.txt",m+"\n"+s+"\n");}};
    }
    public static void Run()
    {
        SubframeRoot.Validate(); // Six deterministic interpolation/boundary/immutability checks.
        actors=int.Parse(Arg("-probeActors"));hz=int.Parse(Arg("-probeHz"));output=Arg("-probeOutput");
        EditorSceneManager.OpenScene("Assets/Examples/Scenes/01_Advanced/ExamplePathTest.unity");
        var template=GameObject.Find("Test1");
        if(template==null)throw new Exception("Missing upstream Test1 scene root");
        foreach(var root in template.scene.GetRootGameObjects())if(root.name.StartsWith("Test")&&root!=template)UnityEngine.Object.DestroyImmediate(root);
        for(int i=1;i<actors;i++){
            var clone=UnityEngine.Object.Instantiate(template);clone.name="Probe"+i;
            // Separate paths, preserving their velocity/turn geometry.
            foreach(var path in clone.GetComponentsInChildren<PathCharacterController>())
                for(int k=0;k<path.Path.Length;k++)path.Path[k].Position+=new Unity.Mathematics.float2((i%6)*4,(i/6)*4);
        }
        foreach(var mm in UnityEngine.Object.FindObjectsByType<MotionMatchingController>(FindObjectsSortMode.None)){
            mm.LockFPS=false;mm.DebugSkeleton=false;mm.DebugFutureSkeleton=false;mm.DebugGUI=false;
            mm.DebugPose=false;mm.DebugTrajectory=false;mm.DebugSearch=false;mm.DebugContacts=false;
        }
        EditorSettings.enterPlayModeOptionsEnabled=true;
        EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
        SessionState.SetBool("TouchlineMMProbe",true);Attach();
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if(EditorApplication.timeSinceStartup-started>240){EditorApplication.Exit(2);return;}
        if(!EditorApplication.isPlaying||Time.frameCount==lastFrame)return;
        lastFrame=Time.frameCount;
        // Fixed simulation step, wall time is NOT reported as device frame time.
        Time.captureDeltaTime=1f/hz;QualitySettings.vSyncCount=0;Application.targetFrameRate=-1;
        if(controllers==null){
            controllers=UnityEngine.Object.FindObjectsByType<MotionMatchingController>(FindObjectsSortMode.None);
            previous=controllers.Select(c=>c.transform.position).ToArray();
            if(controllers.Length!=actors){EditorApplication.Exit(3);return;}
        }
        frames++;
        var p=PROFILE.GET_DATA("Motion Matching Total");
        double total=p?.TotalMs??0;
        if(frames>WarmupFrames){
            costs.Add(total-previousCost);
            for(int i=0;i<controllers.Length;i++){
                float step=Vector3.Distance(previous[i],controllers[i].transform.position);maxStep=Math.Max(maxStep,step);
                if(step<.00001f)stationaryRootSamples++; // Ten micrometres, on a continuously moving path.
            }
        }
        previousCost=total;
        for(int i=0;i<controllers.Length;i++)previous[i]=controllers[i].transform.position;
        if(frames==WarmupFrames+180)Capture();
        if(frames<WarmupFrames+MeasuredFrames)return;
        costs.Sort();
        var result=new Result{actors=actors,hz=hz,frames=costs.Count,errors=errors,editorSearchErrors=editorSearchErrors,
            meanAnimationCpuMs=costs.Average(),p95AnimationCpuMs=costs[(int)(costs.Count*.95)],
            maxRootStepMetres=maxStep,stationaryRootSamplePercent=100f*stationaryRootSamples/(actors*costs.Count),profileCalls=p?.Count??0,
            databaseHz=controllers[0].DatabaseFrameRate,
            unity=Application.unityVersion,device=SystemInfo.processorType,
            note="Editor CPU timing for upstream animation only; excludes rendering and Touchline simulation. Synthetic paths, no ball. Not Android performance."};
        File.WriteAllText(output+"/result.json",JsonUtility.ToJson(result,true));
        SessionState.SetBool("TouchlineMMProbe",false);EditorApplication.update-=Tick;EditorApplication.Exit(errors==0&&p!=null?0:1);
    }
    static void Capture()
    {
        var cam=Camera.main;if(cam==null)return;
        cam.transform.position=controllers[0].transform.position+new Vector3(5,4,-7);
        cam.transform.LookAt(controllers[0].transform.position+Vector3.up);
        var rt=new RenderTexture(960,720,24);cam.targetTexture=rt;cam.Render();
        var old=RenderTexture.active;RenderTexture.active=rt;var tex=new Texture2D(960,720,TextureFormat.RGB24,false);
        tex.ReadPixels(new Rect(0,0,960,720),0,0);tex.Apply();File.WriteAllBytes(output+"/preview.png",tex.EncodeToPNG());
        RenderTexture.active=old;cam.targetTexture=null;UnityEngine.Object.DestroyImmediate(tex);UnityEngine.Object.DestroyImmediate(rt);
    }
    [Serializable] class Result{public int actors,hz,frames,errors,editorSearchErrors,profileCalls,databaseHz;public double meanAnimationCpuMs,p95AnimationCpuMs;public float maxRootStepMetres,stationaryRootSamplePercent;public string unity,device,note;}
}

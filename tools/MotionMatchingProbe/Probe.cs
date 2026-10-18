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
    // Equal path duration at every refresh rate: two seconds warm-up, twenty measured.
    static int WarmupFrames=>2*hz;
    static int MeasuredFrames=>20*hz;
    static readonly PoseMetrics poseMetrics=new PoseMetrics();
    static int actors,hz,lastFrame=-1,frames,errors,editorSearchErrors,stationaryRootSamples;
    static string output;
    static double started,previousCost;
    static MotionMatchingController[] controllers;
    static Vector3[] previous;
    static float maxStep;
    static float maxRecordedPositionError;
    static string TracePath=>Environment.GetCommandLineArgs().Contains("-probeTrace")?Arg("-probeTrace"):null;
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
        SubframePose.Validate(); // Eight joint, contact and reusable-buffer checks.
        RecordedMatchController.CheckInterpolation();
        actors=int.Parse(Arg("-probeActors"));hz=int.Parse(Arg("-probeHz"));output=Arg("-probeOutput");
        EditorSceneManager.OpenScene("Assets/Examples/Scenes/01_Advanced/ExamplePathTest.unity");
        File.WriteAllText(output+"/contact-checks.json",JsonUtility.ToJson(FootContactChecks.Run(),true));
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
        if(TracePath!=null){
            int track=0;
            foreach(var mm in UnityEngine.Object.FindObjectsByType<MotionMatchingController>(FindObjectsSortMode.None)){
                var old=mm.CharacterController;
                var recorded=old.gameObject.AddComponent<RecordedMatchController>();
                recorded.TraceFile=TracePath;recorded.TrackIndex=track++;recorded.MotionMatching=mm;
                mm.CharacterController=recorded;UnityEngine.Object.DestroyImmediate(old);
            }
            var ground=GameObject.CreatePrimitive(PrimitiveType.Plane);ground.name="Recorded match ground";
            ground.transform.position=new Vector3(0,.002f,0);ground.transform.localScale=new Vector3(11,1,7);
            ground.GetComponent<Renderer>().sharedMaterial=new Material(Shader.Find("Universal Render Pipeline/Lit"));
            ground.GetComponent<Renderer>().sharedMaterial.color=new Color(.12f,.28f,.13f);
        }
        EditorSettings.enterPlayModeOptionsEnabled=true;
        EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
        SessionState.SetBool("TouchlineMMProbe",true);Attach();
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if(EditorApplication.timeSinceStartup-started>240){EditorApplication.Exit(2);return;}
        // Do not keep collecting partial skeletons after an upstream import/runtime error.
        if(errors>0){SessionState.SetBool("TouchlineMMProbe",false);EditorApplication.Exit(1);return;}
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
        poseMetrics.Sample(controllers,hz,frames>WarmupFrames);
        var p=PROFILE.GET_DATA("Motion Matching Total");
        double total=p?.TotalMs??0;
        if(frames>WarmupFrames){
            costs.Add(total-previousCost);
            for(int i=0;i<controllers.Length;i++){
                float step=Vector3.Distance(previous[i],controllers[i].transform.position);maxStep=Math.Max(maxStep,step);
                if(step<.00001f)stationaryRootSamples++; // Ten micrometres, on a continuously moving path.
                if(controllers[i].CharacterController is RecordedMatchController recorded)
                    maxRecordedPositionError=Math.Max(maxRecordedPositionError,Vector3.Distance(controllers[i].transform.position,recorded.GetPosition()));
            }
        }
        previousCost=total;
        for(int i=0;i<controllers.Length;i++)previous[i]=controllers[i].transform.position;
        if(frames==WarmupFrames+3*hz||frames==WarmupFrames+10*hz||frames==WarmupFrames+17*hz)Capture();
        if(frames<WarmupFrames+MeasuredFrames)return;
        costs.Sort();
        poseMetrics.Finish();
        File.WriteAllText(output+"/pose-metrics.json",JsonUtility.ToJson(poseMetrics,true));
        if(TracePath!=null){
            var speeds=new List<float>();int sprintFrames=0;
            var poses=controllers[0].PoseSet;
            for(int i=0;i<poses.NumberPoses;i++){
                poses.GetPose(i,out PoseVector pose);
                var velocity=pose.JointLocalVelocities[0];
                float speed=Mathf.Sqrt(velocity.x*velocity.x+velocity.z*velocity.z);
                speeds.Add(speed);if(speed>=6f)sprintFrames++; // Six m/s: diagnostic threshold, not a gameplay rule.
            }
            speeds.Sort();
            File.WriteAllText(output+"/database-coverage.json",JsonUtility.ToJson(new Coverage{
                poses=speeds.Count,clips=poses.NumberClips,maxSpeed=speeds[speeds.Count-1],p99Speed=speeds[(int)(speeds.Count*.99)],
                framesAtLeastSixMetresPerSecond=sprintFrames,note="Upstream demo locomotion database only, not the private football pack. Root velocity metadata; does not validate football contacts."},true));
            var recordings=controllers.Select(c=>(RecordedMatchController)c.CharacterController).ToArray();
            var traceResult=new TraceResult{maxRecordedPositionError=maxRecordedPositionError,
                maxAnimationRootCorrection=recordings.Max(c=>c.MaxRootCorrection),maxAnimationYawCorrection=recordings.Max(c=>c.MaxYawCorrection),
                meanAnimationRootCorrection=recordings.Sum(c=>c.TotalRootCorrection)/recordings.Sum(c=>c.RootSamples),
                note="Root correction includes warm-up; position error measured after pose evaluation. Offline recorded future, not live prediction. Locomotion demo clips do not implement football actions."};
            File.WriteAllText(output+"/trace-result.json",JsonUtility.ToJson(traceResult,true));
            if(maxRecordedPositionError>.001f)errors++; // One millimetre maximum drift from interpolated Core positions.
        }
        var result=new Result{actors=actors,hz=hz,frames=costs.Count,errors=errors,editorSearchErrors=editorSearchErrors,
            meanAnimationCpuMs=costs.Average(),p95AnimationCpuMs=costs[(int)(costs.Count*.95)],
            maxRootStepMetres=maxStep,stationaryRootSamplePercent=100f*stationaryRootSamples/(actors*costs.Count),profileCalls=p?.Count??0,
            databaseHz=controllers[0].DatabaseFrameRate,
            unity=Application.unityVersion,device=SystemInfo.processorType,
            note="Editor CPU animation only; excludes rendering and Touchline simulation. "+(TracePath==null?"Synthetic paths.":"Offline Touchline recording, native ticks interpolated; recorded future available.")+" No ball or football actions. Not Android performance."};
        File.WriteAllText(output+"/result.json",JsonUtility.ToJson(result,true));
        SessionState.SetBool("TouchlineMMProbe",false);EditorApplication.update-=Tick;EditorApplication.Exit(errors==0&&p!=null?0:1);
    }
    static void Capture()
    {
        var cam=Camera.main;if(cam==null)return;
        var focus=TracePath==null?controllers[0]:controllers.First(c=>((RecordedMatchController)c.CharacterController).TrackIndex==9);
        cam.transform.position=focus.transform.position+new Vector3(5,4,-7);
        cam.transform.LookAt(focus.transform.position+Vector3.up);
        var rt=new RenderTexture(960,720,24);cam.targetTexture=rt;cam.Render();
        var old=RenderTexture.active;RenderTexture.active=rt;var tex=new Texture2D(960,720,TextureFormat.RGB24,false);
        tex.ReadPixels(new Rect(0,0,960,720),0,0);tex.Apply();File.WriteAllBytes(output+"/preview-"+(frames/hz)+"s.png",tex.EncodeToPNG());
        RenderTexture.active=old;cam.targetTexture=null;UnityEngine.Object.DestroyImmediate(tex);UnityEngine.Object.DestroyImmediate(rt);
    }
    [Serializable] class Result{public int actors,hz,frames,errors,editorSearchErrors,profileCalls,databaseHz;public double meanAnimationCpuMs,p95AnimationCpuMs;public float maxRootStepMetres,stationaryRootSamplePercent;public string unity,device,note;}
    [Serializable] class TraceResult{public float maxRecordedPositionError,maxAnimationRootCorrection,maxAnimationYawCorrection;public double meanAnimationRootCorrection;public string note;}
    [Serializable] class Coverage{public int poses,clips,framesAtLeastSixMetresPerSecond;public float maxSpeed,p99Speed;public string note;}
}

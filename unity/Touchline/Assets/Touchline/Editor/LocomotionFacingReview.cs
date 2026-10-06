using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Touchline.Core;

namespace Touchline.Editor
{
    // Deliberately abrupt synthetic turns expose continuity failures; they are
    // not real match motion or a claim that the simulation reverses instantly.
    [InitializeOnLoad] public static class LocomotionFacingReview
    {
        [Serializable] class Sample { public string file,scenario;public int fps,frame;public float time,yaw,targetYaw,error,leftHeight,rightHeight;public Vector3 position,leftFoot,rightFoot; }
        [Serializable] class Report { public bool passed,synthetic=true,physicalAndroid=false;public int scenarios;public string limitation="Constructed starts, stops and instantaneous quarter/half turns. Presentation yaw lags an abrupt reversal while directional locomotion supplies backward/side steps. No new mocap or physical-device frame-rate claim.";public Sample[] samples; }
        static readonly List<Sample> samples=new List<Sample>();
        static readonly string[] names={"start-stop","quarter-turn","half-turn","identity-reset"};
        static readonly int[] rates={30,60,120};
        static GameObject sceneRoot;static PlayerView view;static Actor actor;static Camera camera;static RenderTexture target;static string output;
        static int scene,frame,last=-1;
        static LocomotionFacingReview(){EditorApplication.update+=Tick;}
        public static void Run()
        {
            string name=Environment.GetCommandLineArgs().FirstOrDefault(a=>a.StartsWith("-touchlineFacingOutput="))?.Substring("-touchlineFacingOutput=".Length)??"locomotion-facing-review-v1";
            if(!System.Text.RegularExpressions.Regex.IsMatch(name,"^[a-zA-Z0-9-]+$"))throw new Exception("Invalid output name");
            output=Path.GetFullPath("../../artifacts/unity/"+name);
            if(Directory.Exists(output)||File.Exists(output))throw new Exception("Preserve previous proof; use a new output name");
            SessionState.SetString("LocomotionFacingOutput",output);ProjectBuilder.Configure();SessionState.SetBool("LocomotionFacingReview",true);EditorApplication.isPlaying=true;
        }
        static void Layer(GameObject root){root.layer=30;foreach(Transform child in root.transform)Layer(child.gameObject);}
        static void Setup()
        {
            TouchlineApp.Instance.GetComponent<UIDocument>().rootVisualElement.style.display=DisplayStyle.None;
            if(scene==0){output=SessionState.GetString("LocomotionFacingOutput","");if(Directory.Exists(output))throw new Exception("Proof exists");Directory.CreateDirectory(output);}
            sceneRoot=new GameObject("Synthetic locomotion review");var player=new GameObject("Player");player.transform.SetParent(sceneRoot.transform);
            view=player.AddComponent<PlayerView>();view.Build(new PlayerData{id="facing-review",heightCm=182,preferredFoot="Right"},0,9,new Color(.08f,.45f,.76f));
            actor=new Actor{id="facing-review",slot=9,action="run",angle=0};
            var ground=GameObject.CreatePrimitive(PrimitiveType.Plane);ground.transform.SetParent(sceneRoot.transform);ground.transform.localScale=Vector3.one*2;ground.GetComponent<Renderer>().sharedMaterial=PlayerView.Material(new Color(.16f,.28f,.13f));
            camera=new GameObject("Facing camera").AddComponent<Camera>();camera.transform.SetParent(sceneRoot.transform);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.05f,.08f,.10f);camera.cullingMask=1<<30;camera.fieldOfView=38;camera.enabled=false;
            camera.transform.position=new Vector3(5,3.3f,5);camera.transform.LookAt(new Vector3(0,1,0));
            var light=new GameObject("Facing light").AddComponent<Light>();light.transform.SetParent(sceneRoot.transform);light.type=LightType.Directional;light.intensity=1.2f;light.transform.rotation=Quaternion.Euler(45,-25,0);light.cullingMask=1<<30;
            Layer(sceneRoot);target=new RenderTexture(960,720,24){antiAliasing=2};target.Create();camera.targetTexture=target;camera.aspect=4f/3;
        }
        static void Pose(int scenario,float time)
        {
            float x=0,z=0,speed=3,yaw=0;
            if(scenario==0){float u=time-.5f;if(time<.5f){speed=6*time;z=3*time*time;}else if(time<1){speed=3-6*u;z=.75f+3*u-3*u*u;}else{speed=0;z=1.5f;}}
            else if(time<.5f)z=3*time;
            else if(scenario==1){x=3*(time-.5f);z=1.5f;yaw=90;}
            else {z=1.5f-3*(time-.5f);yaw=180;}
            actor.position=actor.previous=new Point(x,z);actor.angle=yaw*Mathf.Deg2Rad;
            actor.velocity=new Point(Mathf.Sin(actor.angle)*speed,Mathf.Cos(actor.angle)*speed);
        }
        static void Capture(int scenario,int fps,float time)
        {
            string name=names[scenario]+"-"+fps+"fps-"+frame.ToString("D3")+".png";
            var previous=RenderTexture.active;Texture2D image=null;
            var skins=new List<SkinnedMeshRenderer>();var proxies=new List<GameObject>();var meshes=new List<Mesh>();
            try{
                // A camera may already have skinned this body earlier in the
                // Editor frame. Bake these exact measured bones for the proof;
                // this diagnostic snapshot does not alter production rendering.
                foreach(var skin in view.GetComponentsInChildren<SkinnedMeshRenderer>()){
                    if(!skin.enabled)continue;var mesh=new Mesh();skin.BakeMesh(mesh);meshes.Add(mesh);
                    var proxy=new GameObject("Current diagnostic locomotion skin");proxy.layer=skin.gameObject.layer;
                    proxy.transform.SetParent(skin.transform,false);proxy.AddComponent<MeshFilter>().sharedMesh=mesh;proxy.AddComponent<MeshRenderer>().sharedMaterials=skin.sharedMaterials;
                    proxies.Add(proxy);skins.Add(skin);skin.enabled=false;
                }
                camera.Render();RenderTexture.active=target;image=new Texture2D(960,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,960,720),0,0);image.Apply();File.WriteAllBytes(Path.Combine(output,name),image.EncodeToPNG());
            }
            finally{
                foreach(var skin in skins)skin.enabled=true;foreach(var proxy in proxies)UnityEngine.Object.DestroyImmediate(proxy);foreach(var mesh in meshes)UnityEngine.Object.DestroyImmediate(mesh);
                if(image!=null)UnityEngine.Object.DestroyImmediate(image);RenderTexture.active=previous;
            }
            var left=view.FootPosition(true);var right=view.FootPosition(false);float yaw=view.transform.eulerAngles.y;
            samples.Add(new Sample{file=name,scenario=names[scenario],fps=fps,frame=frame,time=time,yaw=yaw,targetYaw=actor.angle*Mathf.Rad2Deg,error=Mathf.Abs(Mathf.DeltaAngle(yaw,actor.angle*Mathf.Rad2Deg)),position=view.transform.position,leftFoot=left,rightFoot=right,leftHeight=left.y,rightHeight=right.y});
        }
        static void Tick()
        {
            if(!SessionState.GetBool("LocomotionFacingReview",false)||!EditorApplication.isPlaying||TouchlineApp.Instance==null||last==Time.frameCount)return;last=Time.frameCount;
            try{
                if(view==null){Setup();return;}
                int scenario=scene%4,fps=rates[scene/4];float time=(float)frame/fps;Pose(scenario,time);
                if(scenario==3&&frame==fps/2)view.ChangeIdentity(new PlayerData{id="incoming-review",heightCm=178});
                view.Render(actor,1,frame==0||scenario>0&&frame==fps/2?0:1f/fps,new Vector3(0,.11f,4),new PlayerMotionContext{hasSimulationClock=true,simulationClock=(float)Math.Floor(time*10+.0001f)/10});
                if(Vector3.Distance(view.transform.position,new Vector3(actor.position.x,0,actor.position.z))>.0001f)throw new Exception("Presentation changed authoritative position");
                camera.transform.position=view.transform.position+new Vector3(4,2.6f,4);camera.transform.LookAt(view.transform.position+Vector3.up*.95f);
                if(frame==fps/2||frame==fps*3/5||frame==fps*4/5||frame==fps*11/10||frame==fps*3/2)Capture(scenario,fps,time);
                frame++;if(frame<=fps*3/2)return;
                UnityEngine.Object.DestroyImmediate(sceneRoot);view=null;target.Release();UnityEngine.Object.DestroyImmediate(target);frame=0;scene++;
                if(scene==12){
                    foreach(var sample in samples.Where(s=>s.time>=1.5f&&s.scenario!="start-stop"))if(sample.error>.1f)throw new Exception("Turn failed to settle within one second");
                    foreach(var sample in samples.Where(s=>s.scenario=="identity-reset"&&Mathf.Abs(s.time-.5f)<.001f))if(sample.error>.1f)throw new Exception("Substitution did not reset presentation");
                    File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(new Report{passed=true,scenarios=12,samples=samples.ToArray()},true));SessionState.SetBool("LocomotionFacingReview",false);Debug.Log("TOUCHLINE_LOCOMOTION_FACING_REVIEW_OK");EditorApplication.Exit(0);
                }
            }catch(Exception e){SessionState.SetBool("LocomotionFacingReview",false);Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}

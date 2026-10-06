using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Touchline.Core;

namespace Touchline.Editor
{
    public static class KeeperCompactReview
    {
        [Serializable] class Sample { public string file,scenario;public float time,contactError,bodyAngle,leftFootHeight,rightFootHeight; }
        [Serializable] class Report { public bool passed,synthetic=true,physicalAndroid=false;public Sample[] samples; }
        static void Capture(Camera camera,RenderTexture target,PlayerView view,string path)
        {
            var previous=RenderTexture.active;Texture2D image=null;var skins=new List<SkinnedMeshRenderer>();var proxies=new List<GameObject>();var meshes=new List<Mesh>();
            try{
                // Each diagnostic sample is evaluated synchronously. Bake the
                // current bones rather than photographing a GPU skin cache
                // belonging to the previous Camera.Render in this Editor frame.
                foreach(var skin in view.GetComponentsInChildren<SkinnedMeshRenderer>()){
                    if(!skin.enabled)continue;
                    var mesh=new Mesh();skin.BakeMesh(mesh);meshes.Add(mesh);
                    var proxy=new GameObject("Current diagnostic skin");proxy.transform.SetParent(skin.transform,false);proxy.AddComponent<MeshFilter>().sharedMesh=mesh;proxy.AddComponent<MeshRenderer>().sharedMaterials=skin.sharedMaterials;
                    proxies.Add(proxy);skins.Add(skin);skin.enabled=false;
                }
                camera.Render();RenderTexture.active=target;image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());
            }finally{
                foreach(var skin in skins)skin.enabled=true;foreach(var proxy in proxies)UnityEngine.Object.DestroyImmediate(proxy);foreach(var mesh in meshes)UnityEngine.Object.DestroyImmediate(mesh);
                if(image!=null)UnityEngine.Object.DestroyImmediate(image);RenderTexture.active=previous;
            }
        }
        public static void Run()
        {
            try{
                string folder=Environment.GetCommandLineArgs().FirstOrDefault(a=>a.StartsWith("-touchlineCompactOutput="))?.Split('=')[1]??"keeper-compact-review-v1";
                if(!System.Text.RegularExpressions.Regex.IsMatch(folder,"^[a-zA-Z0-9-]+$"))throw new Exception("Invalid output folder");
                var output=Path.GetFullPath("../../artifacts/unity/"+folder);if(Directory.Exists(output))throw new Exception("Use a new output name to preserve evidence");Directory.CreateDirectory(output);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                RenderSettings.ambientLight=new Color(.6f,.6f,.6f);
                var light=new GameObject("Review sun").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.4f;light.transform.rotation=Quaternion.Euler(40,-30,0);
                var ground=GameObject.CreatePrimitive(PrimitiveType.Plane);ground.transform.localScale=Vector3.one*2;ground.GetComponent<Renderer>().sharedMaterial=PlayerView.Material(new Color(.1f,.3f,.17f));
                var camera=new GameObject("Review camera").AddComponent<Camera>();camera.transform.position=new Vector3(3.1f,2.0f,4.7f);camera.transform.LookAt(new Vector3(0,.95f,.2f));camera.fieldOfView=34;camera.backgroundColor=new Color(.08f,.10f,.12f);camera.clearFlags=CameraClearFlags.SolidColor;
                var target=new RenderTexture(1280,900,24);target.antiAliasing=2;target.Create();camera.targetTexture=target;camera.aspect=1280f/900;
                var names=new[]{"central-low","central-mid","central-chest","central-high","lateral-left","lateral-right"};
                bool animation=Environment.GetCommandLineArgs().Contains("-touchlineCompactAnimation");
                var heights=new[]{.22f,.6f,1.1f,1.95f,.6f,1.1f};var samples=new List<Sample>();
                for(int scenario=0;scenario<names.Length;scenario++){
                    var go=new GameObject(names[scenario]);var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="compact-review",heightCm=182},0,0,Color.yellow);
                    float lateral=scenario<4?.2f:scenario==4?-1.1f:1.1f,forward=scenario<4?.65f:.3f;
                    var actor=new Actor{slot=0,action="idle"};view.Render(actor,1,.01f);
                    actor.action="dive";actor.actionKind="save-catch";actor.actionSequence=1;actor.actionContactTime=.2f;actor.actionTarget=new Point(lateral,forward);actor.actionHeight=heights[scenario];actor.diveSide=lateral<0?-1:1;
                    var ball=GameObject.CreatePrimitive(PrimitiveType.Sphere);ball.transform.localScale=Vector3.one*.22f;ball.GetComponent<Renderer>().sharedMaterial=PlayerView.Material(Color.white);
                    var impact=new Vector3(lateral,heights[scenario],forward);
                    for(int frame=0;frame<=120;frame++){
                        float time=frame*.01f;actor.actionTime=1.2f-time;view.Render(actor,1,.01f,impact);
                        ball.transform.position=time<.2f?impact+Vector3.forward*(.2f-time)*8:view.HeldBallPosition;
                        if(frame!=10&&frame!=20&&frame!=40&&frame!=70&&frame!=120&&!(animation&&frame%4==0))continue;
                        string file=names[scenario]+"-"+frame.ToString("D3")+".png";
                        Capture(camera,target,view,Path.Combine(output,file));
                        float error=Vector3.Distance(view.HeldBallPosition,impact),angle=Quaternion.Angle(go.transform.Find("Rig").localRotation,Quaternion.identity);
                        if(frame==20&&error>.17f)throw new Exception("Contact missed "+names[scenario]+": "+error);
                        if(scenario<4&&angle>35)throw new Exception("Central catch became a dive");
                        samples.Add(new Sample{file=file,scenario=names[scenario],time=time,contactError=error,bodyAngle=angle,leftFootHeight=view.FootPosition(true).y,rightFootHeight=view.FootPosition(false).y});
                    }
                    UnityEngine.Object.DestroyImmediate(go);UnityEngine.Object.DestroyImmediate(ball);
                }
                File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(new Report{passed=true,samples=samples.ToArray()},true));Debug.Log("TOUCHLINE_KEEPER_COMPACT_OK");EditorApplication.Exit(0);
            }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}

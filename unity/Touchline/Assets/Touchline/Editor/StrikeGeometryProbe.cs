using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Touchline.Core;
namespace Touchline.Editor
{
    public static class StrikeGeometryProbe
    {
        public static void Run(){
            string output=Environment.GetCommandLineArgs().First(a=>a.StartsWith("-strikeOutput=")).Substring("-strikeOutput=".Length);
            if(Directory.Exists(output))throw new Exception("Proof exists");Directory.CreateDirectory(output);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var ground=GameObject.CreatePrimitive(PrimitiveType.Plane);ground.GetComponent<Renderer>().sharedMaterial=PlayerView.Material(new Color(.08f,.2f,.1f));
            var light=new GameObject("Light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.3f;light.transform.rotation=Quaternion.Euler(45,30,0);RenderSettings.ambientLight=Color.gray;
            var camera=new GameObject("Camera").AddComponent<Camera>();camera.fieldOfView=35;camera.backgroundColor=new Color(.04f,.06f,.08f);camera.clearFlags=CameraClearFlags.SolidColor;
            camera.transform.position=new Vector3(2,1.4f,3);camera.transform.LookAt(Vector3.up*.8f);
            var target=new RenderTexture(960,960,24);camera.targetTexture=target;
            var ball=new Vector3(0,.11f,.42f);var marker=GameObject.CreatePrimitive(PrimitiveType.Sphere);marker.transform.position=ball;marker.transform.localScale=Vector3.one*.22f;
            var report=new System.Text.StringBuilder();
            foreach(int height in new[]{160,205})foreach(bool left in new[]{false,true})foreach(string kind in new[]{"shot","pass"}){
                var go=new GameObject("Strike geometry");var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="geometry",heightCm=height,preferredFoot=left?"Left":"Right"},0,9,Color.white);
                var actor=new Actor{slot=9,action="kick",actionKind=kind,actionSequence=1,actionTarget=new Point(ball.x,ball.z),actionHeight=ball.y};
                for(int frame=0;frame<=18;frame++){
                    actor.actionTime=.64f-frame*.01f;view.Render(actor,1,.01f,ball);
                }
                report.AppendLine($"height={height} left={left} kind={kind} clip={view.MecanimGestureClip}");
                MecanimPrototypeFilm.Capture(camera,target,go,Path.Combine(output,$"{height}-{left}-{kind}.jpg"));
                UnityEngine.Object.DestroyImmediate(go);
            }
            File.WriteAllText(Path.Combine(output,"captured.txt"),report.ToString()+"Static impact poses; no match trajectory or Android validation.");
            camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(target);EditorApplication.Exit(0);
        }
    }
}

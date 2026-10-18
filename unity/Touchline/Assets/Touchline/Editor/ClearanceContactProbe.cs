using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Touchline.Core;
namespace Touchline.Editor
{
    public static class ClearanceContactProbe
    {
        public static void Run(){
            string output=Environment.GetCommandLineArgs().First(a=>a.StartsWith("-clearanceOutput=")).Substring("-clearanceOutput=".Length);
            if(Directory.Exists(output))throw new Exception("Proof exists");Directory.CreateDirectory(output);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var ground=GameObject.CreatePrimitive(PrimitiveType.Plane);ground.GetComponent<Renderer>().sharedMaterial=PlayerView.Material(new Color(.08f,.2f,.1f));
            var light=new GameObject("Light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.3f;light.transform.rotation=Quaternion.Euler(45,30,0);RenderSettings.ambientLight=Color.gray;
            var camera=new GameObject("Camera").AddComponent<Camera>();camera.fieldOfView=35;camera.backgroundColor=new Color(.04f,.06f,.08f);camera.clearFlags=CameraClearFlags.SolidColor;
            var rt=new RenderTexture(960,960,24);camera.targetTexture=rt;
            var report=new System.Text.StringBuilder();
            foreach(bool left in new[]{false,true}){
                var go=new GameObject("Defender");var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="retreat-clear",heightCm=182,preferredFoot=left?"Left":"Right"},0,2,Color.white);
                var actor=new Actor{slot=2,action="run",intent="mark",velocity=new Point(0,-2),angle=Mathf.PI};
                for(int i=0;i<60;i++){actor.previous=actor.position;actor.position+=actor.velocity/60;view.Render(actor,1,1f/60,new Vector3(0,.11f,9),new PlayerMotionContext{defending=true});}
                actor.previous=actor.position;actor.velocity=new Point();actor.action="kick";actor.actionKind="clearance";actor.actionSequence=1;actor.angle=0;
                var ball=new Vector3(actor.position.x,.11f,actor.position.z+.42f);actor.actionTarget=new Point(ball.x,ball.z);actor.actionHeight=.11f;
                var marker=GameObject.CreatePrimitive(PrimitiveType.Sphere);marker.transform.position=ball;marker.transform.localScale=Vector3.one*.22f;marker.GetComponent<Renderer>().sharedMaterial=PlayerView.Material(Color.white);
                camera.transform.position=new Vector3(2,1.5f,actor.position.z+3.5f);camera.transform.LookAt(new Vector3(0,.85f,actor.position.z));
                for(int frame=0;frame<=70;frame++){
                    actor.actionTime=Mathf.Max(0,.64f-frame*.01f);if(frame>=64){actor.action="run";actor.actionKind="";}view.Render(actor,1,.01f,ball);
                    if(frame==18){float error=Vector3.Distance(view.BootContactPosition(left),ball);report.AppendLine($"left={left} contactMetres={error}");if(error>.06f)throw new Exception("Contact regression");}
                    if(frame!=0&&frame!=9&&frame!=18&&frame!=35&&frame!=60&&frame!=63&&frame!=64&&frame!=65&&frame!=70)continue;
                    MecanimPrototypeFilm.Capture(camera,rt,go,Path.Combine(output,(left?"left":"right")+"-"+frame+".jpg"));
                }
                UnityEngine.Object.DestroyImmediate(marker);UnityEngine.Object.DestroyImmediate(go);
            }
            File.WriteAllText(Path.Combine(output,"passed.txt"),report.ToString()+"Ball marker remains at impact for pose review; this is not a ball-trajectory simulation.");
            camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(rt);EditorApplication.Exit(0);
        }
    }
}

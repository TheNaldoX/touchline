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
            bool approach=Environment.GetCommandLineArgs().Contains("-strikeApproach");
            bool authoritative=Environment.GetCommandLineArgs().Contains("-strikeAuthoritative");
            if(approach){
                foreach(int approachFrames in new[]{45,50,55,60})foreach(bool left in new[]{false,true}){
                    var go=new GameObject("Approach support diagnostic");var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="support-continuity",heightCm=182,preferredFoot=left?"Left":"Right"},0,9,Color.white);
                    var actor=new Actor{id="support-continuity",slot=9,action="run",velocity=new Point(0,3.5f)};
                    for(int frame=0;frame<approachFrames;frame++){
                        if(authoritative){
                            float time=(frame+1)/60f,end=(Mathf.Floor(time/MatchSimulation.Step)+1)*MatchSimulation.Step;
                            actor.previous=actor.velocity*(end-MatchSimulation.Step);actor.position=actor.velocity*end;
                            view.Render(actor,(time-end+MatchSimulation.Step)/MatchSimulation.Step,1f/60);
                        }else{actor.previous=actor.position;actor.position+=actor.velocity/60;view.Render(actor,1,1f/60);}
                    }
                    if(authoritative)actor.position=actor.velocity*(approachFrames/60f);
                    var support=view.FootPosition(!left);var offset=new Vector3(actor.position.x,0,actor.position.z);
                    camera.transform.position=offset+new Vector3(2,1.4f,3);camera.transform.LookAt(offset+Vector3.up*.8f);
                    ball=offset+new Vector3(0,.11f,.42f);marker.transform.position=ball;
                    var supportBone=go.GetComponentsInChildren<Transform>().Single(t=>t.name==(!left?"foot.L":"foot.R"));
                    report.AppendLine($"approachFrames={approachFrames} left={left} frame=approach ankleY={support.y:F4} soleY={SoleHeight(supportBone):F4}");
                    MecanimPrototypeFilm.Capture(camera,target,go,Path.Combine(output,$"approach-{approachFrames}-{left}.jpg"));
                    actor.previous=actor.position;actor.velocity=new Point();actor.action="kick";actor.actionKind="pass";actor.actionSequence=1;actor.actionTarget=new Point(ball.x,ball.z);actor.actionHeight=ball.y;
                    for(int frame=0;frame<=18;frame++){
                        actor.actionTime=.64f-frame*.01f;view.Render(actor,1,.01f,ball);
                        var foot=view.FootPosition(!left);report.AppendLine($"approachFrames={approachFrames} left={left} frame={frame} ankleY={foot.y:F4} soleY={SoleHeight(supportBone):F4} planarDelta={Vector3.ProjectOnPlane(foot-support,Vector3.up).magnitude:F4}");
                        if(frame==0||frame==9||frame==18)MecanimPrototypeFilm.Capture(camera,target,go,Path.Combine(output,$"approach-{approachFrames}-{left}-{frame}.jpg"));
                    }
                    report.AppendLine($"approachFrames={approachFrames} left={left} contactError={Vector3.Distance(view.InsideFootContactPosition(left),ball):F4}");
                    if(actor.position.x!=offset.x||actor.position.z!=offset.z)throw new Exception("Rendering changed the actor position");
                    UnityEngine.Object.DestroyImmediate(go);
                }
                File.WriteAllText(Path.Combine(output,"captured.txt"),report.ToString()+$"Authoritative 10 Hz snapshots={authoritative}; sole measured from actual boot vertices. No runtime correction or Fold validation.");
                camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(target);EditorApplication.Exit(0);return;
            }
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
        static float SoleHeight(Transform foot)=>FootballBootMesh.Shared.vertices.Min(v=>foot.TransformPoint(v).y);
    }
}

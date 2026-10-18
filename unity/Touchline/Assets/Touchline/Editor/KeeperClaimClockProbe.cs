using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Touchline.Core;
namespace Touchline.Editor
{
    public static class KeeperClaimClockProbe
    {
        public static void Run(){
            string output=Environment.GetCommandLineArgs().First(a=>a.StartsWith("-claimClockOutput=")).Substring("-claimClockOutput=".Length);
            if(Directory.Exists(output))throw new Exception("Proof exists");Directory.CreateDirectory(output);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var ground=GameObject.CreatePrimitive(PrimitiveType.Plane);ground.GetComponent<Renderer>().sharedMaterial=PlayerView.Material(new Color(.08f,.2f,.1f));
            var light=new GameObject("Light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.3f;light.transform.rotation=Quaternion.Euler(45,30,0);RenderSettings.ambientLight=Color.gray;
            var camera=new GameObject("Camera").AddComponent<Camera>();camera.fieldOfView=38;camera.backgroundColor=new Color(.04f,.06f,.08f);camera.clearFlags=CameraClearFlags.SolidColor;
            camera.transform.position=new Vector3(2.6f,1.7f,3.5f);camera.transform.LookAt(Vector3.up*1.1f);
            var target=new RenderTexture(960,960,24);camera.targetTexture=target;
            var marker=GameObject.CreatePrimitive(PrimitiveType.Sphere);marker.transform.localScale=Vector3.one*.22f;
            var report=new System.Text.StringBuilder("height,elapsed,heldPointErrorMetres\n");
            foreach(float height in new[]{1.1f,2.2f}){
                var go=new GameObject("Claim clock");var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="claim-clock",heightCm=182},0,0,Color.yellow);
                var actor=new Actor{slot=0,action="claim",actionKind=MatchSimulation.RecordedKeeperClaim,actionSequence=1,actionContactTime=.07f,actionTarget=new Point(0,.48f),actionHeight=height};
                for(int frame=0;frame<=70;frame++){
                    float elapsed=frame*.01f;actor.actionTime=MatchSimulation.KeeperClaimPresentationDuration-elapsed;
                    float gather=Mathf.SmoothStep(0,1,(elapsed-actor.actionContactTime)/MatchSimulation.KeeperClaimGatherDuration);
                    var ball=Vector3.Lerp(new Vector3(0,height,.48f),new Vector3(0,1.1f,.36f),gather);marker.transform.position=ball;
                    view.Render(actor,1,.01f,ball);
                    report.AppendLine(FormattableString.Invariant($"{height},{elapsed},{Vector3.Distance(view.HeldBallPosition,ball)}"));
                    if(frame==7||frame==30||frame==62)MecanimPrototypeFilm.Capture(camera,target,go,Path.Combine(output,FormattableString.Invariant($"height{height}-frame{frame}.jpg")));
                }
                UnityEngine.Object.DestroyImmediate(go);
            }
            File.WriteAllText(Path.Combine(output,"poses.csv"),report.ToString());
            File.WriteAllText(Path.Combine(output,"captured.txt"),"Recorded claim timeline only. High glove reach remains unresolved; no Android validation.");
            camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(target);EditorApplication.Exit(0);
        }
    }
}

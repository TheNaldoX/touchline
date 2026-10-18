using System;
using System.IO;
using System.Linq;
using System.Globalization;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Touchline.Core;
namespace Touchline.Editor
{
    public static class KeeperRecoveryProbe
    {
        public static void Run(){
            string output=Environment.GetCommandLineArgs().First(a=>a.StartsWith("-recoveryOutput=")).Substring("-recoveryOutput=".Length);
            if(Directory.Exists(output))throw new Exception("Proof exists");Directory.CreateDirectory(output);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var ground=GameObject.CreatePrimitive(PrimitiveType.Plane);ground.GetComponent<Renderer>().sharedMaterial=PlayerView.Material(new Color(.08f,.2f,.1f));
            var light=new GameObject("Light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.3f;light.transform.rotation=Quaternion.Euler(45,30,0);RenderSettings.ambientLight=Color.gray;
            var camera=new GameObject("Camera").AddComponent<Camera>();camera.fieldOfView=38;camera.backgroundColor=new Color(.04f,.06f,.08f);camera.clearFlags=CameraClearFlags.SolidColor;
            camera.transform.position=new Vector3(2.6f,1.6f,3);camera.transform.LookAt(Vector3.up*.8f);
            var target=new RenderTexture(960,960,24);camera.targetTexture=target;
            var report=new System.Text.StringBuilder("side,time,hipY,leftAnkleY,rightAnkleY\n");
            bool high=Environment.GetCommandLineArgs().Contains("-highDive");
            if(high){camera.transform.position=new Vector3(3.8f,2.6f,5);camera.transform.LookAt(Vector3.up*1.2f);}
            foreach(int side in new[]{-1,1}){
                var go=new GameObject("Recovering keeper");var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="recovering"},0,0,Color.yellow);
                var actor=new Actor{action="dive",actionKind=high?"save-catch":"",diveSide=side,actionSequence=1,actionContactTime=high?.18f:0,actionTarget=new Point(side*(high?1.1f:.8f),.3f),actionHeight=high?2.2f:.5f};
                var ball=new Vector3(actor.actionTarget.x,actor.actionHeight,actor.actionTarget.z);
                var marker=high?GameObject.CreatePrimitive(PrimitiveType.Sphere):null;
                if(marker!=null){marker.transform.position=ball;marker.transform.localScale=Vector3.one*.22f;}
                for(int frame=0;frame<=120;frame++){
                    actor.actionTime=1.2f-frame*.01f;string before=JsonUtility.ToJson(actor);view.Render(actor,1,.01f);
                    if(before!=JsonUtility.ToJson(actor))throw new Exception("Presentation changed actor");
                    var hips=view.RigRoot.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Hips);
                    report.AppendLine(string.Join(",",side.ToString(),(frame*.01f).ToString("F2",CultureInfo.InvariantCulture),hips.position.y.ToString("F6",CultureInfo.InvariantCulture),view.FootPosition(true).y.ToString("F6",CultureInfo.InvariantCulture),view.FootPosition(false).y.ToString("F6",CultureInfo.InvariantCulture)));
                    if(high&&frame==18&&Vector3.Distance(view.HeldBallPosition,ball)>.17f)throw new Exception("High dive contact missed");
                    if(frame==75||frame==90||frame==110||frame==120||high&&(frame==0||frame==9||frame==18||frame==30))MecanimPrototypeFilm.Capture(camera,target,go,Path.Combine(output,$"side{side}-frame{frame}.jpg"));
                }
                UnityEngine.Object.DestroyImmediate(go);
                if(marker!=null)UnityEngine.Object.DestroyImmediate(marker);
            }
            File.WriteAllText(Path.Combine(output,"poses.csv"),report.ToString());
            File.WriteAllText(Path.Combine(output,"passed.txt"),high?"High lateral catch contact and recovery, both sides. Fixed ball marker, no ball flight or Fold validation.":"Two existing low-dive recoveries captured. No ball/contact or Fold validation.");
            camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(target);EditorApplication.Exit(0);
        }
    }
}

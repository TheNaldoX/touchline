using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Touchline.Core;
namespace Touchline.Editor
{
    // Isolated synthetic rig review: no career, random simulation or TouchlineApp.
    public static class CarrierMarkingReview
    {
        [Serializable] class Row {public string scenario,file;public int fps;public float markerBallDot,carrierBallDot,leftFootY,rightFootY,poseDifference;}
        [Serializable] class Report {public bool passed,synthetic=true,physicalAndroid=false;public string legend="Blue: existing mark. Orange: mark-carrier. Each white sphere is its nearby ball. Constructed retreat/shuffle/hold, no contact event or physical-device FPS measurement.";public Row[] samples;}
        static void Layer(GameObject go){go.layer=29;foreach(Transform child in go.transform)Layer(child.gameObject);}
        public static void Run()
        {
            string name=Environment.GetCommandLineArgs().FirstOrDefault(a=>a.StartsWith("-touchlineCarrierOutput="))?.Substring("-touchlineCarrierOutput=".Length)??"carrier-marking-review-v1";
            if(!System.Text.RegularExpressions.Regex.IsMatch(name,"^[a-zA-Z0-9-]+$"))throw new Exception("Invalid proof name");
            string output=Path.GetFullPath("../../artifacts/unity/"+name);if(Directory.Exists(output))throw new Exception("Proof already exists");Directory.CreateDirectory(output);
            var rows=new List<Row>();
            foreach(int fps in new[]{30,60,120})foreach(string scenario in new[]{"retreat","shuffle","hold"}){
                var root=new GameObject("Carrier marking isolated review");var proxies=new List<GameObject>();var meshes=new List<Mesh>();RenderTexture target=null;
                try{
                    var views=new PlayerView[2];var actors=new Actor[2];var balls=new Vector3[2];
                    for(int i=0;i<2;i++){
                        var go=new GameObject(i==0?"Blue existing marker":"Orange carrier marker");go.transform.SetParent(root.transform);
                        views[i]=go.AddComponent<PlayerView>();views[i].Build(new PlayerData{id="carrier-review",heightCm=182},0,2,i==0?new Color(.1f,.4f,.9f):new Color(.95f,.4f,.06f));
                        var start=new Point(i==0?-1.4f:1.4f,0);
                        actors[i]=new Actor{slot=2,action=scenario=="hold"?"idle":"run",intent=i==0?"mark":"mark-carrier",angle=Mathf.PI,position=start,previous=start,velocity=scenario=="retreat"?new Point(0,-2):scenario=="shuffle"?new Point(2,0):new Point()};
                        balls[i]=new Vector3(start.x,.11f,7);
                        var sphere=GameObject.CreatePrimitive(PrimitiveType.Sphere);sphere.transform.SetParent(root.transform);sphere.transform.position=balls[i];sphere.transform.localScale=Vector3.one*.22f;sphere.GetComponent<Renderer>().sharedMaterial=PlayerView.Material(Color.white);
                    }
                    for(int frame=0;frame<fps;frame++)for(int i=0;i<2;i++){
                        var actor=actors[i];actor.previous=actor.position;actor.position+=actor.velocity/fps;string before=JsonUtility.ToJson(actor);
                        views[i].Render(actor,1,1f/fps,balls[i],new PlayerMotionContext{defending=true});if(before!=JsonUtility.ToJson(actor))throw new Exception("Presentation mutated Actor");
                    }
                    var first=views[0].GetComponentInChildren<SkinnedMeshRenderer>().bones;var second=views[1].GetComponentInChildren<SkinnedMeshRenderer>().bones;float difference=0;
                    for(int k=0;k<first.Length;k++)difference=Mathf.Max(difference,Vector3.Distance(first[k].position-views[0].transform.position,second[k].position-views[1].transform.position));
                    var row=new Row{scenario=scenario,fps=fps,markerBallDot=Vector3.Dot(views[0].transform.forward,Vector3.ProjectOnPlane(balls[0]-views[0].transform.position,Vector3.up).normalized),carrierBallDot=Vector3.Dot(views[1].transform.forward,Vector3.ProjectOnPlane(balls[1]-views[1].transform.position,Vector3.up).normalized),leftFootY=views[1].FootPosition(true).y,rightFootY=views[1].FootPosition(false).y,poseDifference=difference};
                    if(fps==60){
                        var ground=GameObject.CreatePrimitive(PrimitiveType.Plane);ground.transform.SetParent(root.transform);ground.transform.localScale=Vector3.one*2;ground.GetComponent<Renderer>().sharedMaterial=PlayerView.Material(new Color(.1f,.22f,.12f));
                        var light=new GameObject("Review light").AddComponent<Light>();light.transform.SetParent(root.transform);light.type=LightType.Directional;light.intensity=1.3f;light.transform.rotation=Quaternion.Euler(45,-25,0);light.cullingMask=1<<29;
                        var camera=new GameObject("Review camera").AddComponent<Camera>();camera.transform.SetParent(root.transform);camera.enabled=false;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.05f,.07f,.09f);camera.cullingMask=1<<29;camera.fieldOfView=48;
                        var center=(views[0].transform.position+views[1].transform.position)*.5f;camera.transform.position=center+new Vector3(5,5.5f,9);camera.transform.LookAt(center+new Vector3(0,.4f,2));
                        Layer(root);target=new RenderTexture(1120,840,24);target.Create();camera.targetTexture=target;camera.aspect=4f/3;
                        foreach(var view in views)foreach(var skin in view.GetComponentsInChildren<SkinnedMeshRenderer>()){
                            var mesh=new Mesh();skin.BakeMesh(mesh);meshes.Add(mesh);var proxy=new GameObject("Exact measured pose");proxy.layer=29;proxy.transform.SetParent(skin.transform,false);proxy.AddComponent<MeshFilter>().sharedMesh=mesh;proxy.AddComponent<MeshRenderer>().sharedMaterials=skin.sharedMaterials;proxies.Add(proxy);skin.enabled=false;
                        }
                        var previous=RenderTexture.active;Texture2D image=null;try{camera.Render();RenderTexture.active=target;image=new Texture2D(1120,840,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1120,840),0,0);image.Apply();row.file=scenario+"-60fps.png";File.WriteAllBytes(Path.Combine(output,row.file),image.EncodeToPNG());}finally{RenderTexture.active=previous;if(image!=null)UnityEngine.Object.DestroyImmediate(image);}
                    }
                    rows.Add(row);
                }finally{foreach(var proxy in proxies)UnityEngine.Object.DestroyImmediate(proxy);foreach(var mesh in meshes)UnityEngine.Object.DestroyImmediate(mesh);if(target!=null){target.Release();UnityEngine.Object.DestroyImmediate(target);}UnityEngine.Object.DestroyImmediate(root);}
            }
            bool passed=rows.All(r=>r.carrierBallDot>.98f&&r.markerBallDot>.98f&&r.poseDifference<.0002f&&r.leftFootY>-.025f&&r.rightFootY>-.025f);
            File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(new Report{passed=passed,samples=rows.ToArray()},true));Debug.Log("TOUCHLINE_CARRIER_MARKING_REVIEW_"+(passed?"OK":"FAILED"));EditorApplication.Exit(passed?0:1);
        }
    }
}

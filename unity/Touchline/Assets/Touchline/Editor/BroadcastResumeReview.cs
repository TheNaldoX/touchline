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
    public static class BroadcastResumeReview
    {
        [Serializable] class Shot { public string file; public int width,height,end; public float cameraDrift; public Vector3 ballViewport; }
        [Serializable] class Report { public bool passed,physicalAndroid=false; public string limitation="Native paused synthetic fixtures. No device performance claim."; public Shot[] scenes; }
        static void Capture(Camera camera,RenderTexture target,GameObject root,string path)
        {
            var skins=new List<SkinnedMeshRenderer>();var proxies=new List<GameObject>();var meshes=new List<Mesh>();var previous=RenderTexture.active;Texture2D image=null;
            try{
                foreach(var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>()){
                    if(!skin.enabled||!skin.gameObject.activeInHierarchy)continue;
                    var mesh=new Mesh();skin.BakeMesh(mesh,true);meshes.Add(mesh);var proxy=new GameObject("Native current skin");proxy.layer=skin.gameObject.layer;proxy.transform.SetParent(skin.transform,false);proxy.AddComponent<MeshFilter>().sharedMesh=mesh;proxy.AddComponent<MeshRenderer>().sharedMaterials=skin.sharedMaterials;proxies.Add(proxy);skins.Add(skin);skin.enabled=false;
                }
                camera.Render();RenderTexture.active=target;image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());
            }finally{foreach(var skin in skins)skin.enabled=true;foreach(var proxy in proxies)UnityEngine.Object.DestroyImmediate(proxy);foreach(var mesh in meshes)UnityEngine.Object.DestroyImmediate(mesh);if(image!=null)UnityEngine.Object.DestroyImmediate(image);RenderTexture.active=previous;}
        }
        public static void Run()
        {
            string[] keys={"match-camera-mode","match-live-speed","match-camera-zoom"};var exists=keys.Select(PlayerPrefs.HasKey).ToArray();int mode=PlayerPrefs.GetInt(keys[0],0),speed=PlayerPrefs.GetInt(keys[1],1);float zoom=PlayerPrefs.GetFloat(keys[2],1);
            bool passed=false;
            try{
                const string prefix="-touchlineCameraOutput=";var name=Environment.GetCommandLineArgs().FirstOrDefault(a=>a.StartsWith(prefix))?.Substring(prefix.Length)??"camera-resume-native-v1";
                if(!System.Text.RegularExpressions.Regex.IsMatch(name,"^[a-zA-Z0-9-]+$"))throw new Exception("Invalid output name");
                string output=Path.GetFullPath("../../artifacts/unity/"+name);if(Directory.Exists(output))throw new Exception("Preserve earlier proofs");Directory.CreateDirectory(output);
                PlayerPrefs.SetInt(keys[0],0);PlayerPrefs.SetInt(keys[1],1);PlayerPrefs.SetFloat(keys[2],1);
                var db=JsonUtility.FromJson<Database>(Resources.Load<TextAsset>("Data/database").text);var clubs=db.clubs.Where(c=>c.playable).Take(2).ToArray();var shots=new List<Shot>();
                foreach(var size in new[]{new Vector2Int(432,1000),new Vector2Int(1280,970),new Vector2Int(1448,400)})foreach(int end in new[]{-1,1}){
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                    var career=new Career{club=clubs[0].id};career.lineup=Career.Select(db,career.club,career.tactic);var sim=MatchSimulation.Create(db,career,clubs[1].id);var state=sim.State;
                    state.clock=1800;state.restart=0;state.phase="play";state.remainder=0;var p=new Point(end*47,end*18);state.ball=new BallState{position=p,previous=p,height=.11f,previousHeight=.11f};
                    // Put nearby actors in view while preserving their identities.
                    for(int i=0;i<state.actors.Length;i++){var a=state.actors[i];a.position=a.previous=new Point(end*(25+(i%6)*4),-20+(i/6)*12);a.velocity=new Point();a.action="idle";}
                    var root=new GameObject("Paused resumed match");var arena=root.AddComponent<MatchArena>();arena.Initialize(db,sim);arena.enabled=false;arena.Paused=true;arena.Broadcast.Enabled=false;
                    var target=new RenderTexture(size.x,size.y,24){antiAliasing=2};target.Create();var camera=arena.MatchCamera;camera.targetTexture=target;camera.aspect=(float)size.x/size.y;
                    arena.RenderFrame(1f/60);var first=camera.transform.position;var viewport=camera.WorldToViewportPoint(arena.BallDisplayPosition);string file=$"resume-{size.x}x{size.y}-{end}.png";Capture(camera,target,root,Path.Combine(output,file));
                    for(int i=0;i<240;i++)arena.RenderFrame(1f/60);float drift=Vector3.Distance(first,camera.transform.position);
                    if(drift>.25f||viewport.x<.1f||viewport.x>.9f||viewport.y<.1f||viewport.y>.9f||state.clock!=1800)throw new Exception($"Resume framing failed: {size}, end={end}, drift={drift}, ball={viewport}, clock={state.clock}");
                    shots.Add(new Shot{file=file,width=size.x,height=size.y,end=end,cameraDrift=drift,ballViewport=viewport});
                    arena.Zoom(-.15f);
                    for(int frame=0;frame<=120;frame++){
                        var before=camera.transform.position;arena.ReframeCamera(frame==0?0:1f/60);
                        if(Vector3.Distance(before,camera.transform.position)>8f/60+.002f)throw new Exception("Manual zoom moved too quickly");
                        if(frame==0||frame==30||frame==120)Capture(camera,target,root,Path.Combine(output,$"zoom-{size.x}x{size.y}-{end}-{frame}.png"));
                    }
                    if(Environment.GetCommandLineArgs().Contains("-touchlineCameraTracking")){
                        arena.Speed=10;
                        for(int frame=0;frame<160;frame++){
                            float time=frame/60f*arena.Speed;var point=new Point(end*Mathf.Sin(time*.55f)*49,Mathf.Sin(time*.85f)*32);
                            state.ball.position=state.ball.previous=point;arena.RenderFrame(1f/60);
                            var projected=camera.WorldToViewportPoint(arena.BallDisplayPosition);
                            if(projected.z<=0||projected.x<.1f||projected.x>.9f||projected.y<.1f||projected.y>.9f)throw new Exception("Tracking lost the ball");
                            if(frame%30==0)Capture(camera,target,root,Path.Combine(output,$"tracking-{size.x}x{size.y}-{end}-{frame}.png"));
                        }
                    }
                    camera.targetTexture=null;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(root);
                }
                File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(new Report{passed=true,scenes=shots.ToArray()},true));Debug.Log("TOUCHLINE_CAMERA_RESUME_OK");passed=true;
            }catch(Exception error){Debug.LogException(error);}
            finally{
                if(exists[0])PlayerPrefs.SetInt(keys[0],mode);else PlayerPrefs.DeleteKey(keys[0]);if(exists[1])PlayerPrefs.SetInt(keys[1],speed);else PlayerPrefs.DeleteKey(keys[1]);if(exists[2])PlayerPrefs.SetFloat(keys[2],zoom);else PlayerPrefs.DeleteKey(keys[2]);
            }
            EditorApplication.Exit(passed?0:1);
        }
    }
}

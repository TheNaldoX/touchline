using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Touchline.Core;

namespace Touchline.Editor
{
    // Tourne une courte séquence de match dans GitHub Actions (Unity en mode batch,
    // rendu logiciel sous xvfb) pour juger les animations sans téléphone.
    // Images JPEG à 30 i/s dans build/film/<nom>/{broadcast,follow}/, encodées
    // ensuite en vidéo par le workflow. Paramètres : -touchlineFilm=nom,
    // -touchlineFilmStart=secondes de jeu avant de filmer, -touchlineFilmSeconds,
    // -touchlineFilmHome / -touchlineFilmAway (identifiants de clubs), -touchlineFilmSeed.
    public static class CiMatchFilm
    {
        const int Fps=30,Width=960,Height=540;
        static string Arg(string key,string fallback)=>Environment.GetCommandLineArgs().FirstOrDefault(a=>a.StartsWith(key+"="))?.Substring(key.Length+1)??fallback;

        public static void Run()
        {
            try{
                string name=Arg("-touchlineFilm","film");if(!System.Text.RegularExpressions.Regex.IsMatch(name,"^[a-zA-Z0-9.-]+$"))throw new Exception("Nom de film invalide");
                float start=float.Parse(Arg("-touchlineFilmStart","95"),System.Globalization.CultureInfo.InvariantCulture);
                float seconds=Mathf.Clamp(float.Parse(Arg("-touchlineFilmSeconds","15"),System.Globalization.CultureInfo.InvariantCulture),1,40);
                uint seed=uint.Parse(Arg("-touchlineFilmSeed","731"));
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var db=JsonUtility.FromJson<Database>(Resources.Load<TextAsset>("Data/database").text);
                string home=Arg("-touchlineFilmHome","176"),away=Arg("-touchlineFilmAway","160");
                if(db.Find(db.Squad(home).FirstOrDefault()?.id??"")==null||!db.Squad(away).Any())throw new Exception("Clubs introuvables : "+home+" / "+away);
                var career=new Career{club=home};career.lineup=Career.Select(db,home,career.tactic);
                var sim=MatchSimulation.Create(db,career,away,seed,2700);sim.State.professionalRules=true;sim.State.venueSide=0;
                // Avance la partie jusqu'au moment filmé (pas de rendu pendant ce temps).
                while(sim.State.clock<start&&!sim.State.halfTime&&!sim.State.finished)sim.Advance(MatchSimulation.Step);
                var output=Path.GetFullPath(Path.Combine("build","film",name));if(Directory.Exists(output))Directory.Delete(output,true);
                var broadcastDir=Path.Combine(output,"broadcast");var followDir=Path.Combine(output,"follow");Directory.CreateDirectory(broadcastDir);Directory.CreateDirectory(followDir);
                var root=new GameObject("CI match film");var arena=root.AddComponent<MatchArena>();arena.Initialize(db,sim);arena.Speed=1;arena.Paused=false;arena.Broadcast.SetMode(MatchViewingMode.Full);
                var target=new RenderTexture(Width,Height,24){antiAliasing=2};target.Create();
                arena.MatchCamera.targetTexture=target;arena.MatchCamera.aspect=(float)Width/Height;
                // Caméra « télé » rapprochée : basse, sur le côté, qui suit l'action en douceur.
                var follow=new GameObject("Follow camera").AddComponent<Camera>();follow.enabled=false;follow.fieldOfView=28;follow.nearClipPlane=.15f;follow.farClipPlane=270;
                follow.clearFlags=CameraClearFlags.SolidColor;follow.backgroundColor=RenderSettings.fogColor;follow.targetTexture=target;follow.aspect=(float)Width/Height;
                Vector3 focus=Vector3.zero,focusVelocity=Vector3.zero;int count=Mathf.RoundToInt(seconds*Fps);
                for(int i=0;i<count;i++){
                    arena.RenderFrame(1f/Fps);
                    var desired=arena.BallDisplayPosition;desired.y=.9f;
                    var owner=sim.State.ball.owner==null?-1:Array.FindIndex(sim.State.actors,a=>a.id==sim.State.ball.owner);
                    if(owner>=0){var visual=arena.PlayerVisual(owner);if(visual!=null)desired=Vector3.Lerp(visual.transform.position+Vector3.up*.9f,desired,.3f);}
                    focus=i==0?desired:Vector3.SmoothDamp(focus,desired,ref focusVelocity,.35f,40,1f/Fps);
                    follow.transform.position=new Vector3(focus.x*.85f,5.5f,Mathf.Max(focus.z-20,-38)); // reste devant les tribunes (touche à 34 m)follow.transform.LookAt(focus);
                    Capture(arena.MatchCamera,target,root,Path.Combine(broadcastDir,"frame-"+i.ToString("D4")+".jpg"));
                    Capture(follow,target,root,Path.Combine(followDir,"frame-"+i.ToString("D4")+".jpg"));
                    if(arena.Paused)arena.Paused=false; // pas d'arrêt de diffusion pendant le tournage
                }
                File.WriteAllText(Path.Combine(output,"info.txt"),$"home={home} away={away} seed={seed} start={start} seconds={seconds} fps={Fps} clock_end={sim.State.clock:0.0} score={sim.State.score[0]}-{sim.State.score[1]}\n");
                arena.MatchCamera.targetTexture=null;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(follow.gameObject);UnityEngine.Object.DestroyImmediate(root);
                Debug.Log("TOUCHLINE_FILM_OK "+output);EditorApplication.Exit(0);
            }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }

        // Même principe que NaturalMatchFilm : les maillages animés sont figés
        // (BakeMesh) avant chaque image, car le mode batch ne les met pas à jour seul.
        static void Capture(Camera camera,RenderTexture target,GameObject root,string path)
        {
            var skins=new List<SkinnedMeshRenderer>();var proxies=new List<GameObject>();var meshes=new List<Mesh>();var previous=RenderTexture.active;Texture2D image=null;
            try{
                foreach(var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>()){
                    if(!skin.enabled||!skin.gameObject.activeInHierarchy)continue;var mesh=new Mesh();skin.BakeMesh(mesh);meshes.Add(mesh);
                    var proxy=new GameObject("Baked skin");proxy.layer=skin.gameObject.layer;proxy.transform.SetParent(skin.transform,false);
                    proxy.AddComponent<MeshFilter>().sharedMesh=mesh;proxy.AddComponent<MeshRenderer>().sharedMaterials=skin.sharedMaterials;proxies.Add(proxy);skins.Add(skin);skin.enabled=false;
                }
                camera.Render();RenderTexture.active=target;image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
                image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToJPG(88));
            }finally{
                foreach(var skin in skins)skin.enabled=true;foreach(var proxy in proxies)UnityEngine.Object.DestroyImmediate(proxy);
                foreach(var mesh in meshes)UnityEngine.Object.DestroyImmediate(mesh);if(image!=null)UnityEngine.Object.DestroyImmediate(image);RenderTexture.active=previous;
            }
        }
    }
}

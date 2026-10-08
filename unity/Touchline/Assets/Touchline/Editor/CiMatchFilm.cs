using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
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
            var log=new System.Text.StringBuilder();Application.LogCallback hook=(message,stack,type)=>{if(log.Length<200000)log.AppendLine(type+": "+message+(type==LogType.Exception||type==LogType.Error?"\n"+stack:""));};
            Application.logMessageReceived+=hook;string logPath=null;
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
                logPath=Path.Combine(output,"unity-messages.txt");
                var diagnostics=new System.Text.StringBuilder();
                diagnostics.AppendLine("pipeline="+(UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline!=null?UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline.name:"none")+" quality="+(QualitySettings.renderPipeline!=null?QualitySettings.renderPipeline.name:"none"));
                var broadcastDir=Path.Combine(output,"broadcast");var followDir=Path.Combine(output,"follow");Directory.CreateDirectory(broadcastDir);Directory.CreateDirectory(followDir);
                // -touchlineLighting=night : éclairage de soirée (projecteurs) ; sinon après-midi.
                PlayerPrefs.SetInt(StadiumLighting.PreferenceKey,Arg("-touchlineLighting","day")=="night"?StadiumLighting.Night:StadiumLighting.Day);
                var root=new GameObject("CI match film");var arena=root.AddComponent<MatchArena>();arena.Initialize(db,sim);PlayerView.UseMecanim=Arg("-touchlineMecanim","1")=="1";arena.Speed=1;arena.Paused=false;arena.Broadcast.SetMode(MatchViewingMode.Full);
                var target=new RenderTexture(Width,Height,24){antiAliasing=4};target.Create(); // même MSAA que TouchlineURP (4×)
                arena.MatchCamera.targetTexture=target;arena.MatchCamera.aspect=(float)Width/Height;
                {var probe=new GameObject("Probe camera").AddComponent<Camera>();probe.enabled=false;probe.clearFlags=CameraClearFlags.SolidColor;probe.backgroundColor=Color.red;probe.cullingMask=0;probe.targetTexture=target;
                 Capture(probe,target,root,Path.Combine(output,"probe-red.jpg"));diagnostics.AppendLine("probe-red mean="+MeanColor(target));UnityEngine.Object.DestroyImmediate(probe.gameObject);}
                // Caméra « télé » rapprochée : basse, sur le côté, qui suit l'action en douceur.
                var follow=new GameObject("Follow camera").AddComponent<Camera>();follow.enabled=false;follow.fieldOfView=32;follow.nearClipPlane=.15f;follow.farClipPlane=270;
                follow.clearFlags=CameraClearFlags.SolidColor;follow.backgroundColor=RenderSettings.fogColor;follow.targetTexture=target;follow.aspect=(float)Width/Height;
                // Même post-traitement que la caméra de diffusion (étalonnage BroadcastGrade).
                follow.GetUniversalAdditionalCameraData().renderPostProcessing=arena.MatchCamera.GetUniversalAdditionalCameraData().renderPostProcessing;
                Vector3 focus=Vector3.zero,focusVelocity=Vector3.zero;int count=Mathf.RoundToInt(seconds*Fps);
                // Mesures de fluidité sur les 22 joueurs (rendu à 30 i/s) :
                // glissement d'un pied posé (m/s), à-coup de trajectoire (variation
                // d'accélération, m/s³) et vitesse de rotation (°/s).
                var lastRoot=new Vector3[22];var lastVelocity=new Vector3[22];var lastAcceleration=new Vector3[22];var lastYaw=new float[22];var lastFeet=new Vector3[44];
                var slides=new List<float>();var slidesByAction=new Dictionary<string,List<float>>();var yawByAction=new Dictionary<string,List<float>>();var jerks=new List<float>();var yawRates=new List<float>();float dt=1f/Fps;
                for(int i=0;i<count;i++){
                    arena.RenderFrame(1f/Fps);
                    for(int k=0;k<22;k++){var v=arena.PlayerVisual(k);if(v==null||sim.State.actors[k].sentOff)continue;var rootPosition=v.transform.position;float yaw=v.transform.eulerAngles.y;
                        for(int f=0;f<2;f++){var foot=v.FootPosition(f==0);if(i>1&&foot.y<.12f&&lastFeet[k*2+f].y<.12f){var d=foot-lastFeet[k*2+f];d.y=0;slides.Add(d.magnitude/dt);var key=sim.State.actors[k].action;if(!slidesByAction.TryGetValue(key,out var list))slidesByAction[key]=list=new List<float>();list.Add(d.magnitude/dt);}lastFeet[k*2+f]=foot;}
                        var velocity=(rootPosition-lastRoot[k])/dt;var acceleration=(velocity-lastVelocity[k])/dt;
                        if(i>3&&(rootPosition-lastRoot[k]).magnitude<.5f){jerks.Add((acceleration-lastAcceleration[k]).magnitude/dt);yawRates.Add(Mathf.Abs(Mathf.DeltaAngle(lastYaw[k],yaw))/dt);var key=sim.State.actors[k].action;if(!yawByAction.TryGetValue(key,out var list))yawByAction[key]=list=new List<float>();list.Add(Mathf.Abs(Mathf.DeltaAngle(lastYaw[k],yaw))/dt);}
                        lastRoot[k]=rootPosition;lastVelocity[k]=velocity;lastAcceleration[k]=acceleration;lastYaw[k]=yaw;}
                    var desired=arena.BallDisplayPosition;desired.y=.9f;
                    var owner=sim.State.ball.owner==null?-1:Array.FindIndex(sim.State.actors,a=>a.id==sim.State.ball.owner);
                    if(owner>=0){var visual=arena.PlayerVisual(owner);if(visual!=null)desired=Vector3.Lerp(visual.transform.position+Vector3.up*.9f,desired,.3f);}
                    focus=i==0?desired:Vector3.SmoothDamp(focus,desired,ref focusVelocity,.35f,40,1f/Fps);
                    follow.transform.position=new Vector3(focus.x,3.4f,Mathf.Max(focus.z-12,-40));follow.transform.LookAt(focus+Vector3.down*.2f); // caméra basse et proche, façon diffusion rapprochée
                    Capture(arena.MatchCamera,target,root,Path.Combine(broadcastDir,"frame-"+i.ToString("D4")+".jpg"));
                    if(i==0||i==count-1)diagnostics.AppendLine("frame "+i+" broadcast mean="+MeanColor(target)+" camera="+arena.MatchCamera.transform.position+" enabled="+arena.MatchCamera.enabled);
                    Capture(follow,target,root,Path.Combine(followDir,"frame-"+i.ToString("D4")+".jpg"));
                    if(arena.Paused)arena.Paused=false; // pas d'arrêt de diffusion pendant le tournage
                }
                string Stats(List<float> values){if(values.Count==0)return "—";values.Sort();return $"moyenne {values.Average():0.00} · médiane {values[values.Count/2]:0.00} · p95 {values[(int)(values.Count*.95f)]:0.00} · max {values[values.Count-1]:0.00} (n={values.Count})";}
                File.WriteAllText(Path.Combine(output,"metrics.txt"),"pied posé, glissement (m/s) : "+Stats(slides)+"\nà-coup de trajectoire (m/s³) : "+Stats(jerks)+"\nrotation (°/s) : "+Stats(yawRates)+"\n\nglissement par action :\n"+string.Join("\n",slidesByAction.OrderByDescending(x=>x.Value.Count).Select(x=>"  "+x.Key+" : "+Stats(x.Value)))+"\n\nrotation par action :\n"+string.Join("\n",yawByAction.OrderByDescending(x=>x.Value.Count).Select(x=>"  "+x.Key+" : "+Stats(x.Value)))+"\n");
                File.WriteAllText(Path.Combine(output,"info.txt"),$"home={home} away={away} seed={seed} start={start} seconds={seconds} fps={Fps} clock_end={sim.State.clock:0.0} score={sim.State.score[0]}-{sim.State.score[1]}\nmecanim={PlayerView.UseMecanim} ready={PlayerView.MecanimReady}\ngraphics={SystemInfo.graphicsDeviceType} {SystemInfo.graphicsDeviceName} {SystemInfo.graphicsDeviceVersion}\n"+diagnostics);
                arena.MatchCamera.targetTexture=null;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(follow.gameObject);UnityEngine.Object.DestroyImmediate(root);
                Debug.Log("TOUCHLINE_FILM_OK "+output);File.WriteAllText(logPath,log.ToString());EditorApplication.Exit(0);
            }catch(Exception e){Debug.LogException(e);if(logPath!=null)File.WriteAllText(logPath,log.ToString());EditorApplication.Exit(1);}
        }

        static string MeanColor(RenderTexture target)
        {
            var previous=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();RenderTexture.active=previous;
            var pixels=image.GetPixels32();double r=0,g=0,b=0;foreach(var p in pixels){r+=p.r;g+=p.g;b+=p.b;}UnityEngine.Object.DestroyImmediate(image);
            return $"{r/pixels.Length:0}/{g/pixels.Length:0}/{b/pixels.Length:0}";
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

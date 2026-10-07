using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Touchline.Core;

namespace Touchline.Editor
{
    // Prototype Mecanim filmé dans GitHub Actions (branche film/proto-…).
    // 1. Construit l'avatar Humanoid du joueur Touchline et vérifie qu'il est valide.
    // 2. Sans animations importées : pose Humanoid neutre puis quelques poses de
    //    muscles (bras, jambes) pour vérifier le sens des articulations.
    // 3. Avec des clips dans Assets/Touchline/Animations/Mixamo : les joue à la
    //    suite sur le joueur (vue de face « broadcast », vue de profil « follow »).
    public static class MecanimPrototypeFilm
    {
        const int Fps=30,Width=960,Height=540;
        const string ClipFolder="Assets/Touchline/Animations/Mixamo";
        static string Arg(string key,string fallback)=>Environment.GetCommandLineArgs().FirstOrDefault(a=>a.StartsWith(key+"="))?.Substring(key.Length+1)??fallback;

        public static void Run()
        {
            var log=new System.Text.StringBuilder();Application.LogCallback hook=(m,st,t)=>{if(log.Length<200000)log.AppendLine(t+": "+m+(t==LogType.Exception||t==LogType.Error?"\n"+st:""));};
            Application.logMessageReceived+=hook;string output=null;
            try{
                string name=Arg("-touchlineFilm","proto");
                output=Path.GetFullPath(Path.Combine("build","film",name));if(Directory.Exists(output))Directory.Delete(output,true);
                var front=Path.Combine(output,"broadcast");var side=Path.Combine(output,"follow");Directory.CreateDirectory(front);Directory.CreateDirectory(side);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var db=JsonUtility.FromJson<Database>(Resources.Load<TextAsset>("Data/database").text);
                var player=db.Squad("176").OrderByDescending(p=>p.rating).First(p=>!p.Goalkeeper);
                var root=new GameObject("Prototype");
                var ground=GameObject.CreatePrimitive(PrimitiveType.Plane);ground.transform.SetParent(root.transform);ground.transform.localScale=Vector3.one*3;ground.GetComponent<Renderer>().sharedMaterial=PlayerView.Material(new Color(.09f,.3f,.12f));
                var sun=new GameObject("Sun").AddComponent<Light>();sun.transform.SetParent(root.transform);sun.type=LightType.Directional;sun.intensity=1.4f;sun.transform.rotation=Quaternion.Euler(45,30,0);
                RenderSettings.ambientLight=new Color(.5f,.55f,.6f);
                var go=new GameObject("Player");go.transform.SetParent(root.transform);var view=go.AddComponent<PlayerView>();view.Build(player,0,5,new Color(.15f,.25f,.6f));
                var info=new System.Text.StringBuilder();
                var avatar=view.HumanoidAvatar();
                info.AppendLine($"avatar valid={avatar.isValid} human={avatar.isHuman} sidesSwapped={PlayerView.HumanSidesSwapped}");
                var animator=view.RigRoot.gameObject.AddComponent<Animator>();animator.avatar=avatar;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                var target=new RenderTexture(Width,Height,24){antiAliasing=4};target.Create();
                var camFront=MakeCamera("Front",target);var camSide=MakeCamera("Side",target);
                int frame=0;
                void Shoot(){
                    var hips=animator.GetBoneTransform(HumanBodyBones.Hips);var focus=(hips!=null?hips.position:go.transform.position+Vector3.up)+Vector3.up*.05f;
                    camFront.transform.position=focus+new Vector3(0,.3f,4.2f);camFront.transform.LookAt(focus);
                    camSide.transform.position=focus+new Vector3(4.2f,.3f,0);camSide.transform.LookAt(focus);
                    Capture(camFront,target,root,Path.Combine(front,"frame-"+frame.ToString("D4")+".jpg"));
                    Capture(camSide,target,root,Path.Combine(side,"frame-"+frame.ToString("D4")+".jpg"));frame++;
                }
                var clips=AssetDatabase.FindAssets("t:AnimationClip",new[]{ClipFolder}).Select(AssetDatabase.GUIDToAssetPath).Distinct()
                    .SelectMany(path=>AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview__"))).ToList();
                info.AppendLine("clips="+clips.Count+(clips.Count>0?" : "+string.Join(", ",clips.Select(c=>$"{c.name} {c.length:0.00}s human={c.humanMotion}")):""));
                if(clips.Count==0){
                    // Poses de muscles : neutre, bras levés, jambe avant, accroupi.
                    var handler=new HumanPoseHandler(avatar,view.RigRoot);var pose=new HumanPose();handler.GetHumanPose(ref pose);
                    var poses=new List<(string,Action<float[]>)>{
                        ("neutre",m=>{}),
                        ("bras",m=>{Set(m,"Left Arm Down-Up",1);Set(m,"Right Arm Down-Up",1);}),
                        ("jambe-avant",m=>{Set(m,"Left Upper Leg Front-Back",.8f);Set(m,"Right Lower Leg Stretch",-.6f);}),
                        ("course",m=>{Set(m,"Left Upper Leg Front-Back",.6f);Set(m,"Right Upper Leg Front-Back",-.4f);Set(m,"Right Lower Leg Stretch",-.7f);Set(m,"Left Arm Front-Back",-.6f);Set(m,"Right Arm Front-Back",.6f);Set(m,"Left Forearm Stretch",-.5f);Set(m,"Right Forearm Stretch",-.5f);}),
                    };
                    foreach(var (label,apply) in poses){
                        var muscles=new float[HumanTrait.MuscleCount];apply(muscles);pose.muscles=muscles;pose.bodyPosition=new Vector3(0,1,0);pose.bodyRotation=Quaternion.identity;
                        handler.SetHumanPose(ref pose);go.transform.position=Vector3.zero;
                        for(int k=0;k<15;k++)Shoot();info.AppendLine("pose "+label+" frames "+(frame-15)+"-"+(frame-1));
                    }
                }else{
                    // Pose neutre d'abord (contrôle de l'avatar), puis une sélection jouée à la suite.
                    {var handler=new HumanPoseHandler(avatar,view.RigRoot);var pose=new HumanPose();handler.GetHumanPose(ref pose);pose.muscles=new float[HumanTrait.MuscleCount];pose.bodyPosition=new Vector3(0,1,0);pose.bodyRotation=Quaternion.identity;handler.SetHumanPose(ref pose);for(int k=0;k<10;k++)Shoot();info.AppendLine("pose neutre frames 0-9");}
                    string[] wanted=Arg("-touchlineClips","Jog Forward;Dribble;Kick Soccerball;Soccer Pass;Receive Soccerball;Soccer Tackle;Header Soccerball;Goalkeeper Diving Save").Split(';');
                    var selection=wanted.Select(w=>clips.FirstOrDefault(c=>string.Equals(c.name,w,StringComparison.OrdinalIgnoreCase))).Where(c=>c!=null).ToList();
                    if(selection.Count==0)selection=clips.OrderBy(c=>c.name).Take(8).ToList();
                    var graph=PlayableGraph.Create("Prototype");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                    var outputPlayable=AnimationPlayableOutput.Create(graph,"Animation",animator);
                    foreach(var clip in selection){
                        var playable=AnimationClipPlayable.Create(graph,clip);playable.SetApplyFootIK(true);outputPlayable.SetSourcePlayable(playable);
                        int n=Mathf.Clamp(Mathf.RoundToInt(Mathf.Min(clip.length*(clip.isLooping?2:1),3f)*Fps),15,90);info.AppendLine($"clip {clip.name} {clip.length:0.00}s loop={clip.isLooping} frames {frame}-{frame+n-1}");
                        for(int k=0;k<n;k++){graph.Evaluate(k==0?0:1f/Fps);go.transform.position=Vector3.zero;Shoot();}
                        playable.Destroy();
                    }
                    graph.Destroy();
                }
                File.WriteAllText(Path.Combine(output,"info.txt"),info.ToString());
                File.WriteAllText(Path.Combine(output,"metrics.txt"),"prototype Mecanim\n");
                Debug.Log("TOUCHLINE_PROTO_OK");File.WriteAllText(Path.Combine(output,"unity-messages.txt"),log.ToString());EditorApplication.Exit(0);
            }catch(Exception e){Debug.LogException(e);if(output!=null){Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"unity-messages.txt"),log.ToString());}EditorApplication.Exit(1);}
        }

        static void Set(float[] muscles,string name,float value){int i=Array.IndexOf(HumanTrait.MuscleName,name);if(i>=0)muscles[i]=value;}

        static Camera MakeCamera(string name,RenderTexture target)
        {
            var c=new GameObject(name).AddComponent<Camera>();c.enabled=false;c.fieldOfView=35;c.nearClipPlane=.1f;c.farClipPlane=100;c.clearFlags=CameraClearFlags.SolidColor;
            c.backgroundColor=new Color(.35f,.43f,.5f);c.targetTexture=target;c.aspect=(float)Width/Height;return c;
        }

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

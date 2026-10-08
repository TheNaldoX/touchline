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
        const string ClipFolder="Assets/Touchline/Resources/Animations/Mixamo";
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
                    string[] wanted=Arg("-touchlineClips","Header Soccerball (1);Kneeing Soccerball;Kneeing Soccerball (1);Soccer Pass;Soccer Pass (miroir);Sprint Turn;Sprint Turn (miroir)").Split(';');
                    var selection=wanted.Select(w=>clips.FirstOrDefault(c=>string.Equals(c.name,w,StringComparison.OrdinalIgnoreCase))).Where(c=>c!=null).ToList();
                    if(selection.Count==0)selection=clips.OrderBy(c=>c.name).Take(8).ToList();
                    // Analyse de chaque clip (60 Hz) : instant de vitesse maximale de chaque
                    // pied et de chaque main, hauteur maxi de la tête — sert à caler le contact
                    // avec le ballon sur l'instant décidé par le moteur de match.
                    {var timing=new System.Text.StringBuilder();timing.AppendLine("clip;durée;boucle;pied G pic (s);vitesse G;pied D pic (s);vitesse D;main G pic;main D pic;tête min (m);tête max (m);lacet début/fin/extrême (°);genou G haut (s@m);genou D haut (s@m);torse recul (s)");
                     var g=PlayableGraph.Create("Analyse");g.SetTimeUpdateMode(DirectorUpdateMode.Manual);var o=AnimationPlayableOutput.Create(g,"A",animator);
                     var bones=new[]{HumanBodyBones.LeftFoot,HumanBodyBones.RightFoot,HumanBodyBones.LeftHand,HumanBodyBones.RightHand};
                     foreach(var clip in clips.OrderBy(c=>c.name)){
                        var pl=AnimationClipPlayable.Create(g,clip);o.SetSourcePlayable(pl);float step=1f/60;int samples=Mathf.Max(2,Mathf.CeilToInt(clip.length/step));
                        var last=new Vector3[4];var peak=new float[4];var peakTime=new float[4];float headMin=9,headMax=0,lean=0; // lean : décalage latéral (m, +X) de la tête au point le plus bas
                        // Lacet du bassin (°, + = vers la droite) mesuré par l'axe des hanches, hauteur maxi des genoux
                        // (contrôle de la cuisse) et recul maxi du torse par rapport au bassin (amorti de la poitrine).
                        float yawStart=0,yawEnd=0,yawExtreme=0,chestBackTime=0,chestBack=9;var kneeTop=new float[2];var kneeTime=new float[2];
                        for(int k=0;k<=samples;k++){pl.SetTime(k*step);g.Evaluate(0);var hips=animator.GetBoneTransform(HumanBodyBones.Hips).position;
                            for(int b=0;b<4;b++){var pos=animator.GetBoneTransform(bones[b]).position-new Vector3(hips.x,0,hips.z);if(k>0){float v=(pos-last[b]).magnitude/step;if(v>peak[b]){peak[b]=v;peakTime[b]=k*step;}}last[b]=pos;}
                            var headPos=animator.GetBoneTransform(HumanBodyBones.Head).position;float head=headPos.y;if(head<headMin)lean=headPos.x-hips.x;headMin=Mathf.Min(headMin,head);headMax=Mathf.Max(headMax,head);
                            var across=animator.GetBoneTransform(HumanBodyBones.RightUpperLeg).position-animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg).position;var facing=Vector3.Cross(across,Vector3.up);
                            float yaw=Mathf.Atan2(facing.x,facing.z)*Mathf.Rad2Deg;if(k==0)yawStart=yaw;yawEnd=yaw;if(Mathf.Abs(Mathf.DeltaAngle(yawStart,yaw))>Mathf.Abs(yawExtreme))yawExtreme=Mathf.DeltaAngle(yawStart,yaw);
                            for(int s2=0;s2<2;s2++){float knee=animator.GetBoneTransform(s2==0?HumanBodyBones.LeftLowerLeg:HumanBodyBones.RightLowerLeg).position.y;if(knee>kneeTop[s2]){kneeTop[s2]=knee;kneeTime[s2]=k*step;}}
                            var chest=animator.GetBoneTransform(HumanBodyBones.Chest);if(chest!=null){float back=Vector3.Dot(chest.position-hips,facing.normalized);if(back<chestBack){chestBack=back;chestBackTime=k*step;}}}
                        // Vitesse naturelle (boucles) : recul moyen du pied d'appui par rapport aux hanches.
                        float natural=0,naturalX=0;if(clip.isLooping){var g2=PlayableGraph.Create("N");g2.SetTimeUpdateMode(DirectorUpdateMode.Manual);var o2=AnimationPlayableOutput.Create(g2,"N",animator);var p2=AnimationClipPlayable.Create(g2,clip);o2.SetSourcePlayable(p2);
                            var footBone=animator.GetBoneTransform(HumanBodyBones.LeftFoot);float minY=9;var ys=new List<float>();var zs=new List<float>();var xs=new List<float>();
                            for(int k=0;k<=samples;k++){p2.SetTime(k*step);g2.Evaluate(0);var h=animator.GetBoneTransform(HumanBodyBones.Hips).position;ys.Add(footBone.position.y);zs.Add(footBone.position.z-h.z);xs.Add(footBone.position.x-h.x);minY=Mathf.Min(minY,footBone.position.y);}
                            float sum=0,sumX=0;int count=0;for(int k=1;k<ys.Count;k++)if(ys[k]<minY+.02f&&ys[k-1]<minY+.02f){sum+=-(zs[k]-zs[k-1])/step;sumX+=-(xs[k]-xs[k-1])/step;count++;}natural=count>0?sum/count:0;naturalX=count>0?sumX/count:0;g2.Destroy();}
                        timing.AppendLine($"{clip.name};natural={natural:0.00};naturalX={naturalX:0.00};lean={lean:0.00};{clip.length:0.000};{clip.isLooping};{peakTime[0]:0.000};{peak[0]:0.0};{peakTime[1]:0.000};{peak[1]:0.0};{peakTime[2]:0.000};{peakTime[3]:0.000};{headMin:0.00};{headMax:0.00};{yawStart:0}/{yawEnd:0}/{yawExtreme:0};{kneeTime[0]:0.000}@{kneeTop[0]:0.00};{kneeTime[1]:0.000}@{kneeTop[1]:0.00};{chestBackTime:0.000}");
                        pl.Destroy();}
                     g.Destroy();File.WriteAllText(Path.Combine(output,"clip-timing.csv"),timing.ToString());}
                    var graph=PlayableGraph.Create("Prototype");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                    var outputPlayable=AnimationPlayableOutput.Create(graph,"Animation",animator);
                    foreach(var clip in selection){
                        var playable=AnimationClipPlayable.Create(graph,clip);playable.SetApplyFootIK(true);outputPlayable.SetSourcePlayable(playable);
                        int n=Mathf.Clamp(Mathf.RoundToInt(Mathf.Min(clip.length*(clip.isLooping?2:1),3f)*Fps),15,90);info.AppendLine($"clip {clip.name} {clip.length:0.00}s loop={clip.isLooping} frames {frame}-{frame+n-1}");
                        for(int k=0;k<n;k++){graph.Evaluate(k==0?0:1f/Fps);go.transform.position=Vector3.zero;Shoot();}
                        playable.Destroy();
                    }
                    graph.Destroy();
                    // Scénarios de match simulés (pas de 0,1 s du moteur, rendu à 30 i/s) joués par
                    // PlayerView comme en match : virages serrés, amortis, tacle debout, contrôle en
                    // pivotant. Glissement des pieds posés mesuré comme dans CiMatchFilm.
                    var lab=new System.Text.StringBuilder("scénario;images;geste(s);glissement médiane (m/s);p95;max;n\n");
                    Point Dir(float degrees)=>new Point(Mathf.Sin(degrees*Mathf.Deg2Rad),Mathf.Cos(degrees*Mathf.Deg2Rad));
                    void Begin(Actor a,string action,string kind,float duration,float contact,Point aim,float height){if(a.action==action&&a.actionKind==kind)return;a.action=action;a.actionKind=kind;a.actionSequence++;a.actionTime=duration;a.actionContactTime=contact;a.actionTarget=aim;a.actionHeight=height;}
                    void Scenario(string label,float seconds,Action<Actor,float> script){
                        var actor=new Actor{id=player.id,slot=5,action="idle",angle=0,position=new Point(0,-4),previous=new Point(0,-4)};
                        script(actor,0);view.ResetPresentation();float clock=0;int start=frame;var slides=new List<float>();var lastFeet=new Vector3[2];var seen=new List<string>();
                        int frames=Mathf.RoundToInt(seconds*Fps);
                        for(int f=0;f<frames;f++){
                            float t=f/(float)Fps;
                            while(clock+MatchSimulation.Step<=t+1e-4f){clock+=MatchSimulation.Step;actor.previous=actor.position;script(actor,clock);actor.position=actor.previous+actor.velocity*MatchSimulation.Step;}
                            var forward=new Vector3(Mathf.Sin(actor.angle),0,Mathf.Cos(actor.angle));var ball=new Vector3(actor.position.x,.11f,actor.position.z)+forward*.6f;
                            view.Render(actor,Mathf.Clamp01((t-clock)/MatchSimulation.Step),f==0?0:1f/Fps,ball,default);
                            if(view.MecanimGestureClip!=null&&!seen.Contains(view.MecanimGestureClip))seen.Add(view.MecanimGestureClip);
                            for(int s=0;s<2;s++){var foot=view.FootPosition(s==0);if(f>1&&foot.y<.12f&&lastFeet[s].y<.12f){var d=foot-lastFeet[s];d.y=0;slides.Add(d.magnitude*Fps);}lastFeet[s]=foot;}
                            var hips=animator.GetBoneTransform(HumanBodyBones.Hips);var focus=hips.position+Vector3.up*.05f;
                            camFront.transform.position=focus+new Vector3(0,.8f,6f);camFront.transform.LookAt(focus);camSide.transform.position=focus+new Vector3(6f,.8f,0);camSide.transform.LookAt(focus);
                            Capture(camFront,target,root,Path.Combine(front,"frame-"+frame.ToString("D4")+".jpg"));Capture(camSide,target,root,Path.Combine(side,"frame-"+frame.ToString("D4")+".jpg"));frame++;
                        }
                        slides.Sort();string Q(float q)=>slides.Count>0?slides[Mathf.Min(slides.Count-1,(int)(slides.Count*q))].ToString("0.00"):"—";
                        lab.AppendLine($"{label};{start}-{frame-1};{(seen.Count>0?string.Join(" + ",seen):"aucun")};{Q(.5f)};{Q(.95f)};{(slides.Count>0?slides[slides.Count-1].ToString("0.00"):"—")};{slides.Count}");
                        info.AppendLine($"scénario {label} frames {start}-{frame-1}");
                    }
                    const float RunSpeed=4.5f,CutAngle=80f; // m/s, ° : virage serré en pleine course
                    Scenario("virage-droite",2.4f,(a,t)=>{float heading=t<1f?0:CutAngle;a.action="run";a.angle=heading*Mathf.Deg2Rad;a.velocity=Dir(heading)*RunSpeed;});
                    Scenario("virage-gauche",2.4f,(a,t)=>{float heading=t<1f?0:-CutAngle;a.action="run";a.angle=heading*Mathf.Deg2Rad;a.velocity=Dir(heading)*RunSpeed;});
                    Scenario("amorti-poitrine",2f,(a,t)=>{
                        if(t<.5f){a.action="run";a.velocity=Dir(0)*1.2f;}
                        else if(t<1.1f-.001f){Begin(a,"control",MatchSimulation.ChestControl,MatchSimulation.BodyControlDuration(MatchSimulation.ChestControl),MatchSimulation.BodyControlContactTime,a.position+Dir(0)*.3f,1.3f);a.actionTime=MatchSimulation.BodyControlDuration(MatchSimulation.ChestControl)-(t-.5f);a.velocity=Dir(0)*.3f;}
                        else{a.action="idle";a.actionKind=null;a.actionTime=0;a.velocity=new Point();}});
                    Scenario("amorti-cuisse",2f,(a,t)=>{
                        if(t<.5f){a.action="run";a.velocity=Dir(0)*1.2f;}
                        else if(t<.98f-.001f){Begin(a,"control",MatchSimulation.ThighControl,MatchSimulation.BodyControlDuration(MatchSimulation.ThighControl),MatchSimulation.BodyControlContactTime,a.position+Dir(0)*.3f+Dir(90)*.12f,.8f);a.actionTime=MatchSimulation.BodyControlDuration(MatchSimulation.ThighControl)-(t-.5f);a.velocity=Dir(0)*.3f;}
                        else{a.action="idle";a.actionKind=null;a.actionTime=0;a.velocity=new Point();}});
                    Scenario("tacle-debout",2f,(a,t)=>{
                        float duration=MatchSimulation.TacklePreparation+MatchSimulation.TackleRecovery;
                        if(t<.5f){a.action="run";a.velocity=Dir(0)*2f;}
                        else if(t<.5f+duration-.001f){Begin(a,"tackle",MatchSimulation.StandingDuel,duration,MatchSimulation.TacklePreparation,a.position+Dir(0)*.7f+Dir(90)*.15f,.11f);a.actionTime=duration-(t-.5f);a.velocity=a.velocity*.5f;}
                        else{a.action="idle";a.actionKind=null;a.actionTime=0;a.velocity=new Point();}});
                    Scenario("controle-pivot",2.2f,(a,t)=>{
                        // Contrôle du pied en se retournant (150°) puis départ dans la nouvelle direction.
                        if(t<.5f){a.action="run";a.velocity=Dir(0)*1.5f;}
                        else if(t<.8f-.001f){Begin(a,"control","control-foot",.3f,0,a.position,.11f);a.actionTime=.3f-(t-.5f);a.angle=150*Mathf.Deg2Rad;a.velocity=Dir(0)*.3f;}
                        else{a.action="run";a.actionKind=null;a.actionTime=0;a.angle=150*Mathf.Deg2Rad;a.velocity=Dir(150)*2f;}});
                    File.WriteAllText(Path.Combine(output,"lab.txt"),lab.ToString());
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

using System;
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
    // Measures the existing Humanoid import before considering any raw-FBX toe correction.
    public static class SprintHumanoidSeamProbe
    {
        [Serializable] public class Measurement {public string bone;public float seamPositionMetres,seamLocalDegrees;}
        [Serializable] public class Report {public bool validAvatar,loopTime,loopPose,footIK;public float duration,epsilonSeconds;public Measurement[] bones;public string limitation="Native imported clip only. No raw-FBX repair applied; no full match or Android validation.";}
        public static void Run(){
            string arg=Environment.GetCommandLineArgs().First(a=>a.StartsWith("-seamOutput="));string output=arg.Substring("-seamOutput=".Length);
            if(Directory.Exists(output))throw new Exception("Proof already exists");Directory.CreateDirectory(output);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var db=JsonUtility.FromJson<Database>(Resources.Load<TextAsset>("Data/database").text);
            var player=db.Squad("176").OrderByDescending(p=>p.rating).First(p=>!p.Goalkeeper);
            var go=new GameObject("Seam review");var view=go.AddComponent<PlayerView>();view.Build(player,0,5,new Color(.1f,.35f,.8f));
            var avatar=view.HumanoidAvatar();if(!avatar.isValid||!avatar.isHuman)throw new Exception("Invalid avatar");
            var animator=view.RigRoot.gameObject.AddComponent<Animator>();animator.avatar=avatar;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            const string path="Assets/Touchline/Resources/Animations/Mixamo/Two Cycle Sprint.fbx";
            var clip=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview__"));
            var settings=AnimationUtility.GetAnimationClipSettings(clip);
            if(!settings.loopTime||!settings.loopBlend)throw new Exception("Expected native loop correction");
            var graph=PlayableGraph.Create("Sprint seam");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var playable=AnimationClipPlayable.Create(graph,clip);var stream=AnimationPlayableOutput.Create(graph,"Pose",animator);stream.SetSourcePlayable(playable);graph.Play();
            var boneIds=new[]{HumanBodyBones.LeftFoot,HumanBodyBones.RightFoot,HumanBodyBones.LeftToes,HumanBodyBones.RightToes};
            var bones=boneIds.Select(animator.GetBoneTransform).ToArray();if(bones.Any(b=>b==null))throw new Exception("Missing foot bone");
            var hips=animator.GetBoneTransform(HumanBodyBones.Hips);
            const float epsilon=.0001f; // seconds, separates discontinuity from normal motion.
            var lamp=new GameObject("Light").AddComponent<Light>();lamp.type=LightType.Directional;lamp.transform.rotation=Quaternion.Euler(40,-30,0);lamp.intensity=1.4f;RenderSettings.ambientLight=Color.gray;
            var camera=new GameObject("Review camera").AddComponent<Camera>();camera.backgroundColor=new Color(.04f,.06f,.08f);camera.clearFlags=CameraClearFlags.SolidColor;
            var rt=new RenderTexture(900,900,24);camera.targetTexture=rt;var texture=new Texture2D(900,900,TextureFormat.RGB24,false);
            try{
                foreach(bool ik in new[]{false,true}){
                    playable.SetApplyFootIK(ik);
                    void Pose(double t){playable.SetTime(t);graph.Evaluate(0);}
                    for(int i=0;i<120;i++)Pose(i/60.0);
                    Pose(clip.length*2-epsilon);var positions=bones.Select(b=>b.position-hips.position).ToArray();var rotations=bones.Select(b=>b.localRotation).ToArray();
                    Pose(clip.length*2+epsilon);
                    var report=new Report{validAvatar=true,loopTime=settings.loopTime,loopPose=settings.loopBlend,footIK=ik,duration=clip.length,epsilonSeconds=epsilon,bones=bones.Select((b,i)=>new Measurement{bone=boneIds[i].ToString(),seamPositionMetres=Vector3.Distance(positions[i],b.position-hips.position),seamLocalDegrees=Quaternion.Angle(rotations[i],b.localRotation)}).ToArray()};
                    File.WriteAllText(Path.Combine(output,ik?"ik.json":"no-ik.json"),JsonUtility.ToJson(report,true));
                    // Across 0.2 ms, 3 mm permits 15 m/s bone motion while rejecting a visible discontinuity.
                    if(report.bones.Any(b=>float.IsNaN(b.seamPositionMetres)||float.IsInfinity(b.seamPositionMetres)||b.seamPositionMetres>.003f||b.seamLocalDegrees>.5f))throw new Exception("Humanoid loop seam exceeds 3 mm / 0.5 degrees");
                    if(!ik)continue;
                    foreach(int frame in new[]{-2,-1,0,1,2}){
                        Pose(clip.length*2+frame/60.0);var focus=(bones[0].position+bones[1].position)*.5f+Vector3.up*.25f;
                        camera.transform.position=focus+new Vector3(2,.7f,2);camera.transform.LookAt(focus);camera.fieldOfView=38;
                        camera.Render();var old=RenderTexture.active;RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,900,900),0,0);texture.Apply();RenderTexture.active=old;
                        File.WriteAllBytes(Path.Combine(output,"seam-"+(frame+2)+".png"),texture.EncodeToPNG());
                    }
                }
                File.WriteAllText(Path.Combine(output,"passed.txt"),"Eight native foot/toe checks passed across loop, with and without foot IK (3 mm / 0.5 degrees); not full match validation.");
            }finally{graph.Destroy();camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(texture);}
            EditorApplication.Exit(0);
        }
    }
}

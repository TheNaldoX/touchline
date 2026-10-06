using System;
using UnityEngine;

namespace Touchline
{
    [Serializable] public class BodyFrame { public float[] joints,root;public float bob,leftHeight,rightHeight; }
    [Serializable] public class BodyClip { public string source,sourceUrl,sha256;public float duration,stride,contactPhase;public int contactFrame,startFrame,endFrame;public BodyFrame[] samples; }
    public struct BodyGround { public float bob,left,right; }
    public static class FullBodyMotion
    {
        public const int JointCount=19;
        static BodyClip walk,run;static BodyClip[] kicks;static bool loaded;
        static readonly int[] Mirror={0,1,2,3,4,8,9,10,5,6,7,15,16,17,18,11,12,13,14};
        static BodyClip LoadClip(string name){var asset=Resources.Load<TextAsset>("Animations/"+name);return asset==null?null:JsonUtility.FromJson<BodyClip>(asset.text);}
        static void Load(){if(loaded)return;loaded=true;walk=LoadClip("body-walk");run=LoadClip("body-run");kicks=new BodyClip[6];for(int i=0;i<kicks.Length;i++)kicks[i]=LoadClip("football-"+i);}
        public static bool Available { get { Load();return walk?.samples?.Length>1&&run?.samples?.Length>1&&Array.TrueForAll(kicks,c=>c?.samples?.Length>1); } }
        public static float Stride(float speed){Load();return Available?Mathf.Lerp(walk.stride,run.stride,Mathf.InverseLerp(1.4f,3.5f,speed)):2;}
        static void Frames(BodyClip clip,float phase,out BodyFrame a,out BodyFrame b,out float t){float f=Mathf.Clamp01(phase)*(clip.samples.Length-1);int index=Mathf.Min((int)f,clip.samples.Length-2);a=clip.samples[index];b=clip.samples[index+1];t=f-index;}
        static Vector3 Joint(BodyFrame a,BodyFrame b,float t,int i)=>new Vector3(Mathf.Lerp(a.joints[i*3],b.joints[i*3],t),Mathf.Lerp(a.joints[i*3+1],b.joints[i*3+1],t),Mathf.Lerp(a.joints[i*3+2],b.joints[i*3+2],t));
        static BodyGround Ground(BodyFrame a,BodyFrame b,float t)=>new BodyGround{bob=Mathf.Lerp(a.bob,b.bob,t),left=Mathf.Lerp(a.leftHeight,b.leftHeight,t),right=Mathf.Lerp(a.rightHeight,b.rightHeight,t)};
        public static BodyGround Locomotion(float phase,float speed,Vector3[] target)
        {
            Load();Frames(walk,Mathf.Repeat(phase,1),out var a,out var b,out float t);Frames(run,Mathf.Repeat(phase,1),out var c,out var d,out float u);float blend=Mathf.InverseLerp(1.4f,3.5f,speed);
            for(int i=0;i<JointCount;i++)target[i]=Vector3.Lerp(Joint(a,b,t,i),Joint(c,d,u,i),blend);
            var x=Ground(a,b,t);var y=Ground(c,d,u);return new BodyGround{bob=Mathf.Lerp(x.bob,y.bob,blend),left=Mathf.Lerp(x.left,y.left,blend),right=Mathf.Lerp(x.right,y.right,blend)};
        }
        public static BodyGround Kick(int variant,float elapsed,bool left,Vector3[] target)
        {
            Load();var clip=kicks[Math.Abs(variant%kicks.Length)];float phase=elapsed<=.18f?Mathf.Lerp(0,clip.contactPhase,Mathf.Clamp01(elapsed/.18f)):Mathf.Lerp(clip.contactPhase,1,Mathf.Clamp01((elapsed-.18f)/.46f));
            Frames(clip,phase,out var a,out var b,out float t);for(int i=0;i<JointCount;i++){target[i]=Joint(a,b,t,left?Mirror[i]:i);if(left)target[i].x=-target[i].x;}
            var ground=Ground(a,b,t);return left?new BodyGround{bob=ground.bob,left=ground.right,right=ground.left}:ground;
        }
    }
}

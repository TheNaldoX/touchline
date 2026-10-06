using System;
using UnityEngine;

namespace Touchline
{
    [Serializable] public struct CapturedPose { public float leftArm,rightArm,leftElbow,rightElbow,leftLift,rightLift,sway; }
    [Serializable] public class CapturedClip { public string source,sha256;public float duration;public int sourceFrames,startFrame,endFrame;public CapturedPose[] samples; }
    public static class CapturedLocomotion
    {
        static CapturedClip walk,run;static bool loaded;
        public static bool Available{get{Load();return walk?.samples?.Length>1&&run?.samples?.Length>1;}}
        static void Load(){if(loaded)return;loaded=true;var w=Resources.Load<TextAsset>("Animations/walk");var r=Resources.Load<TextAsset>("Animations/run");if(w!=null)walk=JsonUtility.FromJson<CapturedClip>(w.text);if(r!=null)run=JsonUtility.FromJson<CapturedClip>(r.text);}
        static CapturedPose Blend(CapturedPose a,CapturedPose b,float t)=>new CapturedPose{leftArm=Mathf.Lerp(a.leftArm,b.leftArm,t),rightArm=Mathf.Lerp(a.rightArm,b.rightArm,t),leftElbow=Mathf.Lerp(a.leftElbow,b.leftElbow,t),rightElbow=Mathf.Lerp(a.rightElbow,b.rightElbow,t),leftLift=Mathf.Lerp(a.leftLift,b.leftLift,t),rightLift=Mathf.Lerp(a.rightLift,b.rightLift,t),sway=Mathf.Lerp(a.sway,b.sway,t)};
        static CapturedPose At(CapturedClip c,float phase){float f=Mathf.Repeat(phase,1)*c.samples.Length;int i=(int)f;return Blend(c.samples[i],c.samples[(i+1)%c.samples.Length],f-i);}
        public static CapturedPose Sample(float phase,float speed){Load();return Available?Blend(At(walk,phase),At(run,phase),Mathf.InverseLerp(1.8f,4.5f,speed)):default;}
    }
}

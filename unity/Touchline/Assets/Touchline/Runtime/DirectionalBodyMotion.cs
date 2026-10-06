using UnityEngine;

namespace Touchline
{
    // Phase-aligned capture blend space. Facing and travel are separate: a
    // defender can retreat or shuffle while keeping the opponent in view.
    public static class DirectionalBodyMotion
    {
        static BodyClip[,,] clips;static BodyClip[] limp;
        static readonly string[] Directions={"forward","left","back","right"};
        static bool loaded,available;
        static BodyClip Load(string name){var text=Resources.Load<TextAsset>("Animations/style-"+name);return text==null?null:JsonUtility.FromJson<BodyClip>(text.text);}
        static void Ensure()
        {
            if(loaded)return;loaded=true;available=true;clips=new BodyClip[4,2,2];limp=new BodyClip[2];
            for(int d=0;d<4;d++)for(int speed=0;speed<2;speed++)for(int v=0;v<2;v++){var c=Load(Directions[d]+(speed==0?"-walk-":"-run-")+v);clips[d,speed,v]=c;available&=c?.samples?.Length>1;}
            for(int v=0;v<2;v++){limp[v]=Load("limp-"+v);available&=limp[v]?.samples?.Length>1;}
        }
        public static bool Available{get{Ensure();return available;}}
        static void Blend(Vector3 travel,float speed,out int a,out int b,out float direction,out float pace)
        {
            float angle=Mathf.Repeat(Mathf.Atan2(travel.x,travel.z)*Mathf.Rad2Deg,360)/90;
            a=Mathf.FloorToInt(angle)%4;b=(a+1)%4;direction=angle-Mathf.Floor(angle);direction=Mathf.SmoothStep(0,1,direction);
            pace=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.8f,2.4f,speed));
        }
        static float ReachStride(BodyClip c,float speed)=>c.stride*Mathf.Clamp(speed/(c.stride/c.duration),1,1.7f);
        public static float Stride(float speed,Vector3 travel,int variant,bool injured)
        {
            Ensure();if(!available)return FullBodyMotion.Stride(speed);variant&=1;
            if(injured&&travel.normalized.z>.7f&&speed<2.3f)return ReachStride(limp[variant],speed);
            Blend(travel,speed,out int a,out int b,out float direction,out float pace);
            float x=Mathf.Lerp(ReachStride(clips[a,0,variant],speed),ReachStride(clips[a,1,variant],speed),pace),y=Mathf.Lerp(ReachStride(clips[b,0,variant],speed),ReachStride(clips[b,1,variant],speed),pace);
            return Mathf.Lerp(x,y,direction);
        }
        static void Frames(BodyClip c,float phase,out BodyFrame a,out BodyFrame b,out float t)
        {
            float f=Mathf.Repeat(phase,1)*(c.samples.Length-1);int index=Mathf.Min((int)f,c.samples.Length-2);a=c.samples[index];b=c.samples[index+1];t=f-index;
        }
        static void Add(BodyClip clip,float phase,float weight,Vector3[] target,ref BodyGround ground)
        {
            if(weight<=.0001f)return;Frames(clip,phase,out var a,out var b,out float t);
            for(int i=0;i<FullBodyMotion.JointCount;i++){int k=i*3;target[i]+=new Vector3(Mathf.Lerp(a.joints[k],b.joints[k],t),Mathf.Lerp(a.joints[k+1],b.joints[k+1],t),Mathf.Lerp(a.joints[k+2],b.joints[k+2],t))*weight;}
            ground.bob+=Mathf.Lerp(a.bob,b.bob,t)*weight;ground.left+=Mathf.Max(0,Mathf.Lerp(a.leftHeight,b.leftHeight,t))*weight;ground.right+=Mathf.Max(0,Mathf.Lerp(a.rightHeight,b.rightHeight,t))*weight;
        }
        public static BodyGround Sample(float phase,float speed,Vector3 travel,int variant,bool injured,Vector3[] target)
        {
            Ensure();if(!available)return FullBodyMotion.Locomotion(phase,speed,target);
            for(int i=0;i<FullBodyMotion.JointCount;i++)target[i]=Vector3.zero;var ground=new BodyGround();variant&=1;
            if(injured&&travel.normalized.z>.7f&&speed<2.3f){Add(limp[variant],phase,1,target,ref ground);return ground;}
            Blend(travel,speed,out int a,out int b,out float direction,out float pace);
            Add(clips[a,0,variant],phase,(1-direction)*(1-pace),target,ref ground);Add(clips[a,1,variant],phase,(1-direction)*pace,target,ref ground);
            Add(clips[b,0,variant],phase,direction*(1-pace),target,ref ground);Add(clips[b,1,variant],phase,direction*pace,target,ref ground);
            return ground;
        }
    }
}

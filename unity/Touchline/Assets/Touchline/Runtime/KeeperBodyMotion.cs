using UnityEngine;

namespace Touchline
{
    // Captured joint directions supply the push-off and flight articulation.
    // The simulation's impact point and timing still control the final reach.
    public static class KeeperBodyMotion
    {
        static BodyClip[] clips;static bool available;
        static readonly int[] Mirror={0,1,2,3,4,8,9,10,5,6,7,15,16,17,18,11,12,13,14};
        static void Load()
        {
            if(clips!=null)return;clips=new BodyClip[6];available=true;
            for(int i=0;i<6;i++){
                var data=Resources.Load<TextAsset>("Animations/keeper-capture-"+i);
                clips[i]=data==null?null:JsonUtility.FromJson<BodyClip>(data.text);
                available&=clips[i]?.samples?.Length>1&&clips[i].samples[0].root?.Length==3;
            }
        }
        public static bool Available{get{Load();return available;}}
        public static bool Sample(float phase,int variant,int side,Vector3[] target,out Quaternion pelvisRotation)
        {
            Load();pelvisRotation=Quaternion.identity;if(!available)return false;
            var clip=clips[(variant&int.MaxValue)%clips.Length];float f=Mathf.Clamp01(phase)*clip.contactPhase*(clip.samples.Length-1);
            int index=Mathf.Min((int)f,clip.samples.Length-2);float t=f-index;var a=clip.samples[index];var b=clip.samples[index+1];
            bool mirror=(clip.samples[clip.samples.Length-1].root[0]>clip.samples[0].root[0])!=(side>0);
            for(int i=0;i<19;i++){
                int k=(mirror?Mirror[i]:i)*3;
                target[i]=new Vector3(Mathf.Lerp(a.joints[k],b.joints[k],t),Mathf.Lerp(a.joints[k+1],b.joints[k+1],t),Mathf.Lerp(a.joints[k+2],b.joints[k+2],t));
                if(mirror)target[i].x=-target[i].x;
            }
            var left=target[11]-target[15];var up=target[2]-target[0];var forward=Vector3.Cross(left,up);
            if(forward.sqrMagnitude>.0001f&&up.sqrMagnitude>.0001f)pelvisRotation=Quaternion.LookRotation(forward,up);
            return true;
        }
    }
}

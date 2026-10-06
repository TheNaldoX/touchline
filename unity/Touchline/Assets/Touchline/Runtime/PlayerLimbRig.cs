using System.Collections.Generic;
using UnityEngine;

namespace Touchline
{
    public sealed partial class PlayerView
    {
        sealed class LimbRig
        {
            public readonly Transform upperArm,lowerArm,wrist,upperLeg,lowerLeg,foot,toe;
            public LimbRig(Dictionary<string,Transform> source,string side)
            {
                upperArm=source["upperarm01."+side];lowerArm=source["lowerarm01."+side];wrist=source["wrist."+side];
                upperLeg=source["upperleg01."+side];lowerLeg=source["lowerleg01."+side];foot=source["foot."+side];toe=source["toe1-1."+side];
            }
        }
        readonly LimbRig[] limbs=new LimbRig[2];
        LimbRig Limb(string side)=>limbs[side=="L"?0:1];
        void PrepareLimbs(){limbs[0]=new LimbRig(bones,"L");limbs[1]=new LimbRig(bones,"R");}
        static void Rotate(Transform bone,float x,float y,float z)=>bone.localRotation=Quaternion.Euler(x,y,z);
        void Aim(Transform bone,Transform end,Vector3 localDirection,float weight)
        {
            if(localDirection.sqrMagnitude<.000001f)return;
            var delta=Quaternion.FromToRotation(end.position-bone.position,transform.TransformDirection(localDirection));bone.rotation=Quaternion.Slerp(bone.rotation,delta*bone.rotation,weight);
        }
    }
}

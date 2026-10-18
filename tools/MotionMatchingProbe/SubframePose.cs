// Isolated experiment. Each controller owns reusable buffers; cached poses remain immutable.
using System;
using Unity.Mathematics;

namespace MotionMatching
{
    public sealed class SubframePose
    {
        PoseVector buffer;
        public PoseVector Sample(PoseSet poses,int frame,float fraction)
        {
            poses.GetPose(frame,out PoseVector current,out int clip);
            bool hasNext=frame+1<poses.GetAnimationClip(clip).End;
            PoseVector next=current;
            if(hasNext)poses.GetPose(frame+1,out next);
            return Blend(current,next,fraction,hasNext);
        }
        public PoseVector Blend(PoseVector a,PoseVector b,float fraction,bool hasNext)
        {
            int count=a.JointLocalPositions.Length;
            if(buffer.JointLocalPositions==null||buffer.JointLocalPositions.Length!=count)
                buffer=new PoseVector(new float3[count],new quaternion[count],new float3[count],new float3[count],false,false);
            float t=hasNext?math.saturate(fraction):0;
            for(int i=0;i<count;i++){
                buffer.JointLocalPositions[i]=math.lerp(a.JointLocalPositions[i],b.JointLocalPositions[i],t);
                buffer.JointLocalRotations[i]=math.slerp(a.JointLocalRotations[i],b.JointLocalRotations[i],t);
                buffer.JointLocalVelocities[i]=math.lerp(a.JointLocalVelocities[i],b.JointLocalVelocities[i],t);
                buffer.JointLocalAngularVelocities[i]=math.lerp(a.JointLocalAngularVelocities[i],b.JointLocalAngularVelocities[i],t);
            }
            // Contact events are discrete: do not anticipate the next frame's landing/release.
            buffer.LeftFootContact=a.LeftFootContact;buffer.RightFootContact=a.RightFootContact;
            return buffer;
        }
        public static void Validate()
        {
            var a=new PoseVector(new[]{float3.zero,float3.zero},new[]{quaternion.identity,quaternion.identity},
                new[]{float3.zero,float3.zero},new[]{float3.zero,float3.zero},true,false);
            var b=new PoseVector(new[]{new float3(2),new float3(2)},new[]{quaternion.identity,quaternion.RotateY(math.PI/2)},
                new[]{new float3(4),new float3(4)},new[]{new float3(6),new float3(6)},false,true);
            var sampler=new SubframePose();var p=sampler.Blend(a,b,.5f,true);
            Require(math.distance(p.JointLocalPositions[1],new float3(1))<.00001f,"hips midpoint");
            Require(math.abs(math.dot(p.JointLocalRotations[1].value,quaternion.RotateY(math.PI/4).value))>.99999f,"joint midpoint");
            Require(p.JointLocalVelocities[1].x==2&&p.JointLocalAngularVelocities[1].x==3,"velocities");
            Require(p.LeftFootContact&&!p.RightFootContact,"discrete contacts");
            var positions=p.JointLocalPositions;
            p=sampler.Blend(a,b,.8f,false);Require(p.JointLocalPositions[1].x==0,"clip edge");
            Require(ReferenceEquals(positions,p.JointLocalPositions),"buffer reuse");
            Require(a.JointLocalPositions[1].x==0&&b.JointLocalPositions[1].x==2,"immutable sources");
            var other=new SubframePose().Blend(a,b,.7f,true);
            Require(!ReferenceEquals(other.JointLocalPositions,p.JointLocalPositions),"independent transition buffers");
        }
        static void Require(bool valid,string label){if(!valid)throw new Exception("Subframe pose: "+label);}
    }
}

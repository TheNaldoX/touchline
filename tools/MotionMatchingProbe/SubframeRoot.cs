// Experimental adapter for the isolated MIT MotionMatching package, not Touchline.
using System;
using Unity.Mathematics;

namespace MotionMatching
{
    public static class SubframeRoot
    {
        public static void Sample(PoseSet poses, int frame, float fraction,
            out float3 position, out quaternion rotation)
        {
            poses.GetPose(frame, out PoseVector current, out int clip);
            // Clip.End is exclusive: never blend unrelated animation clips.
            bool hasNext = frame + 1 < poses.GetAnimationClip(clip).End;
            PoseVector next = current;
            if (hasNext) poses.GetPose(frame + 1, out next);
            Blend(current, next, fraction, hasNext, out position, out rotation);
        }

        public static void Blend(PoseVector current, PoseVector next, float fraction,
            bool hasNext, out float3 position, out quaternion rotation)
        {
            float t = hasNext ? math.saturate(fraction) : 0f;
            position = math.lerp(current.JointLocalPositions[0], next.JointLocalPositions[0], t);
            rotation = math.slerp(current.JointLocalRotations[0], next.JointLocalRotations[0], t);
        }

        public static void Validate()
        {
            var a = new PoseVector(new[] { float3.zero }, new[] { quaternion.identity },
                new[] { float3.zero }, new[] { float3.zero }, false, false);
            var b = new PoseVector(new[] { new float3(2, 0, 0) },
                new[] { quaternion.RotateY(math.PI / 2) }, new[] { float3.zero }, new[] { float3.zero }, false, false);
            Blend(a,b,.5f,true,out var p,out var r);
            Require(math.distance(p,new float3(1,0,0))<.00001f,"midpoint position");
            Require(math.abs(math.dot(r.value,quaternion.RotateY(math.PI/4).value))>.99999f,"midpoint rotation");
            Blend(a,b,0,true,out p,out r); Require(math.length(p)<.00001f,"integer frame");
            Blend(a,b,.9f,false,out p,out r); Require(math.length(p)<.00001f,"clip boundary");
            Blend(a,b,2,true,out p,out r); Require(math.distance(p,b.JointLocalPositions[0])<.00001f,"fraction clamp");
            Require(math.length(a.JointLocalPositions[0])==0 && b.JointLocalPositions[0].x==2,"shared poses unchanged");
        }
        static void Require(bool valid,string label) { if(!valid)throw new Exception("Subframe root: "+label); }
    }
}

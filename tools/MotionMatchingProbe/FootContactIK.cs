// Isolated prototype adapter; no Touchline dependency.
using Unity.Mathematics;
using UnityEngine;

namespace MotionMatching
{
    public static class FootContactIK
    {
        public static void Solve(float3 target,Transform hip,Transform knee,Transform ankle,float3 forward)
        {
            // The toe-to-ankle offset used for target was computed with this orientation.
            // Preserve the already interpolated animation rotation within this pose evaluation;
            // otherwise rotating the parents displaces the toe after solving the ankle.
            Quaternion animatedRotation=ankle.rotation;
            TwoJointIK.Solve(target,hip,knee,ankle,forward);
            ankle.rotation=animatedRotation;
        }
    }
}

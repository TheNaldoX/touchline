using UnityEngine;
namespace Touchline
{
    // Sequential settling footsteps, presentation only; no root motion.
    public sealed class StopStepMotion
    {
        readonly Vector3[] origin=new Vector3[2],destination=new Vector3[2],position=new Vector3[2];
        readonly Quaternion[] startRotation=new Quaternion[2],rotation=new Quaternion[2];
        Quaternion finishRotation;
        float elapsed,duration;int first;
        public bool Active { get; private set; }
        public bool Complete=>Active&&elapsed>=2*duration;
        public int MovingFoot=>!Active||Complete?-1:elapsed<duration?first:1-first;
        public Vector3 Position(int foot)=>position[foot];
        public Quaternion Rotation(int foot)=>rotation[foot];
        public void Cancel(){Active=false;elapsed=0;}
        static float Horizontal(Vector3 a,Vector3 b)=>new Vector2(a.x-b.x,a.z-b.z).magnitude;
        public bool Begin(Vector3 left,Vector3 right,Vector3 leftRest,Vector3 rightRest,Quaternion leftFacing,Quaternion rightFacing,Quaternion restFacing,float maximumStepDistance=.45f)
        {
            Cancel();
            float l=Horizontal(left,leftRest),r=Horizontal(right,rightRest);
            // Leave genuinely airborne or unreachable poses to their existing
            // action/locomotion recovery rather than fabricate planted support.
            float maximum=Mathf.Clamp(maximumStepDistance,.45f,.65f);
            if(l>maximum||r>maximum||left.y>.13f&&right.y>.13f)return false;
            if(Mathf.Max(l,r)<.015f)return false;
            origin[0]=position[0]=left;origin[1]=position[1]=right;
            destination[0]=leftRest;destination[1]=rightRest;
            startRotation[0]=rotation[0]=leftFacing;startRotation[1]=rotation[1]=rightFacing;
            finishRotation=restFacing;
            first=Mathf.Abs(left.y-right.y)>.025f?(left.y>right.y?0:1):(l>=r?0:1);
            duration=Mathf.Clamp(.22f+Mathf.Max(l,r)*.26f,.22f,.34f);
            Active=true;return true;
        }
        public void Advance(float dt)
        {
            if(!Active||float.IsNaN(dt)||float.IsInfinity(dt)||dt<=0)return;
            elapsed=Mathf.Min(2*duration,elapsed+dt);
            for(int i=0;i<2;i++){
                float u=Mathf.Clamp01((elapsed-(i==first?0:duration))/duration);
                // Horizontal travel starts after lift and ends before landing.
                float blend=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.15f,.85f,u));
                position[i]=Vector3.Lerp(origin[i],destination[i],blend);
                position[i].y=Mathf.Lerp(origin[i].y,destination[i].y,u)+.08f*Mathf.Sin(Mathf.PI*u);
                rotation[i]=Quaternion.Slerp(startRotation[i],finishRotation,blend);
            }
        }
    }
}

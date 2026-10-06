using UnityEngine;

namespace Touchline
{
    // Presentation only: this does not extend the simulation's save radius.
    public static class KeeperCompactCatch
    {
        public static float Weight(Vector3 localContact)
        {
            float lateral=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.48f,.95f,Mathf.Abs(localContact.x)));
            float forward=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.95f,1.35f,localContact.z));
            float behind=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.45f,-.1f,localContact.z));
            float high=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(2.0f,2.45f,localContact.y));
            return lateral*forward*behind*high;
        }

        public static void Body(Vector3 localContact,float elapsed,float contact,out Vector3 position,out Quaternion rotation,out float reach)
        {
            float prepare=Mathf.SmoothStep(0,1,elapsed/Mathf.Max(.02f,contact));
            float recover=Mathf.SmoothStep(0,1,(elapsed-contact-.13f)/.65f);
            reach=prepare*(1-recover);
            float low=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.2f,1.1f,localContact.y));
            float high=Mathf.SmoothStep(0,1,Mathf.InverseLerp(1.45f,2.0f,localContact.y));
            var pivot=new Vector3(0,.95f,0);
            // Fold at the hips with both soles supporting the gather. An
            // overhead ball extends the arms; it does not trigger a fall.
            rotation=Quaternion.Euler((32*low+10*high)*reach,0,-localContact.x*12*reach);
            var pelvis=pivot+new Vector3(localContact.x*.22f,-.60f*low,Mathf.Clamp(localContact.z-.4f,0,.2f)*.45f+.06f*high)*reach;
            position=pelvis-rotation*pivot;
        }
    }
}

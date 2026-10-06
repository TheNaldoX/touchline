using UnityEngine;
using Touchline.Core;

namespace Touchline
{
    public sealed partial class PlayerView
    {
        public Vector3 HeaderContactPosition => bones["head"].TransformPoint(new Vector3(0,.10f,.09f))+transform.forward*.11f;
        void HeaderPose(Actor actor,float remaining)
        {
            float elapsed=Mathf.Clamp(.64f-remaining,0,.64f);
            float contact=actor.actionContactTime>0?actor.actionContactTime:.12f;
            float approach=Mathf.SmoothStep(0,1,elapsed/contact);
            float follow=Mathf.SmoothStep(0,1,(elapsed-contact)/.32f);
            float reach=approach*(1-follow);
            float landing=Mathf.Sin(Mathf.Clamp01((elapsed-contact)/(.64f-contact))*Mathf.PI);
            // Meet the incoming ball with the torso almost upright. The larger
            // forward fold belongs to the follow-through, otherwise an oblique
            // header projects the forehead beyond the reachable contact point.
            Rotate("spine02",Mathf.Lerp(-10,6,approach)+20*Mathf.Sin(follow*Mathf.PI)-6*follow,0,0);
            Rotate("head",8*approach,0,0);
            Rotate("upperarm01.L",-30*reach,0,-35-15*reach);
            Rotate("upperarm01.R",-30*reach,0,35+15*reach);
            Rotate("lowerarm01.L",-30,0,0);Rotate("lowerarm01.R",-30,0,0);
            var point=actor.actionSequence>0?new Vector3(actor.actionTarget.x,actor.actionHeight,actor.actionTarget.z):HeaderContactPosition+Vector3.up*.18f;
            var delta=point-HeaderContactPosition;
            var horizontal=Vector3.ClampMagnitude(new Vector3(delta.x,0,delta.z),.40f);
            float rise=Mathf.Clamp(delta.y,-.14f,.45f);
            float jump=Mathf.SmoothStep(0,1,Mathf.Max(0,rise)/.18f),after=elapsed-contact;
            float recovery=Mathf.Clamp01((after-.16f)/Mathf.Max(.05f,.64f-contact-.16f));
            float absorb=Mathf.Sin(recovery*Mathf.PI)*jump;
            body.position+=(horizontal+Vector3.up*rise)*reach-Vector3.up*(landing*.035f+absorb*.085f);
            float spread=Mathf.SmoothStep(0,1,after/.20f)*jump;
            for(int i=0;i<2;i++){
                var foot=transform.TransformPoint(new Vector3(i==0?.18f:-.18f,.08f,0));
                bool lead=(i==0)==leftFooted;
                float grounded=Mathf.SmoothStep(0,1,(after-(lead?.15f:.23f))/.08f);
                foot+=horizontal*reach+Vector3.up*Mathf.Max(0,rise)*reach*(1-grounded);
                foot+=transform.forward*((lead?.13f:-.08f)*spread);
                SolveLeg(Sides[i],foot);Limb(Sides[i]).foot.rotation=transform.rotation;
            }
        }
    }
}

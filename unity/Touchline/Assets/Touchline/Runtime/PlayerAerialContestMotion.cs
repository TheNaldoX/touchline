// A losing aerial contestant preserves his own space and never targets a
// second ball contact. This gesture is procedural, not a mocap import.
using UnityEngine;
using Touchline.Core;

namespace Touchline
{
    public sealed partial class PlayerView
    {
        void AerialContestPose(Actor actor,float remaining)
        {
            if(actor.action!=MatchSimulation.AerialContest)return;
            float duration=MatchSimulation.AerialContestDuration,elapsed=Mathf.Clamp(duration-remaining,0,duration);
            float contact=Mathf.Clamp(actor.actionContactTime,.08f,.20f),approach=Mathf.SmoothStep(0,1,elapsed/contact);
            float recover=Mathf.SmoothStep(0,1,(elapsed-contact)/Mathf.Max(.1f,duration-contact)),reach=approach*(1-recover);
            var ball=new Vector3(actor.actionTarget.x,actor.actionHeight,actor.actionTarget.z);
            var local=transform.InverseTransformDirection(transform.position-ball);local.y=0;
            if(local.sqrMagnitude<.001f)local=-Vector3.forward;local.Normalize();
            // The beaten contestant yields torso space; he never solves his
            // head onto the winner's impact point or moves his root to it.
            Rotate("spine02",local.z*18*reach,0,-local.x*18*reach);
            Rotate("head",8*reach,0,0);
            Rotate("upperarm01.L",-25*reach,0,-38*reach);Rotate("upperarm01.R",-25*reach,0,38*reach);
            Rotate("lowerarm01.L",18*reach,0,0);Rotate("lowerarm01.R",18*reach,0,0);
            float rise=Mathf.Clamp(ball.y-HeaderContactPosition.y-.16f,0,.18f);
            body.position+=Vector3.up*(rise*reach-.04f*Mathf.Sin(recover*Mathf.PI));
            for(int i=0;i<2;i++){
                bool lead=(i==0)==leftFooted;float land=Mathf.SmoothStep(0,1,(elapsed-contact-(lead?.12f:.19f))/.10f);
                var foot=transform.TransformPoint(new Vector3(i==0?.19f:-.19f,.08f,0));
                foot+=Vector3.up*(rise*reach*(1-land));
                SolveLeg(Sides[i],foot);Limb(Sides[i]).foot.rotation=transform.rotation;
            }
        }
    }
}

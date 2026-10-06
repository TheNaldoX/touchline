using UnityEngine;
using Touchline.Core;

namespace Touchline
{
    public sealed partial class PlayerView
    {
        Vector3 distributionContactOffset;
        public Vector3 DistributionHandPosition=>bones["wrist.R"].position+distributionContactOffset;
        void FinalKeeperDistributionContact(Actor actor,float remaining,Vector3 ball)
        {
            if(!MatchSimulation.HandDistribution(actor.action))return;
            float elapsed=MatchSimulation.KeeperDistributionDuration-remaining;
            float contact=actor.actionContactTime>0?actor.actionContactTime:MatchSimulation.KeeperDistributionContact;
            if(elapsed>contact+.00001f)return;
            bool roll=actor.action=="keeper-roll";
            var fingers=roll?transform.forward:body.up;var palm=roll?body.up:transform.forward;
            distributionContactOffset=fingers*.075f+palm*.025f;
            // Transition and ground layers have finished. The visual hand must meet
            // the authoritative held preparation path, never move that path itself.
            SolveArm("R",ball-distributionContactOffset,roll?body.forward:-body.up);
            bones["wrist.R"].rotation=Quaternion.LookRotation(fingers,palm)*Quaternion.Inverse(Quaternion.LookRotation(handAxes[1],handPalmAxes[1]));
        }
        void KeeperDistributionPose(Actor actor,float remaining,Vector3 ball)
        {
            if(!MatchSimulation.HandDistribution(actor.action))return;
            bool roll=actor.action=="keeper-roll";float elapsed=MatchSimulation.KeeperDistributionDuration-remaining;
            float contact=actor.actionContactTime>0?actor.actionContactTime:MatchSimulation.KeeperDistributionContact;
            float wind=Mathf.SmoothStep(0,1,elapsed/contact),follow=Mathf.SmoothStep(0,1,(elapsed-contact)/.6f);
            // The authoritative ball reaches its low wind-up point at 42% of preparation.
            // Lower the body with that wind-up, not half-way through the whole action after release.
            float bend=roll?Mathf.SmoothStep(0,1,elapsed/(contact*.42f))*(1-follow):0;
            // While the ball is behind the hip, load through the knees rather than
            // pitching both shoulders away from it. Transfer the torso forward
            // only with the authoritative forward swing; keep the release pose.
            float forwardSwing=Mathf.SmoothStep(0,1,(elapsed/contact-.42f)/.58f);
            float kneeLoad=Mathf.Lerp(.44f,.32f,forwardSwing);
            float backLoad=bend*(1-forwardSwing);
            body.localPosition+=new Vector3(-.08f*backLoad,-kneeLoad*bend,-.06f*backLoad);
            Rotate("spine05",roll?Mathf.Lerp(10,50,forwardSwing)*bend:0,0,0);
            Rotate("spine02",roll?Mathf.Lerp(5,20,forwardSwing)*bend:Mathf.Lerp(-12,20,follow),Mathf.Lerp(-18,16,wind),10*backLoad);
            var release=new Vector3(actor.actionTarget.x,actor.actionHeight,actor.actionTarget.z);
            var aim=elapsed<=contact?ball:Vector3.Lerp(release,transform.TransformPoint(new Vector3(-.2f,roll?.55f:1.05f,.65f)),follow);
            var fingers=roll?transform.forward:body.up;var palm=roll?body.up:transform.forward;
            distributionContactOffset=fingers*.075f+palm*.025f;
            SolveArm("R",aim-distributionContactOffset,roll?body.forward:-body.up);
            bones["wrist.R"].rotation=Quaternion.LookRotation(fingers,palm)*Quaternion.Inverse(Quaternion.LookRotation(handAxes[1],handPalmAxes[1]));
            SolveArm("L",transform.TransformPoint(new Vector3(.43f,.95f-.18f*bend,.45f+.15f*bend)),body.forward);
            var freeHand=bones["wrist.L"];var forearm=(freeHand.position-bones["lowerarm01.L"].position).normalized;
            freeHand.rotation=Quaternion.FromToRotation(freeHand.TransformDirection(handAxes[0]),forearm)*freeHand.rotation;
            // Transfer weight over a short leading step. The trailing foot
            // remains planted instead of dropping into a two-legged squat.
            float step=wind*(1-follow);
            SolveLeg("L",transform.TransformPoint(new Vector3(.20f,.08f+Mathf.Sin(step*Mathf.PI)*.055f,.28f*step)));
            SolveLeg("R",transform.TransformPoint(new Vector3(-.20f,.08f,0)));
            bones["foot.L"].rotation=bones["foot.R"].rotation=transform.rotation;
        }
    }
}

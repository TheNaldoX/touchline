using UnityEngine;
using Touchline.Core;

namespace Touchline
{
    public sealed partial class PlayerView
    {
        bool slideHasEntryPose;
        Vector3[] capturedSlidePositions;
        Vector3[] capturedSlideBasePositions;
        Quaternion[] capturedSlideBaseRotations;
        void PrepareCapturedSlide(Actor actor,bool reset)
        {
            bool changed=poseAction!=actor.action||poseSequence!=actor.actionSequence;
            PrepareGroundEntry(actor,reset,changed);
            if(actor.action!="slide"||reset||!changed)return;
            if(capturedSlidePositions==null)capturedSlidePositions=new Vector3[skeleton.Length];
            transitionBodyPosition=body.localPosition;transitionBodyRotation=body.localRotation;
            for(int i=0;i<skeleton.Length;i++){capturedSlidePositions[i]=skeleton[i].localPosition;transitionPose[i]=skeleton[i].localRotation;}
        }
        void MecanimSlideContact(Actor actor,float remaining,Vector3 renderedBall)
        {
            if(actor.action!="slide")return;
            float elapsed=Mathf.Clamp(MatchSimulation.SlidingDuelDuration-remaining,0,MatchSimulation.SlidingDuelDuration);
            const float SlideReturnDuration=.5f; // seconds: return to the current captured locomotion, not an arbitrary rest pose.
            float release=Mathf.SmoothStep(0,1,Mathf.InverseLerp(MatchSimulation.SlidingDuelDuration-SlideReturnDuration,MatchSimulation.SlidingDuelDuration,elapsed));
            var basePosition=body.localPosition;var baseRotation=body.localRotation;
            if(release>0){
                if(capturedSlideBasePositions==null){capturedSlideBasePositions=new Vector3[skeleton.Length];capturedSlideBaseRotations=new Quaternion[skeleton.Length];}
                for(int i=0;i<skeleton.Length;i++){capturedSlideBasePositions[i]=skeleton[i].localPosition;capturedSlideBaseRotations[i]=skeleton[i].localRotation;}
            }
            SlidingContactPose(actor,remaining,renderedBall,true);
            KeepBootsAbovePitch(transform.forward,true);
            if(release>0){
                body.localPosition=Vector3.Lerp(body.localPosition,basePosition,release);body.localRotation=Quaternion.Slerp(body.localRotation,baseRotation,release);
                for(int i=0;i<skeleton.Length;i++){skeleton[i].localPosition=Vector3.Lerp(skeleton[i].localPosition,capturedSlideBasePositions[i],release);skeleton[i].localRotation=Quaternion.Slerp(skeleton[i].localRotation,capturedSlideBaseRotations[i],release);}
                KeepBootsAbovePitch(transform.forward,true);
            }
            if(!slideHasEntryPose||capturedSlidePositions==null||elapsed>=.15f)return;
            float blend=Mathf.SmoothStep(0,1,elapsed/.15f); // seconds: the existing slide entry, before accepted impact.
            for(int i=0;i<skeleton.Length;i++)skeleton[i].localPosition=Vector3.Lerp(capturedSlidePositions[i],skeleton[i].localPosition,blend);
        }
        // Called before PlayerView replaces the outgoing skeleton. Its existing
        // transitionPose/body snapshot remains valid through this slide action.
        void PrepareGroundEntry(Actor actor,bool reset,bool actionChanged)
        {
            if(reset||actionChanged)slideHasEntryPose=actor.action=="slide"&&!reset;
        }
        void BlendSlidingEntry(float elapsed)
        {
            if(!slideHasEntryPose||elapsed>=.15f)return;
            float weight=Mathf.SmoothStep(0,1,elapsed/.15f);
            body.localPosition=Vector3.Lerp(transitionBodyPosition,body.localPosition,weight);
            body.localRotation=Quaternion.Slerp(transitionBodyRotation,body.localRotation,weight);
            for(int i=0;i<skeleton.Length;i++)skeleton[i].localRotation=Quaternion.Slerp(transitionPose[i],skeleton[i].localRotation,weight);

        }

        // A contact-induced loss of balance onto a knee and supporting hand.
        // This is a procedural recovery, not a captured prone/ragdoll fall.
        void GroundContactPose(Actor actor,float remaining,Vector3 renderedBall)
        {
            if(actor.action=="slide"){SlidingContactPose(actor,remaining,renderedBall);return;}
            if(actor.action!="fall")return;
            float t=Mathf.Clamp(MatchSimulation.ContactFallDuration-remaining,0,MatchSimulation.ContactFallDuration);
            float collapse=Mathf.SmoothStep(0,1,t/.36f),rise=Mathf.SmoothStep(0,1,(t-1.03f)/1.17f),down=collapse*(1-rise);
            float sign=actor.diveSide>=0?1:-1;
            body.localPosition+=new Vector3(sign*.055f*down,-.43f*down,.035f*down);
            Rotate("spine02",34*down,sign*9*down,-sign*7*down);Rotate("head",18*down,0,0);
            // The leading sole stays planted. The trailing knee folds under
            // the pelvis, then returns before the body regains full height.
            for(int i=0;i<2;i++){
                bool lead=(i==0)==(sign>0);float footSign=i==0?1:-1;
                var foot=transform.TransformPoint(new Vector3(footSign*.20f,.08f,(lead?.30f:-.24f)*down));
                SolveLeg(Sides[i],foot,transform.forward);Limb(Sides[i]).foot.rotation=transform.rotation;
            }
            string brace=sign>0?"L":"R",balance=sign>0?"R":"L";
            float handDown=Mathf.SmoothStep(0,1,(t-.10f)/.30f)*(1-Mathf.SmoothStep(0,1,(t-1.16f)/.42f));
            var wrist=Limb(brace).wrist;
            var anchor=transform.TransformPoint(new Vector3(sign*.32f,.12f,.48f));
            SolveArm(brace,Vector3.Lerp(wrist.position,anchor,handDown),transform.forward);
            int index=sign>0?0:1;
            var palmDown=Quaternion.LookRotation(transform.forward,Vector3.down)*Quaternion.Inverse(Quaternion.LookRotation(handAxes[index],handPalmAxes[index]));
            wrist.rotation=Quaternion.Slerp(wrist.rotation,palmDown,handDown);
            SolveArm(balance,transform.TransformPoint(new Vector3(-sign*(.24f+.16f*down),1.08f-.20f*down,.23f)));
        }

        void SlidingContactPose(Actor actor,float remaining,Vector3 renderedBall,bool captured=false)
        {
            float t=Mathf.Clamp(MatchSimulation.SlidingDuelDuration-remaining,0,MatchSimulation.SlidingDuelDuration);
            float launch=Mathf.SmoothStep(0,1,t/MatchSimulation.SlidingDuelContact),rise=Mathf.SmoothStep(0,1,(t-.78f)/.92f),down=launch*(1-rise);
            float sign=actor.diveSide>=0?1:-1;string lead=sign>0?"L":"R",trail=sign>0?"R":"L";
            var point=new Vector3(actor.actionTarget.x,actor.actionHeight,actor.actionTarget.z);
            if(t<MatchSimulation.SlidingDuelContact-.0001f&&renderedBall!=Vector3.zero){
                // Follow the visible approach before impact. The simulation's
                // prediction is replaced by its recorded impact on the last
                // tick; using it directly made the reaching pose jump early.
                // After contact the recorded point stays fixed during recovery.
                var distance=Vector3.ProjectOnPlane(renderedBall-transform.position,Vector3.up).magnitude;
                float near=Mathf.InverseLerp(2.2f,1.5f,distance)*Mathf.InverseLerp(.7f,.4f,renderedBall.y);
                point=Vector3.Lerp(point,renderedBall,near);
            }
            var contact=BootTarget(point);var local=transform.InverseTransformPoint(contact);local.x=Mathf.Clamp(local.x,-.55f,.55f);local.z=Mathf.Clamp(local.z,-(.22f+.11f/transform.localScale.y),.76f);local.y=Mathf.Clamp(local.y,.08f,.22f);
            // The hips trail the reaching boot during the committed slide.
            // Root and accepted ball contact remain simulation-authoritative.
            float extend=launch*(1-Mathf.SmoothStep(0,1,(t-.48f)/.64f));
            float hipBack=Mathf.Clamp(local.z-.72f,-1.02f,-.12f);
            var rotation=Quaternion.Euler(-46*down,0,sign*36*down);var pelvis=new Vector3(-sign*.035f*down,.95f-.64f*down,hipBack*extend);
            body.localRotation=rotation;body.localPosition=pelvis-rotation*new Vector3(0,.95f,0);
            if(captured){
                // Mecanim already translates/rotates the humanoid hips. Place
                // those actual hips, rather than applying a second full drop.
                var hips=bones["root"];
                body.localRotation=rotation*Quaternion.Inverse(hips.localRotation);
                body.localPosition=pelvis-body.localRotation*hips.localPosition;
            }
            Rotate("spine02",4*down,0,0);Rotate("head",23*down,0,0);
            var rest=transform.TransformPoint(new Vector3(sign*.18f,.08f,0));
            // A ball beside the pelvis requires the ankle behind the toe's
            // contact point. Restricting the ankle to forward z made a close
            // slide overshoot by exactly that clamped distance. Permit the
            // boot contact to reach the root without moving the actor itself.
            var leadTarget=Vector3.Lerp(rest,transform.TransformPoint(local),extend);
            var reaching=Limb(lead);
            float reach=Vector3.Distance(reaching.upperLeg.position,reaching.lowerLeg.position)+Vector3.Distance(reaching.lowerLeg.position,reaching.foot.position)-.015f;
            var delta=leadTarget-reaching.upperLeg.position;
            if(delta.magnitude>reach){
                // Accompany a committed reach with the pelvis, within 24 cm,
                // instead of asking the two leg bones to stretch. The actor,
                // engaged leg and accepted impact remain authoritative.
                body.position+=Vector3.ClampMagnitude(delta.normalized*(delta.magnitude-reach),.24f)*extend;
            }
            SolveCapturedSupport(lead,leadTarget,transform.forward);reaching.foot.rotation=transform.rotation;
            SolveCapturedSupport(trail,transform.TransformPoint(new Vector3(-sign*.21f,.08f,hipBack*extend-.18f*down)),transform.up+transform.forward*.25f);Limb(trail).foot.rotation=transform.rotation;
            for(int i=0;i<2;i++){
                float s=i==0?1:-1;var hand=Limb(Sides[i]).wrist;var anchor=transform.TransformPoint(new Vector3(s*.38f,.20f,hipBack*extend-.06f));
                SolveArm(Sides[i],Vector3.Lerp(hand.position,anchor,down),transform.forward);
                var palmDown=Quaternion.LookRotation(transform.forward,Vector3.down)*Quaternion.Inverse(Quaternion.LookRotation(handAxes[i],handPalmAxes[i]));
                hand.rotation=Quaternion.Slerp(hand.rotation,palmDown,down);
            }
            BlendSlidingEntry(t);
        }
    }
}








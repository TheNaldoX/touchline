using UnityEngine;
using Touchline.Core;

namespace Touchline
{
    public sealed partial class PlayerView
    {
        Quaternion[] compactCatchPose;
        void KeeperDivePose(Actor actor,float remaining)
        {
            float elapsed=Mathf.Clamp(1.2f-remaining,0,1.2f);
            float contact=Mathf.Clamp(actor.actionContactTime>0?actor.actionContactTime:.2f,.02f,.4f);
            var point=actor.actionSequence>0?new Vector3(actor.actionTarget.x,actor.actionHeight,actor.actionTarget.z):transform.TransformPoint(new Vector3(actor.diveSide*.85f,.7f,.25f));
            var local=transform.InverseTransformPoint(point);var direction=new Vector3(local.x,0,local.z);
            float compact=KeeperCompactCatch.Weight(local);
            if(compact>=.9999f){KeeperCompactPose(actor,elapsed,contact,point,local,1);return;}
            if(direction.sqrMagnitude<.01f)direction=Vector3.right*actor.diveSide;direction.Normalize();
            float launch=Mathf.SmoothStep(0,1,elapsed/contact),land=Mathf.SmoothStep(0,1,(elapsed-contact-.12f)/.28f);
            float recover=Mathf.SmoothStep(0,1,(elapsed-.68f)/.52f),crouch=Mathf.Sin(recover*Mathf.PI);
            float tilt=Mathf.Lerp(78,35,Mathf.InverseLerp(.4f,2.3f,local.y));
            var rotation=Quaternion.AngleAxis(Mathf.Lerp(tilt,82,land)*launch*(1-recover),Vector3.Cross(Vector3.up,direction));
            var pivot=Vector3.up*.95f;
            var impactRotation=Quaternion.AngleAxis(tilt,Vector3.Cross(Vector3.up,direction));
            var pelvis=local-impactRotation*new Vector3(0,.70f,.28f);
            pelvis.x=Mathf.Clamp(pelvis.x,-1.35f,1.35f);pelvis.z=Mathf.Clamp(pelvis.z,-.8f,1.1f);pelvis.y=Mathf.Clamp(pelvis.y,.36f,1.5f);
            var landingPelvis=new Vector3(pelvis.x,.34f,pelvis.z);
            pelvis=Vector3.Lerp(pelvis,landingPelvis,land);
            pelvis=Vector3.Lerp(pivot,pelvis,launch);pelvis=Vector3.Lerp(pelvis,pivot,recover)-Vector3.up*(.22f*crouch);
            body.localRotation=rotation;body.localPosition=pelvis-rotation*pivot;
            Rotate("spine02",22*crouch,0,0);
            if(KeeperBodyMotion.Sample(elapsed/contact,motionIdentity+actor.actionSequence,local.x>=0?1:-1,capturedJoints,out var captureRotation)){
                var adapt=body.localRotation*Quaternion.Inverse(captureRotation);float weight=launch*(1-recover);
                for(int i=0;i<2;i++){
                    int leg=i==0?11:15,arm=i==0?5:8;var side=Sides[i];
                    Aim(Limb(side).upperLeg,Limb(side).lowerLeg,adapt*(capturedJoints[leg+1]-capturedJoints[leg]),weight);
                    Aim(Limb(side).lowerLeg,Limb(side).foot,adapt*(capturedJoints[leg+2]-capturedJoints[leg+1]),weight);
                    Aim(Limb(side).foot,Limb(side).toe,adapt*(capturedJoints[leg+3]-capturedJoints[leg+2]),weight);
                    Aim(Limb(side).upperArm,Limb(side).lowerArm,adapt*(capturedJoints[arm+1]-capturedJoints[arm]),weight);
                    Aim(Limb(side).lowerArm,Limb(side).wrist,adapt*(capturedJoints[arm+2]-capturedJoints[arm+1]),weight);
                }
            }
            // The recordings finish in flight. Absorb the landing with both
            // knees instead of freezing the final airborne scissor pose.
            for(int i=0;i<2;i++){
                float sign=i==0?1:-1;var side=Sides[i];float absorb=land*(1-recover);
                Aim(Limb(side).upperLeg,Limb(side).lowerLeg,rotation*new Vector3(sign*.10f,-.78f,.38f),absorb);
                Aim(Limb(side).lowerLeg,Limb(side).foot,rotation*new Vector3(0,-.83f,-.27f),absorb);
                var foot=Limb(side).foot;foot.rotation=Quaternion.Slerp(foot.rotation,body.rotation,absorb);
            }
            // Both hands secure a catch. A parry reaches with the near hand;
            // the other arm balances the body instead of copying its pose.
            bool parry=actor.actionKind=="save-parry";
            var rest=transform.TransformPoint(new Vector3(0,1.05f,.32f));
            var aim=Vector3.Lerp(rest,point,launch);
            var secure=body.TransformPoint(new Vector3(0,1.23f,.30f));
            aim=Vector3.Lerp(aim,secure,Mathf.SmoothStep(0,1,(elapsed-contact-.08f)/.4f));
            aim=Vector3.Lerp(aim,rest-Vector3.up*(.22f*crouch),recover);
            gripOffset=parry?Vector3.zero:body.up*.075f;
            for(int i=0;i<2;i++){
                float sign=i==0?1:-1;var handTarget=aim-transform.forward*.06f+body.right*(sign*.11f)-gripOffset;
                if(parry&&((local.x>=0)!=(i==0)))handTarget=Vector3.Lerp(handTarget,body.TransformPoint(new Vector3(sign*.30f,1.03f,-.12f)),launch*(1-recover));
                var elbowPole=KeeperBodyMotion.Available?Limb(Sides[i]).lowerArm.position-Limb(Sides[i]).upperArm.position:-body.up;
                SolveArm(Sides[i],handTarget,elbowPole);
                var wrist=Limb(Sides[i]).wrist;var handDirection=Vector3.Lerp(body.up,transform.TransformDirection(direction),.35f);
                if(parry)wrist.rotation=Quaternion.FromToRotation(wrist.TransformDirection(handAxes[i]),handDirection)*wrist.rotation;
                else OrientKeeperHand(i,aim);
                // After the parry the lower arm braces on the landing side;
                // using the upper arm here would cross both arms over the
                // chest. A catch keeps both hands protecting the ball.
                if(parry&&((local.x>=0)==(i==0))){
                    float brace=Mathf.SmoothStep(0,1,(elapsed-contact-.18f)/.14f)*(1-Mathf.SmoothStep(0,1,(elapsed-.72f)/.18f));
                    var support=landingPelvis+direction*.45f+Vector3.forward*.22f;support.y=.12f;
                    var anchor=transform.TransformPoint(support);
                    SolveArm(Sides[i],Vector3.Lerp(wrist.position,anchor,brace),body.forward);
                    var palmDown=Quaternion.LookRotation(transform.forward,Vector3.down)*Quaternion.Inverse(Quaternion.LookRotation(handAxes[i],handPalmAxes[i]));
                    wrist.rotation=Quaternion.Slerp(wrist.rotation,palmDown,brace);
                }
            }
            // Recover onto the leg nearest the landing first. The trailing
            // foot swings underneath only once that support is established.
            // Keep each planted sole fixed while the pelvis rises.
            for(int i=0;i<2;i++){
                bool near=(local.x>=0)==(i==0);
                float plant=Mathf.SmoothStep(0,1,(elapsed-(near?.66f:.84f))/.18f);
                bool takeoff=elapsed<contact*.25f;if(takeoff)plant=1-launch;
                if(plant<=0)continue;
                var foot=Limb(Sides[i]).foot;var target=transform.TransformPoint(new Vector3(i==0?.18f:-.18f,.08f,0));
                target=Vector3.Lerp(foot.position,target,plant);
                if(!takeoff)target.y+=Mathf.Sin(plant*Mathf.PI)*.10f*transform.localScale.y;
                SolveLeg(Sides[i],target,body.forward);foot.rotation=Quaternion.Slerp(foot.rotation,transform.rotation,plant);
            }
            if(compact>0)KeeperCompactPose(actor,elapsed,contact,point,local,compact);
        }
        void KeeperCompactPose(Actor actor,float elapsed,float contact,Vector3 point,Vector3 local,float weight)
        {
            bool blending=weight<.9999f;
            var oldBody=body.localPosition;var oldRotation=body.localRotation;var oldGrip=gripOffset;
            var leftHand=Limb("L").wrist.position;var rightHand=Limb("R").wrist.position;
            var leftFoot=Limb("L").foot.position;var rightFoot=Limb("R").foot.position;
            if(blending){
                if(compactCatchPose==null)compactCatchPose=new Quaternion[skeleton.Length];
                for(int i=0;i<skeleton.Length;i++)compactCatchPose[i]=skeleton[i].localRotation;
            }
            for(int i=0;i<skeleton.Length;i++)skeleton[i].localRotation=Quaternion.identity;
            KeeperCompactCatch.Body(local,elapsed,contact,out var position,out var rotation,out var reach);
            body.localPosition=position;body.localRotation=rotation;
            bool parry=actor.actionKind=="save-parry";
            float launch=Mathf.SmoothStep(0,1,elapsed/contact);
            float gather=Mathf.SmoothStep(0,1,(elapsed-contact-.04f)/.40f);
            var rest=transform.TransformPoint(new Vector3(0,1.05f,.32f));
            var secure=body.TransformPoint(new Vector3(0,1.10f,.32f));
            var aim=Vector3.Lerp(Vector3.Lerp(rest,point,launch),secure,gather);
            gripOffset=parry?Vector3.zero:body.up*.075f;
            if(blending){
                body.localPosition=Vector3.Lerp(oldBody,position,weight);body.localRotation=Quaternion.Slerp(oldRotation,rotation,weight);
                for(int i=0;i<skeleton.Length;i++)skeleton[i].localRotation=Quaternion.Slerp(compactCatchPose[i],skeleton[i].localRotation,weight);
                gripOffset=Vector3.Lerp(oldGrip,gripOffset,weight);
            }
            for(int i=0;i<2;i++){
                float sign=i==0?1:-1;var side=Sides[i];
                var target=aim-transform.forward*.06f+body.right*(sign*.11f)-gripOffset;
                if(parry&&((local.x>=0)!=(i==0)))target=Vector3.Lerp(target,body.TransformPoint(new Vector3(sign*.3f,1.06f,.12f)),launch*(1-gather));
                if(blending)target=Vector3.Lerp(i==0?leftHand:rightHand,target,weight);
                SolveArm(side,target,-body.up+body.right*(sign*.18f));
                OrientKeeperHand(i,aim);
                // The feet stay planted throughout a central gather, including
                // the low squat, instead of cycling through an airborne dive.
                var footTarget=transform.TransformPoint(new Vector3(sign*.18f,.08f,0));
                if(blending)footTarget=Vector3.Lerp(i==0?leftFoot:rightFoot,footTarget,weight);
                SolveLeg(side,footTarget,transform.forward);
                Limb(side).foot.rotation=Quaternion.Slerp(Limb(side).foot.rotation,transform.rotation,weight);
            }
            if(bones.TryGetValue("head",out var head)){
                var gaze=head.parent.InverseTransformDirection(aim-head.position);
                float pitch=Mathf.Clamp(-Mathf.Atan2(gaze.y,new Vector2(gaze.x,gaze.z).magnitude)*Mathf.Rad2Deg,-30,35);
                float yaw=Mathf.Clamp(Mathf.Atan2(gaze.x,gaze.z)*Mathf.Rad2Deg,-25,25);
                head.localRotation=Quaternion.Slerp(head.localRotation,Quaternion.Euler(pitch,yaw,0),weight*reach);
            }
        }
        float mecanimPlacementLower;Vector3 placementLeft,placementRight,placementRoot;bool placementTracked,placementActive;
        void MecanimKeeperPlacement(Actor actor,float remaining,Vector3 ballPosition,float dt,bool reset)
        {
            bool placing=actor.action=="place-ball",rising=actor.action=="keeper-rise";
            if(reset||actor.slot!=0){mecanimPlacementLower=0;placementTracked=false;placementActive=false;}
            if(actor.slot!=0)return;
            var left=Limb("L");var right=Limb("R");
            if(!placementTracked){placementLeft=left.wrist.position;placementRight=right.wrist.position;}
            else {var travel=transform.position-placementRoot;placementLeft+=travel;placementRight+=travel;}
            float desired=placing?Mathf.SmoothStep(0,1,1-remaining/MatchSimulation.KeeperPlaceDuration):rising?Mathf.SmoothStep(0,1,remaining/MatchSimulation.KeeperRiseDuration):0;
            // Normal placement progresses below this rate; interruptions recover over time.
            mecanimPlacementLower=Mathf.MoveTowards(mecanimPlacementLower,desired,Mathf.Max(0,dt)*6);
            float lower=mecanimPlacementLower;if(placing)placementActive=true;
            const float settledHands=.005f; // metres before returning completely to the captured animation
            if(!placing&&lower<=0&&Vector3.Distance(placementLeft,left.wrist.position)<settledHands&&Vector3.Distance(placementRight,right.wrist.position)<settledHands)placementActive=false;
            if(!placementActive){placementLeft=left.wrist.position;placementRight=right.wrist.position;placementRoot=transform.position;placementTracked=true;return;}
            var lf=left.foot.position;var rf=right.foot.position;
            var lr=left.foot.rotation;var rr=right.foot.rotation;
            body.localPosition+=Vector3.down*(.48f*lower); // metres in the rig's local scale
            if(bones.TryGetValue("spine05",out var upperSpine))upperSpine.localRotation=Quaternion.Euler(35*lower,0,0)*upperSpine.localRotation;
            if(bones.TryGetValue("spine02",out var lowerSpine))lowerSpine.localRotation=Quaternion.Euler(25*lower,0,0)*lowerSpine.localRotation;
            var contact=ballPosition-transform.forward*.06f;gripOffset=Vector3.zero;
            for(int i=0;i<2;i++){
                var limb=Limb(Sides[i]);float sign=i==0?1:-1;
                var target=contact+transform.right*(sign*.11f);
                if(!placing)target=Vector3.Lerp(limb.wrist.position,target,lower);
                const float placementHandSpeed=4; // metres/second, including entry from an unfinished catch
                SolveArm(Sides[i],Vector3.MoveTowards(i==0?placementLeft:placementRight,target,placementHandSpeed*Mathf.Max(0,dt)),-body.up);
                if(placing)OrientKeeperHand(i,ballPosition);
            }
            SolveLeg("L",lf,transform.forward);SolveLeg("R",rf,transform.forward);
            left.foot.rotation=lr;right.foot.rotation=rr;
            placementLeft=left.wrist.position;placementRight=right.wrist.position;placementRoot=transform.position;placementTracked=true;
        }
        void KeeperHandlingPose(Actor actor,float remaining,Vector3 ballPosition)
        {
            bool placing=actor.action=="place-ball",rising=actor.action=="keeper-rise";
            if(!placing&&!rising&&actor.action!="keeper-hold")return;
            float lower=placing?Mathf.SmoothStep(0,1,1-remaining/MatchSimulation.KeeperPlaceDuration):rising?Mathf.SmoothStep(0,1,remaining/MatchSimulation.KeeperRiseDuration):0;
            // The simulation holds the ball at 1.10 m for every stature. Tall
            // rigs need a small supporting knee/hip bend to reach that same
            // point; otherwise the held mesh floats above the physical ball
            // and jumps down when the keeper begins distribution.
            float tall=Mathf.InverseLerp(1,1.15f,transform.localScale.y);
            float support=rising?0:1-lower;
            body.localPosition+=Vector3.down*(.48f*lower+.09f*tall*support);
            Rotate("spine05",35*lower+5*tall*support,0,0);Rotate("spine02",25*lower,0,0);
            var contact=ballPosition-transform.forward*.06f;
            gripOffset=rising?Vector3.zero:body.up*(.075f*(1-lower));
            for(int i=0;i<2;i++){
                float sign=i==0?1:-1;var target=contact+transform.right*(sign*.1f)-gripOffset;
                if(rising)target=Vector3.Lerp(target,transform.TransformPoint(new Vector3(sign*.25f,.78f,.02f)),1-lower);
                SolveArm(Sides[i],target);SolveLeg(Sides[i],transform.TransformPoint(new Vector3(sign*.16f,.08f,0)));Limb(Sides[i]).foot.rotation=transform.rotation;
                if(!rising)OrientKeeperHand(i,ballPosition);
            }
        }
    }
}

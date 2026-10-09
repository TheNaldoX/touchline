using UnityEngine;
using Touchline.Core;

namespace Touchline
{
    public sealed partial class PlayerView
    {
        float mecanimClaimWeight;Vector3 mecanimClaimAim,mecanimClaimLeft,mecanimClaimRight,mecanimClaimRoot;bool mecanimClaimTracked;
        void MecanimClaimReadiness(Actor actor,KeeperClaimAnticipationSample sample,float remaining,float dt,bool reset)
        {
            const float readinessRate=5; // blend weight per second
            const float handSpeed=3.6f,readyReach=.35f; // m/s; metres in front of the body while preparing
            if(reset||actor.slot!=0){mecanimClaimWeight=0;mecanimClaimTracked=false;}
            if(actor.slot!=0)return;
            bool claim=MatchSimulation.HasRecordedKeeperClaim(actor)&&mecanimClaimWeight>0;
            float target=sample.active?sample.weight:claim?1:0;
            mecanimClaimWeight=Mathf.MoveTowards(mecanimClaimWeight,target,Mathf.Max(0,dt)*readinessRate);
            if(sample.active){
                var planar=new Vector3(sample.target.x-transform.position.x,0,sample.target.z-transform.position.z);
                // Prepare near the body, not at an interception point still beyond arm reach.
                mecanimClaimAim=transform.position+Vector3.ClampMagnitude(planar,readyReach)+Vector3.up*sample.height;
            }else if(claim){
                var impact=new Vector3(actor.actionTarget.x,actor.actionHeight,actor.actionTarget.z);
                float gather=Mathf.SmoothStep(0,1,MatchSimulation.KeeperClaimSecondsAfterContact(actor,remaining)/MatchSimulation.KeeperClaimGatherDuration);
                mecanimClaimAim=Vector3.Lerp(impact,transform.position+transform.forward*.36f+Vector3.up*1.1f,gather);
            }
            // The blend may finish before the hands have returned: keep their speed bounded until settled.
            if(mecanimClaimWeight<=0&&!mecanimClaimTracked)return;
            var left=Limb("L").wrist;var right=Limb("R").wrist;
            var capturedLeft=left.position;var capturedRight=right.position;
            if(!mecanimClaimTracked){mecanimClaimLeft=left.position;mecanimClaimRight=right.position;}
            else {var travel=transform.position-mecanimClaimRoot;mecanimClaimLeft+=travel;mecanimClaimRight+=travel;}
            var l=Vector3.Lerp(left.position,mecanimClaimAim+body.right*.11f-body.up*.075f,mecanimClaimWeight);
            var r=Vector3.Lerp(right.position,mecanimClaimAim-body.right*.11f-body.up*.075f,mecanimClaimWeight);
            SolveArm("L",Vector3.MoveTowards(mecanimClaimLeft,l,handSpeed*Mathf.Max(0,dt)),-body.up);
            SolveArm("R",Vector3.MoveTowards(mecanimClaimRight,r,handSpeed*Mathf.Max(0,dt)),-body.up);
            OrientKeeperHand(0,mecanimClaimAim);OrientKeeperHand(1,mecanimClaimAim);
            mecanimClaimLeft=left.position;mecanimClaimRight=right.position;mecanimClaimRoot=transform.position;mecanimClaimTracked=true;
            const float settledDistance=.005f; // metres: hand tracking can stop only once it meets the captured pose
            if(mecanimClaimWeight<=0&&Vector3.Distance(mecanimClaimLeft,capturedLeft)<settledDistance&&Vector3.Distance(mecanimClaimRight,capturedRight)<settledDistance)mecanimClaimTracked=false;
        }

        Quaternion[] claimPreviewPose;float claimPreviewWeight;int claimPreviewSequence;Vector3 claimPreviewImpact;
        void AnticipateKeeperClaimPose(Actor actor,KeeperClaimAnticipationSample sample,float dt,bool reset)
        {
            if(reset)claimPreviewWeight=0;
            if(MatchSimulation.HasRecordedKeeperClaim(actor)){
                if(actor.actionSequence!=claimPreviewSequence+2)claimPreviewWeight=0;
                return;
            }
            if(actor.action!="idle"&&actor.action!="run"||actor.slot!=0){claimPreviewWeight=0;return;}
            bool hadPreview=claimPreviewWeight>.001f;
            float target=sample.active?sample.weight:0;
            claimPreviewWeight=Mathf.MoveTowards(claimPreviewWeight,target,Mathf.Max(0,dt)*(target>claimPreviewWeight?5:6));
            if(sample.active){
                var desired=new Vector3(sample.target.x,sample.height,sample.target.z);
                claimPreviewImpact=reset||!hadPreview?desired:Vector3.MoveTowards(claimPreviewImpact,desired,Mathf.Max(0,dt)*8);
                claimPreviewSequence=actor.actionSequence;
            }
            if(claimPreviewWeight<=0)return;
            if(claimPreviewPose==null)claimPreviewPose=new Quaternion[skeleton.Length];
            for(int i=0;i<skeleton.Length;i++)claimPreviewPose[i]=skeleton[i].localRotation;
            var oldBody=body.localPosition;var oldRotation=body.localRotation;var oldGrip=gripOffset;
            var oldLeft=Limb("L").wrist.position;var oldRight=Limb("R").wrist.position;var oldLF=Limb("L").foot.position;var oldRF=Limb("R").foot.position;
            KeeperClaimGeometry(claimPreviewImpact,1,0);
            var left=Limb("L").wrist.position;var right=Limb("R").wrist.position;var lf=Limb("L").foot.position;var rf=Limb("R").foot.position;
            body.localPosition=Vector3.Lerp(oldBody,body.localPosition,claimPreviewWeight);body.localRotation=Quaternion.Slerp(oldRotation,body.localRotation,claimPreviewWeight);
            for(int i=0;i<skeleton.Length;i++)skeleton[i].localRotation=Quaternion.Slerp(claimPreviewPose[i],skeleton[i].localRotation,claimPreviewWeight);
            gripOffset=Vector3.Lerp(oldGrip,gripOffset,claimPreviewWeight);
            SolveArm("L",Vector3.Lerp(oldLeft,left,claimPreviewWeight),-body.up);SolveArm("R",Vector3.Lerp(oldRight,right,claimPreviewWeight),-body.up);
            SolveLeg("L",Vector3.Lerp(oldLF,lf,claimPreviewWeight),transform.forward);SolveLeg("R",Vector3.Lerp(oldRF,rf,claimPreviewWeight),transform.forward);
        }
        void RecordedKeeperClaimPose(Actor actor,float remaining)
        {
            float elapsed=MatchSimulation.KeeperClaimPresentationDuration-remaining;
            float prepare=Mathf.Max(claimPreviewWeight,Mathf.SmoothStep(0,1,elapsed/actor.actionContactTime));
            float after=MatchSimulation.KeeperClaimSecondsAfterContact(actor,remaining);
            float gather=Mathf.SmoothStep(0,1,after/MatchSimulation.KeeperClaimGatherDuration);
            var impact=new Vector3(actor.actionTarget.x,actor.actionHeight,actor.actionTarget.z);
            KeeperClaimGeometry(impact,prepare,gather);
        }
        void KeeperClaimGeometry(Vector3 impact,float prepare,float gather)
        {
            var local=transform.InverseTransformPoint(impact);float reach=prepare*(1-gather);
            float low=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.2f,1.1f,local.y));
            float jump=Mathf.Clamp(local.y-1.88f,0,.75f)*reach;
            var pivot=Vector3.up*.95f;
            var rotation=Quaternion.Euler(32*low*reach,0,-Mathf.Clamp(local.x,-1,1)*12*reach);
            var shift=new Vector3(Mathf.Clamp(local.x*.65f,-.65f,.65f),-.6f*low,Mathf.Clamp(local.z-.35f,-.20f,.8f)*.6f)*reach;
            float tall=Mathf.InverseLerp(1,1.15f,transform.localScale.y);
            body.localRotation=rotation;body.localPosition=pivot+shift+Vector3.up*(jump-(.025f+.09f*tall)*gather)-rotation*pivot;
            for(int i=0;i<skeleton.Length;i++)skeleton[i].localRotation=Quaternion.identity;
            Rotate("spine05",5*tall*gather,0,0);
            var hold=transform.position+transform.forward*.36f+Vector3.up*1.1f;
            var aim=Vector3.Lerp(Vector3.Lerp(hold,impact,prepare),hold,gather);
            gripOffset=body.up*.075f;
            for(int i=0;i<2;i++){
                float sign=i==0?1:-1;var side=Sides[i];
                SolveArm(side,aim-transform.forward*.06f+body.right*(sign*.11f)-gripOffset,-body.up+body.right*(sign*.18f));OrientKeeperHand(i,aim);
                var target=transform.TransformPoint(new Vector3(sign*.18f+shift.x*.45f,.08f+jump,shift.z*.5f));
                SolveLeg(side,target,transform.forward);Limb(side).foot.rotation=transform.rotation;
            }
            if(bones.TryGetValue("head",out var head)){
                var gaze=head.parent.InverseTransformDirection(aim-head.position);
                float pitch=Mathf.Clamp(-Mathf.Atan2(gaze.y,new Vector2(gaze.x,gaze.z).magnitude)*Mathf.Rad2Deg,-30,35);
                float yaw=Mathf.Clamp(Mathf.Atan2(gaze.x,gaze.z)*Mathf.Rad2Deg,-25,25);
                head.localRotation=Quaternion.Euler(pitch*reach,yaw*reach,0);
            }
        }
    }
}

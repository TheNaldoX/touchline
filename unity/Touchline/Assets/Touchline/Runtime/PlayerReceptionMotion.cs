using UnityEngine;
using Touchline.Core;

namespace Touchline
{
    public sealed partial class PlayerView
    {
        public Vector3 ChestContactPosition=>bones["spine01"].TransformPoint(new Vector3(0,.095f,.13f))+transform.forward*.11f;
        public Vector3 ThighContactPosition(bool left)=>Limb(left?"L":"R").lowerLeg.position+transform.up*.08f+transform.forward*.11f;

        // Procedural cushioning layered onto the existing human rig. The
        // contact and recovery follow saved simulation time, never wall time.
        void ReceptionRotation(string name,float x,float y,float z,float weight)
        {
            if(bones.TryGetValue(name,out var bone))bone.localRotation=Quaternion.Slerp(bone.localRotation,Quaternion.Euler(x,y,z),weight);
        }
        void BodyReceptionPose(Actor actor,float remaining)
        {
            if(!MatchSimulation.IsBodyControl(actor))return;
            float duration=MatchSimulation.BodyControlDuration(actor.actionKind),elapsed=Mathf.Clamp(duration-remaining,0,duration);
            float contact=MatchSimulation.BodyControlContactTime;
            float approach=Mathf.SmoothStep(0,1,elapsed/contact),release=Mathf.SmoothStep(0,1,(elapsed-contact)/(duration-contact));
            float weight=approach*(1-release),absorb=Mathf.Sin(release*Mathf.PI);
            var point=new Vector3(actor.actionTarget.x,actor.actionHeight,actor.actionTarget.z);
            ApplyReceptionOverlay(actor.actionKind,point,receivingLeft,weight,absorb,release);
        }
        void ApplyReceptionOverlay(string kind,Vector3 point,bool receiveLeft,float weight,float absorb,float release)
        {
            // At the first contact frame the outgoing run/turn has already
            // been blended above. A zero-weight overlay must not overwrite
            // bent elbows with identity and create a one-frame arm snap.
            float upperBodyWeight=Mathf.Clamp01(weight+absorb);
            ReceptionRotation("spine02",-10*weight+9*absorb,0,0,upperBodyWeight);ReceptionRotation("head",14*release,0,0,upperBodyWeight);
            ReceptionRotation("upperarm01.L",-18*weight,0,-36*weight,upperBodyWeight);ReceptionRotation("upperarm01.R",-18*weight,0,36*weight,upperBodyWeight);
            ReceptionRotation("lowerarm01.L",22*weight,0,0,upperBodyWeight);ReceptionRotation("lowerarm01.R",22*weight,0,0,upperBodyWeight);
            if(kind==MatchSimulation.ChestControl){
                var offset=point-ChestContactPosition;
                var horizontal=Vector3.ClampMagnitude(new Vector3(offset.x,0,offset.z),.32f);
                body.position+=(horizontal+Vector3.up*Mathf.Clamp(offset.y,-.28f,.18f))*weight;
                body.position-=transform.forward*(.055f*absorb);
                for(int i=0;i<2;i++){
                    var side=Sides[i];var foot=Limb(side).foot;
                    var rest=transform.TransformPoint(new Vector3(i==0?.20f:-.20f,.08f,0));
                    SolveLeg(side,Vector3.Lerp(foot.position,rest,weight));
                    foot.rotation=Quaternion.Slerp(foot.rotation,transform.rotation,weight);
                }
            }else{
                string side=receiveLeft?"L":"R";var leg=Limb(side);
                var originalFoot=leg.foot.position;
                var desiredKnee=point-transform.up*.08f-transform.forward*.11f;
                // Cushion by flexing the support knee and taking the pelvis
                // back. Raising it to fit the lifted knee makes the opposite
                // leg too short to reach the pitch, visibly lifting its sole.
                body.position-=transform.up*(.055f*weight);
                var hip=leg.upperLeg.position;float upper=Vector3.Distance(hip,leg.lowerLeg.position);
                var toward=desiredKnee-hip;float vertical=Vector3.Dot(toward,transform.up);
                var horizontal=Vector3.ProjectOnPlane(toward,transform.up);
                float reach=Mathf.Sqrt(Mathf.Max(.0001f,upper*upper-vertical*vertical));
                if(horizontal.sqrMagnitude>.0001f)body.position+=horizontal.normalized*Mathf.Clamp(horizontal.magnitude-reach,-.30f,.10f)*weight;
                hip=leg.upperLeg.position;
                var knee=hip+(desiredKnee-hip).normalized*upper;
                leg.upperLeg.rotation=Quaternion.Slerp(leg.upperLeg.rotation,Quaternion.FromToRotation(leg.lowerLeg.position-hip,knee-hip)*leg.upperLeg.rotation,weight);
                var foot=Vector3.Lerp(originalFoot,leg.lowerLeg.position-transform.up*(Vector3.Distance(leg.lowerLeg.position,leg.foot.position)*.90f)+transform.forward*.10f,weight);
                leg.lowerLeg.rotation=Quaternion.FromToRotation(leg.foot.position-leg.lowerLeg.position,foot-leg.lowerLeg.position)*leg.lowerLeg.rotation;
                leg.foot.rotation=Quaternion.Slerp(leg.foot.rotation,transform.rotation*Quaternion.Euler(-12*weight,0,0),weight);
                var support=receiveLeft?"R":"L";
                var supportFoot=Limb(support).foot;var supportRest=transform.TransformPoint(new Vector3(receiveLeft?-.20f:.20f,.08f,0));
                SolveLeg(support,Vector3.Lerp(supportFoot.position,supportRest,weight));
                supportFoot.rotation=Quaternion.Slerp(supportFoot.rotation,transform.rotation,weight);
            }
        }
    }
}

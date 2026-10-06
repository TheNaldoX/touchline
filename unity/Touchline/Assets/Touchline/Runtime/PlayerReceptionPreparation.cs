using UnityEngine;
using Touchline.Core;
namespace Touchline
{
    public sealed partial class PlayerView
    {
        float receptionPreparationWeight,receptionFrameDelta,receptionCancellationRemaining;
        float bodyCarryHandoffRemaining;
        float BodyCarryHandoffWeight=>Mathf.SmoothStep(0,1,1-bodyCarryHandoffRemaining/.10f);
        const float ReceptionCancellationDuration=.16f;
        readonly Vector3[] receptionCancellationOffsets=new Vector3[2];
        readonly Quaternion[] receptionCancellationRotations=new Quaternion[2];
        string receptionPreparationKind,receptionPreparationSource;
        Point receptionPreparationFlightStart;
        Vector3 receptionPreparationPoint;
        bool receptionPreparationLeft,receptionPreparedHandoff;
        int receptionPreparedSequence=-1;
        void ClearReceptionPreparation(){receptionPreparationWeight=0;receptionPreparationKind=null;receptionPreparedHandoff=false;receptionPreparedSequence=-1;receptionCancellationRemaining=0;}
        bool PreparedReceptionHandoff(Actor actor,float elapsed)=>receptionPreparedHandoff&&actor.actionSequence==receptionPreparedSequence&&elapsed<MatchSimulation.BodyControlContactTime;
        void UpdateBodyReceptionPreparation(Actor actor,float dt,bool reset,BodyReceptionAnticipationSample sample)
        {
            receptionFrameDelta=Mathf.Max(0,dt);
            bool carryLocomotion=actor.action=="run"||actor.action=="idle";
            if(reset||!carryLocomotion)bodyCarryHandoffRemaining=0;
            else if(poseAction=="control"&&(actor.actionKind==MatchSimulation.ChestControl||actor.actionKind==MatchSimulation.ThighControl))bodyCarryHandoffRemaining=.10f;
            else bodyCarryHandoffRemaining=Mathf.Max(0,bodyCarryHandoffRemaining-receptionFrameDelta);
            if(reset||actor.slot==0){ClearReceptionPreparation();return;}
            if(MatchSimulation.IsBodyControl(actor)){
                if(poseAction!="control"||poseSequence!=actor.actionSequence){
                    var actual=new Vector3(actor.actionTarget.x,actor.actionHeight,actor.actionTarget.z);
                    receptionPreparedHandoff=receptionPreparationWeight>.05f&&receptionPreparationKind==actor.actionKind&&receivingLeft==receptionPreparationLeft&&sample.source==receptionPreparationSource&&Vector3.Distance(actual,receptionPreparationPoint)<.45f;
                    receptionPreparedSequence=actor.actionSequence;
                }
                // Keep the captured outgoing readiness through the first80ms.
                // It is not applied again as a second overlay on real control.
                return;
            }
            bool locomotion=actor.action=="run"||actor.action=="idle";
            if(!locomotion||poseAction=="control"){ClearReceptionPreparation();if(!locomotion)return;}
            if(receptionCancellationRemaining>0)return;
            bool changed=sample.active&&(sample.kind!=receptionPreparationKind||sample.source!=receptionPreparationSource||Point.Distance(sample.flightStart,receptionPreparationFlightStart)>.01f);
            if(changed&&receptionPreparationWeight<.005f){
                receptionPreparationKind=sample.kind;receptionPreparationSource=sample.source;receptionPreparationFlightStart=sample.flightStart;
                receptionPreparationPoint=new Vector3(sample.point.x,sample.height,sample.point.z);
                float lateral=transform.InverseTransformPoint(receptionPreparationPoint).x;receptionPreparationLeft=Mathf.Abs(lateral)<.06f?leftFooted:lateral>0;
                changed=false;
            }
            float desired=sample.active&&!changed?.80f*Mathf.SmoothStep(0,1,(.35f-sample.eta)/.30f):0;
            receptionPreparationWeight=Mathf.MoveTowards(receptionPreparationWeight,desired,Mathf.Max(0,dt)*(desired>receptionPreparationWeight?3:5));
            if(sample.active&&!changed){var point=new Vector3(sample.point.x,sample.height,sample.point.z);receptionPreparationPoint=Vector3.Lerp(receptionPreparationPoint,point,1-Mathf.Exp(-Mathf.Max(0,dt)*12));}
        }
        // Applied after locomotion/stop support, never over an actual contact.
        void BodyReceptionPreparation(Actor actor,PlayerMotionContext context)
        {
            if(actor.action!="run"&&actor.action!="idle"){receptionCancellationRemaining=0;return;}
            if(context.carrying||context.reaction!=0){ClearReceptionPreparation();return;}
            if(context.contactWeight>.1f&&receptionPreparationWeight>.0001f){
                // The opponent keeps control of the torso/contact layer. Lower
                // only the already-raised feet towards the live locomotion pose,
                // rather than deleting40cm of leg preparation in one frame.
                ClearReceptionPreparation();receptionCancellationRemaining=ReceptionCancellationDuration;
                for(int i=0;i<2;i++){receptionCancellationOffsets[i]=previousFeet[i]-Limb(Sides[i]).foot.position;receptionCancellationRotations[i]=previousFootRotations[i];}
            }
            if(receptionCancellationRemaining>0){
                float weight=Mathf.SmoothStep(0,1,receptionCancellationRemaining/ReceptionCancellationDuration);
                for(int i=0;i<2;i++){var foot=Limb(Sides[i]).foot;var rotation=foot.rotation;SolveLeg(Sides[i],foot.position+receptionCancellationOffsets[i]*weight);foot.rotation=Quaternion.Slerp(rotation,receptionCancellationRotations[i],weight);}
                receptionCancellationRemaining=Mathf.Max(0,receptionCancellationRemaining-receptionFrameDelta);return;
            }
            if(receptionPreparationWeight<=.0001f)return;
            ApplyReceptionOverlay(receptionPreparationKind,receptionPreparationPoint,receptionPreparationLeft,receptionPreparationWeight,0,0);
        }
    }
}

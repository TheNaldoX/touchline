using UnityEngine;
using Touchline.Core;
namespace Touchline
{
    // Applied after pivots and before authoritative ball contacts.
    public sealed partial class PlayerView
    {
        readonly StopStepMotion stoppingSteps=new StopStepMotion();
        bool movedBeforeStop;Vector3 stoppingRoot;
        void StopStepFootwork(Actor actor,float dt,bool reset,PlayerMotionContext context)
        {
            if(reset){capturePoseOverride[0]=false;capturePoseOverride[1]=false;}
            float speed=actor.velocity.Length;
            bool ready=DefensiveReadinessIntent(actor.intent);
            bool locomotion=actor.action=="idle"||actor.action=="run";
            bool contact=actor.slot==0||context.carrying||context.contactWeight>.1f||context.reaction!=0;
            if(reset||!locomotion||contact){stoppingSteps.Cancel();movedBeforeStop=!reset&&locomotion&&speed>.35f;return;}
            // Readiness plants both feet at .25m/s. Capture the outgoing
            // support on that same crossing, before its unconditional IK snap.
            if(speed>(ready?.25f:.08f)){stoppingSteps.Cancel();movedBeforeStop|=speed>.35f;return;}
            // A deliberate pivot owns both feet and must not fight a gathering step.
            float yawRate=Mathf.Abs(Mathf.DeltaAngle(lastRotation.eulerAngles.y,transform.eulerAngles.y))/Mathf.Max(.001f,dt);
            if(pivotTime>0||yawRate>20){stoppingSteps.Cancel();return;}
            if(stoppingSteps.Active&&Vector3.Distance(stoppingRoot,transform.position)>.08f){stoppingSteps.Cancel();movedBeforeStop=false;return;}
            if(!stoppingSteps.Active&&movedBeforeStop){
                float width=ready?.22f:.16f;
                var left=transform.TransformPoint(new Vector3(width,.08f,0));var right=transform.TransformPoint(new Vector3(-width,.08f,0));
                if(stoppingSteps.Begin(previousFeet[0],previousFeet[1],left,right,previousFootRotations[0],previousFootRotations[1],transform.rotation,ready?.60f:.45f)){
                    stoppingRoot=transform.position;movedBeforeStop=false;
                }
            }
            if(!stoppingSteps.Active)return;
            stoppingSteps.Advance(dt);
            for(int i=0;i<2;i++){
                SolveLeg(Sides[i],stoppingSteps.Position(i));Limb(Sides[i]).foot.rotation=stoppingSteps.Rotation(i);
                // Keep the procedural fallback's anchors in agreement on restart.
                planted[i]=stoppingSteps.Position(i);swingFrom[i]=planted[i];
                // Captured locomotion is evaluated before this layer on the
                // next frame. Hand it the actual latest support, otherwise a
                // restart pins the foot to the anchor from before braking.
                capturePlant[i]=planted[i];captureStance[i]=stoppingSteps.MovingFoot!=i;
                captureReleaseTime[i]=0;captureReleaseOffset[i]=Vector3.zero;capturePoseOverride[i]=true;
            }
        }
    }
}

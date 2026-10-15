using UnityEngine;
using Touchline.Core;
namespace Touchline
{
    // Applied after pivots and before authoritative ball contacts.
    public sealed partial class PlayerView
    {
        readonly StopStepMotion stoppingSteps=new StopStepMotion();
        bool movedBeforeStop;Vector3 stoppingRoot;
        float standingSupportWeight;
        readonly Vector3[] standingSupportTargets=new Vector3[2];
        void MecanimStandingSupport(Actor actor,float speed,float dt,bool reset)
        {
            bool stationary=actor.action=="idle"||actor.action=="run";
            float desired=stationary?1-Mathf.InverseLerp(.08f,.5f,speed):0; // m/s: leave support correction as the stride accelerates.
            if(receptionPreparationWeight>.05f)desired=0; // The receiving overlay owns the raised leg and support.
            if(reset&&MatchSimulation.IsBodyControl(actor))desired=1;
            const float SupportBlendRate=8; // weight per second, shared by both geometric solver passes.
            standingSupportWeight=reset?desired:Mathf.MoveTowards(standingSupportWeight,desired,Mathf.Max(0,dt)*SupportBlendRate);
            if(standingSupportWeight<=0)return;
            if(receptionBootVertices==null)receptionBootVertices=FootballBootMesh.Shared.vertices;
            const float StandingSoleClearance=.01f,MaximumSupportDrop=.20f; // metres; preserve the captured ankle orientation.
            // Refine once from the visible mesh after solving both supports:
            // reaching the ankle target alone does not guarantee a grounded
            // sole in the resulting pose. Do not advance time twice.
            const float MaximumPelvisDrop=.10f; // metres per rendered pose, shared between refinements.
            float remainingPelvisDrop=MaximumPelvisDrop;
            for(int refinement=0;refinement<2;refinement++){
            float pelvisDrop=0;
            for(int i=0;i<2;i++){
                var leg=Limb(Sides[i]);float low=float.PositiveInfinity;
                foreach(var vertex in receptionBootVertices)low=Mathf.Min(low,leg.foot.TransformPoint(vertex).y);
                float drop=footStep[i]>0?0:Mathf.Clamp(low-StandingSoleClearance,0,MaximumSupportDrop)*standingSupportWeight;
                var target=standingSupportTargets[i]=leg.foot.position-Vector3.up*drop;
                float reach=Vector3.Distance(leg.upperLeg.position,leg.lowerLeg.position)+Vector3.Distance(leg.lowerLeg.position,leg.foot.position)-.005f;
                var delta=leg.upperLeg.position-target;
                if(delta.magnitude>reach)pelvisDrop=Mathf.Max(pelvisDrop,(delta.magnitude-reach)/Mathf.Max(.5f,delta.normalized.y)+.002f);
            }
            // Only settle the pelvis enough to make the ground targets reachable;
            // moving the authoritative player root would change the match.
            float appliedPelvisDrop=Mathf.Min(remainingPelvisDrop,pelvisDrop);
            body.position-=Vector3.up*appliedPelvisDrop;remainingPelvisDrop-=appliedPelvisDrop;
            for(int i=0;i<2;i++){
                var leg=Limb(Sides[i]);
                var rotation=leg.foot.rotation;
                SolveCapturedSupport(Sides[i],standingSupportTargets[i],leg.lowerLeg.position-leg.upperLeg.position);leg.foot.rotation=rotation;
            }
            }
        }
        const float StopReleaseDuration=.12f; // seconds: hand grounded supports back to the captured stride.
        float stopReleaseRemaining;
        readonly Vector3[] stopReleaseFrom=new Vector3[2];
        readonly Quaternion[] stopReleaseRotation=new Quaternion[2];
        void MecanimStopRelease(Actor actor,bool wasSettling,float dt,bool reset,PlayerMotionContext context)
        {
            bool locomotion=actor.action=="run"||actor.action=="idle";
            if(reset||!locomotion||context.carrying||context.contactWeight>.1f||context.reaction!=0){stopReleaseRemaining=0;return;}
            if(wasSettling&&!stoppingSteps.Active){
                stopReleaseRemaining=StopReleaseDuration;
                for(int i=0;i<2;i++){stopReleaseFrom[i]=previousFeet[i];stopReleaseRotation[i]=previousFootRotations[i];}
            }
            if(stoppingSteps.Active){stopReleaseRemaining=0;return;}
            if(stopReleaseRemaining<=0)return;
            stopReleaseRemaining=Mathf.Max(0,stopReleaseRemaining-Mathf.Max(0,dt));
            float blend=Mathf.SmoothStep(0,1,1-stopReleaseRemaining/StopReleaseDuration);
            for(int i=0;i<2;i++){
                var leg=Limb(Sides[i]);var rotation=Quaternion.Slerp(stopReleaseRotation[i],leg.foot.rotation,blend);
                var target=Vector3.Lerp(stopReleaseFrom[i],leg.foot.position,blend);
                SolveCapturedSupport(Sides[i],target,leg.lowerLeg.position-leg.upperLeg.position);
                leg.foot.rotation=rotation;footLockPoint[i]=leg.foot.position;
            }
        }
        void StopStepFootwork(Actor actor,float dt,bool reset,PlayerMotionContext context,bool mecanim=false)
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
                if(mecanim){
                    // Resume captured locomotion from the support actually shown,
                    // not its anchor from before the two settling footsteps.
                    footLockPoint[i]=stoppingSteps.Position(i);footStep[i]=0;
                    footLocked[i]=stoppingSteps.MovingFoot!=i;footLockWeight[i]=footLocked[i]?1:0;
                }
            }
        }
    }
}

using UnityEngine;
using Touchline.Core;

namespace Touchline
{
    public sealed partial class PlayerView
    {
        readonly Vector3[] capturedJoints=new Vector3[FullBodyMotion.JointCount];
        readonly Vector3[] capturePlant=new Vector3[2];readonly bool[] captureStance=new bool[2];
        readonly bool[] capturePoseOverride=new bool[2];
        readonly Vector3[] captureReleaseOffset=new Vector3[2];readonly float[] captureReleaseTime=new float[2];
        readonly float[] capturedAnklePitch=new float[2];readonly bool[] capturedAnklePitchValid=new bool[2];
        readonly bool[] capturePendingRelease=new bool[2],captureStartingRelease=new bool[2];
        int motionIdentity;
        // previousFeet is captured from the final visible pose. Reconcile the
        // swing in that same frame, after ContextBody adds its lean/readiness.
        // Ball, duel and dribble contacts remain later in the presentation stack.
        void FinishCapturedSupportRelease()
        {
            for(int i=0;i<2;i++){
                if(!capturePendingRelease[i])continue;
                capturePendingRelease[i]=false;
                var foot=Limb(Sides[i]).foot;var rotation=foot.rotation;
                if(captureStartingRelease[i])captureReleaseOffset[i]=previousFeet[i]-foot.position;
                var target=foot.position+captureReleaseOffset[i]*Mathf.SmoothStep(0,1,captureReleaseTime[i]/.14f);
                SolveLeg(Sides[i],target);foot.rotation=rotation;
            }
        }
        void KeepBootsAbovePitch(Vector3 bendPole,bool preserveCurrentPlane=false)
        {
            // Resolve the entire rotated boot after every pose and contact
            // layer, including launch/landing, rather than just locomotion.
            for(int i=0;i<2;i++){
                var foot=Limb(Sides[i]).foot;var rotation=foot.rotation;
                var center=rotation*new Vector3(0,.02f,.075f);
                float extent=Mathf.Abs((rotation*Vector3.right).y)*.057f+Mathf.Abs((rotation*Vector3.up).y)*.085f+Mathf.Abs((rotation*Vector3.forward).y)*.145f;
                float low=foot.position.y+(center.y-extent)*transform.localScale.y;
                if(low<.01f){var target=foot.position;target.y+=.01f-low;var pole=preserveCurrentPlane?Limb(Sides[i]).lowerLeg.position-Limb(Sides[i]).upperLeg.position:bendPole;SolveLeg(Sides[i],target,pole);foot.rotation=rotation;}
            }
        }
        void CapturedBody(Actor actor,float alpha,float dt,bool reset,bool actionChanged)
        {
            for(int i=0;i<2;i++){capturePendingRelease[i]=false;captureStartingRelease[i]=false;}
            bool kick=actor.action=="kick";if(kick||actor.action!="run"&&actor.action!="idle"&&!MatchSimulation.PreparingFootDelivery(actor)){capturedAnklePitchValid[0]=false;capturedAnklePitchValid[1]=false;}if(!kick&&actor.action!="run"&&actor.action!="idle"&&!MatchSimulation.PreparingFootDelivery(actor))return;
            float elapsed=Mathf.Clamp(.64f-actor.actionTime-(1-alpha)*.1f,0,.64f);
            float weight=kick?Mathf.SmoothStep(0,1,elapsed/.07f)*(1-Mathf.SmoothStep(0,1,(elapsed-.49f)/.15f)):Mathf.InverseLerp(.15f,1.1f,actor.velocity.Length);
            if(kick&&ShortPass(actor))weight*=.62f;
            if(weight<=0){capturedAnklePitchValid[0]=false;capturedAnklePitchValid[1]=false;return;}
            if(motionIdentity==0){uint hash=2166136261;foreach(char c in PlayerId??"player")hash=unchecked((hash^c)*16777619);motionIdentity=(int)(hash%10000)+1;}
            var ground=kick?FullBodyMotion.Kick(motionIdentity+actor.actionSequence,elapsed,leftFooted,capturedJoints):DirectionalBodyMotion.Sample(gait,actor.velocity.Length,transform.InverseTransformDirection(new Vector3(actor.velocity.x,0,actor.velocity.z)),motionIdentity,actor.injured,capturedJoints);
            body.localPosition=Vector3.Lerp(body.localPosition,new Vector3(0,Mathf.Clamp(ground.bob,-.065f,.065f)-.025f,0),weight);
            Aim("spine05","spine02",capturedJoints[2]-capturedJoints[1],weight);
            Aim("spine02","neck01",capturedJoints[3]-capturedJoints[2],weight);
            for(int i=0;i<2;i++){
                string side=Sides[i];int arm=i==0?5:8,leg=i==0?11:15;
                Aim(Limb(side).upperArm,Limb(side).lowerArm,capturedJoints[arm+1]-capturedJoints[arm],weight);
                Aim(Limb(side).lowerArm,Limb(side).wrist,capturedJoints[arm+2]-capturedJoints[arm+1],weight);
                Aim(Limb(side).upperLeg,Limb(side).lowerLeg,capturedJoints[leg+1]-capturedJoints[leg],weight);
                Aim(Limb(side).lowerLeg,Limb(side).foot,capturedJoints[leg+2]-capturedJoints[leg+1],weight);
                bool overridden=capturePoseOverride[i];
                var foot=Limb(side).foot;bool striking=kick&&((i==0)==leftFooted);
                bool contact=!striking&&(kick||(i==0?ground.left:ground.right)<.055f);
                if(reset){captureReleaseTime[i]=0;capturePlant[i]=foot.position;capturePlant[i].y=.08f;}
                else if(actionChanged){captureReleaseTime[i]=0;capturePlant[i]=previousFeet[i];capturePlant[i].y=.08f;}
                else if(contact&&!captureStance[i]){capturePlant[i]=previousFeet[i];capturePlant[i].y=.08f;captureReleaseTime[i]=0;}
                if(!kick&&!reset&&!actionChanged&&!contact&&(captureStance[i]||capturePoseOverride[i])){captureStartingRelease[i]=true;captureReleaseTime[i]=.14f;}
                if(contact){var hip=Limb(side).upperLeg.position;var target=capturePlant[i];var horizontal=Vector3.ProjectOnPlane(target-hip,Vector3.up);if(horizontal.magnitude>.62f)target=hip+horizontal.normalized*.62f;target.y=.08f;SolveLeg(side,target);}
                else if(!kick&&captureReleaseTime[i]>0){captureReleaseTime[i]=Mathf.Max(0,captureReleaseTime[i]-dt);capturePendingRelease[i]=true;}
                captureStance[i]=contact;capturePoseOverride[i]=false;
                if(striking){
                    // The ball remains on the simulation trajectory. Move the
                    // boot to its release point, never the ball to the boot.
                    var point=new Vector3(actor.actionTarget.x,actor.actionHeight,actor.actionTarget.z);
                    if(actor.actionSequence==0)point=transform.position+transform.forward*.42f+Vector3.up*.11f;
                    var target=BootTarget(point);
                    float contactWeight=1-Mathf.SmoothStep(0,1,Mathf.Abs(elapsed-.18f)/.075f);
                    SolveLeg(side,Vector3.Lerp(foot.position,target,contactWeight));
                }
                foot.rotation=Quaternion.Slerp(foot.rotation,transform.rotation,contact||striking?1:.8f);
                if(!kick){
                    // Retain captured ankle articulation in swing, then roll
                    // over the toes as a forward support leaves the ground.
                    var toe=capturedJoints[leg+3]-capturedJoints[leg+2];
                    float pitch=Mathf.Clamp(-Mathf.Atan2(toe.y,new Vector2(toe.x,toe.z).magnitude)*Mathf.Rad2Deg,-20,32);
                    if(contact){var local=transform.InverseTransformPoint(foot.position);float forward=transform.InverseTransformDirection(new Vector3(actor.velocity.x,0,actor.velocity.z)).z;pitch=forward>.5f?Mathf.SmoothStep(0,16,Mathf.InverseLerp(-.12f,-.42f,local.z)):0;}
                    // Blend ankle articulation with locomotion, just like the
                    // captured joints. Otherwise a gentle restart at weight .16
                    // applies a full 32-degree toe pitch and instantly raises
                    // the whole boot by a tenth of a metre to clear the turf.
                    float targetPitch=pitch*weight;
                    // Later balance layers rotate the body after this authored
                    // ankle angle. Do not feed their extra pitch back into the
                    // rate limiter every frame; sync only on a genuine pose handoff.
                    float previousPitch=reset?targetPitch:capturedAnklePitchValid[i]&&!overridden?capturedAnklePitch[i]:Mathf.DeltaAngle(0,(Quaternion.Inverse(lastRotation)*previousFootRotations[i]).eulerAngles.x);
                    pitch=Mathf.MoveTowardsAngle(previousPitch,targetPitch,240*Mathf.Max(0,dt));capturedAnklePitch[i]=pitch;capturedAnklePitchValid[i]=true;
                    var rotation=transform.rotation*Quaternion.Euler(pitch,0,0);
                    float radians=pitch*Mathf.Deg2Rad,sole=.015f+transform.localScale.y*(.065f*Mathf.Cos(radians)+Mathf.Max(.22f*Mathf.Sin(radians),-.07f*Mathf.Sin(radians)));
                    if(foot.position.y<sole){var target=foot.position;target.y=sole;SolveLeg(side,target);}
                    foot.rotation=rotation;
                }
                if(foot.position.y<.055f){var target=foot.position;target.y=.08f;SolveLeg(side,target);}
            }
        }
        void Aim(string start,string end,Vector3 localDirection,float weight)
        {
            if(localDirection.sqrMagnitude<.000001f)return;var bone=bones[start];var delta=Quaternion.FromToRotation(bones[end].position-bone.position,transform.TransformDirection(localDirection));bone.rotation=Quaternion.Slerp(bone.rotation,delta*bone.rotation,weight);
        }
    }
}



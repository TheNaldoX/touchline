using UnityEngine;
using Touchline.Core;

namespace Touchline
{
    public sealed partial class PlayerView
    {
        readonly Transform[] fingerBones=new Transform[30];
        readonly Vector3[] fingerAxes=new Vector3[30];
        readonly float[] handCurl=new float[2];
        readonly Vector3[] handPalmAxes=new Vector3[2];
        Vector3 gripOffset;
        void PrepareHands()
        {
            for(int side=0;side<2;side++){
                var wrist=Limb(Sides[side]).wrist;var middle=bones["finger3-1."+Sides[side]];var index=bones["finger2-1."+Sides[side]];var little=bones["finger5-1."+Sides[side]];
                handAxes[side]=(middle.position-wrist.position).normalized;
                handPalmAxes[side]=Vector3.Cross(little.position-index.position,handAxes[side]).normalized*(side==0?1:-1);
            }
            for(int side=0;side<2;side++)for(int finger=1;finger<=5;finger++)for(int joint=1;joint<=3;joint++){
                int index=side*15+(finger-1)*3+joint-1;string name="finger"+finger+"-"+joint+"."+Sides[side];
                if(!bones.TryGetValue(name,out var bone))continue;fingerBones[index]=bone;
                foreach(var definition in source.bones)if(definition.name==name){
                    var direction=(Vector(definition.tail)-Vector(definition.head)).normalized;
                    var palm=handPalmAxes[side];
                    fingerAxes[index]=Vector3.Cross(direction,palm).normalized;break;
                }
            }
        }
        void OrientKeeperHand(int side,Vector3 ball)
        {
            var wrist=Limb(Sides[side]).wrist;
            var palm=(ball-(wrist.position+body.up*.075f)).normalized;
            if(palm.sqrMagnitude<.01f)palm=transform.forward;
            var fingers=Vector3.ProjectOnPlane(body.up,palm).normalized;
            if(fingers.sqrMagnitude<.01f)fingers=Vector3.ProjectOnPlane(transform.forward,palm).normalized;
            wrist.rotation=Quaternion.LookRotation(fingers,palm)*Quaternion.Inverse(Quaternion.LookRotation(handAxes[side],handPalmAxes[side]));
        }
        void HandPose(Actor actor,float remaining,float dt,bool reset,PlayerMotionContext context)
        {
            float curl=actor.velocity.Length>.8f?.48f:.20f;
            if(actor.action=="kick"||actor.action=="header")curl=.40f;
            if(actor.action=="throw"||actor.action=="keeper-hold"||actor.action=="place-ball")curl=.32f;
            if(actor.intent=="keeper"||actor.intent=="close-angle")curl=.08f;
            if(actor.action=="claim")curl=Mathf.Lerp(.08f,.38f,Mathf.SmoothStep(0,1,(.8f-remaining)/.18f));
            if(actor.action=="dive"){
                float elapsed=1.2f-remaining,contact=Mathf.Clamp(actor.actionContactTime>0?actor.actionContactTime:.2f,.02f,.4f);
                curl=actor.actionKind=="save-catch"?Mathf.Lerp(.08f,.38f,Mathf.SmoothStep(0,1,(elapsed-contact)/.12f)):.08f;
            }
            // The action takes precedence over the keeper's positional intent.
            if(actor.action=="keeper-hold"||actor.action=="place-ball"||actor.action=="throw")curl=.38f;
            if(MatchSimulation.HandDistribution(actor.action))curl=Mathf.Lerp(.38f,.08f,Mathf.SmoothStep(0,1,(MatchSimulation.KeeperDistributionDuration-remaining-actor.actionContactTime)/.12f));
            if(context.reaction==1||actor.action=="hurt")curl=.05f;
            if(context.reaction==2)curl=.72f;
            if(context.reaction==2){
                for(int i=0;i<2;i++){
                    var wrist=Limb(Sides[i]).wrist;
                    var forearm=(wrist.position-Limb(Sides[i]).lowerArm.position).normalized;
                    wrist.rotation=Quaternion.FromToRotation(wrist.TransformDirection(handAxes[i]),forearm)*wrist.rotation;
                }
            }
            if((actor.action=="run"||actor.action=="idle")&&context.reaction==0){
                for(int i=0;i<2;i++){
                    if(context.requestWeight>0&&context.requestSide==(i==0?1:-1))continue;
                    var limb=Limb(Sides[i]);var direction=(limb.wrist.position-limb.lowerArm.position).normalized;
                    var palm=Vector3.ProjectOnPlane(transform.right*(i==0?-1:1),direction).normalized;
                    if(palm.sqrMagnitude<.01f)palm=Vector3.ProjectOnPlane(transform.forward,direction).normalized;
                    limb.wrist.rotation=Quaternion.LookRotation(direction,palm)*Quaternion.Inverse(Quaternion.LookRotation(handAxes[i],handPalmAxes[i]));
                }
            }
            for(int side=0;side<2;side++){
                float desired=context.requestSide==(side==0?1:-1)?Mathf.Lerp(curl,.08f,context.requestWeight):curl;
                if(side==0&&MatchSimulation.HandDistribution(actor.action))desired=.20f;
                handCurl[side]=reset?desired:Mathf.Lerp(handCurl[side],desired,1-Mathf.Exp(-Mathf.Max(0,dt)*16));
                for(int finger=0;finger<5;finger++)for(int joint=0;joint<3;joint++){
                    int index=side*15+finger*3+joint;var bone=fingerBones[index];if(bone==null)continue;
                    float angle=(joint==0?48:joint==1?78:52)*handCurl[side]*(finger==0?.65f:1);
                    bone.localRotation=Quaternion.AngleAxis(angle,fingerAxes[index]);
                }
            }
        }
    }
}

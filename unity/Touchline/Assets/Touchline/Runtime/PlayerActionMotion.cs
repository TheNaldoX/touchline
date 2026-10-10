using UnityEngine;
using Touchline.Core;

namespace Touchline
{
    // Presentation context is reconstructed from the saved simulation. It never
    // advances its own clock, so pause, replay speed and save/resume agree.
    public struct PlayerMotionContext
    {
        public bool carrying,defending; public int reaction,requestSide; public float reactionTime,requestWeight;
        public bool hasSimulationClock;public float simulationClock;
        public float contactWeight;public Point contactDirection;
        public KeeperClaimAnticipationSample keeperClaim;
        public BodyReceptionAnticipationSample bodyReception;
        public KeeperReadinessSample keeperReadiness;
        public bool Same(PlayerMotionContext other)=>hasSimulationClock==other.hasSimulationClock&&simulationClock==other.simulationClock&&carrying==other.carrying&&defending==other.defending&&reaction==other.reaction&&reactionTime==other.reactionTime&&requestSide==other.requestSide&&requestWeight==other.requestWeight&&contactWeight==other.contactWeight&&contactDirection.x==other.contactDirection.x&&contactDirection.z==other.contactDirection.z&&keeperClaim.Same(other.keeperClaim)&&bodyReception.Same(other.bodyReception)&&keeperReadiness.Same(other.keeperReadiness);
        public static PlayerMotionContext From(MatchState match,Actor actor,float alpha,float stature=1.8f)
        {
            var result=new PlayerMotionContext{hasSimulationClock=true,simulationClock=match.clock,carrying=match.ball.owner==actor.id&&!match.ball.held,defending=match.restart<=0&&MatchSimulation.PossessionSide(match)!=actor.side};
            result.keeperClaim=KeeperClaimAnticipation.Evaluate(match,actor,alpha);
            result.keeperReadiness=KeeperReadiness.Evaluate(match,actor,alpha,result.keeperClaim.active);
            result.bodyReception=BodyReceptionAnticipationSample.From(match,actor,alpha,stature);
            if(match.restart<=0&&!actor.sentOff&&(actor.action=="run"||actor.action=="idle")&&match.actors!=null)foreach(var other in match.actors){
                if(other==actor||other.sentOff||other.side==actor.side||other.action=="dive")continue;
                var delta=actor.position-other.position;float distance=delta.Length;if(distance<.01f||distance>1.05f)continue;
                var normal=delta/distance;float closing=Point.Dot(other.velocity-actor.velocity,normal);float weight=Mathf.Clamp01((1.05f-distance)/.5f)*Mathf.Clamp01((closing+1)/3);
                if(weight>result.contactWeight){result.contactWeight=weight;result.contactDirection=normal;}
            }
            if((actor.action=="run"||actor.action=="idle")&&match.restart<=0&&!string.IsNullOrEmpty(match.ball.owner)&&match.ball.owner!=actor.id&&match.ball.side==actor.side&&(actor.intent=="near-post"||actor.intent=="far-post"||actor.intent=="box-arrival")&&Point.Distance(actor.position,match.ball.position)<28){
                bool clear=true;if(match.actors!=null)foreach(var opponent in match.actors)if(opponent.side!=actor.side&&!opponent.sentOff&&Point.Distance(opponent.position,actor.position)<1.5f){clear=false;break;}
                if(clear){float phase=Mathf.Repeat(match.clock-(1-alpha)*MatchSimulation.Step+actor.slot*.37f,4);
                    result.requestWeight=Mathf.SmoothStep(0,1,phase/.18f)*(1-Mathf.SmoothStep(0,1,(phase-.65f)/.38f));
                    var delta=match.ball.position-actor.position;result.requestSide=delta.x*Mathf.Cos(actor.angle)-delta.z*Mathf.Sin(actor.angle)>=0?1:-1;
                }
            }
            if(match.phase!="goal"||match.restart<=0)return result;
            for(int i=match.events.Count-1;i>=0;i--){var e=match.events[i];if(e.kind!="goal")continue;
                float elapsed=Mathf.Max(0,match.clock-e.time-(1-alpha)*MatchSimulation.Step);
                if(elapsed>=3)return result;
                result.reaction=e.side!=actor.side?-1:e.player==actor.id?2:1;
                // Full-duration goals include 45 seconds of preparation.
                // React when the goal happens, not during the last three
                // seconds of that restart countdown.
                result.reactionTime=elapsed;break;
            }
            return result;
        }
    }
    public sealed partial class PlayerView
    {
        bool contactLeft;float breathPhase,accelerationLean;
        readonly LocomotionAcceleration accelerationPresentation=new LocomotionAcceleration();
        readonly KeeperReadinessPresentation keeperReadinessPresentation=new KeeperReadinessPresentation();
        readonly Vector3[] handAxes=new Vector3[2];
        static bool ShortPass(Actor actor)=>actor.actionKind=="pass"||actor.actionKind=="through"||actor.actionKind=="cutback";
        public Vector3 InsideFootContactPosition(bool left)
        {
            float sign=left?1:-1;var foot=Limb((left?"L":"R")).foot;
            return foot.TransformPoint(new Vector3(-sign*.08f,0,.12f))+foot.TransformDirection(new Vector3(-sign,0,0))*.11f;
        }
        void PlantBothFeet(float width=.17f)
        {
            for(int i=0;i<2;i++){SolveLeg(Sides[i],transform.TransformPoint(new Vector3(i==0?width:-width,.08f,0)));Limb(Sides[i]).foot.rotation=transform.rotation;}
        }
        void ContextBody(Actor actor,float dt,bool reset,Vector3 ball,PlayerMotionContext context)
        {
            float speed=actor.velocity.Length;
            float keeperReadiness=keeperReadinessPresentation.Sample(context.keeperReadiness,dt,context.hasSimulationClock,context.simulationClock,reset);
            // Actor velocity changes on fixed simulation ticks. Dividing that
            // change by one rendered frame created saturated pulses at 10 Hz,
            // whose average strength depended on the display refresh rate.
            accelerationLean=accelerationPresentation.Sample(speed,dt,context.hasSimulationClock,context.simulationClock,reset);
            bool locomotion=actor.action=="idle"||actor.action=="run"||MatchSimulation.PreparingFootDelivery(actor);
            if(!locomotion)return;
            // Distance drives moving cadence; this small idle clock advances
            // only when Render is evaluated, and is frozen by the pause cache.
            if(!reset)breathPhase+=Mathf.Min(dt,.1f)*(1.6f+(100-actor.fitness)*.018f);
            float fatigue=Mathf.Clamp01((85-actor.fitness)/60);
            body.localRotation*=Quaternion.Euler(accelerationLean*.65f,0,0);
            if(speed<.8f){body.localPosition+=Vector3.up*(Mathf.Sin(breathPhase)*.006f);Rotate("spine02",fatigue*9,0,0);}
            if(context.carrying){Rotate("spine02",7,0,0);Rotate("head",10,0,0);}
            else if((actor.slot==0||DefensiveReadinessIntent(actor.intent))&&speed<2.5f){
                float readiness=(1-Mathf.InverseLerp(1,2.5f,speed))*(actor.slot==0?keeperReadiness:1);body.localPosition+=Vector3.down*(.085f*readiness);Rotate("spine02",10*readiness,0,0);
                SolveArm("L",Vector3.Lerp(Limb("L").wrist.position,transform.TransformPoint(new Vector3(.37f,.95f,.27f)),readiness));
                SolveArm("R",Vector3.Lerp(Limb("R").wrist.position,transform.TransformPoint(new Vector3(-.37f,.95f,.27f)),readiness));
                if(speed<.25f)PlantBothFeet(.22f);
            }
            if(context.contactWeight>0){
                // Anticipation and upper-body balance at an upright contact.
                // Reconstructed from the match; feet and ball contacts keep
                // their authoritative positions and captured locomotion.
                var push=transform.InverseTransformDirection(new Vector3(context.contactDirection.x,0,context.contactDirection.z));float w=context.contactWeight;
                bones["spine02"].localRotation*=Quaternion.Euler(push.z*7*w,0,-push.x*8*w);
                for(int i=0;i<2;i++){Limb(Sides[i]).upperArm.localRotation*=Quaternion.Euler(-8*w,0,(i==0?-1:1)*12*w);Limb(Sides[i]).lowerArm.localRotation*=Quaternion.Euler(12*w,0,0);}
            }
            if(context.requestWeight>0){
                int index=context.requestSide>0?0:1;var side=Sides[index];var wrist=Limb(side).wrist;
                var target=transform.TransformPoint(new Vector3(context.requestSide*.32f,1.67f,.14f));
                SolveArm(side,Vector3.Lerp(wrist.position,target,context.requestWeight));
                var upright=Quaternion.FromToRotation(wrist.TransformDirection(handAxes[index]),body.up)*wrist.rotation;
                wrist.rotation=Quaternion.Slerp(wrist.rotation,upright,context.requestWeight);
            }
            if(context.reaction!=0){
                float t=context.reactionTime,w=Mathf.SmoothStep(0,1,t/.22f)*(1-Mathf.SmoothStep(0,1,(t-2.45f)/.55f));
                if(context.reaction==2){
                    float open=Mathf.Lerp(.38f,.68f,w);SolveArm("L",transform.TransformPoint(new Vector3(open,1.32f+.3f*w,.12f)));SolveArm("R",transform.TransformPoint(new Vector3(-open,1.32f+.3f*w,.12f)));Rotate("head",-12*w,0,0);
                }else if(context.reaction==1){
                    float gap=Mathf.Lerp(.22f,.035f,(.5f+.5f*Mathf.Cos(t*15))*w);
                    for(int i=0;i<2;i++){
                        SolveArm(Sides[i],transform.TransformPoint(new Vector3(i==0?gap:-gap,1.3f,.38f)),-transform.up);
                        var wrist=Limb(Sides[i]).wrist;var fingers=transform.up+transform.forward*.2f;
                        wrist.rotation=Quaternion.FromToRotation(wrist.TransformDirection(handAxes[i]),fingers)*wrist.rotation;
                    }
                }else{Rotate("spine02",14*w,0,0);Rotate("head",22*w,0,0);Rotate("upperarm01.L",0,0,-8);Rotate("upperarm01.R",0,0,8);}
            }
        }
        void DuelAndInjuryPose(Actor actor,float remaining,bool entered)
        {
            var point=new Vector3(actor.actionTarget.x,actor.actionHeight,actor.actionTarget.z);
            if(entered){float x=transform.InverseTransformPoint(point).x;contactLeft=Mathf.Abs(x)<.06f?leftFooted:x>0;}
            float sign=contactLeft?1:-1;
            if(actor.action=="kick"&&(actor.actionKind=="cross"||actor.actionKind=="switch"||actor.actionKind=="clearance")){
                float elapsed=Mathf.Clamp(.64f-remaining,0,.64f),w=Mathf.Sin(elapsed/.64f*Mathf.PI);
                float footSign=leftFooted?1:-1;
                Rotate("spine02",actor.actionKind=="clearance"?8*w:-4*w,footSign*16*w,footSign*5*w);
                Rotate(Limb((leftFooted?"R":"L")).upperArm,-28*w,0,footSign*48*w);
            }
            if(actor.action=="tackle"||actor.action=="block"){
                float reach=DuelReach(actor,remaining);
                body.localPosition+=Vector3.down*(.10f*reach);Rotate("spine02",12*reach,sign*10*reach,0);
                Rotate("upperarm01.L",-15,0,-35*reach);Rotate("upperarm01.R",-15,0,35*reach);PlantBothFeet(.21f);
                // Low blocks use a boot; higher impacts turn the torso. Arms
                // stay out of the ball path rather than becoming a hand save.
                if(actor.action=="tackle"||actor.actionHeight<.65f){
                    var local=transform.InverseTransformPoint(point);local.x=Mathf.Clamp(local.x,-.48f,.48f);local.z=Mathf.Clamp(local.z,-.18f,.68f);local.y=Mathf.Clamp(local.y,.11f,.52f);
                    var foot=Limb((contactLeft?"L":"R")).foot;SolveLeg(contactLeft?"L":"R",Vector3.Lerp(foot.position,BootTarget(transform.TransformPoint(local)),reach));foot.rotation=transform.rotation;
                }else{Rotate("spine02",8*reach,sign*32*reach,sign*8*reach);}
            }
            if(actor.action=="miscontrol"){
                float t=Mathf.Clamp01((.4f-remaining)/.4f),w=Mathf.Sin(t*Mathf.PI);
                body.localRotation*=Quaternion.Euler(10*w,0,-sign*7*w);Rotate("upperarm01.L",-25*w,0,-45*w);Rotate("upperarm01.R",-25*w,0,45*w);
            }
            if(actor.action=="hurt"){
                float t=Mathf.Clamp01((2-remaining)/2),w=Mathf.SmoothStep(0,1,t/.16f)*(1-Mathf.SmoothStep(0,1,(t-.78f)/.22f));
                body.localPosition+=Vector3.down*(.12f*w);Rotate("spine02",32*w,0,0);Rotate("head",18*w,0,0);
                SolveArm(contactLeft?"L":"R",transform.TransformPoint(new Vector3(sign*.17f,Mathf.Lerp(1,.61f,w),.20f)));
                SolveArm(contactLeft?"R":"L",transform.TransformPoint(new Vector3(-sign*.35f,1.25f+.25f*w,.18f)));
                if(actor.velocity.Length<.3f)PlantBothFeet();
            }
        }
        void MecanimThrowContact(Actor actor,float remaining,Vector3 ball)
        {
            if(actor.action!="throw"||actor.slot==0||actor.actionHeight<=0)return;
            const float approachSeconds=.25f,recoverySeconds=.25f,gripHalfWidth=.095f; // seconds and metres
            float elapsed=1.1f-remaining,contact=actor.actionContactTime>0?actor.actionContactTime:.5f;
            float weight=1-Mathf.SmoothStep(0,1,Mathf.Abs(elapsed-contact)/(elapsed<=contact?approachSeconds:recoverySeconds));
            if(weight<=0)return;
            var aim=elapsed<=contact?ball:new Vector3(actor.actionTarget.x,actor.actionHeight,actor.actionTarget.z);
            for(int i=0;i<2;i++){
                string side=i==0?"L":"R";
                var hand=Limb(side).wrist;var rotation=hand.rotation;
                var target=aim+transform.right*(side=="L"?-gripHalfWidth:gripHalfWidth);
                SolveArm(side,Vector3.Lerp(hand.position,target,weight),-body.up);hand.rotation=rotation;
            }
        }
        void ThrowPose(Actor actor,float remaining,Vector3 ball)
        {
            float elapsed=Mathf.Clamp(1.1f-remaining,0,1.1f),contact=actor.actionContactTime>0?actor.actionContactTime:.5f;
            float wind=Mathf.SmoothStep(0,1,elapsed/contact),follow=Mathf.SmoothStep(0,1,(elapsed-contact)/.6f);
            Rotate("spine02",Mathf.Lerp(-8,16,elapsed<contact?wind*.3f:follow),0,0);
            // Before release both hands follow the simulated ball, including
            // its lift from the touchline; after release they follow through.
            var release=new Vector3(actor.actionTarget.x,actor.actionHeight,actor.actionTarget.z);
            var aim=elapsed<=contact?ball:Vector3.Lerp(release,transform.TransformPoint(new Vector3(0,1.05f,.48f)),follow);
            float pickup=elapsed<=contact?1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.3f,1.1f,ball.y/transform.localScale.y)):0;
            body.localPosition+=Vector3.down*(.43f*pickup);Rotate("spine02",Mathf.Lerp(elapsed<contact?-8+wind*7:16*follow,52,pickup),0,0);
            SolveArm("L",aim+transform.right*.095f);SolveArm("R",aim-transform.right*.095f);PlantBothFeet(.19f);
        }
        void FinalBallContacts(Actor actor,float remaining,float alpha,Vector3 ball,PlayerMotionContext context)
        {
            if(actor.action=="kick"&&ShortPass(actor)){
                float elapsed=Mathf.Clamp(.64f-remaining,0,.64f),w=1-Mathf.SmoothStep(0,1,Mathf.Abs(elapsed-.18f)/.11f);
                float sign=leftFooted?1:-1;string side=leftFooted?"L":"R";var foot=Limb(side).foot;var rotation=transform.rotation*Quaternion.Euler(0,sign*65*w,0);
                var point=new Vector3(actor.actionTarget.x,actor.actionHeight,actor.actionTarget.z);
                var target=point-rotation*new Vector3(-sign*(.08f*transform.localScale.y+.11f),0,.12f*transform.localScale.y);target.y=Mathf.Max(.08f,target.y);
                SolveLeg(side,Vector3.Lerp(foot.position,target,w));foot.rotation=rotation;
            }
            if(!context.carrying||(actor.action!="run"&&actor.action!="idle")||actor.velocity.Length<.4f||ball.y>.4f)return;
            var local=transform.InverseTransformPoint(ball);if(new Vector2(local.x,local.z).magnitude>.9f)return;
            // The same distance phase drives the core's close/far ball touch.
            float phase=Mathf.Max(0,actor.stride-actor.velocity.Length*MatchSimulation.Step*(1-alpha))*2.4f;float near=.5f-.5f*Mathf.Sin(phase);float touch=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.70f,1,near));
            // Alternate between touches, while neither foot is placed at the
            // ball. The previous boundary switched feet at maximum contact.
            bool left=((Mathf.FloorToInt((phase-Mathf.PI*.5f)/(Mathf.PI*2))&1)==0)==leftFooted;
            touch*=BodyCarryHandoffWeight;
            if(touch<=.0001f)return;
            var carryFoot=Limb((left?"L":"R")).foot;var carryRotation=carryFoot.rotation;
            SolveLeg(left?"L":"R",Vector3.Lerp(carryFoot.position,BootTarget(ball),touch));
            carryFoot.rotation=Quaternion.Slerp(carryRotation,transform.rotation,touch);
        }
    }
}

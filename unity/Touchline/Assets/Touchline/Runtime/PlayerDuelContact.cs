using UnityEngine;
using Touchline.Core;

namespace Touchline
{
    public sealed partial class PlayerView
    {
        static float DuelReach(Actor actor,float remaining)
        {
            if(actor.action=="tackle"&&actor.actionKind==MatchSimulation.StandingDuel&&actor.tackleWithdrawFrom>0){
                float duration=Mathf.Min(actor.tackleWithdrawFrom,MatchSimulation.TackleRecovery*.35f);
                // Before the cancelling tick, interpolation still shows the
                // preparation. Afterwards withdraw from that exact extension,
                // rather than jumping into the end of the normal recovery.
                if(remaining>duration)return PreparedReach(actor.tackleWithdrawFrom+remaining-duration);
                return PreparedReach(actor.tackleWithdrawFrom)*Mathf.SmoothStep(0,1,remaining/Mathf.Max(.001f,duration));
            }
            float recovery=actor.action=="tackle"?MatchSimulation.TackleRecovery:.45f;
            float preparation=actor.action=="tackle"&&actor.actionKind==MatchSimulation.StandingDuel?MatchSimulation.TacklePreparation:0;
            float elapsed=Mathf.Max(0,preparation+recovery-remaining);
            if(preparation>0&&elapsed<preparation)return Mathf.SmoothStep(0,1,elapsed/preparation);
            return 1-Mathf.SmoothStep(0,1,(elapsed-preparation)/recovery);
        }
        static float PreparedReach(float remaining)
        {
            float elapsed=Mathf.Max(0,MatchSimulation.TacklePreparation+MatchSimulation.TackleRecovery-remaining);
            return elapsed<MatchSimulation.TacklePreparation?Mathf.SmoothStep(0,1,elapsed/MatchSimulation.TacklePreparation):1-Mathf.SmoothStep(0,1,(elapsed-MatchSimulation.TacklePreparation)/MatchSimulation.TackleRecovery);
        }
        void FinalDuelContact(Actor actor,float remaining)
        {
            if(actor.action!="tackle"&&(actor.action!="block"||actor.actionHeight>=.65f))return;
            float weight=DuelReach(actor,remaining);
            if(weight<=0)return;
            var point=new Vector3(actor.actionTarget.x,actor.actionHeight,actor.actionTarget.z);
            var offset=point-transform.position;offset.y=0;
            // An interception records the actual impact. Recover from that
            // extension without clipping the target to a rectangle near the feet.
            if(offset.magnitude>1.25f)return;
            var side=contactLeft?"L":"R";var support=contactLeft?"R":"L";
            var leg=Limb(side);var supportFoot=Limb(support).foot;
            var supportTarget=supportFoot.position;supportTarget.y=.08f;
            var facing=offset.sqrMagnitude>.01f?Vector3.RotateTowards(transform.forward,offset.normalized,65*Mathf.Deg2Rad,0):transform.forward;
            var rotation=Quaternion.Slerp(transform.rotation,Quaternion.LookRotation(facing,Vector3.up),weight);
            var ankle=point-rotation*new Vector3(0,0,.22f*transform.localScale.y+.11f);
            ankle.y=Mathf.Max(.08f,ankle.y);
            var reach=Vector3.Distance(leg.upperLeg.position,leg.lowerLeg.position)+Vector3.Distance(leg.lowerLeg.position,leg.foot.position)-.015f;
            var delta=ankle-leg.upperLeg.position;
            if(delta.magnitude>reach){
                var shift=delta.normalized*(delta.magnitude-reach);
                body.position+=Vector3.ClampMagnitude(shift,.38f)*weight;
            }
            SolveLeg(support,supportTarget);supportFoot.rotation=transform.rotation;
            SolveLeg(side,Vector3.Lerp(leg.foot.position,ankle,weight));leg.foot.rotation=rotation;
        }
    }
}

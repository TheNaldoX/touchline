using UnityEngine;
using Touchline.Core;

namespace Touchline
{
    public sealed partial class PlayerView
    {
        float pivotTurn,pivotTime;int pivotFoot;
        readonly Vector3[] pivotFrom=new Vector3[2];
        readonly Quaternion[] pivotRotation=new Quaternion[2];
        void PivotFootwork(Actor actor,float dt,bool reset)
        {
            bool preparing=MatchSimulation.PreparingFootDelivery(actor)||actor.action=="keeper-hold"&&actor.actionKind==MatchSimulation.KeeperDistributionTurn;
            if(reset||actor.velocity.Length>.45f||(actor.action!="idle"&&actor.action!="run"&&!preparing)){pivotTurn=0;pivotTime=0;return;}
            float delta=Mathf.DeltaAngle(lastRotation.eulerAngles.y,transform.eulerAngles.y);pivotTurn+=delta;
            if(pivotTime<=0&&Mathf.Abs(pivotTurn)>=12){
                var leftRest=transform.TransformPoint(new Vector3(.17f,.08f,0));var rightRest=transform.TransformPoint(new Vector3(-.17f,.08f,0));
                float leftDistance=Vector3.ProjectOnPlane(previousFeet[0]-leftRest,Vector3.up).sqrMagnitude,rightDistance=Vector3.ProjectOnPlane(previousFeet[1]-rightRest,Vector3.up).sqrMagnitude;
                pivotFoot=Mathf.Abs(leftDistance-rightDistance)<.0025f?1-pivotFoot:leftDistance>rightDistance?0:1;pivotTurn=0;pivotTime=.28f;
                for(int i=0;i<2;i++){pivotFrom[i]=previousFeet[i];pivotFrom[i].y=Mathf.Max(.08f,pivotFrom[i].y);pivotRotation[i]=Limb(Sides[i]).foot.rotation;}
            }
            if(pivotTime<=0)return;
            pivotTime=Mathf.Max(0,pivotTime-Mathf.Max(0,dt));float t=1-pivotTime/.28f;
            for(int i=0;i<2;i++){
                var target=pivotFrom[i];
                if(i==pivotFoot){var end=transform.TransformPoint(new Vector3(i==0?.17f:-.17f,.08f,0));target=Vector3.Lerp(target,end,Mathf.SmoothStep(0,1,t));target.y=.08f+Mathf.Sin(t*Mathf.PI)*.085f;}
                SolveLeg(Sides[i],target);Limb(Sides[i]).foot.rotation=Quaternion.Slerp(pivotRotation[i],transform.rotation,i==pivotFoot?Mathf.SmoothStep(0,1,t):0);
                planted[i]=target;swingFrom[i]=target;
                // A later run must resume from these pivot supports, not the
                // captured running anchors that existed before the turn.
                capturePlant[i]=target;captureStance[i]=i!=pivotFoot||pivotTime<=0;
                captureReleaseTime[i]=0;captureReleaseOffset[i]=Vector3.zero;capturePoseOverride[i]=true;
            }
        }
    }
}

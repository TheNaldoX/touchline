using NUnit.Framework;
using Touchline.Core;
using UnityEngine;

namespace Touchline.Tests
{
    public class FootPreparationPresentationTests
    {
        [TestCase(30,160,1)][TestCase(60,160,1)][TestCase(120,160,1)]
        [TestCase(30,200,-1)][TestCase(60,200,-1)][TestCase(120,200,-1)]
        public void BrakingTurnPreservesActorAndContinuousGroundedSupports(int fps,int stature,int direction)
        {
            var root=new GameObject("Foot preparation continuity");
            try{
                var view=root.AddComponent<PlayerView>();view.Build(new PlayerData{id="preparation",heightCm=stature},0,9,Color.blue);
                var actor=new Actor{id="preparation",slot=9,action="run",velocity=new Point(0,4),fitness=90};
                for(int i=0;i<fps;i++){actor.previous=actor.position;actor.position+=actor.velocity/fps;view.Render(actor,1,1f/fps);}
                actor.action=MatchSimulation.FootDeliveryPreparation;actor.actionKind="shot";actor.actionSequence++;
                actor.actionContactTime=1;actor.actionTime=1;
                var left=view.FootPosition(true);var right=view.FootPosition(false);float yaw=root.transform.eulerAngles.y,maxYawRate=0,maxSettledFootStep=0;
                for(int frame=1;frame<=fps;frame++){
                    float t=(float)frame/fps;actor.angle=direction*Mathf.Min(Mathf.PI,t*5);
                    actor.actionTime=Mathf.Max(.001f,1-t);actor.velocity=new Point(0,Mathf.Max(0,4-8*t));
                    actor.previous=actor.position;actor.position+=actor.velocity/fps;
                    var ball=new Vector3(actor.position.x+Mathf.Sin(actor.angle)*.43f,.11f,actor.position.z+Mathf.Cos(actor.angle)*.43f);
                    string before=JsonUtility.ToJson(actor);
                    view.Render(actor,1,1f/fps,ball,new PlayerMotionContext{carrying=true,hasSimulationClock=true,simulationClock=t});
                    Assert.AreEqual(before,JsonUtility.ToJson(actor),"Presentation must not rewrite the contestable ball or actor state");
                    float nextYaw=root.transform.eulerAngles.y;maxYawRate=Mathf.Max(maxYawRate,Mathf.Abs(Mathf.DeltaAngle(yaw,nextYaw))*fps);yaw=nextYaw;
                    var nextLeft=view.FootPosition(true);var nextRight=view.FootPosition(false);
                    Assert.GreaterOrEqual(nextLeft.y,.025f);Assert.GreaterOrEqual(nextRight.y,.025f);
                    if(t>.65f)maxSettledFootStep=Mathf.Max(maxSettledFootStep,Vector3.Distance(left,nextLeft),Vector3.Distance(right,nextRight));
                    left=nextLeft;right=nextRight;
                }
                TestContext.WriteLine($"{fps}Hz {stature}cm yaw={maxYawRate:F3}deg/s settledFootStep={maxSettledFootStep:F5}m");
                Assert.LessOrEqual(maxYawRate,361,"Preparation uses the bounded locomotion turn instead of exponential contact snapping");
                Assert.Less(maxSettledFootStep,.035f+3f/fps,"A stopped pivot must step through its supports");
                Assert.Less(Mathf.Abs(Mathf.DeltaAngle(root.transform.eulerAngles.y,direction*180)),4);
            }finally{Object.DestroyImmediate(root);}
        }
    }
}

using System.Linq;
using NUnit.Framework;
using Touchline.Core;
using UnityEngine;

namespace Touchline.Tests
{
    public class KeeperGripTests
    {
        [TestCase(30)] [TestCase(60)] [TestCase(120)]
        public void CapturedHandsDistinguishCatchFromParryAtEveryRenderRate(int hz)
        {
            bool mode=PlayerView.UseMecanim;PlayerView.UseMecanim=true;
            try{
                foreach(bool catchBall in new[]{true,false}){
                    var go=new GameObject("Captured keeper fingers");
                    try{
                        var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="captured-grip",heightCm=185},0,0,Color.yellow);
                        var actor=new Actor{slot=0,action="idle",intent="keeper"};float dt=1f/hz;
                        for(int i=0;i<hz;i++)view.Render(actor,1,dt);
                        actor.action="dive";actor.actionKind=catchBall?"save-catch":"save-parry";actor.actionSequence=1;actor.actionContactTime=.18f;actor.actionTarget=new Point(1,.3f);actor.actionHeight=1.1f;
                        for(int i=0;i<=hz/2;i++){
                            float elapsed=i*dt;actor.actionTime=1.2f-elapsed;view.Render(actor,1,dt,new Vector3(1,1.1f,.3f));
                            if(elapsed<=actor.actionContactTime)Assert.Less(Quaternion.Angle(Finger(go).localRotation,Quaternion.identity),8,"Hand stays open until impact");
                        }
                        float curl=Quaternion.Angle(Finger(go).localRotation,Quaternion.identity);
                        if(catchBall)Assert.Greater(curl,26,"Caught ball is secured");else Assert.Less(curl,8,"Parry does not grip the ball");
                    }finally{Object.DestroyImmediate(go);}
                }
            }finally{PlayerView.UseMecanim=mode;}
        }
        static Transform Finger(GameObject go)=>go.GetComponentsInChildren<Transform>().First(t=>t.name=="finger2-2.L");
        [Test] public void FingersCloseAfterCatchButStayOpenForParry()
        {
            float[] curls=new float[2];
            for(int trial=0;trial<2;trial++){
                var go=new GameObject("Keeper grip");try{
                    var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="grip",heightCm=185},0,0,Color.yellow);
                    var actor=new Actor{action="dive",actionKind=trial==0?"save-catch":"save-parry",actionSequence=1,actionContactTime=.18f,actionHeight=1.1f,actionTarget=new Point(1,.3f)};
                    float before=0;for(int frame=0;frame<=50;frame++){actor.actionTime=1.2f-frame*.01f;view.Render(actor,1,.01f,new Vector3(1,1.1f,.3f));if(frame==17)before=Quaternion.Angle(Finger(go).localRotation,Quaternion.identity);}
                    Assert.That(before,Is.LessThan(8),"Do not close the fingers before reaching the ball");
                    curls[trial]=Quaternion.Angle(Finger(go).localRotation,Quaternion.identity);
                }finally{Object.DestroyImmediate(go);}
            }
            Assert.That(curls[0],Is.GreaterThan(26));Assert.That(curls[1],Is.LessThan(8));
        }
        [Test] public void HoldingActionOverridesPositioningIntent()
        {
            var go=new GameObject("Held ball grip");try{
                var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="hold",heightCm=185},0,0,Color.yellow);
                view.Render(new Actor{action="keeper-hold",intent="keeper",actionTime=.5f},1,.01f,new Vector3(0,1.1f,.3f));
                Assert.That(Quaternion.Angle(Finger(go).localRotation,Quaternion.identity),Is.GreaterThan(26));
            }finally{Object.DestroyImmediate(go);}
        }
    }
}

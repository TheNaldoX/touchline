using System.Linq;
using NUnit.Framework;
using Touchline.Core;
using UnityEngine;

namespace Touchline.Tests
{
    public class KeeperGripTests
    {
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

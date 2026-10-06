using NUnit.Framework;
using UnityEngine;

namespace Touchline.Tests
{
    public class BroadcastGoalGroundTests034
    {
        [TestCase(1.32f,-1,-1)] [TestCase(1.32f,-1,1)] [TestCase(1.32f,1,-1)] [TestCase(1.32f,1,1)]
        [TestCase(1.77778f,-1,-1)] [TestCase(1.77778f,-1,1)] [TestCase(1.77778f,1,-1)] [TestCase(1.77778f,1,1)]
        public void NaturalCornerFocusRetainsTheGroundContactAndCrossbarThroughoutDelivery(float aspect,int end,int flank)
        {
            var go=new GameObject("Natural corner goal ground framing");
            try{
                var camera=go.AddComponent<Camera>();camera.fieldOfView=46;camera.aspect=aspect;
                for(int frame=0;frame<=90;frame++){
                    float progress=frame/90f;
                    var ball=new Vector3(end*Mathf.Lerp(52.5f,42,progress),.11f+Mathf.Sin(progress*Mathf.PI)*3,flank*Mathf.Lerp(34,0,progress));
                    var focus=new Vector3(Mathf.Clamp(ball.x*.90f,-46,46),.6f,Mathf.Clamp(ball.z*.58f,-19,19));
                    BroadcastFraming.Apply(camera,focus,ball,false,true,end,1);
                    Check(camera,ball);
                    for(int side=-1;side<=1;side+=2){Check(camera,new Vector3(end*52.5f,0,side*3.66f));Check(camera,new Vector3(end*52.5f,2.44f,side*3.66f));}
                    Assert.Less(Vector3.Distance(camera.transform.position,focus),110,"Protect the goal's ground contacts without a runaway zoom");
                }
            }finally{Object.DestroyImmediate(go);}
        }
        static void Check(Camera camera,Vector3 point){var projected=camera.WorldToViewportPoint(point);Assert.Greater(projected.z,0);Assert.That(projected.x,Is.InRange(.10f,.9f));Assert.That(projected.y,Is.InRange(.10f,.9f));}
    }
}

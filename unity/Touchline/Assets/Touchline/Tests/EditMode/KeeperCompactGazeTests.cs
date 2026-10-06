using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Touchline.Core;

namespace Touchline.Tests
{
    public class KeeperCompactGazeTests
    {
        [TestCase(.22f)] [TestCase(1.1f)] [TestCase(1.95f)]
        public void CompactCatchTracksBallWithinNeckLimitsWithoutChangingReach(float height)
        {
            var go=new GameObject("Keeper compact gaze");
            try{
                var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="gaze",heightCm=182},0,0,Color.yellow);
                var actor=new Actor{slot=0,action="dive",actionKind="save-catch",actionSequence=1,actionTime=1,actionContactTime=.2f,actionTarget=new Point(.2f,.65f),actionHeight=height};
                var ball=new Vector3(.2f,height,.65f);view.Render(actor,1,.01f,ball);
                var head=go.GetComponentsInChildren<Transform>().First(t=>t.name=="head");
                var desired=(ball-head.position).normalized;
                Assert.Greater(Vector3.Dot(head.forward,desired),Vector3.Dot(head.parent.forward,desired)+.02f,"Looking at the ball should improve over the rigid neck");
                Assert.LessOrEqual(Mathf.Abs(Mathf.DeltaAngle(0,head.localEulerAngles.x)),35.01f);
                Assert.LessOrEqual(Mathf.Abs(Mathf.DeltaAngle(0,head.localEulerAngles.y)),25.01f);
                Assert.Less(Vector3.Distance(view.HeldBallPosition,ball),.15f);
                actor.actionTime=0;view.Render(actor,1,.01f,ball);Assert.Less(Quaternion.Angle(head.localRotation,Quaternion.identity),.01f,"The neck relaxes after recovery");
            }finally{Object.DestroyImmediate(go);}
        }
    }
}

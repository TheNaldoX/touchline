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
                actor.actionTime=0;for(int i=0;i<60;i++)view.Render(actor,1,1f/60,ball);
                var relaxed=head.localRotation;view.ResetPresentation();view.Render(actor,1,0,ball);
                Assert.Less(Quaternion.Angle(head.localRotation,relaxed),.01f,"The neck returns to the captured recovery pose, not a procedural identity rotation");
            }finally{Object.DestroyImmediate(go);}
        }
        [TestCase(30,.22f)] [TestCase(60,.22f)] [TestCase(120,.22f)]
        [TestCase(30,1.95f)] [TestCase(60,1.95f)] [TestCase(120,1.95f)]
        public void CapturedCatchGazeMovesAndReleasesContinuously(int fps,float height)
        {
            var root=new GameObject("Continuous keeper gaze");try{
                var view=root.AddComponent<PlayerView>();view.Build(new PlayerData{id="continuous-gaze",heightCm=182},0,0,Color.yellow);
                var actor=new Actor{slot=0,action="dive",actionKind="save-catch",actionSequence=1,actionContactTime=.2f,actionTarget=new Point(.2f,.65f),actionHeight=height,actionTime=1.2f};
                var ball=new Vector3(.2f,height,.65f);view.Render(actor,1,0,ball);
                var head=root.GetComponentsInChildren<Transform>().First(t=>t.name=="head");var previous=head.localRotation;
                for(int f=1;f<=fps*2;f++){
                    float t=f/(float)fps;actor.actionTime=Mathf.Max(0,1.2f-t);string before=JsonUtility.ToJson(actor);view.Render(actor,1,1f/fps,ball);
                    Assert.Less(Quaternion.Angle(previous,head.localRotation),360f/fps+1,"No neck snap on tracking or recovery");previous=head.localRotation;
                    if(f==fps/5)Assert.Less(Vector3.Distance(view.HeldBallPosition,ball),.15f,"Head tracking preserves the hand contact");
                    view.Render(actor,1,0,ball);Assert.Less(Quaternion.Angle(previous,head.localRotation),.01f,"Pause holds the head");
                    Assert.AreEqual(before,JsonUtility.ToJson(actor));Assert.AreEqual(Vector3.zero,root.transform.position);
                }
            }finally{Object.DestroyImmediate(root);}
        }
    }
}

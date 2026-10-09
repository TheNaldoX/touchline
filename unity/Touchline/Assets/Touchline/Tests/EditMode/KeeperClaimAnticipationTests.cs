using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Touchline.Core;

namespace Touchline.Tests
{
    public class KeeperClaimAnticipationTests
    {
        static MatchState Scene(float height,out Actor keeper,int side=0,int period=1)
        {
            int dir=-(side==0?1:-1)*(period==2?-1:1);
            keeper=new Actor{id="keeper",slot=0,side=side,action="idle",position=new Point(dir*49,0),previous=new Point(dir*49,0),angle=-dir*Mathf.PI*.5f};
            return new MatchState{period=period,restart=0,phase="play",actors=new[]{keeper},ball=new BallState{kind="cross",owner=null,side=1-side,from="crosser",start=new Point(dir*40,0),end=new Point(dir*50,0),duration=1,elapsed=.6f,startHeight=height,endHeight=height}};
        }
        [TestCase(.4f,0,1)] [TestCase(1.8f,0,1)] [TestCase(2.2f,0,1)] [TestCase(2.2f,1,1)] [TestCase(2.2f,0,2)] [TestCase(2.2f,1,2)]
        public void IncomingTrajectoryProducesReadinessWithoutChangingState(float height,int side,int period)
        {
            var state=Scene(height,out var keeper,side,period);string before=JsonUtility.ToJson(state);
            var sample=KeeperClaimAnticipation.Evaluate(state,keeper,1);
            Assert.IsTrue(sample.active);Assert.That(sample.remaining,Is.InRange(.18f,.21f));Assert.That(sample.height,Is.EqualTo(height).Within(.0001f));
            Assert.That(Point.Distance(sample.target,keeper.position),Is.EqualTo(1.05f).Within(.0001f));Assert.AreEqual(before,JsonUtility.ToJson(state));
        }
        [TestCase("shot")] [TestCase("none")] [TestCase("loose")]
        public void DoesNotPreemptShotSavesOrUnknownFlight(string kind)
        {
            var state=Scene(2.2f,out var keeper);state.ball.kind=kind;Assert.IsFalse(KeeperClaimAnticipation.Evaluate(state,keeper,1).active);
        }
        [Test] public void TrajectoryChangesOpponentsAndOtherActionsInvalidatePrediction()
        {
            var state=Scene(2.2f,out var keeper);state.ball.end=new Point(-30,0);Assert.IsFalse(KeeperClaimAnticipation.Evaluate(state,keeper,1).active);
            state=Scene(2.2f,out keeper);keeper.action="dive";Assert.IsFalse(KeeperClaimAnticipation.Evaluate(state,keeper,1).active);
            state=Scene(2.2f,out keeper);keeper.controlTime=.2f;Assert.IsFalse(KeeperClaimAnticipation.Evaluate(state,keeper,1).active);
            state=Scene(2.2f,out keeper);state.ball.side=keeper.side;Assert.IsFalse(KeeperClaimAnticipation.Evaluate(state,keeper,1).active);
            state=Scene(2.2f,out keeper);state.actors=new[]{keeper,new Actor{id="blocker",side=1,slot=9,position=new Point(-47,0)}};Assert.IsFalse(KeeperClaimAnticipation.Evaluate(state,keeper,1).active);
            state.actors[1].sentOff=true;Assert.IsTrue(KeeperClaimAnticipation.Evaluate(state,keeper,1).active);
        }
        [TestCase(30)] [TestCase(60)] [TestCase(120)]
        public void PreparationRaisesRealWristsProgressivelyBeforeClaim(int fps)
        {
            var state=Scene(2.2f,out var keeper);var go=new GameObject("Claim anticipation timing");
            try{
                go.transform.rotation=Quaternion.Euler(0,90,0);var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="keeper",heightCm=182},0,0,Color.yellow);
                var right=go.GetComponentsInChildren<Transform>().First(t=>t.name=="wrist.R");Vector3 previous=Vector3.zero;float maxSpeed=0;
                for(int frame=0;frame<=Mathf.FloorToInt(.34f*fps);frame++){
                    state.ball.elapsed=.45f+frame/(float)fps;var sample=KeeperClaimAnticipation.Evaluate(state,keeper,1);
                    view.Render(keeper,1,1f/fps,new Vector3(sample.target.x,sample.height,sample.target.z),new PlayerMotionContext{keeperClaim=sample});
                    if(frame>0)maxSpeed=Mathf.Max(maxSpeed,Vector3.Distance(right.position,previous)*fps);previous=right.position;
                }
                Assert.Greater(right.position.y,1.8f,"Hands need to be high before the keeper obtains the ball");Assert.Less(maxSpeed,12,"No one-frame catch snap");
                var pending=KeeperClaimAnticipation.Evaluate(state,keeper,1);keeper.action="claim";keeper.actionKind=MatchSimulation.RecordedKeeperClaim;keeper.actionSequence=2;keeper.actionTime=.8f;keeper.actionContactTime=.12f;keeper.actionTarget=pending.target;keeper.actionHeight=pending.height;
                view.Render(keeper,1,1f/fps,new Vector3(pending.target.x,pending.height,pending.target.z));
                Assert.Less(Vector3.Distance(right.position,previous),.13f,"Obtaining possession must preserve the prepared pose");
            }finally{Object.DestroyImmediate(go);}
        }
        [TestCase(30,false)] [TestCase(60,false)] [TestCase(120,false)]
        [TestCase(30,true)] [TestCase(60,true)] [TestCase(120,true)]
        public void ReadinessPreservesCapturedFeetAndReleasesCanceledTrajectory(int fps,bool moving)
        {
            var state=Scene(2.2f,out var keeper);var go=new GameObject("Ready keeper");var reference=new GameObject("Captured keeper");
            try{
                var view=go.AddComponent<PlayerView>();var captured=reference.AddComponent<PlayerView>();
                view.Build(new PlayerData{id="keeper",heightCm=182},0,0,Color.yellow);captured.Build(new PlayerData{id="keeper",heightCm=182},0,0,Color.yellow);
                var wrist=go.GetComponentsInChildren<Transform>().First(t=>t.name=="wrist.R");var control=reference.GetComponentsInChildren<Transform>().First(t=>t.name=="wrist.R");
                Vector3 previous=Vector3.zero;float maximum=0,raised=0;
                for(int frame=0;frame<=fps;frame++){
                    float time=frame/(float)fps;state.ball.elapsed=.45f+time;
                    keeper.previous=keeper.position;if(moving&&time>.34f){keeper.velocity=new Point(0,3.5f);keeper.position+=keeper.velocity/fps;keeper.action="run";}
                    var sample=time<=.34f?KeeperClaimAnticipation.Evaluate(state,keeper,1):default;
                    view.Render(keeper,1,1f/fps,default,new PlayerMotionContext{keeperClaim=sample});captured.Render(keeper,1,1f/fps);
                    Assert.Less(Vector3.Distance(view.FootPosition(true),captured.FootPosition(true)),.001f);
                    Assert.Less(Vector3.Distance(view.FootPosition(false),captured.FootPosition(false)),.001f);
                    Assert.AreEqual(reference.transform.position,go.transform.position);
                    if(frame>0)maximum=Mathf.Max(maximum,Vector3.Distance(wrist.position,previous)*fps);previous=wrist.position;raised=Mathf.Max(raised,wrist.position.y-control.position.y);
                }
                Assert.Greater(raised,.5f,"The rendered keeper actually prepares the catch");
                Assert.Less(maximum,8,"Cancellation must blend out without snapping the hands");
                Assert.Less(Vector3.Distance(wrist.position,control.position),.02f,"Return to captured idle after cancellation");
            }finally{Object.DestroyImmediate(go);Object.DestroyImmediate(reference);}
        }
    }
}

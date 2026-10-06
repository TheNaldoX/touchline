using System.Linq;
using NUnit.Framework;
using Touchline.Core;
using UnityEngine;
namespace Touchline.Tests
{
    public class SlidingEntryContinuityTests
    {
        [TestCase(160,1,30,20)] [TestCase(182,-1,30,31)] [TestCase(205,1,30,43)]
        [TestCase(160,-1,120,20)] [TestCase(182,1,120,31)] [TestCase(205,-1,120,43)]
        public void EnteringASlideKeepsTheOutgoingRunningPoseAtElapsedZero(int height,int side,int fps,int warmFrames)
        {
            var go=new GameObject("Slide entry");
            try{
                var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="slide-entry",heightCm=height},0,2,Color.white);
                var actor=new Actor{slot=2,action="run",velocity=new Point(0,4.8f)};
                for(int i=0;i<warmFrames;i++){actor.previous=actor.position;actor.position+=actor.velocity*(1f/fps);actor.stride+=4.8f/fps;view.Render(actor,1,1f/fps);}
                var bones=go.GetComponentsInChildren<Transform>();string[] names={"upperleg01.L","upperleg01.R","lowerleg01.L","lowerleg01.R","foot.L","foot.R","wrist.L","wrist.R"};
                var joints=names.Select(n=>bones.Single(b=>b.name==n)).ToArray();var before=joints.Select(j=>go.transform.InverseTransformPoint(j.position)).ToArray();
                var root=go.transform.position;var ball=root+new Vector3(side*.12f,.11f,.14f);
                actor.previous=actor.position;actor.action="slide";actor.actionKind=MatchSimulation.SlidingDuel;actor.actionSequence=1;actor.actionTime=MatchSimulation.SlidingDuelDuration;actor.actionContactTime=MatchSimulation.SlidingDuelContact;actor.diveSide=side;actor.actionTarget=new Point(ball.x,ball.z);actor.actionHeight=.11f;
                view.Render(actor,1,1f/fps,ball);
                var steps=joints.Select((j,i)=>Vector3.Distance(before[i],go.transform.InverseTransformPoint(j.position))*go.transform.localScale.y).ToArray();
                TestContext.WriteLine(string.Join("; ",names.Select((name,i)=>name+" entryStep="+steps[i].ToString("F6")+"m")));
                Assert.Less(steps.Max(),.003f,"Elapsed zero must preserve the outgoing pose rather than snap running limbs to the slide stance");
                var times=Enumerable.Range(1,Mathf.CeilToInt(fps*MatchSimulation.SlidingDuelContact)).Select(i=>Mathf.Min(i/(float)fps,MatchSimulation.SlidingDuelContact)).Append(MatchSimulation.SlidingDuelContact).Distinct().OrderBy(t=>t).ToArray();
                float last=0;
                foreach(float time in times){actor.actionTime=MatchSimulation.SlidingDuelDuration-time;view.Render(actor,1,time-last,ball);last=time;Assert.AreEqual(root,go.transform.position);}
                Assert.Less(Vector3.Distance(view.BootContactPosition(side>0),ball),.03f,"The entry blend must finish before exact accepted contact");
            }finally{Object.DestroyImmediate(go);}
        }
    }
}

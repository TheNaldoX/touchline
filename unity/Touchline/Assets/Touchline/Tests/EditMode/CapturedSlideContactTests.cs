using System.Linq;
using NUnit.Framework;
using Touchline.Core;
using UnityEngine;

namespace Touchline.Tests
{
    public class CapturedSlideContactTests
    {
        [TestCase(160,30,1)][TestCase(160,30,-1)]
        [TestCase(182,60,1)][TestCase(182,60,-1)]
        [TestCase(205,120,1)][TestCase(205,120,-1)]
        public void PhysicalEntryAndContactRemainStableDuringRepeatedPausedFrames(int height,int fps,int side)
        {
            var go=new GameObject("Captured slide pause");try{
                var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="slide-pause",heightCm=height},0,3,Color.blue);
                var actor=new Actor{id="slide-pause",slot=3,action="run",velocity=new Point(0,4)};
                float dt=1f/fps;
                for(int i=0;i<fps;i++){actor.previous=actor.position;actor.position+=actor.velocity*dt;view.Render(actor,1,dt);}
                var joints=go.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("upperleg01")||t.name.StartsWith("lowerleg01")||t.name.StartsWith("foot.")||t.name.StartsWith("wrist.")).ToArray();
                var before=joints.Select(j=>j.position).ToArray();
                actor.previous=actor.position;actor.velocity=new Point();actor.action="slide";actor.actionKind=MatchSimulation.SlidingDuel;actor.actionSequence=1;actor.diveSide=side;actor.actionTime=MatchSimulation.SlidingDuelDuration;actor.actionContactTime=MatchSimulation.SlidingDuelContact;
                var ball=go.transform.position+new Vector3(side*.12f,.11f,.65f);actor.actionTarget=new Point(ball.x,ball.z);actor.actionHeight=ball.y;
                string state=JsonUtility.ToJson(actor);
                for(int f=0;f<20;f++){view.Render(actor,1,dt,ball);for(int j=0;j<joints.Length;j++)Assert.Less(Vector3.Distance(before[j],joints[j].position),.003f);}
                Assert.AreEqual(state,JsonUtility.ToJson(actor));
                float elapsed=0;
                while(elapsed<MatchSimulation.SlidingDuelContact){float next=Mathf.Min(MatchSimulation.SlidingDuelContact,elapsed+dt);actor.actionTime=MatchSimulation.SlidingDuelDuration-next;view.Render(actor,1,next-elapsed,ball);elapsed=next;}
                Assert.Less(Vector3.Distance(view.BootContactPosition(side>0),ball),.05f);
                before=joints.Select(j=>j.position).ToArray();state=JsonUtility.ToJson(actor);
                for(int f=0;f<20;f++){view.Render(actor,1,0,ball);for(int j=0;j<joints.Length;j++)Assert.Less(Vector3.Distance(before[j],joints[j].position),.003f);}
                Assert.AreEqual(state,JsonUtility.ToJson(actor));
            }finally{Object.DestroyImmediate(go);}
        }
    }
}

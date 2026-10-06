using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Touchline.Core;

namespace Touchline.Tests
{
    public class KeeperReadinessThresholdTests
    {
        [TestCase(30,false)] [TestCase(60,false)] [TestCase(120,false)]
        [TestCase(30,true)] [TestCase(60,true)] [TestCase(120,true)]
        public void CrossingReadyStanceSpeedDoesNotSnapPreparedWrists(int fps,bool slowing)
        {
            var go=new GameObject("Ready stance speed boundary");
            try{
                var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="threshold",heightCm=182},0,0,Color.yellow);
                var actor=new Actor{slot=0,action="run",velocity=new Point(0,slowing?2.51f:2.49f),fitness=90};
                var context=new PlayerMotionContext{keeperClaim=new KeeperClaimAnticipationSample{active=true,target=new Point(0,.75f),height=2.2f,remaining=.175f,weight=.5f}};
                var ball=new Vector3(0,2.2f,.75f);for(int frame=0;frame<30;frame++)view.Render(actor,1,1f/fps,ball,context);
                var joints=go.GetComponentsInChildren<Transform>();var left=joints.First(t=>t.name=="wrist.L");var right=joints.First(t=>t.name=="wrist.R");var beforeLeft=left.position;var beforeRight=right.position;
                actor.velocity=new Point(0,slowing?2.49f:2.51f);view.Render(actor,1,1f/fps,ball,context);
                Assert.Less(Vector3.Distance(left.position,beforeLeft),.03f,"A0.02m/s speed change cannot abruptly replace the left ready arm");
                Assert.Less(Vector3.Distance(right.position,beforeRight),.03f,"A0.02m/s speed change cannot abruptly replace the right ready arm");
            }finally{Object.DestroyImmediate(go);}
        }
    }
}

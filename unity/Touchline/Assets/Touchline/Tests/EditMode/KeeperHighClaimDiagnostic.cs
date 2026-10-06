using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Touchline.Core;

namespace Touchline.Tests
{
    // Baseline diagnostic, intentionally staged outside Assets. A failing
    // contact assertion demonstrates a missing high-claim pose, not a request
    // to change the successful interception or increase reach tolerances.
    public class KeeperHighClaimDiagnostic
    {
        [TestCase(1.1f)] [TestCase(1.8f)] [TestCase(2.2f)]
        public void HandsShouldReachAnAuthoritativeKeeperClaim(float height)
        {
            var db=new Database{players=Enumerable.Range(0,40).Select(i=>new PlayerData{id="claim"+i,name="P"+i,team=i<20?"a":"b",position=i%20==0?"GB":i%20<5?"DEF":i%20<9?"MIL":"ATT",rating=80,heightCm=182}).ToArray()};
            var career=new Career{club="a"};career.lineup=Career.Select(db,"a",career.tactic);var sim=MatchSimulation.Create(db,career,"b",91,2700);var m=sim.State;
            m.restart=0;m.phase="play";m.clock=100;m.decision=10;foreach(var actor in m.actors)actor.sentOff=true;
            var keeper=m.actors[0];keeper.sentOff=false;keeper.position=keeper.previous=new Point(-49,0);keeper.angle=Mathf.PI*.5f;keeper.action="idle";
            // Begin inside the contact radius at the renderer's own 0.48 m
            // forward reach to isolate height from the separate lateral sweep.
            m.ball=new BallState{kind="pass",side=1,lastTouch=1,from=m.actors[20].id,previous=new Point(-48.52f,0),position=new Point(-49,0),height=height,previousHeight=height,elapsed=.6f,velocity=new Point(-30,0)};
            bool hit=(bool)typeof(MatchSimulation).GetMethod("ResolveReception",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(sim,null);
            Assert.IsTrue(hit);Assert.AreEqual("claim",keeper.action);Assert.IsTrue(sim.KeeperContact.valid);Assert.IsTrue(sim.KeeperContact.caught);
            var contact=sim.KeeperContact;var ball=new Vector3(contact.impact.x,contact.height,contact.impact.z);
            var go=new GameObject("High claim diagnostic");
            try{
                go.transform.rotation=Quaternion.Euler(0,90,0);var view=go.AddComponent<PlayerView>();view.Build(db.Find(keeper.id),0,0,Color.yellow);
                view.Render(keeper,1,.01f,ball);float distance=Vector3.Distance(view.HeldBallPosition,ball);
                TestContext.WriteLine("Actual interception: height="+height+" gap="+distance+" hands="+view.HeldBallPosition+" impact="+ball);
                Assert.Less(distance,.17f,"The accepted catch should not teleport from its physical impact to lower hands");
            }finally{Object.DestroyImmediate(go);}
        }
    }
}

// Copy into EditMode only after integrating the keeper snapshot hunks.
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Touchline.Core;

namespace Touchline.Tests
{
    public class KeeperLaunchIntegrationTests
    {
        [TestCase(2f,.22f)] [TestCase(2f,1.1f)] [TestCase(2f,2.2f)]
        [TestCase(2.3f,.22f)] [TestCase(2.3f,1.1f)] [TestCase(2.3f,2.2f)]
        [TestCase(2.6f,.22f)] [TestCase(2.6f,1.1f)] [TestCase(2.6f,2.2f)]
        public void AnyDistantSaveMustHaveActualRootTravelAndReachBallWithTheRenderedGlove(float lateral,float height)
        {
            var db=new Database{players=Enumerable.Range(0,40).Select(i=>new PlayerData{id="launch"+i,name="L"+i,team=i<20?"a":"b",position=i%20==0?"GB":i%20<5?"DEF":i%20<9?"MIL":"ATT",rating=80,heightCm=180}).ToArray()};
            var career=new Career{club="a"};career.lineup=Career.Select(db,"a",career.tactic);var sim=MatchSimulation.Create(db,career,"b",731,2700);var m=sim.State;
            foreach(var actor in m.actors)actor.sentOff=true;
            var keeper=m.actors[11];keeper.sentOff=false;keeper.position=keeper.previous=new Point(48,0);keeper.angle=-Mathf.PI*.5f;
            m.restart=0;m.phase="play";m.clock=10;m.shots[0]=1;
            m.ball=new BallState{kind="shot",side=0,from=m.actors[9].id,start=new Point(30,lateral),end=new Point(54,lateral),duration=.9f,elapsed=.35f,
                position=new Point(30+24*(.35f/.9f),lateral),previous=new Point(30+24*(.35f/.9f),lateral),height=height,previousHeight=height,startHeight=height,endHeight=height};
            var original=keeper.position;
            for(int i=0;i<9&&m.metrics[1].saves==0&&m.score[0]==0;i++)sim.Advance(.1);
            TestContext.WriteLine("Target "+lateral+"m / "+height+"m: saves="+m.metrics[1].saves+", goals="+m.score[0]+", root="+Point.Distance(original,keeper.position));
            Assert.LessOrEqual(Point.Distance(original,keeper.position),1.1f,"A dive cannot teleport the root to the ball");
            if(m.metrics[1].saves==0)return; // An unreachable shot must remain missable.
            Assert.Greater(Point.Distance(original,keeper.position),.15f,"Extended coverage must come from physical movement");
            var go=new GameObject("Natural distant keeper impact");var baked=new Mesh();try{
                var view=go.AddComponent<PlayerView>();view.Build(db.Find(keeper.id),1,0,Color.yellow);go.transform.rotation=Quaternion.Euler(0,keeper.angle*Mathf.Rad2Deg,0);
                float remaining=1.2f-keeper.actionContactTime;
                float alpha=Mathf.Clamp01(1-(remaining-keeper.actionTime)/MatchSimulation.Step);
                var point=new Vector3(keeper.actionTarget.x,keeper.actionHeight,keeper.actionTarget.z);view.Render(keeper,alpha,1f/60,point);
                float nearest=100;
                foreach(var skin in go.GetComponentsInChildren<SkinnedMeshRenderer>())if(skin.name.Contains("glove")||skin.name.Contains("Glove")){
                    skin.BakeMesh(baked);foreach(var vertex in baked.vertices)nearest=Mathf.Min(nearest,Vector3.Distance(skin.transform.TransformPoint(vertex),point));
                }
                TestContext.WriteLine("Actual glove-to-ball centre distance "+nearest+"m at "+keeper.actionContactTime+"s");
                Assert.Less(nearest,.17f,"Do not count a save beyond the rendered glove's physical reach");
                if(keeper.actionKind=="save-catch")Assert.Less(Vector3.Distance(view.HeldBallPosition,point),.17f);
            }finally{Object.DestroyImmediate(baked);Object.DestroyImmediate(go);}
        }
    }
}

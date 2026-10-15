using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Touchline.Core;

namespace Touchline.Tests
{
    public class KeeperWideShotReadingTests
    {
        [TestCase(0,1,6f,false)] [TestCase(1,1,-6f,false)]
        [TestCase(0,2,-6f,false)] [TestCase(1,2,6f,false)]
        [TestCase(0,1,3.5f,true)] [TestCase(1,2,-4f,true)]
        public void ClearlyWideFlightDoesNotLaunchButNearPostShotsStillDo(int side,int half,float lateral,bool launch)
        {
            var db=new Database{players=Enumerable.Range(0,40).Select(i=>new PlayerData{id="wide"+i,name="Player"+i,team=i<20?"a":"b",position=i%20==0?"GB":i%20<5?"DEF":i%20<9?"MIL":"ATT",rating=80}).ToArray()};
            var career=new Career{club="a"};career.lineup=Career.Select(db,"a",career.tactic);var sim=MatchSimulation.Create(db,career,"b",731,2700);
            sim.State.period=half;var keeper=sim.State.actors[side*11];int towardOwn=-sim.Direction(side);
            keeper.position=keeper.previous=new Point(towardOwn*48,0);keeper.action="idle";keeper.keeperLaunchRemaining=0;
            sim.State.ball=new BallState{kind="shot",side=1-side,start=new Point(towardOwn*30,lateral),end=new Point(towardOwn*54,lateral),elapsed=.5f,duration=.9f,startHeight=1,endHeight=1};
            var target=new Point(towardOwn*49,0);uint seed=sim.State.seed;
            var result=(Point)typeof(MatchSimulation).GetMethod("AnticipateKeeperShot",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(sim,new object[]{keeper,target});
            Assert.AreEqual(launch,keeper.action=="dive");Assert.AreEqual(seed,sim.State.seed,"Reading a flight does not draw random numbers");
            if(!launch){Assert.AreEqual(target,result);Assert.AreEqual(0,keeper.keeperLaunchRemaining);}else Assert.Greater(keeper.keeperLaunchRemaining,0);
        }
    }
}

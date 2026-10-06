using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Touchline.Core;

namespace Touchline.Tests
{
    public class KeeperHandlingTests
    {
        Database db;MatchSimulation sim;Actor keeper;
        [SetUp] public void Setup(){db=new Database{players=Enumerable.Range(0,40).Select(i=>new PlayerData{id="p"+i,name="P"+i,team=i<20?"a":"b",position=i%20==0?"GB":i%20<5?"DEF":i%20<9?"MIL":"ATT",rating=70}).ToArray()};var c=new Career{club="a"};c.lineup=Career.Select(db,"a",c.tactic);sim=MatchSimulation.Create(db,c,"b");var m=sim.State;m.restart=0;m.phase="play";m.decision=10;foreach(var p in m.actors)p.previous=p.position=new Point(20,p.slot*2-10);keeper=m.actors[0];keeper.previous=keeper.position=new Point(-48,0);keeper.angle=Mathf.PI*.5f;m.ball=new BallState{kind="loose",position=keeper.position,previous=keeper.position,side=1,lastTouch=1,height=.3f};}
        void Catch(){sim.Advance(.1);Assert.AreEqual(keeper.id,sim.State.ball.owner);Assert.IsTrue(sim.State.ball.held);}
        [Test] public void CatchSettlesAndPlacesBallBeforeKicking()
        {
            Catch();var actions=new HashSet<string>();float previous=1.1f;bool placed=false;
            for(int i=0;i<50;i++){var b=sim.State.ball;actions.Add(keeper.action);if(b.held)Assert.AreEqual(0,sim.State.passes[0]);if(keeper.action=="place-ball"){Assert.LessOrEqual(b.height,previous+.001f);Assert.Less(previous-b.height,.26f);previous=b.height;placed=true;}if(keeper.action=="keeper-rise"){Assert.IsFalse(b.held);Assert.That(b.height,Is.EqualTo(.11f).Within(.001));}sim.Advance(.1);}
            Assert.IsTrue(placed);foreach(var action in new[]{"claim","keeper-hold","place-ball","keeper-rise"})Assert.IsTrue(actions.Contains(action),action);Assert.Greater(sim.State.passes[0]+sim.State.events.Count(e=>e.kind=="clearance"&&e.side==0),0);
        }
        [Test] public void KeeperCanPassToNearbySupportAfterPuttingBallDown(){sim.State.actors[2].position=new Point(-30,10);Catch();sim.Advance(4);Assert.Greater(sim.State.passes[0],0);Assert.IsFalse(sim.State.ball.held);}
        [Test] public void OpponentCannotTackleBallOutOfKeepersHands(){Catch();var p=sim.State.actors[20];p.position=keeper.position+new Point(.3f,0);sim.Advance(.5);Assert.IsTrue(sim.State.ball.held);Assert.AreEqual(keeper.id,sim.State.ball.owner);Assert.AreEqual("hold",sim.Decide(keeper));}
        [Test] public void TeamMateBackPassIsControlledWithFeet(){sim.State.ball.side=sim.State.ball.lastTouch=0;sim.Advance(.1);Assert.AreEqual(keeper.id,sim.State.ball.owner);Assert.IsFalse(sim.State.ball.held);Assert.AreEqual("control",keeper.action);}
        [Test] public void PlacingStateResumesIdenticallyAfterSave(){Catch();for(int i=0;i<30&&keeper.action!="place-ball";i++)sim.Advance(.1);Assert.AreEqual("place-ball",keeper.action);sim.Advance(.2);var resumed=new MatchSimulation(db,JsonUtility.FromJson<MatchState>(JsonUtility.ToJson(sim.State)));sim.Advance(6);resumed.Advance(6);Assert.AreEqual(JsonUtility.ToJson(sim.State),JsonUtility.ToJson(resumed.State));}
        [Test] public void ExistingSaveDuringCatchRestoresHandPossession(){Catch();sim.State.ball.held=false;var resumed=new MatchSimulation(db,JsonUtility.FromJson<MatchState>(JsonUtility.ToJson(sim.State)));Assert.IsTrue(resumed.State.ball.held);}
        [Test] public void DismissedKeeperReleasesHandPossession(){Catch();sim.SendOff(keeper.id);Assert.IsFalse(sim.State.ball.held);Assert.IsNull(sim.State.ball.owner);Assert.AreEqual("loose",sim.State.ball.kind);}
        [Test] public void PlacementHandsFollowTheSimulationBallWithGroundedFeet()
        {
            var go=new GameObject("Keeper placing");var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="keeper"},0,0,Color.yellow);var actor=new Actor{action="place-ball"};
            try{for(int i=0;i<=30;i++){float phase=i/30f,t=Mathf.SmoothStep(0,1,phase);actor.actionTime=MatchSimulation.KeeperPlaceDuration*(1-phase);var ball=new Vector3(0,Mathf.Lerp(1.1f,.11f,t),Mathf.Lerp(.36f,.42f,t));view.Render(actor,1,1f/60,ball);Assert.Less(Vector3.Distance(view.HeldBallPosition,ball),.16f,"Hand/ball contact at "+phase);Assert.Greater(view.FootPosition(true).y,-.025f);Assert.Greater(view.FootPosition(false).y,-.025f);}}
            finally{Object.DestroyImmediate(go);}
        }
    }
}

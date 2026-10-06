using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Touchline.Core;

namespace Touchline.Tests
{
    public class BallContinuityTests
    {
        Database db;MatchSimulation sim;MatchState m;
        [SetUp] public void Setup(){db=new Database{players=Enumerable.Range(0,40).Select(i=>new PlayerData{id="p"+i,name="P"+i,team=i<20?"a":"b",position=i%20==0?"GB":i%20<5?"DEF":i%20<9?"MIL":"ATT",rating=70}).ToArray()};var c=new Career{club="a"};c.lineup=Career.Select(db,"a",c.tactic);sim=MatchSimulation.Create(db,c,"b");m=sim.State;m.restart=0;m.phase="play";m.decision=100;foreach(var a in m.actors){a.position=new Point(-20,a.slot*2-10);a.previous=a.position;}m.ball=new BallState{kind="loose",height=.11f};}
        void Ball(Point p,Point v,int side=0){m.ball.position=p;m.ball.previous=p;m.ball.velocity=v;m.ball.side=side;m.ball.lastTouch=side;}
        [Test] public void RollingBallLosesSpeedGradually(){Ball(new Point(0,20),new Point(8,0));sim.Advance(.1);Assert.That(m.ball.position.x,Is.EqualTo(.8f).Within(.01));Assert.That(m.ball.velocity.x,Is.InRange(7.7f,8));sim.Advance(.2);Assert.Greater(m.ball.position.x,2);}
        [Test] public void FallingBallBouncesAndRemainsFree(){Ball(new Point(0,20),new Point(3,0));m.ball.height=.15f;m.ball.verticalVelocity=-3;sim.Advance(.1);Assert.Greater(m.ball.verticalVelocity,0);Assert.AreEqual(.11f,m.ball.height);Assert.IsNull(m.ball.owner);}
        [Test] public void PartiallyCrossedGoalLineDoesNotAwardGoal(){Ball(new Point(52.55f,0),new Point());sim.Advance(.1);Assert.AreEqual(0,m.score.Sum());Assert.AreEqual("play",m.phase);}
        [Test] public void DeflectedBallCanScoreOwnGoal(){Ball(new Point(52,0),new Point(9,0),1);m.ball.lastTouchId=m.actors[12].id;sim.Advance(.1);Assert.AreEqual(1,m.score[0]);Assert.AreEqual(1,m.metrics[1].ownGoals);Assert.AreEqual("goal",m.phase);}
        [Test] public void OnTargetShotDeflectionIsNotCountedAsOwnGoal(){Ball(new Point(52,0),new Point(9,0),1);m.ball.goalAttempt=true;m.ball.shotOnTarget=true;m.ball.shotSide=0;m.ball.from=m.actors[9].id;sim.Advance(.1);Assert.AreEqual(1,m.score[0]);Assert.AreEqual(0,m.metrics[1].ownGoals);Assert.AreEqual(1,m.metrics[0].shotsOnTarget);}
        [Test] public void ThrowCannotScoreDirectly(){Ball(new Point(52,0),new Point(9,0));m.ball.directThrow=true;sim.Advance(.1);Assert.AreEqual(0,m.score.Sum());Assert.AreEqual("goal-kick",m.phase);}
        [Test] public void PostReboundKeepsBallInPlay(){Ball(new Point(52,3.66f),new Point(12,0));sim.Advance(.1);Assert.AreEqual("play",m.phase);Assert.Less(m.ball.velocity.x,0);Assert.AreEqual(1,m.events.Count(e=>e.kind=="woodwork"));}
        [Test] public void CollisionUsesRelativeMotionAndEarliestContact(){var start=new Point();var end=new Point(10,0);float near=MatchSimulation.ContactFraction(start,end,new Point(3,0),new Point(3,0),.5f);float far=MatchSimulation.ContactFraction(start,end,new Point(8,0),new Point(8,0),.5f);Assert.That(near,Is.EqualTo(.25f).Within(.001));Assert.Less(near,far);Assert.AreEqual(2,MatchSimulation.ContactFraction(start,end,new Point(5,3),new Point(5,2),.5f));}
        [Test] public void TechniqueAndPressureChangeFirstTouchDifficulty(){var p=m.actors[9];db.Find(p.id).attributes=new[]{new AttributeValue{key="ballControl",value=95}};float good=sim.FirstTouchError(p,20,8);db.Find(p.id).attributes[0].value=35;float poor=sim.FirstTouchError(p,20,8);Assert.Greater(poor,good);Assert.Greater(sim.FirstTouchError(p,20,.5f),poor);}
        [Test] public void LooseBallDrawsAChaserFromEachTeam(){Ball(new Point(0,20),new Point(6,0));sim.Advance(.1);foreach(int side in new[]{0,1})Assert.AreEqual(1,m.actors.Count(a=>a.side==side&&a.intent=="loose-ball"));}
        [Test] public void KeeperSweepsOnlyWithinPenaltyArea(){Ball(new Point(42,0),new Point(-2,0));m.actors[11].position=new Point(48,0);sim.Advance(.1);Assert.Less(sim.MovementTarget(11).x,44);Assert.Greater(sim.MovementTarget(11).x,36);}
        [Test] public void KeeperNarrowsAngleWhenStrikerBreaksClear(){var striker=m.actors[9];striker.position=new Point(39,0);striker.carryTarget=new Point(45,0);m.ball.owner=striker.id;m.ball.position=striker.position;m.ball.controlOrigin=striker.position;m.actors[11].position=new Point(50,0);sim.Advance(.1);Assert.Less(sim.MovementTarget(11).x,48);}
        [Test] public void OpponentWaitsForStoppageAndGroupsTiredReplacements(){m.clock=500;m.period=2;m.awayReviewAt=480;m.score[0]=1;foreach(var a in m.actors.Where(a=>a.side==1&&a.slot>0))a.fitness=50;string home=JsonUtility.ToJson(m.homeTactic);sim.Advance(.1);Assert.AreEqual(0,m.substitutions[1]);Assert.AreEqual("chase",m.awayPlan);m.restart=3;m.phase="throw-in";m.restartSide=0;sim.Advance(.1);Assert.AreEqual(2,m.substitutions[1]);Assert.AreEqual(1,m.awayWindows.Count);Assert.AreEqual(home,JsonUtility.ToJson(m.homeTactic));}
        [Test] public void NewPhysicsAndOpponentStateSurviveReload(){m.clock=500;m.period=2;m.awayReviewAt=480;Ball(new Point(0,20),new Point(9,0));m.ball.height=1;m.ball.verticalVelocity=-1;var resumed=new MatchSimulation(db,JsonUtility.FromJson<MatchState>(JsonUtility.ToJson(m)));sim.Advance(10);resumed.Advance(10);Assert.AreEqual(JsonUtility.ToJson(m),JsonUtility.ToJson(resumed.State));}
        [Test] public void KeeperContactsShotBeforeDefenderBehindHimAndParriesAwayFromGoal(){
            var keeper=m.actors[11];keeper.position=new Point(49.7f,0);keeper.action="dive";keeper.actionTime=.8f;db.Find(keeper.id).attributes=new[]{new AttributeValue{key="gkDiving",value=99}};m.actors[12].position=new Point(51.5f,1.2f);
            m.ball=new BallState{kind="shot",from=m.actors[9].id,side=0,lastTouch=0,position=new Point(48,1.2f),start=new Point(48,1.2f),end=new Point(53.4f,1.2f),elapsed=.3f,duration=.5f,height=.3f,startHeight=.3f,endHeight=.3f,goalAttempt=true,shotOnTarget=true,shotSide=0};m.shots[0]=1;
            sim.Advance(.1);Assert.AreEqual(1,m.metrics[1].saves);Assert.AreEqual(0,m.events.Count(e=>e.kind=="block"));Assert.AreEqual("loose",m.ball.kind);Assert.IsFalse(m.ball.held);Assert.Less(m.ball.velocity.x,0);Assert.AreEqual(1,m.metrics[0].shotsOnTarget);
        }
        [Test] public void KeeperOutsidePenaltyAreaUsesFeet(){Ball(new Point(35,0),new Point());m.actors[11].position=new Point(35,0);sim.Advance(.1);Assert.AreEqual(m.actors[11].id,m.ball.owner);Assert.AreEqual(0,m.metrics[1].keeperClaims);Assert.AreEqual("control",m.actors[11].action);}
    }
}

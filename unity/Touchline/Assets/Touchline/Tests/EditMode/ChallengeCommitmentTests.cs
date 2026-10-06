using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Touchline.Core;

namespace Touchline.Tests
{
    public class ChallengeCommitmentTests
    {
        Database db;MatchSimulation sim;Actor owner,defender;
        object Call(string name,params object[] args)=>typeof(MatchSimulation).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(sim,args);
        [SetUp] public void Setup()
        {
            db=new Database{players=Enumerable.Range(0,40).Select(i=>new PlayerData{id="p"+i,name="P"+i,team=i<20?"a":"b",position=i%20==0?"GB":i%20<5?"DEF":i%20<9?"MIL":"ATT",rating=75}).ToArray()};
            var career=new Career{club="a"};career.lineup=Career.Select(db,"a",career.tactic);sim=MatchSimulation.Create(db,career,"b");
            sim.State.restart=0;sim.State.phase="play";sim.State.decision=10;sim.State.professionalRules=true;
            foreach(var actor in sim.State.actors){actor.sentOff=true;actor.position=actor.previous=new Point(30,20);}
            owner=sim.State.actors[9];defender=sim.State.actors[12];owner.sentOff=defender.sentOff=false;
            Place(new Point(),new Point(.8f,.43f));
        }
        void Place(Point carrier,Point opponent)
        {
            owner.previous=owner.position=owner.carryTarget=carrier;owner.angle=0;
            defender.previous=defender.position=opponent;defender.angle=-(float)System.Math.PI*.5f;
            var ball=carrier+new Point(0,.43f);
            sim.State.possessionSide=0;sim.State.ball=new BallState{owner=owner.id,side=0,lastTouch=0,lastTouchId=owner.id,position=ball,previous=ball,controlOrigin=ball};
        }
        int Commitments(int trials){int n=0;for(int i=0;i<trials;i++)if((bool)Call("CommitsToChallenge",defender,owner))n++;return n;}

        [Test] public void DefenderInReachJockeysInsteadOfLungingEveryStep()
        {
            int n=Commitments(200);
            Assert.Greater(n,0,"A defender in reach must eventually challenge");
            Assert.Less(n,100,"He must not commit on most 0.1 s steps");
        }
        [Test] public void PressingInstructionRaisesCommitment()
        {
            sim.State.awayTactic.pressing=.1f;int passive=Commitments(1000);
            sim.State.awayTactic.pressing=.9f;int aggressive=Commitments(1000);
            Assert.Greater(aggressive,passive*1.4f);
        }
        [Test] public void ClearChanceInFrontOfGoalAlwaysTriggersTheChallenge()
        {
            int dir=sim.Direction(owner.side);
            Place(new Point(dir*46,0),new Point(dir*46+.8f,.43f));
            Assert.AreEqual(50,Commitments(50));
        }
        [Test] public void NothingRollsWhenTheBallIsOutOfReach()
        {
            Place(new Point(),new Point(4,0));uint seed=sim.State.seed;
            Assert.AreEqual(0,Commitments(20));Assert.AreEqual(seed,sim.State.seed);
        }
        [Test] public void LostPokeFromBehindTheRunnerCanBeAFoul()
        {
            int fouls=0;
            // Spread seeds: the first draw of consecutive small seeds is nearly identical.
            for(uint i=1;i<=64;i++){
                Setup();sim.State.seed=unchecked(i*2654435761u);int dir=sim.Direction(owner.side);
                Place(new Point(-dir*10,0),new Point(-dir*10.6f,.1f));owner.velocity=new Point(dir*4,0);
                if((bool)Call("MistimedChallenge",defender,owner)){fouls++;Assert.AreEqual("free-kick",sim.State.phase);Assert.AreEqual(owner.side,sim.State.restartSide);}
            }
            Assert.Greater(fouls,10);Assert.Less(fouls,60);
        }
        [Test] public void LostPokeInsideTheAreaIsNeverARandomPenalty()
        {
            for(uint seed=1;seed<=64;seed++){
                Setup();sim.State.seed=seed;int dir=sim.Direction(owner.side);
                Place(new Point(dir*42,0),new Point(dir*41.4f,.1f));owner.velocity=new Point(dir*4,0);
                Assert.IsFalse((bool)Call("MistimedChallenge",defender,owner),"Seed "+seed);
                Assert.IsFalse(sim.State.events.Any(e=>e.kind=="foul"||e.kind=="penalty"),"Seed "+seed);
            }
        }
        [Test] public void StationaryCarrierIsNotTrippedByAMissedPoke()
        {
            for(uint seed=1;seed<=32;seed++){Setup();sim.State.seed=seed;owner.velocity=new Point();Assert.IsFalse((bool)Call("MistimedChallenge",defender,owner));}
        }
    }
}

using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Touchline.Core;

public static class GroundedDecisionScenarios
{
    public static object Run(bool enforce=true)
    {
        var rows=new List<object>();int failures=0;
        foreach(int side in new[]{0,1})foreach(int period in new[]{1,2})foreach(bool patient in new[]{false,true})
        foreach(string action in new[]{"fall","slide"})foreach(float recovery in new[]{1.5f,.05f}){
            var db=new Database{players=Enumerable.Range(0,40).Select(i=>new PlayerData{id="ground"+i,name="G"+i,team=i<20?"a":"b",position=i%20==0?"GB":i%20<5?"DEF":i%20<9?"MIL":"ATT",rating=90}).ToArray()};
            var c=new Career{club="a"};c.lineup=Career.Select(db,"a",c.tactic);var sim=MatchSimulation.Create(db,c,"b",73,2700);var m=sim.State;
            m.period=period;m.restart=0;m.phase="play";m.clock=100;sim.Tactic(side).workIntoBox=patient;
            foreach(var a in m.actors){a.sentOff=true;a.position=a.previous=new Point(0,30);}
            int dir=sim.Direction(side);var striker=m.actors[side*11+9];var blocker=m.actors[(1-side)*11+2];var chaser=m.actors[(1-side)*11+4];
            striker.sentOff=blocker.sentOff=chaser.sentOff=false;
            m.actors[(1-side)*11].position=new Point(dir*51,0);
            striker.position=striker.previous=striker.carryTarget=new Point(dir*43,0);striker.angle=dir*(float)Math.PI*.5f;
            blocker.position=blocker.previous=new Point(dir*45,0);blocker.action=action;blocker.actionTime=recovery;
            chaser.position=chaser.previous=new Point(dir*41,0);
            m.possessionSide=side;m.ball=new BallState{owner=striker.id,side=side,lastTouch=side,lastTouchId=striker.id,position=striker.position+new Point(dir*.43f,0),previous=striker.position+new Point(dir*.43f,0),controlOrigin=striker.position,kind="none"};
            string chosen=sim.Decide(striker);bool expectedShot=recovery>1;bool onTarget=m.ball.shotOnTarget;
            bool pass=(chosen=="shot")==expectedShot;
            if(expectedShot&&chosen=="shot"){
                sim.Advance(.8);
                pass&=!m.events.Any(e=>e.kind=="block"&&e.player==blocker.id);
                if(onTarget)pass&=m.score[side]==1;
            }
            if(!pass)failures++;
            rows.Add(new{kind="finish",side,period,patient,action,recovery,chosen,expectedShot,onTarget,goal=m.score[side],passed=pass});

            // An incapacitated player cannot sprint across a pass lane before
            // recovering, but a player recovered before arrival still matters.
            striker.position=new Point(0,0);blocker.position=new Point(dir*5,1);chaser.sentOff=true;blocker.action=action;blocker.actionTime=recovery;
            float safety=(float)typeof(MatchSimulation).GetMethod("Safety",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(sim,new object[]{striker,new Point(dir*10,0),false});
            bool safe=recovery>1?safety>.95f:safety<.95f;if(!safe)failures++;
            rows.Add(new{kind="pass-lane",side,period,patient,action,recovery,safety,passed=safe});
        }
        if(enforce&&failures>0)throw new Exception(failures+" grounded decision regressions out of "+rows.Count);
        return new{passed=failures==0,failures,count=rows.Count,rows};
    }
}

namespace Touchline.Tests
{
    public class GroundedDecisionTests
    {
        [NUnit.Framework.Test] public void FallenAndRecoveringDefendersAreJudgedAtExpectedBallArrival()=>NUnit.Framework.Assert.DoesNotThrow(()=>GroundedDecisionScenarios.Run());
    }
}

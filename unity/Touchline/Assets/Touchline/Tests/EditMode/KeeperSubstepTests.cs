using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Touchline.Core;

public static class KeeperSubstepScenarios
{
    static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static MatchSimulation Create(int side,int period,out Actor keeper)
    {
        var db=new Database{players=Enumerable.Range(0,40).Select(i=>new PlayerData{id="keeper-clock"+i,name="K"+i,team=i<20?"a":"b",position=i%20==0?"GB":i%20<5?"DEF":i%20<9?"MIL":"ATT",rating=80}).ToArray()};
        var career=new Career{club="a"};career.lineup=Career.Select(db,"a",career.tactic);
        var sim=MatchSimulation.Create(db,career,"b",91,2700);sim.State.period=period;sim.State.restart=0;sim.State.phase="play";sim.State.clock=100;
        foreach(var p in sim.State.actors)p.sentOff=true;
        keeper=sim.State.actors[side*11];keeper.sentOff=false;return sim;
    }
    static float Skill(MatchSimulation sim,Actor keeper,string key)=>(float)typeof(MatchSimulation).GetMethod("Skill",Flags).Invoke(sim,new object[]{keeper,key});
    public static object Run(bool enforce=true)
    {
        int count=0,failures=0;var examples=new List<object>();
        foreach(int side in new[]{0,1})foreach(int period in new[]{1,2})foreach(float pose in new[]{-1f,.04f,.14f,.30f})
        foreach(float afterReaction in new[]{-.02f,.02f,.06f,.14f})foreach(float lateral in new[]{.2f,.6f,.85f,1.15f,1.6f})foreach(float startX in new[]{46f,47.5f,49f}){
            var sim=Create(side,period,out var keeper);var m=sim.State;int direction=-sim.Direction(side);
            keeper.previous=new Point(direction*49,0);keeper.position=new Point(direction*49,.08f);
            keeper.action=pose<0?"idle":"dive";keeper.actionTime=pose<0?0:1.2f-pose;
            float reaction=.14f+(100-Skill(sim,keeper,"gkReflexes"))*.002f,elapsed=reaction+afterReaction;
            float diving=Skill(sim,keeper,"gkDiving");
            var shot=new BallState{kind="shot",side=1-side,from="unrelated",previous=new Point(direction*startX,lateral),position=new Point(direction*(startX+3),lateral),height=.4f,previousHeight=.4f,elapsed=elapsed,velocity=new Point(direction*30,0)};
            m.ball=shot;
            // Independent dense-time oracle, including moving keeper, reaction
            // threshold and clamped dive extension. It does not call the solver.
            float expected=2;
            for(int i=0;i<=5000;i++){
                float f=i/5000f,time=elapsed-MatchSimulation.Step*(1-f);
                float reach=time<=reaction?.42f:pose<0?.55f+diving*.0035f:.55f+(diving*.010f+.40f)*Mathx.Clamp((pose-MatchSimulation.Step*(1-f))/.28f,0,1);
                var ball=Point.Lerp(shot.previous,shot.position,f);if(Math.Abs(ball.x)>52.61f)break;
                if(Point.Distance(ball,Point.Lerp(keeper.previous,keeper.position,f))<=reach){expected=f;break;}
            }
            var from=shot.previous;var end=shot.position;
            bool hit=(bool)typeof(MatchSimulation).GetMethod("ResolveShotContact",Flags).Invoke(sim,null);
            float actual=hit?(m.ball.position.x-from.x)/(end.x-from.x):2;
            bool passed=hit==(expected<=1)&&(!hit||Math.Abs(actual-expected)<.0008f);
            count++;if(!passed){failures++;if(examples.Count<20)examples.Add(new{side,period,pose,afterReaction,lateral,startX,expected,actual});}
        }
        // Exercise the normal tick ordering as well: the wide ball has already
        // passed before this keeper can react, even though he reacts by tick end.
        foreach(int side in new[]{0,1})foreach(int period in new[]{1,2}){
            var sim=Create(side,period,out var keeper);var m=sim.State;int direction=-sim.Direction(side);
            keeper.position=keeper.previous=new Point(direction*49,0);keeper.action="idle";
            float reaction=.14f+(100-Skill(sim,keeper,"gkReflexes"))*.002f,elapsed=reaction-.06f;
            var start=new Point(direction*(47.5f-40*elapsed),.7f);
            m.ball=new BallState{kind="shot",side=1-side,from="unrelated",start=start,end=start+new Point(direction*40,0),duration=1,elapsed=elapsed,position=new Point(direction*47.5f,.7f),previous=new Point(direction*47.5f,.7f),height=.4f,previousHeight=.4f,startHeight=.4f,endHeight=.4f};
            sim.Advance(.1);bool passed=m.metrics[side].saves==0;count++;
            if(!passed){failures++;examples.Add(new{kind="actual-tick",side,period,saves=m.metrics[side].saves});}
        }
        if(enforce&&failures>0)throw new Exception(failures+" keeper contact timeline failures / "+count);
        return new{passed=failures==0,count,failures,examples};
    }
}

namespace Touchline.Tests
{
    public class KeeperSubstepTests
    {
        [NUnit.Framework.Test] public void ReachIsEvaluatedAtImpactAcrossReactionAndDiveBoundaries()=>NUnit.Framework.Assert.DoesNotThrow(()=>KeeperSubstepScenarios.Run());
    }
}

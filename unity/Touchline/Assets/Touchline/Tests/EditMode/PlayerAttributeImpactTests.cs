using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Touchline.Core;

public static class PlayerAttributeImpactScenarios
{
    static readonly BindingFlags Flags=BindingFlags.NonPublic|BindingFlags.Instance;
    static MatchSimulation Create(int side,int period,uint seed,out Actor actor,out PlayerData data)
    {
        var db=new Database{players=Enumerable.Range(0,40).Select(i=>new PlayerData{id="ability"+i,name="A"+i,team=i<20?"a":"b",position=i%20==0?"GB":i%20<5?"DEF":i%20<9?"MIL":"ATT",rating=75}).ToArray()};
        var c=new Career{club="a"};c.lineup=Career.Select(db,c.club,c.tactic);var sim=MatchSimulation.Create(db,c,"b",seed,2700);
        var m=sim.State;m.restart=0;m.phase="play";m.period=period;m.clock=100;m.decision=10;
        foreach(var p in m.actors){p.sentOff=true;p.previous=p.position=new Point(0,30);}
        actor=m.actors[side*11+9];actor.sentOff=false;actor.angle=sim.Direction(side)*(float)Math.PI/2;data=db.Find(actor.id);
        var keeper=m.actors[(1-side)*11];keeper.sentOff=false;keeper.position=keeper.previous=new Point(sim.Direction(side)*50,0);
        return sim;
    }
    static void Attribute(PlayerData data,string key,float value)
    {
        var items=(data.attributes??Array.Empty<AttributeValue>()).Where(a=>a.key!=key).ToList();items.Add(new AttributeValue{key=key,value=value});data.attributes=items.ToArray();
    }
    static void Own(MatchSimulation sim,Actor actor)
    {
        sim.State.possessionSide=actor.side;sim.State.ball=new BallState{owner=actor.id,from=actor.id,side=actor.side,lastTouch=actor.side,position=actor.position,previous=actor.position,height=.11f,previousHeight=.11f,kind="none"};
    }
    public static object Run(bool enforce=true)
    {
        var rows=new List<object>();int failures=0,total=0;
        foreach(string attribute in new[]{"longShots","composure"})foreach(float distance in new[]{12f,29f})foreach(float pressure in new[]{1.5f,8f}){
            int[] target=new int[2];
            for(int level=0;level<2;level++)for(int seed=1;seed<=256;seed++){
                int side=seed%2,period=seed%4<2?1:2;var sim=Create(side,period,(uint)(seed*7919),out var shooter,out var data);int dir=sim.Direction(side);
                shooter.position=shooter.previous=new Point(dir*(52.5f-distance),0);shooter.angle=dir*(float)Math.PI*.5f;
                var marker=sim.State.actors[(1-side)*11+3];marker.sentOff=false;marker.position=marker.previous=shooter.position-new Point(dir*pressure,0);
                Attribute(data,attribute,level==0?20:95);Own(sim,shooter);
                typeof(MatchSimulation).GetMethod("Shoot",Flags).Invoke(sim,new object[]{shooter,false,false,false});
                if(sim.State.ball.shotOnTarget)target[level]++;
            }
            bool shouldMatter=attribute=="longShots"?distance>28:pressure<4;
            bool passed=shouldMatter?target[1]>target[0]:target[1]==target[0];total++;if(!passed)failures++;
            rows.Add(new{attribute,distance,pressure,low=target[0],high=target[1],attempts=256,shouldMatter,passed});
        }
        int[] passes=new int[2],carries=new int[2],shots=new int[2];int choices=0;
        foreach(int side in new[]{0,1})foreach(int period in new[]{1,2})foreach(float x in new[]{0f,12f,25f})foreach(float ahead in new[]{5f,10f,16f})foreach(float lateral in new[]{5f,10f,16f})foreach(float pressure in new[]{3f,7f}){
            choices++;
            for(int profile=0;profile<2;profile++){
                var sim=Create(side,period,701,out var player,out var data);int dir=sim.Direction(side);
                player.position=player.previous=new Point(dir*x,0);Own(sim,player);sim.State.carryTime=1;
                var mate=sim.State.actors[side*11+7];mate.sentOff=false;mate.position=mate.previous=new Point(dir*(x+ahead),lateral);
                var cover=sim.State.actors[(1-side)*11+2];cover.sentOff=false;cover.position=cover.previous=new Point(dir*46,28);
                var marker=sim.State.actors[(1-side)*11+3];marker.sentOff=false;marker.position=marker.previous=player.position-new Point(dir*pressure,0);
                // At an identical overall rating, compare a distributor and a
                // carrier. Neither gains finishing, morale, pace or fitness.
                foreach(string key in new[]{"shortPassing","longPassing","vision"})Attribute(data,key,profile==0?92:48);
                foreach(string key in new[]{"dribbling","agility"})Attribute(data,key,profile==0?48:92);
                string decision=sim.Decide(player);if(decision=="carry")carries[profile]++;else if(decision=="shot")shots[profile]++;else passes[profile]++;
            }
        }
        bool styles=passes[0]>passes[1]&&carries[1]>carries[0];total++;if(!styles)failures++;
        rows.Add(new{kind="same-overall-rating-styles",choices,distributorPasses=passes[0],carrierPasses=passes[1],distributorCarries=carries[0],carrierCarries=carries[1],shots,passed=styles});
        // Patience is still a tactical instruction. Both specialists take the
        // unmarked tap-in instead of using their personal passing preference.
        foreach(bool patient in new[]{false,true})foreach(int side in new[]{0,1})foreach(int period in new[]{1,2}){
            var sim=Create(side,period,79,out var player,out var data);player.position=player.previous=new Point(sim.Direction(side)*46,0);Own(sim,player);sim.Tactic(side).workIntoBox=patient;
            Attribute(data,"shortPassing",95);Attribute(data,"vision",95);Attribute(data,"dribbling",20);
            string choice=sim.Decide(player);bool passed=choice=="shot";total++;if(!passed)failures++;
            rows.Add(new{kind="clear-finish-priority",patient,side,period,choice,passed});
        }
        int[] attempts=new int[2];int patientShots=0;
        foreach(int side in new[]{0,1})foreach(int period in new[]{1,2})foreach(float distance in new[]{21f,22f,23f,24f,25f,26f})foreach(float mentality in new[]{.25f,.5f,.75f})foreach(bool patient in new[]{false,true})for(int level=0;level<2;level++){
            var sim=Create(side,period,71,out var player,out var data);player.position=player.previous=new Point(sim.Direction(side)*(52.5f-distance),0);Own(sim,player);sim.State.carryTime=1;
            sim.Tactic(side).workIntoBox=patient;sim.Tactic(side).mentality=mentality;Attribute(data,"longShots",level==0?20:95);
            if(sim.Decide(player)=="shot"){if(patient)patientShots++;else attempts[level]++;}
        }
        bool distanceChoices=attempts[1]>attempts[0]&&patientShots==0;total++;if(!distanceChoices)failures++;
        rows.Add(new{kind="long-shot-choice-and-patience",poorLongShots=attempts[0],strongLongShots=attempts[1],patientShots,passed=distanceChoices});
        float[] run=new float[2];
        for(int level=0;level<2;level++){
            var sim=Create(0,1,61,out var player,out var data);player.position=player.previous=new Point(0,0);player.carryTarget=new Point(40,0);Own(sim,player);sim.State.decision=100;
            Attribute(data,"sprintSpeed",level==0?20:95);sim.Advance(2);run[level]=player.position.x;
        }
        bool running=run[1]>run[0]+1;total++;if(!running)failures++;
        rows.Add(new{kind="same-acceleration-different-sprint",seconds=2,slowMetres=run[0],fastMetres=run[1],passed=running});
        if(enforce&&failures>0)throw new Exception(failures+" attribute impact checks failed");
        return new{passed=failures==0,failures,total,shotTrials=4096,choiceTrials=choices*2+8+288,rows};
    }
}

namespace Touchline.Tests
{
    public class PlayerAttributeImpactTests
    {
        [NUnit.Framework.Test] public void AttributesAffectRelevantExecutionAndPlayingChoices()=>NUnit.Framework.Assert.DoesNotThrow(()=>PlayerAttributeImpactScenarios.Run());
    }
}

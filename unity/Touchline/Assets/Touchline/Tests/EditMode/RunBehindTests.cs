using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Touchline.Core;

namespace Touchline.Tests
{
    // Appels en profondeur (MatchRunsBehind) : départ quand le porteur est libre,
    // jamais pour un défenseur ni quand le porteur est pressé.
    public sealed class RunBehindTests
    {
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;

        static MatchSimulation Setup(int side,float ownerPressure,out Actor owner,out Actor runner,out Actor defender)
        {
            var db=new Database{players=Enumerable.Range(0,40).Select(i=>new PlayerData{id="rb"+i,name="R"+i,team=i<20?"a":"b",position=i%20==0?"GB":i%20<5?"DEF":i%20<9?"MIL":"ATT",rating=80}).ToArray()};
            var c=new Career{club="a"};c.lineup=Career.Select(db,"a",c.tactic);var sim=MatchSimulation.Create(db,c,"b",41,2700);int dir=sim.Direction(side);
            owner=sim.State.actors[side*11+6];runner=sim.State.actors[side*11+9];defender=sim.State.actors[(1-side)*11+2];var second=sim.State.actors[(1-side)*11+3];var keeper=sim.State.actors[(1-side)*11];
            foreach(var p in sim.State.actors){p.sentOff=true;p.velocity=new Point();}
            foreach(var p in new[]{owner,runner,defender,second,keeper})p.sentOff=false;
            owner.position=new Point(dir*10,0);runner.position=new Point(dir*28,4);defender.position=new Point(dir*30,6);second.position=new Point(dir*30,-6);keeper.position=new Point(dir*50,0);
            if(ownerPressure>0){var presser=sim.State.actors[(1-side)*11+5];presser.sentOff=false;presser.position=owner.position+new Point(dir*ownerPressure,0);}
            sim.State.phase="play";sim.State.restart=0;sim.State.ball=new BallState{owner=owner.id,side=side,position=owner.position,height=.11f};sim.State.possessionSide=side;
            return sim;
        }

        static bool Call(MatchSimulation sim,Actor runner,Actor owner,string duty,ref Point q)
        {
            var args=new object[]{runner,owner,duty,q};bool r=(bool)typeof(MatchSimulation).GetMethod("RunBehind",Flags).Invoke(sim,args);q=(Point)args[3];return r;
        }

        [TestCase(0)][TestCase(1)]
        public void FreeOwnerTriggersRunBeyondTheLine(int side)
        {
            var sim=Setup(side,0,out var owner,out var runner,out _);int dir=sim.Direction(side);bool started=false;var q=new Point(28,4);
            for(int i=0;i<600&&!started;i++){q=new Point(28,4);started=Call(sim,runner,owner,"attack",ref q);}
            Assert.IsTrue(started,"Un attaquant sur la ligne, porteur libre, doit finir par lancer un appel.");
            Assert.Greater(runner.runBehind,0f);
            Assert.Greater(q.x,30f+5f,"La cible de l'appel doit être nettement derrière la ligne (repère attaque).");
            Assert.Less(sim.State.decision,.6f,"Le porteur doit relever la tête rapidement après l'appel.");
        }

        [Test]
        public void DefendersNeverRunAndPressuredOwnerIsNotServed()
        {
            var sim=Setup(0,0,out var owner,out var runner,out _);var q=new Point(28,4);
            for(int i=0;i<600;i++){q=new Point(28,4);Assert.IsFalse(Call(sim,runner,owner,"defend",ref q));}
            sim=Setup(0,1.5f,out owner,out runner,out _);
            for(int i=0;i<600;i++){q=new Point(28,4);Assert.IsFalse(Call(sim,runner,owner,"attack",ref q),"Porteur pressé : pas d'appel.");}
        }

        [Test]
        public void RunningPlayerKeepsTargetBehindLineUntilRunEnds()
        {
            var sim=Setup(0,0,out var owner,out var runner,out _);runner.runBehind=.25f;var q=new Point(20,4);
            Assert.IsTrue(Call(sim,runner,owner,"attack",ref q));Assert.GreaterOrEqual(q.x,30f);
            q=new Point(20,4);Assert.IsTrue(Call(sim,runner,owner,"attack",ref q));
            q=new Point(20,4);Assert.IsTrue(Call(sim,runner,owner,"attack",ref q));
            Assert.LessOrEqual(runner.runBehind,0f,"L'appel doit s'arrêter après sa durée.");
        }
    }
}

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

        [TestCase(0,1)][TestCase(1,1)][TestCase(0,2)][TestCase(1,2)]
        public void AlignedGroundReceptionKeepsMovingButTurningAndBodyControlsBrake(int side,int period)
        {
            foreach(string kind in new[]{"control-foot",MatchSimulation.ChestControl,MatchSimulation.ThighControl,"turn"}){
                var sim=Setup(side,0,out var owner,out _,out _);sim.State.period=period;int dir=sim.Direction(side);
                foreach(var actor in sim.State.actors)actor.sentOff=actor!=owner;
                owner.position=new Point();owner.velocity=new Point(dir*4,0);owner.angle=dir*(float)Math.PI/2;
                owner.action="control";owner.actionKind=kind=="turn"?"control-foot":kind;owner.actionTime=.3f;owner.controlTime=.2f;
                owner.carryTarget=new Point((kind=="turn"?-dir:dir)*8,0);sim.State.ball.position=owner.position;
                typeof(MatchSimulation).GetMethod("Move",Flags).Invoke(sim,null);
                if(kind=="control-foot"){
                    Assert.Greater(sim.MovementTarget(side*11+6).x*dir,4,"An aligned ground touch must not impose a stationary waypoint.");
                    Assert.GreaterOrEqual(owner.velocity.x*dir,4,"The receiver keeps the running first touch.");
                }else{
                    Assert.Less(sim.MovementTarget(side*11+6).Length,.001f,"Turning and body cushioning still require braking.");
                    Assert.Less(owner.velocity.x*dir,4,"Braking remains progressive rather than snapping velocity to zero.");
                    Assert.Greater(owner.velocity.x*dir,0);
                }
            }
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
        public void RunningPlayerKeepsTargetBehindLineAndStoppageEndsRun()
        {
            var sim=Setup(0,0,out var owner,out var runner,out _);runner.runBehind=.25f;var q=new Point(20,4);
            Assert.IsTrue(Call(sim,runner,owner,"attack",ref q));Assert.GreaterOrEqual(q.x,30f);
            // The countdown runs in Move() for every player, whoever has the ball.
            sim.Advance(.3);Assert.LessOrEqual(runner.runBehind,0f,"L'appel doit s'arrêter après sa durée, même sans porteur.");
            runner.runBehind=1f;typeof(MatchSimulation).GetMethod("Restart",Flags).Invoke(sim,new object[]{"throw-in",0,new Point(0,34),2f});
            Assert.AreEqual(0f,runner.runBehind,"Un arrêt de jeu met fin à l'appel.");
        }
    }
}

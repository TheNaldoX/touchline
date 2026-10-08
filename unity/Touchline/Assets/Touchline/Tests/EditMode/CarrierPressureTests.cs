using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Touchline.Core;

namespace Touchline.Tests
{
    // Pression rapprochée : un porteur serré finit par perdre le ballon, d'autant
    // plus vite que les presseurs sont nombreux et bons tacleurs, qu'il est peu
    // technique et qu'il court vite. L'IA en tient compte : elle évite de
    // conduire dans plusieurs adversaires et de servir un partenaire marqué de près,
    // et le joueur le plus proche va presser un porteur dans son propre tiers.
    public sealed class CarrierPressureTests
    {
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static AttributeValue[] Skills(float tackle,float dribble)=>new[]{
            new AttributeValue{key="standingTackle",value=tackle},new AttributeValue{key="defensiveAwareness",value=tackle},
            new AttributeValue{key="dribbling",value=dribble},new AttributeValue{key="ballControl",value=dribble},new AttributeValue{key="strength",value=dribble}};
        static MatchSimulation Setup(int side,float tackle,float dribble,out Actor carrier,out Actor first,out Actor second)
        {
            var db=new Database{players=Enumerable.Range(0,40).Select(i=>new PlayerData{id="cp"+i,name="C"+i,team=i<20?"a":"b",position=i%20==0?"GB":i%20<5?"DEF":i%20<9?"MIL":"ATT",rating=70,
                attributes=Skills(tackle,dribble)}).ToArray()};
            var c=new Career{club="a"};c.lineup=Career.Select(db,"a",c.tactic);var sim=MatchSimulation.Create(db,c,"b",17,2700);int dir=sim.Direction(side);
            sim.State.professionalRules=true;carrier=sim.State.actors[side*11+7];first=sim.State.actors[(1-side)*11+6];second=sim.State.actors[(1-side)*11+5];
            foreach(var p in sim.State.actors){p.sentOff=true;p.velocity=new Point();}
            foreach(var p in new[]{carrier,first,second,sim.State.actors[side*11],sim.State.actors[(1-side)*11]})p.sentOff=false;
            carrier.position=carrier.previous=new Point(0,0);carrier.angle=dir>0?1.5708f:-1.5708f;
            // Le ballon devant le pied du porteur, le premier presseur face à lui.
            first.position=first.previous=new Point(dir*1.1f,.3f);second.position=second.previous=new Point(30,30);
            sim.State.phase="play";sim.State.restart=0;sim.State.clock=600;
            sim.State.ball=new BallState{owner=carrier.id,side=side,position=new Point(dir*.4f,0),height=.11f};sim.State.possessionSide=side;
            return sim;
        }

        [Test]
        public void NoHazardWithoutPresserInReach()
        {
            var sim=Setup(0,70,70,out var carrier,out var first,out _);
            first.position=new Point(5,5);
            Assert.AreEqual(0,sim.CarrierPressureHazard(carrier,out var presser));Assert.IsNull(presser);
        }

        [Test]
        public void HazardGrowsWithPressersSpeedAndDefensiveSkill()
        {
            var sim=Setup(0,70,70,out var carrier,out var first,out var second);int dir=sim.Direction(0);
            float one=sim.CarrierPressureHazard(carrier,out var presser);
            Assert.Greater(one,0);Assert.AreSame(first,presser);
            second.position=new Point(dir*.6f,-.9f);
            Assert.Greater(sim.CarrierPressureHazard(carrier,out _),one*1.5f,"Deux presseurs");
            second.position=new Point(30,30);carrier.velocity=new Point(dir*6,0);
            Assert.Greater(sim.CarrierPressureHazard(carrier,out _),one*1.5f,"Porteur lancé");
            var strong=Setup(0,85,60,out var c2,out _,out _);var weak=Setup(0,60,85,out var c3,out _,out _);
            Assert.Greater(strong.CarrierPressureHazard(c2,out _),2*weak.CarrierPressureHazard(c3,out _),"Tacleur contre dribbleur");
        }

        [TestCase(0)][TestCase(1)]
        public void PressedCarrierEventuallyLosesTheBall(int side)
        {
            var sim=Setup(side,75,65,out var carrier,out var first,out _);
            var resolve=typeof(MatchSimulation).GetMethod("ResolveCarrierPressure",Private);bool lost=false;
            for(int i=0;i<600&&!lost;i++){first.duelCooldown=0;first.action="idle";first.actionTime=0;lost=(bool)resolve.Invoke(sim,new object[]{carrier});}
            Assert.IsTrue(lost);
            if(sim.State.phase=="free-kick"){Assert.AreEqual(1,sim.State.metrics[first.side].fouls);return;}
            Assert.IsNull(sim.State.ball.owner);Assert.AreEqual("loose",sim.State.ball.kind);Assert.AreEqual(first.side,sim.State.ball.lastTouch);
            Assert.AreEqual(1,sim.State.metrics[side].pressuredLosses);Assert.AreEqual("tackle",first.action);
            Assert.AreEqual("tackle",sim.State.events.Last().kind);
        }

        [Test]
        public void PressedCarrierDecidesSooner()
        {
            var sim=Setup(0,70,70,out var carrier,out var first,out _);sim.State.decision=1.5f;
            var resolve=typeof(MatchSimulation).GetMethod("ResolveCarrierPressure",Private);resolve.Invoke(sim,new object[]{carrier});
            Assert.Less(sim.State.decision,.7f);
        }

        [Test]
        public void CarryingIntoTwoDefendersAndPassingToMarkedReceiverCost()
        {
            var sim=Setup(0,70,70,out var carrier,out var first,out var second);int dir=sim.Direction(0);
            var crowd=typeof(MatchSimulation).GetMethod("CarryCrowdCost",Private);var marked=typeof(MatchSimulation).GetMethod("MarkedReceiverCost",Private);
            var end=new Point(dir*6,0);first.position=new Point(dir*3,.5f);second.position=new Point(30,30);
            Assert.AreEqual(0f,(float)crowd.Invoke(sim,new object[]{carrier,end}),"Un seul défenseur : duel normal");
            second.position=new Point(dir*4.5f,-.8f);
            Assert.Greater((float)crowd.Invoke(sim,new object[]{carrier,end}),0f,"Deux défenseurs dans le couloir");
            Assert.Greater((float)marked.Invoke(sim,new object[]{.8f}),(float)marked.Invoke(sim,new object[]{1.6f}));
            Assert.AreEqual(0f,(float)marked.Invoke(sim,new object[]{5f}));
        }

        [TestCase(0)][TestCase(1)]
        public void NearestDefenderPressesCarrierInHisOwnThird(int side)
        {
            var sim=Setup(side,70,70,out var carrier,out var first,out var second);int dir=sim.Direction(side);
            sim.Tactic(1-side).pressing=.5f;sim.Tactic(1-side).line=.4f;sim.State.turnoverAt=-100;
            carrier.position=carrier.previous=new Point(-dir*30,10);sim.State.ball.position=carrier.position+new Point(dir*.4f,0);
            first.position=first.previous=new Point(-dir*22,8);second.position=second.previous=new Point(-dir*15,-5);
            typeof(MatchSimulation).GetMethod("Move",Private).Invoke(sim,null);
            Assert.AreEqual("press",first.intent);
        }
    }
}

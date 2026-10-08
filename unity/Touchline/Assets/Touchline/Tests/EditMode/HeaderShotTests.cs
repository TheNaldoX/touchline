using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Touchline.Core;

namespace Touchline.Tests
{
    // Tête sur centre : un attaquant marqué de près à 9 m du but cherche le cadre ;
    // à 22 m il remet plutôt que de tenter une tête sans espoir.
    public sealed class HeaderShotTests
    {
        [TestCase(0)][TestCase(1)]
        public void MarkedAttackerHeadsAtGoalInsideBoxOnly(int side)
        {
            var db=new Database{players=Enumerable.Range(0,40).Select(i=>new PlayerData{id="hd"+i,name="H"+i,team=i<20?"a":"b",position=i%20==0?"GB":i%20<5?"DEF":i%20<9?"MIL":"ATT",rating=80}).ToArray()};
            var c=new Career{club="a"};c.lineup=Career.Select(db,"a",c.tactic);var sim=MatchSimulation.Create(db,c,"b",23,2700);int dir=sim.Direction(side);
            var striker=sim.State.actors[side*11+10];var marker=sim.State.actors[(1-side)*11+2];
            foreach(var p in sim.State.actors)p.sentOff=p!=striker&&p!=marker;
            var heads=typeof(MatchSimulation).GetMethod("HeadsForGoal",BindingFlags.Instance|BindingFlags.NonPublic);
            striker.position=new Point(dir*43.5f,1);marker.position=new Point(dir*44.3f,1.6f);
            Assert.IsTrue((bool)heads.Invoke(sim,new object[]{striker}),"9 m, marqué : tête au but");
            striker.position=new Point(dir*30.5f,1);marker.position=new Point(dir*31.3f,1.6f);
            Assert.IsFalse((bool)heads.Invoke(sim,new object[]{striker}),"22 m, marqué : remise");
        }
    }
}

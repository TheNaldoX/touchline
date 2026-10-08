using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Touchline.Core;

namespace Touchline.Tests
{
    // Forme du jour et avantage du terrain : un match n'est pas joué au même
    // niveau chaque semaine, et l'équipe qui reçoit a un léger avantage.
    public sealed class MatchFormAndVenueTests
    {
        [Test]
        public void MatchDayFormIsStableCenteredAndSpread()
        {
            Assert.AreEqual(MatchSimulation.MatchDayForm(42,0),MatchSimulation.MatchDayForm(42,0));
            var forms=Enumerable.Range(1,4000).Select(i=>MatchSimulation.MatchDayForm((uint)i*7919u,i%2)).ToArray();
            double mean=forms.Average(),sd=Math.Sqrt(forms.Average(f=>(f-mean)*(f-mean)));
            Assert.AreEqual(0,mean,.005);
            Assert.AreEqual(MatchSimulation.MatchFormSpread,sd,.01,"Écart-type ≈ MatchFormSpread");
            Assert.That(forms.Max(),Is.LessThanOrEqualTo(3*MatchSimulation.MatchFormSpread+1e-4));
        }

        [Test]
        public void VenueSideGetsHomeAdvantage()
        {
            var db=new Database{players=Enumerable.Range(0,40).Select(i=>new PlayerData{id="vn"+i,name="V"+i,team=i<20?"a":"b",position=i%20==0?"GB":i%20<5?"DEF":i%20<9?"MIL":"ATT",rating=70,attributes=new[]{new AttributeValue{key="vision",value=60}}}).ToArray()};
            var c=new Career{club="a"};c.lineup=Career.Select(db,"a",c.tactic);var sim=MatchSimulation.Create(db,c,"b",5,2700);
            var skill=typeof(MatchSimulation).GetMethod("Skill",BindingFlags.Instance|BindingFlags.NonPublic);var p=sim.State.actors[6];
            sim.State.venueSide=0;float home=(float)skill.Invoke(sim,new object[]{p,"vision"});
            sim.State.venueSide=1;float away=(float)skill.Invoke(sim,new object[]{p,"vision"});
            sim.State.venueSide=-1;float neutral=(float)skill.Invoke(sim,new object[]{p,"vision"});
            Assert.Greater(home,away);Assert.AreEqual(away,neutral,1e-4f);
            Assert.AreEqual(1+MatchSimulation.HomeAdvantage,home/away,.01f);
        }
    }
}

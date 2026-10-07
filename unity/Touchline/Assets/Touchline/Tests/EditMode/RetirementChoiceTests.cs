using System.Linq;
using NUnit.Framework;
using Touchline.Core;

namespace Touchline.Tests
{
    // Retraite : à la fin d'un contrat, un joueur de 35 ans finit souvent par
    // arrêter ; jamais en cours de contrat, jamais à 25 ans.
    public sealed class RetirementChoiceTests
    {
        static Career At(int year)=>new Career{club="x",life=new ClubLife{day=300},world=new CareerWorld{year=year}};

        [Test]
        public void VeteranRetiresSometimesAtContractEndOnly()
        {
            var players=Enumerable.Range(0,200).Select(i=>new PlayerData{id="rt"+i,team="c",age=35,rating=65,position="MIL"}).ToArray();
            var ending=new Employment{club="c",until=310};var running=new Employment{club="c",until=300+365};
            var c=At(2027);
            int retiring=players.Count(p=>c.ChoosesToRetire(p,ending));
            Assert.That(retiring,Is.InRange(50,110),"~40 % à 35 ans en fin de contrat");
            Assert.That(players.Count(p=>c.ChoosesToRetire(p,running)),Is.EqualTo(0),"Pas en cours de contrat");
            foreach(var p in players)p.age=25;
            Assert.That(players.Count(p=>c.ChoosesToRetire(p,ending)),Is.EqualTo(0),"Pas à 25 ans");
        }

        [Test]
        public void GoalkeepersAndStarsPlayLonger()
        {
            var ending=new Employment{club="c",until=310};var c=At(2027);
            var outfield=Enumerable.Range(0,300).Select(i=>new PlayerData{id="ro"+i,team="c",age=34,rating=65,position="MIL"}).ToArray();
            var keepers=Enumerable.Range(0,300).Select(i=>new PlayerData{id="rk"+i,team="c",age=34,rating=65,position="GB"}).ToArray();
            var stars=Enumerable.Range(0,300).Select(i=>new PlayerData{id="rs"+i,team="c",age=34,rating=84,position="MIL"}).ToArray();
            int o=outfield.Count(p=>c.ChoosesToRetire(p,ending)),k=keepers.Count(p=>c.ChoosesToRetire(p,ending)),s=stars.Count(p=>c.ChoosesToRetire(p,ending));
            Assert.Less(k,o);Assert.Less(s,o);
        }
    }
}

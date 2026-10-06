using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Touchline.Core;

namespace Touchline.Tests
{
    // Économie des clubs IA : ambition stable par club et recettes liées au classement.
    public sealed class AiEconomyStanceTests
    {
        [Test]
        public void AmbitionIsStableAndSpread()
        {
            var values=Enumerable.Range(0,200).Select(i=>Career.AiAmbition("club"+i)).ToArray();
            Assert.That(values.All(v=>v>=0&&v<=1));
            Assert.AreEqual(Career.AiAmbition("club7"),Career.AiAmbition("club7"));
            Assert.That(values.Count(v=>v<.2f),Is.GreaterThan(20),"Des clubs prudents");
            Assert.That(values.Count(v=>v>.8f),Is.GreaterThan(20),"Des clubs dépensiers");
        }

        [Test]
        public void FinalTableSetsNextSeasonRevenueButNotManagedClub()
        {
            var ids=new[]{"m1","m2","m3","m4","m5"};
            var db=new Database{clubs=ids.Select(id=>new ClubData{id=id,league="t.1",annualRevenue=100_000_000}).ToArray(),players=new PlayerData[0]};
            var c=new Career{club="m3",world=new CareerWorld{divisions=new List<Division>{new Division{id="t.1",clubs=ids.ToList()}}}};
            typeof(Career).GetMethod("ApplyMeritRevenue",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(c,new object[]{db});
            Assert.AreEqual(115_000_000,db.clubs[0].annualRevenue,1000,"Champion : +15 %");
            Assert.AreEqual(100_000_000,db.clubs[2].annualRevenue,"Club dirigé inchangé");
            Assert.AreEqual(85_000_000,db.clubs[4].annualRevenue,1000,"Dernier : -15 %");
            // Le bonus ne se cumule pas d'une saison à l'autre.
            typeof(Career).GetMethod("ApplyMeritRevenue",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(c,new object[]{db});
            Assert.AreEqual(115_000_000,db.clubs[0].annualRevenue,1000);
        }
    }
}

using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Touchline.Core;

namespace Touchline.Tests
{
    public class CatalogueMigrationTests
    {
        [TestCase(false)] [TestCase(true)]
        public void SaveWithoutFictionalCatalogueRestoresAfterCatalogueExpansion(bool compact)
        {
            var original=new Database{
                leagues=new[]{new LeagueData{id="test",name="Test",country="France"}},
                clubs=Enumerable.Range(0,4).Select(i=>new ClubData{id="c"+i,name="Club "+i,league="test",playable=true,annualRevenue=20000000}).ToArray(),
                players=Enumerable.Range(0,96).Select(i=>new PlayerData{id="p"+i,name="Joueur "+i,team="c"+i/24,age=25,position=i%24<2?"GB":"MIL",positions=new[]{i%24<2?"GK":"CM"},rating=65,potential=75,wage=500,value=100000,fitness=100,morale=75}).ToArray()};
            string pristine=JsonUtility.ToJson(original);
            var c=new Career{club="c0",saveBaseline=SaveBaseline.From(original)};c.lineup=Career.Select(original,c.club,c.tactic);c.EnsureWorld(original);
            var changed=original.Find("p4").Copy();changed.wage=789;changed.morale=44;c.world.rosterChanges.RemoveAll(p=>p.id==changed.id);c.world.rosterChanges.Add(changed);
            var expectedLineup=c.lineup.ToArray();var expectedFixtures=c.world.fixtures.Select(f=>f.id).ToArray();
            if(compact)Assert.IsTrue(c.PrepareCompactSave());
            string saved=JsonUtility.ToJson(c);c.RestoreAfterSave();
            var expanded=JsonUtility.FromJson<Database>(pristine);
            GeneratedWorld.AppendFrozen(expanded,JsonUtility.FromJson<Database>(Resources.Load<TextAsset>("Data/generated-world-v1").text));
            var restored=JsonUtility.FromJson<Career>(saved);
            Assert.IsTrue(CareerSaveRestore.TryRestore(expanded,restored,out var db));
            CollectionAssert.AreEqual(expectedLineup,restored.lineup);
            CollectionAssert.AreEqual(expectedFixtures,restored.world.fixtures.Select(f=>f.id).ToArray());
            Assert.AreEqual(789,db.Find("p4").wage);Assert.AreEqual(44,db.Find("p4").morale);
            Assert.AreEqual(2784,db.players.Count(p=>GeneratedWorld.IsGenerated(p.id)));
            Assert.IsFalse(restored.world.fixtures.Any(f=>GeneratedWorld.IsGenerated(f.home)||GeneratedWorld.IsGenerated(f.away)));
        }
    }
}

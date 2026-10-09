using System.Linq;
using NUnit.Framework;
using Touchline.Core;
using UnityEngine;

namespace Touchline.Tests
{
    public class CareerSaveRestoreTests
    {
        Database Original()=>new Database{
            schema=1,importedAt="2026-10-05",provenance="Test source",
            clubs=new[]{new ClubData{id="a",name="Club A",league="fra.1",annualRevenue=10000000,financeSource="Original source"}},
            players=Enumerable.Range(0,11).Select(i=>new PlayerData{id="p"+i,name="P"+i,team="a",age=25,source="Original player source"}).ToArray(),
            leagues=new[]{new LeagueData{id="fra.1"}},fixtures=new Fixture[0]
        };
        Career Initial()=>new Career{club="a",lineup=Enumerable.Range(0,11).Select(i=>"p"+i).ToArray()};
        Career BadPrimary(){var c=Initial();c.world=new CareerWorld();c.world.rosterChanges.Add(new PlayerData{id="p0",team="free",age=42,source="Rejected source"});c.world.divisions.Add(new Division{id="fra.2",clubs={"a"}});c.changedRevenues.Add(new RevenueChange{club="a",revenue=1});return c;}

        [TestCase("retired",false)][TestCase("retired",true)]
        [TestCase("free",false)][TestCase("free",true)]
        [TestCase("b",false)][TestCase("b",true)]
        public void UnemployedHistoricalLineupSurvivesDeparture(string destination,bool compact)
        {
            var original=Original();var c=Initial();c.world=new CareerWorld{managerStatus="unemployed"};
            var departed=original.Find("p0").Copy();departed.team=destination;c.world.rosterChanges.Add(departed);
            c.saveBaseline=SaveBaseline.From(original);if(compact)Assert.IsTrue(c.PrepareCompactSave());
            string json=JsonUtility.ToJson(c);c.RestoreAfterSave();var saved=JsonUtility.FromJson<Career>(json);
            Assert.IsTrue(CareerSaveRestore.TryRestore(original,saved,out var restored));
            Assert.AreEqual(destination,restored.Find("p0").team);Assert.AreEqual("a",original.Find("p0").team);
        }
        [Test] public void UnemployedExceptionDoesNotAcceptMissingPlayersOrAnActiveMatch()
        {
            var original=Original();var c=BadPrimary();c.world.managerStatus="unemployed";
            c.lineup[0]="unknown";Assert.IsFalse(CareerSaveRestore.TryRestore(original,c,out _));
            c.lineup[0]="p0";c.match=new MatchState{home="a",away="b"};
            Assert.IsFalse(CareerSaveRestore.TryRestore(original,c,out _));
            c.match=null;c.world.activeFixture="active";
            Assert.IsFalse(CareerSaveRestore.TryRestore(original,c,out _));
        }

        [Test] public void RejectedPrimaryCannotContaminateBackupOrImportedCatalogue()
        {
            var original=Original();string before=JsonUtility.ToJson(original);
            Assert.IsFalse(CareerSaveRestore.TryRestore(original,BadPrimary(),out var rejected));Assert.IsNull(rejected);
            Assert.AreEqual(before,JsonUtility.ToJson(original));
            Assert.IsTrue(CareerSaveRestore.TryRestore(original,Initial(),out var backup));
            Assert.AreEqual("a",backup.Find("p0").team);Assert.AreEqual(25,backup.Find("p0").age);Assert.AreEqual("Original player source",backup.Find("p0").source);
            Assert.AreEqual("fra.1",backup.clubs[0].league);Assert.AreEqual(10000000,backup.clubs[0].annualRevenue);
            Assert.AreEqual(before,JsonUtility.ToJson(original));
        }
        [Test] public void ValidCareerRestoresItsOwnClubRevenueAndRosterWithoutMutatingOriginal()
        {
            var original=Original();var c=Initial();c.world=new CareerWorld();
            c.world.rosterChanges.Add(new PlayerData{id="p0",team="a",age=26,source="Career source"});
            c.world.divisions.Add(new Division{id="fra.2",clubs={"a"}});c.changedRevenues.Add(new RevenueChange{club="a",revenue=7000000});
            Assert.IsTrue(CareerSaveRestore.TryRestore(original,c,out var restored));
            Assert.AreEqual(26,restored.Find("p0").age);Assert.AreEqual("fra.2",restored.clubs[0].league);Assert.AreEqual(7000000,restored.clubs[0].annualRevenue);
            Assert.AreEqual(25,original.Find("p0").age);Assert.AreEqual("fra.1",original.clubs[0].league);Assert.AreEqual(10000000,original.clubs[0].annualRevenue);
            Assert.AreNotSame(original.clubs[0],restored.clubs[0]);Assert.AreSame(original.leagues,restored.leagues);
        }
        [Test] public void InvalidLineupAndDuplicateRosterIdsNeverPublishPartialWorld()
        {
            var original=Original();var before=JsonUtility.ToJson(original);var duplicate=BadPrimary();duplicate.lineup[10]="p0";
            Assert.IsFalse(CareerSaveRestore.TryRestore(original,duplicate,out _));
            var c=BadPrimary();c.world.rosterChanges.Add(new PlayerData{id="p0",team="a"});
            Assert.IsFalse(CareerSaveRestore.TryRestore(original,c,out _));Assert.AreEqual(before,JsonUtility.ToJson(original));
            Assert.IsFalse(CareerSaveRestore.TryRestore(original,null,out _));
        }
        [Test] public void ClubCopiesRetainEverySerializedFieldWithoutSharingMutableClubRecords()
        {
            var club=new ClubData{id="a",name="Name",league="fra.1",color="blue",logo="club-a",financeSource="Finance",stadium="Stadium",stadiumSource="Ground source",capacitySource="Capacity",sourceSeason="2026",rosterSource="Roster",rosterAsOf="2026-10-05",referenceSeason="2025",referenceFinanceSource="Ref",annualRevenue=123,referenceRevenue=100,stadiumCapacity=50000,playable=true,reserve=true};
            Assert.AreEqual(JsonUtility.ToJson(club),JsonUtility.ToJson(club.Copy()));Assert.AreNotSame(club,club.Copy());
        }
    }
}

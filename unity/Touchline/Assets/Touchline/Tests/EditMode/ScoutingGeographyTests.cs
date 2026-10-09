using System;
using System.Linq;
using NUnit.Framework;
using Touchline.Core;

namespace Touchline.Tests
{
    public class ScoutingGeographyTests
    {
        Database db;
        [SetUp] public void Setup()
        {
            db=new Database {leagues=new[]{new LeagueData{id="fra.1",name="Ligue 1",country="France"}},clubs=new[]{
                new ClubData{id="fr",name="French club",league="fra.1",country="Wrong legacy value"},
                new ClubData{id="bodo",name="Bodo/Glimt",league="europe",country="Norvège",countrySource="https://www.uefa.com/uefachampionsleague/clubs/",countryAsOf="2026-10-09"},
                new ClubData{id="unknown",name="Unknown club",league="europe",country="Unknown club"}}};
        }
        [Test] public void LeagueCountryTakesPrecedenceOverOldClubSnapshot(){Assert.AreEqual("France",ScoutingGeography.Country(db,db.clubs[0]));}
        [Test] public void VerifiedClubIsReachableWithoutPlayableLeague(){CollectionAssert.AreEqual(new[]{"bodo"},ScoutingGeography.ClubIds(db,"Norvège"));Assert.IsTrue(ScoutingGeography.Countries(db).Contains("Norvège"));Assert.AreEqual(1,db.leagues.Length);}
        [Test] public void UnsourcedCountryAndInvalidDateAreNotTerritories(){db.clubs[2].country="Atlantis";Assert.IsNull(ScoutingGeography.Country(db,db.clubs[2]));db.clubs[1].countryAsOf="not-a-date";Assert.IsNull(ScoutingGeography.Country(db,db.clubs[1]));CollectionAssert.AreEqual(new[]{"France"},ScoutingGeography.Countries(db));}
        [Test] public void ClubNameIsNotAcceptedAsACountryEvenWithSource(){db.clubs[1].country=db.clubs[1].name;Assert.IsNull(ScoutingGeography.Country(db,db.clubs[1]));}
        [Test] public void AllKeepsUnknownClubsButLeagueFilterRemainsStrict(){Assert.AreEqual(3,ScoutingGeography.ClubIds(db).Count());CollectionAssert.AreEqual(new[]{"fr"},ScoutingGeography.ClubIds(db,"Tous","Ligue 1"));Assert.AreEqual(0,ScoutingGeography.ClubIds(db,"Norvège","Ligue 1").Count());}
    }
    public class ScoutingGeographyMissionTests
    {
        Database db;Career c;
        [SetUp] public void Setup()
        {
            db=new Database{leagues=new[]{new LeagueData{id="test",name="Test",country="France"}},clubs=Enumerable.Range(0,4).Select(i=>new ClubData{id="c"+i,name="Club "+i,league="test",playable=true,annualRevenue=20000000}).ToArray(),players=Enumerable.Range(0,96).Select(i=>new PlayerData{id="p"+i,name="Player "+i,team="c"+i/24,age=24,position=i%24<2?"GK":"CB",positions=new[]{i%24<2?"GK":"CB"},rating=65,potential=80,value=100000,wage=500}).ToArray()};
            c=new Career{club="c0"};c.lineup=Career.Select(db,c.club,c.tactic);c.EnsureWorld(db);
        }
        void Tick(){c.life.day++;typeof(Career).GetMethod("ScoutingDay",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(c,new object[]{db});}
        [Test] public void MissionCanScoutASourcedTerritoryOutsideLeagueCatalogue()
        {
            var target=db.clubs.First(x=>x.id=="c1");target.league="europe";target.country="Norvège";target.countrySource="https://www.uefa.com/uefachampionsleague/clubs/";target.countryAsOf="2026-10-09";
            var m=c.CreateScoutMission(db,"Tous","Tous",16,45,100000000,1000000,"ready",c.ObservationCost,"Norvège");var candidates=c.ScoutCandidates(db,m).ToArray();Assert.IsNotEmpty(candidates);Assert.IsTrue(candidates.All(p=>p.team==target.id));Tick();Assert.AreEqual(1,m.found);Assert.AreEqual(target.id,db.Find(m.players.Single()).team);
        }
        [Test] public void CountryMissionExcludesFreePlayersButAllStillFindsThem()
        {
            db.Find("p24").team="free";db.leagues[0].country="France";var m=c.CreateScoutMission(db,"Tous","Tous",16,45,100000000,1000000,"free",c.ObservationCost,"France");Assert.AreEqual(0,c.ScoutCandidates(db,m).Count());m.country="Tous";Assert.IsTrue(c.ScoutCandidates(db,m).Any(p=>p.id=="p24"));
        }
        [Test] public void UnsourcedOutsideTerritoryCannotDebitAMission()
        {
            var target=db.clubs.First(x=>x.id=="c1");target.league="europe";target.country="Atlantis";long cash=c.life.cash;Assert.Throws<ArgumentException>(()=>c.CreateScoutMission(db,"Tous","Tous",16,45,100000000,1000000,"ready",c.ObservationCost,"Atlantis"));Assert.AreEqual(cash,c.life.cash);
        }
    }
}

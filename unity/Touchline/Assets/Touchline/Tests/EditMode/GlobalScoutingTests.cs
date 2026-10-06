using System;
using System.Linq;
using NUnit.Framework;
using Touchline.Core;
using UnityEngine;

namespace Touchline.Tests
{
    public class GlobalScoutingTests
    {
        static Database Synthetic(int europeanClubs)
        {
            var clubs=Enumerable.Range(0,europeanClubs+2).Select(i=>new ClubData{id="c"+i,name="Club "+i,league=i<europeanClubs?"fra.1":"usa.1",playable=i<europeanClubs,annualRevenue=100000000}).ToArray();
            var players=clubs.SelectMany(c=>Enumerable.Range(0,11).Select(i=>new PlayerData{id=c.id+"p"+i,name=c.name+" Player "+i,team=c.id,age=26,rating=70,potential=70,fitness=100,morale=75,wage=1000,value=1000000,
                position=i==0?"GB":i<5?"DEF":i<8?"MIL":"ATT",positions=new[]{i==0?"GK":i<5?"CB":i<8?"CM":"ST"}})).ToArray();
            return new Database{clubs=clubs,players=players,leagues=new[]{new LeagueData{id="fra.1",name="European league"},new LeagueData{id="usa.1",name="Scouting league",scoutingOnly=true}}};
        }

        [Test] public void ScoutingClubsRemainRecruitableWithoutDomesticFixtures()
        {
            var db=Synthetic(2);var c=new Career{club="c0"};c.EnsureWorld(db);
            Assert.AreEqual(11,db.Squad("c2").Count,"A scouting-only team must keep its actual roster accessible.");
            Assert.IsNotNull(db.Find("c2p9"));
            Assert.AreEqual(1,c.world.divisions.Count);
            Assert.AreEqual("fra.1",c.world.divisions[0].id);
            Assert.IsFalse(c.world.fixtures.Any(f=>f.home=="c2"||f.away=="c2"||f.home=="c3"||f.away=="c3"));
            Assert.IsFalse(c.world.cups.Any(cup=>cup.id=="cup-usa"||cup.entrants.Contains("c2")||cup.entrants.Contains("c3")));
        }

        [Test] public void FutureEuropeanQualificationCannotSelectWorldwideScoutingClubs()
        {
            var db=Synthetic(108);var c=new Career{club="c0"};c.EnsureLife(db);c.life.day=365;c.EnsureWorld(db);
            Assert.AreEqual(2027,c.world.year);
            var europe=c.world.cups.Where(cup=>cup.european).ToArray();
            Assert.AreEqual(3,europe.Length,"Exercise all three generated European entrant pools.");
            Assert.AreEqual(108,europe.SelectMany(cup=>cup.entrants).Distinct().Count());
            Assert.IsFalse(europe.Any(cup=>cup.entrants.Contains("c108")||cup.entrants.Contains("c109")));
            Assert.IsFalse(c.world.fixtures.Any(f=>f.home=="c108"||f.away=="c108"||f.home=="c109"||f.away=="c109"));
        }

        [Test] public void WorldwideIdentitiesKeepDatedBiographiesAndEstimatedFinanceLabels()
        {
            var db=JsonUtility.FromJson<Database>(Resources.Load<TextAsset>("Data/database").text);
            var worldLeagues=db.leagues.Where(l=>l.scoutingOnly).Select(l=>l.id).ToArray();
            var worldClubs=db.clubs.Where(c=>worldLeagues.Contains(c.league)).ToArray();
            var ids=worldClubs.Select(c=>c.id).ToArray();var players=db.players.Where(p=>ids.Contains(p.team)).ToArray();
            Assert.AreEqual(15,worldLeagues.Length);Assert.AreEqual(286,worldClubs.Length);Assert.AreEqual(9174,players.Length);
            Assert.IsTrue(worldClubs.All(c=>!c.playable));
            Assert.IsFalse(worldClubs.Any(c=>c.name=="TBD Home"||c.name=="TBD Away"),"Fixture placeholders are not clubs to scout or finance.");
            Assert.IsTrue(players.All(p=>!string.IsNullOrEmpty(p.birthDate)&&p.rosterAsOf=="2026-10-05"));
            Assert.AreEqual(players.Length,players.Select(p=>p.id).Distinct().Count());
            Assert.AreEqual(players.Length,players.Select(p=>p.name.ToLowerInvariant()+"|"+p.birthDate).Distinct().Count());
            Assert.IsTrue(players.All(p=>!string.IsNullOrEmpty(p.assessment)&&!string.IsNullOrEmpty(p.salarySource)&&p.valueSource.StartsWith("Estimation")));
            var published=players.Where(p=>p.salarySource.StartsWith("Base brute publiée MLSPA2026")).ToArray();
            Assert.AreEqual(603,published.Length,"Only unique full-name and concordant MLS club matches may use the dated public salary guide.");
            Assert.IsTrue(players.Except(published).All(p=>p.salarySource.StartsWith("Estimation")),"Unknown contracts and composite image-rights costs must remain explicit estimates.");
            Assert.IsTrue(worldClubs.All(c=>c.financeSource.StartsWith("Estimation")&&c.annualRevenue>=1200000));
        }

        [Test] public void WorldwideStarsUseDatedAssessmentsWithoutCheapSalaryArbitrage()
        {
            var db=JsonUtility.FromJson<Database>(Resources.Load<TextAsset>("Data/database").text);
            var messi=db.Find("45843");var ronaldo=db.Find("22774");var neymar=db.Find("132948");var wang=db.Find("365941");
            Assert.AreEqual(89,messi.rating);Assert.AreEqual("Gauche",messi.preferredFoot);
            Assert.AreEqual(84,ronaldo.rating);Assert.AreEqual("Droit",ronaldo.preferredFoot);
            Assert.AreEqual(65,wang.rating);Assert.AreEqual(82,neymar.rating);
            Assert.AreEqual(414200,messi.wage,"MLSPA annual base25MUSD converted using ECB15June2026 /52; guaranteed compensation is not added twice.");
            Assert.Greater(ronaldo.wage,3000000,"Public contract estimates must prevent recruitment at a reserve wage.");
            Assert.AreEqual(314950,neymar.wage);StringAssert.Contains("PAS salaire de base",neymar.salarySource);
            StringAssert.Contains("Estimation",neymar.assessment);StringAssert.Contains("25/09/2026",messi.assessment);
            Assert.GreaterOrEqual(messi.potential,messi.rating);Assert.GreaterOrEqual(ronaldo.potential,ronaldo.rating);
        }
    }
}

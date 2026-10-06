using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Touchline.Core;
namespace Touchline.Tests
{
    public sealed class RoleMatchOpportunityTests
    {
        Database db;Career c;PlayerLife p;
        [SetUp] public void SetUp(){c=StaffTrainingLifecycleTests.Fixture(out db);p=c.Person("p1");c.world.fixtures.Clear();c.world.history.Clear();c.life.day=60;var contract=c.Contract(db,p.id);contract.role="key";contract.joined=0;contract.appearancesAtSigning=0;}
        Fixture Game(int day=47,string id="actual"){var f=new Fixture{id=id,home=c.club,away="c1",league="fra.1",played=true,day=day};c.world.fixtures.Add(f);return f;}
        void Review()=>typeof(Career).GetMethod("DevelopmentAndRoles",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(c,new object[]{db});
        [Test] public void NoPlayedFixtureCannotBreakAContractRole(){Review();Assert.AreEqual(75,p.morale);Assert.AreEqual(60,p.trust);Assert.IsFalse(c.life.messages.Any(m=>m.subject=="Mon rôle dans votre projet"));}
        [Test] public void AllAvailableAppearancesAreRespectedEvenInSparseCalendar(){Game();p.appearances=1;Review();Assert.AreEqual(75,p.morale);Assert.AreEqual(60,p.trust);}
        [Test] public void GenuineBenchPlayerStillReceivesConsequences(){Game();Review();Assert.AreEqual(71,p.morale);Assert.AreEqual(57,p.trust);Assert.AreEqual(1,c.life.messages.Count(m=>m.subject=="Mon rôle dans votre projet"));}
        [Test] public void FormerManagerDoesNotReceiveContractRoleComplaints(){Game();c.world.managerStatus="dismissed";Review();Assert.AreEqual(75,p.morale);Assert.AreEqual(60,p.trust);}
        [Test] public void CurrentSuspensionOrAgreedRestPausesRoleComplaint(){Game();p.banUntil=61;Review();Assert.AreEqual(75,p.morale);p.banUntil=0;p.restUntil=61;Review();Assert.AreEqual(75,p.morale);}
        [Test] public void OnlyPlayedOwnFixturesSinceSigningCount(){var before=Game(10,"before");Game(47,"valid");Game(70,"future");Game(48,"unplayed").played=false;Game(49,"other").home="c2";c.Contract(db,p.id).joined=20;Assert.AreEqual(1,c.RoleMatchOpportunities(p.id));}
        [Test] public void PreviousSeasonArchiveCountsWithoutDuplicatingCurrentFixtures(){var same=Game();c.world.history.Add(same);c.world.history.Add(new Fixture{id="previous-season",home=c.club,away="c2",played=true,day=20});Assert.AreEqual(2,c.RoleMatchOpportunities(p.id));}
        [Test] public void DocumentedPastInjuryDoesNotInventPlayingOpportunity(){Game(47,"injured");Game(53,"recovered");c.life.medical.Add(new MedicalCase{player=p.id,opened=45,closed=50});Assert.AreEqual(1,c.RoleMatchOpportunities(p.id));p.appearances=1;Review();Assert.AreEqual(75,p.morale);}
        [Test] public void IncomingLoanAndFormerOwnershipHaveNoPermanentRoleOpportunity(){Game();c.Contract(db,p.id).parent="c2";Assert.AreEqual(0,c.RoleMatchOpportunities(p.id));c.Contract(db,p.id).parent=null;c.Contract(db,p.id).club="c2";Assert.AreEqual(0,c.RoleMatchOpportunities(p.id));}
        [Test] public void RenewedContractUsesAppearanceBaseline(){Game(47);var contract=c.Contract(db,p.id);contract.joined=0;contract.appearancesAtSigning=20;p.appearances=21;Review();Assert.AreEqual(75,p.morale);}
        [Test] public void SparseLegacyArchiveIsNeverReplacedWithInventedWeeks(){c.world.history=null;Game();p.appearances=18;Review();Assert.AreEqual(75,p.morale);Assert.IsNull(c.world.history);}
        [Test] public void PublicFirstSummerFortnightHasNoArtificialBenchComplaint(){c=StaffTrainingLifecycleTests.Fixture(out db);c.life.training="rest";for(int d=1;d<=14;d++)c.AdvanceDay(db);Assert.AreEqual(0,c.world.fixtures.Count(f=>f.played&&(f.home==c.club||f.away==c.club)));Assert.IsFalse(c.life.messages.Any(m=>m.subject=="Un moment pour parler ?"));Assert.IsTrue(c.life.players.All(x=>x.morale==75));}
        [Test] public void PublicFortnightStillContactsAnActuallyUnusedPlayer(){c.life.day=13;c.life.training="rest";Game(7);c.AdvanceDay(db);Assert.AreEqual(1,c.life.messages.Count(m=>m.subject=="Un moment pour parler ?"));}
        [TestCase("key",7,75)][TestCase("key",6,71)][TestCase("starter",5,75)][TestCase("starter",4,71)]
        public void ExistingRoleShareThresholdIsPreserved(string role,int appearances,int expectedMorale){c.Contract(db,p.id).role=role;for(int i=0;i<10;i++)Game(40+i,"game-"+i);p.appearances=appearances;Review();Assert.AreEqual(expectedMorale,p.morale);}
    }
}

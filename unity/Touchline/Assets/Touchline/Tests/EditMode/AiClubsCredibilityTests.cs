using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Touchline.Core;

namespace Touchline.Tests
{
    // IA des autres clubs sur plusieurs saisons : propriétaire endetté, vendeurs
    // qui gardent leurs titulaires, départs remplacés sans gonfler l'effectif,
    // rotation en coupe nationale.
    public sealed class AiClubsCredibilityTests
    {
        Database db;Career c;
        // Huit clubs de 24 joueurs (deux divisions), le club dirigé est c0.
        [SetUp] public void Setup(){db=new Database{leagues=new[]{new LeagueData{id="fra.1",name="L1"},new LeagueData{id="fra.2",name="L2",tier=2}},clubs=Enumerable.Range(0,8).Select(i=>new ClubData{id="c"+i,name="Club "+i,league=i<4?"fra.1":"fra.2",playable=true,annualRevenue=20000000+i*1000000}).ToArray(),players=Enumerable.Range(0,8*24).Select(i=>new PlayerData{id="p"+i,name="Alex "+i,team="c"+(i/24),age=24,position=i%24==0||i%24==12?"GB":i%24<8?"DEF":i%24<16?"MIL":"ATT",positions=new[]{i%24==0||i%24==12?"GK":i%24<8?"CB":i%24<16?"CM":"ST"},rating=65,potential=80,value=100000,wage=500,fitness=100,morale=75,attributes=new[]{new AttributeValue{key="shortPassing",value=65},new AttributeValue{key="sprintSpeed",value=65}}}).ToArray()};c=new Career{club="c0"};c.lineup=Career.Select(db,c.club,c.tactic);c.EnsureWorld(db);}
        void AiSummer(){typeof(Career).GetMethod("AiSummerEconomy",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(c,new object[]{db});}
        [Test] public void IndebtedOwnerStopsFundingPlannedLosses()
        {
            Assert.AreEqual(.9f,Career.AiOwnerStanceAmbition(.9f,0,100_000_000),1e-6,"Sans dette : ambition inchangée");
            Assert.AreEqual(.9f,Career.AiOwnerStanceAmbition(.9f,10_000_000,100_000_000),1e-6,"Petite dette tolérée");
            float half=Career.AiOwnerStanceAmbition(.9f,32_500_000,100_000_000);
            Assert.That(half,Is.GreaterThan(0f).And.LessThan(.9f),"Dette moyenne : ambition réduite");
            Assert.AreEqual(0f,Career.AiOwnerStanceAmbition(.9f,80_000_000,100_000_000),1e-6,"Dette lourde : politique prudente");
        }

        [Test] public void SellerKeepsItsStartersFromAClubOfTheSameSize()
        {
            var squad=db.Squad("c1").OrderBy(p=>p.id,System.StringComparer.Ordinal).ToList();
            foreach(var p in squad){p.rating=70;p.value=100000;}
            var starters=squad.Take(11).ToList();foreach(var p in starters)p.rating=85;
            c.world.year++;c.life.day=365;AiSummer();
            Assert.That(c.world.aiTransfers.Any(t=>t.seller=="c1"),"Le banc reste vendable");
            Assert.IsFalse(c.world.aiTransfers.Any(t=>t.seller=="c1"&&starters.Any(p=>p.id==t.player)),"Aucun titulaire cédé à un club de taille comparable");
        }

        [Test] public void AnnouncedDeparturesAreReplacedOnlyUpToSquadSizeAndByFreeAgentsFirst()
        {
            c.EnsureAiClubAccounts(db);
            c.world.developmentReferences=new List<ClubDevelopmentReference>{new ClubDevelopmentReference{club="c1",rating=65,wage=500,value=100000,revenue=21000000,squadSize=22}};
            var team=db.Squad("c1").Select(p=>p.id).ToArray();
            foreach(var contract in c.world.contracts.Where(x=>team.Contains(x.player))){contract.until=c.life.day+10;contract.wage=100000;db.Find(contract.player).wage=100000;}
            var free=Enumerable.Range(0,6).Select(i=>new PlayerData{id="free"+i,name="Libre "+i,team="free",age=27,position=i==0?"GB":"DEF",positions=new[]{i==0?"GK":"CB"},rating=64,potential=64,value=50000,wage=900,fitness=100,morale=75,attributes=new[]{new AttributeValue{key="shortPassing",value=64}}}).ToArray();
            db.players=db.players.Concat(free).ToArray();
            typeof(Career).GetMethod("AnnualPlayerDevelopment",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(c,new object[]{db});
            var contracts=c.world.contracts.GroupBy(x=>x.player).ToDictionary(g=>g.Key,g=>g.Last());
            var staying=db.Squad("c1").Where(p=>!(contracts.TryGetValue(p.id,out var e)&&e.aiRelease)).ToArray();
            Assert.AreEqual(24,db.Squad("c1").Count(p=>contracts[p.id].aiRelease),"Les 24 refusent la baisse de salaire");
            Assert.AreEqual(22,staying.Length,"Remplacés jusqu'à la taille de référence, pas un pour un");
            Assert.That(staying.Count(p=>p.id.StartsWith("free")),Is.GreaterThanOrEqualTo(5),"Joueurs libres du même poste recrutés d'abord");
            Assert.That(staying.Where(p=>p.id.StartsWith("free")).All(p=>contracts[p.id].club=="c1"&&p.wage<100000));
        }

        [Test] public void DomesticCupRotationOnlyForAClearlyStrongerSide()
        {
            var cup=new Fixture{league="cup-fra",home="c1",away="c5"};
            Assert.AreEqual(3,Career.CupRestedPlayers(cup,72,64),"Favori net : il fait tourner");
            Assert.AreEqual(0,Career.CupRestedPlayers(cup,64,72),"Outsider : équipe type");
            Assert.AreEqual(0,Career.CupRestedPlayers(cup,70,68),"Écart faible : équipe type");
            Assert.AreEqual(0,Career.CupRestedPlayers(new Fixture{league="cup-fra",neutral=true},72,64),"Finale : équipe type");
            Assert.AreEqual(0,Career.CupRestedPlayers(new Fixture{league="fra.1"},72,64),"Championnat : équipe type");
        }
    }
}

using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Touchline.Core;

namespace Touchline.Tests
{
    public class CompactSaveTests
    {
        string catalogue;Database db;Career c;
        static Database Catalogue()=>new Database{leagues=new[]{new LeagueData{id="fra.1",name="L1"}},clubs=Enumerable.Range(0,4).Select(i=>new ClubData{id="c"+i,name="Club "+i,league="fra.1",playable=true,annualRevenue=20000000}).ToArray(),
            players=Enumerable.Range(0,4*24).Select(i=>new PlayerData{id="p"+i,name="Alex "+i,team="c"+(i/24),age=24+i%9,birthDate=i%3==0?"1999-0"+(1+i%9)+"-1"+(i%9):null,position=i%24==0||i%24==12?"GB":i%24<8?"DEF":i%24<16?"MIL":"ATT",positions=new[]{i%24==0||i%24==12?"GK":i%24<8?"CB":i%24<16?"CM":"ST"},rating=60+i%15,potential=80,value=100000,wage=500,fitness=100,morale=75,source="https://example.test/player/"+i,
                attributes=new[]{new AttributeValue{key="shortPassing",value=60+i%11},new AttributeValue{key="sprintSpeed",value=65},new AttributeValue{key="finishing",value=50+i%7}}}).ToArray()};
        [SetUp] public void Setup()
        {
            catalogue=JsonUtility.ToJson(Catalogue());db=JsonUtility.FromJson<Database>(catalogue);
            c=new Career{club="c0",saveBaseline=SaveBaseline.From(db)};c.lineup=Career.Select(db,c.club,c.tactic);c.EnsureWorld(db);
        }
        void Play(int days){for(int i=0;i<days;i++){var f=c.NextFixture();if(f!=null&&f.day<=c.life.day){c.PrepareLineup(db);var sim=MatchSimulation.Create(db,c,f.home==c.club?f.away:f.home);c.match=sim.State;c.world.activeFixture=f.id;c.ApplyMatchContext(sim);sim.State.finished=true;sim.State.clock=720;sim.State.score[0]=1;c.RecordMatch(db);c.match=null;}c.AdvanceDay(db);}}
        string Compact(){bool packed=c.PrepareCompactSave();Assert.IsTrue(packed);try{return JsonUtility.ToJson(c);}finally{c.RestoreAfterSave();}}
        Career Load(string json,out Database restored){var state=JsonUtility.FromJson<Career>(json);Assert.IsTrue(CareerSaveRestore.TryRestore(JsonUtility.FromJson<Database>(catalogue),state,out restored));return state;}
        void AssertSameReload()
        {
            string full=JsonUtility.ToJson(c),compact=Compact();
            Assert.AreEqual(full,JsonUtility.ToJson(c),"Saving must leave the live career untouched");
            var a=Load(full,out var dbA);var b=Load(compact,out var dbB);
            Assert.AreEqual(JsonUtility.ToJson(a),JsonUtility.ToJson(b));
            Assert.AreEqual(dbA.players.Length,dbB.players.Length);
            for(int i=0;i<dbA.players.Length;i++)Assert.AreEqual(JsonUtility.ToJson(dbA.players[i]),JsonUtility.ToJson(dbB.players[i]),dbA.players[i].id);
        }

        [Test] public void FreshCareerReloadsIdenticallyFromTheCompactFormat()=>AssertSameReload();
        [Test] public void ChangedPlayersContractsAndResultsReloadIdentically()
        {
            Play(20);
            var p=db.Find("p30");p.team="c0";p.rating+=1.37f;p.attributes[1].value=71.25f;p.salarySource="Contrat négocié | test\tavec séparateurs\\";p.positions=new[]{"CM","DM"};p.evidence=null;
            var q=db.Find("p5");q.attributes[0].value+=2.0879974f;q.attributes[2].value=10;q.name="Nom @changé";
            c.Contract(db,"p30").terms=new MarketTerms{loanWagePercent=40,goalBonus=5000};c.Contract(db,"p31").role="starter";
            c.world.rosterChanges.Add(p);c.world.rosterChanges.Add(q);
            AssertSameReload();
        }
        [Test] public void GeneratedPlayersAreKeptCompletely()
        {
            var made=new PlayerData{id="gen-x",name="Nouveau Joueur",team="academy-c0",age=16,rating=48.5f,potential=77,attributes=new[]{new AttributeValue{key="vision",value=51.3f}}};
            db.players=db.players.Concat(new[]{made}).ToArray();c.world.rosterChanges.Add(made);
            AssertSameReload();
        }
        [Test] public void CompactSaveIsMuchSmaller()
        {
            Play(10);string full=JsonUtility.ToJson(c),compact=Compact();
            Assert.Less(compact.Length,full.Length/3);
        }
        [Test] public void UnknownFieldFromAnotherVersionIsIgnored()
        {
            Play(5);var json=Compact();var state=JsonUtility.FromJson<Career>(json);
            state.world.compactContractSchema[0]="fieldRemovedInThisVersion";
            Assert.IsTrue(CareerSaveRestore.TryRestore(JsonUtility.FromJson<Database>(catalogue),state,out _));
        }
        [Test] public void WithoutBaselineTheFormerFormatIsKept()
        {
            c.saveBaseline=null;Assert.IsFalse(c.PrepareCompactSave());
            StringAssert.Contains("\"contracts\":[{",JsonUtility.ToJson(c));
        }
    }
}

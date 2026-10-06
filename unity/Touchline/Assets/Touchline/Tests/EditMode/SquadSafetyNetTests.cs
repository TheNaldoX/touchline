using System.Linq;
using NUnit.Framework;
using Touchline.Core;

namespace Touchline.Tests
{
    public class SquadSafetyNetTests
    {
        Database db;Career c;
        [SetUp] public void Setup()
        {
            db=new Database{leagues=new[]{new LeagueData{id="fra.1",name="L1"}},clubs=Enumerable.Range(0,4).Select(i=>new ClubData{id="c"+i,name="Club "+i,league="fra.1",playable=true,annualRevenue=20000000}).ToArray(),
                players=Enumerable.Range(0,4*24).Select(i=>new PlayerData{id="p"+i,name="Alex "+i,team="c"+(i/24),age=24,position=i%24==0||i%24==12?"GB":i%24<8?"DEF":i%24<16?"MIL":"ATT",positions=new[]{i%24==0||i%24==12?"GK":i%24<8?"CB":i%24<16?"CM":"ST"},rating=65,potential=80,value=100000,wage=500,fitness=100,morale=75,attributes=new[]{new AttributeValue{key="shortPassing",value=65},new AttributeValue{key="sprintSpeed",value=65}}}).ToArray()};
            c=new Career{club="c0"};c.lineup=Career.Select(db,c.club,c.tactic);c.EnsureWorld(db);
        }
        void Release(int keep,bool keepKeeper)
        {
            int kept=0;
            foreach(var p in db.Squad("c0").OrderBy(p=>p.Goalkeeper?0:1).ToArray()){
                if(kept<keep&&(keepKeeper||!p.Goalkeeper)){kept++;continue;}
                p.team="free";var contract=c.Contract(db,p.id);contract.club="free";c.life.players.RemoveAll(x=>x.id==p.id);
            }
        }
        [Test] public void FullSquadIsLeftUntouched()
        {
            int academy=db.players.Count(p=>p.team=="academy-c0");
            Assert.IsEmpty(c.EnsureMatchSquad(db));Assert.AreEqual(academy,db.players.Count(p=>p.team=="academy-c0"));
        }
        [Test] public void LapsedSquadIsCompletedFromTheAcademyInsteadOfBlockingTheFixture()
        {
            Assert.Greater(db.players.Count(p=>p.team=="academy-c0"),10,"The world starts with an academy intake");
            Release(5,false);Assert.Throws<System.InvalidOperationException>(()=>Career.Select(db,"c0",c.tactic));
            var promoted=c.EnsureMatchSquad(db);
            Assert.GreaterOrEqual(db.Squad("c0").Count,Career.MinimumMatchSquad);
            Assert.GreaterOrEqual(db.Squad("c0").Count(p=>p.Goalkeeper),1,"A goalkeeper is registered first");
            foreach(var id in promoted){Assert.AreEqual("c0",db.Find(id).team);Assert.AreEqual("c0",c.Contract(db,id).club);Assert.IsTrue(c.life.players.Any(x=>x.id==id));Assert.Greater(c.Contract(db,id).until,c.life.day);}
            Assert.DoesNotThrow(()=>c.PrepareLineup(db));
            Assert.IsTrue(c.life.messages.Any(m=>m.subject=="Effectif complété avec le centre de formation"));
        }
        [Test] public void PromotedYouthSurviveASaveReload()
        {
            Release(8,true);var promoted=c.EnsureMatchSquad(db);Assert.IsNotEmpty(promoted);
            var copy=UnityEngine.JsonUtility.FromJson<Career>(UnityEngine.JsonUtility.ToJson(c));
            var fresh=new Database{leagues=db.leagues,clubs=db.clubs,players=db.players.Select(p=>p.Copy()).ToArray()};
            foreach(var p in fresh.players.Where(p=>promoted.Contains(p.id)))p.team="academy-c0";
            copy.RestoreWorld(fresh);
            foreach(var id in promoted)Assert.AreEqual("c0",fresh.Find(id).team);
        }
    }
}

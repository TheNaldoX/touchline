using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Touchline.Core;
using UnityEngine;

namespace Touchline.Tests
{
    public class LaunchCareerStorageTests
    {
        string directory,path;Database db;
        [SetUp] public void Setup(){directory=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../artifacts/unity/launch-menu-unit",Guid.NewGuid().ToString("N")));Directory.CreateDirectory(directory);path=Path.Combine(directory,"career.json");db=new Database{clubs=new[]{new ClubData{id="a",name="A",league="test"},new ClubData{id="b",name="B",league="test"}},players=Enumerable.Range(0,22).Select(i=>new PlayerData{id="p"+i,name="P"+i,team=i<11?"a":"b",position=i%11==0?"GB":"MIL",rating=75}).ToArray(),leagues=new LeagueData[0],fixtures=new Fixture[0]};}
        Career State()=>new Career{club="a",manager="Validation",lineup=Enumerable.Range(0,11).Select(i=>"p"+i).ToArray(),life=new ClubLife{day=12}};
        [Test] public void NewCatalogueContainsImportedPlayersWithoutFictionalExtension()
        {
            var fresh=TouchlineCatalogue.Load();
            Assert.AreEqual(21815,fresh.players.Length);
            Assert.IsFalse(fresh.players.Any(p=>GeneratedWorld.IsGenerated(p.id)));
            Assert.IsFalse(fresh.clubs.Any(c=>GeneratedWorld.IsGenerated(c.id)));
            Assert.IsTrue(fresh.players.All(p=>p.source.StartsWith("https://www.espn.com/",StringComparison.Ordinal)));
        }
        [Test] public void RealCareerDoesNotLoadLegacyExtension()
        {
            var c=State();File.WriteAllText(path,JsonUtility.ToJson(c));
            var loaded=LaunchCareerStorage.Read(path,"Principal",db);
            Assert.IsTrue(loaded.Valid,loaded.Error);Assert.AreEqual(db.players.Length,loaded.Restored.players.Length);
            Assert.IsNotNull(loaded.State.saveBaseline);
        }
        [TestCase(false,false)][TestCase(true,false)][TestCase(false,true)][TestCase(true,true)]
        public void LegacyFictionalTransferRestoresWithoutChangingNewCatalogue(bool compact,bool recruited)
        {
            db.leagues=new[]{new LeagueData{id="test",name="Test",country="France"}};
            foreach(var team in db.clubs){team.playable=true;team.annualRevenue=20000000;}
            var original=JsonUtility.FromJson<Database>(JsonUtility.ToJson(db));
            var expanded=JsonUtility.FromJson<Database>(JsonUtility.ToJson(db));
            GeneratedWorld.AppendFrozen(expanded,JsonUtility.FromJson<Database>(Resources.Load<TextAsset>("Data/generated-world-v1").text));
            var c=new Career{club="a",saveBaseline=SaveBaseline.From(expanded)};
            c.lineup=Career.Select(expanded,c.club,c.tactic);c.EnsureWorld(expanded);
            var bought=expanded.players.First(p=>GeneratedWorld.IsGenerated(p.id)).Copy();
            if(recruited){bought.team=c.club;bought.wage=789;
                c.world.rosterChanges.RemoveAll(p=>p.id==bought.id);c.world.rosterChanges.Add(bought);}
            if(compact)Assert.IsTrue(c.PrepareCompactSave());
            string raw=JsonUtility.ToJson(c);c.RestoreAfterSave();File.WriteAllText(path,raw);
            var loaded=LaunchCareerStorage.Read(path,"Principal",original);
            Assert.IsTrue(loaded.Valid,loaded.Error);Assert.AreEqual(bought.team,loaded.Restored.Find(bought.id).team);
            Assert.AreEqual(bought.wage,loaded.Restored.Find(bought.id).wage);
            Assert.IsFalse(original.players.Any(p=>GeneratedWorld.IsGenerated(p.id)));
            Assert.AreEqual(raw,File.ReadAllText(path),"Reading must not rewrite a personal save.");
            Assert.IsTrue(loaded.State.PrepareCompactSave());
            File.WriteAllText(path,JsonUtility.ToJson(loaded.State));loaded.State.RestoreAfterSave();
            var again=LaunchCareerStorage.Read(path,"Principal",original);
            Assert.IsTrue(again.Valid,again.Error);Assert.AreEqual(bought.wage,again.Restored.Find(bought.id).wage);
        }
        [TestCase(false)][TestCase(true)] public void AttributeVisibilityChoiceSurvivesCareerLoad(bool reveal)
        {
            db.Find("p12").attributes=new[]{new AttributeValue{key="shortPassing",value=75}};
            var c=State();c.revealAttributes=reveal;File.WriteAllText(path,JsonUtility.ToJson(c));
            var loaded=LaunchCareerStorage.Read(path,"Principal",db);Assert.IsTrue(loaded.Valid,loaded.Error);
            Assert.AreEqual(reveal,loaded.State.revealAttributes);
            var assessment=loaded.State.AssessedAttribute(loaded.Restored,"p12","shortPassing");
            Assert.AreEqual(reveal,assessment.known,"An unobserved opponent must follow the selected mode");
            if(reveal)Assert.AreEqual(assessment.low,assessment.high);
        }
        [Test] public void LegacyCalendarMigrationRecalculatesAgesAfterDateShiftWithoutWritingFiles()
        {
            var c=State();c.calendarEpoch=null;db.Find("p0").birthDate="2000-07-21";db.Find("p0").age=26;
            string raw=JsonUtility.ToJson(c);raw=System.Text.RegularExpressions.Regex.Replace(raw,"\"calendarEpoch\"\\s*:\\s*(?:null|\"[^\"]*\"),?","");
            File.WriteAllText(path,raw);var e=LaunchCareerStorage.Read(path,"Legacy",db);
            Assert.IsTrue(e.Valid,e.Error);Assert.IsTrue(e.CalendarMigrated);Assert.AreEqual(79,e.State.life.day);Assert.AreEqual(26,e.Restored.Find("p0").age);Assert.AreEqual(raw,File.ReadAllText(path));Assert.AreEqual(26,db.Find("p0").age);
        }
        [Test] public void BrowsingMissingFilesDoesNotCreateACareer(){var e=LaunchCareerStorage.ReadMetadata(path,"Principal",db);Assert.IsFalse(e.Exists);Assert.IsFalse(e.CanAttempt);Assert.IsFalse(File.Exists(path));}
        [Test] public void JsonUtilityNullMatchRoundTripRemainsAPlayableCareer(){var c=State();File.WriteAllText(path,JsonUtility.ToJson(c));string before=File.ReadAllText(path);var e=LaunchCareerStorage.Read(path,"Principal",db);Assert.IsTrue(e.Valid,e.Error);Assert.IsNull(e.State.match);Assert.AreEqual(before,File.ReadAllText(path));}
        [Test] public void HeaderDoesNotRetainCareerWorldOrInventNullMatch(){var c=State();File.WriteAllText(path,JsonUtility.ToJson(c));var e=LaunchCareerStorage.ReadMetadata(path,"Principal",db);Assert.IsTrue(e.CanAttempt);Assert.AreEqual("a",e.Club);Assert.AreEqual("Validation",e.Manager);Assert.AreEqual(12,e.Day);Assert.IsFalse(e.HasMatch);Assert.IsFalse(e.Valid,"A header is not full validation");Assert.IsNull(e.State);Assert.IsNull(e.Restored);}
        [Test] public void RealMatchJsonUtilityRoundTripValidatesAndReportsChronology(){var c=State();c.match=MatchSimulation.Create(db,c,"b",71,2700).State;c.match.clock=1234;c.match.score[0]=2;File.WriteAllText(path,JsonUtility.ToJson(c));var header=LaunchCareerStorage.ReadMetadata(path,"Principal",db);Assert.IsTrue(header.HasMatch);Assert.AreEqual(20,header.MatchMinute);var e=LaunchCareerStorage.Read(path,"Principal",db);Assert.IsTrue(e.Valid,e.Error);Assert.AreEqual(22,e.State.match.actors.Length);Assert.AreEqual(1234,e.State.match.clock);Assert.AreSame(e.State.tactic,e.State.match.homeTactic);}
        [Test] public void CorruptPrimaryCannotContaminateReadableBackup(){File.WriteAllText(path,"{broken");File.WriteAllText(path+".backup",JsonUtility.ToJson(State()));string source=JsonUtility.ToJson(db),backup=File.ReadAllText(path+".backup");Assert.IsFalse(LaunchCareerStorage.Read(path,"Principal",db).Valid);Assert.IsTrue(LaunchCareerStorage.Read(path+".backup","Secours",db).Valid);Assert.AreEqual("{broken",File.ReadAllText(path));Assert.AreEqual(backup,File.ReadAllText(path+".backup"));Assert.AreEqual(source,JsonUtility.ToJson(db));}
        [Test] public void RecoveryWritePreservesBackupAndDamagedOriginal(){File.WriteAllText(path,"{broken");string backup=JsonUtility.ToJson(State());File.WriteAllText(path+".backup",backup);var c=State();c.life.day=20;LaunchCareerStorage.Write(path,JsonUtility.ToJson(c),false);Assert.AreEqual(backup,File.ReadAllText(path+".backup"));Assert.AreEqual("{broken",File.ReadAllText(Directory.GetFiles(directory,"career.json.invalid-*").Single()));Assert.IsTrue(LaunchCareerStorage.Read(path,"Principal",db).Valid);}
        [Test] public void KnownGoodPrimaryRotatesIntoRealBackup(){string prior=JsonUtility.ToJson(State());File.WriteAllText(path,prior);var c=State();c.life.day=20;LaunchCareerStorage.Write(path,JsonUtility.ToJson(c),true);Assert.AreEqual(prior,File.ReadAllText(path+".backup"));Assert.AreEqual(20,LaunchCareerStorage.Read(path,"Principal",db).State.life.day);}
        [Test] public void RealTruncatedMatchIsRejectedWithoutDiscardingItsFile(){var c=State();c.match=MatchSimulation.Create(db,c,"b").State;c.match.actors=c.match.actors.Take(21).ToArray();string raw=JsonUtility.ToJson(c);File.WriteAllText(path,raw);Assert.IsFalse(LaunchCareerStorage.Read(path,"Principal",db).Valid);Assert.AreEqual(raw,File.ReadAllText(path));}
        [Test] public void RealMatchWithInvalidScoreIsRejected(){var c=State();c.match=MatchSimulation.Create(db,c,"b").State;c.match.score=new[]{0};File.WriteAllText(path,JsonUtility.ToJson(c));Assert.IsFalse(LaunchCareerStorage.Read(path,"Principal",db).Valid);}
        [Test] public void MissingLegacyCountersAreRepairedButWrongLengthsRejected(){var c=State();c.match=MatchSimulation.Create(db,c,"b").State;c.match.metrics=null;c.match.completedPasses=null;File.WriteAllText(path,JsonUtility.ToJson(c));var restored=LaunchCareerStorage.Read(path,"Principal",db);Assert.IsTrue(restored.Valid,restored.Error);Assert.AreEqual(2,restored.State.match.metrics.Length);Assert.IsNotNull(restored.State.match.metrics[0]);Assert.AreEqual(2,restored.State.match.completedPasses.Length);c.match.passes=new[]{1};File.WriteAllText(path,JsonUtility.ToJson(c));Assert.IsFalse(LaunchCareerStorage.Read(path,"Principal",db).Valid);}
    }
}

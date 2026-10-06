using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Touchline.Core;
using UnityEngine;

namespace Touchline.Tests
{
    public sealed class CareerRosterOrderTests
    {
        static Database Original()=>new Database{leagues=new[]{new LeagueData{id="fra.1",name="L1"}},clubs=Enumerable.Range(0,4).Select(i=>new ClubData{id="c"+i,name="Club "+i,league="fra.1",playable=true,annualRevenue=20000000+i*1000000}).ToArray(),players=Enumerable.Range(0,96).Select(i=>new PlayerData{id="p"+i,name="Given"+i+" Family"+i,team="c"+(i/24),age=36,position=i%24<2?"GK":"CM",positions=new[]{i%24<2?"GK":"CM"},rating=65,potential=80,value=100000,wage=500,fitness=100,morale=75,attributes=new[]{new AttributeValue{key="shortPassing",value=65}}}).ToArray()};
        static Career Setup(Database db){var c=new Career{club="c0"};c.lineup=Career.Select(db,c.club,c.tactic);c.EnsureWorld(db);return c;}
        static void Annual(Career c,Database db){c.world.year++;c.life.day+=365;typeof(Career).GetMethod("AnnualPlayerDevelopment",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(c,new object[]{db});}
        [Test] public void EditedKnownPlayersStayAtTheirOriginalIndices()
        {
            var db=Original();var c=Setup(db);var ids=db.players.Select(p=>p.id).ToArray();var p=c.world.rosterChanges.FirstOrDefault(x=>x.id=="p80")??db.Find("p80").Copy();p.rating=71;c.world.rosterChanges.Insert(0,p);c.RestoreWorld(db);CollectionAssert.AreEqual(ids,db.players.Select(x=>x.id));Assert.AreEqual(71,db.Find("p80").rating);
        }
        [Test] public void NewlyCreatedPlayersAppendInTheirSavedCreationOrder()
        {
            var db=Original();var c=Setup(db);var existing=db.players.Select(p=>p.id).ToArray();c.world.rosterChanges.Insert(0,db.Find("p25").Copy());c.world.rosterChanges.Add(new PlayerData{id="new-a",team="free",name="A"});c.world.rosterChanges.Add(db.Find("p1").Copy());c.world.rosterChanges.Add(new PlayerData{id="new-b",team="free",name="B"});c.RestoreWorld(db);CollectionAssert.AreEqual(existing.Concat(new[]{"new-a","new-b"}),db.players.Select(p=>p.id));
        }
        [Test] public void RepeatedRestoreIsIdempotentForRosterOrderAndIdentity()
        {
            var db=Original();var c=Setup(db);c.world.rosterChanges.Insert(0,db.Find("p80").Copy());c.RestoreWorld(db);string first=JsonUtility.ToJson(db.players);c.RestoreWorld(db);Assert.AreEqual(first,JsonUtility.ToJson(db.players));Assert.AreEqual(db.players.Length,db.players.Select(p=>p.id).Distinct().Count());
        }
        [Test] public void InitialRosterIsNotMutatedByValidatedRestore()
        {
            var original=Original();var db=Original();var c=Setup(db);var p=db.Find("p80").Copy();p.rating=71;c.world.rosterChanges.Insert(0,p);string before=JsonUtility.ToJson(original);Assert.IsTrue(CareerSaveRestore.TryRestore(original,c,out var restored));Assert.AreEqual(before,JsonUtility.ToJson(original));Assert.AreEqual("p80",restored.players[80].id);Assert.AreEqual(71,restored.players[80].rating);
        }
        [Test] public void SavedEditOrderCannotChangeGeneratedNamesOrAnnualDevelopment()
        {
            var db=Original();var c=Setup(db);
            // New identities retain their actual creation order. Only edits to
            // existing database players arrive in a different order, as during
            // real negotiations and medical/contract updates.
            c.world.rosterChanges=db.players.Where(p=>p.id.StartsWith("gen-")).Concat(db.players.Where(p=>!p.id.StartsWith("gen-")).Reverse()).Select(p=>p.Copy()).ToList();
            var resumed=JsonUtility.FromJson<Career>(JsonUtility.ToJson(c));Assert.IsTrue(CareerSaveRestore.TryRestore(Original(),resumed,out var resumedDb));Annual(c,db);Annual(resumed,resumedDb);Assert.AreEqual(c.life.seed,resumed.life.seed);Assert.AreEqual(JsonUtility.ToJson(db.players),JsonUtility.ToJson(resumedDb.players));Assert.IsTrue(db.players.Any(p=>p.id.StartsWith("regen-")));
        }
        [Test] public void EmptyLegacyChangesPreserveAllPlayerPositions()
        {
            var db=Original();var c=Setup(db);c.world.rosterChanges=null;string before=JsonUtility.ToJson(db.players);c.RestoreWorld(db);Assert.AreEqual(before,JsonUtility.ToJson(db.players));
        }
        [Test] public void AnchoredRevenueProjectionKeepsItsProvenanceOnReload()
        {
            var db=Original();var c=Setup(db);typeof(Career).GetMethod("ChangeDivisionRevenue",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(c,new object[]{db,"c1",false});string source=db.clubs.Single(t=>t.id=="c1").financeSource;var resumed=JsonUtility.FromJson<Career>(JsonUtility.ToJson(c));Assert.IsTrue(CareerSaveRestore.TryRestore(Original(),resumed,out var restored));Assert.AreEqual(source,restored.clubs.Single(t=>t.id=="c1").financeSource);Assert.AreEqual(db.clubs.Single(t=>t.id=="c1").annualRevenue,restored.clubs.Single(t=>t.id=="c1").annualRevenue);
        }
        [Test] public void LegacyRevenueWithoutAnchorDoesNotInventInitialEvidence()
        {
            var db=Original();var c=Setup(db);c.changedRevenues.Add(new RevenueChange{club="c1",revenue=1234567});c.RestoreWorld(db);Assert.AreEqual("Projection de carrière après changement de division",db.clubs.Single(t=>t.id=="c1").financeSource);Assert.AreEqual(1234567,db.clubs.Single(t=>t.id=="c1").annualRevenue);
        }
    }
}

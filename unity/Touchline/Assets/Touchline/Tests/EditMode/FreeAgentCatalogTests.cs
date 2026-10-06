using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Touchline.Core;

namespace Touchline.Tests
{
    public partial class ProfessionalTests
    {
        FreeAgentReference Departure(string id="p24",string status="scheduled-release")=>new FreeAgentReference{id="release-"+id,playerId=id,displayName=db.Find(id).name,formerClubId=db.Find(id).team,formerClubName="Club 1",status=status,autoReleaseEligible=true,announcedAt="2026-06-01",asOfDate="2026-06-15",contractEndsOn="2026-06-30",effectiveDate="2026-07-01",sourceUrl="https://example.test/official-release",consultedDate="2026-10-05"};
        void Catalog(params FreeAgentReference[] refs){db.freeAgents=refs;db.freeAgentCatalogVersion++;c.EnsureWorld(db);}
        [Test] public void ConfirmedReleasePreservesEmployerUntilExpiryAndEnablesPrecontract()
        {
            Catalog(Departure());Assert.AreEqual("c1",db.Find("p24").team);Assert.IsNotNull(c.AnnouncedFreeAgentRelease(db,"p24"));Assert.IsTrue(c.CanPrecontract(db,"p24"));Assert.AreEqual(15,c.Contract(db,"p24").until);Days(16);Assert.AreEqual("free",db.Find("p24").team);Assert.IsNull(c.AnnouncedFreeAgentRelease(db,"p24"));
        }
        [Test] public void ReleaseUnknownBeforeAnnouncementDoesNotChangeTheContract()
        {
            var r=Departure();r.announcedAt="2026-06-20";r.asOfDate="2026-06-20";int until=c.Contract(db,"p24").until;Catalog(r);Assert.AreEqual(until,c.Contract(db,"p24").until);Assert.IsNull(c.AnnouncedFreeAgentRelease(db,"p24"));Days(5);Assert.IsNotNull(c.AnnouncedFreeAgentRelease(db,"p24"));
        }
        [Test] public void RenewalOffersAndUncertainExpiryCannotReleasePlayers()
        {
            Catalog(Departure("p24","renewal-offered"),Departure("p25","expiry-unknown"));Days(16);Assert.AreEqual("c1",db.Find("p24").team);Assert.AreEqual("c1",db.Find("p25").team);
        }
        [Test] public void ConflictingCurrentClubCannotBeOverriddenByAnOldReleaseList()
        {
            var r=Departure();r.formerClubId="c2";Catalog(r);Days(16);Assert.AreEqual("c1",db.Find("p24").team);
        }
        [Test] public void SignedCareerTransferSurvivesCatalogUpdatesAndUnitySaveReload()
        {
            c.ProposeTransfer(db,"p24",120000,600,3,"starter");Days(2);c.SignTransfer(db,"p24");Catalog(Departure());var restored=JsonUtility.FromJson<Career>(JsonUtility.ToJson(c));restored.RestoreWorld(db);restored.EnsureWorld(db);Assert.AreEqual(c.club,db.Find("p24").team);Assert.IsNull(restored.AnnouncedFreeAgentRelease(db,"p24"));
        }
        [Test] public void EflSeasonBoundaryDoesNotInventAnIndividualContractEnd()
        {
            var r=Departure();r.contractEndsOn=null;r.effectiveDateBasis="conservative-season-boundary-not-individual-contract-verification";int before=c.Contract(db,"p24").until;Catalog(r);Assert.AreEqual(before,c.Contract(db,"p24").until);Assert.IsNotNull(c.AnnouncedFreeAgentRelease(db,"p24"));Assert.IsFalse(c.CanPrecontract(db,"p24"));Days(16);Assert.AreEqual("free",db.Find("p24").team);
        }
        [Test] public void LaterObservedFreeIdentityNeedsKnownBirthAndPositionAndCorrectDate()
        {
            var r=new FreeAgentReference{id="future-real-name",displayName="Verified Candidate",birthDate="2000-01-15",positions=new[]{"GK"},nationality="France",status="free-observed-later",asOfDate="2026-10-05",announcedAt="2026-10-05",effectiveDate="2026-10-05",sourceUrl="https://example.test/official-list"};Catalog(r);Assert.IsFalse(db.players.Any(p=>p.name==r.displayName));c.life.day=(int)(new DateTime(2026,10,5)-Career.Epoch).TotalDays;c.EnsureWorld(db);var p=db.players.Single(p=>p.name==r.displayName);Assert.AreEqual(26,p.age);Assert.AreEqual("free",p.team);Assert.IsTrue(p.Goalkeeper);Assert.IsTrue(new[]{"gkReflexes","gkDiving","gkHandling","gkKicking","gkPositioning"}.All(k=>p.attributes.Any(a=>a.key==k)));Assert.IsTrue(p.assessment.Contains("estimés"));
        }
        [Test] public void MissingBirthOrPositionDoesNotCreateAnInventedPlayableIdentity()
        {
            var r=new FreeAgentReference{id="incomplete",displayName="Incomplete Candidate",status="free-observed-later",asOfDate="2026-06-15",effectiveDate="2026-06-15",birthDate=null,positions=new[]{"ST"}};Catalog(r);Assert.IsFalse(db.players.Any(p=>p.name==r.displayName));
        }
        FreeAgentReference FreeProfile(string name,string birth,string role,long value,string valuation="2026-05-01")=>new FreeAgentReference{id=name,sourceId="dated-free-test",displayName=name,birthDate=birth,positions=new[]{role},status="free-continuous",autoReleaseEligible=true,asOfDate="2026-06-15",effectiveDate="2026-05-01",sourceUrl="https://example.test/free",marketValueEuro=value,valuationDate=valuation,valuationSourceUrl="https://example.test/value",valuationObservedAt="2026-10-05"};
        [Test] public void DatedFreeValuationsSeedDistinctUncertainRoleProfilesAndNegotiableWages()
        {
            Catalog(FreeProfile("Senior Defender","1986-01-01","CB",800000),FreeProfile("Young Striker","2005-01-01","ST",50000));
            var defender=db.players.Single(p=>p.name=="Senior Defender");var striker=db.players.Single(p=>p.name=="Young Striker");
            Assert.Greater(defender.rating,striker.rating+15);Assert.AreEqual(800000,defender.value);Assert.Greater(defender.wage,striker.wage);
            Assert.Greater(defender.Attribute("standingTackle"),defender.Attribute("finishing"));Assert.Less(defender.Attribute("sprintSpeed"),defender.Attribute("composure"));
            Assert.Greater(striker.potential,striker.rating);Assert.AreEqual(defender.rating,defender.potential);Assert.IsNull(defender.preferredFoot);
            Assert.IsTrue(defender.assessment.Contains("incertitude"));Assert.IsTrue(defender.valueSource.Contains("2026-05-01"));Assert.IsTrue(defender.salarySource.Contains("estimées"));
        }
        [Test] public void LaterValuationDoesNotLeakIntoInitialAbilityOrEconomicValue()
        {
            Catalog(FreeProfile("Future Value","2000-01-01","CM",20000000,"2026-10-01"),FreeProfile("Unknown Value","2000-01-01","CM",0));
            var future=db.players.Single(p=>p.name=="Future Value");var unknown=db.players.Single(p=>p.name=="Unknown Value");
            Assert.AreEqual(unknown.rating,future.rating);Assert.AreEqual(unknown.value,future.value);Assert.IsFalse(future.valueSource.Contains("2026-10-01"));
        }
        [TestCase(false)][TestCase(true)] public void HomonymousFreeIdentityCannotMoveAnEmployedPlayerBornOnAnotherDate(bool nestedEvidence)
        {
            var existing=db.Find("p24");existing.name="Same Name";existing.age=19;
            if(nestedEvidence)existing.evidence=new PlayerIdentityEvidence{birthDate="2007-01-01"};else existing.birthDate="2007-01-01";
            var reference=FreeProfile("Same Name","1997-01-01","ST",50000);Catalog(reference);
            Assert.AreEqual("c1",existing.team);var newcomer=db.players.Single(p=>p.name==existing.name&&p.birthDate==reference.birthDate);
            Assert.AreEqual("free",newcomer.team);Assert.AreNotEqual(existing.id,newcomer.id);int count=db.players.Length;c.life.day++;c.EnsureWorld(db);Assert.AreEqual(count,db.players.Length);
        }
        [Test] public void OldBooleanImportDoesNotBlockNewVersionedReferencesOrDuplicatePlayers()
        {
            c.freeAgentsImported=true;Catalog(Departure());Assert.AreEqual(1,c.freeAgentsImportVersion);Days(16);db.freeAgentCatalogVersion=2;c.EnsureWorld(db);Assert.AreEqual(2,c.freeAgentsImportVersion);Assert.AreEqual(1,db.players.Count(p=>p.id=="p24"));Assert.AreEqual("free",db.Find("p24").team);
        }
        [Test] public void LegacyKeeperMigrationPreservesSignedClubAndAddsRecognizedKeeperKeys()
        {
            c.freeAgentsImported=true;var p=db.Find("p24");p.id="unfp-free-2";p.team=c.club;p.attributes=new[]{new AttributeValue{key="goalkeeperReflexes",value=57}};c.EnsureWorld(db);Assert.AreEqual(c.club,p.team);Assert.IsTrue(p.attributes.Any(a=>a.key=="gkReflexes"&&a.value==57));Assert.IsTrue(p.attributes.Any(a=>a.key=="gkHandling"));
        }
    }
}

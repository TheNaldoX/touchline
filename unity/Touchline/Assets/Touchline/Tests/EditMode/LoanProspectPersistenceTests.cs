using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Touchline.Core;

namespace Touchline.Tests
{
    public sealed class LoanProspectPersistenceTests
    {
        [Test] public void PersistedAppearanceBaselineAndReviewStopSurvivePurchase()
        {
            var(c,db)=LoanProspectChecks.Setup();var path=LoanProspectChecks.Start(c,db,end:42,appearances:5);c.world.fixtures.Clear();
            LoanProspectChecks.Fixture(c,1,"one");c.life.day=1;c.ReviewOutgoingLoanAppearances(db);
            var contract=c.Contract(db,"p20");var savedContract=JsonUtility.FromJson<Employment>(JsonUtility.ToJson(contract));var savedPath=JsonUtility.FromJson<YouthPath>(JsonUtility.ToJson(path));
            c.world.contracts[c.world.contracts.IndexOf(contract)]=savedContract;c.world.youth[c.world.youth.IndexOf(path)]=savedPath;
            c.ReviewOutgoingLoanAppearances(db);Assert.AreEqual(1,savedPath.loanAppearances,"Loading replayed the already-reviewed fixture.");
            for(int day=2;day<=5;day++){LoanProspectChecks.Fixture(c,day,"game"+day);c.life.day=day;c.ReviewOutgoingLoanAppearances(db);}
            Assert.IsTrue(savedContract.loanAppearanceTracking);Assert.AreEqual(5,savedPath.loanAppearances);Assert.AreEqual(5,savedPath.loanReviewedThrough);
            c.life.day=42;typeof(Career).GetMethod("ResolveLoans",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(c,new object[]{db});Assert.AreEqual("c1",db.Find("p20").team);
        }
        [Test] public void LoanGrowthDoesNotOvershootAFractionalPotential()
        {
            var(c,db)=LoanProspectChecks.Setup();var path=LoanProspectChecks.Start(c,db);var p=db.Find("p20");p.rating=70;p.potential=70.2f;path.progress=.9999f;c.world.fixtures.Clear();LoanProspectChecks.Fixture(c,1,"one");c.life.day=1;c.ReviewOutgoingLoanAppearances(db);
            Assert.Greater(path.trackedLoanMinutes,0);Assert.AreEqual(70.2f,p.rating);
        }
        [Test] public void LoanCounterDoesNotRelabelLegacyTrainingMinutesAsMatches()
        {
            var(c,db)=LoanProspectChecks.Setup();var path=LoanProspectChecks.Start(c,db);path.minutes=6000;c.world.fixtures.Clear();LoanProspectChecks.Fixture(c,1,"one");c.life.day=1;c.ReviewOutgoingLoanAppearances(db);
            Assert.Greater(path.trackedLoanMinutes,0);Assert.AreEqual(6000,path.minutes-path.trackedLoanMinutes);Assert.AreEqual(0,path.seniorMinutes);
        }
        [Test] public void OldJsonWithoutAppearanceFieldsUsesUnknownHistoricalBaseline()
        {
            var(c,db)=LoanProspectChecks.Setup();var path=LoanProspectChecks.Start(c,db,end:28,appearances:5);var contract=c.Contract(db,"p20");
            string json=JsonUtility.ToJson(contract).Replace("\"loanAppearanceTracking\":true,","").Replace(",\"loanAppearanceTracking\":true","");
            var old=JsonUtility.FromJson<Employment>(json);Assert.IsFalse(old.loanAppearanceTracking);
            c.world.contracts[c.world.contracts.IndexOf(contract)]=old;c.world.youth[c.world.youth.IndexOf(path)]=JsonUtility.FromJson<YouthPath>("{\"player\":\"p20\",\"group\":\"loan\",\"minutes\":9000}");
            c.world.fixtures.Clear();c.life.day=28;typeof(Career).GetMethod("ResolveLoans",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(c,new object[]{db});
            Assert.AreEqual("c0",db.Find("p20").team);Assert.IsTrue(old.loanAppearanceHistoryEstimated,"Old appearances were presented as verified rather than resumed from an unknown baseline.");
        }
    }
}

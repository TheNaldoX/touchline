using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Touchline.Core;

namespace Touchline.Tests
{
    public sealed class DataProvenanceTests
    {
        DataProvenance Load()=>JsonUtility.FromJson<DataProvenance>(Resources.Load<TextAsset>("Data/data-provenance").text);
        [Test] public void SidecarCoversEveryBundledPlayerAndClubWithoutNumericAbilities()
        {
            var catalog=Load();var db=JsonUtility.FromJson<Database>(Resources.Load<TextAsset>("Data/database").text);
            CollectionAssert.AreEquivalent(db.players.Select(p=>p.id),catalog.players.Select(p=>p.id));
            CollectionAssert.AreEquivalent(db.clubs.Select(c=>c.id),catalog.clubs.Select(c=>c.id));
            Assert.AreEqual(catalog.players.Length,catalog.players.Select(p=>p.id).Distinct().Count());
            var fields=typeof(PlayerProvenance).GetFields().Select(f=>f.Name).ToArray();
            foreach(var key in new[]{"rating","potential","attributes","development","estimate"})CollectionAssert.DoesNotContain(fields,key);
            Assert.That(catalog.inputSha256,Does.Match("^[0-9a-f]{64}$"));
        }
        [Test] public void EaRatingEvidenceDoesNotCertifyWageOrContract()
        {
            var players=Load().players.Where(p=>p.ratingProvider=="EA SPORTS").ToArray();Assert.Greater(players.Length,100);
            Assert.IsTrue(players.All(p=>p.contractStatus=="simulation"));
            var separatelySourced=players.Where(p=>p.salaryStatus!="simulation").ToArray();Assert.Greater(separatelySourced.Length,100);
            Assert.IsTrue(separatelySourced.All(p=>!string.IsNullOrEmpty(p.salaryUrl)&&p.salaryProvider!="EA SPORTS"&&p.salaryDate!=""),"An EA assessment must never certify remuneration; salary references require a separate source/date.");
            Assert.IsTrue(separatelySourced.All(p=>p.salaryStatus=="reported-base-currency-converted"||p.salaryStatus=="public-estimate-fixed-cost"||p.salaryStatus=="public-estimate-composite-contract-cost"));
            StringAssert.Contains("Estimation Touchline",ProvenanceLabels.Salary(true));
            StringAssert.Contains("non vérifié",ProvenanceLabels.Contract(true));
        }
        [Test] public void SignedCareerContractIsNotPresentedAsARealWorldVerifiedSalary()
        {
            StringAssert.Contains("carrière",ProvenanceLabels.Salary(false));
            StringAssert.Contains("pas une rémunération réelle vérifiée",ProvenanceLabels.Salary(false));
            StringAssert.Contains("carrière",ProvenanceLabels.Contract(false));
            StringAssert.Contains("simulation",ProvenanceLabels.Contract(null));
        }
        [Test] public void SalaryReferencesDistinguishPublishedBaseFromCompositeImageRightsAndCareerNegotiation()
        {
            var sources=Load().players;var messi=sources.Single(p=>p.id=="45843");var neymar=sources.Single(p=>p.id=="132948");
            Assert.AreEqual("MLS Players Association",messi.salaryProvider);Assert.AreEqual("USD",messi.salaryCurrency);Assert.AreEqual(25000000,messi.annualSalaryBase);Assert.AreEqual(28333333,messi.annualGuaranteedCompensation);
            StringAssert.Contains("référence séparée",ProvenanceLabels.Salary(true,messi));StringAssert.Contains("droits d’image",ProvenanceLabels.Salary(true,neymar));
            StringAssert.Contains("négocié",ProvenanceLabels.Salary(false,messi));StringAssert.Contains("pas une rémunération réelle vérifiée",ProvenanceLabels.Salary(false,messi));
        }
        [TestCase(0,"masqués")][TestCase(50,"fourchettes")][TestCase(100,"accessibles")]
        public void SourceLabelsRespectObservationStageAndNeverExposeAbility(int knowledge,string expected)
        {
            var source=new PlayerProvenance{ratingProvider="EA SPORTS",ratingEdition="FC 27"};
            var text=ProvenanceLabels.Assessment(source,knowledge);
            StringAssert.Contains(expected,text);StringAssert.Contains("pas une mesure objective",text);
            StringAssert.DoesNotContain("/ 20",text);StringAssert.DoesNotContain("★",text);
        }
        [Test] public void ChangedCareerAffiliationNeverClaimsToBeCurrentExternalRoster()
        {
            var source=new PlayerProvenance{baselineClub="176"};string before=JsonUtility.ToJson(source);
            StringAssert.Contains("modifiée dans votre carrière",ProvenanceLabels.Roster(source,"160"));
            StringAssert.Contains("pas vérifiée en temps réel",ProvenanceLabels.Roster(source,"176"));
            Assert.AreEqual(before,JsonUtility.ToJson(source));
            StringAssert.Contains("provenance non disponible",ProvenanceLabels.Roster(null,"176"));
        }
        [Test] public void MissingObservationDateIsNotSubstitutedWithImportDate()
        {
            Assert.AreEqual("date non renseignée",ProvenanceLabels.Date(null));
            Assert.AreEqual("date non renseignée",ProvenanceLabels.Date(""));
            Assert.AreEqual("date non renseignée",ProvenanceLabels.Date("unknown"));
            Assert.AreEqual("12/09/2026",ProvenanceLabels.Date("2026-09-12T20:11:33.493Z"));
        }
        [Test] public void PublishedCashAndRevenuesRemainSeparateReferencesWithAccountingPeriods()
        {
            var refs=Load().financialReferences;Assert.GreaterOrEqual(refs.Select(r=>r.club).Distinct().Count(),10);
            var madrid=refs.Where(r=>r.club=="86").ToArray();Assert.AreEqual(2,madrid.Length);
            Assert.AreEqual(1221300000,madrid.Single(r=>r.metric=="revenue").amount);
            Assert.AreEqual(82800000,madrid.Single(r=>r.metric=="cash").amount);
            foreach(var r in refs){CollectionAssert.Contains(new[]{"revenue","cash","personnel","operating-cost"},r.metric);Assert.IsNotEmpty(r.period);Assert.IsNotEmpty(r.scope);Assert.IsNotEmpty(r.publisher);Assert.IsNotEmpty(r.checkedAt);Assert.IsTrue(new Uri(r.url).Scheme=="https");}
            var marseille=refs.Where(r=>r.club=="176").ToArray();Assert.AreEqual(4,marseille.Length);
            Assert.AreEqual(188723000,marseille.Single(r=>r.metric=="revenue").amount);
            Assert.AreEqual(153653000,marseille.Single(r=>r.metric=="personnel").amount);
            Assert.AreEqual(103714000,marseille.Single(r=>r.metric=="operating-cost").amount);
            Assert.AreEqual(114152000,marseille.Single(r=>r.metric=="cash").amount);
            Assert.AreEqual(994000000,refs.Single(r=>r.club=="83").amount,"Barça actual revenue must not use next-year forecast");
        }
    }
}

using NUnit.Framework;
namespace Touchline.Tests
{
    public class MedicalResponsibilityTests
    {
        [TestCase("effective-retirement-before-team","conservative")] [TestCase("effective-retirement-before-team","surgery")] [TestCase("effective-retirement-before-team","injection")]
        [TestCase("retired-team","conservative")] [TestCase("retired-team","surgery")] [TestCase("retired-team","injection")]
        [TestCase("outgoing-loan","conservative")] [TestCase("outgoing-loan","surgery")] [TestCase("outgoing-loan","injection")]
        [TestCase("transfer","conservative")] [TestCase("transfer","surgery")] [TestCase("transfer","injection")]
        public void CareOutsideClubResponsibilityRefusesBeforeAnyEffect(string departure,string treatment)=>MedicalResponsibilityChecks.DepartedCareIsRefusedWithoutMutation(departure,treatment);
        [Test] public void AnnouncedFutureRetirementDoesNotRemoveCurrentTreatment()=>MedicalResponsibilityChecks.FutureRetirementRemainsTreatable();
        [Test] public void ALoanReturnCannotReopenOldCareButCanStartANewEpisode()=>MedicalResponsibilityChecks.ALoanReturnDoesNotReactivateAnOldDiagnosis();
        [Test] public void LegacyClosureRecordsObservationWithoutRecoveryAndRunsOnce()=>MedicalResponsibilityChecks.LegacyRepairIsExplicitAndIdempotent();
        [Test] public void ReadingHistoryOrDecisionCountersNeverRepairsState()=>MedicalResponsibilityChecks.ReadingNeverClosesAnEpisode();
        [Test] public void ClosureSurvivesSaveRoundtripAndMissingFieldsDoNotCloseOwnedLegacyCare()=>MedicalResponsibilityChecks.ClosurePersistsAndMissingLegacyFieldsStayActive();
        [TestCase(null)] [TestCase("")]
        public void ActiveCareSurvivesSaveWithNullOrEmptyClosure(string marker)=>MedicalResponsibilityChecks.ActiveCareSurvivesSave(marker);
    }
}

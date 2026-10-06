using NUnit.Framework;
namespace Touchline.Tests
{
    public class RetiredAgreementTests
    {
        [TestCase("date-before-team-change",false)] [TestCase("date-before-team-change",true)]
        [TestCase("retired-contract",false)] [TestCase("retired-contract",true)]
        [TestCase("retired-snapshot",false)] [TestCase("retired-snapshot",true)]
        [TestCase("snapshot-legacy-null-contracts",false)] [TestCase("snapshot-legacy-null-contracts",true)]
        public void EffectiveRetirementClosesOnlyTheDecisionWithoutReadingOrChangingHistory(string evidence,bool outgoing)=>RetiredAgreementChecks.Effective(evidence,outgoing);
        [TestCase(false)] [TestCase(true)] public void FutureAnnouncementKeepsCurrentAgreementsAndExpiry(bool outgoing)=>RetiredAgreementChecks.FutureAnnouncement(outgoing);
        [Test] public void PlayerRetirementDoesNotRemoveStaffOpportunities()=>RetiredAgreementChecks.StaffIsUnchanged();
        [TestCase("date-before-team-change")] [TestCase("retired-team")] [TestCase("retired-snapshot")] public void RetainedDialogueCallbackRefusesRetirementWithoutAnyMutation(string evidence)=>RetiredAgreementChecks.RetainedDialogueCannotMutateAfterRetirement(evidence);
        [Test] public void MissingOrUnrelatedPersistentEvidenceDoesNotInventRetirement()=>RetiredAgreementChecks.NullAndUnrelatedEvidence();
        [Test] public void EffectiveRetirementRemovesDialogueBeforeTeamMutationButFutureAnnouncementDoesNot()=>RetiredAgreementChecks.DiscussionBeforeTeamChange();
    }
}

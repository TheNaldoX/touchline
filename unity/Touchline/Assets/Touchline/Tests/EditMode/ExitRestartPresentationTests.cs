using NUnit.Framework;
namespace Touchline.Tests {
 public class ExitRestartPresentationTests {
  [TestCase("goal-kick",1)][TestCase("goal-kick",-1)][TestCase("corner",1)][TestCase("corner",-1)][TestCase("throw-in",1)][TestCase("throw-in",-1)]
  public void PreserveCrossingAndReplaceOnlyAtTrueSpot(string kind,int sign)=>ExitRestartScenarios.ExactCrossingAndNoReturnFlight(kind,sign);
  [TestCase(30,1)][TestCase(60,1)][TestCase(120,1)][TestCase(30,2)][TestCase(60,2)][TestCase(120,2)][TestCase(30,10)][TestCase(60,10)][TestCase(120,10)]
  public void RepositionIsCoveredAtEveryCadence(int fps,int speed)=>ExitRestartScenarios.OpaqueSwitchAtEveryCadence(fps,speed);
  [TestCase(false)][TestCase(true)]public void ActualCoreExitEvidence(bool firstRelease)=>ExitRestartScenarios.ActualBoundaryHook(firstRelease);
  [Test]public void PauseSkipRollbackIdentity()=>ExitRestartScenarios.PauseSkipRollbackAndIdentity();
  [Test]public void PreparationNeverWaitsForPresentation()=>ExitRestartScenarios.PreparationAndReleaseInvalidateWithoutDelay();
  [Test]public void FirstReleasedStepKeepsPreparationAndFlight()=>ExitRestartScenarios.FirstStepPreparationUsesGlobalFraction();
 }
}

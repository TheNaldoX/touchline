using NUnit.Framework;
namespace Touchline.Tests
{
 public class FrenchFootballPositionsTests
 {
  [TestCase("GK","GB","Gardien")]
  [TestCase("CB","DC","Défenseur central")]
  [TestCase("LB","DG","Défenseur gauche")]
  [TestCase("RB","DD","Défenseur droit")]
  [TestCase("LM","MG","Milieu gauche")]
  [TestCase("RM","MD","Milieu droit")]
  [TestCase("LW","AG","Ailier gauche")]
  [TestCase("RW","AD","Ailier droit")]
  [TestCase("DM","MDC","Milieu défensif")]
  [TestCase("CDM","MDC","Milieu défensif")]
  [TestCase("CM","MC","Milieu central")]
  [TestCase("AM","MOC","Milieu offensif")]
  [TestCase("CAM","MOC","Milieu offensif")]
  [TestCase("ST","BT","Buteur")]
  public void AllBundledAndFormationRolesHaveFrenchPresentation(string id,string compact,string label)
  {Assert.AreEqual(compact,FrenchFootballPositions.Short(id));Assert.AreEqual(label,FrenchFootballPositions.Label(id));}
  [Test] public void TranslationDoesNotChangeDatabaseOrSavedFilterCodes()
  {var ids=new[]{"CDM","CAM","LB","RW"};var before=(string[])ids.Clone();Assert.AreEqual("Milieu défensif / Milieu offensif / Défenseur gauche / Ailier droit",FrenchFootballPositions.List(ids));CollectionAssert.AreEqual(before,ids);Assert.AreEqual("MDC / MOC / DG / AD",FrenchFootballPositions.CompactList(ids));}
  [Test] public void MissingAndFutureRolesStayInspectable()
  {Assert.AreEqual("Poste non renseigné",FrenchFootballPositions.Label(null));Assert.AreEqual("—",FrenchFootballPositions.Short(""));Assert.AreEqual("NEW_ROLE",FrenchFootballPositions.Label("NEW_ROLE"));Assert.AreEqual("",FrenchFootballPositions.List(null));}
 }
}

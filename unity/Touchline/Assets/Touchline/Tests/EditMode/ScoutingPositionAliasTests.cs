using System.Linq;
using NUnit.Framework;
using Touchline.Core;
namespace Touchline.Tests
{
    public partial class ProfessionalTests
    {
        [TestCase("DM","CDM")][TestCase("AM","CAM")]
        public void ScoutMissionFindsImportedAliasesWithoutChangingSavedPositions(string requested,string imported)
        {
            foreach(var p in db.players)p.nationality="France";
            var target=db.Find("p25");target.nationality="Spain";target.positions=new[]{imported};target.position="MIL";target.age=19;target.wage=300;target.value=50000;
            var mission=Mission(requested,"Spain",18,21,60000,1400,"prospect");
            CollectionAssert.AreEqual(new[]{target.id},c.ScoutCandidates(db,mission).Select(p=>p.id));
            Days(1);CollectionAssert.AreEqual(new[]{target.id},mission.players);
            Assert.AreEqual(requested,mission.role);CollectionAssert.AreEqual(new[]{imported},target.positions);
        }
    }
    public class FootballPositionAliasTests
    {
        [TestCase("DM","CDM")][TestCase("AM","CAM")][TestCase("CDM","DM")][TestCase("CAM","AM")][TestCase("GK","GB")]
        public void SearchAliasesAreSymmetric(string requested,string stored)
        {Assert.IsTrue(FootballPositions.Matches(new PlayerData{position="MIL",positions=new[]{stored}},requested));}
        [Test] public void DefensiveMidfielderDoesNotIncludeEveryCentralMidfielder()
        {Assert.IsFalse(FootballPositions.Matches(new PlayerData{position="MIL",positions=new[]{"CM"}},"DM"));}
        [Test] public void BroadAndNullFiltersPreserveTheirMeaning()
        {Assert.IsTrue(FootballPositions.Matches(new PlayerData{position="MIL",positions=new[]{"CM"}},"MIL"));Assert.IsFalse(FootballPositions.Matches(null,"Tous"));}
    }
}

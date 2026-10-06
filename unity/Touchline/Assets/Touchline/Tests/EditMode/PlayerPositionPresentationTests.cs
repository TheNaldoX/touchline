using NUnit.Framework;
using Touchline.Core;
namespace Touchline.Tests
{
    public class PlayerPositionPresentationTests
    {
        [TestCase("ATT","RW,RM","Ailier droit","Ailier droit / Milieu droit")]
        [TestCase("MIL","RM,RW,CAM","Milieu droit","Milieu droit / Ailier droit / Milieu offensif")]
        [TestCase("ATT","ST,LW","Buteur","Buteur / Ailier gauche")]
        [TestCase("DEF","RB,RWB","Défenseur droit","Défenseur droit / Piston droit")]
        [TestCase("LB","CB,LB","Défenseur gauche","Défenseur gauche / Défenseur central")]
        [TestCase("CDM","DM,CDM,CM","Milieu défensif","Milieu défensif / Milieu central")]
        [TestCase("GB","GK,GB","Gardien","Gardien")]
        [TestCase("MIL","CAM,AM","Milieu offensif","Milieu offensif")]
        [TestCase("ATT","ATT","Attaquant","Attaquant")]
        [TestCase("FUTURE_CODE","","FUTURE_CODE","FUTURE_CODE")]
        [TestCase("","RW","Ailier droit","Ailier droit")]
        [TestCase("","","Poste non renseigné","Poste non renseigné")]
        public void PreciseRepertoireIsPresentedWithoutRewritingImportedIds(string primary,string repertoire,string label,string list)
        {
            var positions=repertoire.Length==0?new string[0]:repertoire.Split(',');var original=(string[])positions.Clone();var player=new PlayerData{position=primary,positions=positions};
            Assert.AreEqual(label,FrenchFootballPositions.PlayerLabel(player));Assert.AreEqual(list,FrenchFootballPositions.PlayerList(player));Assert.AreEqual(primary,player.position);CollectionAssert.AreEqual(original,player.positions);
        }
        [Test] public void MissingPrimaryFallsBackToFirstNonemptyGenericRepertoire(){var p=new PlayerData{positions=new[]{null,"","MIL"}};Assert.AreEqual("Milieu",FrenchFootballPositions.PlayerLabel(p));}
        [Test] public void NullPlayerAndNullRepertoireRemainExplicit(){Assert.AreEqual("Poste non renseigné",FrenchFootballPositions.PlayerList(null));Assert.AreEqual("Défenseur",FrenchFootballPositions.PlayerList(new PlayerData{position="DEF"}));}
        [Test] public void DisplayNeverChangesTacticalFitOrFilterMembership()
        {
            var p=new PlayerData{position="MIL",positions=new[]{"RM","RW","CAM"}};float fit=p.Fit("AM");bool filtered=FootballPositions.Matches(p,"AM");FrenchFootballPositions.PlayerList(p);FrenchFootballPositions.PlayerLabel(p);Assert.AreEqual(fit,p.Fit("AM"));Assert.AreEqual(filtered,FootballPositions.Matches(p,"AM"));
        }
    }
}

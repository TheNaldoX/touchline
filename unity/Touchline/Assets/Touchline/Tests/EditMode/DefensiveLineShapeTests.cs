using System;
using System.Linq;
using NUnit.Framework;
using Touchline.Core;

namespace Touchline.Tests
{
    public class DefensiveLineShapeTests
    {
        [TestCase("4-3-3")][TestCase("4-4-2")][TestCase("4-2-3-1")][TestCase("3-4-2-1")]
        public void DefensiveLineMovesTheBackLineWithoutMovingTheStrikerBlock(string formation)
        {
            var low=new Tactic{line=.2f};low.SetFormation(formation);
            var high=new Tactic{line=.8f};high.SetFormation(formation);
            int defender=Array.FindIndex(low.withoutBall,s=>s.role=="CB");
            int striker=Array.FindIndex(low.withoutBall,s=>s.role=="ST");
            float defenderShift=high.Position(defender,false,0).x-low.Position(defender,false,0).x;
            Assert.Greater(defenderShift,10,"Les défenseurs appliquent réellement la hauteur demandée.");
            Assert.AreEqual(low.Position(striker,false,0).x,high.Position(striker,false,0).x,"Changer la ligne des défenseurs ne doit pas déplacer aussi le premier rideau de 16 mètres.");
        }
        [TestCase("4-3-3")][TestCase("4-4-2")][TestCase("4-2-3-1")][TestCase("3-4-2-1")]
        public void MidfieldLinksBothLinesWithoutCopyingTheFullDefensiveShift(string formation)
        {
            var low=new Tactic{line=.2f};low.SetFormation(formation);var high=new Tactic{line=.8f};high.SetFormation(formation);
            int defender=Array.FindIndex(low.withoutBall,s=>s.role=="CB");
            int midfielder=Array.FindIndex(low.withoutBall,s=>s.role=="CM"||s.role=="DM");
            float shift=high.Position(midfielder,false,0).x-low.Position(midfielder,false,0).x;
            Assert.Greater(shift,0);Assert.Less(shift,high.Position(defender,false,0).x-low.Position(defender,false,0).x);
        }
        [Test] public void DefensiveLineDoesNotChangeTheInPossessionShape()
        {
            var low=new Tactic{line=.2f};var high=new Tactic{line=.8f};
            foreach(int slot in Enumerable.Range(0,11))foreach(float ball in new[]{-25f,0,25f})
                Assert.AreEqual(low.Position(slot,true,ball).x,high.Position(slot,true,ball).x);
        }
    }
}

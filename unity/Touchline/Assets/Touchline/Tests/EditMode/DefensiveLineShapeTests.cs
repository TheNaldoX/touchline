using System.Linq;
using NUnit.Framework;
using Touchline.Core;

namespace Touchline.Tests
{
    public sealed class DefensiveLineShapeTests
    {
        [TestCase("4-3-3")][TestCase("4-4-2")][TestCase("4-2-3-1")][TestCase("3-4-2-1")]
        public void RaisingDefensiveLineCompressesTheRearBlockWithoutMovingTheForwards(string shape)
        {
            var tactic=new Tactic();tactic.SetFormation(shape);
            tactic.line=.2f;var low=Enumerable.Range(0,11).Select(i=>tactic.Position(i,false,0)).ToArray();
            var attack=Enumerable.Range(0,11).Select(i=>tactic.Position(i,true,0)).ToArray();
            tactic.line=.8f;var high=Enumerable.Range(0,11).Select(i=>tactic.Position(i,false,0)).ToArray();
            int defender=Enumerable.Range(1,10).First(i=>tactic.withoutBall[i].y<35);
            int midfielder=Enumerable.Range(1,10).First(i=>tactic.withoutBall[i].y>40&&tactic.withoutBall[i].y<55);
            Assert.Less(high[midfielder].x-high[defender].x,low[midfielder].x-low[defender].x,"A higher defensive line closes the space to midfield.");
            Assert.Greater(high[defender].x-low[defender].x,10,"The actual defenders still obey the line setting.");
            for(int i=0;i<11;i++){
                if(tactic.withoutBall[i].y>=55)Assert.AreEqual(low[i].x,high[i].x,"The front engagement position belongs to its own shape and pressing.");
                Assert.AreEqual(low[i].z,high[i].z,"Defensive width remains independent.");
                Assert.AreEqual(attack[i].x,tactic.Position(i,true,0).x,"Attacking shape remains independent.");
            }
        }
    }
}

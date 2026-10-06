using System;
using System.Linq;
using NUnit.Framework;
using Touchline.Core;

namespace Touchline.Tests
{
    public partial class ProfessionalTests
    {
        [Test] public void TacticalSwapPreservesElevenAndExchangesExactSlots(){var ids=(string[])c.lineup.Clone();c.AssignTacticalPlayer(db,4,ids[7]);Assert.AreEqual(ids[7],c.lineup[4]);Assert.AreEqual(ids[4],c.lineup[7]);CollectionAssert.AreEquivalent(ids,c.lineup);}
        [Test] public void BenchAssignmentDoesNotMoveOtherStarters(){var ids=(string[])c.lineup.Clone();var incoming=db.Squad(c.club).First(p=>!ids.Contains(p.id));c.AssignTacticalPlayer(db,9,incoming.id);Assert.AreEqual(incoming.id,c.lineup[9]);for(int i=0;i<11;i++)if(i!=9)Assert.AreEqual(ids[i],c.lineup[i]);Assert.AreEqual(11,c.lineup.Distinct().Count());}
        [Test] public void TacticalAssignmentRejectsOpponentAndLeavesLineupUntouched(){var ids=(string[])c.lineup.Clone();Assert.Throws<InvalidOperationException>(()=>c.AssignTacticalPlayer(db,9,"p24"));CollectionAssert.AreEqual(ids,c.lineup);}
        [Test] public void LiveSwapPreservesActorFitnessAndCards(){var sim=MatchSimulation.Create(db,c,"c1");c.match=sim.State;var first=c.match.actors[4];var second=c.match.actors[7];first.fitness=48;first.yellows=1;c.AssignTacticalPlayer(db,7,first.id);Assert.AreSame(first,c.match.actors[7]);Assert.AreSame(second,c.match.actors[4]);Assert.AreEqual(48,c.match.actors[7].fitness);Assert.AreEqual(1,c.match.actors[7].yellows);Assert.AreEqual(0,c.match.substitutions[0]);for(int i=0;i<22;i++)Assert.AreEqual(i%11,c.match.actors[i].slot);}
        [Test] public void TacticalSubstitutionUsesMatchRulesAndForbidsReturn(){var sim=MatchSimulation.Create(db,c,"c1");c.match=sim.State;var old=c.TacticalEleven[9];var incoming=db.Squad(c.club).First(p=>!c.TacticalEleven.Contains(p.id));c.AssignTacticalPlayer(db,9,incoming.id);Assert.AreEqual(1,c.match.substitutions[0]);Assert.AreEqual(incoming.id,c.TacticalEleven[9]);Assert.Throws<InvalidOperationException>(()=>c.AssignTacticalPlayer(db,9,old));}
        [Test] public void ExcludedPositionCannotBeFilledOrSwapped(){var sim=MatchSimulation.Create(db,c,"c1");c.match=sim.State;c.match.actors[4].sentOff=true;var ids=c.TacticalEleven;Assert.Throws<InvalidOperationException>(()=>c.AssignTacticalPlayer(db,4,ids[7]));Assert.Throws<InvalidOperationException>(()=>c.AssignTacticalPlayer(db,7,ids[4]));CollectionAssert.AreEqual(ids,c.TacticalEleven);}
    }
}

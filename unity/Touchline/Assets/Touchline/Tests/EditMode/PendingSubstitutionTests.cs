using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Touchline.Core;

namespace Touchline.Tests
{
    public partial class ProfessionalTests
    {
        MatchSimulation LiveSubstitutionMatch(){var sim=MatchSimulation.Create(db,c,"c1");c.match=sim.State;c.match.phase="play";c.match.restart=0;c.match.clock=100;c.match.decision=100;c.match.ball.owner=c.match.actors[9].id;c.match.ball.position=c.match.actors[9].position;return sim;}
        string[] Bench()=>db.Squad(c.club).Where(p=>!c.match.used.Contains(p.id)).Select(p=>p.id).ToArray();
        [Test] public void PreparedSubstitutionWaitsForStoppageAndCanBeCancelled(){var sim=LiveSubstitutionMatch();var old=c.match.actors[9].id;var incoming=Bench()[0];c.AssignTacticalPlayer(db,9,incoming);sim.Advance(.1);Assert.AreEqual(old,c.match.actors[9].id);Assert.AreEqual(0,c.match.substitutions[0]);Assert.AreEqual(1,c.match.pendingSubstitutions.Count);sim.CancelSubstitution(old);c.match.restart=2;c.match.phase="throw-in";sim.Advance(.1);Assert.AreEqual(old,c.match.actors[9].id);Assert.IsFalse(c.match.used.Contains(incoming));}
        [Test] public void PreparedChangesShareOneWindowAtNextStoppage(){var sim=LiveSubstitutionMatch();var bench=Bench();for(int i=0;i<3;i++)c.AssignTacticalPlayer(db,7+i,bench[i]);c.match.restart=2;c.match.phase="throw-in";sim.Advance(.1);Assert.AreEqual(3,c.match.substitutions[0]);Assert.AreEqual(1,c.match.homeWindows.Count);Assert.AreEqual(0,c.match.pendingSubstitutions.Count);for(int i=0;i<3;i++)Assert.AreEqual(bench[i],c.match.actors[7+i].id);}
        [Test] public void PendingSubstitutionFollowsOutgoingPlayerAfterTacticalSwap(){var sim=LiveSubstitutionMatch();var incoming=Bench()[0];c.AssignTacticalPlayer(db,9,incoming);var old=c.match.actors[9].id;c.AssignTacticalPlayer(db,7,old);c.match.restart=2;c.match.phase="throw-in";sim.Advance(.1);Assert.AreEqual(incoming,c.match.actors[7].id);Assert.IsFalse(c.match.actors.Any(p=>p.id==old));}
        [Test] public void SameBenchPlayerCannotBePreparedTwiceAndQuotaIsReserved(){var sim=LiveSubstitutionMatch();var bench=Bench();c.AssignTacticalPlayer(db,9,bench[0]);Assert.Throws<InvalidOperationException>(()=>c.AssignTacticalPlayer(db,8,bench[0]));for(int i=1;i<5;i++)c.AssignTacticalPlayer(db,9-i,bench[i]);Assert.Throws<InvalidOperationException>(()=>c.AssignTacticalPlayer(db,4,bench[5]));c.AssignTacticalPlayer(db,9,bench[5]);Assert.AreEqual(5,c.match.pendingSubstitutions.Count);Assert.IsTrue(c.match.pendingSubstitutions.Any(p=>p.incoming==bench[5]));}
        [Test] public void HalfTimeAppliesPendingChangesWithoutWindow(){var sim=LiveSubstitutionMatch();c.AssignTacticalPlayer(db,9,Bench()[0]);c.match.clock=359.9f;sim.Advance(.1);Assert.IsTrue(c.match.halfTime);Assert.AreEqual(1,c.match.substitutions[0]);Assert.AreEqual(0,c.match.homeWindows.Count);}
        [Test] public void ExclusionCancelsOnlyItsPreparedChange(){var sim=LiveSubstitutionMatch();var bench=Bench();c.AssignTacticalPlayer(db,9,bench[0]);c.AssignTacticalPlayer(db,8,bench[1]);c.match.actors[9].sentOff=true;c.match.restart=2;c.match.phase="throw-in";sim.Advance(.1);Assert.AreEqual(1,c.match.substitutions[0]);Assert.AreEqual(bench[1],c.match.actors[8].id);Assert.IsFalse(c.match.used.Contains(bench[0]));Assert.AreEqual(1,c.match.events.Count(e=>e.kind=="substitution-cancelled"));}
        [Test] public void SaveRestoresPendingChangesAndSameContinuation(){var sim=LiveSubstitutionMatch();c.AssignTacticalPlayer(db,9,Bench()[0]);var copy=new MatchSimulation(db,JsonUtility.FromJson<MatchState>(JsonUtility.ToJson(c.match)));sim.State.restart=copy.State.restart=2;sim.State.phase=copy.State.phase="throw-in";sim.Advance(10);copy.Advance(10);Assert.AreEqual(JsonUtility.ToJson(sim.State),JsonUtility.ToJson(copy.State));}
        [Test] public void FinalWhistleClearsChangesThatCouldNotEnter(){var sim=LiveSubstitutionMatch();c.AssignTacticalPlayer(db,9,Bench()[0]);c.match.period=2;c.match.clock=719.9f;sim.Advance(.1);Assert.IsTrue(c.match.finished);Assert.AreEqual(0,c.match.pendingSubstitutions.Count);Assert.AreEqual(0,c.match.substitutions[0]);}
    }
}

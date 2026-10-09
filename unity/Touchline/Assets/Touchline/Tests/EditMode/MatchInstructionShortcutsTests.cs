using System;
using NUnit.Framework;
using UnityEngine;
using Touchline.Core;

namespace Touchline.Tests
{
    public class MatchInstructionShortcutsTests
    {
        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
        public void ShortcutChangesOnlyRequestedInstructionAndBindsReloadedMatch(int key)
        {
            var c=new Career();c.match=new MatchState{actors=new Actor[22],homeTactic=new Tactic()};
            var expected=JsonUtility.FromJson<Tactic>(JsonUtility.ToJson(c.tactic));
            switch((MatchInstruction)key){case MatchInstruction.Pressing:expected.pressing=.8f;break;case MatchInstruction.Line:expected.line=.8f;break;
                case MatchInstruction.Width:expected.width=.8f;break;case MatchInstruction.Tempo:expected.tempo=.8f;break;case MatchInstruction.Directness:expected.directness=.8f;break;}
            MatchInstructionShortcuts.Apply(c,(MatchInstruction)key,2);
            Assert.AreEqual(JsonUtility.ToJson(expected),JsonUtility.ToJson(c.tactic));
            Assert.IsTrue(ReferenceEquals(c.match.homeTactic,c.tactic));
        }
        [Test] public void InvalidChoiceDoesNotMutateCareer()
        {
            var c=new Career();string before=JsonUtility.ToJson(c);
            Assert.Throws<ArgumentOutOfRangeException>(()=>MatchInstructionShortcuts.Apply(c,MatchInstruction.Pressing,3));
            Assert.Throws<ArgumentOutOfRangeException>(()=>MatchInstructionShortcuts.Apply(c,(MatchInstruction)99,1));
            Assert.AreEqual(before,JsonUtility.ToJson(c));
        }
    }
}

using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Touchline.Core;

namespace Touchline.Tests
{
    public class PositionFitAliasTests
    {
        [TestCase("CDM", "DM")]
        [TestCase("DM", "CDM")]
        [TestCase("CAM", "AM")]
        [TestCase("AM", "CAM")]
        [TestCase("GB", "GK")]
        [TestCase("GK", "GB")]
        public void NativeRoleAliasesHaveFullFitWithoutRewritingData(string imported, string requested)
        {
            var positions = new[] { imported };
            var player = new PlayerData { position = "MIL", positions = positions };
            Assert.AreEqual(1f, player.Fit(requested));
            Assert.AreSame(positions, player.positions);
            Assert.AreEqual(imported, player.positions[0]);
            Assert.AreEqual("MIL", player.position);
        }

        [TestCase("CDM", "DM")]
        [TestCase("CAM", "AM")]
        [TestCase("GK", "GK")]
        [TestCase("GB", "GK")]
        [TestCase("LW", "LW")]
        public void PrimarySpecificPositionWorksWithoutAlternatePositionArray(string primary, string role)
        {
            var player = new PlayerData { position = primary };
            Assert.AreEqual(1f, player.Fit(role));
            Assert.IsNull(player.positions);
            Assert.AreEqual(primary, player.position);
        }

        [TestCase("GB")]
        [TestCase("GK")]
        public void KeeperAliasesKeepOutfieldPenalty(string primary)
        {
            var player = new PlayerData { position = primary };
            Assert.IsTrue(player.Goalkeeper);
            Assert.AreEqual(.25f, player.Fit("ST"));
            var outfield = new PlayerData { position = "ATT", positions = new[] { "ST" } };
            Assert.AreEqual(.25f, outfield.Fit(primary));
        }

        [TestCase("LB", "RB")]
        [TestCase("LW", "RW")]
        [TestCase("DM", "AM")]
        public void DistinctSidesAndMidfieldLinesStayDifferent(string position, string role)
        {
            Assert.AreEqual(.65f, new PlayerData { position = position, positions = new[] { position } }.Fit(role));
        }

        [Test] public void LegacyGroupFallbacksAndMissingRoleStayConservative()
        {
            Assert.AreEqual(.86f, new PlayerData { position = "MIL" }.Fit("CDM"));
            Assert.AreEqual(.86f, new PlayerData { position = "DEF" }.Fit("CB"));
            Assert.AreEqual(.86f, new PlayerData { position = "ATT" }.Fit("ST"));
            Assert.AreEqual(.65f, new PlayerData().Fit(null));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("unknown")]
        [TestCase("Tous")]
        [TestCase("Tous postes")]
        public void MissingOrSearchOnlyRoleDoesNotMeanFullFit(string role)
        {
            Assert.AreEqual(.65f, new PlayerData { position = "LW", positions = new[] { "LW", null } }.Fit(role));
            Assert.AreEqual(.65f, new PlayerData().Fit(role));
        }

        static Database Squad(bool importedAliases)
        {
            string[] roles = { "GK", "LB", "CB", "CB", "RB", "DM", "DM", "LW", "AM", "RW", "ST", "CM", "ST" };
            return new Database { players = Enumerable.Range(0, roles.Length * 2).Select(i => {
                var role = roles[i % roles.Length];
                if (importedAliases) role = role == "DM" ? "CDM" : role == "AM" ? "CAM" : role;
                return new PlayerData { id = "fit-" + i, name = "Player " + i, team = i < roles.Length ? "a" : "b", position = "MIL", positions = new[] { role }, rating = i % roles.Length == 11 ? 86 : 80, fitness = 100 };
            }).ToArray() };
        }

        static MatchSimulation Match(Database db)
        {
            var career = new Career { club = "a" };
            career.tactic.SetFormation("4-2-3-1");
            career.lineup = Career.Select(db, "a", career.tactic);
            return MatchSimulation.Create(db, career, "b", 1977, 2700);
        }

        [Test] public void ImportedSpecialistsKeepTheirPlacesAgainstHigherRatedGeneralist()
        {
            var db = Squad(true); var tactic = new Tactic(); tactic.SetFormation("4-2-3-1");
            var selected = Career.Select(db, "a", tactic);
            Assert.AreEqual("fit-5", selected[5]);
            Assert.AreEqual("fit-6", selected[6]);
            Assert.AreEqual("fit-8", selected[8]);
            CollectionAssert.AreEqual(Career.Select(Squad(false), "a", tactic), selected);
        }

        [Test] public void SamePlayerGetsSameEffectiveMatchSkillForImportedAlias()
        {
            var normal = Match(Squad(false)); var imported = Match(Squad(true));
            var skill = typeof(MatchSimulation).GetMethod("Skill", BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (int slot in new[] { 5, 6, 8 })
            {
                Assert.AreEqual(normal.State.actors[slot].id, imported.State.actors[slot].id);
                Assert.AreEqual(skill.Invoke(normal, new object[] { normal.State.actors[slot], "shortPassing" }), skill.Invoke(imported, new object[] { imported.State.actors[slot], "shortPassing" }));
            }
        }

        [Test] public void AliasSpellingDoesNotChangeTwoMinutesOfMatchDecisions()
        {
            var normal = Match(Squad(false)); var imported = Match(Squad(true));
            for (int step = 0; step < 120; step++)
            {
                normal.Advance(1); imported.Advance(1);
                Assert.AreEqual(normal.State.seed, imported.State.seed, "decision random stream at second " + step);
                Assert.AreEqual(normal.State.ball.owner, imported.State.ball.owner);
                Assert.AreEqual(normal.State.ball.position.x, imported.State.ball.position.x);
                Assert.AreEqual(normal.State.ball.position.z, imported.State.ball.position.z);
                for (int i = 0; i < 22; i++)
                {
                    Assert.AreEqual(normal.State.actors[i].id, imported.State.actors[i].id);
                    Assert.AreEqual(normal.State.actors[i].position.x, imported.State.actors[i].position.x);
                    Assert.AreEqual(normal.State.actors[i].position.z, imported.State.actors[i].position.z);
                    Assert.AreEqual(normal.State.actors[i].action, imported.State.actors[i].action);
                }
            }
            CollectionAssert.AreEqual(normal.State.events.Select(e => e.kind + ":" + e.player + ":" + e.time).ToArray(), imported.State.events.Select(e => e.kind + ":" + e.player + ":" + e.time).ToArray());
        }
    }
}

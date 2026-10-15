using System;
using NUnit.Framework;
using Touchline.Core;

namespace Touchline.Tests
{
    public class RealAcademyCatalogueTests
    {
        static PlayerData Youth()=>new PlayerData{id="source-1",name="Émile Test",team="academy-c",birthDate="2010-07-21",position="MIL",rating=45,potential=65,source="https://example.org/identity",rosterSource="https://example.org/roster",assessment="Estimated attributes; verified identity"};
        static Database World()=>new Database{players=Array.Empty<PlayerData>(),clubs=new[]{new ClubData{id="c"}}};
        [Test] public void SourcedYouthIsCopiedAgedAtCareerStartAndAppendedOnlyOnce()
        {
            var db=World();var player=Youth();var source=new Database{players=new[]{player}};
            Assert.AreEqual(1,RealAcademyCatalogue.Append(db,source));Assert.AreEqual(15,db.players[0].age);
            db.players[0].team="elsewhere";Assert.AreEqual(0,RealAcademyCatalogue.Append(db,source));
            Assert.AreEqual("elsewhere",db.players[0].team);Assert.AreEqual("academy-c",player.team);
        }
        [Test] public void ExistingIdentityWithAnotherIdOrUnaccentedNameIsNeverDuplicatedOrMoved()
        {
            var db=World();db.players=new[]{new PlayerData{id="original",name="Emile Test",team="loan-club"}};
            Assert.AreEqual(0,RealAcademyCatalogue.Append(db,new Database{players=new[]{Youth()}}));
            Assert.AreEqual("loan-club",db.players[0].team);
        }
        [TestCase("birth")] [TestCase("club")] [TestCase("source")] [TestCase("roster")] [TestCase("assessment")]
        public void IncompleteEvidenceCannotCreateAnAcademyPlayer(string missing)
        {
            var p=Youth();switch(missing){case "birth":p.birthDate=null;break;case "club":p.team="academy-unknown";break;case "source":p.source=null;break;case "roster":p.rosterSource=null;break;default:p.assessment=null;break;}
            var db=World();Assert.AreEqual(0,RealAcademyCatalogue.Append(db,new Database{players=new[]{p}}));Assert.AreEqual(0,db.players.Length);
        }
    }
}

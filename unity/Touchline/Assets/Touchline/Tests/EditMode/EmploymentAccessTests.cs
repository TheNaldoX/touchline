using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Touchline.Core;

namespace Touchline.Tests
{
    public partial class ProfessionalTests
    {
        void DismissForAccessTest()
        {
            c.world.managerStatus="dismissed";c.world.jobDay=c.life.day;c.life.nextFixture=int.MaxValue;
        }

        [Test] public void DismissedManagerCannotChooseFormerClubMedicalTreatment()
        {
            c.OpenInjury(db,c.lineup[9],"bruise");var injury=c.Injury(c.lineup[9]);DismissForAccessTest();
            string before=JsonUtility.ToJson(c);
            Assert.Throws<InvalidOperationException>(()=>c.Treat(db,injury.id,"conservative"));
            Assert.AreEqual(before,JsonUtility.ToJson(c));
        }

        [TestCase("ticket")][TestCase("debt")][TestCase("sponsor")][TestCase("transfer")]
        public void DismissedManagerCannotCommitFormerClubMoney(string action)
        {
            c.world.debt=100000;DismissForAccessTest();string before=JsonUtility.ToJson(c);
            Assert.Throws<InvalidOperationException>(()=>{switch(action){case "ticket":c.SetTicketPrice(30);break;case "debt":c.RepayDebt(1000);break;case "sponsor":c.NegotiateSponsor(0,10000,2);break;case "transfer":c.ProposeTransfer(db,"p24",120000,600,3,"starter");break;}});
            Assert.AreEqual(before,JsonUtility.ToJson(c));
        }

        [Test] public void DismissedManagerCannotChangeFormerClubLineupOrStaffAdvice()
        {
            c.tactic.pressing=.9f;foreach(var p in c.life.players)p.fitness=70;c.ApplyLife(db);DismissForAccessTest();
            string before=JsonUtility.ToJson(c);
            Assert.Throws<InvalidOperationException>(()=>c.AssignTacticalPlayer(db,9,c.lineup[8]));
            Assert.Throws<InvalidOperationException>(()=>c.ApplyStaffAdvice(db,"pressing"));
            Assert.AreEqual(before,JsonUtility.ToJson(c));
        }

        [Test] public void DismissedManagerCanContinueDespiteAnExistingSuspension()
        {
            DismissForAccessTest();int day=c.life.day;c.life.managerBanUntil=day+20;
            Assert.DoesNotThrow(()=>c.AdvanceDay(db));Assert.AreEqual(day+1,c.life.day);Assert.AreEqual("dismissed",c.world.managerStatus);
        }

        [TestCase(true)][TestCase(false)]
        public void DismissedManagerCanAnswerAnUnexpiredJobApproach(bool accept)
        {
            DismissForAccessTest();string oldClub=c.club;int day=c.life.day;
            var offer=new JobApproach{club="c1",until=day+14};c.approaches.Add(offer);
            Assert.DoesNotThrow(()=>c.AnswerApproach(db,"c1",accept));
            Assert.AreEqual(accept?"accepted":"declined",offer.status);Assert.AreEqual(accept?"c1":oldClub,c.club);
            Assert.AreEqual(accept?"employed":"dismissed",c.world.managerStatus);Assert.AreEqual(day,c.life.day);
            if(accept)Assert.DoesNotThrow(()=>c.SetDelegation("training",false));
        }

        [Test] public void DismissedManagerStillWaitsSevenDaysBeforeApplyingForAJob()
        {
            DismissForAccessTest();Assert.Throws<InvalidOperationException>(()=>c.TakeJob(db,"c1"));
            c.life.day+=7;Assert.DoesNotThrow(()=>c.TakeJob(db,"c1"));Assert.AreEqual("c1",c.club);Assert.AreEqual("employed",c.world.managerStatus);
        }

        [Test] public void JobApproachExceptionDoesNotBypassExpiryOrLiveMatchRestrictions()
        {
            DismissForAccessTest();c.approaches.Add(new JobApproach{club="c1",until=c.life.day-1});
            Assert.Throws<InvalidOperationException>(()=>c.AnswerApproach(db,"c1",true));
            c.approaches.Last().until=c.life.day+14;c.match=MatchSimulation.Create(db,c,"c1").State;
            Assert.Throws<InvalidOperationException>(()=>c.AnswerApproach(db,"c1",true));Assert.AreEqual("c0",c.club);
        }
    }
}

using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Touchline.Core;

namespace Touchline.Tests
{
    public partial class ProfessionalTests
    {
        [Test] public void IncomingAgreementIsActionableUntilSignedButItsInitialReceiptIsNot()
        {
            c.ProposeTransfer(db,"p24",120000,600,3,"starter");var receipt=c.life.messages.Last(m=>m.player=="p24"&&m.subject=="Proposition reçue");
            Days(2);var message=c.life.messages.Last(m=>m.player=="p24"&&m.subject=="Accord de principe");var offer=c.world.offers.Last(o=>o.player=="p24");
            Assert.IsNotNull(message.reference);Assert.AreSame(offer,c.PendingTransferAgreement(message));Assert.IsTrue(c.MessageNeedsDecision(message));Assert.IsFalse(c.MessageNeedsDecision(receipt));
            message.read=true;Assert.IsTrue(c.MessageNeedsDecision(message),"Reading an agreement does not sign it");
            c.SignTransfer(db,"p24");Assert.AreEqual("c0",db.Find("p24").team);Assert.IsFalse(c.MessageNeedsDecision(message));Assert.IsNull(c.PendingTransferAgreement(message));
        }
        [Test] public void OldIncomingAgreementDoesNotBecomeActionableForANewNegotiation()
        {
            c.ProposeTransfer(db,"p24",120000,600,3,"starter");Days(2);var old=c.life.messages.Last(m=>m.subject=="Accord de principe"&&m.player=="p24");c.world.offers.Last().status="expired";
            Days(1);c.ProposeTransfer(db,"p24",130000,650,3,"starter");Days(2);var current=c.life.messages.Last(m=>m.subject=="Accord de principe"&&m.player=="p24");
            Assert.AreNotEqual(old.reference,current.reference);Assert.IsFalse(c.MessageNeedsDecision(old));Assert.IsTrue(c.MessageNeedsDecision(current));
            old.reference=null;Assert.IsFalse(c.MessageNeedsDecision(old),"Old saved mail predating this agreement is not a new decision");
            current.reference=null;Assert.IsTrue(c.MessageNeedsDecision(current),"The current legacy agreement remains actionable");
        }
        [Test] public void AcceptedOutgoingLoanIsAnImportantDecisionAndSigningResolvesIt()
        {
            c.ProposeOutgoingLoan(db,"p1","c1",1000,new MarketTerms{loanWagePercent=60,loanEndDay=c.life.day+90,optionFee=120000});
            var receipt=c.life.messages.Last(m=>m.subject=="Prêt proposé");Days(2);var message=c.life.messages.Last(m=>m.subject=="Accord de prêt");var offer=c.outgoingLoans.Last();
            Assert.IsTrue(c.MessageNeedsDecision(message));Assert.AreEqual(3,c.MessagePriority(message));Assert.AreSame(offer,c.PendingOutgoingLoanAgreement(message));Assert.IsNull(c.PendingTransferAgreement(message));Assert.IsFalse(c.MessageNeedsDecision(receipt));
            message.read=true;Assert.IsTrue(c.MessageNeedsDecision(message));c.SignOutgoingLoan(db,"p1");
            Assert.AreEqual("c1",db.Find("p1").team);Assert.AreEqual(60,c.Contract(db,"p1").terms.loanWagePercent);Assert.IsFalse(c.MessageNeedsDecision(message));
        }
        [Test] public void ExpiredOrOtherClubsOutgoingLoanDoesNotProduceASignatureAction()
        {
            c.ProposeOutgoingLoan(db,"p1","c1",0,new MarketTerms{loanWagePercent=50,loanEndDay=c.life.day+90});Days(2);var message=c.life.messages.Last(m=>m.subject=="Accord de prêt");var offer=c.outgoingLoans.Last();
            string reference=message.reference;message.reference=null;Assert.IsTrue(c.MessageNeedsDecision(message),"Current old-format loan mail remains usable");message.reference=reference;
            offer.owner="c2";Assert.IsNull(c.PendingOutgoingLoanAgreement(message));offer.owner=c.club;c.life.day=offer.due+8;Assert.IsFalse(c.MessageNeedsDecision(message));
        }
        [Test] public void StaffAgreementHasItsOwnDecisionAndSurvivesSaveRoundTrip()
        {
            var member=c.Staff("scout");c.ProposeStaffContract(db,member.id,Career.MonthlySalary(member.wage)*2,2);var receipt=c.life.messages.Last(m=>m.action=="staff"&&m.subject=="Proposition reçue");
            Days(2);var message=c.life.messages.Last(m=>m.action=="staff"&&m.subject=="Accord de principe");var offer=c.staffOffers.Last();
            Assert.AreSame(offer,c.PendingStaffAgreement(message));Assert.IsTrue(c.MessageNeedsDecision(message));Assert.AreEqual(3,c.MessagePriority(message));Assert.IsFalse(c.MessageNeedsDecision(receipt));
            var restored=JsonUtility.FromJson<Career>(JsonUtility.ToJson(c));var restoredMessage=restored.life.messages.Single(m=>m.id==message.id);Assert.IsNotNull(restored.PendingStaffAgreement(restoredMessage));
            c.SignStaffContract(db,member.id);Assert.AreEqual("signed",offer.status);Assert.IsFalse(c.MessageNeedsDecision(message));
        }
        [Test] public void LegacyStaffAgreementMatchesTheAgentAndCannotReactivateOtherStaffMail()
        {
            var member=c.Staff("scout");c.ProposeStaffContract(db,member.id,Career.MonthlySalary(member.wage)*2,2);Days(2);var message=c.life.messages.Last(m=>m.action=="staff"&&m.subject=="Accord de principe");message.reference=null;
            Assert.IsTrue(c.MessageNeedsDecision(message));var other=c.Mail("Agent · Un autre recruteur","Accord de principe","Un autre dossier.",null,"staff");Assert.IsFalse(c.MessageNeedsDecision(other));
            message.day=c.staffOffers.Last().due-1;Assert.IsFalse(c.MessageNeedsDecision(message));message.day=c.staffOffers.Last().due;c.world.managerStatus="dismissed";Assert.IsFalse(c.MessageNeedsDecision(message));
        }
    }
}

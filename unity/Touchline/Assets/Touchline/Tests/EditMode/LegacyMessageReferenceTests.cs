using NUnit.Framework;
using UnityEngine;
using Touchline.Core;

namespace Touchline.Tests
{
    public class LegacyMessageReferenceTests
    {
        [TestCase("incoming")] [TestCase("outgoing")] [TestCase("staff")] [TestCase("medical")]
        [TestCase("facilities")] [TestCase("integrity")] [TestCase("commercial")] [TestCase("jobs")]
        public void EmptyLegacyReferenceSurvivesSaveButExplicitOtherReferenceNeverMatches(string kind)
        {
            foreach(string reference in new string[]{null,""}){
                var c=new Career{club="a",world=new CareerWorld(),life=new ClubLife{day=3,cash=1000000}};
                c.life.players.Add(new PlayerLife{id="p"});
                var m=new ClubMessage{id=1,day=3,player="p",reference=reference};c.life.messages.Add(m);
                switch(kind){
                    case "incoming":m.action="transfer";m.subject="Accord de principe";c.world.offers.Add(new TransferOffer{player="p",destination="a",status="accepted",due=3});break;
                    case "outgoing":m.action="transfer";m.subject="Accord de prêt";c.outgoingLoans.Add(new OutgoingLoanOffer{player="p",owner="a",borrower="b",status="accepted",due=3});break;
                    case "staff":m.action="staff";m.subject="Accord de principe";m.sender="Agent · Alex";c.staffMarket.Add(new StaffMember{id="s",name="Alex"});c.staffOffers.Add(new StaffOffer{staff="s",club="a",status="accepted",due=3});break;
                    case "medical":m.action="medical";m.subject="Joueur • blessure";c.life.medical.Add(new MedicalCase{id=1,player="p",opened=3,remaining=5});break;
                    case "facilities":m.action="facilities";m.subject="Projet soutenu";c.life.projects.Add(new FacilityProject{kind="training",status="approved",requested=0});break;
                    case "integrity":m.action="integrity";m.subject="Une alerte interne vous concerne";c.life.investigations.Add(new IntegrityCase{kind="leak",opened=0,due=20,alerted=true});break;
                    case "commercial":m.action="finance";m.subject="Proposition acceptée";m.text="Horizon propose un contrat.";c.world.sponsors.Add(new CommercialDeal{name="Horizon",slot="kit",status="accepted",asking=1000,counterDay=6});break;
                    case "jobs":m.action="jobs";m.sender="Agent • carrière";m.subject="Approche de Club B";c.approaches.Add(new JobApproach{club="b",offered=3,until=17});break;
                }
                Assert.IsTrue(c.MessageNeedsDecision(m),"Legacy fixture must be actionable before saving.");
                var restored=JsonUtility.FromJson<Career>(JsonUtility.ToJson(c));restored.BindMatchTactic();var mail=restored.life.messages[0];
                string before=JsonUtility.ToJson(restored);
                Assert.IsTrue(restored.MessageNeedsDecision(mail),"Active legacy decision disappeared after save.");
                Assert.AreEqual(before,JsonUtility.ToJson(restored),"Reading a legacy decision mutates its state.");
                mail.reference="unrelated/explicit/dossier";Assert.IsFalse(restored.MessageNeedsDecision(mail),"An explicit other dossier must not use legacy matching.");
                if(kind=="integrity"){
                    mail.reference="";restored.RespondIntegrity(0,"ignore");
                    Assert.IsFalse(restored.MessageNeedsDecision(mail));Assert.AreEqual("ignore",restored.life.investigations[0].response);
                }
            }
        }
    }
}

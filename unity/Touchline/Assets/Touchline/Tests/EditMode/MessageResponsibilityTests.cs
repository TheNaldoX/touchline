using NUnit.Framework;
using Touchline.Core;
namespace Touchline.Tests {
 public class MessageResponsibilityTests {
  Career c;ClubMessage medical,facility,integrity;
  [SetUp] public void Setup(){
   c=new Career{world=new CareerWorld{managerStatus="employed"},life=new ClubLife{day=8}};
   c.life.players.Add(new PlayerLife{id="p"});
   c.life.medical.Add(new MedicalCase{id=4,player="p",opened=2,treatment="pending",remaining=7,total=7});
   c.life.projects.Add(new FacilityProject{kind="academy",level=2,requested=3,status="approved"});
   c.life.investigations.Add(new IntegrityCase{kind="intermediary",opened=4,status="pending",alerted=true});
   medical=new ClubMessage{action="medical",player="p",reference="medical/4"};
   facility=new ClubMessage{action="facilities",reference="facility/academy/3/2"};
   integrity=new ClubMessage{action="integrity",reference="integrity/intermediary/4/"};
  }
  [Test] public void EmployedManagerStillOwnsOpenDossiers(){foreach(var m in new[]{medical,facility,integrity})Assert.IsTrue(c.MessageNeedsDecision(m),m.action);}
  [Test] public void DismissedManagerDoesNotReceiveDecisionsFromFormerClub(){c.world.managerStatus="dismissed";foreach(var m in new[]{medical,facility,integrity})Assert.IsFalse(c.MessageNeedsDecision(m),m.action);Assert.AreEqual("pending",c.life.medical[0].treatment);Assert.AreEqual("approved",c.life.projects[0].status);Assert.IsNull(c.life.investigations[0].response);}
  [Test] public void SuspendedManagerDoesNotDiscardTheClubsOpenDossiers(){c.life.managerBanUntil=30;foreach(var m in new[]{medical,facility,integrity})Assert.IsTrue(c.MessageNeedsDecision(m),m.action);}
  [Test] public void NullMessageIsNotActionable(){Assert.IsFalse(c.MessageNeedsDecision(null));}
 }
}

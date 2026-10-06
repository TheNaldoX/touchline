using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Touchline.Core;

namespace Touchline.Tests
{
    public partial class ProfessionalTests
    {
        [Test] public void ReadMedicalMessageRetainsDecisionUntilTreatment()
        {
            var injury=c.OpenInjury(db,"p9","bruise");var message=c.life.messages.Last();message.read=true;
            Assert.IsTrue(c.MessageNeedsDecision(message));Assert.AreEqual(3,c.MessagePriority(message));
            c.Treat(db,injury.id,"conservative");Assert.IsFalse(c.MessageNeedsDecision(message));Assert.AreEqual(1,c.MessagePriority(message));
        }
        [Test] public void ANewInjuryDoesNotReopenThePreviousMedicalNotification()
        {
            var first=c.OpenInjury(db,"p9","bruise");var old=c.life.messages.Last();first.closed=c.life.day;
            var next=c.OpenInjury(db,"p9","muscle");var current=c.life.messages.Last();
            Assert.IsFalse(c.MessageNeedsDecision(old));Assert.IsTrue(c.MessageNeedsDecision(current));
            var saved=JsonUtility.FromJson<Career>(JsonUtility.ToJson(c));
            Assert.AreEqual("medical/"+next.id,saved.life.messages.Last().reference);Assert.IsFalse(saved.MessageNeedsDecision(saved.life.messages.First(m=>m.id==old.id)));
        }
        [Test] public void AnApprovedProjectDoesNotReopenOtherFacilityMessages()
        {
            var old=c.Mail("Présidence","Projet soutenu","Ancien projet",null,"facilities","facility/training/0/2");
            c.life.projects.Add(new FacilityProject{kind="training",level=2,requested=0,status="complete"});
            c.life.projects.Add(new FacilityProject{kind="academy",level=2,requested=10,status="approved"});
            c.life.day=13;var current=c.Mail("Présidence","Projet soutenu","Projet actuel",null,"facilities","facility/academy/10/2");
            var routine=c.Mail("Direction","Travaux livrés","Autre chantier",null,"facilities");
            Assert.IsFalse(c.MessageNeedsDecision(old));Assert.IsFalse(c.MessageNeedsDecision(routine));Assert.IsTrue(c.MessageNeedsDecision(current));
            c.life.projects.Last().status="building";Assert.IsFalse(c.MessageNeedsDecision(current));
        }
        [Test] public void IntegrityDecisionBelongsToTheAlertInsteadOfEveryIntegrityMessage()
        {
            c.life.investigations.Add(new IntegrityCase{kind="leak",opened=0,alerted=true});c.life.day=3;
            var routine=c.Mail("Référent","Contrôle indépendant financé","Bilan",null,"integrity");
            var alert=c.Mail("Référent","Une alerte interne vous concerne","Dossier",null,"integrity","integrity/leak/0/");
            Assert.IsFalse(c.MessageNeedsDecision(routine));Assert.IsTrue(c.MessageNeedsDecision(alert));
            c.life.investigations[0].response="cooperate";Assert.IsFalse(c.MessageNeedsDecision(alert));
        }
        [Test] public void FinancialCrisisAndPinnedMessageOutrankNewRoutineMail()
        {
            c.life.cash=-1;var finance=c.Mail("Direction financière","Trésorerie","Découvert");var ordinary=c.Mail("Staff","Routine","Charge");
            Assert.Greater(c.MessagePriority(finance),c.MessagePriority(ordinary));ordinary.pinned=true;Assert.AreEqual(3,c.MessagePriority(ordinary));ordinary.pinned=false;Assert.AreEqual(0,c.MessagePriority(ordinary));
        }
        [Test] public void AgreedRecoveryBlocksSelectionAndEndsExactlyOnce()
        {
            var p=c.Person("p9");p.fitness=65;c.Talk(db,p.id,"recovery",false);Assert.IsFalse(c.Available(p.id));Assert.AreEqual(3,p.restUntil);
            Days(2);Assert.IsFalse(c.Available(p.id));Days(1);Assert.IsTrue(c.Available(p.id));Assert.AreEqual(-1,p.restUntil);
            Assert.AreEqual(1,c.life.messages.Count(m=>m.subject=="Fin du repos convenu"));Days(1);Assert.AreEqual(1,c.life.messages.Count(m=>m.subject=="Fin du repos convenu"));
        }
        [Test] public void NewAndOldConversationTopicsShareCooldown()
        {
            c.Talk(db,"p9","development",false);float trust=c.Person("p9").trust;int messages=c.life.messages.Count;
            Assert.Throws<InvalidOperationException>(()=>c.Talk(db,"p9","support",true));Assert.Throws<InvalidOperationException>(()=>c.Talk(db,"p9","role",false));
            Assert.AreEqual(messages,c.life.messages.Count);Assert.AreEqual(trust,c.Person("p9").trust);
        }
        [Test] public void PromiseReviewCannotRenewDeadlineOrResetAppearances()
        {
            var p=c.Person("p9");p.promiseUntil=21;p.promiseStarts=4;p.appearances=5;c.Talk(db,p.id,"promiseReview",false);
            Assert.AreEqual(21,p.promiseUntil);Assert.AreEqual(4,p.promiseStarts);Assert.AreEqual(5,p.appearances);StringAssert.Contains("1 apparition",c.life.messages.Last().text);
        }
        [Test] public void HighRoleLowMinutesNeedsActualSelectionForAmbitiousPlayer()
        {
            string id=c.life.players.First(p=>new[]{"Ambitieux","Exigeant"}.Contains(c.CareerPersonality(p.id))).id;
            c.Contract(db,id).role="key";c.life.matches=10;c.Person(id).appearances=0;float before=c.Person(id).trust;c.Talk(db,id,"role",false);
            Assert.AreEqual(before-1,c.Person(id).trust);Assert.AreEqual("key",c.Contract(db,id).role);
        }
        [Test] public void HealthySupportHasLimitedBenefitAndCannotFarmDaily()
        {
            var p=c.Person("p9");p.morale=90;p.trust=80;c.Talk(db,p.id,"support",false);Assert.AreEqual(90.5f,p.morale);Assert.AreEqual(80.5f,p.trust);Assert.Throws<InvalidOperationException>(()=>c.Talk(db,p.id,"support",false));
        }
        [Test] public void CommunicationStateSurvivesSaveAndPlayerThreadGroupsStaffReplies()
        {
            c.Person("p9").fitness=65;c.Talk(db,"p9","recovery",true);var message=c.life.messages.Last();message.pinned=true;
            var other=c.Mail("Préparateur","Suivi","Évolution","p9","medical");Assert.AreEqual(c.MessageThreadKey(message),c.MessageThreadKey(other));
            var saved=JsonUtility.FromJson<Career>(JsonUtility.ToJson(c));Assert.AreEqual(3,saved.Person("p9").restUntil);Assert.IsTrue(saved.life.messages.First(m=>m.id==message.id).pinned);
        }
        [Test] public void MessageSerialDetectsNewMailEvenWhenHistoryCapRemovesOlderMessages()
        {
            for(int i=0;i<300;i++)c.Mail("Staff","Routine","Message");int serial=c.life.serial;c.Mail("Médecin","Urgence","Nouveau","p9","medical");Assert.AreEqual(300,c.life.messages.Count);Assert.AreEqual(1,c.life.messages.Count(m=>m.id>serial));
        }
        [Test] public void DevelopmentAdviceUsesActualMinutesContractAndStaffInsteadOfHiddenPotential()
        {
            db.Find("p9").age=20;c.life.matches=12;c.Person("p9").appearances=0;c.Contract(db,"p9").role="rotation";c.Staff("youth").wage=100;c.Staff("youth").coaching=16;
            string advice=c.PlayerDevelopmentAdvice(db,"p9");StringAssert.Contains("prêt",advice);db.Find("p9").potential=99;Assert.AreEqual(advice,c.PlayerDevelopmentAdvice(db,"p9"));
            c.Contract(db,"p9").role="key";StringAssert.Contains("statut contractuel",c.PlayerDevelopmentAdvice(db,"p9"));c.Staff("youth").coaching=6;StringAssert.Contains("trop limité",c.PlayerDevelopmentAdvice(db,"p9"));
        }
        [Test] public void DevelopmentConversationProducesActionableContextWithoutAutomaticImprovement()
        {
            var player=db.Find("p9");player.age=20;float rating=player.rating,potential=player.potential,growth=c.Person(player.id).growth;c.Talk(db,player.id,"development",false);
            StringAssert.Contains("Parcours à concrétiser",c.PlayerSituation(db,player.id));Assert.AreEqual(rating,player.rating);Assert.AreEqual(potential,player.potential);Assert.AreEqual(growth,c.Person(player.id).growth);
            c.OpenInjury(db,player.id,"bruise");StringAssert.StartsWith("Priorité au dossier médical",c.PlayerDevelopmentAdvice(db,player.id));
        }
        [Test] public void ReadPendingMedicalAndPinnedMailSurviveRoutineInboxChurn()
        {
            c.OpenInjury(db,"p9","bruise");var medical=c.life.messages.Last();medical.read=true;var pin=c.Mail("Staff","À conserver","Dossier");pin.read=true;pin.pinned=true;
            for(int i=0;i<400;i++)c.Mail("Vous","Routine","Compte rendu");
            Assert.AreEqual(300,c.life.messages.Count);Assert.IsTrue(c.life.messages.Any(m=>m.id==medical.id));Assert.IsTrue(c.life.messages.Any(m=>m.id==pin.id));Assert.IsTrue(c.MessageNeedsDecision(medical));
        }
    }
}

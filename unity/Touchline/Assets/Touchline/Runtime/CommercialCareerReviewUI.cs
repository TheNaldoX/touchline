using System;
using System.Linq;
using Touchline.Core;
using UnityEngine.UIElements;
namespace Touchline {
 public sealed partial class TouchlineApp {
  void CommercialAgreementReview(ClubMessage message){
   var deal=Career.PendingCommercialAgreement(message);if(deal==null){Message("Cette proposition commerciale n’est plus à confirmer.");return;}
   var panel=AgreementPanel("Partenariat · "+deal.name,"commercial-agreement-review");var body=Scroll(panel);
   Text(body,deal.status=="counter"?"CONTRE-PROPOSITION À ACCEPTER":"CONDITIONS ACCEPTÉES","eyebrow");
   ProfileFact(body,"Emplacement",deal.slot);ProfileFact(body,"Recette annuelle",Money(deal.asking));ProfileFact(body,"Durée",deal.years+" an(s)");
   Text(body,"Partenaire fictif de la carrière. La signature engage les conditions présentées et les versements hebdomadaires ; elle ne remplace pas un autre contrat encore actif. Cette proposition n’a pas de date limite imposée.","notice");
   var actions=Row(panel,"inbox-thread-actions");actions.AddToClassList("agreement-actions");
   PlayerManagementButton(actions,"Signer le partenariat",()=>RunDecision(()=>{if(!ReferenceEquals(Career.PendingCommercialAgreement(message),deal))throw new InvalidOperationException("Les conditions ou l’emplacement ont changé : relisez la proposition actuelle.");Career.SignSponsor(Career.world.sponsors.IndexOf(deal));})).name="agreement-sign-commercial";
   Button(actions,"Finances",()=>Navigate("Finances"));Button(actions,"Retour au message",()=>OpenMessageThread(message.id));
  }
  bool CareerApproachActionsAvailable=>(Career.match==null||Career.match.finished)&&Career.life.managerBanUntil<=Career.life.day;
  void JobApproachReview(ClubMessage message){
   var offer=Career.PendingJobApproach(message);if(offer==null){Message("Cette approche n’est plus ouverte.");return;}
   var panel=AgreementPanel("Approche · "+ClubName(offer.club),"job-approach-review");var body=Scroll(panel);
   Text(body,offer.reason);ProfileFact(body,"Philosophie recherchée",offer.style??"Non précisée");ProfileFact(body,"Compatibilité",offer.fit.ToString("0")+" %");ProfileFact(body,"Salaire mensuel proposé",Money(Core.Career.MonthlySalary(offer.weeklySalary)));ProfileFact(body,"Réponse au plus tard",AgreementDate(offer.until));
   Text(body,"Accepter vous fait quitter votre poste et reprendre les moyens du nouveau club à la même date. Décliner ferme seulement cette approche. La lecture ne constitue pas une réponse.","notice");
   var actions=Row(panel,"inbox-thread-actions");actions.AddToClassList("agreement-actions");
   var accept=Button(actions,"Accepter le poste",()=>Confirm("Rejoindre "+ClubName(offer.club)+" ?","Confirmer votre changement de club ?",()=>RunDecision(()=>{if(!ReferenceEquals(Career.PendingJobApproach(message),offer))throw new InvalidOperationException("Cette approche n’est plus valable.");Career.AnswerApproach(Database,offer.club,true);})));accept.name="agreement-accept-job";accept.SetEnabled(CareerApproachActionsAvailable);
   var decline=Button(actions,"Décliner",()=>RunDecision(()=>{if(!ReferenceEquals(Career.PendingJobApproach(message),offer))throw new InvalidOperationException("Cette approche n’est plus valable.");Career.AnswerApproach(Database,offer.club,false);}));decline.name="agreement-decline-job";decline.SetEnabled(CareerApproachActionsAvailable);
   if(!CareerApproachActionsAvailable)Text(body,"La réponse sera disponible après le match ou votre retour de suspension.","muted");
   Button(actions,"Retour au message",()=>OpenMessageThread(message.id));
  }
 }
}

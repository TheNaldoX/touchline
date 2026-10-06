using System;
using System.Linq;
using Touchline.Core;
using UnityEngine.UIElements;

namespace Touchline
{
    public sealed partial class TouchlineApp
    {
        string AgreementDate(int day)=>Core.Career.Epoch.AddDays(day).ToString("dd MMM yyyy",French);
        static string AgreementRole(string role)=>PlayingTimeRoles.Label(role);
        VisualElement AgreementPanel(string title,string name)
        {
            var panel=Modal(title);panel.name=name;panel.AddToClassList("agreement-review");panel.Children().First().AddToClassList("agreement-heading");return panel;
        }
        void OpenRecruitmentAgreements(){recruitmentTab="Négociations et prêts";Navigate("Recrutement");}
        void AgreementLoanFacts(VisualElement body,MarketTerms terms,int end)
        {
            ProfileFact(body,"Fin du prêt",AgreementDate(end));ProfileFact(body,"Part salariale du club d’accueil",(terms?.loanWagePercent??50)+" %");
            ProfileFact(body,"Option d’achat",terms?.optionFee>0?Money(terms.optionFee):"Aucune");
            ProfileFact(body,"Obligation d’achat",terms?.obligationFee>0?Money(terms.obligationFee)+(terms.obligationAppearances>0?" après "+terms.obligationAppearances+" apparitions":" · automatique à la fin du prêt"):"Aucune");
            ProfileFact(body,"Clause de rappel",terms?.recall==true?"Pendant le mercato":"Aucune");
        }
        void TransferAgreementReview(TransferOffer offer)
        {
            var player=Database.Find(offer.player);if(player==null){Message("Le joueur n’est plus disponible.");return;}
            bool retired=player.team=="retired"||Career.PlayerRetirementEffective(player.id);
            var panel=AgreementPanel("Accord · "+player.name,"transfer-agreement-review");var body=Scroll(panel);
            Text(body,offer.renewal?"PROLONGATION":offer.precontract?"PRÉCONTRAT":offer.loan?"PRÊT ENTRANT":"TRANSFERT","eyebrow");
            Text(body,retired?"Retraite effective · conditions conservées pour l’historique. Aucun contrat de joueur ne peut être signé.":"Conditions acceptées par l’agent. La signature reste votre décision.","notice");
            ProfileFact(body,"Club actuel",ClubName(player.team));ProfileFact(body,"Indemnité",Money(offer.fee));
            ProfileFact(body,"Salaire mensuel intégral",Money(Core.Career.MonthlySalary(offer.wage)));ProfileFact(body,"Temps de jeu promis",AgreementRole(offer.role));Text(body,PlayingTimeRoles.Description(offer.role),"notice").name="agreement-playing-time-description";
            ProfileFact(body,"Durée du contrat",offer.years+" an(s)");ProfileFact(body,"Prime de présence",Money(offer.bonus));ProfileFact(body,"Clause libératoire",offer.clause>0?Money(offer.clause):"Aucune");
            if(offer.loan){long share=offer.wage*(offer.terms?.loanWagePercent??100)/100;ProfileFact(body,"Votre prise en charge mensuelle",Money(Core.Career.MonthlySalary(share)));ProfileFact(body,"Part conservée par le club prêteur",Money(Core.Career.MonthlySalary(offer.wage-share)));AgreementLoanFacts(body,offer.terms??new MarketTerms(),offer.terms?.loanEndDay??Career.life.day+365);}
            if(offer.precontract)ProfileFact(body,"Arrivée prévue",AgreementDate(offer.joinDay));
            if(!offer.loan&&!offer.precontract&&offer.terms?.upfrontPercent<100)ProfileFact(body,"Échelonnement",offer.terms.upfrontPercent+" % immédiatement, puis "+offer.terms.instalments+" échéances trimestrielles");
            ProfileFact(body,"Prime d’agent à la signature",Money(offer.wage*2));ProfileFact(body,"Confirmer au plus tard",AgreementDate(offer.due+7));
            Text(body,"La signature vérifie à nouveau les moyens du club, la situation du joueur et le mercato. Un accord arrivé à échéance ne peut pas être signé.","footnote");
            var actions=Row(panel,"inbox-thread-actions");actions.AddToClassList("agreement-actions");var sign=PlayerManagementButton(actions,"Signer l’accord",()=>RunDecision(()=>{
                if(offer.status!="accepted"||offer.due+7<Career.life.day||(offer.destination!=null&&offer.destination!=Career.club)||!Career.world.offers.Contains(offer))throw new InvalidOperationException("Cet accord n’est plus valable.");
                Career.SignTransfer(Database,offer.player);
            }));sign.name="agreement-sign-transfer";sign.AddToClassList("primary");sign.SetEnabled(PlayerManagementAvailable&&!retired);if(retired)sign.tooltip="Retraite effective : signature impossible.";Button(actions,"Tous les dossiers",OpenRecruitmentAgreements);
        }
        void OutgoingLoanAgreementReview(OutgoingLoanOffer offer)
        {
            var player=Database.Find(offer.player);if(player==null){Message("Le joueur n’est plus disponible.");return;}
            bool retired=player.team=="retired"||Career.PlayerRetirementEffective(player.id);
            var panel=AgreementPanel("Prêt sortant · "+player.name,"outgoing-loan-agreement-review");var body=Scroll(panel);
            if(retired)Text(body,"Retraite effective · conditions conservées pour l’historique. Aucun prêt de joueur ne peut être signé.","notice");
            Text(body,"ACCORD DU JOUEUR ET DU CLUB","eyebrow");ProfileFact(body,"Club d’accueil",ClubName(offer.borrower));ProfileFact(body,"Indemnité reçue",Money(offer.fee));
            ProfileFact(body,"Salaire mensuel intégral",Money(Core.Career.MonthlySalary(player.wage)));
            ProfileFact(body,"Part mensuelle restant à votre club",Money(Core.Career.MonthlySalary(player.wage-player.wage*(offer.terms?.loanWagePercent??100)/100)));
            AgreementLoanFacts(body,offer.terms??new MarketTerms(),offer.terms?.loanEndDay??Career.life.day+365);ProfileFact(body,"Confirmer au plus tard",AgreementDate(offer.due+7));
            Text(body,"La signature confirme le prêt et sa répartition salariale. Le club doit conserver un effectif suffisant et le mercato doit être ouvert.","footnote");
            var actions=Row(panel,"inbox-thread-actions");actions.AddToClassList("agreement-actions");var sign=PlayerManagementButton(actions,"Signer le prêt sortant",()=>RunDecision(()=>{
                if(offer.status!="accepted"||offer.owner!=Career.club||offer.due+7<Career.life.day||!Career.outgoingLoans.Contains(offer))throw new InvalidOperationException("Cet accord de prêt n’est plus valable.");
                Career.SignOutgoingLoan(Database,offer.player);
            }));sign.name="agreement-sign-outgoing";sign.AddToClassList("primary");sign.SetEnabled(PlayerManagementAvailable&&!retired);if(retired)sign.tooltip="Retraite effective : signature impossible.";Button(actions,"Tous les dossiers",OpenRecruitmentAgreements);
        }
        void StaffAgreementReview(StaffOffer offer)
        {
            var member=Career.staffMarket.FirstOrDefault(s=>s.id==offer.staff);if(member==null){Message("Ce membre du staff n’est plus disponible.");return;}
            var panel=AgreementPanel("Contrat staff · "+member.name,"staff-agreement-review");var body=Scroll(panel);
            Text(body,"ACCORD DE PRINCIPE","eyebrow");ProfileFact(body,"Fonction",StaffRole(member.role));ProfileFact(body,"Employeur actuel",member.club==null?"Libre":ClubName(member.club));
            ProfileFact(body,"Salaire mensuel",Money(Core.Career.MonthlySalary(offer.wage)));ProfileFact(body,"Durée",offer.years+" an(s)");ProfileFact(body,"Indemnité de changement de club",Money(offer.compensation));ProfileFact(body,"Prime d’agent à la signature",Money(offer.wage*2));ProfileFact(body,"Confirmer au plus tard",AgreementDate(offer.due+7));
            bool occupied=Career.life.staff.members.Any(s=>s.role==member.role&&s.id!=member.id);
            if(occupied)Text(body,"Ce poste est occupé. Vous devez d’abord organiser le départ du responsable actuel ; son indemnité de rupture reste distincte.","notice");
            Text(body,"La signature vérifie le poste, l’employeur actuel, la trésorerie et le plafond salarial du staff.","footnote");
            var actions=Row(panel,"inbox-thread-actions");actions.AddToClassList("agreement-actions");var sign=PlayerManagementButton(actions,"Signer le contrat staff",()=>RunDecision(()=>{
                if(offer.status!="accepted"||offer.club!=Career.club||offer.due+7<Career.life.day||!Career.staffOffers.Contains(offer))throw new InvalidOperationException("Cet accord du staff n’est plus valable.");
                Career.SignStaffContract(Database,offer.staff);
            }));sign.name="agreement-sign-staff";sign.AddToClassList("primary");sign.SetEnabled(PlayerManagementAvailable&&!occupied);if(occupied)sign.tooltip="Libérez d’abord le poste dans Staff et délégation.";
            Button(actions,"Gérer le staff",()=>Navigate("Staff et délégation"));
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

namespace Touchline.Core
{
    [Serializable] public class MarketTerms
    {
        public int loanWagePercent=100,loanEndDay,upfrontPercent=100,instalments,obligationAppearances;
        public long optionFee,obligationFee,goalBonus;
        public float sellOnPercent,annualRise,promotionRise,relegationCut;
        public bool recall;
    }
    [Serializable] public class PaymentDue { public string player,label,club,counterparty;public int due;public long amount;public bool settled,legacyUnpaired; }
    public partial class Career
    {
        public List<PaymentDue> payments=new List<PaymentDue>();
        public List<string> shortlist=new List<string>();
        public string MarketPhase=>WindowOpen?Date.Month==1?Date.Day>=25?"Dernière semaine du mercato d’hiver":"Mercato d’hiver":Date.Month==9||Date.Month==8&&Date.Day>=25?"Dernière ligne droite estivale":Date.Month==6?"Ouverture du mercato estival":"Mercato estival":Date.Month==5||Date.Month==6?"Préparation du marché":"Hors mercato · observation et prolongations";
        public long CommittedPurchases=>world==null?0:world.contracts.Where(c=>c.club==club&&c.IsLoan&&c.terms?.obligationFee>0).Sum(c=>c.terms.obligationFee)+payments.Where(p=>!p.settled&&p.club==club).Sum(p=>p.amount);
        public long Payroll(Database db)
        {
            // Only this club's incoming/outgoing loans are mapped. UI queries
            // do not rebuild the complete world's aggregated payroll map.
            var loans=new Dictionary<string,Employment>();
            foreach(var c in world?.contracts??Enumerable.Empty<Employment>())if(c.IsLoan&&c.player!=null&&(c.club==club||c.parent==club))loans[c.player]=c;
            var knownClubs=new HashSet<string>(db.clubs.Select(t=>t.id));long total=0;
            foreach(var p in db.Squad(club))total+=loans.TryGetValue(p.id,out var c)&&c.club==p.team&&c.parent!=p.team&&knownClubs.Contains(c.parent)?BorrowedLoanWage(p,c):p.wage;
            foreach(var c in loans.Values)if(c.parent==club&&c.club!=club&&knownClubs.Contains(c.club)){
                var p=db.Find(c.player);if(p!=null&&p.team==c.club)total+=p.wage-BorrowedLoanWage(p,c);
            }
            return total;
        }
        public void SetOfferTerms(string id,MarketTerms terms)
        {
            OffPitch();var offer=world.offers.LastOrDefault(o=>o.player==id&&OfferForManagedClub(o)&&o.status=="pending");if(offer==null)throw new InvalidOperationException("Aucune proposition en cours pour votre club.");
            ValidateTerms(terms,offer.loan);if(offer.loan&&terms.loanEndDay>(world.contracts.FirstOrDefault(c=>c.player==id)?.until??0))throw new InvalidOperationException("Le prêt dépasse l’échéance connue du contrat parent.");if(terms.obligationFee>TransferBudget-offer.fee)throw new InvalidOperationException("L’obligation d’achat excède le budget restant à engager.");offer.terms=terms;
        }
        void ValidateTerms(MarketTerms t,bool loan)
        {
            if(t==null||t.loanWagePercent<0||t.loanWagePercent>100||t.optionFee<0||t.obligationFee<0||t.optionFee>0&&t.obligationFee>0||t.obligationAppearances<0||t.upfrontPercent<25||t.upfrontPercent>100||t.instalments<0||t.instalments>4||t.upfrontPercent<100&&t.instalments==0||t.upfrontPercent==100&&t.instalments!=0||t.sellOnPercent<0||t.sellOnPercent>30||t.annualRise<0||t.annualRise>15||t.promotionRise<0||t.promotionRise>30||t.relegationCut<0||t.relegationCut>35||t.goalBonus<0||t.recall&&t.obligationFee>0)throw new ArgumentException("Clauses invalides.");
            if(loan&&(t.loanEndDay<life.day+28||t.loanEndDay>life.day+365||t.instalments>0))throw new ArgumentException("Un prêt dure de 28 jours à un an, sans échéancier d’indemnité.");
        }
        public void ToggleShortlist(string id){if(shortlist.Contains(id))shortlist.Remove(id);else shortlist.Add(id);}
        public void ExerciseLoanOption(Database db,string id)
        {
            OffPitch();if(PlayingCareerEnded(db.Find(id)))throw new InvalidOperationException("La carrière de ce joueur est terminée ; aucune option d’achat ne peut être levée.");var c=world.contracts.FirstOrDefault(x=>x.player==id&&x.club==club&&x.IsLoan&&x.loanUntil>=life.day);if(c?.terms==null||c.terms.optionFee<=0)throw new InvalidOperationException("Aucune option d’achat active.");
            long cost=c.terms.optionFee;if(cost>TransferBudget)throw new InvalidOperationException("Budget de transfert insuffisant.");if(Payroll(db)+ReservedWages+c.wage-c.wage*c.terms.loanWagePercent/100>Math.Max(WageBudget,Payroll(db)))throw new InvalidOperationException("Budget salarial insuffisant pour prendre en charge le salaire intégral.");string owner=c.parent;BeforeFinancialTermsChange(db,club,owner);Charge(cost,"Option d’achat • "+db.Find(id).name);CreditTransferRecipient(db,owner,cost,"Option d’achat • "+db.Find(id).name);world.transferSpent+=cost;ConvertLoanPurchase(c,db.Find(id));AfterFinancialTermsChange(db,club,owner);SavePlayer(db.Find(id));Mail("Secrétariat","Option d’achat levée",db.Find(id).name+" est transféré définitivement. Le salaire intégral est désormais à votre charge.",id,"transfer");
        }
        public void RecallLoan(Database db,string id)
        {
            OffPitch();var c=world.contracts.FirstOrDefault(x=>x.player==id&&x.parent==club);if(c?.terms?.recall!=true||!WindowOpen)throw new InvalidOperationException("Le rappel nécessite une clause prévue au contrat et une fenêtre ouverte.");c.loanUntil=life.day;ResolveLoans(db);
        }
        void ResolveLoans(Database db)
        {
            ReviewOutgoingLoanAppearances(db);
            foreach(var c in world.contracts.Where(c=>c.IsLoan&&c.loanUntil<=life.day).ToArray()){
                var p=db.Find(c.player);if(p==null){ForgetLoanPlayerHistory(c.parent,c.player);CloseRetiredLoan(c);continue;}if(p.team=="retired"||c.retirement>=0&&c.retirement<=life.day){RetireEmployment(db,p,c,true);continue;}var terms=c.terms??new MarketTerms();string owner=c.parent,borrower=c.club;BeforeFinancialTermsChange(db,owner,borrower);bool owned=c.parent==club,borrowed=c.club==club;int appearances=borrowed?(life.players.FirstOrDefault(x=>x.id==p.id)?.appearances??0)-c.appearancesAtSigning:c.loanAppearanceTracking?Math.Max(0,(world.youth.FirstOrDefault(y=>y.player==p.id)?.loanAppearances??0)-c.appearancesAtSigning):0;
                bool purchase=terms.obligationFee>0&&(terms.obligationAppearances==0||appearances>=terms.obligationAppearances);
                if(purchase){SettleSignedPrincipal(db,borrower,owner,terms.obligationFee,"Obligation d’achat • "+p.name);if(borrowed)world.transferSpent+=terms.obligationFee;ConvertLoanPurchase(c,p);Mail("Secrétariat","Obligation d’achat déclenchée",p.name+" est transféré définitivement selon les conditions signées.",p.id,"transfer");}
                else{RestoreLoanParent(c,p,owner);RestoreLoanPlayerHistory(p,owner,borrower);if(p.team==club){var path=world.youth.FirstOrDefault(y=>y.player==p.id);if(path!=null)path.group="senior";}Mail("Secrétariat","Fin de prêt",p.name+" retourne dans son club. Les options non levées sont caduques.",p.id,"transfer");}
                AfterFinancialTermsChange(db,owner,borrower);SavePlayer(p);if(db.Squad(club).Count>=11)lineup=Select(db,club,tactic);
            }
        }
        void MarketDay(Database db)
        {
            payments??=new List<PaymentDue>();shortlist??=new List<string>();ResolveLoans(db);ActivatePrecontracts(db);OutgoingLoanDay(db);
            foreach(var payment in payments.Where(p=>!p.settled&&p.due<=life.day)){
                if(payment.counterparty==null&&payment.player!=null){
                    var recipients=world.offers.Where(o=>o.player==payment.player&&o.destination==payment.club&&o.status=="signed"&&o.fee>0&&o.due<=payment.due&&IsFinancialClub(db,o.seller)&&o.seller!=payment.club).Select(o=>o.seller).Distinct().ToArray();
                    if(recipients.Length==1)payment.counterparty=recipients[0];
                }
                if(IsFinancialClub(db,payment.counterparty)&&IsFinancialClub(db,payment.club))SettleSignedPrincipal(db,payment.club,payment.counterparty,payment.amount,payment.label);
                else if(payment.club==club){payment.legacyUnpaired=payment.player!=null;Account(-payment.amount,payment.label+(payment.legacyUnpaired?" · ancien destinataire non documenté":""));}
                else if(payment.player!=null&&IsFinancialClub(db,payment.club)){
                    payment.legacyUnpaired=true;BeforeFinancialTermsChange(db,payment.club);var payer=world.aiAccounts.First(a=>a.club==payment.club);AiEntry(payer,-payment.amount,payment.label+" · ancien destinataire non documenté");CoverAiDeficit(payer);
                }else continue;
                payment.settled=true;
            }
            if(Date.Month==6&&Date.Day==15||Date.Month==1&&Date.Day==1||Date.Month==8&&Date.Day==25||Date.Month==1&&Date.Day==25||Date.Month==9&&Date.Day==2||Date.Month==2&&Date.Day==2)Mail("Cellule mercato",MarketPhase,"Consultez les dossiers, les échéances et les engagements. Un accord non signé ne déplace pas le joueur.",null,"transfer");
        }
        void SettleTransferFee(Database db,TransferOffer offer)
        {
            var t=offer.terms??new MarketTerms();long immediate=offer.fee*t.upfrontPercent/100;Charge(immediate+offer.wage*2,"Signature • indemnité et prime d’agent");CreditTransferRecipient(db,offer.seller,immediate,"Cession • "+offer.player);long remaining=offer.fee-immediate;
            for(int i=0;i<t.instalments&&remaining>0;i++){long amount=i==t.instalments-1?remaining-(remaining/t.instalments)*i:remaining/t.instalments;payments.Add(new PaymentDue{club=club,counterparty=IsFinancialClub(db,offer.seller)?offer.seller:null,player=offer.player,amount=amount,due=life.day+90*(i+1),label="Échéance de transfert • "+offer.player});}
        }
    }
}

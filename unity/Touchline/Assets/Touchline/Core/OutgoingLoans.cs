using System;
using System.Collections.Generic;
using System.Linq;

namespace Touchline.Core
{
    [Serializable] public class OutgoingLoanOffer { public string player,owner,borrower,status="pending";public int due;public long fee;public MarketTerms terms; }
    public partial class Career
    {
        public List<OutgoingLoanOffer> outgoingLoans=new List<OutgoingLoanOffer>();
        public void ProposeOutgoingLoan(Database db,string player,string borrower,long fee,MarketTerms terms)
        {
            OffPitch();var p=db.Find(player);if(!WindowOpen||PlayingCareerEnded(p)||p.team!=club||p.age<18||Contract(db,player).IsLoan||db.Squad(club).Count<19||!LoanRecipientAllowed(db,borrower))throw new InvalidOperationException("Un prêt nécessite un joueur majeur sous contrat, un effectif suffisant et un mercato ouvert.");
            ValidateTerms(terms,true);if(fee<0||terms.loanEndDay>Contract(db,player).until)throw new InvalidOperationException("Conditions invalides : le prêt doit finir avant le contrat du joueur.");
            if(outgoingLoans.Any(o=>o.player==player&&(o.status=="pending"||o.status=="accepted")))throw new InvalidOperationException("Un prêt est déjà en discussion.");
            var offer=new OutgoingLoanOffer{player=player,owner=club,borrower=borrower,fee=fee,terms=terms,due=life.day+2};outgoingLoans.Add(offer);Mail("Agent · "+p.name,"Prêt proposé","Le joueur et le club d’accueil étudient le temps de jeu, la prise en charge salariale et les clauses. Réponse sous deux jours.",player,"transfer",OutgoingLoanMessageReference(offer));
        }
        void OutgoingLoanDay(Database db)
        {
            outgoingLoans??=new List<OutgoingLoanOffer>();foreach(var o in outgoingLoans.Where(o=>o.status=="pending"&&o.due<=life.day)){
                var p=db.Find(o.player);var contract=world.contracts.FirstOrDefault(c=>c.player==o.player);
                if(p==null||PlayingCareerEnded(p)||p.team!=o.owner||contract==null||contract.IsLoan||o.terms==null||o.terms.loanEndDay<=life.day||o.terms.loanEndDay>contract.until){
                    o.status="expired";
                    if(o.owner==club)Mail("Secrétariat","Proposition de prêt caduque",(p?.name??o.player)+" : la situation du joueur ou les échéances contractuelles ont changé. Aucun prêt n’a été signé.",p?.id,"transfer",OutgoingLoanMessageReference(o));
                    continue;
                }
                var assessment=LoanClubAssessment(db,o.player,o.borrower,o.terms,o.fee);bool fit=assessment?.levelCompatible==true,affordable=assessment?.affordable==true;
                o.status=fit&&affordable?"accepted":"declined";if(o.owner==club)Mail("Agent · "+p.name,o.status=="accepted"?"Accord de prêt":"Prêt refusé",o.status=="accepted"?"Le club accepte les conditions. Vous disposez de sept jours pour signer.":!fit?"Le niveau et le temps de jeu envisagés ne conviennent pas au joueur.":"Le club d’accueil ne peut pas financer ces conditions. Réduisez l’indemnité, la part salariale ou les clauses.",p.id,"transfer",OutgoingLoanMessageReference(o));}
            foreach(var o in outgoingLoans.Where(o=>o.status=="accepted"&&o.due+7<life.day))o.status="expired";
        }
        public void SignOutgoingLoan(Database db,string player)
        {
            OffPitch();var o=outgoingLoans.LastOrDefault(o=>o.player==player&&o.owner==club&&o.status=="accepted"&&o.due+7>=life.day);if(o==null||!WindowOpen||db.Squad(club).Count<19)throw new InvalidOperationException("Le prêt ne peut plus être signé.");var p=db.Find(player);if(PlayingCareerEnded(p))throw new InvalidOperationException("La carrière de ce joueur est terminée ; cet accord de prêt ne peut plus être signé.");var c=Contract(db,player);if(p.team!=club||c.IsLoan||o.terms==null||o.terms.loanEndDay<=life.day||o.terms.loanEndDay>c.until)throw new InvalidOperationException("La situation du joueur a changé.");
            ReceiveNpcTransfer(db,o.borrower,o.fee,"Indemnité de prêt • "+p.name);
            CaptureLoanParent(c,p);c.parentUntil=c.until;c.parent=club;c.club=o.borrower;c.originalWage=p.wage;c.wage=p.wage;c.terms=o.terms;c.loanUntil=o.terms.loanEndDay;PrepareLoanPurchase(c,3,true);p.team=o.borrower;
            var path=world.youth.FirstOrDefault(y=>y.player==player);if(path==null){path=new YouthPath{player=player,group="loan"};world.youth.Add(path);}path.group="loan";StartOutgoingLoanTracking(c,path);
            AfterFinancialTermsChange(db,club,o.borrower);life.players.RemoveAll(x=>x.id==player);SavePlayer(p);o.status="signed";lineup=Select(db,club,tactic);Mail("Secrétariat","Prêt signé",p.name+" est prêté. Le club d’accueil finance "+o.terms.loanWagePercent+" % de son salaire.",player,"transfer");
        }
    }
}

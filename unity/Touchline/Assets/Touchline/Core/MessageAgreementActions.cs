using System;
using System.Linq;

namespace Touchline.Core
{
    public partial class Career
    {
        static string TransferMessageReference(TransferOffer o)=>"transfer/"+o.player+"/"+o.destination+"/"+o.seller+"/"+o.due+"/"+o.attempts+"/"+o.fee+"/"+o.wage;
        static string OutgoingLoanMessageReference(OutgoingLoanOffer o)=>"outgoing-loan/"+o.player+"/"+o.owner+"/"+o.borrower+"/"+o.due+"/"+o.fee;
        static string StaffMessageReference(StaffOffer o)=>"staff-contract/"+o.staff+"/"+o.club+"/"+o.employer+"/"+o.due+"/"+o.wage+"/"+o.years;
        static bool AgreementMessageMatches(ClubMessage m,string reference,string subject,int due)
        {
            if(m.subject!=subject)return false;
            if(!string.IsNullOrEmpty(m.reference))return m.reference==reference;
            // Compatibility for previously saved agreement mails, without
            // reactivating every older message about the same person.
            return m.day>=due&&m.day<=due+7;
        }
        // Persisted career evidence only: an announced future retirement does
        // not close an agreement, and no missing date is inferred from age.
        public bool PlayerRetirementEffective(string id)
        {
            if(string.IsNullOrEmpty(id))return false;
            if(world?.contracts?.Any(c=>c.player==id&&(c.club=="retired"||life!=null&&c.retirement>=0&&c.retirement<=life.day))==true)return true;
            return world?.rosterChanges?.LastOrDefault(p=>p.id==id)?.team=="retired";
        }
        public TransferOffer PendingTransferAgreement(ClubMessage m)
        {
            if(m?.action!="transfer"||world==null||world.managerStatus!="employed"||PlayerRetirementEffective(m.player))return null;
            return world.offers.LastOrDefault(o=>o.player==m.player&&(o.destination==null||o.destination==club)&&o.status=="accepted"&&o.due+7>=life.day&&AgreementMessageMatches(m,TransferMessageReference(o),"Accord de principe",o.due));
        }
        public OutgoingLoanOffer PendingOutgoingLoanAgreement(ClubMessage m)
        {
            if(m?.action!="transfer"||world==null||world.managerStatus!="employed"||PlayerRetirementEffective(m.player))return null;
            return outgoingLoans?.LastOrDefault(o=>o.player==m.player&&o.owner==club&&o.status=="accepted"&&o.due+7>=life.day&&AgreementMessageMatches(m,OutgoingLoanMessageReference(o),"Accord de prêt",o.due));
        }
        public StaffOffer PendingStaffAgreement(ClubMessage m)
        {
            if(m?.action!="staff"||world==null||world.managerStatus!="employed")return null;
            return staffOffers?.LastOrDefault(o=>o.club==club&&o.status=="accepted"&&o.due+7>=life.day&&AgreementMessageMatches(m,StaffMessageReference(o),"Accord de principe",o.due)&&
                (!string.IsNullOrEmpty(m.reference)||m.sender=="Agent · "+staffMarket?.FirstOrDefault(s=>s.id==o.staff)?.name));
        }
    }
}

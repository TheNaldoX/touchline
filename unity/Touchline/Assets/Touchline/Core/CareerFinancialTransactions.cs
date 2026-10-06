using System;
using System.Linq;

namespace Touchline.Core
{
    public partial class Career
    {
        bool IsFinancialClub(Database db,string id)=>id!=null&&db.clubs.Any(t=>t.id==id);
        void EnsureFinancialAccounts(Database db)
        {
            if(world.aiAccounts==null||world.aiAccounts.Count<db.clubs.Length||world.aiAccounts.Any(a=>!a.hasOperatingSnapshot))EnsureAiClubAccounts(db);
        }
        void ValidateNpcPayment(Database db,string payer,long amount)
        {
            if(amount<0||!IsFinancialClub(db,payer)||payer==club)throw new InvalidOperationException("Club payeur ou indemnité invalide.");
            EnsureFinancialAccounts(db);
            var a=world.aiAccounts.First(x=>x.club==payer);var t=db.clubs.First(x=>x.id==payer);
            long available=Math.Max(0,ProjectedAiOperatingBalance(a)-AiCommitted(payer)-t.annualRevenue/40);
            if(amount>available)throw new InvalidOperationException("Le club acheteur ne peut plus financer cette indemnité et ses engagements.");
        }
        void BeforeFinancialTermsChange(Database db,params string[] ids)
        {
            EnsureFinancialAccounts(db);
            foreach(var id in ids.Distinct()){
                if(!IsFinancialClub(db,id))continue;var a=world.aiAccounts.First(x=>x.club==id);
                if(id==club){a.cash=life.cash;a.operatingDebt=world.debt;}
                else ProjectAiOperatingPeriod(db.clubs.First(t=>t.id==id),a,db.Squad(id),life.day);
            }
        }
        void AfterFinancialTermsChange(Database db,params string[] ids)
        {
            EnsureFinancialAccounts(db);
            var payrolls=ClubWeeklyPayrolls(db);
            foreach(var id in ids.Distinct())if(IsFinancialClub(db,id)){
                var a=world.aiAccounts.First(x=>x.club==id);if(id==club){a.cash=life.cash;a.operatingDebt=world.debt;}
                a.projectedFromDay=life.day;CaptureAiOperatingSnapshot(db.clubs.First(t=>t.id==id),a,payrolls[id]);
            }
        }
        void CreditTransferRecipient(Database db,string recipient,long amount,string label)
        {
            if(amount==0||!IsFinancialClub(db,recipient)||recipient==club)return;
            AiEntry(world.aiAccounts.First(x=>x.club==recipient),amount,label);
        }
        void ReceiveNpcTransfer(Database db,string payer,long amount,string label)
        {
            ValidateNpcPayment(db,payer,amount);BeforeFinancialTermsChange(db,payer,club);
            AiEntry(world.aiAccounts.First(x=>x.club==payer),-amount,label+" · paiement au club vendeur");Account(amount,label);
        }
        void SettleSignedPrincipal(Database db,string payer,string recipient,long amount,string label)
        {
            if(amount<0||!IsFinancialClub(db,payer)||!IsFinancialClub(db,recipient)||payer==recipient)throw new InvalidOperationException("Contreparties financières invalides.");
            BeforeFinancialTermsChange(db,payer,recipient);var pa=world.aiAccounts.First(a=>a.club==payer);var ra=world.aiAccounts.First(a=>a.club==recipient);
            AiEntry(pa,-amount,label+" · paiement");AiEntry(ra,amount,label+" · encaissement");
            // A previously signed mandatory liability is paid with an explicitly
            // recorded overdraft if necessary; it cannot create free money.
            CoverAiDeficit(pa);
        }
    }
}

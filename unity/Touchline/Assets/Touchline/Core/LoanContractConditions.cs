using System;
using System.Linq;

namespace Touchline.Core
{
    [Serializable] public class LoanContractConditions
    {
        public string role,source;
        public PlayingTimeUsage playingTime;
        public int until,joined,appearancesAtSigning,wageChangeDay;
        public long wage,appearanceBonus,releaseClause,nextWage;
        public bool estimated,aiRelease;
        public MarketTerms terms;
    }
    public partial class Career
    {
        static MarketTerms CopyContractTerms(MarketTerms t)=>t==null?null:new MarketTerms{
            loanWagePercent=t.loanWagePercent,loanEndDay=t.loanEndDay,upfrontPercent=t.upfrontPercent,instalments=t.instalments,obligationAppearances=t.obligationAppearances,
            optionFee=t.optionFee,obligationFee=t.obligationFee,goalBonus=t.goalBonus,sellOnPercent=t.sellOnPercent,annualRise=t.annualRise,promotionRise=t.promotionRise,relegationCut=t.relegationCut,recall=t.recall
        };
        static LoanContractConditions Conditions(Employment c)=>new LoanContractConditions{
            role=c.role,playingTime=c.playingTime?.Copy(),until=c.until,joined=c.joined,appearancesAtSigning=c.appearancesAtSigning,wageChangeDay=c.wageChangeDay,wage=c.wage,
            appearanceBonus=c.appearanceBonus,releaseClause=c.releaseClause,nextWage=c.nextWage,estimated=c.estimated,aiRelease=c.aiRelease,terms=CopyContractTerms(c.terms),source=c.conditionsSource
        };
        static void ApplyConditions(Employment c,LoanContractConditions s)
        {
            c.role=s.role;c.playingTime=s.playingTime?.Copy();c.until=s.until;c.joined=s.joined;c.appearancesAtSigning=s.appearancesAtSigning;c.wageChangeDay=s.wageChangeDay;c.wage=s.wage;
            c.appearanceBonus=s.appearanceBonus;c.releaseClause=s.releaseClause;c.nextWage=s.nextWage;c.estimated=s.estimated;c.aiRelease=s.aiRelease;c.terms=CopyContractTerms(s.terms);c.conditionsSource=s.source;
        }
        static MarketTerms PermanentContractTerms(MarketTerms source)
        {
            var t=CopyContractTerms(source);if(t==null)return null;t.loanWagePercent=100;t.loanEndDay=0;t.optionFee=0;t.obligationFee=0;t.obligationAppearances=0;t.recall=false;t.upfrontPercent=100;t.instalments=0;return t;
        }
        void CaptureLoanParent(Employment c,PlayerData p)
        {
            CaptureLoanPlayerHistory(p,c.club);c.parentConditions=Conditions(c);c.parentConditions.wage=p.wage;if(string.IsNullOrWhiteSpace(c.parentConditions.source))c.parentConditions.source="Conditions parent enregistrées avant la signature du prêt";c.parentConditionsUnavailable=false;
        }
        void PrepareLoanPurchase(Employment c,int years,bool estimated)
        {
            c.purchaseConditions=Conditions(c);var s=c.purchaseConditions;s.until=DayOf(new DateTime(Date.Year+years,6,30));s.terms=PermanentContractTerms(s.terms);s.nextWage=0;s.wageChangeDay=0;s.aiRelease=false;s.estimated=estimated;
            s.source=estimated?"Conditions d’achat simulées : trois ans, salaire intégral conservé ; aucun contrat personnel futur négocié":"Conditions personnelles prévues dans l’offre de prêt acceptée";
            c.until=c.loanUntil;c.nextWage=0;c.wageChangeDay=0;c.aiRelease=false;
        }
        void RestoreLoanParent(Employment c,PlayerData p,string owner)
        {
            if(c.parentConditions!=null){ApplyConditions(c,c.parentConditions);c.parentConditionsUnavailable=false;}
            else{
                // Older saves cannot recover a role, bonus or clause which was overwritten.
                c.playingTime=null;if(c.parentUntil>0)c.until=c.parentUntil;if(c.originalWage>0)c.wage=c.originalWage;c.terms=null;c.estimated=true;c.parentConditionsUnavailable=true;c.conditionsSource="Ancienne sauvegarde : conditions personnelles parent perdues ; seuls salaire et échéance documentés sont restaurés, les autres valeurs conservées restent incertaines";
            }
            p.team=owner;c.club=owner;p.wage=c.wage;c.parent=null;c.loanUntil=0;c.parentUntil=0;c.originalWage=0;c.parentConditions=null;c.purchaseConditions=null;
        }
        void ConvertLoanPurchase(Employment c,PlayerData p)
        {
            ForgetLoanPlayerHistory(c.parent,p.id);var s=c.purchaseConditions;
            if(s==null){s=Conditions(c);s.until=DayOf(new DateTime(Date.Year+3,6,30));s.estimated=true;s.source="Ancien accord : conditions personnelles permanentes inconnues, engagement de trois ans simulé";}
            ApplyConditions(c,s);c.playingTime=new PlayingTimeUsage{club=c.club};c.terms=PermanentContractTerms(c.terms);c.joined=life.day;c.appearancesAtSigning=life.players.FirstOrDefault(x=>x.id==p.id)?.appearances??0;c.nextWage=0;c.wageChangeDay=0;c.aiRelease=false;
            c.parent=null;c.loanUntil=0;c.parentUntil=0;c.originalWage=0;c.parentConditions=null;c.purchaseConditions=null;c.parentConditionsUnavailable=false;p.wage=c.wage;
        }
        void ProcessLoanParentWageChanges(Database db)
        {
            if(world?.contracts==null)return;
            foreach(var c in world.contracts){var s=c.parentConditions;if(!c.IsLoan||s==null||s.nextWage<=0||s.wageChangeDay>life.day)continue;var p=db.Find(c.player);if(p==null||p.team!=c.club)continue;
                BeforeFinancialTermsChange(db,c.parent,c.club);long due=s.nextWage;p.wage=due;c.wage=due;c.originalWage=due;s.wage=due;s.nextWage=0;s.wageChangeDay=0;if(c.purchaseConditions!=null)c.purchaseConditions.wage=due;
                AfterFinancialTermsChange(db,c.parent,c.club);SavePlayer(p);
            }
        }
    }
}

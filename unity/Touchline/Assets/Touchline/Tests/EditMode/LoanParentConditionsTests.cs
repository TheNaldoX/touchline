using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Touchline.Core;

public static class LoanParentConditionsChecks
{
    static void Require(bool b,string s){if(!b)throw new InvalidOperationException(s);}
    static object Call(Career c,string name,params object[] args)=>typeof(Career).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(c,args);
    static (Career c,Database db) Setup(){
        var db=new Database{leagues=new[]{new LeagueData{id="fra.1",name="L1"},new LeagueData{id="fra.2",name="L2",tier=2}},clubs=Enumerable.Range(0,8).Select(i=>new ClubData{id="c"+i,name="Club "+i,league=i<4?"fra.1":"fra.2",playable=true,annualRevenue=20000000+i*1000000}).ToArray(),players=Enumerable.Range(0,192).Select(i=>new PlayerData{id="p"+i,name="Alex "+i,team="c"+(i/24),age=24,position=i%24==0||i%24==12?"GB":i%24<8?"DEF":i%24<16?"MIL":"ATT",positions=new[]{i%24==0||i%24==12?"GK":i%24<8?"CB":i%24<16?"CM":"ST"},rating=65,potential=80,value=100000,wage=500,attributes=new[]{new AttributeValue{key="shortPassing",value=65}}}).ToArray()};
        var c=new Career{club="c0"};c.lineup=Career.Select(db,c.club,c.tactic);c.EnsureWorld(db);return(c,db);
    }
    static Employment Parent(Career c,Database db,string id)
    {
        var p=db.Find(id);p.wage=1001;var e=c.Contract(db,id);e.wage=1001;e.role="key";e.until=300;e.joined=-70;e.appearancesAtSigning=9;e.appearanceBonus=45;e.releaseClause=888888;e.estimated=false;e.aiRelease=false;e.nextWage=1501;e.wageChangeDay=90;e.terms=new MarketTerms{sellOnPercent=15,annualRise=8,promotionRise=12,relegationCut=20,goalBonus=7};return e;
    }
    static void Incoming(Career c,Database db,MarketTerms terms=null)
    {
        c.ProposeTransfer(db,"p24",0,1001,4,"starter",true,13,123456,terms??new MarketTerms{loanWagePercent=40,loanEndDay=28});c.world.offers.Last().status="accepted";c.SignTransfer(db,"p24");
    }
    static void RequireRestored(Employment e)
    {
        Require(e.role=="key"&&e.until==300&&e.joined==-70&&e.appearancesAtSigning==9&&e.appearanceBonus==45&&e.releaseClause==888888&&!e.estimated&&!e.aiRelease,"Parent contract role, expiry, seniority, bonus, clause or flags lost.");
        Require(e.terms!=null&&e.terms.sellOnPercent==15&&e.terms.annualRise==8&&e.terms.promotionRise==12&&e.terms.relegationCut==20&&e.terms.goalBonus==7&&e.terms.optionFee==0&&e.terms.obligationFee==0,"Parent contract clauses not restored or borrower purchase terms leaked.");
        Require(e.parent==null&&e.loanUntil==0&&e.parentConditions==null&&e.purchaseConditions==null&&!e.parentConditionsUnavailable,"Finished loan retained temporary snapshots or marked known conditions unknown.");
    }
    public static Dictionary<string,object> Run(string only=null)
    {
        var result=new Dictionary<string,object>();
        if(only==null||only=="incomingLoanRestoresCompleteParentAndCopiesTerms"){
            var(c,db)=Setup();var e=Parent(c,db,"p24");var originalTerms=e.terms;Incoming(c,db);Require(e.until==28&&e.parentConditions.until==300&&e.role=="starter"&&e.parentConditions.role=="key","Temporary and parent conditions were not separated.");originalTerms.annualRise=1;e.terms.sellOnPercent=0;e.retirement=100;c.life.day=28;Call(c,"ResolveLoans",db);RequireRestored(e);Require(e.wage==1001&&e.nextWage==1501&&e.wageChangeDay==90&&e.retirement==100,"Return lost deferred parent raise or overwrote a new personal retirement decision.");result["incomingLoanRestoresCompleteParentAndCopiesTerms"]=true;
        }
        if(only==null||only=="outgoingLoanRestoresParentClausesAndRole"){
            var(c,db)=Setup();var e=Parent(c,db,"p10");c.ProposeOutgoingLoan(db,"p10","c1",0,new MarketTerms{loanWagePercent=40,loanEndDay=28,optionFee=10000});c.outgoingLoans.Last().status="accepted";c.SignOutgoingLoan(db,"p10");c.life.day=28;Call(c,"ResolveLoans",db);RequireRestored(e);Require(db.Find("p10").team=="c0"&&e.nextWage==1501&&e.wageChangeDay==90,"Outgoing return changed ownership or pending parent raise.");result["outgoingLoanRestoresParentClausesAndRole"]=true;
        }
        if(only==null||only=="serializedSnapshotsSurviveReturn"){
            var(c,db)=Setup();Parent(c,db,"p24");Incoming(c,db);var json=UnityEngine.JsonUtility.ToJson(c.Contract(db,"p24"));var restored=UnityEngine.JsonUtility.FromJson<Employment>(json);Require(restored.parentConditions!=null&&restored.purchaseConditions!=null,"Serialized contract lost parent or purchase snapshots.");c.world.contracts=c.world.contracts.Where(e=>e.player!="p24").Append(restored).ToList();c.life.day=28;Call(c,"ResolveLoans",db);RequireRestored(restored);result["serializedSnapshotsSurviveReturn"]=true;
        }
        if(only==null||only=="purchaseOptionAppliesAcceptedPermanentTerms"){
            var(c,db)=Setup();Parent(c,db,"p24");Incoming(c,db,new MarketTerms{loanWagePercent=40,loanEndDay=28,optionFee=10000});var e=c.Contract(db,"p24");int until=e.purchaseConditions.until;c.ExerciseLoanOption(db,"p24");Require(e.parent==null&&e.until==until&&e.until>28&&e.role=="starter"&&e.appearanceBonus==13&&e.releaseClause==123456&&!e.estimated&&e.wage==1001,"Option restored former parent instead of accepted new contract.");Require(e.parentConditions==null&&e.purchaseConditions==null&&e.terms.optionFee==0&&e.terms.obligationFee==0&&e.terms.loanEndDay==0&&e.terms.loanWagePercent==100&&!e.terms.recall,"Permanent contract retains temporary loan clauses.");result["purchaseOptionAppliesAcceptedPermanentTerms"]=true;
        }
        if(only==null||only=="mandatoryPurchaseUsesExplicitEstimatedOutboundTerms"){
            var(c,db)=Setup();var e=Parent(c,db,"p10");c.ProposeOutgoingLoan(db,"p10","c1",0,new MarketTerms{loanWagePercent=40,loanEndDay=28,obligationFee=10000});c.outgoingLoans.Last().status="accepted";c.SignOutgoingLoan(db,"p10");Require(e.purchaseConditions.estimated&&e.purchaseConditions.source.Contains("simulées"),"Unnegotiated outgoing personal purchase terms presented as known.");c.life.day=28;Call(c,"ResolveLoans",db);Require(e.parent==null&&e.until>28&&e.estimated&&e.nextWage==0&&e.parentConditions==null&&e.terms.obligationFee==0&&e.terms.loanEndDay==0&&e.terms.loanWagePercent==100,"Mandatory purchase left parent raise, snapshot or loan clauses active.");result["mandatoryPurchaseUsesExplicitEstimatedOutboundTerms"]=true;
        }
        if(only==null||only=="legacyMissingConditionsNotInventedOrZeroWaged"){
            foreach(long original in new[]{0L,450L}){var(c,db)=Setup();var p=db.Find("p24");p.team=c.club;var e=c.Contract(db,p.id);e.club=c.club;e.parent="c1";e.parentUntil=300;e.originalWage=original;e.loanUntil=28;e.terms=new MarketTerms{loanWagePercent=40,optionFee=12345};c.life.day=28;Call(c,"ResolveLoans",db);Require(e.parentConditionsUnavailable&&e.estimated&&e.parentConditions==null&&e.terms==null&&p.wage==(original>0?original:500)&&e.until==300&&p.team=="c1","Legacy return invented missing conditions or introduced zero wage.");}
            result["legacyMissingConditionsNotInventedOrZeroWaged"]=true;
        }
        if(only==null||only=="scheduledParentRaiseAppliesBeforeWeeklyPayAndSurvivesReturn"){
            var(c,db)=Setup();var e=Parent(c,db,"p24");e.wageChangeDay=7;Incoming(c,db);long initial=((Dictionary<string,long>)Call(c,"ClubWeeklyPayrolls",db)).Values.Sum();for(int i=0;i<7;i++)c.AdvanceDay(db);
            var payroll=(Dictionary<string,long>)Call(c,"ClubWeeklyPayrolls",db);Require(e.wage==1501&&e.originalWage==1501&&e.parentConditions.wage==1501&&e.parentConditions.nextWage==0&&e.purchaseConditions.wage==1501,"Scheduled parent raise was deferred while on loan.");Require(c.Payroll(db)==12600&&payroll["c1"]==12401&&payroll.Values.Sum()==initial+500,"Scheduled raise not divided using exact gross wage.");Require(c.life.ledger.Last(x=>x.day==7&&x.label=="Salaires de l’effectif").amount==-12600,"Weekly manager pay used salary before its effective date.");Require(c.world.aiAccounts.First(a=>a.club=="c1").projectedGrossPayroll==12401*52,"Parent NPC snapshot failed to capture new retained salary.");for(int i=7;i<28;i++)c.AdvanceDay(db);RequireRestored(e);Require(e.wage==1501&&db.Find("p24").wage==1501&&e.nextWage==0&&e.wageChangeDay==0,"Loan return erased or reapplied an already effective parent raise.");result["scheduledParentRaiseAppliesBeforeWeeklyPayAndSurvivesReturn"]=true;
        }
        if(only==null||only=="loanExpiryReturnsBeforeEmploymentRelease"){
            var(c,db)=Setup();var e=Parent(c,db,"p10");c.ProposeOutgoingLoan(db,"p10","c1",0,new MarketTerms{loanWagePercent=40,loanEndDay=28});c.outgoingLoans.Last().status="accepted";c.SignOutgoingLoan(db,"p10");for(int i=0;i<28;i++)c.AdvanceDay(db);RequireRestored(e);Require(db.Find("p10").team=="c0"&&e.until==300,"Temporary loan expiry released player before parent restoration.");result["loanExpiryReturnsBeforeEmploymentRelease"]=true;
        }
        if(only==null||only=="loanExpiryConvertsBeforeEmploymentRelease"){
            var(c,db)=Setup();Parent(c,db,"p24");Incoming(c,db,new MarketTerms{loanWagePercent=40,loanEndDay=28,obligationFee=10000});for(int i=0;i<28;i++)c.AdvanceDay(db);var e=c.Contract(db,"p24");Require(db.Find("p24").team==c.club&&e.parent==null&&e.until>28&&e.terms.obligationFee==0,"Loan end day released player before mandatory purchase conversion.");result["loanExpiryConvertsBeforeEmploymentRelease"]=true;
        }
        if(only==null||only=="developmentLoanPreservesParentAndClosesSalarySnapshots"){
            var(c,db)=Setup();var e=Parent(c,db,"p10");db.Find("p10").age=19;c.world.youth.Add(new YouthPath{player="p10",group="senior"});c.LoanYouth(db,"p10","c1");Require(e.parentConditions!=null&&e.until==e.loanUntil&&e.loanUntil<=300&&c.world.aiAccounts.First(a=>a.club=="c1").projectedGrossPayroll==12500*52,"Development loan lost parent conditions, expiry bound or new borrower salary snapshot.");c.life.day=e.loanUntil;Call(c,"ResolveLoans",db);RequireRestored(e);result["developmentLoanPreservesParentAndClosesSalarySnapshots"]=true;
        }
        return result;
    }
}

namespace Touchline.Tests
{
    public sealed class LoanParentConditionsTests
    {
        [NUnit.Framework.TestCase("incomingLoanRestoresCompleteParentAndCopiesTerms")]
        [NUnit.Framework.TestCase("outgoingLoanRestoresParentClausesAndRole")]
        [NUnit.Framework.TestCase("serializedSnapshotsSurviveReturn")]
        [NUnit.Framework.TestCase("purchaseOptionAppliesAcceptedPermanentTerms")]
        [NUnit.Framework.TestCase("mandatoryPurchaseUsesExplicitEstimatedOutboundTerms")]
        [NUnit.Framework.TestCase("legacyMissingConditionsNotInventedOrZeroWaged")]
        [NUnit.Framework.TestCase("scheduledParentRaiseAppliesBeforeWeeklyPayAndSurvivesReturn")]
        [NUnit.Framework.TestCase("loanExpiryReturnsBeforeEmploymentRelease")]
        [NUnit.Framework.TestCase("loanExpiryConvertsBeforeEmploymentRelease")]
        [NUnit.Framework.TestCase("developmentLoanPreservesParentAndClosesSalarySnapshots")]
        public void ParentAndPermanentLoanConditionsStayDistinct(string scenario)
        {
            NUnit.Framework.Assert.That(LoanParentConditionsChecks.Run(scenario)[scenario],NUnit.Framework.Is.True);
        }
    }
}

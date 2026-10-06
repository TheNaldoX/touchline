using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Touchline.Core;

public static class LoanSalaryChecks
{
    static void Require(bool b,string s){if(!b)throw new InvalidOperationException(s);}
    static object Call(Career c,string name,params object[] args)=>typeof(Career).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(c,args);
    static (Career c,Database db) Setup(){
        var db=new Database{leagues=new[]{new LeagueData{id="fra.1",name="L1"},new LeagueData{id="fra.2",name="L2",tier=2}},clubs=Enumerable.Range(0,8).Select(i=>new ClubData{id="c"+i,name="Club "+i,league=i<4?"fra.1":"fra.2",playable=true,annualRevenue=20000000+i*1000000}).ToArray(),players=Enumerable.Range(0,192).Select(i=>new PlayerData{id="p"+i,name="Alex "+i,team="c"+(i/24),age=24,position=i%24==0||i%24==12?"GB":i%24<8?"DEF":i%24<16?"MIL":"ATT",positions=new[]{i%24==0||i%24==12?"GK":i%24<8?"CB":i%24<16?"CM":"ST"},rating=65,potential=80,value=100000,wage=500,attributes=new[]{new AttributeValue{key="shortPassing",value=65}}}).ToArray()};
        var c=new Career{club="c0"};c.lineup=Career.Select(db,c.club,c.tactic);c.EnsureWorld(db);return(c,db);
    }
    static Dictionary<string,long> Payrolls(Career c,Database db)=>(Dictionary<string,long>)Call(c,"ClubWeeklyPayrolls",db);
    public static Dictionary<string,object> Run(string only=null)
    {
        var result=new Dictionary<string,object>();
        foreach(int percent in new[]{0,40,100})if(only==null||only=="parentGrossAndShares"+percent){
            var(c,db)=Setup();var p=db.Find("p24");p.wage=1001;var contract=c.Contract(db,p.id);int originalUntil=contract.until;
            long before=Payrolls(c,db).Values.Sum();long salary=Career.WeeklySalary(Career.MonthlySalary(p.wage));
            Require(salary==p.wage,"Monthly display must preserve the gross weekly base when converted back.");
            c.ProposeTransfer(db,p.id,0,salary,3,"rotation",true,terms:new MarketTerms{loanWagePercent=percent,loanEndDay=28});
            var offer=c.world.offers.Last();Require(offer.wage==1001,"Loan proposal rewrote contractual gross salary through monthly conversion.");offer.status="accepted";c.SignTransfer(db,p.id);
            long borrower=1001L*percent/100,owner=1001-borrower;var payrolls=Payrolls(c,db);
            Require(p.wage==1001&&contract.wage==1001&&contract.originalWage==1001,"Signed loan altered parent gross salary.");
            Require(c.Payroll(db)==12000+borrower&&payrolls["c1"]==11500+owner&&payrolls.Values.Sum()==before,"Owner and borrower salary shares do not conserve full gross wage.");
            Require(c.world.aiAccounts.First(a=>a.club=="c1").projectedGrossPayroll==payrolls["c1"]*52,"Parent's operating snapshot omitted retained wage share.");
            c.life.day=28;Call(c,"ResolveLoans",db);payrolls=Payrolls(c,db);
            Require(p.team=="c1"&&p.wage==1001&&contract.wage==1001&&contract.until==originalUntil&&contract.parent==null,"Loan return changed parent salary, duration or ownership.");
            Require(c.Payroll(db)==12000&&payrolls["c1"]==12501&&payrolls.Values.Sum()==before,"Loan return left residual or duplicate payroll commitments.");
            result["parentGrossAndShares"+percent]=true;
        }
        if(only==null||only=="agentBandAndSubmittedSalaryCannotRaiseGross"){
            var(c,db)=Setup();var p=db.Find("p24");p.wage=1001;var agent=c.PlayerAgent(db,p.id,true);long monthly=Career.MonthlySalary(p.wage);
            Require(agent.monthlyLow==monthly&&agent.monthlyHigh==monthly,"Agent demanded a new personal salary for an existing parent loan contract.");
            c.ProposeTransfer(db,p.id,0,999999,3,"rotation",true,terms:new MarketTerms{loanWagePercent=40,loanEndDay=28});
            Require(c.world.offers.Last().wage==1001,"Submitted loan wage can raise or reduce parent contract instead of negotiating shares.");result["agentBandAndSubmittedSalaryCannotRaiseGross"]=true;
        }
        if(only==null||only=="legacyInflatedAcceptedLoanRejectedBeforeAccounting"){
            var(c,db)=Setup();var p=db.Find("p24");c.world.offers.Add(new TransferOffer{player=p.id,seller=p.team,destination=c.club,status="accepted",loan=true,fee=1000,wage=600,years=3,role="rotation",terms=new MarketTerms{loanWagePercent=40,loanEndDay=28}});
            long cash=c.life.cash;var ledgers=c.world.aiAccounts.Select(a=>a.ledger.Count).ToArray();var dates=c.world.aiAccounts.Select(a=>a.projectedFromDay).ToArray();int contracts=c.world.contracts.Count;bool rejected=false;
            try{c.SignTransfer(db,p.id);}catch(InvalidOperationException){rejected=true;}
            Require(rejected&&p.team=="c1"&&p.wage==500&&c.life.cash==cash&&contracts==c.world.contracts.Count&&c.world.offers.Last().status=="accepted"&&ledgers.SequenceEqual(c.world.aiAccounts.Select(a=>a.ledger.Count))&&dates.SequenceEqual(c.world.aiAccounts.Select(a=>a.projectedFromDay)),"Legacy salary-changing loan rejection changed finances or ownership first.");result["legacyInflatedAcceptedLoanRejectedBeforeAccounting"]=true;
        }
        if(only==null||only=="parentRaiseAfterAcceptanceRequiresNewAgreement"){
            var(c,db)=Setup();var p=db.Find("p24");c.ProposeTransfer(db,p.id,0,500,3,"rotation",true,terms:new MarketTerms{loanWagePercent=40,loanEndDay=28});var offer=c.world.offers.Last();offer.status="accepted";p.wage=700;c.Contract(db,p.id).wage=700;long cash=c.life.cash;bool rejected=false;
            try{c.SignTransfer(db,p.id);}catch(InvalidOperationException){rejected=true;}
            Require(rejected&&p.team=="c1"&&p.wage==700&&c.life.cash==cash&&offer.status=="accepted","Changed parent contract signed at stale wage or partially mutated transaction.");result["parentRaiseAfterAcceptanceRequiresNewAgreement"]=true;
        }
        if(only==null||only=="invalidSalarySharesRejectBeforeOfferCreation"){
            foreach(int percent in new[]{-1,101}){var(c,db)=Setup();int count=c.world.offers.Count;long cash=c.life.cash;bool rejected=false;try{c.ProposeTransfer(db,"p24",0,500,3,"rotation",true,terms:new MarketTerms{loanWagePercent=percent,loanEndDay=28});}catch(ArgumentException){rejected=true;}Require(rejected&&c.world.offers.Count==count&&c.life.cash==cash&&db.Find("p24").team=="c1","Invalid loan share mutated offers, cash or ownership.");}
            result["invalidSalarySharesRejectBeforeOfferCreation"]=true;
        }
        if(only==null||only=="permanentTransferSalaryStillNegotiable"){
            var(c,db)=Setup();c.ProposeTransfer(db,"p24",100000,800,3,"rotation");Require(c.world.offers.Last().wage==800,"Permanent transfer salary was unintentionally fixed to old contract.");result["permanentTransferSalaryStillNegotiable"]=true;
        }
        if(only==null||only=="purchaseOptionReservesExactOwnerComplement"){
            var(c,db)=Setup();var p=db.Find("p24");p.wage=1001;c.ProposeTransfer(db,p.id,0,1001,3,"rotation",true,terms:new MarketTerms{loanWagePercent=40,loanEndDay=28,optionFee=1});c.world.offers.Last().status="accepted";c.SignTransfer(db,p.id);
            long reserve=Math.Max(c.WageBudget,c.Payroll(db))-c.Payroll(db)-600;Require(reserve>0,"Fixture must allow a reserved wage commitment.");c.world.offers.Add(new TransferOffer{player="reserved",destination=c.club,status="scheduled",wage=reserve});long cash=c.life.cash;bool rejected=false;
            try{c.ExerciseLoanOption(db,p.id);}catch(InvalidOperationException){rejected=true;}
            Require(rejected&&c.life.cash==cash&&c.Contract(db,p.id).parent=="c1","Option budget undercounted exact remaining owner salary by one euro.");result["purchaseOptionReservesExactOwnerComplement"]=true;
        }
        if(only==null||only=="agentCounterNegotiatesFeeWithoutRaisingParentWage"){
            var(c,db)=Setup();c.ProposeTransfer(db,"p24",1,999999,3,"rotation",true,terms:new MarketTerms{loanWagePercent=40,loanEndDay=90});c.life.day=2;Call(c,"ManagementDay",db);var first=c.world.offers.Last();
            Require(first.status=="counter"&&first.wage==500&&first.fee>1,"Agent counteroffer raised the parent salary instead of negotiating the loan fee.");
            c.ProposeTransfer(db,"p24",first.fee,1,3,"rotation",true,terms:new MarketTerms{loanWagePercent=40,loanEndDay=90});c.life.day=4;Call(c,"ManagementDay",db);var accepted=c.world.offers.Last();Require(accepted.status=="accepted"&&accepted.wage==500,"Revised loan fee failed while unchanged parent salary should satisfy agent.");c.SignTransfer(db,"p24");
            Require(db.Find("p24").wage==500&&c.Contract(db,"p24").originalWage==500&&c.Payroll(db)==12200,"Negotiated loan changed parent wage or borrower salary share.");result["agentCounterNegotiatesFeeWithoutRaisingParentWage"]=true;
        }
        return result;
    }
}


namespace Touchline.Tests
{
    public sealed class LoanSalaryTests
    {
        [NUnit.Framework.TestCase("parentGrossAndShares0")]
        [NUnit.Framework.TestCase("parentGrossAndShares40")]
        [NUnit.Framework.TestCase("parentGrossAndShares100")]
        [NUnit.Framework.TestCase("agentBandAndSubmittedSalaryCannotRaiseGross")]
        [NUnit.Framework.TestCase("legacyInflatedAcceptedLoanRejectedBeforeAccounting")]
        [NUnit.Framework.TestCase("parentRaiseAfterAcceptanceRequiresNewAgreement")]
        [NUnit.Framework.TestCase("invalidSalarySharesRejectBeforeOfferCreation")]
        [NUnit.Framework.TestCase("permanentTransferSalaryStillNegotiable")]
        [NUnit.Framework.TestCase("purchaseOptionReservesExactOwnerComplement")]
        [NUnit.Framework.TestCase("agentCounterNegotiatesFeeWithoutRaisingParentWage")]
        public void ExistingParentContractSalaryAndFinancialSharesStayConsistent(string scenario)
        {
            NUnit.Framework.Assert.That(LoanSalaryChecks.Run(scenario)[scenario],NUnit.Framework.Is.True);
        }
    }
}

using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Touchline.Core;

public static class FinancialCounterpartyChecks
{
    static void Require(bool b,string s){if(!b)throw new InvalidOperationException(s);}
    static object Call(Career c,string name,params object[] args)=>typeof(Career).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(c,args);
    static (Career c,Database db) Setup(){
        var db=new Database{leagues=new[]{new LeagueData{id="fra.1",name="L1"},new LeagueData{id="fra.2",name="L2",tier=2}},clubs=Enumerable.Range(0,8).Select(i=>new ClubData{id="c"+i,name="Club "+i,league=i<4?"fra.1":"fra.2",playable=true,annualRevenue=20000000+i*1000000}).ToArray(),players=Enumerable.Range(0,192).Select(i=>new PlayerData{id="p"+i,name="Alex "+i,team="c"+(i/24),age=24,position=i%24==0||i%24==12?"GB":i%24<8?"DEF":i%24<16?"MIL":"ATT",positions=new[]{i%24==0||i%24==12?"GK":i%24<8?"CB":i%24<16?"CM":"ST"},rating=65,potential=80,value=100000,wage=500,attributes=new[]{new AttributeValue{key="shortPassing",value=65}}}).ToArray()};
        var c=new Career{club="c0"};c.lineup=Career.Select(db,c.club,c.tactic);c.EnsureWorld(db);return(c,db);
    }
    static AiClubAccount Account(Career c,string id)=>c.world.aiAccounts.First(a=>a.club==id);
    static long Cash(Career c)=>c.life.cash+c.world.aiAccounts.Where(a=>a.club!=c.club).Sum(a=>a.cash);
    static void SettleForecast(Career c,Database db,params string[] ids){foreach(var id in ids)if(id!=c.club)Call(c,"ProjectAiOperatingPeriod",db.clubs.First(t=>t.id==id),Account(c,id),db.Squad(id),c.life.day);}
    static void Offer(Career c,string id,bool loan=false,MarketTerms terms=null,long fee=120000,long wage=600){c.world.offers.Add(new TransferOffer{player=id,seller="c1",destination=c.club,status="accepted",fee=fee,wage=wage,years=3,role="rotation",loan=loan,terms=terms??new MarketTerms()});}
    public static Dictionary<string,object> Run(string only=null){
        var result=new Dictionary<string,object>();
        if(only==null||only=="outgoingLoanPrincipalConserved")
        {
            var(c,db)=Setup();long total=Cash(c),own=c.life.cash,other=Account(c,"c1").cash;
            c.outgoingLoans.Add(new OutgoingLoanOffer{player="p10",owner="c0",borrower="c1",status="accepted",fee=50000,terms=new MarketTerms{loanWagePercent=40,loanEndDay=100}});c.SignOutgoingLoan(db,"p10");
            Require(Cash(c)==total&&c.life.cash==own+50000&&Account(c,"c1").cash==other-50000,"Outgoing loan indemnity lacks equal opposing cash entries.");
            Require(Account(c,"c1").projectedGrossPayroll==12200*52,"Borrower's new loan terms not captured after signature.");result["outgoingLoanPrincipalConserved"]=true;
        }
        if(only==null||only=="transferInstalmentAfterManagerChange")
        {
            var(c,db)=Setup();long total=Cash(c),other=Account(c,"c1").cash;Offer(c,"p24",terms:new MarketTerms{upfrontPercent=50,instalments=2});c.SignTransfer(db,"p24");
            Require(Cash(c)==total-1200&&Account(c,"c1").cash==other+60000,"Transfer fee lost seller receipt or mixed agent cost into it.");
            Require(c.payments.Count==2&&c.payments.All(p=>p.counterparty=="c1"&&p.amount==30000),"Instalments lack persistent seller identity.");
            c.approaches.Add(new JobApproach{club="c2",until=14});c.AnswerApproach(db,"c2",true);c.life.day=90;SettleForecast(c,db,"c0","c1");total=Cash(c);long current=c.life.cash;other=Account(c,"c1").cash;Call(c,"MarketDay",db);
            Require(Cash(c)==total&&c.life.cash==current&&Account(c,"c1").cash==other+30000&&c.payments[0].settled,"Old club's instalment was abandoned after its manager left.");
            total=Cash(c);Call(c,"MarketDay",db);Require(Cash(c)==total,"Instalment paid twice on the same day.");result["transferInstalmentAfterManagerChange"]=true;
        }
        if(only==null||only=="salePrincipalConserved")
        {
            var(c,db)=Setup();long total=Cash(c),other=Account(c,"c1").cash;c.world.offers.Add(new TransferOffer{player="p10",seller="c1",fee=100000,status="sale",due=7});c.AcceptSale(db,"p10");
            Require(Cash(c)==total&&Account(c,"c1").cash==other-100000,"Accepted sale created money without charging its buyer.");result["salePrincipalConserved"]=true;
        }
        if(only==null||only=="optionPrincipalConserved")
        {
            var(c,db)=Setup();Offer(c,"p24",true,new MarketTerms{loanWagePercent=40,loanEndDay=100,optionFee=100000},1000,500);c.SignTransfer(db,"p24");long total=Cash(c),other=Account(c,"c1").cash;c.ExerciseLoanOption(db,"p24");
            Require(Cash(c)==total&&Account(c,"c1").cash==other+100000&&c.Contract(db,"p24").parent==null,"Loan option lost owner receipt.");result["optionPrincipalConserved"]=true;
        }
        if(only==null||only=="mandatoryPurchasePrincipalConserved")
        {
            var(c,db)=Setup();c.outgoingLoans.Add(new OutgoingLoanOffer{player="p10",owner="c0",borrower="c1",status="accepted",fee=0,terms=new MarketTerms{loanWagePercent=40,loanEndDay=100,obligationFee=100000}});c.SignOutgoingLoan(db,"p10");c.life.day=100;SettleForecast(c,db,"c1");long total=Cash(c),other=Account(c,"c1").cash;Call(c,"ResolveLoans",db);
            Require(Cash(c)==total&&Account(c,"c1").cash==other-100000&&c.Contract(db,"p10").parent==null,"Mandatory purchase created money or failed ownership change.");result["mandatoryPurchasePrincipalConserved"]=true;
        }
        if(only==null||only=="insolventBuyerRejectsWithoutMutation")
        {
            var(c,db)=Setup();var a=Account(c,"c1");a.cash=0;long total=Cash(c);int ledger=a.ledger.Count;
            c.world.offers.Add(new TransferOffer{player="p10",seller="c1",fee=100000,status="sale",due=7});bool rejected=false;try{c.AcceptSale(db,"p10");}catch(InvalidOperationException){rejected=true;}
            Require(rejected&&Cash(c)==total&&a.ledger.Count==ledger&&db.Find("p10").team=="c0","Insolvent buyer rejection changed balances or ownership.");result["insolventBuyerRejectsWithoutMutation"]=true;
        }
        if(only==null||only=="mandatoryNpcPurchaseRecordsEqualOverdraft")
        {
            var(c,db)=Setup();c.outgoingLoans.Add(new OutgoingLoanOffer{player="p10",owner="c0",borrower="c1",status="accepted",fee=0,terms=new MarketTerms{loanWagePercent=40,loanEndDay=100,obligationFee=100000}});c.SignOutgoingLoan(db,"p10");
            c.approaches.Add(new JobApproach{club="c2",until=14});c.AnswerApproach(db,"c2",true);c.life.day=100;SettleForecast(c,db,"c0","c1");var borrower=Account(c,"c1");borrower.cash=0;long total=Cash(c),debt=borrower.operatingDebt,current=c.life.cash;Call(c,"ResolveLoans",db);
            Require(borrower.operatingDebt==debt+100000&&borrower.cash==0&&Cash(c)==total+100000&&c.life.cash==current,"Mandatory NPC-to-NPC purchase failed cash/debt conservation or touched current manager cash.");
            result["mandatoryNpcPurchaseRecordsEqualOverdraft"]=true;
        }
        if(only==null||only=="legacyRecipientRecoveredOnlyFromUniqueAgreement")
        {
            var(c,db)=Setup();Offer(c,"p24",terms:new MarketTerms{upfrontPercent=50,instalments=2});c.SignTransfer(db,"p24");foreach(var p in c.payments)p.counterparty=null;
            c.life.day=90;SettleForecast(c,db,"c1");long total=Cash(c),other=Account(c,"c1").cash;Call(c,"MarketDay",db);
            Require(Cash(c)==total&&Account(c,"c1").cash==other+30000&&c.payments[0].counterparty=="c1"&&!c.payments[0].legacyUnpaired,"Unambiguous saved seller agreement was not restored for a legacy instalment.");
            c.payments.Add(new PaymentDue{player="unknown-player",club=c.club,amount=100,due=90,label="Legacy unknown"});total=Cash(c);Call(c,"MarketDay",db);
            Require(Cash(c)==total-100&&c.payments.Last().legacyUnpaired&&c.payments.Last().settled,"Unknown legacy recipient was invented or silently presented as paired.");
            result["legacyRecipientRecoveredOnlyFromUniqueAgreement"]=true;
        }
        if(only==null||only=="actualDailySeasonSettlesInvoicesAndLoanReturn")
        {
            var(c,db)=Setup();Offer(c,"p24",terms:new MarketTerms{upfrontPercent=50,instalments=2});c.SignTransfer(db,"p24");
            c.outgoingLoans.Add(new OutgoingLoanOffer{player="p10",owner="c0",borrower="c1",status="accepted",fee=50000,terms=new MarketTerms{loanWagePercent=40,loanEndDay=100}});c.SignOutgoingLoan(db,"p10");c.world.managerStatus="dismissed";c.world.jobDay=0;
            while(c.world.year==2026){int before=c.life.day;c.AdvanceDay(db);Require(c.life.day==before+1,"Daily transfer scenario skipped calendar days.");}
            Require(c.payments.All(p=>p.settled)&&db.Find("p10").team=="c0"&&c.Contract(db,"p10").parent==null,"Instalments/loan return did not finish during an actual season.");
            var seller=Account(c,"c1");long dues=seller.ledger.Where(e=>e.label.StartsWith("Échéance de transfert")&&e.label.EndsWith("encaissement")).Sum(e=>e.amount);
            Require(dues==60000,"Actual daily season did not pay both seller instalments.");
            result["actualDailySeasonSettlesInvoicesAndLoanReturn"]=true;
        }
        if(only==null||only=="signatureAndOptionRefuseBeforeExternalClosure")
        {
            var(c,db)=Setup();Offer(c,"p24",fee:0,wage:500);c.life.cash=900;long cash=Cash(c);int entries=Account(c,"c1").ledger.Count;bool rejected=false;try{c.SignTransfer(db,"p24");}catch(InvalidOperationException){rejected=true;}
            Require(rejected&&Cash(c)==cash&&Account(c,"c1").ledger.Count==entries&&db.Find("p24").team=="c1","Insufficient agent cash closed an external period before refusal.");
            var(d,dd)=Setup();Offer(d,"p24",true,new MarketTerms{loanWagePercent=40,loanEndDay=100,optionFee=100000},0,500);d.SignTransfer(dd,"p24");d.life.cash=0;cash=Cash(d);entries=Account(d,"c1").ledger.Count;rejected=false;try{d.ExerciseLoanOption(dd,"p24");}catch(InvalidOperationException){rejected=true;}
            Require(rejected&&Cash(d)==cash&&Account(d,"c1").ledger.Count==entries&&d.Contract(dd,"p24").parent=="c1","Rejected option changed counterparty accounting.");result["signatureAndOptionRefuseBeforeExternalClosure"]=true;
        }
        if(only==null||only=="legacyAccountListInitializedBeforePayment")
        {
            var(c,db)=Setup();c.world.aiAccounts=null;Offer(c,"p24",fee:100000,wage:500);c.SignTransfer(db,"p24");
            Require(c.world.aiAccounts.Count==8&&c.world.aiAccounts.All(a=>a.hasOperatingSnapshot)&&Account(c,"c1").cash==2520000+100000,"Old save missing account list failed initialization or lost seller credit.");result["legacyAccountListInitializedBeforePayment"]=true;
        }
        return result;
    }
}

namespace Touchline.Tests
{
    public sealed class FinancialCounterpartyTests
    {
        [NUnit.Framework.TestCase("outgoingLoanPrincipalConserved")]
        [NUnit.Framework.TestCase("transferInstalmentAfterManagerChange")]
        [NUnit.Framework.TestCase("salePrincipalConserved")]
        [NUnit.Framework.TestCase("optionPrincipalConserved")]
        [NUnit.Framework.TestCase("mandatoryPurchasePrincipalConserved")]
        [NUnit.Framework.TestCase("insolventBuyerRejectsWithoutMutation")]
        [NUnit.Framework.TestCase("mandatoryNpcPurchaseRecordsEqualOverdraft")]
        [NUnit.Framework.TestCase("legacyRecipientRecoveredOnlyFromUniqueAgreement")]
        [NUnit.Framework.TestCase("actualDailySeasonSettlesInvoicesAndLoanReturn")]
        [NUnit.Framework.TestCase("signatureAndOptionRefuseBeforeExternalClosure")]
        [NUnit.Framework.TestCase("legacyAccountListInitializedBeforePayment")]
        public void SignedAgreementsConservePrincipalAndRejectSafely(string scenario)
        {
            NUnit.Framework.Assert.That(FinancialCounterpartyChecks.Run(scenario)[scenario],NUnit.Framework.Is.True);
        }
    }
}

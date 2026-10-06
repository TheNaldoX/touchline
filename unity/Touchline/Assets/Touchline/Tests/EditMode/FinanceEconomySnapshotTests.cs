using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Touchline.Core;

public static class FinanceCandidateChecks
{
    static object Invoke(Career c,string name,params object[] args)=>typeof(Career).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(c,args);
    static void Require(bool ok,string text){if(!ok)throw new InvalidOperationException(text);}
    static Career Setup()=>new Career{club="observed",life=new ClubLife{day=0},world=new CareerWorld{managerStatus="dismissed"}};
    static AiClubAccount Snapshot(Career c,ClubData t,long wage,long cash=0,long debt=0){var a=new AiClubAccount{club=t.id,cash=cash,operatingDebt=debt};c.world.aiAccounts.Add(a);Invoke(c,"CaptureAiOperatingSnapshot",t,a,wage);return a;}
    public static Dictionary<string,object> Run(string only=null)
    {
        var result=new Dictionary<string,object>();
        if(only==null||only=="pastPeriodVsNextPeriod"){
            var c=Setup();var t=new ClubData{id="npc",league="fra.1",annualRevenue=33000000};
            var a=Snapshot(c,t,100000);t.league="fra.2";t.annualRevenue=21000000;c.life.day=365;
            Invoke(c,"ProjectAiOperatingPeriod",t,a,new List<PlayerData>{new PlayerData{wage=1}},365);
            long revenue=a.ledger.First(e=>e.label.StartsWith("Projection annuelle")).amount;
            long gross=-a.ledger.First(e=>e.label.StartsWith("Salaires bruts")).amount;
            long contributions=-a.ledger.First(e=>e.label.StartsWith("Cotisations")).amount;
            Require(revenue==(long)(33000000d*365/365.25),"Relegation rewrote last-year revenue.");
            Require(gross==(long)(5200000d*365/365.25),"New/retired roster rewrote past-year wages.");
            Require(contributions==(long)(Career.EmploymentProjection("fra.1",5200000).playerContributions*365/365.25),"French next-division charges used retroactively.");
            Require(contributions!=(long)(Career.EmploymentProjection("fra.2",5200000).playerContributions*365/365.25),"Test did not distinguish French divisions.");
            Invoke(c,"CaptureAiOperatingSnapshot",t,a,50000);c.life.day=730;
            Invoke(c,"ProjectAiOperatingPeriod",t,a,new List<PlayerData>(),730);
            Require(a.ledger.Last(e=>e.label.StartsWith("Projection annuelle")).amount==(long)(21000000d*365/365.25),"New season failed to use new revenue.");
            result["pastPeriodVsNextPeriod"]=true;
        }
        if(only==null||only=="futureDebtAndCommittedCashBudget"){
            var c=Setup();var t=new ClubData{id="npc",league="fra.2",annualRevenue=20000000};
            var a=Snapshot(c,t,50000,0,0);long healthy=(long)Invoke(c,"AiGrossWageCeiling",t);
            a.operatingDebt=50000000;long indebted=(long)Invoke(c,"AiGrossWageCeiling",t);
            Require(indebted<healthy,"Debt did not reduce a new-contract budget.");
            c.payments.Add(new PaymentDue{club=t.id,amount=1000000,due=100});long due=(long)Invoke(c,"AiGrossWageCeiling",t);
            Require(due<indebted,"Unfunded transfer instalment did not reserve spending.");
            a.cash=100000000;long funded=(long)Invoke(c,"AiGrossWageCeiling",t);
            Require(funded==indebted,"Fully funded one-off instalment was charged twice against recurring wages.");
            Require(a.operatingDebt==50000000&&a.cash==100000000,"Budget query changed cash or erased debt.");
            result["futureDebtAndCommittedCashBudget"]=true;
        }
        if(only==null||only=="deficitInterestAndDebtConservation"){
            var c=Setup();var t=new ClubData{id="npc",league="fra.2",annualRevenue=10000000};
            var a=Snapshot(c,t,300000,1000000,7000000);c.life.day=365;
            Invoke(c,"ProjectAiOperatingPeriod",t,a,new List<PlayerData>(),365);
            long borrowed=a.ledger.Where(e=>e.label.StartsWith("Dette d'exploitation")).Sum(e=>e.amount);
            Require(borrowed>0&&a.operatingDebt==7000000+borrowed,"Deficit funding lacks equal recorded liability.");
            Require(a.cash==1000000+a.ledger.Sum(e=>e.amount),"Operating ledger does not conserve cash.");
            Require(a.externalFunding==borrowed,"Borrowed funds could appear as distributable profit.");
            result["deficitInterestAndDebtConservation"]=true;
        }
        if(only==null||only=="legacyMigrationPreservesCashDebtAndDates"){
            var c=Setup();c.life.day=100;var t=new ClubData{id="npc",league="fra.2",annualRevenue=10000000};
            var a=new AiClubAccount{club="npc",cash=12345,openingCash=23456,operatingDebt=7654321,projectedFromDay=0};c.world.aiAccounts.Add(a);
            var db=new Database{clubs=new[]{t},players=new[]{new PlayerData{id="p",team="npc",wage=1000}}};
            c.EnsureAiClubAccounts(db);Require(a.hasOperatingSnapshot&&a.legacySnapshotEstimate,"Old save snapshot migration is not explicit.");
            Require(a.cash==12345&&a.openingCash==23456&&a.operatingDebt==7654321&&a.projectedFromDay==0,"Migration reset historic obligations/accounting.");
            c.EnsureAiClubAccounts(db);Require(a.cash==12345&&a.operatingDebt==7654321,"Migration is not idempotent.");
            result["legacyMigrationPreservesCashDebtAndDates"]=true;
        }
        if(only==null||only=="activeManagerEmploymentProtected"){
            var c=Setup();c.club="observed";c.world.managerStatus="employed";c.world.year=2027;c.life.day=365;c.life.cash=3000000;
            var t=new ClubData{id="observed",league="fra.1",annualRevenue=10000000};var p=new PlayerData{id="p",team="observed",wage=5000};
            var db=new Database{clubs=new[]{t},players=new[]{p}};c.EnsureAiClubAccounts(db);c.world.aiAccounts[0].settledYear=2026;
            long until=c.world.contracts[0].until;Invoke(c,"AiSummerEconomy",db);
            Require(p.wage==5000&&p.team=="observed"&&c.world.contracts[0].until==until,"AI rewrote the employed manager's player contracts.");
            result["activeManagerEmploymentProtected"]=true;
        }
        if(only==null||only=="inheritedContractHonoredUntilExpiry"){
            var c=Setup();c.world.year=2027;c.life.day=365;
            var t=new ClubData{id="npc",league="fra.2",annualRevenue=20000000};Snapshot(c,t,100000,0,50000000);
            var p=new PlayerData{id="p",name="Test Example",team="npc",nationality="France",position="MIL",positions=new[]{"CM"},age=25,rating=70,potential=70,wage=100000};
            var contract=new Employment{player=p.id,club=t.id,wage=p.wage,until=370};c.world.contracts.Add(contract);
            c.world.developmentReferences.Add(new ClubDevelopmentReference{club=t.id,rating=70,wage=100000,value=1000000,revenue=20000000,squadSize=22});
            var db=new Database{clubs=new[]{t},players=new[]{p}};
            Invoke(c,"AnnualPlayerDevelopment",db);
            Require(contract.aiRelease&&contract.until==370&&p.wage==100000&&p.team=="npc","Unaffordable inherited contract was cut or terminated before its expiry.");
            c.life.day=370;Invoke(c,"ProcessAiEmployment",db);
            Require(p.team=="free"&&contract.club=="free","Announced non-renewal failed to occur at actual expiry.");
            result["inheritedContractHonoredUntilExpiry"]=true;
        }
        if(only==null||only=="managerHandoverStartsWithCurrentTerms"){
            var db=new Database{
                leagues=new[]{new LeagueData{id="fra.1",name="L1"},new LeagueData{id="fra.2",name="L2",tier=2}},
                clubs=Enumerable.Range(0,8).Select(i=>new ClubData{id="c"+i,name="Club "+i,league=i<4?"fra.1":"fra.2",playable=true,annualRevenue=20000000+i*1000000}).ToArray(),
                players=Enumerable.Range(0,192).Select(i=>new PlayerData{id="p"+i,name="Alex "+i,team="c"+(i/24),age=24,position=i%24==0||i%24==12?"GB":i%24<8?"DEF":i%24<16?"MIL":"ATT",positions=new[]{i%24==0||i%24==12?"GK":i%24<8?"CB":i%24<16?"CM":"ST"},rating=65,potential=80,value=100000,wage=500,attributes=new[]{new AttributeValue{key="shortPassing",value=65}}}).ToArray()
            };
            var c=new Career{club="c0"};c.lineup=Career.Select(db,c.club,c.tactic);c.EnsureWorld(db);c.life.day=100;c.life.cash=12000000;c.world.debt=700000;
            var t=db.clubs.First(x=>x.id=="c0");t.annualRevenue=30000000;
            var p=db.Find("p0");p.wage=2500;var contract=c.Contract(db,p.id);contract.parent="c2";contract.loanUntil=300;contract.parentUntil=600;contract.terms=new MarketTerms{loanWagePercent=40,loanEndDay=300};
            foreach(var f in c.life.facilities){if(f.kind=="training")f.level=4;if(f.kind=="academy")f.level=3;}
            long payroll=c.Payroll(db);Require(payroll==12500,"Handover fixture did not apply the actual loan wage share.");var a=c.world.aiAccounts.First(x=>x.club=="c0");
            c.approaches.Add(new JobApproach{club="c1",until=114});c.AnswerApproach(db,"c1",true);
            Require(a.projectedFromDay==100&&a.projectedGrossPayroll==payroll*52&&a.projectedRevenue==30000000&&a.projectedLeague=="fra.1"&&a.projectedFacilityLevels==7,"Handover reused a stale pre-manager operating snapshot.");
            Require(a.cash==12000000&&a.operatingDebt==700000,"Handover reset funds or debt already paid through the daily ledger.");
            c.life.day=130;int at=a.ledger.Count;Invoke(c,"ProjectAiOperatingPeriod",t,a,db.Squad("c0"),130);
            Require(a.ledger.Skip(at).First(e=>e.label.StartsWith("Projection annuelle")).amount==(long)(30000000d*30/365.25),"Handover charged the period before the manager's departure twice.");
            c.approaches.Add(new JobApproach{club="c0",until=144});c.AnswerApproach(db,"c0",true);
            p.wage=4000;t.annualRevenue=28000000;c.life.day=160;c.life.cash=11000000;long updatedPayroll=c.Payroll(db);
            foreach(var f in c.life.facilities)if(f.kind=="academy")f.level=4;
            c.approaches.Add(new JobApproach{club="c3",until=174});c.AnswerApproach(db,"c3",true);
            Require(a.projectedFromDay==160&&a.projectedGrossPayroll==updatedPayroll*52&&a.projectedRevenue==28000000&&a.projectedFacilityLevels==8&&a.cash==11000000,"Returning to a club and leaving again reused a stale snapshot.");
            result["managerHandoverStartsWithCurrentTerms"]=true;
        }
        if(only==null||only=="loanPayrollConservedAtEveryShareAndReturn"){
            foreach(int percent in new[]{0,40,100}){
                var c=Setup();var owner=new ClubData{id="owner",league="fra.1",annualRevenue=10000000};var borrower=new ClubData{id="borrower",league="fra.2",annualRevenue=10000000};
                var p=new PlayerData{id="loan",name="Borrowed Example",team="borrower",wage=1001};
                var e=new Employment{player=p.id,club="borrower",parent="owner",wage=1001,originalWage=1001,parentUntil=600,loanUntil=20,terms=new MarketTerms{loanWagePercent=percent,loanEndDay=20}};c.world.contracts.Add(e);
                var db=new Database{clubs=new[]{owner,borrower},players=new[]{p}};c.EnsureAiClubAccounts(db);
                var oa=c.world.aiAccounts.First(a=>a.club=="owner");var ba=c.world.aiAccounts.First(a=>a.club=="borrower");long borrowed=1001L*percent/100;
                Require(oa.projectedGrossPayroll==(1001-borrowed)*52&&ba.projectedGrossPayroll==borrowed*52,"NPC snapshots ignored a signed loan wage share.");
                c.club="owner";long ownerPaid=c.Payroll(db);c.club="borrower";long borrowerPaid=c.Payroll(db);
                Require(ownerPaid==1001-borrowed&&borrowerPaid==borrowed&&ownerPaid+borrowerPaid==1001,"Loan split created/lost wages, including integer rounding.");
                c.life.day=20;Invoke(c,"ResolveLoans",db);c.club="owner";Require(c.Payroll(db)==1001,"Returning loan did not restore owner's full wage.");c.club="borrower";Require(c.Payroll(db)==0,"Returning loan left borrower paying an absent player.");
                e.parent="owner";e.club="borrower";p.team="borrower";e.terms=null;e.loanUntil=100;c.club="owner";ownerPaid=c.Payroll(db);c.club="borrower";borrowerPaid=c.Payroll(db);
                Require(ownerPaid==501&&borrowerPaid==500,"Legacy loan without clauses charged inconsistent default 50/50 shares.");
            }
            result["loanPayrollConservedAtEveryShareAndReturn"]=true;
        }
        return result;
    }
}
namespace Touchline.Tests
{
    public class FinanceEconomySnapshotTests
    {
        [NUnit.Framework.TestCase("pastPeriodVsNextPeriod")]
        [NUnit.Framework.TestCase("futureDebtAndCommittedCashBudget")]
        [NUnit.Framework.TestCase("deficitInterestAndDebtConservation")]
        [NUnit.Framework.TestCase("legacyMigrationPreservesCashDebtAndDates")]
        [NUnit.Framework.TestCase("activeManagerEmploymentProtected")]
        [NUnit.Framework.TestCase("inheritedContractHonoredUntilExpiry")]
        [NUnit.Framework.TestCase("managerHandoverStartsWithCurrentTerms")]
        [NUnit.Framework.TestCase("loanPayrollConservedAtEveryShareAndReturn")]
        public void HistoricalAndFutureOperatingTermsRemainCoherent(string check)
        {
            NUnit.Framework.Assert.AreEqual(true,FinanceCandidateChecks.Run(check)[check]);
        }
    }
}
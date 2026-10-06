using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Touchline.Core;

public static class LoanRetirementChecks
{
    static void Require(bool b,string s){if(!b)throw new InvalidOperationException(s);}
    static object Call(Career c,string name,params object[] args)=>typeof(Career).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(c,args);
    static (Career c,Database db) Setup(){
        var db=new Database{leagues=new[]{new LeagueData{id="fra.1",name="L1"},new LeagueData{id="fra.2",name="L2",tier=2}},clubs=Enumerable.Range(0,8).Select(i=>new ClubData{id="c"+i,name="Club "+i,league=i<4?"fra.1":"fra.2",playable=true,annualRevenue=20000000+i*1000000}).ToArray(),players=Enumerable.Range(0,192).Select(i=>new PlayerData{id="p"+i,name="Alex "+i,team="c"+(i/24),age=24,position=i%24==0||i%24==12?"GB":i%24<8?"DEF":i%24<16?"MIL":"ATT",positions=new[]{i%24==0||i%24==12?"GK":i%24<8?"CB":i%24<16?"CM":"ST"},rating=65,potential=80,value=100000,wage=500,attributes=new[]{new AttributeValue{key="shortPassing",value=65}}}).ToArray()};
        var c=new Career{club="c0"};c.lineup=Career.Select(db,c.club,c.tactic);c.EnsureWorld(db);return(c,db);
    }
    static Dictionary<string,long> Payrolls(Career c,Database db)=>(Dictionary<string,long>)Call(c,"ClubWeeklyPayrolls",db);
    static void Advance(Career c,Database db,int day){while(c.life.day<day)c.AdvanceDay(db);}
    static Employment Incoming(Career c,Database db,string mode,int retirement=7){
        c.ProposeTransfer(db,"p24",1000,500,3,"rotation",true,terms:new MarketTerms{loanWagePercent=40,loanEndDay=28,optionFee=mode=="option"?10000:0,obligationFee=mode=="obligation"?10000:0});c.world.offers.Last().status="accepted";c.SignTransfer(db,"p24");var e=c.Contract(db,"p24");e.retirement=retirement;return e;
    }
    static void Terminal(Career c,Database db,Employment e){
        Require(db.Find(e.player).team=="retired"&&e.club=="retired"&&e.parent==null&&e.loanUntil==0&&e.parentConditions==null&&e.purchaseConditions==null,"Retirement did not terminate ownership and loan commitments.");
        Require(e.terms==null||e.terms.obligationFee==0&&e.terms.optionFee==0&&e.terms.loanEndDay==0,"Retired loan retained an executable purchase clause.");
        Require(db.Find(e.player).wage==500&&e.wage==500,"Retirement erased the historical salary instead of excluding it from payroll.");
    }
    public static Dictionary<string,object> Run(string only=null){
        var result=new Dictionary<string,object>();
        foreach(string mode in new[]{"normal","option","obligation"})if(only==null||only=="terminalIncoming"+mode){
            var(c,db)=Setup();var e=Incoming(c,db,mode);c.Person("p24").appearances=17;var attrs=db.Find("p24").attributes;var history=new Fixture{id="past",played=true,hg=2,ag=1,home="c0",away="c1"};var honour=new Honour{club="c0",competition="cup",year=2025};c.world.history.Add(history);c.world.honours.Add(honour);
            Advance(c,db,7);Terminal(c,db,e);Require(c.Payroll(db)==12000&&Payrolls(c,db)["c1"]==11500,"Retirement still charged owner or borrower salary.");
            Require(c.life.retiredPlayers.Single(x=>x.id=="p24").appearances==17&&!c.life.players.Any(x=>x.id=="p24"),"Known appearances were lost or retired player remained in active training.");
            long paid=c.world.transferSpent;Require(paid==1000,"Loan fee accounting fixture changed.");Advance(c,db,28);Terminal(c,db,e);
            Require(c.world.transferSpent==paid&&ReferenceEquals(db.Find("p24").attributes,attrs)&&c.world.history.Contains(history)&&c.world.honours.Contains(honour),"Loan expiry bought/refunded a retired player or erased retained identity/history.");
            Require(c.life.retiredPlayers.Count(x=>x.id=="p24")==1,"Repeated retirement duplicated known appearances.");
            result["terminalIncoming"+mode]=true;
        }
        if(only==null||only=="purchaseBeforeRetirementRemainsPaid"){
            var(c,db)=Setup();var e=Incoming(c,db,"option");Advance(c,db,3);c.ExerciseLoanOption(db,"p24");Require(c.world.transferSpent==11000,"Fixture did not purchase the player before retirement.");Advance(c,db,28);Terminal(c,db,e);Require(c.world.transferSpent==11000&&c.Payroll(db)==12000&&Payrolls(c,db)["c1"]==11500,"Retirement reversed a completed purchase or retained employment payroll.");result["purchaseBeforeRetirementRemainsPaid"]=true;
        }
        if(only==null||only=="outgoingRetirementClearsBothSalaryShares"){
            var(c,db)=Setup();var e=c.Contract(db,"p10");c.ProposeOutgoingLoan(db,"p10","c1",1000,new MarketTerms{loanWagePercent=40,loanEndDay=28,obligationFee=10000});c.outgoingLoans.Last().status="accepted";c.SignOutgoingLoan(db,"p10");e.retirement=7;
            Require(c.Payroll(db)==11800&&Payrolls(c,db)["c1"]==12200,"Outgoing fixture salary split is wrong.");Advance(c,db,7);Terminal(c,db,e);Require(c.Payroll(db)==11500&&Payrolls(c,db)["c1"]==12000,"Outgoing retirement retained a salary liability.");long receipt=c.life.ledger.Where(x=>x.label.Contains("Indemnité de prêt")).Sum(x=>x.amount);Advance(c,db,28);Terminal(c,db,e);Require(receipt==1000&&!c.life.ledger.Any(x=>x.label.Contains("obligation")||x.label.Contains("rembours")),"Outgoing retirement exercised the obligation or refunded paid fees.");result["outgoingRetirementClearsBothSalaryShares"]=true;
        }
        if(only==null||only=="sameDayRetirementPrecedesMandatoryPurchase"){
            var(c,db)=Setup();var e=Incoming(c,db,"obligation",28);e.parentConditions.until=28;e.parentUntil=28;Advance(c,db,28);Terminal(c,db,e);Require(c.world.transferSpent==1000,"Same-day retirement purchased or released the player before closing his career.");result["sameDayRetirementPrecedesMandatoryPurchase"]=true;
        }
        if(only==null||only=="realParentExpiryAfterReturnBecomesFree"){
            var(c,db)=Setup();var e=c.Contract(db,"p10");e.until=28;c.ProposeOutgoingLoan(db,"p10","c1",0,new MarketTerms{loanWagePercent=40,loanEndDay=28});c.outgoingLoans.Last().status="accepted";c.SignOutgoingLoan(db,"p10");Advance(c,db,28);Require(db.Find("p10").team=="free"&&e.club=="free"&&e.parent==null&&e.retirement<0,"Parent expiry was mistaken for retirement or loan return extended an expired contract.");Require(!c.life.retiredPlayers.Any(x=>x.id=="p10"),"Expiry invented retired appearances.");result["realParentExpiryAfterReturnBecomesFree"]=true;
        }
        if(only==null||only=="legacyRetiredPendingObligationDoesNotInventTransaction"){
            var(c,db)=Setup();var p=db.Find("p24");var e=c.Contract(db,p.id);p.team=e.club="retired";e.parent="c1";e.loanUntil=28;e.terms=new MarketTerms{obligationFee=10000};c.life.day=28;long cash=c.life.cash,spent=c.world.transferSpent;var npc=c.world.aiAccounts.Select(a=>a.cash).ToArray();Call(c,"ResolveLoans",db);Terminal(c,db,e);Require(c.life.cash==cash&&c.world.transferSpent==spent&&npc.SequenceEqual(c.world.aiAccounts.Select(a=>a.cash)),"Legacy retired contract invented a retroactive payment/refund.");result["legacyRetiredPendingObligationDoesNotInventTransaction"]=true;
        }
        if(only==null||only=="knownArchiveRoundtripAndLegacyNull"){
            var(c,db)=Setup();c.life.retiredPlayers=null;var e=Incoming(c,db,"normal");c.Person("p24").appearances=23;Advance(c,db,7);Call(c,"RetireEmployment",db,db.Find("p24"),e,true);Require(c.life.retiredPlayers.Count(x=>x.id=="p24")==1,"Repeated archive duplicated a known player.");
            var json=UnityEngine.JsonUtility.ToJson(c.life);var restored=UnityEngine.JsonUtility.FromJson<ClubLife>(json);Require(restored.retiredPlayers.Single(x=>x.id=="p24").appearances==23&&!restored.players.Any(x=>x.id=="p24"),"Archive did not survive save roundtrip independently of active players.");
            var unknown=db.Find("p25");var contract=c.Contract(db,unknown.id);Call(c,"RetireEmployment",db,unknown,contract,true);Require(!c.life.retiredPlayers.Any(x=>x.id==unknown.id),"Unknown NPC appearances were fabricated.");result["knownArchiveRoundtripAndLegacyNull"]=true;
        }
        if(only==null||only=="twoNpcSnapshotsCloseAccrualBeforeRetirement"){
            var(c,db)=Setup();var e=Incoming(c,db,"normal");c.Person("p24").appearances=19;c.approaches.Add(new JobApproach{club="c2",until=14,status="open"});c.AnswerApproach(db,"c2",true);Advance(c,db,7);Terminal(c,db,e);Require(c.previousClubs.Single(x=>x.club=="c0").life.retiredPlayers.Single(x=>x.id=="p24").appearances==19,"Retirement after manager departure discarded known former-club appearances.");
            var borrower=c.world.aiAccounts.Single(a=>a.club=="c0");var owner=c.world.aiAccounts.Single(a=>a.club=="c1");Require(borrower.projectedFromDay==7&&owner.projectedFromDay==7&&borrower.projectedGrossPayroll==12000*52&&owner.projectedGrossPayroll==11500*52,"Retirement did not close both NPC accrual periods before replacing their salary snapshots.");
            long b=borrower.cash,o=owner.cash;int bl=borrower.ledger.Count,ol=owner.ledger.Count;Call(c,"RetireEmployment",db,db.Find("p24"),e,true);Require(borrower.cash==b&&owner.cash==o&&borrower.ledger.Count==bl&&owner.ledger.Count==ol,"Repeated terminal retirement changed NPC cash/ledger.");Advance(c,db,28);Terminal(c,db,e);Require(c.world.transferSpent==0&&c.previousClubs.Single(x=>x.club=="c0").spent==1000,"Manager move lost paid loan fees or retirement duplicated a purchase.");result["twoNpcSnapshotsCloseAccrualBeforeRetirement"]=true;
        }
        if(only==null||only=="annualRetirementArchivesOnlyKnownManagedState"){
            var(c,db)=Setup();var p=db.Find("p10");p.age=40;c.Person(p.id).appearances=29;db.Find("p25").age=40;Call(c,"AnnualPlayerDevelopment",db);Require(p.team=="retired"&&c.life.retiredPlayers.Single(x=>x.id==p.id).appearances==29&&!c.life.players.Any(x=>x.id==p.id),"Annual retirement did not preserve known appearances outside the active squad.");Require(!c.life.retiredPlayers.Any(x=>x.id=="p25"),"Annual NPC retirement fabricated recorded appearances.");result["annualRetirementArchivesOnlyKnownManagedState"]=true;
        }
        if(only==null||only=="permanentExpiryCannotEraseSameDayRetirementArchive"){
            foreach(bool aiRelease in new[]{false,true}){var(c,db)=Setup();var p=db.Find("p10");var e=c.Contract(db,p.id);e.until=e.retirement=7;e.aiRelease=aiRelease;c.Person(p.id).appearances=31;Advance(c,db,7);Terminal(c,db,e);Require(c.life.retiredPlayers.Single(x=>x.id==p.id).appearances==31&&!c.life.messages.Any(x=>x.player==p.id&&x.subject=="Départ en fin de contrat"),"Same-day expiry erased archived appearances or announced a free transfer before retirement.");}
            result["permanentExpiryCannotEraseSameDayRetirementArchive"]=true;
        }
        if(only==null||only=="annualLoanRetirementClosesPendingClausesAndArchivesKnownAppearances"){
            var(c,db)=Setup();var e=Incoming(c,db,"obligation");db.Find("p24").age=40;c.Person("p24").appearances=37;long paid=c.world.transferSpent;Call(c,"AnnualPlayerDevelopment",db);Terminal(c,db,e);Require(c.life.retiredPlayers.Single(x=>x.id=="p24").appearances==37&&c.world.transferSpent==paid&&!c.world.contracts.Any(x=>x.player=="p24"),"Annual loan retirement left pending purchases, lost known appearances or invented a payment.");result["annualLoanRetirementClosesPendingClausesAndArchivesKnownAppearances"]=true;
        }
        return result;
    }
}

namespace Touchline.Tests { public sealed class LoanRetirementTests {
 [NUnit.Framework.TestCase("terminalIncomingnormal")]
 [NUnit.Framework.TestCase("terminalIncomingoption")]
 [NUnit.Framework.TestCase("terminalIncomingobligation")]
 [NUnit.Framework.TestCase("purchaseBeforeRetirementRemainsPaid")]
 [NUnit.Framework.TestCase("outgoingRetirementClearsBothSalaryShares")]
 [NUnit.Framework.TestCase("sameDayRetirementPrecedesMandatoryPurchase")]
 [NUnit.Framework.TestCase("realParentExpiryAfterReturnBecomesFree")]
 [NUnit.Framework.TestCase("legacyRetiredPendingObligationDoesNotInventTransaction")]
 [NUnit.Framework.TestCase("knownArchiveRoundtripAndLegacyNull")]
 [NUnit.Framework.TestCase("twoNpcSnapshotsCloseAccrualBeforeRetirement")]
 [NUnit.Framework.TestCase("annualRetirementArchivesOnlyKnownManagedState")]
 [NUnit.Framework.TestCase("permanentExpiryCannotEraseSameDayRetirementArchive")]
 [NUnit.Framework.TestCase("annualLoanRetirementClosesPendingClausesAndArchivesKnownAppearances")]
public void RetirementTerminatesLoansWithoutLosingKnownHistory(string scenario){NUnit.Framework.Assert.That(LoanRetirementChecks.Run(scenario)[scenario],NUnit.Framework.Is.True);}
}}

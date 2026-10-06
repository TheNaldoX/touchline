using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Touchline.Core;

public static class LoanDeadlineChecks
{
    static void Require(bool b,string s){if(!b)throw new InvalidOperationException(s);}
    static object Call(Career c,string name,params object[] args)=>typeof(Career).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(c,args);
    static (Career c,Database db) Setup(){
        var db=new Database{leagues=new[]{new LeagueData{id="fra.1",name="L1"},new LeagueData{id="fra.2",name="L2",tier=2}},clubs=Enumerable.Range(0,8).Select(i=>new ClubData{id="c"+i,name="Club "+i,league=i<4?"fra.1":"fra.2",playable=true,annualRevenue=20000000+i*1000000}).ToArray(),players=Enumerable.Range(0,192).Select(i=>new PlayerData{id="p"+i,name="Alex "+i,team="c"+(i/24),age=24,position=i%24==0||i%24==12?"GB":i%24<8?"DEF":i%24<16?"MIL":"ATT",positions=new[]{i%24==0||i%24==12?"GK":i%24<8?"CB":i%24<16?"CM":"ST"},rating=65,potential=80,value=100000,wage=500,attributes=new[]{new AttributeValue{key="shortPassing",value=65}}}).ToArray()};
        var c=new Career{club="c0"};c.lineup=Career.Select(db,c.club,c.tactic);c.EnsureWorld(db);return(c,db);
    }
    static string Fingerprint(Career c)=>c.life.cash+"|"+string.Join(";",c.world.aiAccounts.Select(a=>a.club+":"+a.cash+":"+a.projectedFromDay+":"+a.ledger.Count));
    public static Dictionary<string,object> Run(string only=null)
    {
        var result=new Dictionary<string,object>();
        if(only==null||only=="incomingProposalCannotOutlastParent"){
            var(c,db)=Setup();c.Contract(db,"p24").until=30;string before=Fingerprint(c);int offers=c.world.offers.Count;bool rejected=false;try{c.ProposeTransfer(db,"p24",0,500,3,"rotation",true,terms:new MarketTerms{loanWagePercent=40,loanEndDay=31});}catch(InvalidOperationException){rejected=true;}
            Require(rejected&&before==Fingerprint(c)&&c.world.offers.Count==offers&&db.Find("p24").team=="c1","Overlong incoming proposal mutated an offer or financial account.");result["incomingProposalCannotOutlastParent"]=true;
        }
        if(only==null||only=="incomingSignatureRevalidatesParentDeadline"){
            var(c,db)=Setup();var contract=c.Contract(db,"p24");contract.until=30;c.ProposeTransfer(db,"p24",0,500,3,"rotation",true,terms:new MarketTerms{loanWagePercent=40,loanEndDay=28});var offer=c.world.offers.Last();offer.status="accepted";contract.until=20;c.life.day=2;string before=Fingerprint(c);bool rejected=false;try{c.SignTransfer(db,"p24");}catch(InvalidOperationException){rejected=true;}
            Require(rejected&&before==Fingerprint(c)&&db.Find("p24").team=="c1"&&contract.parent==null&&offer.status=="accepted","Changed parent deadline refusal closed accounts or moved player.");result["incomingSignatureRevalidatesParentDeadline"]=true;
        }
        if(only==null||only=="outgoingSignatureRevalidatesParentDeadline"){
            var(c,db)=Setup();var contract=c.Contract(db,"p10");contract.until=30;c.ProposeOutgoingLoan(db,"p10","c1",1000,new MarketTerms{loanWagePercent=40,loanEndDay=28});var offer=c.outgoingLoans.Last();offer.status="accepted";contract.until=20;c.life.day=2;string before=Fingerprint(c);bool rejected=false;try{c.SignOutgoingLoan(db,"p10");}catch(InvalidOperationException){rejected=true;}
            Require(rejected&&before==Fingerprint(c)&&db.Find("p10").team=="c0"&&contract.parent==null&&offer.status=="accepted","Changed outgoing parent deadline refusal paid fee or moved player.");result["outgoingSignatureRevalidatesParentDeadline"]=true;
        }
        if(only==null||only=="acceptedLoanDoesNotRequire28RemainingDays"){
            foreach(bool outgoing in new[]{false,true}){var(c,db)=Setup();string id=outgoing?"p10":"p24";c.Contract(db,id).until=28;if(outgoing){c.ProposeOutgoingLoan(db,id,"c1",0,new MarketTerms{loanWagePercent=40,loanEndDay=28});c.outgoingLoans.Last().status="accepted";}else{c.ProposeTransfer(db,id,0,500,3,"rotation",true,terms:new MarketTerms{loanWagePercent=40,loanEndDay=28});c.world.offers.Last().status="accepted";}c.life.day=7;if(outgoing)c.SignOutgoingLoan(db,id);else c.SignTransfer(db,id);Require(c.Contract(db,id).loanUntil==28&&c.Contract(db,id).parent!=null,"Accepted 28-day loan incorrectly requires another 28 days at signature.");}
            result["acceptedLoanDoesNotRequire28RemainingDays"]=true;
        }
        if(only==null||only=="amendedLoanTermsCannotOutlastParent"){
            var(c,db)=Setup();c.Contract(db,"p24").until=30;c.ProposeTransfer(db,"p24",0,500,3,"rotation",true,terms:new MarketTerms{loanWagePercent=40,loanEndDay=28});var terms=c.world.offers.Last().terms;bool rejected=false;try{c.SetOfferTerms("p24",new MarketTerms{loanWagePercent=40,loanEndDay=31});}catch(InvalidOperationException){rejected=true;}Require(rejected&&object.ReferenceEquals(terms,c.world.offers.Last().terms),"Changing clauses silently accepted an overlong loan.");result["amendedLoanTermsCannotOutlastParent"]=true;
        }
        if(only==null||only=="missingOrElapsedLoanTermsRejectBeforeAccounting"){
            foreach(bool missing in new[]{false,true}){var(c,db)=Setup();c.Contract(db,"p24");c.world.offers.Add(new TransferOffer{player="p24",seller="c1",destination=c.club,status="accepted",fee=1000,wage=500,years=3,role="rotation",loan=true,terms=missing?null:new MarketTerms{loanEndDay=0}});string before=Fingerprint(c);bool rejected=false;try{c.SignTransfer(db,"p24");}catch(InvalidOperationException){rejected=true;}Require(rejected&&before==Fingerprint(c)&&db.Find("p24").team=="c1","Missing or elapsed loan terms changed balances or were silently invented.");}
            result["missingOrElapsedLoanTermsRejectBeforeAccounting"]=true;
        }
        return result;
    }
}

namespace Touchline.Tests
{
    public sealed class LoanDeadlineTests
    {
        [NUnit.Framework.TestCase("incomingProposalCannotOutlastParent")]
        [NUnit.Framework.TestCase("incomingSignatureRevalidatesParentDeadline")]
        [NUnit.Framework.TestCase("outgoingSignatureRevalidatesParentDeadline")]
        [NUnit.Framework.TestCase("acceptedLoanDoesNotRequire28RemainingDays")]
        [NUnit.Framework.TestCase("amendedLoanTermsCannotOutlastParent")]
        [NUnit.Framework.TestCase("missingOrElapsedLoanTermsRejectBeforeAccounting")]
        public void LoanParentDeadlineIsCheckedBeforeMutation(string scenario)
        {
            NUnit.Framework.Assert.That(LoanDeadlineChecks.Run(scenario)[scenario],NUnit.Framework.Is.True);
        }
    }
}

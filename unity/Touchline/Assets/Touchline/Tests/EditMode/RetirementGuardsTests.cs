using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Touchline.Core;

public static class RetirementGuardsChecks
{
    static void Require(bool b,string s){if(!b)throw new InvalidOperationException(s);}
    static object Call(Career c,string name,params object[] args)=>typeof(Career).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(c,args);
    static (Career c,Database db) Setup(){
        var db=new Database{leagues=new[]{new LeagueData{id="fra.1",name="L1"},new LeagueData{id="fra.2",name="L2",tier=2}},clubs=Enumerable.Range(0,8).Select(i=>new ClubData{id="c"+i,name="Club "+i,league=i<4?"fra.1":"fra.2",playable=true,annualRevenue=20000000+i*1000000}).ToArray(),players=Enumerable.Range(0,192).Select(i=>new PlayerData{id="p"+i,name="Alex "+i,team="c"+(i/24),age=24,position=i%24==0||i%24==12?"GB":i%24<8?"DEF":i%24<16?"MIL":"ATT",positions=new[]{i%24==0||i%24==12?"GK":i%24<8?"CB":i%24<16?"CM":"ST"},rating=65,potential=80,value=100000,wage=500,attributes=new[]{new AttributeValue{key="shortPassing",value=65}}}).ToArray()};
        var c=new Career{club="c0"};c.lineup=Career.Select(db,c.club,c.tactic);c.EnsureWorld(db);return(c,db);
    }
    static string FinancialState(Career c)=>UnityEngine.JsonUtility.ToJson(c.life)+"|"+UnityEngine.JsonUtility.ToJson(c.world);
    public static Dictionary<string,object> Run(string only=null){var result=new Dictionary<string,object>();
        if(only==null||only=="retiredOrEffectiveRetirementRejectsNewProposalWithoutMutation"){
            foreach(bool alreadyRetired in new[]{false,true}){var(c,db)=Setup();var p=db.Find("p24");var e=c.Contract(db,p.id);e.retirement=c.life.day;if(alreadyRetired)p.team=e.club="retired";string before=FinancialState(c);bool rejected=false;try{c.ProposeTransfer(db,p.id,1000,700,3,"rotation");}catch(InvalidOperationException){rejected=true;}Require(rejected&&FinancialState(c)==before&&p.wage==500&&p.team==(alreadyRetired?"retired":"c1"),"Retired/effectively retired proposal modified offers, contract, identity or finances.");}result["retiredOrEffectiveRetirementRejectsNewProposalWithoutMutation"]=true;
        }
        if(only==null||only=="retirementAfterAcceptanceRejectsSignatureBeforeAnyPayment"){
            foreach(int legacy in new[]{0,1,2}){var(c,db)=Setup();c.ProposeTransfer(db,"p24",1000,700,3,"rotation");var offer=c.world.offers.Last();offer.status="accepted";var p=db.Find("p24");var e=c.Contract(db,p.id);e.retirement=c.life.day;if(legacy<2){Call(c,"RetireEmployment",db,p,e,true);if(legacy==1)offer.seller="retired";}string before=FinancialState(c);string team=p.team;bool rejected=false;try{c.SignTransfer(db,p.id);}catch(InvalidOperationException){rejected=true;}Require(rejected&&FinancialState(c)==before&&p.team==team&&p.wage==500&&offer.status=="accepted","Signature resurrected a retired player, charged an agent or mutated NPC accounts before rejecting.");}result["retirementAfterAcceptanceRejectsSignatureBeforeAnyPayment"]=true;
        }
        if(only==null||only=="legacyRetiredOptionRejectsBeforePayment"){
            var(c,db)=Setup();var p=db.Find("p24");var e=c.Contract(db,p.id);p.team="retired";e.club=c.club;e.parent="c1";e.loanUntil=28;e.terms=new MarketTerms{optionFee=10000,loanWagePercent=40};string before=FinancialState(c);bool rejected=false;try{c.ExerciseLoanOption(db,p.id);}catch(InvalidOperationException){rejected=true;}Require(rejected&&FinancialState(c)==before&&p.team=="retired","Legacy option resurrected a retired player or charged money before rejecting.");result["legacyRetiredOptionRejectsBeforePayment"]=true;
        }
        if(only==null||only=="effectiveRetirementCancelsScheduledPrecontractWithoutNewWage"){
            var(c,db)=Setup();var p=db.Find("p24");var e=c.Contract(db,p.id);e.retirement=1;c.world.offers.Add(new TransferOffer{player=p.id,seller=p.team,destination=c.club,status="scheduled",precontract=true,joinDay=1,due=0,wage=900,years=3,role="key"});c.AdvanceDay(db);Require(p.team=="retired"&&p.wage==500&&e.wage==500&&c.world.offers.Last().status=="cancelled"&&c.Payroll(db)==12000,"Scheduled precontract changed the last salary or resurrected a player retiring on his joining day.");result["effectiveRetirementCancelsScheduledPrecontractWithoutNewWage"]=true;
        }
        if(only==null||only=="effectiveRetirementRejectsOutgoingLoanSignatureBeforeReceipt"){
            var(c,db)=Setup();c.ProposeOutgoingLoan(db,"p10","c1",1000,new MarketTerms{loanWagePercent=40,loanEndDay=28});var offer=c.outgoingLoans.Last();offer.status="accepted";c.Contract(db,"p10").retirement=c.life.day;string before=FinancialState(c);bool rejected=false;try{c.SignOutgoingLoan(db,"p10");}catch(InvalidOperationException){rejected=true;}Require(rejected&&FinancialState(c)==before&&db.Find("p10").team==c.club&&offer.status=="accepted","Outgoing loan charged the borrower or changed employment after effective retirement.");result["effectiveRetirementRejectsOutgoingLoanSignatureBeforeReceipt"]=true;
        }
        if(only==null||only=="alreadyRetiredScheduledArrivalRemainsCancelled"){
            var(c,db)=Setup();var p=db.Find("p24");var e=c.Contract(db,p.id);Call(c,"RetireEmployment",db,p,e,true);long cash=c.life.cash;var o=new TransferOffer{player=p.id,seller="c1",destination=c.club,status="scheduled",precontract=true,joinDay=0,due=0,wage=900,years=3,role="key"};c.world.offers.Add(o);Call(c,"ActivatePrecontracts",db);Require(p.team=="retired"&&p.wage==500&&e.wage==500&&o.status=="cancelled"&&c.life.cash==cash,"Scheduled arrival reactivated a player who had already retired.");result["alreadyRetiredScheduledArrivalRemainsCancelled"]=true;
        }
        return result;
    }
}

namespace Touchline.Tests { public sealed class RetirementGuardsTests {
 [NUnit.Framework.TestCase("retiredOrEffectiveRetirementRejectsNewProposalWithoutMutation")]
 [NUnit.Framework.TestCase("retirementAfterAcceptanceRejectsSignatureBeforeAnyPayment")]
 [NUnit.Framework.TestCase("legacyRetiredOptionRejectsBeforePayment")]
 [NUnit.Framework.TestCase("effectiveRetirementCancelsScheduledPrecontractWithoutNewWage")]
 [NUnit.Framework.TestCase("effectiveRetirementRejectsOutgoingLoanSignatureBeforeReceipt")]
 [NUnit.Framework.TestCase("alreadyRetiredScheduledArrivalRemainsCancelled")]
public void ACompletedPlayingCareerCannotBeReopened(string scenario){NUnit.Framework.Assert.That(RetirementGuardsChecks.Run(scenario)[scenario],NUnit.Framework.Is.True);}
}}

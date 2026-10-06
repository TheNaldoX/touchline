using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Touchline.Core;

public static class LoanProspectChecks
{
    static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    public static (Career c,Database db) Setup(){
        var clubs=Enumerable.Range(0,18).Select(i=>new ClubData{id="c"+i,name="Club "+i,league="fra.1",playable=true,annualRevenue=20000000}).ToArray();
        var players=Enumerable.Range(0,18*24).Select(i=>new PlayerData{id="p"+i,name="Player "+i,team="c"+(i/24),age=20,position=i%24<8?"DEF":i%24<16?"MIL":"ATT",positions=new[]{i%24==0?"GK":i%24<8?"CB":i%24<16?"CM":"ST"},rating=65,potential=85,value=100000,wage=500}).ToArray();
        var db=new Database{clubs=clubs,players=players,leagues=new[]{new LeagueData{id="fra.1",name="L1"}}};var c=new Career{club="c0"};c.lineup=Career.Select(db,c.club,c.tactic);c.EnsureWorld(db);return(c,db);
    }
    static MarketTerms Terms(int day=100)=>new MarketTerms{loanWagePercent=50,loanEndDay=day};
    public static YouthPath Start(Career c,Database db,string id="p20",int end=100,int appearances=0){
        c.Contract(db,id).until=300;c.ProposeOutgoingLoan(db,id,"c1",0,new MarketTerms{loanWagePercent=50,loanEndDay=end,obligationFee=appearances>0?10000:0,obligationAppearances=appearances});c.outgoingLoans.Last().status="accepted";c.SignOutgoingLoan(db,id);return c.world.youth.Single(y=>y.player==id);
    }
    public static void Fixture(Career c,int day,string id)=>c.world.fixtures.Add(new Fixture{id=id,league="fra.1",home="c1",away="c2",day=day,played=true});
    public static Dictionary<string,bool> Run(string only=null){
        var results=new Dictionary<string,bool>();void Test(string name,Action action){if(only!=null&&only!=name)return;action();results[name]=true;}
        Test("allEligibleClubsBeyondFriendlyTwelve",()=>{var(c,db)=Setup();Require(c.LoanClubRecommendations(db,"p20",Terms()).Count==17,"Loan destinations were truncated to the twelve friendly recommendations.");db.clubs[1].reserve=true;db.players=db.players.Where(p=>p.team!="c2"||int.Parse(p.id.Substring(1))%24<10).ToArray();Require(c.LoanClubRecommendations(db,"p20",Terms()).Count==15,"Reserve or incomplete roster remained loan eligible.");});
        Test("recommendationsFollowPlayerNotParentTeam",()=>{var(c,db)=Setup();db.Find("p20").rating=45;foreach(var p in db.Squad("c1"))p.rating=45;foreach(var p in db.Squad("c0"))if(p.id!="p20")p.rating=90;var ranked=c.LoanClubRecommendations(db,"p20",Terms());Require(ranked[0].club.id=="c1"&&ranked[0].levelCompatible,"Loan recommendations follow the senior team's level instead of the player's.");});
        Test("positionCompetitionChangesMinutes",()=>{var(c,db)=Setup();var p=db.Find("p20");p.rating=70;foreach(var q in db.Squad("c1")){q.positions=new[]{"CB"};q.position="DEF";q.rating=65;}var open=c.LoanClubAssessment(db,p.id,"c1",Terms(),0);foreach(var q in db.Squad("c1").Take(3)){q.positions=new[]{"ST"};q.position="ATT";q.rating=75;}var crowded=c.LoanClubAssessment(db,p.id,"c1",Terms(),0);Require(open.estimatedMinutesPerMatch>crowded.estimatedMinutesPerMatch&&crowded.competition>=3,"Positional competition does not affect the playing-time forecast.");});
        Test("reserveKeeperBehindStarterHasNoInventedSubstitution",()=>{var(c,db)=Setup();db.Find("p0").rating=65;db.Find("p24").rating=70;var path=Start(c,db,"p0");c.world.fixtures.Clear();Fixture(c,2,"keeper-match");c.life.day=2;c.ReviewOutgoingLoanAppearances(db);Require(path.minutes==0&&path.loanAppearances==0,"Reserve keeper was credited artificial substitute minutes behind a stronger starter.");});
        Test("salaryShareFeeAndPurchaseMatchAiBudget",()=>{var(c,db)=Setup();var p=db.Find("p20");p.wage=1001;var terms=Terms(70);terms.loanWagePercent=40;var report=c.LoanClubAssessment(db,p.id,"c1",terms,123);Require(report.packageCost==123+400*10&&report.affordable,"Loan cost differs from the integer weekly salary contribution used by AI.");terms.obligationFee=db.clubs[1].annualRevenue/10+1;Require(!c.LoanClubAssessment(db,p.id,"c1",terms,123).affordable,"Unaffordable mandatory purchase is recommended.");});
        Test("noHiddenPotentialOrCareerMutation",()=>{var(c,db)=Setup();int contracts=c.world.contracts.Count,minutes=c.world.youth.Sum(y=>y.minutes);var a=c.LoanClubRecommendations(db,"p20",Terms()).Select(r=>r.club.id+":"+r.estimatedMinutesPerMatch+":"+r.levelCompatible).ToArray();db.Find("p20").potential=99;var b=c.LoanClubRecommendations(db,"p20",Terms()).Select(r=>r.club.id+":"+r.estimatedMinutesPerMatch+":"+r.levelCompatible).ToArray();Require(a.SequenceEqual(b)&&contracts==c.world.contracts.Count&&minutes==c.world.youth.Sum(y=>y.minutes),"Opening advice reveals potential or changes career state.");});
        Test("summerWithoutFixturesHasNoMinutesOrGrowth",()=>{var(c,db)=Setup();var path=Start(c,db);c.world.fixtures.Clear();float progress=path.progress;for(int day=1;day<=35;day++){c.life.day=day;c.ReviewOutgoingLoanAppearances(db);}Require(path.minutes==0&&path.loanAppearances==0&&path.progress==progress,"Empty summer weeks manufactured loan minutes, appearances or growth.");});
        Test("playedFixturesCountOnceNotPayrollWeeks",()=>{var(c,db)=Setup();var path=Start(c,db);c.world.fixtures.Clear();Fixture(c,2,"match1");Fixture(c,2,"match1");c.world.fixtures.Add(new Fixture{id="unplayed",home="c1",away="c2",day=2});c.life.day=2;c.ReviewOutgoingLoanAppearances(db);int minutes=path.minutes;Require(minutes>0&&path.loanAppearances==1,"Played match does not produce exactly one estimated appearance.");c.ReviewOutgoingLoanAppearances(db);c.life.day=7;c.ReviewOutgoingLoanAppearances(db);Require(path.minutes==minutes&&path.loanAppearances==1,"Duplicate review or payroll week repeats an appearance.");});
        Test("unavailableConsumesMatchWithoutLaterBackfill",()=>{var(c,db)=Setup();var path=Start(c,db);c.world.fixtures.Clear();db.Find("p20").unavailableDays=3;Fixture(c,2,"injured-match");c.life.day=2;c.ReviewOutgoingLoanAppearances(db);db.Find("p20").unavailableDays=0;c.life.day=3;c.ReviewOutgoingLoanAppearances(db);Require(path.minutes==0&&path.loanAppearances==0,"Injury credited an appearance or recovery invented it afterwards.");});
        Test("legacyMinutesCannotTriggerAppearanceClause",()=>{var(c,db)=Setup();var path=Start(c,db,end:28,appearances:5);path.minutes=6000;var contract=c.Contract(db,"p20");contract.loanAppearanceTracking=false;c.world.fixtures.Clear();c.life.day=28;typeof(Career).GetMethod("ResolveLoans",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(c,new object[]{db});Require(db.Find("p20").team=="c0"&&contract.loanAppearanceHistoryEstimated,"Legacy cumulative minutes were converted into undocumented appearances and purchase.");});
        Test("fifthAppearanceTriggersPurchaseNotFifthWeek",()=>{foreach(int count in new[]{4,5}){var(c,db)=Setup();var path=Start(c,db,end:42,appearances:5);c.world.fixtures.Clear();for(int day=1;day<=42;day++){if(day<=count)Fixture(c,day,"game"+day);c.life.day=day;c.ReviewOutgoingLoanAppearances(db);}typeof(Career).GetMethod("ResolveLoans",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(c,new object[]{db});Require(path.loanAppearances==count&&db.Find("p20").team==(count==5?"c1":"c0"),"Conditional purchase counted weeks/minutes or ignored the fifth estimated appearance.");}});
        return results;
    }
}
namespace Touchline.Tests
{
    public sealed class LoanProspectTests
    {
        [NUnit.Framework.TestCase("allEligibleClubsBeyondFriendlyTwelve")]
        [NUnit.Framework.TestCase("recommendationsFollowPlayerNotParentTeam")]
        [NUnit.Framework.TestCase("positionCompetitionChangesMinutes")]
        [NUnit.Framework.TestCase("reserveKeeperBehindStarterHasNoInventedSubstitution")]
        [NUnit.Framework.TestCase("salaryShareFeeAndPurchaseMatchAiBudget")]
        [NUnit.Framework.TestCase("noHiddenPotentialOrCareerMutation")]
        [NUnit.Framework.TestCase("summerWithoutFixturesHasNoMinutesOrGrowth")]
        [NUnit.Framework.TestCase("playedFixturesCountOnceNotPayrollWeeks")]
        [NUnit.Framework.TestCase("unavailableConsumesMatchWithoutLaterBackfill")]
        [NUnit.Framework.TestCase("legacyMinutesCannotTriggerAppearanceClause")]
        [NUnit.Framework.TestCase("fifthAppearanceTriggersPurchaseNotFifthWeek")]
        public void LoanAdviceAndActualEstimatedPathStayConsistent(string scenario)=>NUnit.Framework.Assert.That(LoanProspectChecks.Run(scenario)[scenario],NUnit.Framework.Is.True);
    }
}

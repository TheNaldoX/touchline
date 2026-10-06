using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Touchline.Core;
public static class SummerBoundaryRegression
{
    static (Career career,Database database) Setup()
    {
        var db=new Database{
            leagues=new[]{new LeagueData{id="fra.1",name="L1"}},
            clubs=Enumerable.Range(0,4).Select(i=>new ClubData{id="c"+i,name="Club "+i,league="fra.1",playable=true,annualRevenue=20000000}).ToArray(),
            players=Enumerable.Range(0,96).Select(i=>new PlayerData{id="p"+i,name="Player "+i,team="c"+(i/24),age=24,position=i%24==0||i%24==12?"GB":i%24<8?"DEF":i%24<16?"MIL":"ATT",positions=new[]{i%24==0||i%24==12?"GK":i%24<8?"CB":i%24<16?"CM":"ST"},rating=65,potential=80,value=100000,wage=500,fitness=100,morale=75,attributes=new[]{new AttributeValue{key="shortPassing",value=65},new AttributeValue{key="sprintSpeed",value=65}}}).ToArray()
        };
        var c=new Career{club="c0"};c.lineup=Career.Select(db,c.club,c.tactic);c.EnsureWorld(db);
        // Isolate the end-of-season predicate: all official competitions complete.
        foreach(var f in c.world.fixtures){f.played=true;f.winner=f.home;}
        foreach(var cup in c.world.cups)cup.finished=true;
        c.life.day=(new DateTime(2027,6,13)-Career.Epoch).Days;
        c.life.nextFixture=int.MaxValue;c.life.cash=100000000;
        return(c,db);
    }

    static List<string> results=new List<string>();
    static void Check(bool condition,string name){results.Add((condition?"PASS ":"FAIL ")+name);}
    static int Day(int month,int day)=>(new DateTime(2027,month,day)-Career.Epoch).Days;
    static int Fees(Career c)=>c.life.ledger.Count(e=>e.label=="Garantie de match amical");
    public static string[] Run()
    {
        results.Clear();
        var (control,db0)=Setup();control.AdvanceDay(db0);control.AdvanceDay(db0);
        Check(control.world.year==2027,"normal season closes on June 15");
        var (c,db)=Setup();c.InviteFriendly(db,"c1",Day(7,1),true);c.AdvanceDay(db);
        // Capture accepted booking before closing day; production acceptance still processes the same invitation.
        c.friendlies.Single().answerDay=c.life.day;
        typeof(Career).GetMethod("PreseasonDay",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(c,new object[]{db});
        var booked=c.world.fixtures.Single(f=>f.league=="friendly"&&!f.played);string id=booked.id;long fee=c.friendlies.Single().guarantee;
        c.AdvanceDay(db);
        Check(c.world.year==2027,"accepted future friendly does not block closing");
        Check(c.world.fixtures.Any(f=>f.id==id&&!f.played&&f.day==Day(7,1)),"accepted fixture identity/date preserved");
        Check(!c.world.history.Any(f=>f.id==id),"unplayed friendly not archived as a result");
        Check(Fees(c)==1&&c.life.ledger.Where(e=>e.label=="Garantie de match amical").Sum(e=>e.amount)==-fee,"guarantee paid exactly once");
        Check(c.world.fixtures.Select(f=>f.id).Distinct().Count()==c.world.fixtures.Count,"fixture IDs unique after closing");
        var (pending,pdb)=Setup();pending.AdvanceDay(pdb);pending.InviteFriendly(pdb,"c1",Day(7,1),true);pending.AdvanceDay(pdb);
        Check(pending.world.year==2027&&pending.friendlies.Single().status=="pending"&&Fees(pending)==0,"pending invitation survives closing without payment");
        pending.AdvanceDay(pdb);Check(pending.friendlies.Single().status=="accepted"&&Fees(pending)==1&&pending.world.fixtures.Any(f=>f.league=="friendly"&&f.day==Day(7,1)),"pending invitation accepted against new season calendar");
        var (camp,cdb)=Setup();camp.ScheduleCamp("local",Day(6,20));long cost=camp.camps.First().cost;
        Check(camp.camps.First().year==2027,"camp assigned actual preparation year before closing");
        camp.AdvanceDay(cdb);camp.AdvanceDay(cdb);bool blocked=false;try{camp.ScheduleCamp("altitude",Day(6,22));}catch(InvalidOperationException){blocked=true;}
        Check(blocked&&camp.camps.Count==1,"paid camp blocks duplicate across season boundary");
        Check(camp.camps.First().year==2027&&camp.camps.First().start==Day(6,20)&&camp.camps.First().status=="planned"&&camp.life.ledger.Count(e=>e.label.StartsWith("Stage de préparation"))==1,"camp date/status/cost preserved without second charge");
        var restore=typeof(Career).GetMethod("RestoreSummerPreparation",BindingFlags.Instance|BindingFlags.NonPublic);
        if(restore==null){Check(false,"conflict reconciliation available");return results.ToArray();}
        var official=c.world.fixtures.First(f=>f.league!="friendly"&&!f.played&&(f.home==c.club||f.away==c.club));
        int officialDay=official.day;var officialDates=c.world.fixtures.Where(f=>f.league!="friendly").ToDictionary(f=>f.id,f=>f.day);
        c.world.fixtures.Remove(booked);booked.day=officialDay;c.friendlies.Single().day=officialDay;
        restore.Invoke(c,new object[]{new List<Fixture>{booked}});
        Check(c.world.fixtures.Where(f=>f.league!="friendly").All(f=>officialDates[f.id]==f.day),"friendly reconciliation never shifts official dates");
        Check(booked.day>=officialDay+3&&c.friendlies.Single().day==booked.day&&Fees(c)==1,"conflicting friendly rescheduled with invitation and no new guarantee");
        Check(c.world.fixtures.Where(f=>f.id!=booked.id&&(f.home==booked.home||f.away==booked.home||f.home==booked.away||f.away==booked.away)).All(f=>Math.Abs(f.day-booked.day)>=3),"rescheduled friendly respects both clubs rest");
        camp.camps.First().year=2026;camp.camps.First().start=officialDay;camp.camps.First().end=officialDay+7;
        camp.world.fixtures.Add(new Fixture{id="campConflict",league="official",home=camp.club,away="c1",day=officialDay});
        restore.Invoke(camp,new object[]{new List<Fixture>()});restore.Invoke(camp,new object[]{new List<Fixture>()});
        Check(camp.camps.First().year==2027&&camp.camps.First().status=="cancelled"&&camp.life.ledger.Count(e=>e.label.StartsWith("Remboursement du stage"))==1&&camp.life.ledger.Where(e=>e.label.StartsWith("Remboursement du stage")).Sum(e=>e.amount)==cost,"legacy camp year migrated and new official clash refunded once");
        foreach(bool published in new[]{true,false}){
            var fixedFriendly=new Fixture{id="fixed-"+published,league="friendly",home=c.club,away="c2",day=Day(8,15),published=published,fixedWindow=!published};
            var generated=new Fixture{id="generated-"+published,league="fra.1",home=c.club,away="c3",day=fixedFriendly.day};c.world.fixtures.Add(generated);int fixedDate=fixedFriendly.day;
            restore.Invoke(c,new object[]{new List<Fixture>{fixedFriendly}});
            Check(fixedFriendly.day==fixedDate&&Math.Abs(generated.day-fixedDate)>=3,(published?"published":"fixed-window")+" friendly remains immutable and simulated official resolves around it");
            c.world.fixtures.Remove(fixedFriendly);c.world.fixtures.Remove(generated);
        }
        var (due,ddb)=Setup();due.world.fixtures.Add(new Fixture{id="dueFriendly",league="friendly",home=due.club,away="c1",day=Day(6,15)});due.AdvanceDay(ddb);due.AdvanceDay(ddb);
        Check(due.world.year==2026&&due.world.fixtures.Single(f=>f.id=="dueFriendly").day==Day(6,15)&&due.life.nextFixture==Day(6,15),"due friendly keeps match selection and blocks closing until played");
        due.CommitFixture(due.world.fixtures.Single(f=>f.id=="dueFriendly"),0,0);
        typeof(Career).GetMethod("WorldDay",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(due,new object[]{ddb});
        Check(due.world.year==2027&&due.world.history.Count(f=>f.id=="dueFriendly"&&f.played)==1,"played due friendly permits closing and archives once");
        return results.ToArray();
    }
}

namespace Touchline.Tests { public class SummerBoundaryTests { [NUnit.Framework.Test] public void PreparationBookingsSurviveSeasonBoundary(){foreach(var result in SummerBoundaryRegression.Run())NUnit.Framework.Assert.That(result.StartsWith("PASS "),NUnit.Framework.Is.True,result);} }
}

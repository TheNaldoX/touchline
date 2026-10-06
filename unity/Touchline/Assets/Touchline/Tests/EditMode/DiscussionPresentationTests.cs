using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Touchline.Core;
using Touchline;

public static class DiscussionPresentationChecks
{
    static void Require(bool b,string s){if(!b)throw new InvalidOperationException(s);}
    static object Call(Career c,string name,params object[] args)=>typeof(Career).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(c,args);
    static (Career c,Database db) Setup(){
        var db=new Database{leagues=new[]{new LeagueData{id="fra.1",name="L1"},new LeagueData{id="fra.2",name="L2",tier=2}},clubs=Enumerable.Range(0,8).Select(i=>new ClubData{id="c"+i,name="Club "+i,league=i<4?"fra.1":"fra.2",playable=true,annualRevenue=20000000+i*1000000}).ToArray(),players=Enumerable.Range(0,192).Select(i=>new PlayerData{id="p"+i,name="Alex "+i,team="c"+(i/24),age=24,position=i%24==0||i%24==12?"GB":i%24<8?"DEF":i%24<16?"MIL":"ATT",positions=new[]{i%24==0||i%24==12?"GK":i%24<8?"CB":i%24<16?"CM":"ST"},rating=65,potential=80,value=100000,wage=500,attributes=new[]{new AttributeValue{key="shortPassing",value=65}}}).ToArray()};
        var c=new Career{club="c0"};c.lineup=Career.Select(db,c.club,c.tactic);c.EnsureWorld(db);return(c,db);
    }
    static DiscussionTopic Topic(PlayerDiscussionPresentation view,string key)=>view.topics.Single(t=>t.key==key);
    static string State(Career c,Database db)=>UnityEngine.JsonUtility.ToJson(c.life)+"|"+UnityEngine.JsonUtility.ToJson(c.world)+"|"+UnityEngine.JsonUtility.ToJson(db);
    public static Dictionary<string,object> Run(string only=null){var result=new Dictionary<string,object>();
        if(only==null||only=="allSubjectsRemainAccessibleWithoutInventedUrgency"){
            var(c,db)=Setup();var p=db.Find("p10");p.age=30;var v=PlayerDiscussionPresentation.From(c,db,p.id);var expected=new[]{"recovery","role","support","promiseReview","development","settle","explain","demand","promise","apologize","leadership"};Require(v.own&&v.topics.Length==11&&new HashSet<string>(v.topics.Select(x=>x.key)).SetEquals(expected)&&v.defaultTopic=="role"&&!v.topics.Any(x=>x.recommended),"Stable preseason fabricated urgent topics or removed a subject.");Require(Topic(v,"promiseReview").blocked!=null&&Topic(v,"promise").blocked==null,"Topic availability differs from actual promise conditions.");result["allSubjectsRemainAccessibleWithoutInventedUrgency"]=true;
        }
        if(only==null||only=="injuryPrioritizesRecoveryAndBlocksNewPlayingPromise"){
            var(c,db)=Setup();c.OpenInjury(db,"p10","muscle");var v=PlayerDiscussionPresentation.From(c,db,"p10");Require(v.defaultTopic=="recovery"&&Topic(v,"recovery").reason.Contains("jours estimés")&&Topic(v,"promise").blocked!=null&&Topic(v,"demand").warning.Contains("mal reçue"),"Injury did not provide factual recovery context and promise restriction.");c.Injury("p10").closed=c.life.day;v=PlayerDiscussionPresentation.From(c,db,"p10");Require(Topic(v,"promise").blocked==null,"Closed injury still blocks a new promise.");result["injuryPrioritizesRecoveryAndBlocksNewPlayingPromise"]=true;
        }
        if(only==null||only=="promiseReviewUsesActualFulfilmentAndDate"){
            var(c,db)=Setup();var p=c.Person("p10");p.promiseUntil=5;p.promiseStarts=3;p.appearances=4;p.fitness=65;var v=PlayerDiscussionPresentation.From(c,db,p.id);Require(v.defaultTopic=="promiseReview"&&Topic(v,"promiseReview").reason==c.PlayerPromiseStatus(p.id)&&Topic(v,"promise").blocked!=null,"Recorded promise is not prioritized or new promise allowed over an existing one.");p.appearances=5;v=PlayerDiscussionPresentation.From(c,db,p.id);Require(Topic(v,"promiseReview").reason.Contains("Engagement tenu")&&Topic(v,"promiseReview").priority<95,"Fulfilled promise still creates imminent concern.");result["promiseReviewUsesActualFulfilmentAndDate"]=true;
        }
        if(only==null||only=="lowMinutesRequirePlayedMatchesAndContractStatus"){
            var(c,db)=Setup();var contract=c.Contract(db,"p10");contract.role="key";contract.joined=0;contract.appearancesAtSigning=0;
            var v=PlayerDiscussionPresentation.From(c,db,"p10");Require(!Topic(v,"role").recommended,"No matches played was treated as failure to select a player.");
            c.life.matches=12;v=PlayerDiscussionPresentation.From(c,db,"p10");Require(!Topic(v,"role").recommended,"A global match counter without played fixture evidence fabricated a role mismatch.");
            c.life.day=60;c.world.fixtures.Clear();c.world.history.Clear();for(int day=1;day<=12;day++)c.world.fixtures.Add(new Fixture{id="role-context-"+day,league="fra.1",home=c.club,away="c1",day=day,played=true});
            c.Person("p10").appearances=8;v=PlayerDiscussionPresentation.From(c,db,"p10");Require(c.RoleMatchOpportunities("p10")==12&&v.defaultTopic=="role"&&Topic(v,"role").priority==80&&Topic(v,"role").reason.Contains(c.PlayingTimeProgress("p10")),"Eight appearances in twelve real fixtures did not reflect the legacy key-player commitment.");
            c.Person("p10").appearances=9;v=PlayerDiscussionPresentation.From(c,db,"p10");Require(!Topic(v,"role").recommended,"Nine appearances in twelve real fixtures still invent a legacy role mismatch.");
            contract.playingTime=new PlayingTimeUsage{club=c.club};for(int game=0;game<4;game++)contract.playingTime.Add(false,20);v=PlayerDiscussionPresentation.From(c,db,"p10");Require(Topic(v,"role").priority==80&&Topic(v,"role").reason.Contains("0 titularisations")&&Topic(v,"role").reason.Contains("Utilisation inférieure"),"A new key-player promise incorrectly treated substitute appearances as starts.");
            contract.role="impact_sub";v=PlayerDiscussionPresentation.From(c,db,"p10");Require(!Topic(v,"role").recommended&&Topic(v,"role").reason.Contains("Engagement respecté"),"Regular twenty-minute entries invented a super-sub promise mismatch.");result["lowMinutesRequirePlayedMatchesAndContractStatus"]=true;
        }
        if(only==null||only=="recentIntegrationMustHaveKnownPastJoinDate"){
            var(c,db)=Setup();c.life.day=30;var e=c.Contract(db,"p10");e.joined=10;var v=PlayerDiscussionPresentation.From(c,db,"p10");Require(v.defaultTopic=="settle"&&Topic(v,"settle").reason.Contains("20 jours"),"Known recent arrival not suggested.");foreach(int day in new[]{0,100}){e.joined=day;Require(!Topic(PlayerDiscussionPresentation.From(c,db,"p10"),"settle").recommended,"Baseline or future contract was presented as a confirmed recent arrival.");}result["recentIntegrationMustHaveKnownPastJoinDate"]=true;
        }
        if(only==null||only=="sharedSevenDayCooldownHasExactDateForEveryTopic"){
            var(c,db)=Setup();var p=c.Person("p10");c.life.day=8;p.lastTalk=3;var v=PlayerDiscussionPresentation.From(c,db,p.id);Require(v.waitDays==2&&v.availableDay==10&&v.topics.Length==11,"Cooldown does not match Talk's shared seven-day rule or hides subjects.");c.life.day=10;v=PlayerDiscussionPresentation.From(c,db,p.id);Require(v.waitDays==0&&v.availableDay==10,"Discussion remained unavailable after the exact shared cooldown.");result["sharedSevenDayCooldownHasExactDateForEveryTopic"]=true;
        }
        if(only==null||only=="tensionAndFatigueUseKnownSignalsOnly"){
            var(c,db)=Setup();var p=c.Person("p10");p.trust=30;p.morale=40;var v=PlayerDiscussionPresentation.From(c,db,p.id);Require(v.defaultTopic=="apologize"&&Topic(v,"support").recommended&&!Topic(v,"demand").recommended,"Relationship indicators not reflected without inventing pressure.");p.fitness=60;v=PlayerDiscussionPresentation.From(c,db,p.id);Require(v.defaultTopic=="recovery","Fatigue not prioritized.");p.restUntil=3;v=PlayerDiscussionPresentation.From(c,db,p.id);Require(!Topic(v,"recovery").recommended&&Topic(v,"recovery").reason.Contains("Repos déjà convenu"),"Existing rest was presented as a new unmet demand.");result["tensionAndFatigueUseKnownSignalsOnly"]=true;
        }
        if(only==null||only=="presentationNeverMutatesStateOrGeneratesMissingContracts"){
            var(c,db)=Setup();c.world.contracts.RemoveAll(e=>e.player=="p10");c.Person("p10").promiseUntil=21;string before=State(c,db);for(int i=0;i<5;i++)PlayerDiscussionPresentation.From(c,db,"p10");Require(before==State(c,db)&&!c.world.contracts.Any(e=>e.player=="p10"),"Presentation generated an employment, consumed RNG, changed promise or modified finances.");Require(!PlayerDiscussionPresentation.From(c,db,"p24").own&&!PlayerDiscussionPresentation.From(c,db,"missing").own,"External/unknown player presented as owned.");db.Find("p10").age=0;var v=PlayerDiscussionPresentation.From(c,db,"p10");Require(!Topic(v,"development").recommended&&Topic(v,"development").reason.Contains("non renseigné"),"Unknown age fabricated youth context.");result["presentationNeverMutatesStateOrGeneratesMissingContracts"]=true;
        }
        return result;
    }
}

namespace Touchline.Tests {public sealed class DiscussionPresentationTests {
[NUnit.Framework.TestCase("allSubjectsRemainAccessibleWithoutInventedUrgency")]
[NUnit.Framework.TestCase("injuryPrioritizesRecoveryAndBlocksNewPlayingPromise")]
[NUnit.Framework.TestCase("promiseReviewUsesActualFulfilmentAndDate")]
[NUnit.Framework.TestCase("lowMinutesRequirePlayedMatchesAndContractStatus")]
[NUnit.Framework.TestCase("recentIntegrationMustHaveKnownPastJoinDate")]
[NUnit.Framework.TestCase("sharedSevenDayCooldownHasExactDateForEveryTopic")]
[NUnit.Framework.TestCase("tensionAndFatigueUseKnownSignalsOnly")]
[NUnit.Framework.TestCase("presentationNeverMutatesStateOrGeneratesMissingContracts")]
public void TopicsReflectRecordedContextWithoutChangingCareer(string scenario){NUnit.Framework.Assert.That(DiscussionPresentationChecks.Run(scenario)[scenario],NUnit.Framework.Is.True);}}}

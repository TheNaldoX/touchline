using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Touchline.Core;

public static class MedicalResponsibilityChecks
{
    static void Require(bool v,string m){if(!v)throw new InvalidOperationException(m);}
    [Serializable] sealed class Snapshot {public Career career;public PlayerData[] players;}
    static string State(Career c,Database db){var state=new Snapshot{career=c,players=db.players};
#if UNITY_5_3_OR_NEWER
        return UnityEngine.JsonUtility.ToJson(state);
#else
        return System.Text.Json.JsonSerializer.Serialize(state,new System.Text.Json.JsonSerializerOptions{IncludeFields=true,IgnoreReadOnlyProperties=true});
#endif
    }
    static (Career c,Database db,MedicalCase medical,ClubMessage message) Setup()
    {
        var db=new Database{players=new[]{new PlayerData{id="p",name="Known player",team="a",age=25,wage=1000}}};
        var c=new Career{club="a",world=new CareerWorld(),life=new ClubLife{day=10,cash=1000000,revenue=20000000}};
        c.life.players.Add(new PlayerLife{id="p"});c.world.contracts.Add(new Employment{player="p",club="a",retirement=-1,wage=1000,until=400});
        var medical=new MedicalCase{id=1,player="p",opened=10,remaining=6,total=6,diagnosis="Known synthetic injury",surgery=true,injection=true,consent=true};c.life.medical.Add(medical);
        var message=c.Mail("Medical staff","Known injury","Known prior diagnosis","p","medical","medical/1");return(c,db,medical,message);
    }
    static void Persist(Career c,PlayerData p)=>typeof(Career).GetMethod("SavePlayer",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(c,new object[]{p});
    public static void DepartedCareIsRefusedWithoutMutation(string departure,string treatment)
    {
        var(c,db,medical,message)=Setup();Require(c.MessageNeedsDecision(message),"Active medical fixture was not a decision.");
        if(departure=="effective-retirement-before-team")c.world.contracts[0].retirement=c.life.day;
        else if(departure=="retired-team"){db.Find("p").team="retired";c.life.players.Clear();c.world.contracts[0].club="retired";Persist(c,db.Find("p"));}
        else if(departure=="outgoing-loan"||departure=="transfer"){db.Find("p").team="b";c.life.players.Clear();c.world.contracts[0].club="b";c.world.contracts[0].parent=departure=="outgoing-loan"?"a":null;Persist(c,db.Find("p"));}
        else throw new ArgumentException(departure);
        string before=State(c,db);bool refused=false;try{c.Treat(db,1,treatment);}catch(InvalidOperationException){refused=true;}
        Require(refused&&before==State(c,db),"Historical treatment changed diagnosis, money, trust, recovery, history or player state.");
        Require(!c.MessageNeedsDecision(message)&&c.MessageDecisionDeadline(message)==null&&c.MessagePriority(message)<3,"Out-of-club care still creates an urgent decision.");
        Require(!message.read&&medical.closed==-1&&medical.remaining==6,"Ending responsibility invented recovery or read the historic message.");
    }
    public static void FutureRetirementRemainsTreatable()
    {
        var(c,db,medical,message)=Setup();c.world.contracts[0].retirement=c.life.day+30;
        Require(c.PlayerMedicalResponsibility("p")&&c.MessageNeedsDecision(message),"Future retirement removed the club's actual current care responsibility.");
        c.Treat(db,1,"conservative");Require(medical.treatment=="conservative"&&string.IsNullOrEmpty(medical.responsibilityEndedReason)&&medical.closed<0,"A future retirement prevented a legitimate treatment.");
    }
    public static void ALoanReturnDoesNotReactivateAnOldDiagnosis()
    {
        var(c,db,medical,message)=Setup();var p=db.Find("p");p.team="b";c.life.players.Clear();Persist(c,p);
        Require(medical.responsibilityEndedReason=="left-club"&&medical.responsibilityEndedDay==10&&medical.closed==-1&&c.Injury("p")==null,"Departure was recorded as recovery, or left the old clinical episode active.");
        c.life.day=150;p.team="a";c.world.contracts[0].club="a";c.life.players.Add(new PlayerLife{id="p"});Persist(c,p);c.ApplyLife(db);
        Require(c.Injury("p")==null&&!c.MessageNeedsDecision(message)&&p.unavailableDays==0&&medical.remaining==6&&medical.responsibilityEndedDay==10,"Loan return resurrected a stale diagnosis or invented its recovery.");
        bool refused=false;try{c.Treat(db,medical.id,"conservative");}catch(InvalidOperationException){refused=true;}Require(refused,"An old episode became treatable after a loan return.");
        c.life.medicalCount=1;var fresh=c.OpenInjury(db,"p","muscle");Require(fresh.id==2&&string.IsNullOrEmpty(fresh.responsibilityEndedReason)&&ReferenceEquals(fresh,c.Injury("p")),"Historical closure prevents a genuinely new clinical episode.");
    }
    public static void LegacyRepairIsExplicitAndIdempotent()
    {
        var(c,db,medical,message)=Setup();c.life.players.Clear();db.Find("p").team="b";
        Require(string.IsNullOrEmpty(medical.responsibilityEndedReason),"Legacy fixture already had a closure marker.");c.ApplyLife(db);
        Require(medical.responsibilityEndedReason=="outside-current-follow-up"&&medical.responsibilityEndedDay==10&&medical.closed==-1&&medical.remaining==6,"Legacy repair inferred a recovery or historic departure date.");
        string after=State(c,db);c.ApplyLife(db);Require(after==State(c,db),"Legacy closure is rewritten on every application or navigation.");
    }
    public static void ReadingNeverClosesAnEpisode()
    {
        var(c,db,medical,message)=Setup();c.life.players.Clear();string before=State(c,db);
        for(int i=0;i<3;i++){c.PlayerMedicalResponsibility("p");c.MessageNeedsDecision(message);c.MessageDecisionDeadline(message);c.Injury("p");}
        Require(before==State(c,db)&&string.IsNullOrEmpty(medical.responsibilityEndedReason),"Read-only navigation repaired or saved medical state.");
    }
    public static void ActiveCareSurvivesSave(string marker)
    {
        var(c,db,medical,message)=Setup();medical.responsibilityEndedReason=marker;
#if UNITY_5_3_OR_NEWER
        var restored=UnityEngine.JsonUtility.FromJson<MedicalCase>(UnityEngine.JsonUtility.ToJson(medical));
#else
        var options=new System.Text.Json.JsonSerializerOptions{IncludeFields=true};
        var restored=System.Text.Json.JsonSerializer.Deserialize<MedicalCase>(System.Text.Json.JsonSerializer.Serialize(medical,options),options);
#endif
        c.life.medical[0]=restored;c.ApplyLife(db);
        Require(ReferenceEquals(c.Injury("p"),restored)&&c.MessageNeedsDecision(message),"Saving an empty responsibility marker removed current care.");
        c.Treat(db,restored.id,"conservative");
        Require(restored.treatment=="conservative"&&string.IsNullOrEmpty(restored.responsibilityEndedReason),"Restored active care was not treatable.");
    }
    public static void ClosurePersistsAndMissingLegacyFieldsStayActive()
    {
        var(c,db,medical,message)=Setup();
#if UNITY_5_3_OR_NEWER
        var legacy=UnityEngine.JsonUtility.FromJson<MedicalCase>("{\"id\":1,\"player\":\"p\",\"opened\":10,\"remaining\":6,\"closed\":-1,\"treatment\":\"pending\"}");
#else
        var options=new System.Text.Json.JsonSerializerOptions{IncludeFields=true};
        var legacy=System.Text.Json.JsonSerializer.Deserialize<MedicalCase>("{\"id\":1,\"player\":\"p\",\"opened\":10,\"remaining\":6,\"closed\":-1,\"treatment\":\"pending\"}",options);
#endif
        c.life.medical[0]=legacy;c.ApplyLife(db);Require(string.IsNullOrEmpty(legacy.responsibilityEndedReason)&&c.MessageNeedsDecision(message),"Missing legacy fields accidentally closed an owned active case.");
        c.life.players.Clear();db.Find("p").team="b";c.ApplyLife(db);
#if UNITY_5_3_OR_NEWER
        var restored=UnityEngine.JsonUtility.FromJson<MedicalCase>(UnityEngine.JsonUtility.ToJson(legacy));
#else
        var restored=System.Text.Json.JsonSerializer.Deserialize<MedicalCase>(System.Text.Json.JsonSerializer.Serialize(legacy,options),options);
#endif
        c.life.medical[0]=restored;c.life.players.Add(new PlayerLife{id="p"});db.Find("p").team="a";c.ApplyLife(db);
        Require(restored.responsibilityEndedReason=="outside-current-follow-up"&&restored.responsibilityEndedDay==10&&restored.closed==-1&&restored.remaining==6&&c.Injury("p")==null&&!c.MessageNeedsDecision(message),"Responsibility closure lost in save roundtrip and resurrected old care.");
    }
    public static Dictionary<string,bool> Run(){var r=new Dictionary<string,bool>();foreach(var departure in new[]{"effective-retirement-before-team","retired-team","outgoing-loan","transfer"})foreach(var treatment in new[]{"conservative","surgery","injection"}){DepartedCareIsRefusedWithoutMutation(departure,treatment);r[departure+"/"+treatment]=true;}FutureRetirementRemainsTreatable();r["future-retirement-treatable"]=true;ALoanReturnDoesNotReactivateAnOldDiagnosis();r["loan-return-new-episode"]=true;LegacyRepairIsExplicitAndIdempotent();r["legacy-repair-idempotent"]=true;ReadingNeverClosesAnEpisode();r["reading-does-not-repair"]=true;ClosurePersistsAndMissingLegacyFieldsStayActive();r["save-roundtrip-and-old-fields"]=true;return r;}
}

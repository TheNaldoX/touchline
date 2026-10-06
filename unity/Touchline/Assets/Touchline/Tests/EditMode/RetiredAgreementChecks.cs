using System;
using System.Linq;
using System.Collections.Generic;
using Touchline.Core;

public static class RetiredAgreementChecks
{
    static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    static Career Setup(bool outgoing)
    {
        var c=new Career{club="a",world=new CareerWorld(),life=new ClubLife{day=10,cash=1000000}};
        c.world.contracts.Add(new Employment{player="p",club="a",retirement=-1,wage=1000,until=400});
        c.world.rosterChanges.Add(new PlayerData{id="p",team="a",wage=1000});
        c.life.players.Add(new PlayerLife{id="p",morale=80,trust=80});
        if(outgoing)c.outgoingLoans.Add(new OutgoingLoanOffer{player="p",owner="a",borrower="b",status="accepted",due=10});
        else c.world.offers.Add(new TransferOffer{player="p",destination="a",seller="b",status="accepted",due=10});
        c.life.messages.Add(new ClubMessage{id=1,day=10,player="p",action="transfer",subject=outgoing?"Accord de pr\u00eat":"Accord de principe",text="Ancien accord",sender="Agent"});return c;
    }
#if !UNITY_5_3_OR_NEWER
    sealed class SlotConverter:System.Text.Json.Serialization.JsonConverter<Slot>
    {
        public override Slot Read(ref System.Text.Json.Utf8JsonReader r,Type t,System.Text.Json.JsonSerializerOptions o)
        {using(var d=System.Text.Json.JsonDocument.ParseValue(ref r)){var e=d.RootElement;return new Slot(e.GetProperty("role").GetString(),e.GetProperty("x").GetSingle(),e.GetProperty("y").GetSingle()){duty=e.GetProperty("duty").GetString()};}}
        public override void Write(System.Text.Json.Utf8JsonWriter w,Slot s,System.Text.Json.JsonSerializerOptions o)
        {w.WriteStartObject();w.WriteString("role",s.role);w.WriteString("duty",s.duty);w.WriteNumber("x",s.x);w.WriteNumber("y",s.y);w.WriteEndObject();}
    }
    static System.Text.Json.JsonSerializerOptions PortableOptions(){var o=new System.Text.Json.JsonSerializerOptions{IncludeFields=true,IgnoreReadOnlyProperties=true};o.Converters.Add(new SlotConverter());return o;}
#endif
    static string State(Career c){
#if UNITY_5_3_OR_NEWER
        return UnityEngine.JsonUtility.ToJson(c);
#else
        return System.Text.Json.JsonSerializer.Serialize(c,PortableOptions());
#endif
    }
    static Career Restore(string state){
#if UNITY_5_3_OR_NEWER
        return UnityEngine.JsonUtility.FromJson<Career>(state);
#else
        return System.Text.Json.JsonSerializer.Deserialize<Career>(state,PortableOptions());
#endif
    }
    public static void Effective(string evidence,bool outgoing)
    {
        var c=Setup(outgoing);var m=c.life.messages[0];Require(c.MessageNeedsDecision(m),"Active fixture must be actionable before retirement: "+evidence+" outgoing="+outgoing+" retired="+c.PlayerRetirementEffective("p")+" offers="+c.world.offers.Count+" outgoingOffers="+c.outgoingLoans.Count);
        switch(evidence){
            case "date-before-team-change":c.world.contracts[0].retirement=c.life.day;break;
            case "retired-contract":c.world.contracts[0].club="retired";break;
            case "retired-snapshot":c.world.rosterChanges[0].team="retired";break;
            case "snapshot-legacy-null-contracts":c.world.contracts=null;c.world.rosterChanges[0].team="retired";break;
            default:throw new ArgumentException(evidence);
        }
        string before=State(c);Require(c.PlayerRetirementEffective("p"),"Persisted effective retirement not recognized.");
        Require(!c.MessageNeedsDecision(m)&&c.MessageDecisionDeadline(m)==null&&c.MessageDecisionTiming(m)==null,"Retired agreement still needs a decision or imposes a deadline.");
        Require(c.PendingTransferAgreement(m)==null&&c.PendingOutgoingLoanAgreement(m)==null,"Retired player remains signable from an old agreement.");
        Require(c.MessagePriority(m)<3&&c.life.messages.Count(c.MessageNeedsDecision)==0,"Retired agreement keeps the decision badge or priority.");
        Require(c.life.messages.Count(x=>!x.read)==1,"Closing a decision must not mark historical mail read.");
        Require(before==State(c),"Read-only agreement checks modified cash, offers, contracts or history.");
        var restored=Restore(before);
        Require(restored.PlayerRetirementEffective("p")&&!restored.MessageNeedsDecision(restored.life.messages[0]),"Persisted retirement lost after serialization.");
    }
    public static void FutureAnnouncement(bool outgoing)
    {
        var c=Setup(outgoing);c.world.contracts[0].retirement=c.life.day+30;var m=c.life.messages[0];
        Require(!c.PlayerRetirementEffective("p")&&c.MessageNeedsDecision(m)&&c.MessagePriority(m)==3,"An announced future retirement removed a valid current agreement.");
        m.read=true;Require(c.MessageNeedsDecision(m)&&c.life.messages.Count(c.MessageNeedsDecision)==1&&c.life.messages.Count(x=>!x.read)==0,"Read and decision counters are incorrectly linked.");
        c.life.day=18;Require(!c.MessageNeedsDecision(m),"Expired agreement remains actionable before future retirement.");
    }
    public static void StaffIsUnchanged()
    {
        var c=Setup(false);c.world.contracts[0].retirement=10;
        var o=new StaffOffer{staff="s",club="a",employer="b",due=10,wage=100,years=2,status="accepted"};c.staffOffers.Add(o);
        var m=new ClubMessage{player="p",action="staff",subject="Accord de principe",day=10,reference="staff-contract/s/a/b/10/100/2"};
        Require(ReferenceEquals(o,c.PendingStaffAgreement(m))&&c.MessageNeedsDecision(m),"Player retirement incorrectly removed a staff agreement.");
    }
    public static void NullAndUnrelatedEvidence()
    {
        var c=Setup(false);c.world.contracts.Add(new Employment{player="other",club="retired",retirement=0});
        Require(!c.PlayerRetirementEffective("p")&&!c.PlayerRetirementEffective(null)&&!c.PlayerRetirementEffective(""),"Unrelated or missing identity was declared retired.");
        c.world.contracts=null;c.world.rosterChanges=null;Require(!c.PlayerRetirementEffective("p"),"Missing legacy evidence invented a retirement.");
        c.world=null;Require(!c.PlayerRetirementEffective("p"),"Missing world invented a retirement.");
    }
    public static void DiscussionBeforeTeamChange()
    {
        var c=Setup(false);var p=c.world.rosterChanges[0];p.age=30;
        var db=new Database{players=new[]{p}};c.world.contracts[0].retirement=c.life.day;
        var closed=Touchline.PlayerDiscussionPresentation.From(c,db,"p");
        Require(!closed.own&&closed.topics.Length==0&&closed.defaultTopic==null,"Effective retirement before team mutation still offers dialogue or urgent topics.");
        c.world.contracts[0].retirement=c.life.day+30;
        var future=Touchline.PlayerDiscussionPresentation.From(c,db,"p");
        Require(future.own&&future.topics.Length==11,"Future retirement incorrectly removed current dialogue subjects.");
    }
    public static void RetainedDialogueCannotMutateAfterRetirement(string evidence)
    {
        var c=Setup(false);var p=c.world.rosterChanges[0];p.age=30;var db=new Database{players=new[]{p}};
        Require(Touchline.PlayerDiscussionPresentation.From(c,db,"p").own,"Stale-view fixture must start as an actual owned active player.");
        if(evidence=="date-before-team-change")c.world.contracts[0].retirement=c.life.day;
        else if(evidence=="retired-team")p.team="retired";
        else if(evidence=="retired-snapshot")c.world.rosterChanges[0]=new PlayerData{id="p",team="retired"};
        else throw new ArgumentException(evidence);
        string before=State(c);bool refused=false;
        try{c.Talk(db,"p","support",false);}catch(InvalidOperationException){refused=true;}
        Require(refused&&before==State(c),"Retained dialogue callback changed messages, cash, trust, morale or last-talk after effective retirement.");
    }
    public static Dictionary<string,bool> Run()
    {
        var result=new Dictionary<string,bool>();
        foreach(var evidence in new[]{"date-before-team-change","retired-contract","retired-snapshot","snapshot-legacy-null-contracts"})foreach(bool outgoing in new[]{false,true}){Effective(evidence,outgoing);result[evidence+"/"+(outgoing?"outgoing":"incoming")]=true;}
        foreach(bool outgoing in new[]{false,true}){FutureAnnouncement(outgoing);result["future-announcement/"+(outgoing?"outgoing":"incoming")]=true;}
        StaffIsUnchanged();result["staff-unchanged"]=true;NullAndUnrelatedEvidence();result["null-and-unrelated-evidence"]=true;
        DiscussionBeforeTeamChange();result["discussion-before-team-change"]=true;foreach(var evidence in new[]{"date-before-team-change","retired-team","retired-snapshot"}){RetainedDialogueCannotMutateAfterRetirement(evidence);result["retained-dialogue/"+evidence]=true;}return result;
    }
}

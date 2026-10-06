using System;
using System.Collections.Generic;
using System.Linq;
namespace Touchline.Core {
 public partial class Career {
  string CommercialMessageReference(CommercialDeal d)=>"commercial/"+club+"/"+Uri.EscapeDataString(d.slot??"")+"/"+Uri.EscapeDataString(d.name??"")+"/"+d.counterDay+"/"+d.asking+"/"+d.years;
  static string ApproachMessageReference(JobApproach o)=>"manager-approach/"+o.club+"/"+o.offered+"/"+o.until;
  public bool CanSignCommercialQuote(CommercialDeal d)=>world!=null&&world.managerStatus=="employed"&&d!=null&&world.sponsors.Contains(d)&&(d.status=="accepted"||d.status=="counter")&&d.asking>0&&!world.sponsors.Any(s=>s.slot==d.slot&&s.status=="signed"&&s.until>life.day);
  public CommercialDeal PendingCommercialAgreement(ClubMessage m){
   if(m?.action!="finance"||world==null||world.managerStatus!="employed"||!(m.subject=="Proposition acceptée"||m.subject=="Contre-proposition"))return null;
   var candidates=world.sponsors.Where(d=>CanSignCommercialQuote(d)&&(!string.IsNullOrEmpty(m.reference)?m.reference==CommercialMessageReference(d):m.day==d.counterDay-3&&(m.text?.StartsWith(d.name+" propose ",StringComparison.Ordinal)??false))).Take(2).ToArray();return candidates.Length==1?candidates[0]:null;
  }
  public JobApproach PendingJobApproach(ClubMessage m){
   if(m?.action!="jobs"||life==null||world==null)return null;
   var candidates=approaches?.Where(o=>o.club!=club&&o.status=="open"&&o.until>=life.day&&(!string.IsNullOrEmpty(m.reference)?m.reference==ApproachMessageReference(o):m.sender=="Agent • carrière"&&m.day==o.offered&&m.subject?.StartsWith("Approche de ",StringComparison.Ordinal)==true)).Take(2).ToArray();return candidates?.Length==1?candidates[0]:null;
  }
  public int? MessageDecisionDeadline(ClubMessage m){
   if(!MessageNeedsDecision(m))return null;
   var transfer=PendingTransferAgreement(m);if(transfer!=null)return transfer.due+7;
   var outgoing=PendingOutgoingLoanAgreement(m);if(outgoing!=null)return outgoing.due+7;
   var staff=PendingStaffAgreement(m);if(staff!=null)return staff.due+7;
   var job=PendingJobApproach(m);if(job!=null)return job.until;
   if(m.action=="medical")return Injury(m.player)?.opened+2;
   if(m.action=="integrity")return life.investigations.FirstOrDefault(i=>i.status=="pending"&&i.alerted&&string.IsNullOrEmpty(i.response)&&(!string.IsNullOrEmpty(m.reference)?m.reference==IntegrityMessageReference(i):m.subject=="Une alerte interne vous concerne"&&m.day>=i.opened+3))?.due;
   return null;
  }
  public string MessageDecisionTiming(ClubMessage m){
   if(!MessageNeedsDecision(m))return null;var deadline=MessageDecisionDeadline(m);
   if(m.action=="medical")return "Sans choix, soins conservateurs à partir du "+Epoch.AddDays(deadline.Value).ToString("dd/MM/yyyy");
   if(m.action=="integrity")return deadline.HasValue?"Réévaluation du dossier le "+Epoch.AddDays(deadline.Value).ToString("dd/MM/yyyy"):"Réponse attendue · enquête en cours";
   if(deadline.HasValue)return "Au plus tard le "+Epoch.AddDays(deadline.Value).ToString("dd/MM/yyyy")+(deadline.Value==life.day?" · aujourd’hui":" · "+Math.Max(0,deadline.Value-life.day)+" jour(s)");
   return PendingCommercialAgreement(m)!=null?"Conditions à confirmer · aucune échéance imposée":"Votre choix est attendu";
  }
  public IOrderedEnumerable<ClubMessage> OrderedMessages(IEnumerable<ClubMessage> messages)=>messages.OrderByDescending(MessageNeedsDecision).ThenBy(m=>MessageDecisionDeadline(m)??int.MaxValue).ThenByDescending(MessagePriority).ThenByDescending(m=>m.id);
  public bool PlayerPromiseFulfilled(string id){var p=Person(id);return p.promiseUntil>=0&&p.appearances-p.promiseStarts>=2;}
  public string PlayerPromiseStatus(string id){
   var p=Person(id);if(p.promiseUntil<0)return "Aucune promesse de temps de jeu en cours.";
   int completed=Math.Max(0,p.appearances-p.promiseStarts);bool fulfilled=completed>=2;
   string status=(fulfilled?"Engagement tenu · validation à l’échéance":"Temps de jeu promis")+" · "+Math.Min(2,completed)+" / 2 apparitions d’au moins 30 minutes · "+Epoch.AddDays(p.promiseUntil).ToString("dd/MM/yyyy");
   if(!fulfilled&&(Injury(id)!=null||p.restUntil>life.day))status+=". Échéance prolongée jour après jour pendant la blessure ou le repos convenu.";return status;
  }
 }
}

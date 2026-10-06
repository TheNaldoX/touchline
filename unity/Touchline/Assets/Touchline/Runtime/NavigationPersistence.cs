using System;
using System.Linq;
using Touchline.Core;
using UnityEngine;
namespace Touchline {
 public sealed partial class TouchlineApp {
  public int SaveRequestCount{get;private set;}
  Career observedContractCareer,newCareerDraftOwner;bool newCareerRevealDraft,loadedCalendarMigrated;
  void ObserveContractPersistence(){if(ReferenceEquals(observedContractCareer,Career))return;if(observedContractCareer!=null)observedContractCareer.ContractCreatedForPersistence-=QueuePreferenceSave;observedContractCareer=Career;if(observedContractCareer!=null)observedContractCareer.ContractCreatedForPersistence+=QueuePreferenceSave;}
  void OnDestroy(){if(observedContractCareer!=null)observedContractCareer.ContractCreatedForPersistence-=QueuePreferenceSave;}
  void EnsureNewCareerDraft(){if(ReferenceEquals(newCareerDraftOwner,Career))return;newCareerDraftOwner=Career;newCareerRevealDraft=Career.revealAttributes;}
  // This probe is deliberately SMALL: no Career, PlayerData, contracts, fixtures or roster records.
  // Staff has four normal members and facilities four records. Capturing their contents detects repairs
  // and rebinding that counts alone miss; it never encodes the large career to decide whether to save.
  [Serializable] sealed class InitializationProbe {
   public int lifeVersion,worldVersion,staffMarketCount,staffOffersCount,worldContracts,worldRoster,worldDivisions,worldFixtures,accounts,incompleteAccounts,dbPlayers;
   public int freeVersion,freeLastChecked,announcedCount,processedCount,medicalResponsibilityClosures;
   public bool worldPresent,initialAiEmployment,freeImported,matchPresent;
   public string calendarEpoch;public string[] squadMemberIds;
   public Facility[] facilities;public DelegationPlan staff;
  }
  string InitializationStamp(){var c=Career;var w=c.world;var l=c.life;return JsonUtility.ToJson(new InitializationProbe{
   lifeVersion=l?.version??-1,worldVersion=w?.version??-1,worldPresent=w!=null,
   squadMemberIds=l?.players?.Select(p=>p.id).OrderBy(id=>id,StringComparer.Ordinal).ToArray(),facilities=l?.facilities?.ToArray(),staff=l?.staff,
   staffMarketCount=c.staffMarket?.Count??-1,staffOffersCount=c.staffOffers?.Count??-1,
   worldContracts=w?.contracts?.Count??-1,worldRoster=w?.rosterChanges?.Count??-1,worldDivisions=w?.divisions?.Count??-1,worldFixtures=w?.fixtures?.Count??-1,
   accounts=w?.aiAccounts?.Count??-1,incompleteAccounts=w?.aiAccounts?.Count(a=>!a.hasOperatingSnapshot)??-1,dbPlayers=Database.players?.Length??-1,
   initialAiEmployment=w?.initialAiEmployment??false,freeVersion=c.freeAgentsImportVersion,freeLastChecked=c.freeAgentsLastCheckedDay,announcedCount=c.announcedFreeAgentReferences?.Count??-1,processedCount=c.processedFreeAgentReferences?.Count??-1,
   medicalResponsibilityClosures=l?.medical?.Count(m=>!string.IsNullOrEmpty(m.responsibilityEndedReason))??0,freeImported=c.freeAgentsImported,matchPresent=c.match!=null,calendarEpoch=c.calendarEpoch
  });}
 }
}


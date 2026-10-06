using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Touchline.Core;

namespace Touchline.Editor
{
    // Run in an isolated visual-validation session. Does not validate a device.
    [InitializeOnLoad] public static class LoanNegotiationSmoke
    {
        static int stage,frames,last=-1;
        static string player,prePlayer,outgoingPlayer,output;static int savedIncomingUntil,savedOutgoingUntil;static string selectedDestination;
        static RenderTexture target;
        [Serializable] sealed class FailureReport {public int stage;public string error;public long cash,payroll,wageBudget;public TransferOffer lastOffer;public string[] interfaceText;}
        static VisualElement Root=>TouchlineApp.Instance.GetComponent<UIDocument>().rootVisualElement;
        static LoanNegotiationSmoke(){EditorApplication.update+=Tick;}
        public static void Run(){output=Path.GetFullPath("../../artifacts/unity/player-loan-prospects-034");if(File.Exists(Path.Combine(output,"report.json")))throw new Exception("Immutable smoke report already exists.");Directory.CreateDirectory(output);ProjectBuilder.Configure();SessionState.SetBool("LoanNegotiationSmoke",true);EditorApplication.isPlaying=true;}
        static void Require(bool value,string message){if(!value)throw new Exception(message);}
        static void Invoke(string name,params object[] args)=>typeof(TouchlineApp).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(TouchlineApp.Instance,args);
        static void Click(string name){var b=Root.Query<Button>().ToList().First(x=>x.name==name||x.text==name);using(var e=NavigationSubmitEvent.GetPooled()){e.target=b;b.SendEvent(e);}}
        static void Resize(int w,int h){var old=target;target=new RenderTexture(w,h,24);target.Create();TouchlineApp.Instance.GetComponent<UIDocument>().panelSettings.targetTexture=target;if(old!=null){old.Release();UnityEngine.Object.DestroyImmediate(old);}}
        static void Capture(string name){var previous=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);RenderTexture.active=previous;}
        static T Field<T>(string name)where T:VisualElement=>Root.Q<T>("negotiation-"+name);
        static string Money(long value)=>(string)typeof(TouchlineApp).GetMethod("Money",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{value});
        static void CheckParts(int percent)
        {
            Field<IntegerField>("loan-share").value=percent;long borrowed=1001L*percent/100,retained=1001-borrowed;string text=Root.Q<Label>("loan-monthly-contributions").text;
            Require(text.Contains("Votre club : "+Money(Career.MonthlySalary(borrowed))+" / mois")&&text.Contains(Money(Career.MonthlySalary(retained))+" / mois"),"Monthly shares disagree with exact weekly payroll complement.");
        }
        static void CheckLoanDraft()
        {
            Require(Field<Toggle>("loan").value&&!Field<LongField>("monthly-wage").enabledInHierarchy&&Field<LongField>("monthly-wage").value==Career.MonthlySalary(1001),"Loan wage is editable or differs from parent contract.");
            Require(Field<LongField>("fee").value==222222&&Field<IntegerField>("loan-share").value==40&&Field<IntegerField>("loan-days").value==200&&Field<LongField>("loan-option").value==345678&&Field<LongField>("loan-obligation").value==0&&Field<IntegerField>("loan-appearances").value==5&&Field<Toggle>("loan-recall").value,"Loan draft terms lost after switching or viewport change.");
            Require(Field<DropdownField>("role").index==1&&Field<LongField>("bonus").value==111&&Field<LongField>("clause").value==888888,"Loan personal draft fields lost.");
            Require(Root.Q<Label>("negotiation-loan-end-date").text.Contains(Career.Epoch.AddDays(TouchlineApp.Instance.Career.life.day+200).ToString("dd MMM yyyy",System.Globalization.CultureInfo.GetCultureInfo("fr-FR"))),"Loan end date does not match the displayed duration.");
            Require(Root.Query<Label>("negotiation-agent-message").ToList().Count==1&&Root.Query<Label>("negotiation-agent-conditions").ToList().Count==1,"Agent conditions duplicated during toggles.");
        }
        static void Tick()
        {
            if(!SessionState.GetBool("LoanNegotiationSmoke",false)||!EditorApplication.isPlaying||TouchlineApp.Instance==null)return;if(last==Time.frameCount)return;last=Time.frameCount;if(++frames<25)return;frames=0;
            try{
                var app=TouchlineApp.Instance;var career=app.Career;var db=app.Database;
                switch(stage++){
                    case 0:
                        output=Path.GetFullPath("../../artifacts/unity/player-loan-prospects-034");
                        app.GetComponent<UIDocument>().panelSettings=UnityEngine.Object.Instantiate(app.GetComponent<UIDocument>().panelSettings);career.EnsureWorld(db);Resize(1600,700);
                        // Marseille starts above its inherited salary ceiling.
                        // Use the real Burgos job context for a genuinely affordable
                        // signing, rather than changing payroll or bypassing budgets.
                        career.approaches.Add(new JobApproach{club="12597",until=career.life.day+14});career.AnswerApproach(db,"12597",true);Invoke("Navigate","Club");
                        Require(career.WageBudget-career.Payroll(db)>=500&&career.TransferBudget>=222222,"Burgos signing fixture lacks real salary/transfer headroom.");
                        player=db.players.First(candidate=>candidate.team!=career.club&&candidate.team!="free"&&candidate.team!="retired"&&candidate.age>=18&&candidate.wage>0&&db.clubs.Any(c=>c.id==candidate.team)&&candidate.value<1000000&&career.Contract(db,candidate.id).parent==null&&career.Contract(db,candidate.id).until>=career.life.day+200).id;
                        var p=db.Find(player);p.wage=1001;career.Contract(db,p.id).wage=1001;career.world.offers.RemoveAll(o=>o.player==player);
                        career.world.offers.Add(new TransferOffer{player=player,seller=p.team,destination=career.club,status="counter",loan=true,fee=123456,wage=1001,years=4,role="key",bonus=77,clause=654321,due=career.life.day,terms=new MarketTerms{loanWagePercent=40,loanEndDay=career.life.day+180,optionFee=98765,obligationAppearances=5,recall=true}});Invoke("TransferDialog",player);break;
                    case 1:
                        Require(Field<Toggle>("loan").value&&!Field<Toggle>("precontract").value&&Field<LongField>("fee").value==123456&&Field<IntegerField>("years").value==4&&Field<DropdownField>("role").index==2&&Field<LongField>("bonus").value==77&&Field<LongField>("clause").value==654321,"Original loan counteroffer type, fee or personal conditions were overwritten by estimate.");
                        Require(Field<IntegerField>("loan-share").value==40&&Field<IntegerField>("loan-days").value==180&&Field<LongField>("loan-option").value==98765&&Field<Toggle>("loan-recall").value,"Counteroffer loan terms not restored.");
                        CheckParts(0);CheckParts(40);CheckParts(100);
                        foreach(int invalid in new[]{-1,101}){Field<IntegerField>("loan-share").value=invalid;Require(Root.Q<Label>("loan-monthly-contributions").text.Contains("Pourcentage invalide")&&!Field<Button>("send").enabledInHierarchy,"Invalid salary share silently clamped or accepted by form.");}CheckParts(40);
                        Field<LongField>("fee").value=222222;Field<IntegerField>("loan-days").value=200;Field<LongField>("loan-option").value=345678;Field<DropdownField>("role").index=1;Field<LongField>("bonus").value=111;Field<LongField>("clause").value=888888;
                        Field<Toggle>("loan").value=false;Field<LongField>("fee").value=777777;Field<LongField>("monthly-wage").value=12345;Field<IntegerField>("years").value=2;Field<DropdownField>("role").index=2;Field<LongField>("bonus").value=222;Field<LongField>("clause").value=999999;Field<IntegerField>("upfront").value=50;Field<IntegerField>("instalments").value=2;
                        for(int i=0;i<5;i++){Field<Toggle>("loan").value=true;CheckLoanDraft();Field<Toggle>("loan").value=false;Require(Field<LongField>("fee").value==777777&&Field<LongField>("monthly-wage").value==12345&&Field<LongField>("monthly-wage").enabledInHierarchy&&Field<IntegerField>("years").value==2&&Field<DropdownField>("role").index==2&&Field<LongField>("bonus").value==222&&Field<LongField>("clause").value==999999&&Field<IntegerField>("upfront").value==50&&Field<IntegerField>("instalments").value==2,"Permanent transfer draft lost across loan toggle.");}
                        Field<Toggle>("loan").value=true;Capture("loan-counter-wide");Resize(1080,2520);break;
                    case 2:CheckLoanDraft();Capture("loan-counter-portrait");Click("negotiation-send");break;
                    case 3:
                        var offer=career.world.offers.Last(o=>o.player==player);Require(offer.status=="pending"&&offer.loan&&offer.fee==222222&&offer.wage==1001&&offer.role=="starter"&&offer.bonus==111&&offer.clause==888888&&offer.terms.loanWagePercent==40&&offer.terms.loanEndDay==career.life.day+200&&offer.terms.optionFee==345678&&offer.terms.obligationAppearances==5&&offer.terms.recall&&offer.terms.upfrontPercent==100&&offer.terms.instalments==0,"Submitted loan differs from reviewed draft or inherited permanent payment schedule.");
                        prePlayer=db.players.First(q=>q.id!=player&&q.team!=career.club&&q.team!="free"&&q.team!="retired"&&q.age>=18&&q.wage>0&&db.clubs.Any(c=>c.id==q.team)).id;var pp=db.Find(prePlayer);career.Contract(db,prePlayer).until=career.life.day+100;career.world.offers.RemoveAll(o=>o.player==prePlayer);career.world.offers.Add(new TransferOffer{player=prePlayer,seller=pp.team,destination=career.club,status="counter",precontract=true,fee=0,wage=900,years=2,role="starter",bonus=5,clause=123456,due=career.life.day,terms=new MarketTerms{upfrontPercent=50,instalments=2}});Invoke("TransferDialog",prePlayer);break;
                    case 4:
                        Require(Field<Toggle>("precontract").value&&!Field<Toggle>("loan").value&&Field<LongField>("fee").value==0&&Field<LongField>("monthly-wage").value==Career.MonthlySalary(900)&&Field<IntegerField>("years").value==2&&Field<DropdownField>("role").index==1&&Field<LongField>("bonus").value==5&&Field<LongField>("clause").value==123456&&Field<IntegerField>("upfront").value==50&&Field<IntegerField>("instalments").value==2,"Precontract counteroffer lost its original type or fields.");
                        Field<Toggle>("loan").value=true;Field<Toggle>("loan").value=false;Require(Field<Toggle>("precontract").value&&Field<LongField>("fee").value==0&&Field<LongField>("monthly-wage").value==Career.MonthlySalary(900),"Precontract draft lost across loan toggle.");Capture("precontract-counter-portrait");
                        string own=career.lineup[0];var old=career.Contract(db,own);old.parent=db.Find(player).team;old.terms=null;old.loanUntil=career.life.day+90;app.PlayerProfile(own);Click("Contrat");break;
                    case 5:
                        var fact=Root.Query<Label>().ToList().First(l=>l.text=="Salaire pris en charge");Require(fact.parent.Query<Label>().ToList().Any(l=>l.text=="50 %"),"Old loan salary share displayed 100 instead of the payroll's legacy 50 percent.");Capture("legacy-loan-profile");
                        savedIncomingUntil=career.Contract(db,prePlayer).until;career.Contract(db,prePlayer).until=career.life.day+70;career.world.offers.RemoveAll(o=>o.player==prePlayer);Invoke("TransferDialog",prePlayer);break;
                    case 6:
                        Field<Toggle>("loan").value=true;Require(Field<IntegerField>("loan-days").value>=28&&Field<IntegerField>("loan-days").value<=70,"Incoming default extends beyond parent contract.");
                        foreach(int invalid in new[]{int.MinValue,0,27,71,366,int.MaxValue}){Field<IntegerField>("loan-days").value=invalid;Require(!Field<Button>("send").enabledInHierarchy&&Root.Q<Label>("negotiation-loan-end-date").text.Contains("Durée invalide"),"Incoming invalid loan duration remains actionable: "+invalid);}
                        foreach(int valid in new[]{28,70}){Field<IntegerField>("loan-days").value=valid;Require(Field<Button>("send").enabledInHierarchy,"Incoming inclusive loan boundary is rejected: "+valid);}
                        Field<IntegerField>("loan-days").value=71;Field<Toggle>("loan").value=false;Field<Toggle>("loan").value=true;Require(Field<IntegerField>("loan-days").value==71&&!Field<Button>("send").enabledInHierarchy,"Invalid incoming duration was silently clamped across modes.");Capture("incoming-duration-bounds");career.Contract(db,prePlayer).until=career.life.day+27;Invoke("TransferDialog",prePlayer);break;
                    case 7:
                        Require(!Field<Toggle>("loan").enabledInHierarchy&&Root.Q<Label>("negotiation-loan-unavailable").text.Contains("moins de 28 jours"),"Incoming loan remains available when parent expires before minimum duration.");Capture("incoming-parent-too-short");career.Contract(db,prePlayer).until=savedIncomingUntil;
                        outgoingPlayer=career.lineup.First(id=>db.Find(id).age>=18&&career.Contract(db,id).parent==null);savedOutgoingUntil=career.Contract(db,outgoingPlayer).until;career.Contract(db,outgoingPlayer).until=career.life.day+65;Invoke("YouthLoanDialog",outgoingPlayer);break;
                    case 8:
                        var outgoingDays=Root.Q<IntegerField>("outgoing-loan-days");var outgoingSend=Root.Q<Button>("outgoing-loan-send");Require(outgoingDays.value==65,"Outgoing default extends beyond parent contract.");
                        foreach(int invalid in new[]{int.MinValue,0,27,66,366,int.MaxValue}){outgoingDays.value=invalid;Require(!outgoingSend.enabledInHierarchy&&Root.Q<Label>("outgoing-loan-end-date").text.Contains("Durée invalide"),"Outgoing invalid loan duration remains actionable: "+invalid);}
                        foreach(int valid in new[]{28,65}){outgoingDays.value=valid;Require(outgoingSend.enabledInHierarchy,"Outgoing inclusive loan boundary is rejected: "+valid);}
                        foreach(int invalidShare in new[]{-1,101}){Root.Q<IntegerField>("outgoing-loan-share").value=invalidShare;Require(!outgoingSend.enabledInHierarchy&&Root.Q<Label>("outgoing-loan-share-error").style.display.value!=DisplayStyle.None,"Outgoing invalid salary share remains actionable.");}Root.Q<IntegerField>("outgoing-loan-share").value=50;Require(outgoingSend.enabledInHierarchy,"Correcting outgoing share did not restore submission.");Capture("outgoing-duration-bounds");career.Contract(db,outgoingPlayer).until=career.life.day+27;Invoke("YouthLoanDialog",outgoingPlayer);break;
                    case 9:
                        Require(!Root.Q<Button>("outgoing-loan-send").enabledInHierarchy&&Root.Q<Label>("outgoing-loan-end-date").text.Contains("moins de 28 jours"),"Outgoing loan remains actionable when parent expires before minimum duration.");Capture("outgoing-parent-too-short");career.Contract(db,outgoingPlayer).until=savedOutgoingUntil;
                        Root.Q<IntegerField>("outgoing-loan-days").value=60;Root.Q<IntegerField>("outgoing-loan-share").value=35;Root.Q<LongField>("outgoing-loan-fee").value=1234;Root.Q<LongField>("outgoing-loan-option").value=8888;Root.Q<IntegerField>("outgoing-loan-appearances").value=5;Root.Q<Toggle>("outgoing-loan-recall").value=true;
                        var friendlyIds=career.FriendlyRecommendations(db).Select(c=>c.id).ToArray();var destinations=career.LoanClubRecommendations(db,outgoingPlayer,new MarketTerms{loanEndDay=career.life.day+60,loanWagePercent=35},1234);Require(destinations.Count>12,"Real loan catalogue lacks destinations beyond friendly twelve.");var alternative=destinations.First(r=>!friendlyIds.Contains(r.club.id));selectedDestination=alternative.club.id;
                        Root.Q<DropdownField>("outgoing-loan-mode").index=1;Root.Q<TextField>("outgoing-loan-search").value=alternative.club.name;var picker=Root.Q<DropdownField>("outgoing-loan-club");picker.index=picker.choices.FindIndex(label=>label.StartsWith(alternative.club.name+" · ",StringComparison.Ordinal));break;
                    case 10:
                        Require(Root.Q<Label>("outgoing-loan-selection").text.Contains(db.clubs.First(c=>c.id==selectedDestination).name),"Selected real club outside friendly recommendations is missing.");
                        Root.Q<TextField>("outgoing-loan-search").value="NO-MATCHING-CLUB-AUDIT-034";Require(Root.Q<DropdownField>("outgoing-loan-club").choices.Count==1&&Root.Q<Label>("outgoing-loan-selection").text.Contains("sélection conservée"),"Searching silently replaced the selected destination.");
                        Require(Root.Q<IntegerField>("outgoing-loan-days").value==60&&Root.Q<IntegerField>("outgoing-loan-share").value==35&&Root.Q<LongField>("outgoing-loan-fee").value==1234&&Root.Q<LongField>("outgoing-loan-option").value==8888&&Root.Q<IntegerField>("outgoing-loan-appearances").value==5&&Root.Q<Toggle>("outgoing-loan-recall").value,"Destination changes destroyed negotiated terms.");Capture("all-loan-destinations-preserve-terms");Click("outgoing-loan-send");break;
                    case 11:
                        var sent=career.outgoingLoans.Last(o=>o.player==outgoingPlayer);Require(sent.borrower==selectedDestination&&sent.fee==1234&&sent.terms.loanEndDay==career.life.day+60&&sent.terms.loanWagePercent==35&&sent.terms.optionFee==8888&&sent.terms.obligationAppearances==5&&sent.terms.recall,"Submitted loan differs from selected destination and preserved clauses.");
                        RetiredProfileSmokeChecks.RetireKnownFixtureAndOpen(app);Resize(1600,700);break;
                    case 12:
                        Capture("retired-overview-wide");RetiredProfileSmokeChecks.AssertOverviewAndOpenContract(app);break;
                    case 13:
                        RetiredProfileSmokeChecks.AssertContract(app);Capture("retired-contract-wide");Resize(1080,2520);break;
                    case 14:
                        RetiredProfileSmokeChecks.AssertContract(app);Capture("retired-contract-portrait");
                        RetiredMessageSmokeChecks.OpenOldTransferThread(app);Resize(1600,700);break;
                    case 15:
                        RetiredMessageSmokeChecks.AssertOldThread(app);Capture("retired-old-message-wide");
                        RetiredMessageSmokeChecks.AssertDirectReviewsAndDialogAreReadOnly(app);Resize(1080,2520);break;
                    case 16:
                        RetiredMessageSmokeChecks.AssertOldThread(app);Capture("retired-old-message-portrait");
                        HistoricalMedicalSmokeChecks.PrepareHistoricalCaseAndOpen(app);Resize(1600,700);break;
                    case 17:
                        HistoricalMedicalSmokeChecks.AssertReadOnlyHistory(app);Capture("medical-history-wide");Resize(1080,2520);break;
                    case 18:
                        HistoricalMedicalSmokeChecks.AssertReadOnlyHistory(app);Capture("medical-history-portrait");
                        File.WriteAllText(Path.Combine(output,"report.json"),"{\"passed\":true,\"parentSalaryReadOnly\":true,\"exactMonthlyContributions0_40_100\":true,\"invalidShareErrorWithoutClamp\":true,\"counterofferLoanAndPrecontractRestore\":true,\"independentTransferAndLoanDrafts\":true,\"agentConditionsDoNotDuplicate\":true,\"loanDraftSurvivesViewportChange\":true,\"submittedOfferMatchesReviewedDraft\":true,\"legacyLoanProfileUses50Percent\":true,\"syntheticAgreementTerms\":true,\"incomingOutgoingDurationBounds\":true,\"parentContractCapsDefaults\":true,\"expiredParentDisablesLoan\":true,\"invalidDurationDraftPreserved\":true,\"searchAllEligibleClubsBeyondFriendlies\":true,\"destinationChangesPreserveTerms\":true,\"retiredKnownArchiveAndHistoricalSalary\":true,\"retiredProfileWidePortrait\":true,\"retiredOldMailDecisionsClosed\":true,\"retiredOldMailWidePortrait\":true,\"retiredDirectReviewsSignatureDisabled\":true,\"retiredDirectDialogReadOnlyBlocked\":true,\"medicalLegacyProbeDetectsClosure\":true,\"medicalHistoryReadOnlyNoSave\":true,\"medicalHistoryWidePortrait\":true,\"physicalAndroid\":false}");SessionState.SetBool("LoanNegotiationSmoke",false);Debug.Log("TOUCHLINE_LOAN_NEGOTIATION_OK");EditorApplication.Exit(0);break;
                }
            }catch(Exception e){
                if(output!=null&&TouchlineApp.Instance?.Career?.world!=null){var app=TouchlineApp.Instance;File.WriteAllText(Path.Combine(output,"failure.json"),JsonUtility.ToJson(new FailureReport{stage=stage,error=e.Message,cash=app.Career.life.cash,payroll=app.Career.Payroll(app.Database),wageBudget=app.Career.WageBudget,lastOffer=app.Career.world.offers.LastOrDefault(o=>o.player==player),interfaceText=Root.Query<Label>().ToList().Select(l=>l.text).ToArray()},true));}
                SessionState.SetBool("LoanNegotiationSmoke",false);Debug.LogException(e);EditorApplication.Exit(1);
            }
        }
    }
}

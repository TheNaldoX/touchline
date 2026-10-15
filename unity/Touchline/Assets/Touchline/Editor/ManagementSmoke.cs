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
    [InitializeOnLoad] public static class ManagementSmoke
    {
        static int stage,frames,last=-1,beforeDay,readMessage,medicalMessage;static string contactPlayer,developmentPlayer;static float readScroll;static RenderTexture target;static string output;
        static TransferOffer incomingAgreement;static OutgoingLoanOffer outgoingAgreement;static StaffOffer staffAgreement;static int agreementMessage;
        static ClubLife originalNotificationLife;static ClubMessage maskedNotification,reusedNotification;
        static ClubMessage auditUnread,auditOther,auditUrgent;static int auditSaveRequests;static int[] auditDismissed;static string auditInjured;
        static TransferOffer deadlineSoon,deadlineLater;static ClubMessage deadlineMail,sponsorMail,approachMail;static CommercialDeal reviewedSponsor;static JobApproach reviewedApproach;static string promisePlayer;static int savedPromiseUntil,savedPromiseStarts,savedPromiseAppearances;
        static long rejectedSignatureCash;static string plannedBorrower;
        static VisualElement Root=>TouchlineApp.Instance.GetComponent<UIDocument>().rootVisualElement;
        static ManagementSmoke(){EditorApplication.update+=Tick;}
        public static void Run(){ProjectBuilder.Configure();SessionState.SetBool("ManagementSmoke",true);EditorApplication.isPlaying=true;}
        static void Click(string id){var b=Root.Query<Button>().ToList().FirstOrDefault(x=>x.name==id||x.text==id);if(b==null)throw new Exception("Management button missing: "+id);using(var e=NavigationSubmitEvent.GetPooled()){e.target=b;b.SendEvent(e);}}
        static void Resize(int width,int height){var old=target;target=new RenderTexture(width,height,24);target.Create();TouchlineApp.Instance.GetComponent<UIDocument>().panelSettings.targetTexture=target;if(old!=null){old.Release();UnityEngine.Object.DestroyImmediate(old);}}
        static void Capture(string name){var old=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);RenderTexture.active=old;}
        static void OpenMail(int id)=>typeof(TouchlineApp).GetMethod("OpenMessage",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(TouchlineApp.Instance,new object[]{id});
        static void AgreementBounds(string panelId,string signId)
        {
            var panel=Root.Q(panelId);var button=Root.Q<Button>(signId);var bounds=Root.worldBound;
            string geometry="panel="+panel?.worldBound+"; button="+button?.worldBound+"; root="+bounds+"; enabled="+button?.enabledInHierarchy+"; minHeight="+button?.resolvedStyle.minHeight+"; manager="+TouchlineApp.Instance.Career.world.managerStatus+"; suspension="+TouchlineApp.Instance.Career.life.managerBanUntil+"; day="+TouchlineApp.Instance.Career.life.day+"; tooltip="+button?.tooltip;
            Debug.Log("AGREEMENT_GEOMETRY "+signId+" "+geometry);File.WriteAllText(Path.Combine(output,signId+"-geometry.txt"),geometry);
            if(panel==null||button==null||!button.enabledInHierarchy||button.worldBound.width<43||button.worldBound.height<43||button.worldBound.yMin<bounds.yMin||button.worldBound.yMax>bounds.yMax+1||button.worldBound.xMax>bounds.xMax+1)throw new Exception("Agreement signature control is inaccessible: "+signId+" "+geometry);
        }
        static string SignatureOutcome(string name,string player,string status)
        {
            var app=TouchlineApp.Instance;var message=app.Career.life.messages.FirstOrDefault(m=>m.id==agreementMessage);
            string ui=string.Join(" | ",Root.Q(className:"modal-panel")?.Query<Label>().ToList().Select(l=>l.text)??Enumerable.Empty<string>());
            string result="status="+status+"; team="+app.Database.Find(player)?.team+"; club="+app.Career.club+"; needsDecision="+(message!=null&&app.Career.MessageNeedsDecision(message))+"; cash="+app.Career.life.cash+"; payroll="+app.Career.Payroll(app.Database)+"; wageBudget="+app.Career.WageBudget+"; reserved="+app.Career.ReservedWages+"; ui="+ui;
            Capture(name);File.WriteAllText(Path.Combine(output,name+"-outcome.txt"),result);Debug.Log("AGREEMENT_OUTCOME "+result);return ui;
        }
        static float Strength(TouchlineApp app,string club)=>(float)typeof(Career).GetMethod("Strength",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(app.Career,new object[]{app.Database,club});
        static void TwoMarketDays(TouchlineApp app){for(int i=0;i<2;i++){if(app.Career.life.day>=app.Career.life.nextFixture)throw new Exception("Market fixture overlaps a scheduled match");app.Career.AdvanceDay(app.Database);}}
        static void NegotiateRealIncoming(TouchlineApp app)
        {
            var career=app.Career;var db=app.Database;
            var clubs=db.clubs.Where(t=>t.playable&&!t.reserve&&t.id!=career.club&&t.annualRevenue>=10000000&&db.Squad(t.id).Count>=19).OrderBy(t=>t.annualRevenue).ToArray();
            var strengths=clubs.ToDictionary(t=>t.id,t=>Strength(app,t.id));string destination=null,playerId=null;
            foreach(var team in clubs)
            {
                long staff=career.staffMarket.Where(s=>s.club==team.id).Sum(s=>s.wage)*52;
                long margin=Career.GrossWageCeiling(team.league,team.annualRevenue,staff)-db.Squad(team.id).Sum(p=>p.wage);
                if(margin<=1000)continue;
                var candidate=db.players.Where(p=>p.team=="free"&&p.age>=18&&p.rating<strengths[team.id]+8&&(long)(p.wage*1.2f)+1<=margin).OrderByDescending(p=>p.rating).FirstOrDefault(p=>clubs.Any(b=>b.id!=team.id&&p.rating>=strengths[b.id]-5&&p.rating<=strengths[b.id]+15&&1000+((long)(p.wage*1.2f)+1)*60/100*14<=b.annualRevenue/20));
                if(candidate==null)continue;destination=team.id;playerId=candidate.id;
                plannedBorrower=clubs.First(b=>b.id!=team.id&&candidate.rating>=strengths[b.id]-5&&candidate.rating<=strengths[b.id]+15&&1000+((long)(candidate.wage*1.2f)+1)*60/100*14<=b.annualRevenue/20).id;break;
            }
            if(destination==null)throw new Exception("No real club and free player meet the model's wage and loan constraints");
            career.approaches.Add(new JobApproach{club=destination,until=career.life.day+14});career.AnswerApproach(db,destination,true);
            var player=db.Find(playerId);long wage=(long)(player.wage*1.2f)+1;
            if(career.Payroll(db)+career.ReservedWages+wage>career.WageBudget)throw new Exception("Chosen real club has insufficient actual wage headroom");
            career.ProposeTransfer(db,playerId,0,wage,3,"rotation");TwoMarketDays(app);
            incomingAgreement=career.world.offers.Last(o=>o.player==playerId&&o.destination==career.club);
            if(incomingAgreement.status!="accepted")throw new Exception("The actual agent did not accept the incoming offer: "+incomingAgreement.status);
            var mail=career.life.messages.Last(m=>m.player==playerId&&m.subject=="Accord de principe");agreementMessage=mail.id;Click("Messages");OpenMail(agreementMessage);
        }
        static void Bounds()
        {
            var rootBounds=Root.worldBound;
            var indicator=Root.Q<ScrollView>("manager-dashboard").verticalScroller;
            var thumb=indicator.Q(className:"unity-base-slider__dragger");
            if(indicator.worldBound.width>12||thumb==null||thumb.worldBound.width>8)throw new Exception("Scroll indicator inherits oversized form styling");
            foreach(var b in Root.Q(className:"navigation").Query<Button>().ToList())if(b.worldBound.width<40||b.worldBound.height<40||b.worldBound.xMin<0||b.worldBound.yMin<0||b.worldBound.xMax>rootBounds.xMax+1||b.worldBound.yMax>rootBounds.yMax+1)throw new Exception("Navigation out of bounds: "+b.text);
            foreach(var name in new[]{"manager-next-fixture","manager-decisions"}){var card=Root.Q(name);if(card==null||card.worldBound.width<100||card.worldBound.xMin<0||card.worldBound.xMax>rootBounds.xMax+1)throw new Exception("Dashboard card overflows: "+name);}
            if(Root.Query<Button>(className:"manager-metric").ToList().Count!=4)throw new Exception("Four club indicators required");
        }
        static void FieldGeometry(VisualElement field,string expected)
        {
            var input=field?.Q(className:"unity-base-field__input");if(input==null)throw new Exception("Missing input visual for "+field?.name);
            var outer=field.worldBound;var inner=input.worldBound;
            if(inner.width<80||inner.height<36||inner.yMin<outer.yMin-1||inner.yMax>outer.yMax+1)throw new Exception("Field input is clipped or collapsed: "+field.name+" "+outer+" / "+inner);
            var text=field.Query<TextElement>().ToList().FirstOrDefault(t=>t.text==expected);
            if(text==null||text.worldBound.height<12||text.worldBound.yMin<inner.yMin-1||text.worldBound.yMax>inner.yMax+1)throw new Exception("Field selected text is not visible inside input: "+field.name+" / "+expected);
        }
        static void Tick()
        {
            if(!SessionState.GetBool("ManagementSmoke",false)||!EditorApplication.isPlaying||TouchlineApp.Instance==null)return;if(last==Time.frameCount)return;last=Time.frameCount;if(++frames<20)return;frames=0;
            try{var app=TouchlineApp.Instance;output=Path.GetFullPath("../../artifacts/unity/management");Directory.CreateDirectory(output);
                switch(stage++){
                    case 0:app.GetComponent<UIDocument>().panelSettings=UnityEngine.Object.Instantiate(app.GetComponent<UIDocument>().panelSettings);app.Career.EnsureWorld(app.Database);Resize(1280,900);Click("Bureau");break;
                    case 1:
                        Bounds();var payrollWarning=Root.Q<Button>("manager-decision-payroll");long currentPayroll=app.Career.Payroll(app.Database);
                        if(currentPayroll<=app.Career.WageBudget||payrollWarning==null||!payrollWarning.ClassListContains("manager-decision-warning"))throw new Exception("The real Marseille salary ceiling excess is missing from the dashboard");
                        string amount=(string)typeof(TouchlineApp).GetMethod("Money",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{Career.MonthlySalary(currentPayroll)});string ceiling=(string)typeof(TouchlineApp).GetMethod("Money",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{Career.MonthlySalary(app.Career.WageBudget)});
                        if(!payrollWarning.Query<Label>().ToList().Any(l=>l.text.Contains(amount+" / mois")&&l.text.Contains("plafond "+ceiling+" / mois")))throw new Exception("Salary warning does not show actual monthly payroll and ceiling");
                        Capture("dashboard-payroll-warning");Click("manager-decision-payroll");if(Root.Q("finance-budget-explanation")==null)throw new Exception("Salary warning does not lead to Finances");Click("Bureau");Capture("dashboard-wide");app.Career.OpenInjury(app.Database,app.Career.lineup[9],"bruise");app.Career.life.projects.Add(new FacilityProject{kind="training",status="approved",cost=100000,contribution=50000});app.Career.world.offers.Add(new TransferOffer{player=app.Career.lineup[8],destination=app.Career.club,status="accepted",due=app.Career.life.day});Click("Bureau");break;
                    case 2:foreach(var name in new[]{"medical","facilities","offers"})if(Root.Q("manager-decision-"+name)==null)throw new Exception("Actionable dossier missing: "+name);Capture("dashboard-decisions");Click("manager-decision-medical");break;
                    case 3:if(!Root.Query<Label>().ToList().Any(l=>l.text=="Centre médical"))throw new Exception("Medical shortcut failed");var injury=app.Career.Injury(app.Career.lineup[9]);app.Career.Treat(app.Database,injury.id,"conservative");Click("Bureau");Resize(1600,700);break;
                    case 4:Bounds();if(Root.Q("manager-decision-medical")!=null)throw new Exception("Resolved medical decision remains");Capture("dashboard-landscape");Resize(1080,2520);break;
                    case 5:Bounds();Capture("dashboard-portrait");app.Career.SetDelegation("training",true);Click("Bureau");break;
                    case 6:if(Root.Q<DropdownField>("manager-training-load").enabledInHierarchy)throw new Exception("Delegated workload should not be editable");app.Career.SetDelegation("training",false);Click("Bureau");break;
                    case 7:var field=Root.Q<DropdownField>("manager-training-load");if(!field.enabledInHierarchy)throw new Exception("Manual workload inaccessible");field.index=2;if(app.Career.life.training!="heavy")throw new Exception("Workload did not update");beforeDay=app.Career.life.day;Click("manager-continue");break;
                    case 8:if(app.Career.life.day!=beforeDay+1)throw new Exception("Continue did not advance one day");readMessage=app.Career.life.messages.Last().id;app.Career.life.messages.Last().read=false;Click("Bureau");break;
                    case 9:Root.Q<ScrollView>("manager-dashboard").scrollOffset=new Vector2(0,120);break;
                    case 10:readScroll=Root.Q<ScrollView>("manager-dashboard").scrollOffset.y;Click("manager-message-"+readMessage);break;
                    case 11:
                        int unread=app.Career.life.messages.Count(m=>!m.read);
                        if(Root.Q<Button>("manager-inbox-open").text!="Messagerie · "+unread+" non lu(s)"||Root.Q<Label>("manager-message-meta-"+readMessage).text.Contains("NON LU"))throw new Exception("Read message counters remain stale");
                        if(Root.Q(className:"modal-panel")==null||Mathf.Abs(Root.Q<ScrollView>("manager-dashboard").scrollOffset.y-readScroll)>.5f)throw new Exception("Reading a message lost its modal or dashboard scroll");
                        Click("Fermer");app.Career.SetDelegation("training",true);app.Career.Staff("fitness").wage=0;Click("Bureau");break;
                    case 12:
                        if(!Root.Q<DropdownField>("manager-training-load").enabledInHierarchy||!Root.Query<Label>().ToList().Any(l=>l.text.Contains("poste de préparateur vacant")))throw new Exception("Vacant preparer leaves inaccessible or misleading workload");Capture("dashboard-vacant-staff");
                        contactPlayer=app.Career.lineup[8];app.Career.OpenInjury(app.Database,contactPlayer,"bruise");medicalMessage=app.Career.life.messages.Last().id;app.Career.life.messages.Last().read=true;app.Career.Mail("Adjoint","Note conservée","Une information épinglée ne doit pas masquer une décision médicale.",contactPlayer,"talk").pinned=true;Click("Bureau");break;
                    case 13:
                        if(Root.Q("manager-notification")==null||Root.Q<Button>("manager-notification-open")==null)throw new Exception("Pending read decision must keep important notification");Capture("dashboard-important-notification");Click("manager-notification-open");break;
                    case 14:
                        if(Root.Q("inbox-conversation")==null||!app.Career.MessageNeedsDecision(app.Career.life.messages.First(m=>m.id==medicalMessage)))throw new Exception("Reading lost medical decision");if(Root.Q("inbox-message-"+medicalMessage)?.ClassListContains("conversation-selected")!=true)throw new Exception("Pinned routine message masks pending medical decision");Capture("inbox-thread");Click("Fermer");Click("À traiter");Root.Q<TextField>("inbox-search").value=app.Database.Find(contactPlayer).name;break;
                    case 15:
                        if(Root.Query<VisualElement>(className:"inbox-thread").ToList().Count!=1)throw new Exception("Inbox contextual search should isolate player thread");Click("inbox-mark-visible-read");if(!app.Career.MessageNeedsDecision(app.Career.life.messages.First(m=>m.id==medicalMessage)))throw new Exception("Mark read resolved actionable decision");Capture("inbox-actionable-search");Click("Lire et décider");Click("inbox-pin");break;
                    case 16:
                        if(!app.Career.life.messages.First(m=>m.id==medicalMessage).pinned)throw new Exception("Inbox pin failed");PlayerDiscussionSmokeChecks.AssertThreadReadNoop(app,medicalMessage);Click("Fermer");Click("Contacter un joueur");Root.Q<TextField>("inbox-contact-search").value=app.Database.Find(contactPlayer).name;break;
                    case 17:
                        if(Root.Query<VisualElement>(className:"inbox-contact").ToList().Count!=1)throw new Exception("Contact search must isolate selected squad member");Click("inbox-contact-"+contactPlayer);break;
                    case 18:
                        if(Root.Q("player-conversation")==null)throw new Exception("Contextual player discussion unavailable");Root.Q<DropdownField>("conversation-topic-picker").value="Charge et récupération";
                        Root.Q<DropdownField>("conversation-channel").index=1;
                        if(Root.Q<Button>("conversation-send").text!="Aborder pendant l’appel")throw new Exception("Call selector retained an SMS action label");
                        Root.Q<DropdownField>("conversation-channel").index=0;
                        if(Root.Q<Button>("conversation-send").text!="Envoyer le SMS")throw new Exception("SMS selector retained a call action label");
                        Root.Q<DropdownField>("conversation-channel").index=1;Capture("player-private-conversation");Click("conversation-send");
                        if(!app.Career.life.messages.Any(m=>m.player==contactPlayer&&m.sender=="Vous"&&m.subject=="Appel • compte rendu")||app.Career.life.messages.Last(m=>m.player==contactPlayer).subject!="Après notre appel")throw new Exception("Call action did not preserve the chosen channel in the conversation history");break;
                    case 19:
                        if(app.Career.Person(contactPlayer).restUntil!=app.Career.life.day+3)throw new Exception("Recovery conversation did not enforce agreed rest");Click("Fermer");Click("Contacter un joueur");Click("inbox-contact-"+contactPlayer);if(Root.Q<Button>("conversation-send").enabledInHierarchy)throw new Exception("Repeated conversation bypassed shared cooldown");Click("Fermer");Click("Messages");Root.Q<TextField>("inbox-search").value=app.Database.Find(contactPlayer).name;Root.Q<DropdownField>("inbox-category").value="Médical";Click("À traiter");break;
                    case 20:
                        var filters=Root.Q("inbox-primary-filters").Query<Button>().ToList();if(filters.Count!=4||filters.Max(b=>b.worldBound.y)-filters.Min(b=>b.worldBound.y)>1)throw new Exception("Four main inbox filters must fit on one row");
                        FieldGeometry(Root.Q<TextField>("inbox-search"),app.Database.Find(contactPlayer).name);FieldGeometry(Root.Q<DropdownField>("inbox-category"),"Médical");
                        foreach(var preview in Root.Query<Label>(className:"inbox-thread-preview").ToList())if(preview.resolvedStyle.height>32.5f)throw new Exception("Inbox preview inherited oversized dashboard styling");
                        if(Root.Query<VisualElement>(className:"inbox-thread").ToList().Count!=1||Root.Q<DropdownField>("inbox-category").value!="Médical")throw new Exception("Independent category and decision filters failed");Capture("inbox-compact-filters");Root.Q<DropdownField>("inbox-category").value="Joueurs";break;
                    case 21:
                        if(Root.Query<VisualElement>(className:"inbox-thread").ToList().Count!=0)throw new Exception("Category change ignored decision filter");Root.Q<DropdownField>("inbox-category").value="Toutes";Root.Q<TextField>("inbox-search").value="";Click("Tous");
                        var path=app.Career.world.youth.First(y=>app.Database.Find(y.player)?.team=="academy-"+app.Career.club);developmentPlayer=path.player;app.Career.PromoteYouth(app.Database,developmentPlayer);app.Database.Find(developmentPlayer).age=19;Click("Contacter un joueur");Root.Q<TextField>("inbox-contact-search").value=app.Database.Find(developmentPlayer).name;Click("inbox-contact-"+developmentPlayer);Root.Q<DropdownField>("conversation-topic-picker").value="Parcours et développement";Click("conversation-send");break;
                    case 22:
                        if(Root.Q<Button>("conversation-open-development")==null)throw new Exception("Development exchange has no actionable follow-through");Click("conversation-open-development");break;
                    case 23:
                        if(Root.Q("conversation-development-plan")==null||Root.Q<Button>("conversation-development-loan")==null||Root.Q<Button>("conversation-development-training")==null)throw new Exception("Development plan lacks real training and loan links");
                        Root.Q<DropdownField>("conversation-development-focus").index=1;Click("conversation-development-save");if(app.Career.world.youth.First(y=>y.player==developmentPlayer).focus!="physical")throw new Exception("Individual formation axis was not saved");break;
                    case 24:
                        FieldGeometry(Root.Q<DropdownField>("conversation-development-focus"),"Physique");FieldGeometry(Root.Q<DropdownField>("conversation-development-mentor"),"Sans mentor");
                        var advice=Root.Q<Label>(className:"conversation-plan-advice");if(advice==null||advice.resolvedStyle.fontSize>14||advice.ClassListContains("section-title"))throw new Exception("Development advice must use readable body typography");Capture("conversation-development-actions");Click("conversation-development-training");break;
                    case 25:
                        if(Root.Q<DropdownField>("manager-training-load")==null)throw new Exception("Development training link did not open actual workload");Click("Messages");Click("Contacter un joueur");Click("inbox-contact-"+developmentPlayer);Click("conversation-open-development");Click("conversation-development-loan");break;
                    case 26:
                        if(!Root.Query<Label>().ToList().Any(l=>l.text.StartsWith("Proposer un prêt · ")))throw new Exception("Development loan link did not open negotiation");Click("Fermer");app.Career.world.managerStatus="dismissed";Click("Bureau");break;
                    case 27:
                        if(Root.Q("manager-training-load")!=null||Root.Query<Button>(className:"manager-metric").ToList().Count!=0||Root.Query<Button>(className:"manager-decision").ToList().Count!=0)throw new Exception("Dismissed manager retains club decisions on dashboard");
                        if(!Root.Query<Button>().ToList().Any(b=>b.text=="Voir les opportunités"))throw new Exception("Dismissed manager needs career access");Capture("dashboard-dismissed");Click("Tactique");break;
                    case 28:
                        if(!Root.Query<Label>().ToList().Any(l=>l.text=="Votre parcours")||Root.Q(className:"tactics-board")!=null)throw new Exception("Dismissed tactical link should redirect to career");
                        Click("Plus");Click("Finances");break;
                    case 29:
                        if(!Root.Query<Label>().ToList().Any(l=>l.text=="Votre parcours"))throw new Exception("Dismissed directory link should redirect to career");
                        app.Career.world.managerStatus="employed";app.Career.life.managerBanUntil=0;Click("Messages");
                        var free=app.Database.players.First(p=>p.team=="free"&&p.age>=18&&p.wage<=500);incomingAgreement=new TransferOffer{player=free.id,seller="free",destination=app.Career.club,status="accepted",due=app.Career.life.day,wage=Math.Max(Math.Max(100,free.wage),Math.Max(0,app.Career.WageBudget-app.Career.Payroll(app.Database)-app.Career.ReservedWages)+1),years=3,role="rotation"};app.Career.world.offers.Add(incomingAgreement);
                        rejectedSignatureCash=app.Career.life.cash;agreementMessage=app.Career.Mail("Agent de "+free.name,"Accord de principe","Accord à vérifier puis signer.",free.id,"transfer").id;OpenMail(agreementMessage);break;
                    case 30:Click("inbox-sign-transfer");break;
                    case 31:
                        Capture("incoming-agreement-portrait");AgreementBounds("transfer-agreement-review","agreement-sign-transfer");Click("agreement-sign-transfer");break;
                    case 32:
                        var rejectedUi=SignatureOutcome("incoming-budget-rejected",incomingAgreement.player,incomingAgreement.status);
                        if(incomingAgreement.status!="accepted"||app.Database.Find(incomingAgreement.player).team!="free"||app.Career.life.cash!=rejectedSignatureCash||!app.Career.MessageNeedsDecision(app.Career.life.messages.First(m=>m.id==agreementMessage))||!rejectedUi.Contains("Budget salarial insuffisant"))throw new Exception("Insufficient wage budget must display its error and preserve the unsigned agreement");
                        Click("Fermer");app.Career.RejectOffer(incomingAgreement.player);NegotiateRealIncoming(app);break;
                    case 33:Click("inbox-sign-transfer");break;
                    case 34:
                        Capture("incoming-valid-agreement-portrait");AgreementBounds("transfer-agreement-review","agreement-sign-transfer");Click("agreement-sign-transfer");break;
                    case 35:
                        SignatureOutcome("incoming-signed",incomingAgreement.player,incomingAgreement.status);
                        if(incomingAgreement.status!="signed"||app.Database.Find(incomingAgreement.player).team!=app.Career.club||app.Career.MessageNeedsDecision(app.Career.life.messages.First(m=>m.id==agreementMessage)))throw new Exception("Inbox signature did not resolve incoming agreement");
                        app.Career.ProposeOutgoingLoan(app.Database,incomingAgreement.player,plannedBorrower,1000,new MarketTerms{loanWagePercent=60,loanEndDay=app.Career.life.day+90,optionFee=120000});TwoMarketDays(app);
                        outgoingAgreement=app.Career.outgoingLoans.Last(o=>o.player==incomingAgreement.player&&o.owner==app.Career.club);if(outgoingAgreement.status!="accepted")throw new Exception("Actual loan negotiation failed: "+outgoingAgreement.status);
                        agreementMessage=app.Career.life.messages.Last(m=>m.player==outgoingAgreement.player&&m.subject=="Accord de prêt").id;OpenMail(agreementMessage);break;
                    case 36:Click("inbox-sign-outgoing");Resize(1280,900);break;
                    case 37:
                        Capture("outgoing-agreement-wide");AgreementBounds("outgoing-loan-agreement-review","agreement-sign-outgoing");if(!Root.Query<Label>().ToList().Any(l=>l.text=="60 %"))throw new Exception("Loan review does not show the negotiated wage share");Click("agreement-sign-outgoing");break;
                    case 38:
                        SignatureOutcome("outgoing-signed",outgoingAgreement.player,outgoingAgreement.status);
                        if(outgoingAgreement.status!="signed"||app.Database.Find(outgoingAgreement.player).team!=outgoingAgreement.borrower||app.Career.MessageNeedsDecision(app.Career.life.messages.First(m=>m.id==agreementMessage)))throw new Exception("Inbox loan signature failed");
                        var member=app.Career.Staff("scout");app.Career.ProposeStaffContract(app.Database,member.id,(long)(Career.MonthlySalary(member.wage)*1.2f)+1,2);TwoMarketDays(app);
                        staffAgreement=app.Career.staffOffers.Last(o=>o.staff==member.id&&o.club==app.Career.club);if(staffAgreement.status!="accepted")throw new Exception("Actual staff negotiation failed: "+staffAgreement.status);
                        agreementMessage=app.Career.life.messages.Last(m=>m.action=="staff"&&m.subject=="Accord de principe").id;OpenMail(agreementMessage);break;
                    case 39:Click("inbox-sign-staff");Resize(1080,2520);break;
                    case 40:
                        Capture("staff-agreement-portrait");AgreementBounds("staff-agreement-review","agreement-sign-staff");Click("agreement-sign-staff");break;
                    case 41:
                        SignatureOutcome("staff-signed",incomingAgreement.player,staffAgreement.status);
                        if(staffAgreement.status!="signed"||app.Career.MessageNeedsDecision(app.Career.life.messages.First(m=>m.id==agreementMessage)))throw new Exception("Inbox staff signature failed");
                        incomingAgreement.status="accepted";incomingAgreement.due=app.Career.life.day-8;OpenMail(app.Career.life.messages.First(m=>m.subject=="Accord de principe"&&m.player==incomingAgreement.player).id);break;
                    case 42:
                        if(Root.Q<Button>("inbox-sign-transfer")!=null)throw new Exception("Expired agreement still offers a direct signature");
                        Click("Fermer");originalNotificationLife=app.Career.life;
                        var notificationPlayer=app.Database.Squad(app.Career.club).First(p=>app.Career.Injury(p.id)==null);app.Career.OpenInjury(app.Database,notificationPlayer.id,"bruise");
                        maskedNotification=app.Career.life.messages.Last();maskedNotification.subject="Nouvelle blessure · notification du premier club";Click("Bureau");break;
                    case 43:
                        var initialBanner=Root.Q("manager-notification");
                        if(initialBanner==null||!initialBanner.Query<Label>().ToList().Any(l=>l.text.Contains(maskedNotification.subject)))throw new Exception("Test decision is missing from the notification banner");
                        if(initialBanner.Q<Label>("manager-notification-timing")?.text!=app.Career.MessageDecisionTiming(maskedNotification)||initialBanner.ClassListContains("manager-notification-urgent"))throw new Exception("Future medical notification has wrong timing or premature today urgency");
                        var dismiss=initialBanner.Query<Button>().ToList().FirstOrDefault(b=>b.text=="×");if(dismiss==null)throw new Exception("Notification dismissal control is missing");
                        using(var dismissEvent=NavigationSubmitEvent.GetPooled()){dismissEvent.target=dismiss;dismiss.SendEvent(dismissEvent);}break;
                    case 44:
                        if(Root.Q("manager-notification")?.Query<Label>().ToList().Any(l=>l.text.Contains(maskedNotification.subject))==true)throw new Exception("Dismissed notification is still displayed");
                        if(!app.Career.MessageNeedsDecision(maskedNotification))throw new Exception("Hiding a notification resolved its underlying medical decision");
                        var replacementLife=JsonUtility.FromJson<ClubLife>(JsonUtility.ToJson(originalNotificationLife));
                        reusedNotification=new ClubMessage{id=maskedNotification.id,day=replacementLife.day,sender="Staff médical",subject="Nouvelle blessure · nouveau club, même numéro",text="Un autre contexte de carrière réutilise cet identifiant.",player=maskedNotification.player,action="medical",reference=maskedNotification.reference};
                        replacementLife.messages=new System.Collections.Generic.List<ClubMessage>{reusedNotification};replacementLife.serial=reusedNotification.id;app.Career.life=replacementLife;Click("Bureau");break;
                    case 45:
                        var replacementBanner=Root.Q("manager-notification");
                        if(replacementBanner==null||!replacementBanner.Query<Label>().ToList().Any(l=>l.text.Contains(reusedNotification.subject)))throw new Exception("A dismissed ID from another ClubLife suppressed the new medical dossier");
                        if(!app.Career.MessageNeedsDecision(reusedNotification))throw new Exception("The replacement medical decision was not retained");Capture("notification-reused-id-new-life");
                        app.Career.life=originalNotificationLife;Click("Bureau");break;
                    case 46:
                        auditUnread=app.Career.Mail("Audit A","audit-read-034 · premier","Premier message à lire.",app.Career.lineup[1],"talk");
                        auditOther=app.Career.Mail("Audit B","audit-read-034 · second","Autre contact conservé.",app.Career.lineup[2],"talk");Click("Messages");Click("Non lus");Root.Q<TextField>("inbox-search").value="audit-read-034";
                        if(Root.Query<VisualElement>(className:"inbox-thread").ToList().Count!=2)throw new Exception("Unread fixture must contain two separate contacts");OpenMail(auditUnread.id);break;
                    case 47:
                        if(!auditUnread.read||Root.Query<VisualElement>(className:"inbox-thread").ToList().Count!=1||Root.Q<Label>("inbox-summary").text!=app.Career.life.messages.Count(m=>!m.read)+" non lu(s) · "+app.Career.life.messages.Count(app.Career.MessageNeedsDecision)+" message(s) liés à une décision")throw new Exception("Reading left unread cards or inbox summary stale");
                        Click("Fermer");if(Root.Q<TextField>("inbox-search").value!="audit-read-034")throw new Exception("Refreshing read state discarded inbox search");auditUnread.pinned=true;Click("Épinglés");OpenMail(auditUnread.id);auditSaveRequests=app.SaveRequestCount;Click("inbox-pin");
                        if(auditUnread.pinned||Root.Query<VisualElement>(className:"inbox-thread").ToList().Count!=0||app.SaveRequestCount!=auditSaveRequests+1)throw new Exception("Unpinning left an obsolete filtered card or caused duplicate full saves");Click("Fermer");break;
                    case 48:
                        auditSaveRequests=app.SaveRequestCount;Click("inbox-mark-visible-read");if(app.SaveRequestCount!=auditSaveRequests)throw new Exception("An empty read selection triggered a full career save");
                        auditInjured=app.Career.lineup.First(id=>app.Career.Injury(id)==null);app.Career.OpenInjury(app.Database,auditInjured,"bruise");auditUrgent=app.Career.life.messages.Last(m=>m.player==auditInjured&&m.action=="medical");app.Career.Mail("Adjoint","Note de routine plus récente","Une note récente ne doit pas cacher la blessure à traiter.",auditInjured,"talk");Click("Tous");Root.Q<TextField>("inbox-search").value=app.Database.Find(auditInjured).name;
                        var urgentCard=Root.Query<VisualElement>(className:"inbox-thread").ToList().Single();if(urgentCard.Q<Label>(className:"inbox-subject")?.text!=auditUrgent.subject)throw new Exception("Actionable thread preview describes a different routine message");OpenMail(auditUrgent.id);Click("Fiche du joueur");break;
                    case 49:
                        if(Root.Q("player-profile")==null||Root.Query<Button>().ToList().All(b=>b.text!="Retour au message"))throw new Exception("Player profile discarded its message context");Click("Retour au message");
                        if(Root.Q("inbox-message-"+auditUrgent.id)?.ClassListContains("conversation-selected")!=true)throw new Exception("Profile return did not restore the exact selected dossier");Click("Fermer");Click("Bureau");auditOther.pinned=true;auditOther.read=false;
                        var ignored=(System.Collections.Generic.HashSet<int>)typeof(TouchlineApp).GetField("dismissedNotifications",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(app);auditDismissed=ignored.ToArray();foreach(var mail in app.Career.life.messages.Where(m=>m.id!=auditOther.id))ignored.Add(mail.id);Click("Bureau");
                        if(Root.Q("manager-notification")?.Query<Label>().ToList().Any(l=>l.text.Contains(auditOther.subject))!=true)throw new Exception("Read-notification fixture did not show the important mail");OpenMail(auditOther.id);break;
                    case 50:
                        // Reading a routine important mail from the dashboard must not keep its old banner after closing.
                        Click("Fermer");var banner=Root.Q("manager-notification");if(banner?.Query<Label>().ToList().Any(l=>l.text.Contains(auditOther.subject))==true)throw new Exception("Closed read message left obsolete notification");var restoreIgnored=(System.Collections.Generic.HashSet<int>)typeof(TouchlineApp).GetField("dismissedNotifications",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(app);restoreIgnored.Clear();restoreIgnored.UnionWith(auditDismissed);OpenMail(auditUrgent.id);Click("Dossier médical / décider");Click("Soins conservateurs");break;
                    case 51:
                        if(app.Career.MessageNeedsDecision(auditUrgent))throw new Exception("Medical choice left the original dossier actionable");if(Root.Q("manager-notification")?.Query<Label>().ToList().Any(l=>l.text.Contains(auditUrgent.subject))==true)throw new Exception("Resolved medical dossier left an urgent notification");
                        app.Career.world.managerStatus="dismissed";Click("Messages");Click("À traiter");Root.Q<TextField>("inbox-search").value="";
                        if(app.Career.life.messages.Any(m=>new[]{"medical","facilities","integrity"}.Contains(m.action)&&app.Career.MessageNeedsDecision(m)))throw new Exception("Former club asks dismissed manager to resolve its medical/governance dossiers");break;
                    case 52:
                        app.Career.world.managerStatus="employed";Click("Bureau");Capture("inbox-read-pin-context-followup");Debug.Log("TOUCHLINE_INBOX_STATE_AND_CONTEXT_OK");break;
                    case 53:
                        var deadlinePlayers=app.Database.players.Where(p=>p.team=="free").Take(2).ToArray();if(deadlinePlayers.Length<2)throw new Exception("Deadline fixture needs two free players");
                        deadlineSoon=new TransferOffer{player=deadlinePlayers[0].id,seller="free",destination=app.Career.club,status="accepted",due=app.Career.life.day-7,wage=deadlinePlayers[0].wage,years=2};deadlineLater=new TransferOffer{player=deadlinePlayers[1].id,seller="free",destination=app.Career.club,status="accepted",due=app.Career.life.day,wage=deadlinePlayers[1].wage,years=2};app.Career.world.offers.Add(deadlineSoon);app.Career.world.offers.Add(deadlineLater);
                        string TransferReference(TransferOffer o)=>"transfer/"+o.player+"/"+o.destination+"/"+o.seller+"/"+o.due+"/"+o.attempts+"/"+o.fee+"/"+o.wage;
                        deadlineMail=app.Career.Mail("Agent","Accord de principe","audit-deadline-034 · expires today",deadlinePlayers[0].id,"transfer",TransferReference(deadlineSoon));app.Career.Mail("Agent","Accord de principe","audit-deadline-034 · one week left",deadlinePlayers[1].id,"transfer",TransferReference(deadlineLater));Click("Bureau");break;
                    case 54:
                        var dueBanner=Root.Q("manager-notification");if(dueBanner==null||!dueBanner.ClassListContains("manager-notification-urgent")||dueBanner.Q<Label>("manager-notification-timing")?.text!=app.Career.MessageDecisionTiming(deadlineMail)||!dueBanner.Query<Label>().ToList().Any(l=>l.text=="ÉCHÉANCE AUJOURD’HUI"))throw new Exception("Today agreement notification lacks truthful timing and accessible urgency label");
                        Capture("notification-agreement-due-today");Click("manager-notification-open");if(Root.Q("manager-notification")!=null||!app.Career.MessageNeedsDecision(deadlineMail))throw new Exception("Opening notification bypassed modal guard or implicitly resolved agreement");Click("Fermer");Click("Messages");Click("À traiter");Root.Q<TextField>("inbox-search").value="audit-deadline-034";
                        var firstDeadline=Root.Query<VisualElement>(className:"inbox-thread").ToList().First();if(!firstDeadline.Query<Label>().ToList().Any(l=>l.text.Contains("aujourd’hui")))throw new Exception("Inbox did not sort/display the agreement expiring today first");OpenMail(deadlineMail.id);Click("Vérifier et signer l’accord");break;
                    case 55:
                        AgreementBounds("transfer-agreement-review","agreement-sign-transfer");if(Root.Q("manager-notification")!=null)throw new Exception("Urgent notification overlays an active agreement review");if(!Root.Q("transfer-agreement-review").Query<Label>().ToList().Any(l=>l.text==Core.Career.Epoch.AddDays(app.Career.life.day).ToString("dd MMM yyyy",System.Globalization.CultureInfo.GetCultureInfo("fr-FR"))))throw new Exception("Review deadline differs from current agreement date");Click("Fermer");app.Career.world.offers.Remove(deadlineSoon);app.Career.world.offers.Remove(deadlineLater);
                        reviewedSponsor=app.Career.world.sponsors.First(d=>d.status!="signed"&&d.status!="expired"&&d.counterDay<=app.Career.life.day&&!app.Career.world.sponsors.Any(other=>other.slot==d.slot&&other.status=="signed"&&other.until>app.Career.life.day));app.Career.NegotiateSponsor(app.Career.world.sponsors.IndexOf(reviewedSponsor),Math.Max(1,reviewedSponsor.annual/2),2);sponsorMail=app.Career.life.messages.Last();Click("Bureau");
                        if(Root.Q("manager-decision-commercial")==null)throw new Exception("Accepted sponsorship is missing from dashboard decisions");OpenMail(sponsorMail.id);Click("inbox-review-commercial");break;
                    case 56:
                        AgreementBounds("commercial-agreement-review","agreement-sign-commercial");if(reviewedSponsor.status!="accepted"||!app.Career.MessageNeedsDecision(sponsorMail))throw new Exception("Viewing the commercial quote implicitly resolved it");Click("agreement-sign-commercial");if(reviewedSponsor.status!="signed"||app.Career.MessageNeedsDecision(sponsorMail))throw new Exception("Signing did not resolve the exact commercial quote");
                        var targetClub=app.Database.clubs.First(c=>c.playable&&c.id!=app.Career.club);reviewedApproach=new JobApproach{club=targetClub.id,offered=app.Career.life.day,until=app.Career.life.day+5,weeklySalary=1000,style="possession",reason="Une offre de poste utilisée par le scénario de validation."};app.Career.approaches.Add(reviewedApproach);approachMail=app.Career.Mail("Agent • carrière","Approche de "+targetClub.name,reviewedApproach.reason,null,"jobs","manager-approach/"+reviewedApproach.club+"/"+reviewedApproach.offered+"/"+reviewedApproach.until);Click("Bureau");if(Root.Q("manager-decision-approaches")==null)throw new Exception("Open job approach is missing from dashboard decisions");
                        app.Career.world.managerStatus="dismissed";Click("Messages");OpenMail(approachMail.id);Click("inbox-review-approach");break;
                    case 57:
                        AgreementBounds("job-approach-review","agreement-decline-job");if(!Root.Q<Button>("agreement-accept-job").enabledInHierarchy)throw new Exception("Dismissed manager cannot answer an actual career offer");if(reviewedApproach.status!="open")throw new Exception("Reading implicitly answered the job approach");Click("agreement-decline-job");if(reviewedApproach.status!="declined"||app.Career.MessageNeedsDecision(approachMail))throw new Exception("Declining did not resolve the exact approach");app.Career.world.managerStatus="employed";
                        // Dashboard-only agreements use current owner/date guards; expired/historical cards must not remain.
                        var loanFixture=new OutgoingLoanOffer{player=app.Career.lineup[9],owner=app.Career.club,borrower=reviewedApproach.club,status="accepted",due=app.Career.life.day,terms=new MarketTerms{loanEndDay=app.Career.life.day+90,loanWagePercent=50}};var freeStaff=app.Career.staffMarket.First(s=>s.club==null);var staffFixture=new StaffOffer{staff=freeStaff.id,employer=null,club=app.Career.club,wage=freeStaff.wage,years=2,status="accepted",due=app.Career.life.day};var integrityFixture=new IntegrityCase{kind="pressure",opened=app.Career.life.day-3,status="pending",alerted=true,due=app.Career.life.day+4};app.Career.outgoingLoans.Add(loanFixture);app.Career.staffOffers.Add(staffFixture);app.Career.life.investigations.Add(integrityFixture);Click("Bureau");
                        foreach(var key in new[]{"outgoing","staff","integrity"})if(Root.Q("manager-decision-"+key)==null)throw new Exception("Dashboard omitted current decision "+key);
                        app.Career.outgoingLoans.Remove(loanFixture);app.Career.staffOffers.Remove(staffFixture);app.Career.life.investigations.Remove(integrityFixture);Click("Bureau");break;
                    case 58:
                        if(Root.Q("manager-decision-outgoing")!=null||Root.Q("manager-decision-staff")!=null)throw new Exception("Dashboard retained removed loan/staff agreement");
                        promisePlayer=app.Career.lineup[7];var promise=app.Career.Person(promisePlayer);savedPromiseUntil=promise.promiseUntil;savedPromiseStarts=promise.promiseStarts;savedPromiseAppearances=promise.appearances;promise.promiseUntil=app.Career.life.day+7;promise.promiseStarts=promise.appearances;app.PlayerProfile(promisePlayer);Click("Relations");
                        if(!Root.Q("profile-body").Query<Label>().ToList().Any(l=>l.text==app.Career.PlayerPromiseStatus(promisePlayer)))throw new Exception("Profile does not show the promise status and precise deadline");break;
                    case 59:
                        app.Career.Person(promisePlayer).appearances+=2;app.PlayerProfile(promisePlayer);Click("Relations");if(!Root.Q("profile-body").Query<Label>(className:"positive").ToList().Any(l=>l.text==app.Career.PlayerPromiseStatus(promisePlayer)))throw new Exception("Fulfilled promise still appears as an outstanding warning");Click("SMS / Appeler");if(!Root.Q("player-conversation").Query<Label>(className:"positive").ToList().Any(l=>l.text.Contains("Engagement tenu")))throw new Exception("Conversation disagrees with the fulfilled promise status");break;
                    case 60:
                        var restoredPromise=app.Career.Person(promisePlayer);restoredPromise.promiseUntil=savedPromiseUntil;restoredPromise.promiseStarts=savedPromiseStarts;restoredPromise.appearances=savedPromiseAppearances;Click("Fermer");Click("Bureau");Capture("decision-deadlines-commercial-career-promises");Debug.Log("TOUCHLINE_DECISION_DEADLINES_COMMERCIAL_CAREER_PROMISES_OK");break;
                    case 61:
                        if(app.Career.Payroll(app.Database)<=app.Career.WageBudget&&Root.Q("manager-decision-payroll")!=null)throw new Exception("Salary warning remains despite available headroom in the current club");
                        if(!ReferenceEquals(app.Career.life,originalNotificationLife)||!app.Career.MessageNeedsDecision(maskedNotification))throw new Exception("The original club context was not restored after the notification test");
                        PlayerDiscussionSmokeChecks.PrepareAndOpen(app);Resize(1600,700);break;
                    case 62:
                        PlayerDiscussionSmokeChecks.AssertBounds(app);Capture("discussion-compact-landscape");PlayerDiscussionSmokeChecks.AssertChooserAndRememberHistory(app);Resize(1080,2520);break;
                    case 63:
                        PlayerDiscussionSmokeChecks.AssertHistoryRetainedAfterRotation(app);Capture("discussion-history-portrait");PlayerDiscussionSmokeChecks.ReturnToPreparedTopic(app);break;
                    case 64:
                        Capture("discussion-prepared-topic-portrait");PlayerDiscussionSmokeChecks.AssertPortraitAndBeginCooldown(app);break;
                    case 65:
                        PlayerDiscussionSmokeChecks.AssertCooldownAndHistory(app);Capture("discussion-cooldown-portrait");Resize(1600,700);break;
                    case 66:
                        PlayerDiscussionSmokeChecks.AssertCooldownAndHistory(app);Capture("discussion-cooldown-landscape");
                        File.WriteAllText(Path.Combine(output,"report.json"),"{\"passed\":true,\"portraitLandscapeNavigation\":true,\"actionableMedicalProjectsOffers\":true,\"resolvedDecisionRemoved\":true,\"delegationRespected\":true,\"vacantPreparerManualLoad\":true,\"readCountersAndScroll\":true,\"importantPersistentNotification\":true,\"inboxThreadsSearchReadPin\":true,\"compactFourFilters\":true,\"independentInboxCategory\":true,\"contextualPlayerDiscussion\":true,\"compactContextualChooserElevenTopics\":true,\"discussionHistoryFilterAndOlderPageSurviveRotation\":true,\"preparedTopicAndChannelSurviveRotation\":true,\"sharedCooldownExactDateAllSubjects\":true,\"threadReadNoopDoesNotSave\":true,\"syntheticDiscussionFixture\":true,\"developmentPlanTrainingLoanLinks\":true,\"conversationCooldownAndRest\":true,\"dismissedDashboard\":true,\"dismissedLinksRedirect\":true,\"manualTraining\":true,\"dailyContinue\":true,\"directIncomingLoanStaffSignatures\":true,\"insufficientWageBudgetPreservesAgreement\":true,\"realClubChangeAndAgentNegotiations\":true,\"agreementSignatureBoundsPortraitLandscape\":true,\"expiredSignatureRemoved\":true,\"notificationDismissalPreservesDecision\":true,\"notificationIdsResetForNewClubLife\":true,\"salaryCeilingWarningAndFinanceLink\":true,\"notificationDecisionTimingAndTodayUrgency\":true,\"notificationModalGuard\":true,\"physicalAndroid\":false}");SessionState.SetBool("ManagementSmoke",false);Debug.Log("TOUCHLINE_MANAGEMENT_OK");EditorApplication.Exit(0);break;
                }
            }catch(Exception e){SessionState.SetBool("ManagementSmoke",false);Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}


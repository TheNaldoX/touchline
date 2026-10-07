using System;
using System.Collections.Generic;
using System.Linq;

namespace Touchline.Core
{
    [Serializable] public class ClubMessage { public int id,day;public string sender,subject,text,player,action,reference;public bool read,pinned; }
    [Serializable] public class PlayerLife { public string id,discussionFocus;public float fitness=100,morale=75,trust=60,growth;public int lastTalk=-30,appearances,promiseUntil=-1,promiseStarts,banUntil,boostUntil=-1,rehabUntil,restUntil=-1; public PlayerLife Copy()=>(PlayerLife)MemberwiseClone(); }
    [Serializable] public class MedicalCase { public int id,opened,remaining,total,closed=-1,responsibilityEndedDay;public string player,diagnosis,treatment="pending",responsibilityEndedReason;public bool surgery,injection,consent,relapsed;public int reliefUntil=-1; }
    [Serializable] public class Facility { public string kind;public int level=1; }
    [Serializable] public class FacilityProject { public string kind,status,funding;public int level,requested,due,started;public long cost,contribution; }
    [Serializable] public class AccountEntry { public int day;public string label;public long amount; }
    [Serializable] public class IntegrityCase { public string kind,player,status="pending";public int opened,due;public long cost,benefit;public bool accepted,alerted,repaid;public string response; }
    [Serializable] public class ClubLife
    {
        public DelegationPlan staff; public int staffTrainingUntil;public string staffTrainingRole,staffTrainingStaffId;
        public int version=1,day,serial,nextFixture=6,matches,medicalCount;public uint seed=825671;
        public string training="balanced",lastResult="La préparation commence",financeSource,financeLeague;
        public long cash,revenue,financeBaselineRevenue,financeBaselineGate;public float boardTrust=70,reputation=70,suspicion;
        public bool recordedMatch,corruptionEnabled;public int managerBanUntil,schemeCooldown=-30;public int leadershipUntil;
        public List<PlayerLife> players=new List<PlayerLife>();public List<PlayerLife> retiredPlayers=new List<PlayerLife>();public List<LoanPlayerHistory> loanedPlayers=new List<LoanPlayerHistory>();public List<ClubMessage> messages=new List<ClubMessage>();
        public List<MedicalCase> medical=new List<MedicalCase>();public List<Facility> facilities=new List<Facility>();
        public List<FacilityProject> projects=new List<FacilityProject>();public List<AccountEntry> ledger=new List<AccountEntry>();public List<IntegrityCase> investigations=new List<IntegrityCase>();
    }
    public partial class Career
    {
        public ClubLife life;
        public DateTime Date=>Touchline.Core.Career.Epoch.AddDays(life?.day??0);
        public void EnsureLife(Database db)
        {
            if(world!=null&&(world.divisions==null||world.divisions.Count==0))world=null;
            BindMatchTactic();if(life==null||life.version<1||life.players==null){var data=db.clubs?.FirstOrDefault(c=>c.id==club);long revenue=data?.annualRevenue??0;if(revenue<=0)revenue=Math.Max(2000000,db.Squad(club).Sum(p=>p.wage)*52*3);
                life=new ClubLife{revenue=revenue,cash=revenue*12/100,financeLeague=data?.league,financeSource=data?.financeSource??"Estimation du jeu à partir de la masse salariale"};
                int level=revenue>300000000?3:revenue>40000000?2:1;foreach(var type in new[]{"training","medical","academy","stadium"})life.facilities.Add(new Facility{kind=type,level=level});
                Mail("Adjoint","Bienvenue au bureau","Continuer avance exactement d’un jour. Préparez votre groupe, consultez vos messages et surveillez les indisponibilités. Le calendrier distingue la préparation et les compétitions officielles.");
            }
            // Older/partial saves can carry a valid life version but lack the
            // facility list. Restore only missing entries, preserving upgrades.
            life.facilities??=new List<Facility>();
            int initialLevel=life.revenue>300000000?3:life.revenue>40000000?2:1;
            foreach(var kind in new[]{"training","medical","academy","stadium"})
                if(!life.facilities.Any(f=>f.kind==kind))life.facilities.Add(new Facility{kind=kind,level=initialLevel});
            SynchronizePlayerAges(db);
            life.players.RemoveAll(p=>db.Find(p.id)?.team!=club);
            foreach(var p in db.Squad(club))if(!life.players.Any(x=>x.id==p.id))life.players.Add(new PlayerLife{id=p.id,fitness=p.fitness,morale=p.morale});
            ApplyLife(db);
        }
        float Roll(){life.seed=unchecked(life.seed*1664525+1013904223);return (life.seed>>8)/16777216f;}
        public PlayerLife Person(string id)=>life.players.First(p=>p.id==id);
        public int Level(string kind)=>life.facilities.First(f=>f.kind==kind).level;
        public ClubMessage Mail(string sender,string subject,string text,string player=null,string action=null,string reference=null)
        {
            var msg=new ClubMessage{id=++life.serial,day=life.day,sender=sender,subject=subject,text=text,player=player,action=action,reference=reference,read=sender=="Vous"};life.messages.Add(msg);
            while(life.messages.Count>300){
                // Archive routine read mail first. Unread, pinned and actionable
                // dossiers survive ordinary inbox churn without unbounded saves.
                int oldest=0,lowest=4;for(int i=0;i<life.messages.Count;i++){var m=life.messages[i];int rank=MessageNeedsDecision(m)?3:m.pinned?2:!m.read?1:0;if(rank>=lowest)continue;oldest=i;lowest=rank;if(rank==0)break;}life.messages.RemoveAt(oldest);
            }
            return msg;
        }
        public MedicalCase Injury(string id)=>life.medical.LastOrDefault(c=>c.player==id&&c.closed<0&&string.IsNullOrEmpty(c.responsibilityEndedReason));
        public bool Available(string id){var p=Person(id);var injury=Injury(id);return p.restUntil<=life.day&&p.banUntil<=life.day&&(injury==null||injury.reliefUntil>=life.day);}
        public void ApplyLife(Database db){if(life==null)return;CloseMissingMedicalResponsibilities(db);foreach(var p in life.players){var data=db.Find(p.id);if(data==null)continue;data.fitness=Mathx.Clamp(p.fitness+(p.boostUntil>=life.day?5:0),15,100);data.morale=p.morale;data.development=(int)p.growth;data.performanceModifier=p.boostUntil>=life.day?.02f:0;data.unavailableDays=Available(p.id)?0:Math.Max(1,Math.Max(p.restUntil-life.day,Math.Max(p.banUntil-life.day,Injury(p.id)?.remaining??0)));}}
        void Charge(long cost,string label){if(cost<0||cost>life.cash)throw new InvalidOperationException("La trésorerie ne couvre pas cette dépense.");Account(-cost,label);}
        void Account(long amount,string label){if(amount>0&&(label.StartsWith("Rachat")||label.StartsWith("Avance de trésorerie"))){var account=world?.aiAccounts?.FirstOrDefault(a=>a.club==club);if(account!=null)account.externalFunding+=amount;}life.cash+=amount;life.ledger.Add(new AccountEntry{day=life.day,label=label,amount=amount});if(life.ledger.Count>180)life.ledger.RemoveAt(0);}
        void RequireEmployment(){if(world!=null&&world.managerStatus!="employed")throw new InvalidOperationException("Vous n’êtes plus en poste. Les décisions de ce club appartiennent à sa nouvelle direction.");}
        void OffPitch(bool allowSuspended=false,bool requireEmployment=true){if(requireEmployment)RequireEmployment();if(match!=null&&!match.finished)throw new InvalidOperationException("Terminez le match avant cette décision.");if(!allowSuspended&&life.managerBanUntil>life.day)throw new InvalidOperationException("Vos décisions sont suspendues. L’adjoint gère les rencontres jusqu’à votre retour.");}
        public void AdvanceDay(Database db)
        {
            EnsureLife(db);OffPitch(allowSuspended:true,requireEmployment:false);if(match?.finished==true&&!life.recordedMatch)RecordMatch(db);
            if(life.day>=life.nextFixture)throw new InvalidOperationException("Une rencontre est prévue aujourd’hui. Jouez-la ou déléguez-la avant de continuer.");
            life.day++;SynchronizePlayerAges(db);ProcessLoanParentWageChanges(db);StaffDay(db);
            foreach(var p in life.players){var injury=Injury(p.id);
                if(injury!=null){if(p.promiseUntil>=0)p.promiseUntil++;if(injury.treatment=="pending"&&life.day-injury.opened>=2){injury.treatment="conservative";Mail("Staff médical","Soins conservateurs engagés",db.Find(p.id).name+" : nous engageons les soins conservateurs en attendant une éventuelle autre décision.",p.id,"medical");}
                    if(injury.treatment!="pending")injury.remaining=Math.Max(0,injury.remaining-1);
                    p.fitness=Mathx.Clamp(p.fitness+.6f,30,90);
                    if(injury.remaining==0){injury.closed=life.day;p.rehabUntil=life.day+5;p.fitness=Math.Min(p.fitness,80);Mail("Staff médical","Reprise progressive",db.Find(p.id).name+" est disponible. Charge réduite pendant cinq jours pour retrouver ses repères.",p.id,"medical");}
                }else{
                    bool protectedDay=world!=null&&(NextFixture()?.day-life.day<=1||world.fixtures.Any(f=>f.played&&(f.home==club||f.away==club)&&life.day-f.day==1));
                    bool agreedRest=p.restUntil>life.day;if(agreedRest&&p.promiseUntil>=0)p.promiseUntil++;
                    float recovery=life.training=="rest"||protectedDay||agreedRest?5:life.training=="heavy"?.7f:2.6f;
                    p.fitness=Mathx.Clamp(p.fitness+recovery+Level("medical")*.12f,20,100);
                    float risk=(life.training=="rest"||protectedDay||agreedRest?0:life.training=="heavy"?.0023f:.0008f)*(1+(100-p.fitness)/30)*(1-Level("medical")*.07f)*(p.rehabUntil>life.day?2:1);
                    if(Roll()<risk)OpenInjury(db,p.id,Roll()<.08f?"ligament":Roll()<.5f?"muscle":"bruise");
                    var data=db.Find(p.id);if(!agreedRest&&(world==null||world.managerStatus=="employed")&&data.age<24&&data.rating<data.potential&&life.training!="rest"){p.growth=Math.Min(data.potential-data.rating,p.growth+(.003f+Level("training")*.0015f+Level("academy")*.001f)*(p.morale/100)*(.75f+Staff("fitness").coaching/40f));}
                }
                bool activeManager=world==null||world.managerStatus=="employed";
                p.morale=activeManager?Mathx.Clamp(p.morale+(life.training=="heavy"&&p.fitness<75?-.4f:0),10,100):p.morale+(70-p.morale)*.01f;
                if((world==null||world.managerStatus=="employed")&&p.promiseUntil>=0&&life.day>=p.promiseUntil){bool kept=p.appearances-p.promiseStarts>=2;p.trust=Mathx.Clamp(p.trust+(kept?5:-10),0,100);p.morale=Mathx.Clamp(p.morale+(kept?3:-7),10,100);p.promiseUntil=-1;Mail(db.Find(p.id).name,"Notre engagement",kept?"Merci de m’avoir donné ma chance. Je sais que je peux compter sur vous.":"Vous m’aviez promis du temps de jeu. J’ai besoin d’explications.",p.id,"talk");}
            }
            foreach(var project in life.projects.Where(p=>p.status=="requested"&&p.due<=life.day).ToArray()){
                if(life.boardTrust>=55&&life.cash>life.revenue*.025&&project.cost<life.revenue*.1){project.status="approved";project.contribution=project.cost/2;Mail("Présidence","Projet soutenu","Le conseil finance la moitié du projet "+FacilityName(project.kind)+". Votre part reste à engager dans Infrastructures.",null,"facilities",FacilityMessageReference(project));}
                else{project.status="refused";Mail("Présidence","Projet différé","Les moyens ou la confiance actuelle ne permettent pas de financer ce projet. Nouvelle demande possible dans 30 jours.",null,"facilities");}}
            foreach(var project in life.projects.Where(p=>p.status=="building"&&p.due<=life.day)){project.status="complete";life.facilities.First(f=>f.kind==project.kind).level=project.level;Mail("Direction des opérations","Travaux livrés",FacilityName(project.kind)+" passe au niveau "+project.level+". Les nouvelles installations sont en service.",null,"facilities");}
            if(life.day%7==0){long wages=Payroll(db),income=RecurringAnnualIncome(db)/52,costs=AnnualOperatingCosts(db.clubs.First(t=>t.id==club),life.facilities.Sum(f=>f.level))/52;
                var personnel=AnnualEmploymentCosts(db);Account(-personnel.playerContributions/52,"Cotisations employeur · joueurs (projection)");Account(-personnel.staffContributions/52,"Cotisations employeur · staff (projection)");Account(-personnel.otherPersonnel/52,"Autres personnels du club · projection");
                Account(income,"Recettes hebdomadaires estimées");Account(-wages,"Salaires de l’effectif");Account(-costs,"Exploitation et infrastructures");if(life.cash<0){life.boardTrust=Math.Max(0,life.boardTrust-5);Mail("Direction financière","Trésorerie sous tension","Le club est à découvert. Les nouveaux investissements sont suspendus.");}}
            IntegrityStoryDay();ResolveIntegrity(db);if(!life.investigations.Any(i=>i.status=="pending"))life.suspicion=Math.Max(0,life.suspicion-.1f);
            if(life.day%7==0&&life.players.Count>0){float condition=(float)life.players.Average(p=>p.fitness);Mail("Préparateur physique","Bilan de charge","Condition moyenne : "+condition.ToString("0")+" %. "+(condition<82?"Le groupe accumule de la fatigue. Prévoyez de la récupération et faites tourner.":"Le groupe supporte la charge actuelle. Conservez une journée légère avant la prochaine rencontre."));}
            if((world==null||world.managerStatus=="employed")&&life.day%14==0){var p=life.players.Where(p=>Available(p.id)&&(world?.contracts?.FirstOrDefault(c=>c.player==p.id)?.playingTime!=null?PlayingTimeConcern(p.id):PlayingTimeRoles.Normalize(world?.contracts?.FirstOrDefault(c=>c.player==p.id)?.role)!="youth"&&RoleMatchOpportunities(p.id)>0&&RoleAppearances(p.id)<Math.Max(1,RoleMatchOpportunities(p.id)/3))).OrderBy(p=>p.morale).FirstOrDefault();if(p!=null){p.morale=Math.Max(15,p.morale-2);Mail(db.Find(p.id).name,"Un moment pour parler ?","J’aimerais comprendre ma place dans le groupe. Pouvez-vous m’appeler ?",p.id,"talk");}}
            PlayerConversationDay(db);WorldDay(db);ApplyLife(db);
        }
        public MedicalCase OpenInjury(Database db,string id,string type)
        {
            if(Injury(id)!=null)throw new InvalidOperationException("Ce joueur est déjà suivi pour une blessure.");var p=Person(id);bool serious=type=="ligament";if(type!="ligament"&&type!="muscle"&&type!="bruise")throw new ArgumentException("Diagnostic inconnu.");
            int days=serious?65+(int)(Roll()*36):type=="muscle"?14+(int)(Roll()*14):3+(int)(Roll()*5);
            var c=new MedicalCase{id=++life.medicalCount,player=id,opened=life.day,remaining=days,total=days,diagnosis=serious?"Lésion ligamentaire du genou":type=="muscle"?"Lésion musculaire de la cuisse":"Contusion douloureuse",surgery=serious,injection=type=="bruise",consent=p.trust>=45};life.medical.Add(c);p.fitness=Math.Min(p.fitness,serious?48:70);p.morale=Math.Max(10,p.morale-3);
            Mail("Staff médical",db.Find(id).name+" • blessure",c.diagnosis+". Indisponibilité estimée : "+days+" jours, sous réserve d’évolution. "+(serious?"Un avis chirurgical peut être demandé.":c.injection?"Une prise en charge antalgique peut être évaluée, sans accélérer la guérison.":"Repos sportif et rééducation recommandés."),id,"medical","medical/"+c.id);return c;
        }
        public void Treat(Database db,int caseId,string treatment)
        {
            OffPitch();var c=life.medical.First(x=>x.id==caseId);if(!string.IsNullOrEmpty(c.responsibilityEndedReason)||!PlayerMedicalResponsibility(c.player)||db.Find(c.player)?.team!=club)throw new InvalidOperationException("Ce dossier est conservé pour l’historique ; le suivi ne relève plus de votre club.");if(c.closed>=0)throw new InvalidOperationException("Le suivi est terminé.");var p=Person(c.player);
            if(treatment=="surgery"){
                if(!c.surgery||c.treatment=="surgery"||life.day-c.opened>10)throw new InvalidOperationException("Cette indication chirurgicale n’est plus disponible.");
                if(!c.consent)throw new InvalidOperationException("Le joueur refuse l’intervention proposée. Les soins conservateurs restent disponibles.");
                Charge(Math.Max(2500,life.revenue/15000),"Intervention et rééducation");c.remaining=55+(int)(Roll()*26);c.treatment="surgery";c.injection=false;
            }else if(treatment=="injection"){
                if(!c.injection||c.reliefUntil>=0||c.remaining>7||!c.consent)throw new InvalidOperationException("Le staff ou le joueur ne valide pas cette option.");
                Charge(Math.Max(200,life.revenue/200000),"Avis antalgique et contrôle réglementaire");c.reliefUntil=life.day+1;c.treatment="injection";p.trust=Math.Max(0,p.trust-2);
            }else if(treatment=="conservative"){
                if(c.treatment!="pending")throw new InvalidOperationException("Un protocole est déjà engagé.");c.treatment="conservative";p.trust=Math.Min(100,p.trust+1);
            }else throw new ArgumentException("Protocole inconnu.");
            Mail("Staff médical","Décision prise",db.Find(c.player).name+" : "+(treatment=="surgery"?"intervention retenue avec son accord. Rééducation estimée à "+c.remaining+" jours.":treatment=="injection"?"prise en charge antalgique autorisée après validation médicale et réglementaire. Disponibilité temporaire, guérison inchangée et risque d’aggravation s’il joue.":"soins conservateurs et rééducation engagés."),c.player,"medical");ApplyLife(db);
        }
        public void Talk(Database db,string id,string topic,bool phone)
        {
            if(db.Find(id)?.team=="retired"||PlayerRetirementEffective(id))throw new InvalidOperationException("La retraite de ce joueur est effective ; aucun nouveau dialogue de gestion ne peut être engagé.");
            if(TalkContext(db,id,topic,phone))return;
            OffPitch();if(db.Find(id)?.team!=club||!life.players.Any(l=>l.id==id))throw new InvalidOperationException("Ce joueur n’appartient plus à votre effectif.");var p=Person(id);if(life.day-p.lastTalk<7)throw new InvalidOperationException("Laissez quelques jours à cet échange pour produire ses effets.");
            if(!new[]{"support","demand","promise","explain","apologize","leadership"}.Contains(topic))throw new ArgumentException("Sujet inconnu.");if(topic=="promise"&&(p.promiseUntil>=0||Injury(id)!=null))throw new InvalidOperationException("Impossible de promettre du temps de jeu dans la situation actuelle.");
            p.lastTalk=life.day;string response;
            if(topic=="support"){p.trust=Math.Min(100,p.trust+(p.morale<70?2:.5f));p.morale=Math.Min(100,p.morale+(p.morale<70?3:.5f));response=p.morale<73?"Merci de votre soutien. Je vais continuer à travailler pour le groupe.":"Merci. Je me sens déjà bien ; l’important sera de garder notre cohérence sur le terrain.";}
            else if(topic=="demand"){bool fair=p.fitness>80&&p.morale>60&&Injury(id)==null;p.morale=Mathx.Clamp(p.morale+(fair?2:-5),10,100);p.trust=Mathx.Clamp(p.trust+(fair?1:-4),0,100);response=fair?"Le message est reçu. Vous pouvez attendre davantage de moi.":"Je ne trouve pas cette pression justifiée dans ma situation.";}
            else if(topic=="explain"){
                bool recovery=Injury(id)!=null||p.fitness<80||p.restUntil>life.day;
                bool unmetRole=!recovery&&PlayingTimeConcern(id);bool credible=recovery||(!unmetRole&&p.trust>=55);
                p.trust=Mathx.Clamp(p.trust+(credible?2:-2),0,100);
                response=Injury(id)!=null?"Je comprends que ma reprise doit être progressive. Tenez-moi informé.":p.fitness<80?"Je sens aussi la fatigue. Je préfère revenir dans de bonnes conditions.":p.restUntil>life.day?"Nous avons convenu de cette récupération. Respectons ce repos avant de reparler des prochaines sélections.":unmetRole?"Mon utilisation ne respecte pas le rôle convenu. Une explication tactique ne remplace pas les sélections promises ; j’attends des actes.":credible?"J’entends votre explication tactique. Je veux savoir comment regagner ma place.":"Les explications ne suffisent plus. J’attends une chance sur le terrain.";
            }
            else if(topic=="apologize"){bool needed=p.trust<55;p.trust=Math.Min(100,p.trust+(needed?3:0));response=needed?"J’apprécie que vous reconnaissiez la difficulté. Il faudra maintenant reconstruire la confiance.":"Nous n’avons pas de problème particulier. Continuons à travailler.";}
            else if(topic=="leadership"){
                bool ready=db.Find(id).age>=26&&p.trust>=60;
                if(!ready){p.morale=Math.Max(10,p.morale-2);response="Je préfère d’abord stabiliser ma propre situation avant de porter celle du vestiaire.";}
                else if(life.day<life.leadershipUntil)response="Le message a déjà été relayé au groupe cette semaine. Laissons-lui du temps et montrons la suite sur le terrain.";
                else{life.leadershipUntil=life.day+7;p.morale=Math.Min(100,p.morale+2);foreach(var teammate in life.players.Where(x=>x.id!=id))teammate.morale=Math.Min(100,teammate.morale+.3f);response="Je vais parler au groupe et montrer l’exemple à l’entraînement.";}
            }
            else{p.promiseUntil=life.day+21;p.promiseStarts=p.appearances;response="D’accord pour deux apparitions d’au moins 30 minutes dans les trois prochaines semaines. Je compte sur votre parole.";}
            Mail("Vous",phone?"Appel • compte rendu":"SMS envoyé",topic=="support"?"Je te soutiens. Parlons de ce dont tu as besoin.":topic=="demand"?"J’attends davantage de ton investissement.":topic=="explain"?"Je veux expliquer tes dernières sélections et ce que j’attends de toi.":topic=="apologize"?"Je reconnais que notre relation a besoin d’attention.":topic=="leadership"?"Peux-tu aider les autres et porter notre message dans le vestiaire ?":"Je te donnerai du temps de jeu dans les prochaines semaines.",id);
            Mail(db.Find(id).name,phone?"Après notre appel":"Réponse",response,id,"talk");ApplyLife(db);
        }
        public static string FacilityName(string kind)=>kind=="training"?"Centre d’entraînement":kind=="medical"?"Pôle médical":kind=="academy"?"Centre de formation":"Stade";
        public long ProjectCost(string kind){int level=Level(kind);return Math.Max(50000,(long)(life.revenue*(kind=="stadium"?.018:.0075)*level*level));}
        public void RequestProject(string kind,bool board)
        {
            OffPitch();if(Level(kind)>=5||life.projects.Any(p=>p.kind==kind&&(p.status=="requested"||p.status=="approved"||p.status=="building"||p.status=="refused"&&life.day-p.requested<30)))throw new InvalidOperationException("Projet déjà engagé ou délai de nouvelle demande non écoulé.");
            if(life.projects.Count(p=>p.status=="building"||p.status=="requested"||p.status=="approved")>=2)throw new InvalidOperationException("Le club peut mener deux projets simultanément.");
            var project=new FacilityProject{kind=kind,level=Level(kind)+1,cost=ProjectCost(kind),requested=life.day,due=life.day+3,status=board?"requested":"approved",funding=board?"board":"club"};
            if(!board)Charge(project.cost,"Travaux : "+FacilityName(kind));life.projects.Add(project);if(!board){project.status="building";project.started=life.day;project.due=life.day+30*project.level+(kind=="stadium"?60:0);}else Mail("Vous","Demande au propriétaire","Financement demandé pour "+FacilityName(kind)+". Une réponse est attendue dans trois jours.",null,"facilities");
        }
        public void StartApprovedProject(string kind){OffPitch();var p=life.projects.FirstOrDefault(p=>p.kind==kind&&p.status=="approved");if(p==null)throw new InvalidOperationException("Aucun projet approuvé.");Charge(p.cost-p.contribution,"Part du club : "+FacilityName(kind));p.status="building";p.started=life.day;p.due=life.day+30*p.level+(kind=="stadium"?60:0);}
        public void CancelProject(string kind){OffPitch();var p=life.projects.FirstOrDefault(p=>p.kind==kind&&(p.status=="requested"||p.status=="approved"));if(p==null)throw new InvalidOperationException("Seul un projet non démarré peut être annulé.");p.status="cancelled";}
        public void PrepareLineup(Database db)
        {
            EnsureLife(db);EnsureMatchSquad(db);if(lineup==null||lineup.Any(id=>db.Find(id)?.team!=club||!life.players.Any(p=>p.id==id)))lineup=Select(db,club,tactic);var selected=new HashSet<string>(lineup.Where(Available));
            for(int i=0;i<11;i++)if(!Available(lineup[i])){var replacement=db.Squad(club).Where(p=>Available(p.id)&&!selected.Contains(p.id)).OrderByDescending(p=>p.rating*p.Fit(tactic.withoutBall[i].role)*p.fitness).FirstOrDefault();if(replacement==null)throw new InvalidOperationException("Effectif disponible insuffisant.");lineup[i]=replacement.id;selected.Add(replacement.id);}
            ApplyLife(db);
        }
        public void RecordMatch(Database db)
        {
            if(match==null||!match.finished||life.recordedMatch)return;life.recordedMatch=true;life.matches++;life.lastResult=db.clubs?.FirstOrDefault(c=>c.id==match.away)?.name+" • "+match.score[0]+" – "+match.score[1];life.boardTrust=Mathx.Clamp(life.boardTrust+(match.score[0]>match.score[1]?1:match.score[0]<match.score[1]?-.5f:0),0,100);life.reputation=Mathx.Clamp(life.reputation+(match.score[0]>match.score[1]?.2f:match.score[0]<match.score[1]?-.1f:0),0,100);
            int estimatedConditionCount=0;
            foreach(var p in life.players){var actor=match.actors.FirstOrDefault(a=>a.id==p.id);var incoming=match.events.FirstOrDefault(e=>e.kind=="substitution"&&e.player==p.id);var outgoing=match.events.FirstOrDefault(e=>e.kind=="substitution"&&e.side==0&&(e.receiver==p.id||e.receiver==null&&e.text.EndsWith(db.Find(p.id).name+".")));var dismissal=match.events.FirstOrDefault(e=>e.kind=="red"&&e.side==0&&e.player==p.id);float end=Math.Min(match.clock/match.SecondsPerMinute,Math.Min(outgoing?.time/match.SecondsPerMinute??float.MaxValue,dismissal?.time/match.SecondsPerMinute??float.MaxValue));float minutes=match.used.Contains(p.id)?Math.Max(0,end-(incoming?.time/match.SecondsPerMinute??0)):0;
                var youthPath=world?.youth.FirstOrDefault(y=>y.player==p.id);if(youthPath!=null&&minutes>0)youthPath.seniorMinutes+=(int)Math.Round(minutes);
                RecordPlayingTime(p.id,minutes>0&&incoming==null,minutes);if(minutes>=30)p.appearances++;if(actor!=null)p.fitness=Math.Min(p.fitness,actor.fitness);
                else if(minutes>0){
                    bool measured=outgoing!=null&&outgoing.hasOutgoingFitness&&outgoing.observedOutgoingFitness>=0&&outgoing.observedOutgoingFitness<=100;
                    if(measured)p.fitness=Math.Min(p.fitness,outgoing.observedOutgoingFitness);
                    else{p.fitness=Math.Max(25,p.fitness-minutes*.18f);estimatedConditionCount++;}
                }
                p.morale=Mathx.Clamp(p.morale+(match.score[0]>match.score[1]?2:match.score[0]<match.score[1]?-2:0),10,100);var injury=Injury(p.id);
                if(minutes>0&&injury?.reliefUntil>=life.day&&Roll()<.28f){injury.remaining+=7;injury.relapsed=true;injury.reliefUntil=-1;Mail("Staff médical","Aggravation après le match",db.Find(p.id).name+" : le retour sous antalgie a aggravé les symptômes. Sept jours supplémentaires estimés.",p.id,"medical");}
                if(minutes>0&&injury==null&&Roll()<.009f*(1+(100-p.fitness)/35))OpenInjury(db,p.id,Roll()<.1f?"ligament":"muscle");
            }
            if(world==null)Account(life.revenue*(70+Level("stadium")*3)/10000,"Recettes de la rencontre de préparation");life.nextFixture=life.day+7;Mail("Adjoint","Débrief de la rencontre",life.lastResult+". Le bilan physique est disponible dans Santé."+(estimatedConditionCount>0?" Condition estimée pour "+estimatedConditionCount+" joueur(s) sorti(s) : mesure de sortie absente ou inexploitable.":""));RecordWorldMatch(db);ApplyLife(db);
        }
        public void StartScheme(string kind,string player=null)
        {
            OffPitch();if(!life.corruptionEnabled||life.managerBanUntil>life.day||life.day-life.schemeCooldown<30||life.investigations.Any(c=>c.status=="pending"))throw new InvalidOperationException("Ce dossier n’est pas accessible actuellement.");
            if(kind!="doping"&&kind!="fixing"){StartExtendedScheme(kind);return;}if(kind=="doping"&&(player==null||!Available(player)))throw new InvalidOperationException("Choisissez un joueur disponible.");
            long cost=Math.Max(5000,life.revenue/(kind=="doping"?2000:250));Charge(cost,"Dossier à risque • "+(kind=="doping"?"dopage":"achat de match"));life.schemeCooldown=life.day;
            bool accepted=Roll()<(kind=="doping"?.65f:.22f);var c=new IntegrityCase{kind=kind,player=player,opened=life.day,due=life.day+10+(int)(Roll()*18),cost=cost,accepted=accepted};life.investigations.Add(c);life.suspicion=Math.Min(100,life.suspicion+25);
            if(kind=="doping"&&accepted){Person(player).boostUntil=life.day+7;Person(player).trust=Math.Max(0,Person(player).trust-8);}
            Mail("Coulisses • fiction","Dossier ouvert",accepted?(kind=="doping"?"Un gain physique temporaire est simulé. La santé, la confiance et une suspension sont en jeu.":"Un intermédiaire fictif prétend pouvoir influencer la prochaine rencontre. Aucun résultat n’est garanti."):"La proposition est refusée. Les fonds sont perdus et le risque d’enquête demeure.",player,"integrity");
        }
        public void ApplyMatchContext(MatchSimulation simulation)
        {
            simulation.State.professionalRules=world!=null;
            var fixture=world?.fixtures.FirstOrDefault(f=>f.id==world.activeFixture);
            simulation.State.venueSide=fixture==null||fixture.home==club?0:fixture.away==club?1:-1;
            life.recordedMatch=false;foreach(var c in life.investigations.Where(c=>c.kind=="fixing"&&c.accepted&&c.status=="pending")){foreach(var p in simulation.State.actors.Where(p=>p.side==1))p.fitness=Math.Max(30,p.fitness-5);c.accepted=false;Mail("Coulisses • fiction","Influence incertaine","Un léger désavantage adverse est simulé pour cette rencontre seulement. Le moteur décide toujours des actions et du résultat.",null,"integrity");}
        }
        public void ProcessMedicalEvents(Database db)
        {
            if(world==null||match==null)return;foreach(var e in match.events.Where(e=>e.kind=="injury"&&e.side==0))if(Injury(e.player)==null&&life.players.Any(p=>p.id==e.player))OpenInjury(db,e.player,"muscle");
        }
        void ResolveIntegrity(Database db)
        {
            foreach(var c in life.investigations.Where(c=>c.status=="pending"&&c.due<=life.day)){
                if(c.benefit>0&&!c.repaid){Account(-c.benefit,"Restitution de l’avance occulte fictive");c.repaid=true;}
                if(c.response=="cooperate"||Roll()<.60f+life.suspicion*.003f){c.status="sanctioned";long fine=Math.Max(c.cost*3,life.revenue/100);if(c.response=="cooperate")fine/=2;Account(-fine,"Sanction disciplinaire fictive");life.boardTrust=Math.Max(0,life.boardTrust-25);life.reputation=Math.Max(0,life.reputation-20);life.managerBanUntil=life.day+14;if(c.kind=="doping"){var p=Person(c.player);p.banUntil=life.day+90;p.boostUntil=-1;if(Injury(p.id)==null)OpenInjury(db,p.id,"muscle");}
                    Mail("Commission • simulation","Sanction prononcée","Amende de "+fine.ToString("N0")+" €, réputation dégradée, décisions de manager déléguées pendant 14 jours."+(c.kind=="doping"?" Joueur suspendu 90 jours et suivi médical.":" Le conseil retire sa confiance au projet."),c.player,"integrity");
                }else{c.status="closed";Mail("Coulisses • fiction","Enquête classée","Ce dossier n’aboutit pas à une sanction. La suspicion et la perte de confiance ne disparaissent pas automatiquement.",c.player,"integrity");}}
        }
    }
}


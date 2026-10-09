using System;
using System.Collections.Generic;
using System.Linq;

namespace Touchline.Core
{
    [Serializable] public class Employment { public string player,club,parent,role="rotation";public int until,parentUntil,loanUntil,retirement=-1,joined,appearancesAtSigning,wageChangeDay;public long wage,appearanceBonus,releaseClause,originalWage,nextWage;public bool estimated=true,aiRelease,loanAppearanceTracking,loanAppearanceHistoryEstimated;public PlayingTimeUsage playingTime;public MarketTerms terms;public LoanContractConditions parentConditions,purchaseConditions;public bool parentConditionsUnavailable;public string conditionsSource; }
    [Serializable] public class ScoutReport { public string player,club,scout,mission;public int started,due,confidence,judging,lastObserved,depth;public float estimate,potential,uncertainty,potentialUncertainty;public string advice; }
    [Serializable] public class TransferOffer { public string player,seller,status="pending",role;public int due,years,attempts;public long fee,wage,bonus,clause;public bool loan,renewal,precontract;public int joinDay;public string destination;public MarketTerms terms; }
    [Serializable] public class YouthPath { public string player,mentor,focus="balanced",group="academy";public int minutes,lastReview,loanAppearances,loanReviewedThrough=-1;public float progress; public string trainingLoad="standard"; public int trainingSessions,seniorMinutes,trackedLoanMinutes,lastAcademyDay=-1; }
    [Serializable] public class CommercialDeal { public string name,slot,status="offer";public int years=2,until,counterDay;public long annual,asking; }
    [Serializable] public class PressAppearance { public string fixture,phase,answer;public int day; }
    public partial class Career
    {
        void SavePlayer(PlayerData p){CloseMedicalResponsibilitiesForPlayer(p);int index=RosterChangeIndex(p.id);if(index<0){world.rosterChanges.Add(p);rosterIndex[p.id]=world.rosterChanges.Count-1;rosterIndexCount=world.rosterChanges.Count;}else world.rosterChanges[index]=p;}
        // After the first season every player is in rosterChanges (~20 000):
        // keep the first index of each id instead of scanning the list on each
        // save. Rebuilt whenever the list is replaced or changes length elsewhere.
        [NonSerialized] Dictionary<string,int> rosterIndex;[NonSerialized] List<PlayerData> rosterIndexSource;[NonSerialized] int rosterIndexCount;
        int RosterChangeIndex(string id)
        {
            var list=world.rosterChanges;
            if(rosterIndex==null||!ReferenceEquals(rosterIndexSource,list)||rosterIndexCount!=list.Count){
                rosterIndexSource=list;rosterIndexCount=list.Count;rosterIndex=new Dictionary<string,int>(list.Count);
                for(int i=0;i<list.Count;i++)if(list[i]?.id!=null&&!rosterIndex.ContainsKey(list[i].id))rosterIndex[list[i].id]=i;
            }
            if(rosterIndex.TryGetValue(id,out int index)&&index<list.Count&&list[index]?.id==id)return index;
            index=list.FindIndex(x=>x.id==id);if(index>=0)rosterIndex[id]=index;return index;
        }
        public Employment Contract(Database db,string id)
        {
            var found=LookupEmployment(id);if(found!=null)return found;
            var p=db.Find(id);var c=new Employment{player=id,club=p.team,wage=p.wage,until=DayOf(new DateTime(world.year+1+(int)(StableIdentity(id)%4),6,30)),appearanceBonus=0};world.contracts.Add(c);ContractCreatedForPersistence?.Invoke();return c;
        }
        void InitializeEmployment(Database db){ImportFreeAgents(db);EnsureStaffMarket(db);foreach(var p in db.Squad(club))Contract(db,p.id);GenerateSponsors();}
        public long WageBudget=>GrossWageCeiling(FinanceLeague,life.revenue,(life.staff?.members.Sum(s=>s.wage)??0)*52);
        public long TransferBudget=>Math.Max(0,Math.Min(life.cash-life.revenue/40-CommittedPurchases,life.revenue*12/100-world.transferSpent-(world.contracts.Where(c=>c.club==club&&c.parent!=null).Sum(c=>c.terms?.obligationFee??0))));
        public int Knowledge(string id)
        {
            if(revealAttributes||life.players.Any(p=>p.id==id))return 100;
            var r=ReportFor(id);if(r==null)return 0;
            return ReportKnowledge(r);
        }
        public void Scout(Database db,string id)
        {
            OffPitch();EnsureScouting();var p=db.Find(id);if(!Scoutable(p))throw new InvalidOperationException("Choisissez un joueur extérieur disponible.");
            RequireScout();if(Knowledge(id)>=90)throw new InvalidOperationException("Rapport complet déjà disponible.");
            if(ActiveObservations>=3)throw new InvalidOperationException("Trois observations sont déjà en cours.");
            if(ReportFor(id)?.confidence<90)throw new InvalidOperationException("Observation déjà en cours.");
            Charge(ObservationCost,"Observation individuelle de "+p.name);StartObservation(db,p,null);
            Mail(Staff("scout").name,"Observation engagée",p.name+" sera observé. Rapport attendu le "+Epoch.AddDays(ReportFor(id).due).ToString("dd/MM")+" ; l’évaluation dépend du niveau du recruteur.",id,"scout");
        }
        public bool WindowOpen=>Date.Month==1||Date.Month==6&&Date.Day>=15||Date.Month==7||Date.Month==8||Date.Month==9&&Date.Day==1;
        bool OfferForManagedClub(TransferOffer offer)=>string.IsNullOrEmpty(offer.destination)||offer.destination==club;
        // Ownership remains with the parent until the return or purchase is processed,
        // even if the scheduled loan end date has passed. Empty legacy parents mean no loan.
        public bool HasActiveLoan(string id)=>world?.contracts?.Any(c=>c.player==id&&!string.IsNullOrEmpty(c.parent))??false;
        public void ProposeTransfer(Database db,string id,long fee,long wage,int years,string role,bool loan=false,long bonus=0,long clause=0,MarketTerms terms=null,bool precontract=false)
        {
            OffPitch();var p=db.Find(id);if(PlayingCareerEnded(p))throw new InvalidOperationException("La carrière de ce joueur est terminée ; aucun nouveau contrat de joueur ne peut être signé.");bool renewal=p.team==club;
            if(!renewal&&!precontract&&p.team!="free"&&!WindowOpen)throw new InvalidOperationException("Mercato fermé. Dans cette version, fenêtres simulées du 15 juin au 1er septembre et du 1er au 31 janvier.");
            if(world.offers.Any(o=>o.player==id&&o.status=="scheduled"))throw new InvalidOperationException("Un précontrat est déjà signé.");
            if(precontract&&!CanPrecontract(db,id))throw new InvalidOperationException("Le contrat doit expirer dans les six prochains mois, sans prêt en cours.");
            if(HasActiveLoan(id))throw new InvalidOperationException("Ce joueur est sous contrat avec son club prêteur. Une prolongation ne peut pas modifier ses droits : utilisez l’option d’achat prévue ou attendez son retour.");
            if(loan&&(renewal||precontract||p.team=="free"))throw new InvalidOperationException("Ce joueur ne peut pas être prêté dans cette situation.");
            if(terms==null)terms=new MarketTerms{loanEndDay=Math.Min(world.seasonEnd,life.day+365)};ValidateTerms(terms,loan);
            if(loan&&terms.loanEndDay>Contract(db,id).until)throw new InvalidOperationException("Le prêt doit prendre fin au plus tard à l’échéance du contrat parent.");
            if(terms.obligationFee>TransferBudget-fee)throw new InvalidOperationException("Budget insuffisant pour cette obligation d’achat.");
            if(p.age<18&&!renewal)throw new InvalidOperationException("Les transferts de mineurs sont exclus de cette simulation simplifiée.");
            if(years<1||years>(p.age<18?3:5)||fee<0||wage<=0||bonus<0||clause<0||!PlayingTimeRoles.Valid(role))throw new ArgumentException("Conditions contractuelles invalides.");
            if(role=="youth"&&p.age>=24)throw new ArgumentException("Le statut de jeune en développement est réservé aux joueurs de moins de 24 ans.");
            if(world.offers.Any(o=>o.player==id&&OfferForManagedClub(o)&&(o.status=="pending"||o.status=="accepted")))throw new InvalidOperationException("Une négociation est déjà ouverte.");
            if(!renewal&&fee>TransferBudget)throw new InvalidOperationException("L’offre dépasse l’enveloppe de recrutement.");
            // A loan reallocates an existing gross salary; it does not amend the parent contract.
            if(loan)wage=p.wage;
            long effectiveWage=loan?wage*terms.loanWagePercent/100:wage;
            long payroll=Payroll(db)+ReservedWages-(renewal?p.wage:0)+effectiveWage;
            if(payroll>Math.Max(WageBudget,Payroll(db)))throw new InvalidOperationException("La masse salariale dépasserait le plafond du conseil.");
            var old=world.offers.LastOrDefault(o=>o.player==id&&OfferForManagedClub(o));int attempts=old!=null&&life.day-old.due<30?old.attempts+1:1;if(attempts>3)throw new InvalidOperationException("Négociations suspendues pendant trente jours.");
            world.offers.Add(new TransferOffer{player=id,seller=p.team,destination=club,precontract=precontract,joinDay=precontract?Contract(db,id).until+1:life.day,terms=terms,fee=renewal||precontract||p.team=="free"?0:fee,wage=wage,years=years,role=role,loan=loan&&!renewal,renewal=renewal,bonus=bonus,clause=clause,due=life.day+2,attempts=attempts});
            Mail("Agent de "+p.name,"Proposition reçue","Nous étudions les conditions et le statut proposé : "+PlayingTimeRoles.Label(role)+". "+PlayingTimeRoles.Description(role)+" Réponse sous deux jours.",id,"transfer");
        }
        public void SignTransfer(Database db,string id)
        {
            OffPitch();var o=world.offers.LastOrDefault(x=>x.player==id&&OfferForManagedClub(x)&&x.status=="accepted");if(o==null||o.due+7<life.day)throw new InvalidOperationException("L’accord n’est plus valable.");
            var p=db.Find(id);if(PlayingCareerEnded(p))throw new InvalidOperationException("La carrière de ce joueur est terminée ; cet accord ne peut plus être signé.");if(p.team!=o.seller||!o.renewal&&!o.precontract&&p.team!="free"&&!WindowOpen)throw new InvalidOperationException("Le joueur ou la fenêtre de transfert a changé.");
            if(HasActiveLoan(id))throw new InvalidOperationException("Le joueur appartient toujours au club prêteur. Cet accord ne peut pas remplacer son prêt ; utilisez l’option d’achat prévue ou attendez son retour.");
            if(o.loan&&(o.terms==null||o.terms.loanEndDay<=life.day||o.terms.loanEndDay>Contract(db,id).until))throw new InvalidOperationException("La fin du prêt ou l’échéance du contrat parent a changé. Renégociez les conditions.");
            if(o.loan&&o.wage!=p.wage)throw new InvalidOperationException("Le salaire du contrat parent a changé ou cet ancien accord le modifiait. Renégociez le prêt et sa prise en charge salariale.");
            if(!o.renewal&&o.fee+(o.terms?.obligationFee??0)>TransferBudget)throw new InvalidOperationException("Budget de transfert insuffisant.");
            if(Payroll(db)+ReservedWages-(o.renewal?p.wage:0)+(o.loan?o.wage*(o.terms?.loanWagePercent??100)/100:o.wage)>Math.Max(WageBudget,Payroll(db)))throw new InvalidOperationException("Budget salarial insuffisant.");
            if(o.precontract){if(!CanPrecontract(db,id))throw new InvalidOperationException("Le contrat actuel a changé.");Charge(o.wage*2,"Prime d’agent • précontrat");o.status="scheduled";o.joinDay=Contract(db,id).until+1;Mail("Secrétariat","Précontrat signé",p.name+" arrivera le "+Epoch.AddDays(o.joinDay).ToString("dd/MM/yyyy")+". Son club est informé ; le salaire ne commence qu’à son arrivée.",id,"transfer");return;}
            long immediate=o.fee*(o.terms?.upfrontPercent??100)/100;if(immediate+o.wage*2>life.cash)throw new InvalidOperationException("La trésorerie ne couvre pas l’indemnité immédiate et la prime d’agent.");
            BeforeFinancialTermsChange(db,club,o.seller);SettleTransferFee(db,o);world.transferSpent+=o.fee;
            var c=Contract(db,id);if(o.loan)CaptureLoanParent(c,p);else{if(!o.renewal)SettlePermanentDepartureGrowth(p);c.parentConditions=null;c.purchaseConditions=null;c.parentConditionsUnavailable=false;}c.parentUntil=c.until;c.originalWage=p.wage;c.joined=life.day;c.appearancesAtSigning=life.players.FirstOrDefault(x=>x.id==id)?.appearances??0;c.terms=o.terms??new MarketTerms{loanEndDay=Math.Min(world.seasonEnd,life.day+365)};c.parent=o.loan?o.seller:null;c.loanUntil=o.loan?c.terms.loanEndDay:0;c.club=club;c.wage=o.wage;c.until=DayOf(new DateTime(Date.Year+o.years,6,30));c.role=o.role;c.playingTime=new PlayingTimeUsage{club=c.club};c.appearanceBonus=o.bonus;c.releaseClause=o.clause;c.estimated=false;c.aiRelease=false;c.nextWage=0;c.wageChangeDay=0;
            if(o.loan)PrepareLoanPurchase(c,o.years,false);
            p.team=club;p.wage=o.wage;AfterFinancialTermsChange(db,club,o.seller);SavePlayer(p);o.status="signed";if(!life.players.Any(x=>x.id==id))life.players.Add(new PlayerLife{id=id,fitness=p.fitness,morale=p.morale});
            Mail("Secrétariat","Contrat signé",p.name+" rejoint votre projet. Statut promis : "+PlayingTimeRoles.Label(c.role)+". "+PlayingTimeRoles.Description(c.role)+" Les salaires et primes sont désormais dus.",id,"transfer");
        }
        public void RejectOffer(string id){OffPitch();var o=world.offers.LastOrDefault(x=>x.player==id&&OfferForManagedClub(x)&&(x.status=="accepted"||x.status=="pending"));if(o==null)throw new InvalidOperationException("Aucune offre ouverte pour votre club.");o.status="withdrawn";}
        public void ListForSale(Database db,string id)
        {
            OffPitch();var p=db.Find(id);if(p.team!=club||db.Squad(club).Count<=18||Contract(db,id).parent!=null)throw new InvalidOperationException("Le club doit conserver dix-huit joueurs et ne peut céder un joueur prêté.");
            if(world.offers.Any(o=>o.player==id&&o.status=="sale"))throw new InvalidOperationException("Offre déjà disponible.");
            var buyer=db.clubs.Where(c=>c.id!=club&&c.annualRevenue>p.value*5).OrderBy(c=>Math.Abs(c.annualRevenue-life.revenue)).FirstOrDefault();if(buyer==null)throw new InvalidOperationException("Aucun acheteur solvable pour le moment.");
            world.offers.Add(new TransferOffer{player=id,seller=buyer.id,fee=(long)(p.value*(.8f+Roll()*.3f)),status="sale",due=life.day+7});Person(id).morale=Math.Max(10,Person(id).morale-4);
        }
        public void AcceptSale(Database db,string id)
        {
            OffPitch();var o=world.offers.LastOrDefault(x=>x.player==id&&x.status=="sale"&&x.due>=life.day);if(o==null||!WindowOpen)throw new InvalidOperationException("Offre ou fenêtre de vente indisponible.");
            if(db.Squad(club).Count<=18)throw new InvalidOperationException("L’effectif deviendrait insuffisant.");var p=db.Find(id);if(p.team!=club)throw new InvalidOperationException("Ce joueur n’appartient plus au club.");
            ReceiveNpcTransfer(db,o.seller,o.fee,"Cession de "+p.name);SettlePermanentDepartureGrowth(p);p.team=o.seller;Contract(db,id).club=p.team;AfterFinancialTermsChange(db,club,o.seller);o.status="sold";life.players.RemoveAll(x=>x.id==id);SavePlayer(p);lineup=Select(db,club,tactic);
        }
        void CreateIntake(Database db)
        {
            if(world.lastIntake==world.year)return;world.lastIntake=world.year;int count=world.youth.Any(y=>db.Find(y.player)?.team=="academy-"+club)?5+Level("academy"):14+Level("academy");
            string[] first={"Alex","Noah","Elias","Adam","Gabriel","Sacha","Amine","Léo","Mathis","Ilyes","Nolan","Hugo"};string[] last={"Martin","Morel","Bernard","Petit","Roux","Diallo","Laurent","Perrin","Henry","Simon","Benoît","Garcia"};
            float baseline=Strength(db,club);var nationality=db.Squad(club).GroupBy(p=>p.nationality).OrderByDescending(g=>g.Count()).FirstOrDefault()?.Key??"France";
            for(int i=0;i<count;i++){
                string role=new[]{"GK","CB","CB","LB","RB","DM","CM","CM","LW","ST","RW","GK","CB","CM","ST","LW","RB","CM","ST"}[i%19];float rating=Mathx.Clamp(baseline-35+Roll()*15,25,65),potential=Mathx.Clamp(rating+12+Roll()*22+Level("academy"),rating,92);
                var p=new PlayerData{id="gen-"+club+"-"+world.year+"-"+i,name=first[(int)(Roll()*first.Length)]+" "+last[(int)(Roll()*last.Length)],team="academy-"+club,age=16+(int)(Roll()*2),number=60+i,position=role=="GK"?"GB":new[]{"CB","LB","RB"}.Contains(role)?"DEF":new[]{"CM","DM"}.Contains(role)?"MIL":"ATT",positions=new[]{role},nationality=nationality,rating=rating,potential=potential,fitness=100,morale=75,wage=150,value=50000,preferredFoot=Roll()<.2f?"Left":"Right"};
                if(db.Find(p.id)!=null)continue;
                var sample=db.Squad(club).FirstOrDefault(x=>x.attributes?.Length>0);p.attributes=(sample?.attributes??new[]{new AttributeValue{key="shortPassing"},new AttributeValue{key="sprintSpeed"},new AttributeValue{key="finishing"}}).Select(a=>new AttributeValue{key=a.key,value=Mathx.Clamp(rating+(Roll()-.5f)*18,10,85)}).ToArray();
                db.players=db.players.Concat(new[]{p}).ToArray();SavePlayer(p);world.youth.Add(new YouthPath{player=p.id,lastAcademyDay=life.day});
            }
            Mail("Directeur de la formation","Nouvelle promotion",count+" jeunes fictifs ont rejoint le centre. Évaluez-les, choisissez leurs axes de travail et préparez progressivement leur passage en équipe première.",null,"academy");
        }
        public void PromoteYouth(Database db,string id)
        {
            OffPitch();string issue=YouthPromotionIssue(db,id);if(issue!=null)throw new InvalidOperationException(issue);
            var y=RequireAcademyPath(db,id);var p=db.Find(id);long wage=Math.Max(250,p.wage);
            BeforeFinancialTermsChange(db,club);p.team=club;y.group="senior";y.mentor=null;p.wage=wage;SavePlayer(p);
            var contract=Contract(db,id);contract.club=club;contract.wage=wage;contract.joined=life.day;
            if(!life.players.Any(x=>x.id==id))life.players.Add(new PlayerLife{id=id,fitness=p.fitness,morale=p.morale});AfterFinancialTermsChange(db,club);
            Mail("Formation","Passage chez les professionnels",p.name+" rejoint l’entraînement de l’équipe première. Un temps de jeu adapté reste essentiel.",id,"academy");
        }
        public void SetYouthPlan(Database db,string id,string focus,string mentor)
        {
            OffPitch();if(!new[]{"balanced","physical","technical","tactical"}.Contains(focus))throw new ArgumentException("Axe inconnu.");
            var y=RequireAcademyPath(db,id);if(db.Find(id).team!="academy-"+club)throw new InvalidOperationException("Le plan dépend désormais du groupe professionnel ou du club emprunteur.");
            if(mentor!=null&&!CanMentorYouth(db,id,mentor))throw new InvalidOperationException("Choisissez un cadre du club de 26 ans ou plus, du même groupe gardiens/joueurs de champ, avec au plus trois jeunes suivis.");
            y.focus=focus;y.mentor=mentor;
        }
        void SeasonPlayers(Database db) => AnnualPlayerDevelopment(db);
        void GenerateSponsors()
        {
            foreach(var slot in new[]{"maillot","équipementier","naming"})for(int i=0;i<3;i++)world.sponsors.Add(new CommercialDeal{name=new[]{"Horizon","Alto","Novalys"}[i]+" · "+slot,slot=slot,annual=Math.Max(10000,(long)(life.revenue*(slot=="maillot"?.05:slot=="naming"?.012:.025)*(1+i*.12))),years=2+i});
        }
        public void NegotiateSponsor(int index,long annual,int years)
        {
            OffPitch();var d=world.sponsors[index];if(d.status=="signed"||d.status=="expired"||life.day<d.counterDay||annual<=0||years<1||years>5)throw new InvalidOperationException("Cette négociation est indisponible.");
            if(world.sponsors.Any(x=>x.slot==d.slot&&x.status=="signed"&&x.until>life.day))throw new InvalidOperationException("Cet emplacement est sous contrat.");
            long limit=(long)(d.annual*(1+(world.supporterTrust-50)*.0015f)+(years-2)*d.annual*.02f);
            d.asking=annual;d.years=years;d.counterDay=life.day+3;if(annual<=limit){d.status="accepted";d.asking=annual;}else{d.status="counter";d.asking=limit;}
            Mail("Direction commerciale",d.status=="accepted"?"Proposition acceptée":"Contre-proposition",d.name+" propose "+d.asking.ToString("N0")+" € par an sur "+years+" ans. Signature disponible dans Finances.",null,"finance",CommercialMessageReference(d));
        }
        public void SignSponsor(int index)
        {
            OffPitch();var d=world.sponsors[index];if((d.status!="accepted"&&d.status!="counter")||world.sponsors.Any(x=>x.slot==d.slot&&x.status=="signed"&&x.until>life.day))throw new InvalidOperationException("Signature indisponible.");
            d.status="signed";d.annual=d.asking;d.until=life.day+365*d.years;Mail("Direction commerciale","Partenariat signé",d.name+" : les recettes seront versées chaque semaine. Engagement ferme jusqu’au terme convenu.",null,"finance");
        }
        public float Occupancy=>Mathx.Clamp(.93f+(world.supporterTrust-70)*.003f-(world.ticket-Mathx.Clamp((float)Math.Sqrt(life.revenue)/400,8,90))*.009f,.15f,1);
        public long GateIncome(float multiplier=1)=>(long)(world.capacity*(1+Level("stadium")*.04f)*Occupancy*world.ticket*.85f*multiplier);
        public void SetTicketPrice(int price){OffPitch();if(price<5||price>250||life.day-world.lastTicketDay<7)throw new InvalidOperationException("Prix de 5 à 250 €, modifiable une fois par semaine.");if(price>world.ticket*1.25f)world.supporterTrust=Math.Max(0,world.supporterTrust-5);world.ticket=price;world.lastTicketDay=life.day;}
        public void RepayDebt(long amount){OffPitch();if(amount<=0||amount>world.debt)throw new InvalidOperationException("Remboursement invalide.");Charge(amount,"Remboursement de dette");world.debt-=amount;}
        public void Press(string phase,string answer)
        {
            OffPitch();var fixture=phase=="before"?NextFixture():world.fixtures.LastOrDefault(f=>f.played&&(f.home==club||f.away==club));
            if(fixture==null||!new[]{"calm","ambition","protect"}.Contains(answer)||phase!="before"&&phase!="after")throw new InvalidOperationException("Aucune conférence disponible.");
            if(world.press.Any(p=>p.fixture==fixture.id&&p.phase==phase))throw new InvalidOperationException("Vous avez déjà pris la parole.");
            if(phase=="before"&&fixture.day-life.day>2)throw new InvalidOperationException("La conférence se tient dans les deux jours précédant la rencontre.");
            world.press.Add(new PressAppearance{fixture=fixture.id,phase=phase,answer=answer,day=life.day});
            foreach(var p in life.players){p.morale=Mathx.Clamp(p.morale+(answer=="protect"?1:answer=="ambition"?(p.trust>60?1:-1):.25f),10,100);}
            world.supporterTrust=Mathx.Clamp(world.supporterTrust+(answer=="ambition"?1:0),0,100);
            Mail("Attaché de presse","Votre déclaration",answer=="calm"?"Nous nous concentrons sur notre travail et sur ce que nous pouvons maîtriser.":answer=="protect"?"J’assume les décisions. Le groupe a besoin de notre soutien.":"Nous voulons imposer notre jeu et obtenir un résultat.",null,"press");
        }
        void ManagementDay(Database db)
        {
            ImportFreeAgents(db);
            ProcessAiEmployment(db);
            MarketDay(db);
            if(life.day%30==0)foreach(var slot in new[]{"maillot","équipementier","naming"})if(!world.sponsors.Any(s=>s.slot==slot&&s.status!="expired")){world.sponsors.Add(new CommercialDeal{name="Horizon · "+slot+" · "+world.year,slot=slot,annual=Math.Max(10000,life.revenue*(slot=="maillot"?50:slot=="naming"?12:25)/1000),years=2});}
            ScoutingDay(db);
            foreach(var o in world.offers.Where(o=>o.status=="pending"&&o.due<=life.day)){
                string destination=string.IsNullOrEmpty(o.destination)?club:o.destination;var p=db.Find(o.player);
                if(p==null||!db.clubs.Any(c=>c.id==destination)){o.status="expired";continue;}
                long expectedFee=o.renewal||o.precontract||p.team=="free"?0:(long)(p.value*(o.loan?.12f:1.05f));long expectedWage=(long)(p.wage*(o.renewal?1.08f:o.loan?1f:1.12f));
                if(o.loan)expectedFee+=(long)(p.wage*26*(100-(o.terms?.loanWagePercent??100))/100);
                string roleIssue=PlayingTimeOfferIssueForClub(db,p.id,o.role,destination);bool willing=o.renewal||p.rating+p.development<Strength(db,destination)+8;bool accepted=roleIssue==null&&o.fee>=expectedFee&&o.wage>=expectedWage&&willing&&(o.clause==0||o.clause>=p.value);
                o.status=accepted?"accepted":"counter";if(!accepted){o.fee=expectedFee;o.wage=expectedWage;}
                if(destination==club)Mail("Agent de "+p.name,accepted?"Accord de principe":"Négociation à reprendre",accepted?"Conditions acceptées avec le statut de "+PlayingTimeRoles.Label(o.role)+". "+PlayingTimeRoles.Description(o.role)+" Vous avez sept jours pour confirmer la signature.":roleIssue!=null?roleIssue:willing?"La proposition doit être relevée : indemnité "+expectedFee.ToString("N0")+" €, salaire mensuel "+MonthlySalary(expectedWage).ToString("N0")+" €. Une clause libératoire ne doit pas être inférieure à la valeur du joueur.":"Le joueur ne juge pas le projet sportif suffisamment attractif.",p.id,"transfer",TransferMessageReference(o));
            }
            foreach(var o in world.offers.Where(o=>o.status=="accepted"&&o.due+7<life.day))o.status="expired";
            foreach(var c in world.contracts.ToArray()){
                var p=db.Find(c.player);if(p==null)continue;
                if(c.club==club&&c.parent==null&&c.until-life.day==180)Mail("Agent de "+p.name,"Six mois de contrat","Le contrat arrive à échéance dans six mois. Nous devons discuter de l’avenir.",p.id,"transfer");
                if(c.club==club&&c.until<=life.day&&c.parent==null&&!(c.retirement>=0&&c.retirement<=life.day)&&!world.offers.Any(o=>o.player==p.id&&o.status=="scheduled")){SettlePermanentDepartureGrowth(p);p.team="free";c.club="free";life.players.RemoveAll(x=>x.id==p.id);SavePlayer(p);Mail("Secrétariat","Départ en fin de contrat",p.name+" quitte le club sans indemnité.",p.id,"transfer");}
                if(c.club==club&&p.age>=RetirementThreshold(p)-3&&c.retirement<0&&life.day%30==0&&Roll()<.035f+Math.Max(0,p.age-(RetirementThreshold(p)-3))*.012f){c.retirement=Math.Max(life.day+90,world.seasonEnd+1);Mail(p.name,"Mon avenir","J’ai décidé de prendre ma retraite après la fin de cette saison. Je voulais vous en parler personnellement.",p.id,"talk");}
                if(c.retirement>=0&&c.retirement<=life.day&&p.team!="retired"){RetireEmployment(db,p,c);life.players.RemoveAll(x=>x.id==p.id);SavePlayer(p);Mail("Secrétariat","Retraite effective",p.name+" met un terme à sa carrière.",p.id);}
            }
            AcademyDevelopmentDay(db);
            WarnThinSquad(db);
            if(life.day%7==0){foreach(var d in world.sponsors.Where(s=>s.status=="signed"&&s.until>life.day))Account(d.annual/52,"Partenariat • "+d.name);if(world.debt>0)Account(-world.debt/1000,"Intérêts de dette estimés");}
            foreach(var d in world.sponsors.Where(d=>d.status=="signed"&&d.until<=life.day)){d.status="expired";Mail("Direction commerciale","Partenariat terminé",d.name+" arrive à échéance. Un emplacement est à nouveau disponible.");}
            if(life.day>=world.reviewDay){world.reviewDay=life.day+30;var division=world.divisions.FirstOrDefault(d=>d.clubs.Contains(club));var table=division==null?new List<Standing>():Table(division.id);var own=table.FirstOrDefault(t=>t.club==club);
                if(own!=null&&own.played>=5){int expected=division.clubs.OrderByDescending(id=>Strength(db,id)).ToList().IndexOf(club)+1,actual=table.IndexOf(own)+1;life.boardTrust=Mathx.Clamp(life.boardTrust+(expected-actual)*.7f,0,100);}
                if(life.cash<0){world.debt+=-life.cash;Account(-life.cash,"Avance de trésorerie du propriétaire • dette");life.boardTrust=Math.Max(0,life.boardTrust-8);}
                if(life.boardTrust<25&&world.managerStatus=="employed"){world.managerStatus="dismissed";world.jobDay=life.day;Mail("Présidence","Fin de votre mandat","Le conseil met fin à votre contrat. Consultez les postes disponibles pour poursuivre votre carrière.",null,"jobs");}
                if(life.day>90&&Roll()<.015f){world.owner="Consortium "+(world.year+world.serial%17);bool invest=Roll()<.6f;Account(invest?life.revenue/20:0,"Rachat • apport du nouveau propriétaire");life.boardTrust=Mathx.Clamp(life.boardTrust-5,0,100);Mail("Présidence","Changement de propriétaire",world.owner+" reprend le club. "+(invest?"Un apport de fonds est annoncé, avec des attentes renforcées.":"La priorité reste l’équilibre financier. Aucun apport immédiat."));}
                Mail("Adjoint","Plan de travail",StaffAdvice(db),null,"training");
            }
            ReviewApproaches(db);
            DevelopmentAndRoles(db);
            if(lineup==null||lineup.Any(id=>db.Find(id)?.team!=club)){if(db.Squad(club).Count>=11)lineup=Select(db,club,tactic);}
        }
        public string StaffAdvice(Database db)
        {
            var next=NextFixture();var tired=life.players.Count(p=>p.fitness<80);var own=Strength(db,club);float enemy=next==null?own:Strength(db,next.home==club?next.away:next.home);
            return tired>4? tired+" joueurs sont sous 80 % de condition. Prévoyez récupération et rotation avant de maintenir un pressing intense.":enemy>own+5?"L’adversaire possède un onze supérieur. Un bloc plus compact et des transitions rapides peuvent protéger nos points faibles ; gardez un attaquant en profondeur.":tactic.width>.8f&&tactic.directness<.3f?"Notre largeur et nos passes courtes allongent les distances de soutien. Rapprochez un milieu du porteur ou autorisez des passes plus directes.":"Le groupe est disponible. Travaillez les automatismes de la formation choisie et conservez une couverture lorsque les latéraux montent.";
        }
        public void TakeJob(Database db,string id)
        {
            if(world.managerStatus!="dismissed"||life.day-world.jobDay<7||id==club||db.Squad(id).Count<18||db.clubs.First(c=>c.id==id).annualRevenue>life.revenue*1.5)throw new InvalidOperationException("Ce poste n’est pas accessible actuellement.");
            MoveManager(db,id);
        }
    }
}

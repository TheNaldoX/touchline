using System;
using System.Collections.Generic;
using System.Linq;

namespace Touchline.Core
{
    [Serializable] public class AiClubAccount
    {
        public string club,policy="équilibre"; public int settledYear=2026,projectedFromDay,training=1,academy=1;
        public long cash,openingCash,externalFunding,operatingDebt,dividends,investments; public List<AccountEntry> ledger=new List<AccountEntry>();
        public bool hasOperatingSnapshot,legacySnapshotEstimate;
        public string projectedLeague;
        public long projectedRevenue,projectedGrossPayroll;
        public int projectedFacilityLevels;
    }
    [Serializable] public class AiTransferRecord { public string player,buyer,seller;public int year;public long fee,agentFee,wage; }
    public partial class Career
    {
        public void EnsureAiClubAccounts(Database db)
        {
            world.aiAccounts??=new List<AiClubAccount>();world.aiTransfers??=new List<AiTransferRecord>();
            if(!world.initialAiEmployment){
                var registered=new HashSet<string>(world.contracts.Select(c=>c.player));var clubIds=new HashSet<string>(db.clubs.Select(t=>t.id));
                foreach(var p in db.players)if(clubIds.Contains(p.team)&&registered.Add(p.id))world.contracts.Add(new Employment{player=p.id,club=p.team,wage=p.wage,until=DayOf(new DateTime(world.year+1+(int)(StableIdentity(p.id)%4),6,30)),estimated=true});
                world.initialAiEmployment=true;
            }
            Dictionary<string,long> grouped=null;
            var known=new HashSet<string>(world.aiAccounts.Select(a=>a.club));
            foreach(var team in db.clubs)if(known.Add(team.id)){
                long cash=team.id==club?life.cash:team.annualRevenue*12/100;
                world.aiAccounts.Add(new AiClubAccount{club=team.id,cash=cash,openingCash=cash,operatingDebt=team.id==club?world.debt:previousClubs.FirstOrDefault(p=>p.club==team.id)?.debt??0,settledYear=world.year,projectedFromDay=life.day,policy=StableIdentity(team.id)%3==0?"réinvestissement":StableIdentity(team.id)%3==1?"équilibre":"rendement"});
                if(team.id==club)Mail("Présidence","Politique financière du propriétaire","En fin de saison, un bénéfice de trésorerie peut donner lieu à une distribution. Les apports du propriétaire sont exclus du résultat distribuable ; six mois de recettes, les dettes et les engagements déjà enregistrés sont réservés. Le versement reste plafonné à 15 % des recettes annuelles. Cette politique est une hypothèse de simulation.",null,"finance");
            }
            foreach(var a in world.aiAccounts)if(!a.hasOperatingSnapshot){
                var team=db.clubs.FirstOrDefault(t=>t.id==a.club);if(team==null)continue;
                grouped??=ClubWeeklyPayrolls(db);
                // Old saves do not contain reconstructible historical payroll.
                // Use current terms once, explicitly, without resetting cash/debt.
                a.legacySnapshotEstimate=a.projectedFromDay<life.day;
                CaptureAiOperatingSnapshot(team,a,grouped.TryGetValue(team.id,out var gross)?gross:0);
            }
        }
        void CaptureAiOperatingSnapshot(ClubData team,AiClubAccount a,long weeklyGross)
        {
            a.projectedRevenue=team.annualRevenue;a.projectedLeague=team.league;
            a.projectedGrossPayroll=weeklyGross*52;a.projectedFacilityLevels=a.training+a.academy;a.hasOperatingSnapshot=true;
        }
        Dictionary<string,long> ClubWeeklyPayrolls(Database db)
        {
            var totals=db.clubs.ToDictionary(t=>t.id,t=>0L);
            var loans=new Dictionary<string,Employment>();
            foreach(var contract in world?.contracts??Enumerable.Empty<Employment>())if(contract.parent!=null&&contract.player!=null)loans[contract.player]=contract;
            foreach(var p in db.players){
                if(p==null||p.team==null||!totals.ContainsKey(p.team))continue;
                if(loans.TryGetValue(p.id,out var loan)&&loan.club==p.team&&loan.parent!=p.team&&totals.ContainsKey(loan.parent)){
                    long borrowed=BorrowedLoanWage(p,loan);
                    totals[p.team]+=borrowed;totals[loan.parent]+=p.wage-borrowed;
                }else totals[p.team]+=p.wage;
            }
            return totals;
        }
        static long BorrowedLoanWage(PlayerData p,Employment loan)
        {
            // An old loan without saved clauses uses the youth-loan 50/50
            // assumption on both sides. New loans always store their terms.
            int percent=Math.Max(0,Math.Min(100,loan.terms?.loanWagePercent??50));return p.wage*percent/100;
        }
        void AiEntry(AiClubAccount account,long amount,string label)
        {
            account.cash+=amount;account.ledger.Add(new AccountEntry{day=life.day,amount=amount,label=label});
            if(account.ledger.Count>100)account.ledger.RemoveAt(0);
            if(account.club==club)Account(amount,label);
        }
        // AiCommitted for every club in one pass over payments, loans and
        // offers (same formula), for loops that would otherwise rescan the
        // whole contract list for each of the ~700 clubs.
        Dictionary<string,long> AiCommittedByClub(IEnumerable<string> ids)
        {
            var unpaid=new Dictionary<string,long>();foreach(var p in payments)if(!p.settled&&p.club!=null)unpaid[p.club]=unpaid.GetValueOrDefault(p.club)+p.amount;
            var obligations=new Dictionary<string,long>();foreach(var c in world.contracts)if(c.parent!=null&&c.club!=null)obligations[c.club]=obligations.GetValueOrDefault(c.club)+(c.terms?.obligationFee??0);
            var incoming=new Dictionary<string,long>();foreach(var o in world.offers)if(o.destination!=null&&(o.status=="accepted"||o.status=="scheduled"))incoming[o.destination]=incoming.GetValueOrDefault(o.destination)+o.fee+o.wage*26;
            var result=new Dictionary<string,long>();
            foreach(var id in ids){
                long committed=unpaid.GetValueOrDefault(id)+obligations.GetValueOrDefault(id)+incoming.GetValueOrDefault(id);
                var former=previousClubs.FirstOrDefault(p=>p.club==id);
                committed+=id==club?world.debt:Math.Max(world.aiAccounts.FirstOrDefault(a=>a.club==id)?.operatingDebt??0,former?.debt??0);
                var projects=id==club?life.projects:former?.life.projects;
                if(projects!=null)foreach(var project in projects)if(project.status=="approved"||project.status=="requested")committed+=Math.Max(0L,project.cost-project.contribution);
                result[id]=committed;
            }
            return result;
        }
        long AiCommitted(string id)
        {
            long committed=payments.Where(p=>p.club==id&&!p.settled).Sum(p=>p.amount)
                +world.contracts.Where(c=>c.club==id&&c.parent!=null).Sum(c=>c.terms?.obligationFee??0)
                +world.offers.Where(o=>o.destination==id&&(o.status=="accepted"||o.status=="scheduled")).Sum(o=>o.fee+o.wage*26);
            var former=previousClubs.FirstOrDefault(p=>p.club==id);
            committed+=id==club?world.debt:Math.Max(world.aiAccounts.FirstOrDefault(a=>a.club==id)?.operatingDebt??0,former?.debt??0);
            var projects=id==club?life.projects:former?.life.projects;
            if(projects!=null)foreach(var project in projects)if(project.status=="approved"||project.status=="requested")committed+=Math.Max(0L,project.cost-project.contribution);
            return committed;
        }
        void ProjectAiOperatingPeriod(ClubData team,AiClubAccount a,List<PlayerData> squad,int day)
        {
            double fraction=Math.Max(0,day-a.projectedFromDay)/365.25;
            if(fraction<=0)return;
            var historicalTeam=new ClubData{id=team.id,league=a.projectedLeague,annualRevenue=a.projectedRevenue};
            AiEntry(a,(long)(a.projectedRevenue*fraction),"Projection annuelle IA · recettes de début de période, billetterie incluse");
            var personnel=EmploymentProjection(a.projectedLeague,a.projectedGrossPayroll);
            AiEntry(a,-(long)(personnel.playersGross*fraction),"Salaires bruts annuels IA");
            AiEntry(a,-(long)(personnel.playerContributions*fraction),"Cotisations employeur IA · projection");
            AiEntry(a,-(long)(personnel.otherPersonnel*fraction),"Autres personnels IA · projection");
            AiEntry(a,-(long)(AnnualOperatingCosts(historicalTeam,a.projectedFacilityLevels)*fraction),"Exploitation, staff et entretien IA · estimation de début de période");
            long debt=a.operatingDebt;if(debt>0)AiEntry(a,-(long)(debt*.052*fraction),"Intérêts annuels de dette IA · estimation");
            CoverAiDeficit(a);
            a.projectedFromDay=day;
        }
        long ProjectedAiOperatingBalance(AiClubAccount a)
        {
            double fraction=Math.Max(0,life.day-a.projectedFromDay)/365.25;
            var team=new ClubData{id=a.club,league=a.projectedLeague,annualRevenue=a.projectedRevenue};
            long expected=a.projectedRevenue-EmploymentProjection(a.projectedLeague,a.projectedGrossPayroll).Total-AnnualOperatingCosts(team,a.projectedFacilityLevels)-(long)(a.operatingDebt*.052);
            return a.cash+(long)(expected*fraction);
        }
        // Ambition of a computer-run club (0 = prudent, 1 = owner who spends
        // beyond revenue), stable for a club so its policy is consistent.
        // Prudent clubs bank cash; ambitious ones push wages and fees and can
        // end up in debt, as real clubs do.
        const float AiTransferShareMin=.06f,AiTransferShareRange=.26f;  // transfer budget, share of annual revenue
        const float AiCashReserveMax=.35f,AiCashReserveRange=.30f;      // cash kept back, share of annual revenue
        const float AiWageShareMin=.55f,AiWageShareRange=.30f;          // loaded payroll ceiling, share of revenue
        const float AiWagePremiumMin=1.0f,AiWagePremiumRange=.6f;     // renewal wage vs league reference (×)
        const float AiOwnerStanceShare=.30f,AiOwnerStanceNeutral=.35f;   // owner stance: -10,5 % to +19,5 % of revenue (most clubs accept a small loss)
        const float AiAmbitiousThreshold=.75f;                           // above: up to 3 signings per summer
        const int AiStarters=11,AiDefaultSquadTarget=32,AiListedMinAge=21;           // starters a seller keeps; squad target without reference; youth never listed
        const float AiStepUpRevenueRatio=1.5f,AiListedFeeShare=.8f;                   // buyer revenue ratio for a step-up move; fee of a listed player (× value)
        public static float AiAmbition(string clubId)=>StableIdentity("ambition:"+clubId)%1001/1000f;
        // Debt (share of annual revenue) from which an owner stops funding planned
        // losses, and from which he demands the most prudent wage policy.
        const float AiOwnerDebtTolerance=.15f,AiOwnerDebtLimit=.5f;
        // Ambition actually applied to the payroll: unchanged without debt, then
        // reduced linearly down to zero (prudent owner) as debt reaches the limit.
        public static float AiOwnerStanceAmbition(float ambition,long debt,long revenue)
        {
            if(revenue<=0||debt<=revenue*AiOwnerDebtTolerance)return ambition;
            float excess=Mathx.Clamp((debt/(float)revenue-AiOwnerDebtTolerance)/(AiOwnerDebtLimit-AiOwnerDebtTolerance),0,1);
            return ambition*(1-excess);
        }
        long AiGrossWageCeiling(ClubData team)
        {
            var a=world.aiAccounts.FirstOrDefault(x=>x.club==team.id);
            if(a==null)return GrossWageCeiling(team.league,team.annualRevenue);
            long projectedCash=ProjectedAiOperatingBalance(a),debt=a.operatingDebt+Math.Max(0,-projectedCash),interest=(long)(debt*.052);
            // Keep a real route to repayment and a small contingency, instead
            // of committing every available euro to another three-year deal.
            long repayment=Math.Min(debt,Math.Min(team.annualRevenue*15/100,Math.Max(team.annualRevenue*3/100,debt*6/100)));
            long due=payments.Where(p=>p.club==team.id&&!p.settled&&p.due<=life.day+365).Sum(p=>p.amount)
                +world.contracts.Where(c=>c.club==team.id&&c.parent!=null&&c.terms?.loanEndDay<=life.day+365).Sum(c=>c.terms?.obligationFee??0)
                +world.offers.Where(o=>o.destination==team.id&&(o.status=="accepted"||o.status=="scheduled")&&o.joinDay<=life.day+365).Sum(o=>o.fee);
            long cashBuffer=Math.Max(0,projectedCash-team.annualRevenue/4-AiCommitted(team.id));
            long unfundedDue=Math.Max(0,due-cashBuffer);
            // An ambitious owner accepts a planned operating loss (prudent owners
            // keep a margin): the share of revenue added or held back.
            float stance=AiOwnerStanceAmbition(AiAmbition(team.id),debt,team.annualRevenue);
            long ownerStance=(long)(team.annualRevenue*AiOwnerStanceShare*(stance-AiOwnerStanceNeutral));
            long available=Math.Max(0,team.annualRevenue-AnnualOperatingCosts(team,a.training+a.academy)-interest-repayment-team.annualRevenue*3/100-unfundedDue+ownerStance);
            double loaded=Math.Min(team.annualRevenue*(AiWageShareMin+AiWageShareRange*stance),available);
            return Math.Max(0,(long)(loaded*ProfessionalPersonnelShare(team.league)/(1+EmployerRatio(team.league))/52));
        }
        void CoverAiDeficit(AiClubAccount a)
        {
            if(a.cash>=0)return;long shortfall=-a.cash;a.operatingDebt+=shortfall;a.externalFunding+=shortfall;
            AiEntry(a,shortfall,"Dette d'exploitation IA · couverture du déficit");if(a.club==club)world.debt=a.operatingDebt;
        }
        void AiSummerEconomy(Database db)
        {
            EnsureAiClubAccounts(db);var accounts=world.aiAccounts.ToDictionary(a=>a.club);
            var squads=db.players.Where(p=>p.team!="retired"&&p.team!="free"&&!p.team.StartsWith("academy-")).GroupBy(p=>p.team).ToDictionary(g=>g.Key,g=>g.ToList());
            var teams=db.clubs.ToDictionary(t=>t.id);var eligible=new HashSet<string>();
            foreach(var team in db.clubs){
                var a=accounts[team.id];if(a.settledYear>=world.year)continue;eligible.Add(team.id);
                if(team.id==club){a.cash=life.cash;a.operatingDebt=world.debt;} // Daily ledger already paid receipts/wages/gates.
                else if(squads.TryGetValue(team.id,out var squad))ProjectAiOperatingPeriod(team,a,squad,life.day);
                CoverAiDeficit(a);
                if(team.id!=club||world.managerStatus!="employed"){
                    long repayment=Math.Min(a.operatingDebt,Math.Max(0,a.cash-team.annualRevenue/4)/2);
                    if(repayment>0){AiEntry(a,-repayment,"Remboursement de dette IA");a.operatingDebt-=repayment;if(team.id==club)world.debt=a.operatingDebt;}
                    var former=previousClubs.FirstOrDefault(p=>p.club==team.id);if(former!=null)former.debt=a.operatingDebt;
                }
            }
            // Candidates are scanned for every club and weak position: pre-filter
            // the invariant conditions (age, value) once and cache derived
            // values. The final order is fully determined by (value ratio, id),
            // so the scan order and the results are unchanged.
            // Keepers and outfield players are kept apart and sorted by rating so
            // a scan stops at the first player below the required level.
            var pool=db.players.Where(p=>p.age>=18&&p.age<=29&&p.value>0).ToArray();
            var poolKeepers=pool.Where(p=>p.Goalkeeper).OrderByDescending(p=>p.rating).ToArray();
            var poolOutfield=pool.Where(p=>!p.Goalkeeper).OrderByDescending(p=>p.rating).ToArray();
            var fits=new Dictionary<(string,string),float>();float Fit(PlayerData p,string role){var key=(role,p.id);if(!fits.TryGetValue(key,out var v))fits[key]=v=p.Fit(role);return v;}
            // Commitments only change with debts settled above; signings below
            // move cash, never these obligations.
            var committed=AiCommittedByClub(eligible);
            var protectedPlayers=new HashSet<string>(world.contracts.Where(c=>c.parent!=null).Select(c=>c.player));
            protectedPlayers.UnionWith(world.offers.Where(o=>o.status=="accepted"||o.status=="scheduled"||o.status=="pending"||o.status=="sale").Select(o=>o.player));
            // A seller lets a player go only for a reason: he is not a starter, the
            // squad is above its size, debt forces sales, the player was listed as
            // surplus, or the buyer is a clearly bigger club (a step up).
            var targets=(world.developmentReferences??new List<ClubDevelopmentReference>()).GroupBy(r=>r.club).ToDictionary(g=>g.Key,g=>g.First().squadSize);
            var starters=new HashSet<string>(squads.Values.SelectMany(q=>q.OrderByDescending(p=>p.rating).ThenBy(p=>p.id,StringComparer.Ordinal).Take(AiStarters)).Select(p=>p.id));
            var listed=new HashSet<string>();
            bool Releases(PlayerData p,List<PlayerData> seller,ClubData buyer)
            {
                if(listed.Contains(p.id)||!starters.Contains(p.id)||seller.Count>(targets.TryGetValue(p.team,out var size)?size:AiDefaultSquadTarget))return true;
                if(!teams.TryGetValue(p.team,out var owner))return true;
                if(accounts.TryGetValue(p.team,out var books)&&books.operatingDebt>owner.annualRevenue*AiOwnerDebtTolerance)return true;
                return buyer.annualRevenue>=owner.annualRevenue*AiStepUpRevenueRatio;
            }
            foreach(var team in db.clubs.OrderByDescending(t=>t.annualRevenue).ThenBy(t=>t.id,StringComparer.Ordinal)){
                if(!eligible.Contains(team.id)||team.id==club&&world.managerStatus=="employed"||!squads.TryGetValue(team.id,out var squad))continue;
                var account=accounts[team.id];float ambition=AiAmbition(team.id);long budget=Math.Max(0,Math.Min((long)(team.annualRevenue*(AiTransferShareMin+AiTransferShareRange*ambition)),account.cash-(long)(team.annualRevenue*(AiCashReserveMax-AiCashReserveRange*ambition))-committed[team.id]));
                int target=targets.TryGetValue(team.id,out var reference)?reference:AiDefaultSquadTarget;
                for(int signing=0;signing<(ambition>AiAmbitiousThreshold?3:2)&&budget>0&&squad.Count<target+2;signing++){
                    PlayerData candidate=null;long wageCeiling=AiGrossWageCeiling(team);long squadWages=squad.Sum(x=>x.wage);
                    foreach(var weak in squad.Where(p=>p.age>22).OrderBy(p=>p.rating).GroupBy(p=>p.positions?.FirstOrDefault()??p.position).Select(g=>g.First())){
                    bool weakKeeper=weak.Goalkeeper;string role=weak.positions?.FirstOrDefault()??(weakKeeper?"GK":"CM");float minimum=weak.rating+3;
                    // Cheapest rating gain first, ties by id: same order as the former
                    // OrderBy/ThenBy, found in one pass instead of sorting every match.
                    candidate=null;float bestCost=0;
                    foreach(var p in weakKeeper?poolKeepers:poolOutfield){
                        if(p.rating<minimum)break;
                        if(!(p.value*1.05+p.wage*4.4<=budget&&squadWages+p.wage*1.1<=wageCeiling&&p.team!=team.id&&!(p.team==club&&world.managerStatus=="employed")&&!protectedPlayers.Contains(p.id)&&squads.TryGetValue(p.team,out var seller)&&seller.Count>22&&(!weakKeeper||seller.Count(x=>x.Goalkeeper)>2)&&Fit(p,role)>=.86f&&Releases(p,seller,team)))continue;
                        float cost=p.value/Math.Max(1,p.rating-weak.rating);
                        if(candidate==null||cost<bestCost||cost==bestCost&&string.CompareOrdinal(p.id,candidate.id)<0){candidate=p;bestCost=cost;}
                    }
                    if(candidate!=null)break;
                    }
                    if(candidate==null)break;
                    string sellerId=candidate.team;long fee=(long)(candidate.value*(listed.Remove(candidate.id)?AiListedFeeShare:1.05)),wage=(long)(candidate.wage*1.1),agentFee=wage*4;
                    AiEntry(account,-fee,"Recrutement IA · "+candidate.name);AiEntry(accounts[sellerId],fee,"Vente IA · "+candidate.name);AiEntry(account,-agentFee,"Honoraires agent IA · "+candidate.name);
                    budget-=fee+agentFee;squads[sellerId].Remove(candidate);squad.Add(candidate);candidate.team=team.id;candidate.wage=wage;candidate.salarySource="Salaire négocié dans la simulation IA";
                    var contract=Contract(db,candidate.id);contract.club=team.id;contract.wage=wage;contract.until=life.day+365*3;contract.joined=life.day;contract.estimated=true;contract.parent=null;contract.aiRelease=false;contract.nextWage=0;contract.wageChangeDay=0;
                    SavePlayer(candidate);protectedPlayers.Add(candidate.id);world.aiTransfers.Add(new AiTransferRecord{player=candidate.id,buyer=team.id,seller=sellerId,year=world.year,fee=fee,agentFee=agentFee,wage=wage});
                    // No hoarding: above its size, the buyer lists its weakest senior
                    // player of the same kind; a club processed later may buy him.
                    if(squad.Count>target){var spare=squad.Where(p=>p!=candidate&&p.Goalkeeper==candidate.Goalkeeper&&p.age>AiListedMinAge&&!protectedPlayers.Contains(p.id)&&!listed.Contains(p.id)).OrderBy(p=>p.rating).ThenBy(p=>p.id,StringComparer.Ordinal).FirstOrDefault();if(spare!=null)listed.Add(spare.id);}
                }
            }
            var nextPayrolls=ClubWeeklyPayrolls(db);
            foreach(var team in db.clubs){
                if(!eligible.Contains(team.id))continue;var a=accounts[team.id];bool managed=team.id==club&&world.managerStatus=="employed";
                long reserve=team.annualRevenue/2+committed[team.id];
                if(!managed){
                    long project=team.annualRevenue*3/100;
                    if(project>0&&a.cash-reserve>project&&(a.training<5||a.academy<5)){
                        string kind=a.academy<=a.training?"centre de formation":"entraînement";if(a.academy<=a.training)a.academy++;else a.training++;
                        AiEntry(a,-project,"Investissement IA · "+kind);a.investments+=project;
                        if(team.id==club){var facility=life.facilities.FirstOrDefault(f=>f.kind==(kind=="entraînement"?"training":"academy"));if(facility!=null)facility.level=Math.Max(facility.level,kind=="entraînement"?a.training:a.academy);}
                    }
                }
                long cashProfit=Math.Max(0,a.cash-a.openingCash-a.externalFunding),surplus=Math.Max(0,a.cash-reserve);
                float distribution=a.policy=="réinvestissement"?.25f:a.policy=="rendement"?.75f:.5f;
                long dividend=Math.Min(cashProfit,Math.Min((long)(surplus*distribution),team.annualRevenue*15/100));
                if(a.operatingDebt>0)dividend=0;
                if(dividend>0){AiEntry(a,-dividend,"Distribution au propriétaire · politique "+a.policy);a.dividends+=dividend;
                    if(team.id==club)Mail("Présidence","Affectation annuelle du résultat","Politique « "+a.policy+" » : "+dividend.ToString("N0")+" € distribués au propriétaire. Six mois de recettes, les dettes et les engagements déjà enregistrés sont préservés. Cette politique est simulée, pas celle du propriétaire réel.",null,"finance");}
                a.openingCash=a.cash;a.externalFunding=0;a.settledYear=world.year;a.projectedFromDay=life.day;
                CaptureAiOperatingSnapshot(team,a,nextPayrolls[team.id]);
            }
            if(world.aiTransfers.Count>3000)world.aiTransfers.RemoveRange(0,world.aiTransfers.Count-3000);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

namespace Touchline.Core
{
    public partial class Career
    {
        public int aiStaffReviewDay=-1;
        static readonly string[] AiStaffRoles={"assistant","fitness","scout","youth"};
        static long AiStaffWeeklyBudget(ClubData team)=>Math.Max(1500,team.annualRevenue/52/40);
        HashSet<string> ProtectedStaffNegotiations()=>new HashSet<string>((staffOffers??new List<StaffOffer>())
            .Where(o=>o.club==club&&o.due+7>=life.day&&(o.status=="pending"||o.status=="accepted"||o.status=="counter")).Select(o=>o.staff));

        void RenewAiStaffContracts(Database db)
        {
            var expired=staffMarket.Where(s=>s.club!=null&&s.club!=club&&s.until<=life.day).ToArray();
            if(expired.Length==0)return;
            var teams=db.clubs.Where(t=>t.playable).ToDictionary(t=>t.id);
            var protectedIds=ProtectedStaffNegotiations();
            var payrolls=staffMarket.Where(s=>s.club!=null).GroupBy(s=>s.club).ToDictionary(g=>g.Key,g=>g.Sum(s=>s.wage));
            var covered=new HashSet<string>(staffMarket.Where(s=>s.club!=null&&s.until>life.day).Select(s=>s.club+"/"+s.role));
            foreach(var member in expired.OrderBy(s=>s.id,StringComparer.Ordinal)){
                if(!teams.TryGetValue(member.club,out var team)||protectedIds.Contains(member.id)||member.wage<=0||!AiStaffRoles.Contains(member.role))continue;
                string slot=member.club+"/"+member.role;
                // Existing wages are honoured up to expiry. Renewal does not
                // silently cut an expensive signed salary to fit the budget.
                if(covered.Contains(slot)||payrolls[member.club]>AiStaffWeeklyBudget(team))continue;
                member.until=life.day+730;covered.Add(slot);
            }
        }

        void FillAiStaffVacancies(Database db)
        {
            if(aiStaffReviewDay>=0&&life.day-aiStaffReviewDay<30)return;
            aiStaffReviewDay=life.day;
            if(world?.aiAccounts==null)return;
            var protectedIds=ProtectedStaffNegotiations();
            var employed=staffMarket.Where(s=>s.club!=null).GroupBy(s=>s.club).ToDictionary(g=>g.Key,g=>g.ToList());
            var freeByRole=staffMarket.Where(s=>s.club==null&&!protectedIds.Contains(s.id)).GroupBy(s=>s.role).ToDictionary(g=>g.Key,g=>g.ToList());
            var accounts=world.aiAccounts.ToDictionary(a=>a.club);
            // Read each employment group once per monthly review. Staff wages
            // remain inside the existing operating-cost projection; only the
            // new one-off signing cost is posted here, avoiding double payroll.
            foreach(var team in db.clubs.Where(t=>t.playable&&t.id!=club).OrderBy(t=>t.id,StringComparer.Ordinal)){
                if(!accounts.TryGetValue(team.id,out var account))continue;
                if(!employed.TryGetValue(team.id,out var members)){members=new List<StaffMember>();employed[team.id]=members;}
                long budget=AiStaffWeeklyBudget(team),payroll=members.Sum(s=>s.wage);
                int level=(int)Mathx.Clamp(6+(float)Math.Log10(Math.Max(1000000,team.annualRevenue)/1000000.0)*4,6,18);
                foreach(string role in AiStaffRoles){
                    if(members.Any(s=>s.role==role))continue;
                    if(!freeByRole.TryGetValue(role,out var freeCandidates))continue;
                    long available=Math.Max(0,budget-payroll);
                    var candidate=freeCandidates.Where(s=>s.club==null&&s.wage>0&&s.wage<=available&&s.tactics<=level+4)
                        .OrderBy(s=>Math.Abs((role=="scout"?s.judging:role=="fitness"||role=="youth"?s.coaching:s.tactics)-level)).ThenBy(s=>s.wage).ThenBy(s=>s.id,StringComparer.Ordinal).FirstOrDefault();
                    if(candidate==null)continue;
                    long signing=candidate.wage*2;
                    if(account.cash-team.annualRevenue/20-AiCommitted(team.id)<signing)continue;
                    AiEntry(account,-signing,"Signature staff IA · "+candidate.name);
                    candidate.club=team.id;candidate.until=life.day+730;members.Add(candidate);payroll+=candidate.wage;
                }
            }
        }
    }
}

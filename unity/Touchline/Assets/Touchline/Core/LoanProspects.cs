using System;
using System.Collections.Generic;
using System.Linq;

namespace Touchline.Core
{
    // Runtime assessment; never a promise, verified wage quote or hidden-potential report.
    public sealed class LoanProspect
    {
        public ClubData club;
        public string role;
        public bool levelCompatible,affordable,calendarLoaded;
        public int competition,estimatedMinutesPerMatch;
        public long packageCost,budgetAllowance;
    }
    public partial class Career
    {
        static readonly string[] loanRoles={"GK","CB","LB","RB","DM","CM","AM","LM","RM","LW","RW","ST"};
        static string[] LoanRoles(PlayerData player)
        {
            if(player.Goalkeeper)return new[]{"GK"};
            var exact=(player.positions??Array.Empty<string>()).Where(r=>loanRoles.Contains(r)&&r!="GK").Distinct().ToArray();
            return exact.Length>0?exact:new[]{player.position=="DEF"?"CB":player.position=="ATT"?"ST":"CM"};
        }
        static bool CompetesForLoanRole(PlayerData player,string role)=>role=="GK"?player.Goalkeeper:!player.Goalkeeper&&player.Fit(role)>=.85f;
        static float LoanClubStrength(IEnumerable<PlayerData> squad)
        {
            var available=squad.Where(p=>p.unavailableDays<=0).OrderByDescending(p=>p.rating+p.development).Take(11).ToArray();
            return available.Length==0?30:(float)available.Average(p=>(p.rating+p.development)*(.85f+p.fitness*.0015f));
        }
        public bool LoanRecipientAllowed(Database db,string recipient)
        {
            var target=db.clubs.FirstOrDefault(c=>c.id==recipient);
            return target!=null&&target.id!=club&&!target.reserve&&db.Squad(recipient).Count>=11;
        }
        LoanProspect AssessLoan(PlayerData player,ClubData target,List<PlayerData> squad,MarketTerms terms,long fee,bool? calendarLoaded=null)
        {
            float strength=LoanClubStrength(squad);int best=-1,competition=0;string bestRole=null;
            foreach(var role in LoanRoles(player)){
                int seats=role=="CB"||role=="CM"?2:1;
                var rivals=squad.Where(p=>p.id!=player.id&&p.unavailableDays<=0&&CompetesForLoanRole(p,role)).ToArray();
                int ahead=rivals.Count(p=>p.rating+p.development>player.rating+player.development+1);
                int peers=rivals.Count(p=>Math.Abs(p.rating+p.development-player.rating-player.development)<=1);
                int minutes=ahead>=seats?20:ahead+peers>=seats*2?45:70;
                if(role=="GK")minutes=ahead>0?0:peers>0?45:70;
                if(player.rating<strength-4)minutes=Math.Min(minutes,20);
                if(player.unavailableDays>0||player.fitness<45)minutes=0;
                if(minutes>best){best=minutes;bestRole=role;competition=rivals.Length;}
            }
            bool valid=terms!=null&&fee>=0&&terms.loanWagePercent>=0&&terms.loanWagePercent<=100&&terms.obligationFee>=0;
            // Same package and future purchase allowance as OutgoingLoanDay.
            // Payroll uses integer weekly contributions, including their complement.
            decimal package=valid?(decimal)fee+(player.wage*terms.loanWagePercent/100)*Math.Max(4,(terms.loanEndDay-life.day)/7):decimal.MaxValue;
            return new LoanProspect{club=target,role=bestRole,competition=competition,estimatedMinutesPerMatch=Math.Max(0,best),levelCompatible=player.rating>=strength-5&&player.rating<=strength+15,affordable=valid&&package<=target.annualRevenue/20&&terms.obligationFee<=target.annualRevenue/10,packageCost=package>long.MaxValue?long.MaxValue:(long)package,budgetAllowance=target.annualRevenue/20,calendarLoaded=calendarLoaded??(world?.fixtures.Any(f=>(f.home==target.id||f.away==target.id)&&f.day>life.day&&f.day<=terms?.loanEndDay)==true)};
        }
        public LoanProspect LoanClubAssessment(Database db,string player,string recipient,MarketTerms terms,long fee)
        {
            var p=db.Find(player);var target=db.clubs.FirstOrDefault(c=>c.id==recipient);
            return p==null||target==null?null:AssessLoan(p,target,db.Squad(recipient),terms,fee);
        }
        public List<LoanProspect> LoanClubRecommendations(Database db,string player,MarketTerms terms,long fee=0)
        {
            var p=db.Find(player);if(p==null)return new List<LoanProspect>();
            // One database pass rather than one complete player scan per club.
            var squads=db.players.Where(q=>q!=null&&q.team!=null).GroupBy(q=>q.team).ToDictionary(g=>g.Key,g=>g.ToList());
            var scheduled=new HashSet<string>();foreach(var f in world?.fixtures??Enumerable.Empty<Fixture>())if(f.day>life.day&&f.day<=terms?.loanEndDay){scheduled.Add(f.home);scheduled.Add(f.away);}
            return db.clubs.Where(c=>c.id!=club&&!c.reserve&&squads.TryGetValue(c.id,out var squad)&&squad.Count>=11)
                .Select(c=>AssessLoan(p,c,squads[c.id],terms,fee,scheduled.Contains(c.id))).OrderByDescending(r=>r.levelCompatible&&r.affordable)
                .ThenByDescending(r=>r.calendarLoaded).ThenByDescending(r=>r.estimatedMinutesPerMatch).ThenBy(r=>r.competition)
                .ThenBy(r=>r.club.id,StringComparer.Ordinal).ToList();
        }
        void StartOutgoingLoanTracking(Employment contract,YouthPath path)
        {
            path.loanReviewedThrough=life.day;contract.appearancesAtSigning=path.loanAppearances;
            contract.loanAppearanceTracking=true;contract.loanAppearanceHistoryEstimated=false;
        }
        public void ReviewOutgoingLoanAppearances(Database db)
        {
            var ownedLoans=world.contracts.Where(c=>c.parent==club&&c.player!=null).GroupBy(c=>c.player).ToDictionary(g=>g.Key,g=>g.First());
            foreach(var path in world.youth){
                if(path.group!="loan"||path.player==null||!ownedLoans.TryGetValue(path.player,out var c))continue;var p=db.Find(path.player);
                if(p==null||p.team!=c.club||p.team=="retired"||c.retirement>=0&&c.retirement<=life.day)continue;
                if(!c.loanAppearanceTracking){
                    // Old minutes never establish an appearance clause. Resume from
                    // a documented baseline; do not manufacture historical starts.
                    StartOutgoingLoanTracking(c,path);c.loanAppearanceHistoryEstimated=true;continue;
                }
                int from=path.loanReviewedThrough;if(from>=life.day)continue;
                var games=world.fixtures.Where(f=>f.played&&f.day>from&&f.day<=life.day&&f.day<=c.loanUntil&&(f.home==p.team||f.away==p.team)).GroupBy(f=>f.id??f.league+"/"+f.day+"/"+f.home+"/"+f.away).Select(g=>g.First()).ToArray();
                path.loanReviewedThrough=life.day;
                var assessment=LoanClubAssessment(db,p.id,p.team,c.terms??new MarketTerms{loanEndDay=c.loanUntil},0);
                int perMatch=assessment?.estimatedMinutesPerMatch??0;
                // Keepers rotate whole matches rather than receive imaginary
                // 20-minute substitutions. This remains a deterministic proxy.
                int appearances=p.Goalkeeper?games.Count(f=>StableIdentity(p.id+"/"+f.id+"/"+f.day)%90<(uint)perMatch):perMatch>0?games.Length:0;
                int minutes=p.Goalkeeper?appearances*90:appearances*perMatch;path.minutes+=minutes;path.trackedLoanMinutes+=minutes;path.progress+=minutes/90f*.022f;
                path.loanAppearances+=appearances;
                if(minutes>0&&path.progress>=1&&p.rating<p.potential){path.progress-=1;float gain=Math.Min(1,p.potential-p.rating);p.rating+=gain;foreach(var a in p.attributes??Array.Empty<AttributeValue>())a.value=Math.Min(99,a.value+gain);SavePlayer(p);}
            }
        }
    }
}

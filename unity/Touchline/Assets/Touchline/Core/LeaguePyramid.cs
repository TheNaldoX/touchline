using System;
using System.Collections.Generic;
using System.Linq;

namespace Touchline.Core
{
    [Serializable] public class PyramidRule { public string upper,lower,mode,description,source;public int direct,down,legs=1,finalLegs=2,gap;public bool rankTie,provisionalRules;public int[] entrants; }
    [Serializable] public class PromotionPair { public string upper,lower,defender,phase;public bool complete;public List<string> ranking=new List<string>(),promoted=new List<string>(),relegated=new List<string>();public List<PlayoffTie> ties=new List<PlayoffTie>(); }
    [Serializable] public class PlayoffTie { public string id,a,b,winner;public int stage;public bool rankTie; }
    [Serializable] public class SplitDeduction {public string club;public int points;}
    [Serializable] public class SplitGroup { public string league;public List<string> top=new List<string>(),middle=new List<string>();public List<SplitDeduction> deductions=new List<SplitDeduction>(); }
    [Serializable] public class RevenueChange { public string club;public long revenue,baseRevenue;public int divisionMoves;public float merit; }
    public partial class Career
    {
        public List<RevenueChange> changedRevenues=new List<RevenueChange>();
        void ChangeDivisionRevenue(Database db,string id,bool promoted)
        {
            var data=db.clubs.First(c=>c.id==id);var change=changedRevenues.FirstOrDefault(r=>r.club==id);
            if(change==null){change=new RevenueChange{club=id,baseRevenue=data.annualRevenue};changedRevenues.Add(change);}
            if(change.baseRevenue<=0){change.baseRevenue=data.annualRevenue;change.divisionMoves=0;}
            change.divisionMoves+=promoted?1:-1;
            // Reversible projection: a promotion/relegation cycle cannot manufacture revenue.
            data.annualRevenue=ProjectedRevenue(change);change.revenue=data.annualRevenue;
            data.financeSource="Projection de carrière par division, ancrée aux recettes initiales";
            if(id==club){life.revenue=data.annualRevenue;life.financeSource=data.financeSource;Mail("Direction financière","Changement de division","Les recettes prévisionnelles et les plafonds du conseil sont réévalués après "+(promoted?"la montée.":"la descente."));}
        }
        // Spread of next-season revenue between the champion and the last club of
        // a division (TV merit, prize money, gates): +15 % / -15 % around the base.
        // Applied to computer-run clubs only; the managed club keeps its own ledger.
        const float MeritRevenueRange=.30f;
        static long ProjectedRevenue(RevenueChange change)=>(long)(change.baseRevenue*Math.Pow(1.55,change.divisionMoves)*(change.merit>0?change.merit:1));
        // d.clubs is in final table order when called.
        void ApplyMeritRevenue(Database db)
        {
            foreach(var d in world.divisions){
                int n=d.clubs.Count;if(n<2)continue;
                for(int i=0;i<n;i++){
                    string id=d.clubs[i];if(id==club&&world.managerStatus=="employed")continue;
                    var data=db.clubs.FirstOrDefault(c=>c.id==id);if(data==null||data.annualRevenue<=0)continue;
                    var change=changedRevenues.FirstOrDefault(r=>r.club==id);
                    if(change==null){change=new RevenueChange{club=id,baseRevenue=data.annualRevenue};changedRevenues.Add(change);}
                    if(change.baseRevenue<=0){change.baseRevenue=data.annualRevenue;change.divisionMoves=0;}
                    change.merit=1+MeritRevenueRange*(.5f-i/(float)(n-1));
                    data.annualRevenue=ProjectedRevenue(change);change.revenue=data.annualRevenue;
                }
            }
        }
        bool LeagueFinished(string id)=>world.fixtures.Any(f=>f.league==id)&&world.fixtures.Where(f=>f.league==id&&!f.knockout).All(f=>f.played);
        void ProgressPyramid(Database db)
        {
            foreach(var league in (db.leagues??Array.Empty<LeagueData>()).Where(l=>l.format?.StartsWith("split")==true)){
                if(world.splits.Any(s=>s.league==league.id)||!LeagueFinished(league.id))continue;var table=Table(league.id);
                bool greek=league.format=="splitGreek";if(table.Count!=(greek?14:12))continue;
                var split=new SplitGroup{league=league.id,top=table.Take(greek?4:6).Select(t=>t.club).ToList()};
                if(greek){split.middle=table.Skip(4).Take(4).Select(t=>t.club).ToList();split.deductions=table.Skip(4).Take(4).Select(t=>new SplitDeduction{club=t.club,points=t.points-(t.points+1)/2}).ToList();}
                world.splits.Add(split);int start=world.fixtures.Where(f=>f.league==league.id).Max(f=>f.day)+7;
                var groups=new List<List<string>>{split.top};if(greek)groups.Add(split.middle);groups.Add(table.Skip(greek?8:6).Select(t=>t.club).ToList());
                foreach(var group in groups){var ring=group.ToList();int n=ring.Count,rounds=n-1;for(int r=0;r<rounds;r++){for(int i=0;i<n/2;i++){string a=ring[i],b=ring[n-1-i];if(r%2==1){var swap=a;a=b;b=swap;}AddFixture(league.id,a,b,start+r*7);if(league.format!="split33")AddFixture(league.id,b,a,start+(r+rounds)*7);}var last=ring[n-1];ring.RemoveAt(n-1);ring.Insert(1,last);}}
            }
            foreach(var rule in db.pyramidRules??Array.Empty<PyramidRule>()){
                var pair=world.promotionPairs.FirstOrDefault(p=>p.upper==rule.upper);
                if(pair==null){if(!LeagueFinished(rule.upper)||!LeagueFinished(rule.lower))continue;var upper=Table(rule.upper);var lower=Table(rule.lower).Where(t=>!db.clubs.First(c=>c.id==t.club).reserve).ToList();if(lower.Count<8||upper.Count<8)continue;
                    int down=rule.down>0?rule.down:rule.direct;pair=new PromotionPair{upper=rule.upper,lower=rule.lower,ranking=lower.Select(t=>t.club).ToList(),promoted=lower.Take(rule.direct).Select(t=>t.club).ToList(),relegated=upper.TakeLast(down).Select(t=>t.club).ToList(),defender=rule.down>0?null:upper[upper.Count-down-1].club};world.promotionPairs.Add(pair);var rank=pair.ranking;
                    if(rule.gap>0&&lower[2].points-lower[3].points>rule.gap){pair.promoted.Add(rank[2]);pair.complete=true;continue;}
                    if(rule.mode=="duel"){AddPlayoff(pair,pair.defender,rank[2],0,2,false);pair.phase="final";}
                    else if(rule.mode=="ladder"){AddPlayoff(pair,rank[rule.entrants[1]-1],rank[rule.entrants[2]-1],0,rule.legs,false);pair.phase="first";}
                    else if(rule.mode=="four"){AddPlayoff(pair,rank[rule.direct],rank[rule.direct+3],0,2,rule.rankTie);AddPlayoff(pair,rank[rule.direct+1],rank[rule.direct+2],0,2,rule.rankTie);pair.phase="semi";}
                    else if(rule.mode=="six"){AddPlayoff(pair,rank[4],rank[7],0,1,rule.rankTie);AddPlayoff(pair,rank[5],rank[6],0,1,rule.rankTie);pair.phase="preliminary";}
                    else{var qualified=rank.Skip(2).Take(6).ToList();for(int i=0;i<3;i++)AddPlayoff(pair,qualified[i],qualified[5-i],0,2,false);pair.phase="dutch-first";}
                    Mail("Secrétariat sportif","Barrages programmés",CompetitionName(db,rule.lower)+" : les rencontres d’accession et de maintien sont ajoutées au calendrier.");
                }
                if(pair.complete)continue;foreach(var tie in pair.ties.Where(t=>t.winner==null)){var fixtures=world.fixtures.Where(f=>f.tie==tie.id).ToArray();if(fixtures.Any(f=>!f.played))continue;int a=fixtures.Sum(f=>f.home==tie.a?f.hg:f.ag),b=fixtures.Sum(f=>f.home==tie.b?f.hg:f.ag);tie.winner=a==b&&tie.rankTie?(pair.ranking.IndexOf(tie.a)<pair.ranking.IndexOf(tie.b)?tie.a:tie.b):fixtures.Last().winner;}
                if(pair.ties.Any(t=>t.winner==null))continue;var last=pair.ties.Last();int stage=last.stage+1;
                if(pair.phase=="first"){AddPlayoff(pair,pair.ranking[rule.entrants[0]-1],last.winner,stage,rule.legs,false);pair.phase="second";}
                else if(pair.phase=="second"){AddPlayoff(pair,pair.defender,last.winner,stage,2,false);pair.phase="final";}
                else if(pair.phase=="preliminary"){AddPlayoff(pair,pair.ranking[2],pair.ties[1].winner,stage,2,rule.rankTie);AddPlayoff(pair,pair.ranking[3],pair.ties[0].winner,stage,2,rule.rankTie);pair.phase="semi";}
                else if(pair.phase=="dutch-first"){AddPlayoff(pair,pair.ties[0].winner,pair.ties[1].winner,stage,2,false);AddPlayoff(pair,pair.defender,pair.ties[2].winner,stage,2,false);pair.phase="semi";}
                else if(pair.phase=="semi"){var two=pair.ties.TakeLast(2).Select(t=>t.winner).ToArray();AddPlayoff(pair,two[0],two[1],stage,rule.finalLegs>0?rule.finalLegs:2,rule.rankTie);pair.phase="final";}
                else{if(last.winner!=pair.defender){pair.promoted.Add(last.winner);if(pair.defender!=null)pair.relegated.Add(pair.defender);}pair.complete=true;}
            }
        }
        void AddPlayoff(PromotionPair pair,string a,string b,int stage,int legs,bool rankTie)
        {
            string tie="playoff-"+world.year+"-"+pair.upper+"-"+pair.ties.Count;pair.ties.Add(new PlayoffTie{id=tie,a=a,b=b,stage=stage,rankTie=rankTie});int day=life.day+4;
            var f=AddFixture("playoff "+pair.lower,a,b,day,stage,true,legs==2?1:0,tie);if(pair.phase=="semi"&&legs==1&&pair.upper.StartsWith("eng")){f.neutral=true;f.venue="Wembley Stadium";}
            if(legs==2)AddFixture("playoff "+pair.lower,b,a,day+5,stage,true,2,tie);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

namespace Touchline.Core
{
    [Serializable] public class Fixture
    {
        public string id,league,home,away,date,venue,source,winner,tie;
        public int day,round,hg,ag,penHome,penAway,leg;
        public bool published,played,knockout,neutral,fixedWindow;
    }
    [Serializable] public class Standing { public string club;public int played,won,draw,lost,gf,ga,points; }
    [Serializable] public class Division { public string id;public List<string> clubs=new List<string>(); }
    [Serializable] public class Honour { public int year;public string club,competition; }
    [Serializable] public class Tournament { public string id;public int stage;public bool european,finished;public List<string> entrants=new List<string>(),seeds=new List<string>(); }
    [Serializable] public class CareerWorld
    {
        public int version=1,year=2026,seasonEnd=333,serial,reviewDay=30,jobDay=-1,lastIntake=-1;
        public string activeFixture,managerStatus="employed",trainingFocus="balanced",owner="Propriétaire actuel";
        public bool enableEurope=true;public List<Division> divisions=new List<Division>();
        public List<Fixture> fixtures=new List<Fixture>();public List<Tournament> cups=new List<Tournament>();
        public List<Honour> honours=new List<Honour>();public List<Fixture> history=new List<Fixture>();
        public List<PromotionPair> promotionPairs=new List<PromotionPair>();public List<SplitGroup> splits=new List<SplitGroup>();
        public List<PlayerData> rosterChanges=new List<PlayerData>();
        public List<ClubDevelopmentReference> developmentReferences=new List<ClubDevelopmentReference>();
        public List<AiClubAccount> aiAccounts=new List<AiClubAccount>();public List<AiTransferRecord> aiTransfers=new List<AiTransferRecord>();public bool initialAiEmployment;
        public List<Employment> contracts=new List<Employment>();public List<ScoutReport> reports=new List<ScoutReport>();
        public List<ScoutMission> scoutMissions=new List<ScoutMission>();
        public List<TransferOffer> offers=new List<TransferOffer>();public List<YouthPath> youth=new List<YouthPath>();
        public List<CommercialDeal> sponsors=new List<CommercialDeal>();public List<PressAppearance> press=new List<PressAppearance>();
        public int ticket=30,capacity=25000,lastTicketDay=-7;public long debt,transferSpent;public float supporterTrust=70;
        // Compact save (CompactSave.cs). Filled only while a save is written
        // and emptied again when it is loaded: rosterChanges, contracts and
        // fixtures are the live lists. compactFormat 0 = former, complete format.
        public int compactFormat;public List<string> compactPlayerSchema=new List<string>(),compactContractSchema=new List<string>(),compactFixtureSchema=new List<string>();
        public List<string> compactRoster=new List<string>(),compactContracts=new List<string>(),compactFixtures=new List<string>(),compactStrings=new List<string>(),compactAttributeKeys=new List<string>();
        public List<PlayerData> compactFullPlayers=new List<PlayerData>();public List<Employment> compactFullContracts=new List<Employment>();public List<Fixture> compactFullFixtures=new List<Fixture>();
    }
    public partial class Career
    {
        public CareerWorld world;
        public void RestoreWorld(Database db)
        {
            if(world==null)return;
            var changed=(world.rosterChanges??new List<PlayerData>()).ToDictionary(p=>p.id);
            // Older save schemas omitted immutable birth-date evidence. Recover
            // only that identity field by stable ID, preserving career values.
            foreach(var original in db.players)
                if(changed.TryGetValue(original.id,out var saved)&&string.IsNullOrEmpty(saved.birthDate)&&string.IsNullOrEmpty(saved.evidence?.birthDate)&&PlayerChronology.TryBirthDate(original,Date,out var birth)){
                    var copy=saved.Copy();copy.birthDate=birth.ToString("yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture);changed[original.id]=copy;
                }
            // Keep the database's stable roster order. Moving every edited
            // player to the end changes tie-breaks and seeded newgen name
            // pools after a save/reload despite an identical career RNG state.
            var originalIds=new HashSet<string>(db.players.Select(p=>p.id));
            db.players=db.players.Select(p=>changed.TryGetValue(p.id,out var saved)?saved:p)
                .Concat(changed.Values.Where(p=>!originalIds.Contains(p.id))).ToArray();
            foreach(var d in world.divisions)foreach(var id in d.clubs){var c=db.clubs.FirstOrDefault(x=>x.id==id);if(c!=null)c.league=d.id;}
            foreach(var r in changedRevenues??new List<RevenueChange>()){var c=db.clubs.FirstOrDefault(x=>x.id==r.club);if(c!=null){c.annualRevenue=r.revenue;c.financeSource=r.baseRevenue>0?"Projection de carrière par division, ancrée aux recettes initiales":"Projection de carrière après changement de division";}}
        }
        public void EnsureWorld(Database db)
        {
            EnsureLife(db);if(world!=null&&world.divisions!=null&&world.divisions.Count>0){ImportFreeAgents(db);EnsureStaffMarket(db);EnsureAiClubAccounts(db);return;}
            world=new CareerWorld();world.year=Date.Month>=6?Date.Year:Date.Year-1;
            foreach(var league in db.leagues??Array.Empty<LeagueData>()){
                if(league.scoutingOnly)continue;
                var ids=db.clubs.Where(c=>c.league==league.id&&db.Squad(c.id).Count>=11).Select(c=>c.id).ToList();
                if(ids.Count>1)world.divisions.Add(new Division{id=league.id,clubs=ids});
            }
            if(world.divisions.Count==0)world.divisions.Add(new Division{id="local",clubs=db.clubs.Select(c=>c.id).ToList()});
            world.capacity=(int)Mathx.Clamp((float)Math.Sqrt(life.revenue)*3,4000,85000);world.ticket=(int)Mathx.Clamp((float)Math.Sqrt(life.revenue)/400,8,90);
            GenerateSeason(db,true);InitializeEmployment(db);EnsureAiClubAccounts(db);CreateIntake(db);
            Mail("Secrétariat sportif","La carrière est ouverte","Le calendrier relie désormais les résultats, les classements et la saison suivante. Les rencontres importées conservent leur source ; les autres affiches et tous les résultats de départ sont simulés.");
            foreach(var f in world.fixtures.Where(f=>!f.played&&f.day<life.day).OrderBy(f=>f.day))SimulateFixture(db,f);
            WorldDay(db);
        }
        int DayOf(DateTime d)=>(int)(d-Touchline.Core.Career.Epoch).TotalDays;
        public string CompetitionName(Database db,string id)=>db.leagues?.FirstOrDefault(l=>l.id==id)?.name??(id=="ucl"?"Ligue des champions":id=="uel"?"Europa League":id=="uecl"?"Conference League":id=="friendly"?"Match amical":id.StartsWith("cup-")?DomesticCupName(id.Substring(4)):id);
        public Fixture NextFixture()=>world?.fixtures.Where(f=>!f.played&&(f.home==club||f.away==club)).OrderBy(f=>f.day).ThenBy(f=>f.id,StringComparer.Ordinal).FirstOrDefault();
        public List<Standing> Table(string competition)
        {
            var ids=world.divisions.FirstOrDefault(d=>d.id==competition)?.clubs??world.cups.FirstOrDefault(c=>c.id==competition)?.entrants??new List<string>();
            var rows=ids.Distinct().ToDictionary(id=>id,id=>new Standing{club=id});
            foreach(var f in world.fixtures.Where(f=>f.league==competition&&f.played&&!f.knockout)){
                if(!rows.ContainsKey(f.home)||!rows.ContainsKey(f.away))continue;
                UpdateStanding(rows[f.home],f.hg,f.ag);UpdateStanding(rows[f.away],f.ag,f.hg);
            }
            var split=world.splits?.FirstOrDefault(x=>x.league==competition);if(split?.deductions!=null)foreach(var deduction in split.deductions)if(rows.ContainsKey(deduction.club))rows[deduction.club].points-=deduction.points;return rows.Values.OrderBy(t=>split==null||split.top.Contains(t.club)?0:split.middle==null||split.middle.Count==0||split.middle.Contains(t.club)?1:2).ThenByDescending(t=>t.points).ThenByDescending(t=>t.gf-t.ga).ThenByDescending(t=>t.gf).ThenBy(t=>t.club,StringComparer.Ordinal).ToList();
        }
        static void UpdateStanding(Standing t,int gf,int ga){t.played++;t.gf+=gf;t.ga+=ga;t.won+=gf>ga?1:0;t.draw+=gf==ga?1:0;t.lost+=gf<ga?1:0;t.points+=gf>ga?3:gf==ga?1:0;}
        Fixture AddFixture(string competition,string h,string a,int day,int round=0,bool knockout=false,int leg=0,string tie=null)
        {
            var f=new Fixture{id="w"+world.year+"-"+(++world.serial),league=competition,home=h,away=a,day=day,round=round,knockout=knockout,leg=leg,tie=tie,source="Calendrier généré"};world.fixtures.Add(f);return f;
        }
        void GenerateSeason(Database db,bool initial)
        {
            world.fixtures.Clear();world.cups.Clear();world.promotionPairs.Clear();world.splits.Clear();world.seasonEnd=DayOf(new DateTime(world.year+1,6,15));
            foreach(var division in world.divisions){
                var imported=initial&&world.year==2026?(db.fixtures??Array.Empty<Fixture>()).Where(f=>f.league==division.id&&division.clubs.Contains(f.home)&&division.clubs.Contains(f.away)).ToArray():Array.Empty<Fixture>();
                if(imported.Length>0){foreach(var x in imported){var f=AddFixture(division.id,x.home,x.away,DayOf(DateTime.Parse(x.date,System.Globalization.CultureInfo.InvariantCulture).Date));f.published=x.published;f.date=x.date;f.venue=x.venue;f.source=x.source??"Base locale ESPN";}}
                else ScheduleLeague(division,db.leagues?.FirstOrDefault(l=>l.id==division.id)?.format);
            }
            foreach(var group in world.divisions.GroupBy(d=>d.id.Split('.')[0])){
                var ids=group.SelectMany(d=>d.clubs).Where(id=>!db.clubs.First(c=>c.id==id).reserve).ToList();if(ids.Count<2)continue;
                var cup=new Tournament{id="cup-"+group.Key,entrants=ids.ToList()};world.cups.Add(cup);ScheduleCup(cup,ids,DayOf(new DateTime(world.year,10,6)),false);
            }
            if(world.enableEurope&&world.divisions.Sum(d=>d.clubs.Count)>=108)ScheduleEurope(db);
            ResolveCalendar();
        }
        void ScheduleLeague(Division d,string format)
        {
            var ring=d.clubs.ToList();if(ring.Count%2!=0)ring.Add(null);int n=ring.Count;
            var rounds=new List<List<string[]>>();for(int r=0;r<n-1;r++){
                var pairs=new List<string[]>();for(int i=0;i<n/2;i++){var a=ring[i];var b=ring[n-1-i];if(a!=null&&b!=null)pairs.Add((r+i)%2==0?new[]{a,b}:new[]{b,a});}rounds.Add(pairs);var last=ring[n-1];ring.RemoveAt(n-1);ring.Insert(1,last);
            }
            int legs=format=="quadruple"?4:format=="split33"?3:2,total=(n-1)*legs;
            int span=(format=="split22"||format=="splitGreek")?215:format=="split33"?242:280;
            for(int l=0;l<legs;l++)for(int r=0;r<rounds.Count;r++)foreach(var pair in rounds[r]){int round=l*(n-1)+r;int day=DayOf(new DateTime(world.year,8,1))+(int)Math.Round(round*(double)span/Math.Max(1,total-1));AddFixture(d.id,pair[l%2],pair[1-l%2],day,round);}
        }
        void ScheduleEurope(Database db)
        {
            if(world.year==2026){foreach(var name in new[]{"ucl","uel","uecl"}){
                var imported=(db.fixtures??Array.Empty<Fixture>()).Where(f=>f.league==name).ToArray();if(imported.Length==0)continue;
                var ids=imported.SelectMany(f=>new[]{f.home,f.away}).Distinct().ToList();
                if(ids.Any(id=>db.Squad(id).Count<11))throw new InvalidOperationException("Effectif européen incomplet : "+name);
                world.cups.Add(new Tournament{id=name,european=true,entrants=ids,stage=-1});
                foreach(var x in imported){var f=AddFixture(name,x.home,x.away,DayOf(DateTime.Parse(x.date,System.Globalization.CultureInfo.InvariantCulture).Date));f.date=x.date;f.published=true;f.source=x.source;f.venue=x.venue;}
            }return;}
            // Qualification is derived from the simulated domestic hierarchy, not an official access list.
            var rankings=world.divisions.Where(d=>!d.id.EndsWith(".2")&&!d.id.EndsWith(".3")&&!d.id.EndsWith(".4")).SelectMany(d=>d.clubs.Select((id,i)=>new{id,rank=i})).OrderBy(x=>x.rank).ThenByDescending(x=>Strength(db,x.id)).Select(x=>x.id).Distinct().ToList();
            foreach(var id in world.divisions.SelectMany(d=>d.clubs))if(!rankings.Contains(id))rankings.Add(id);
            for(int c=0;c<3;c++){
                string name=new[]{"ucl","uel","uecl"}[c];var ids=rankings.Skip(c*36).Take(36).ToList();if(ids.Count<36)continue;
                var cup=new Tournament{id=name,european=true,entrants=ids,stage=-1};world.cups.Add(cup);
                int count=c==2?6:8;for(int offset=1;offset<=count/2;offset++)for(int i=0;i<36;i++){
                    int day=DayOf(new DateTime(world.year,9,15))+((offset-1)*2+(i%2))*18+c;
                    AddFixture(name,ids[i],ids[(i+offset*2-1)%36],day,offset);
                }
            }
        }
        void ScheduleCup(Tournament cup,List<string> ids,int day,bool twoLegs)
        {
            cup.seeds.Clear();if(!cup.european&&ids.Count>1){ids=ids.ToList();for(int j=ids.Count-1;j>0;j--){int k=(int)(Roll()*(j+1));var tmp=ids[j];ids[j]=ids[k];ids[k]=tmp;}int rounds=(int)Math.Ceiling(Math.Log(cup.entrants.Count,2));int first=DayOf(new DateTime(world.year,10,6)),last=DayOf(new DateTime(world.year+1,5,20));day=Math.Max(day,first+(last-first)*cup.stage/Math.Max(1,rounds-1));}if(ids.Count==1){FinishCup(cup,ids[0]);return;}
            if(cup.european&&world.year==2026){int stage=Math.Min(4,Math.Max(0,cup.stage));int[] months={2,3,4,4,6};int[] days=cup.id=="ucl"?new[]{17,10,7,28,5}:new[]{18,11,8,29,cup.id=="uecl"?2:26};if(cup.id=="uel"&&stage==4)months[4]=5;day=DayOf(new DateTime(2027,months[stage],days[stage]));}
            // Byes reduce non-power-of-two fields before the next complete round.
            int power=1;while(power*2<ids.Count)power*=2;int games=ids.Count==power*2?ids.Count/2:ids.Count-power;
            for(int i=0;i<games;i++){
                var h=ids[i];var a=ids[ids.Count-1-i];string tie=cup.id+"-"+cup.stage+"-"+i;
                var f=AddFixture(cup.id,h,a,day,cup.stage,true,twoLegs?1:0,tie);f.neutral=ids.Count==2;f.fixedWindow=cup.european&&world.year==2026;if(f.fixedWindow)f.source="Créneau UEFA 2026/27 · affiche issue de votre carrière";
                if(f.neutral&&f.fixedWindow)f.venue=cup.id=="ucl"?"Estadio Metropolitano, Madrid":cup.id=="uel"?"Stadion Frankfurt":"Beşiktaş Park, Istanbul";
                if(twoLegs){var second=AddFixture(cup.id,a,h,day+7,cup.stage,true,2,tie);second.fixedWindow=f.fixedWindow;second.source=f.source;}
            }
            cup.seeds.AddRange(ids.Skip(games).Take(ids.Count-games*2));
        }
        void FinishCup(Tournament cup,string winner){cup.finished=true;world.honours.Add(new Honour{year=world.year,club=winner,competition=cup.id});if(winner==club){Account(life.revenue/100,"Prime de titre • "+cup.id);Mail("Présidence","Un trophée pour le club","Félicitations pour ce titre. Il rejoint le palmarès de votre carrière.");}}
        void ProgressCups()
        {
            foreach(var cup in world.cups.Where(c=>!c.finished).ToArray()){
                if(world.fixtures.Any(f=>f.league==cup.id&&!f.played))continue;
                var all=world.fixtures.Where(f=>f.league==cup.id).ToList();int date=Math.Max(life.day+4,all.Count>0?all.Max(f=>f.day)+14:life.day+4);
                if(cup.european&&cup.stage==-1){var table=Table(cup.id);cup.seeds=table.Take(8).Select(t=>t.club).ToList();var seeds=cup.seeds.ToList();cup.stage=0;ScheduleCup(cup,table.Skip(8).Take(16).Select(t=>t.club).ToList(),Math.Max(date,DayOf(new DateTime(world.year+1,2,10))),true);cup.seeds.AddRange(seeds);}
                else{var winners=all.Where(f=>f.round==cup.stage&&f.winner!=null).Select(f=>f.winner).Concat(cup.seeds).Distinct().ToList();cup.stage++;if(winners.Count==0){cup.finished=true;continue;}ScheduleCup(cup,winners,date,cup.european&&winners.Count>2);}
            }
        }
        void ResolveCalendar()
        {
            var booked=new Dictionary<string,List<int>>();foreach(var f in world.fixtures.Where(f=>f.played)){foreach(var id in new[]{f.home,f.away}){if(!booked.ContainsKey(id))booked[id]=new List<int>();booked[id].Add(f.day);}}
            // Imported dates are immutable. Only simulated cups/future seasons can be rearranged.
            foreach(var f in world.fixtures.Where(f=>!f.played).OrderByDescending(f=>f.published||f.fixedWindow).ThenBy(f=>f.league=="friendly"?1:0).ThenBy(f=>f.day)){
                foreach(var id in new[]{f.home,f.away})if(!booked.ContainsKey(id))booked[id]=new List<int>();
                int old=f.day;if(!f.published&&!f.fixedWindow)while(booked[f.home].Any(d=>Math.Abs(d-f.day)<3)||booked[f.away].Any(d=>Math.Abs(d-f.day)<3))f.day++;
                if(old!=f.day){f.published=false;f.source="Date réaménagée par le jeu";SyncFriendlyDate(f,old);}booked[f.home].Add(f.day);booked[f.away].Add(f.day);
            }
        }
        // Set only while a day's fixtures are simulated: one grouping of the
        // ~20 000 players instead of one full scan per club and fixture.
        [NonSerialized] Dictionary<string,List<PlayerData>> fixtureDaySquads;
        float Strength(Database db,string id,int rested=0){var players=(fixtureDaySquads!=null?(fixtureDaySquads.TryGetValue(id,out var squad)?squad:new List<PlayerData>()):db.Squad(id)).Where(p=>p.unavailableDays<=0).OrderByDescending(p=>p.rating+p.development).Skip(rested).Take(11).ToArray();return players.Length==0?30:(float)players.Average(p=>(p.rating+p.development)*(.85f+p.fitness*.0015f));}
        int Poisson(float mean){float limit=(float)Math.Exp(-mean),p=1;int n=0;do{n++;p*=Roll();}while(p>limit&&n<12);return n-1;}
        // A computer-run side at least this much stronger (average rating of the
        // XI, 0–100) rests this many of its best players in a domestic cup tie,
        // except in the final (neutral venue).
        const float CupRotationMargin=4f;const int CupRestedStarters=3;
        public static int CupRestedPlayers(Fixture f,float own,float opponent)=>f!=null&&f.league!=null&&f.league.StartsWith("cup-")&&!f.neutral&&own-opponent>=CupRotationMargin?CupRestedStarters:0;
        public void SimulateFixture(Database db,Fixture f)
        {
            if(f.played)return;float home=Strength(db,f.home),away=Strength(db,f.away);
            // Domestic cup rotation: the clearly stronger side rests its best players.
            int homeRested=CupRestedPlayers(f,home,away),awayRested=CupRestedPlayers(f,away,home);
            if(homeRested>0)home=Strength(db,f.home,homeRested);if(awayRested>0)away=Strength(db,f.away,awayRested);
            float diff=(home-away)/22;
            CommitFixture(f,Poisson(Mathx.Clamp(1.45f+diff,.25f,3.4f)),Poisson(Mathx.Clamp(1.1f-diff,.25f,3.1f)));
        }
        public void CommitFixture(Fixture f,int home,int away)
        {
            if(f.played)throw new InvalidOperationException("Résultat déjà enregistré.");if(home<0||away<0)throw new ArgumentException("Score invalide.");
            f.hg=home;f.ag=away;f.played=true;
            if(f.knockout&&f.leg!=1){int h=home,a=away;var first=world.fixtures.FirstOrDefault(x=>x.tie==f.tie&&x.leg==1);if(first!=null){h+=first.ag;a+=first.hg;}
                if(h==a){f.penHome=3+(int)(Roll()*3);f.penAway=f.penHome+(Roll()<.5f?-1:1);f.winner=f.penHome>f.penAway?f.home:f.away;}else f.winner=h>a?f.home:f.away;
            }
        }
        void WorldDay(Database db)
        {
            if(world==null)return;
            var due=world.fixtures.Where(f=>!f.played&&f.day<=life.day&&(world.managerStatus!="employed"||f.home!=club&&f.away!=club)).OrderBy(f=>f.day).ToArray();
            if(due.Length>0)fixtureDaySquads=db.players.GroupBy(p=>p.team??"").ToDictionary(g=>g.Key,g=>g.ToList());
            try{foreach(var f in due){SimulateFixture(db,f);if(f.home==club)Account(GateIncome(),"Billetterie • match dirigé par le successeur");}}
            finally{fixtureDaySquads=null;}
            ProgressCups();ProgressPyramid(db);PreseasonDay(db);ResolveCalendar();ManagementDay(db);
            if(life.day>=world.seasonEnd&&world.fixtures.Where(f=>f.league!="friendly"||f.day<=life.day).All(f=>f.played)&&world.cups.All(c=>c.finished))NewSeason(db);
            var next=NextFixture();life.nextFixture=world.managerStatus=="employed"&&next!=null?next.day:int.MaxValue;
        }
        void RecordWorldMatch(Database db)
        {
            ProcessMedicalEvents(db);
            if(world==null)return;var f=world.fixtures.FirstOrDefault(x=>x.id==world.activeFixture);
            if(f==null||f.played)return;bool home=f.home==club;CommitFixture(f,match.score[home?0:1],match.score[home?1:0]);
            if(home||f.neutral)Account(GateIncome(f.neutral?.5f:1),"Billetterie • "+f.league);
            foreach(var e in match.events.Where(e=>e.kind=="red"&&e.side==0))if(life.players.Any(p=>p.id==e.player)){Person(e.player).banUntil=NextFixture()?.day+1??life.day+8;Mail("Secrétariat sportif","Suspension après exclusion",db.Find(e.player).name+" manquera la prochaine rencontre, toutes compétitions confondues dans cette simulation.",e.player);}
            foreach(var contract in world.contracts.Where(c=>c.club==club&&match.used.Contains(c.player)))Account(-contract.appearanceBonus,"Prime de présence");
            Mail("Attaché de presse","Conférence d’après-match",life.lastResult+". La salle de presse vous attend.",null,"press");
            world.activeFixture=null;WorldDay(db);
        }
        void NewSeason(Database db)
        {
            foreach(var d in world.divisions){var table=Table(d.id);if(table.Count>0){world.honours.Add(new Honour{year=world.year,club=table[0].club,competition=d.id});d.clubs=table.Select(t=>t.club).ToList();}}
            ApplyMeritRevenue(db);
            // Direct exchanges between loaded adjacent tiers. Association-specific play-offs are documented separately.
            var exchanges=new List<Tuple<Division,Division,List<string>,List<string>>>();
            if(db.pyramidRules?.Length>0){foreach(var pair in world.promotionPairs){if(!pair.complete||pair.promoted.Count!=pair.relegated.Count)throw new InvalidOperationException("Barrages incomplets.");exchanges.Add(Tuple.Create(world.divisions.First(d=>d.id==pair.upper),world.divisions.First(d=>d.id==pair.lower),pair.relegated,pair.promoted));}}
            else foreach(var upper in world.divisions){var bits=upper.id.Split('.');if(bits.Length<2||!int.TryParse(bits[1],out int tier))continue;var lower=world.divisions.FirstOrDefault(d=>d.id==bits[0]+"."+(tier+1));if(lower==null)continue;int count=Math.Min(3,Math.Min(upper.clubs.Count,lower.clubs.Count)/4);exchanges.Add(Tuple.Create(upper,lower,upper.clubs.TakeLast(count).ToList(),lower.clubs.Take(count).ToList()));}
            foreach(var e in exchanges){e.Item1.clubs.RemoveAll(x=>e.Item3.Contains(x));e.Item2.clubs.RemoveAll(x=>e.Item4.Contains(x));}
            foreach(var e in exchanges){e.Item1.clubs.AddRange(e.Item4);e.Item2.clubs.AddRange(e.Item3);foreach(var id in e.Item4)ChangeDivisionRevenue(db,id,true);foreach(var id in e.Item3)ChangeDivisionRevenue(db,id,false);}
            foreach(var d in world.divisions)foreach(var id in d.clubs)db.clubs.First(c=>c.id==id).league=d.id;
            var summerFriendlies=world.fixtures.Where(f=>f.league=="friendly"&&!f.played).ToList();
            world.history.AddRange(world.fixtures.Where(f=>f.played&&(f.home==club||f.away==club)));if(world.history.Count>2500)world.history.RemoveRange(0,world.history.Count-2500);
            world.year++;world.transferSpent=0;Mail("Secrétariat sportif","Une nouvelle préparation estivale","Le calendrier ouvre une nouvelle saison. Réservez un stage, proposez des amicaux et préparez le mercato depuis Calendrier et Recrutement.",null,"calendar");SeasonPlayers(db);AiSummerEconomy(db);GenerateSeason(db,false);RestoreSummerPreparation(summerFriendlies);world.lastIntake=-1;CreateIntake(db);
            Mail("Secrétariat sportif","Nouvelle saison",world.year+"–"+(world.year+1)+" : les titres et les mouvements entre les divisions chargées sont enregistrés. Les nouvelles dates sont générées.");
        }
    }
}

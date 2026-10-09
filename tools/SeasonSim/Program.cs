// Simulation de plusieurs saisons de carrière hors Unity, pour calibrer la gestion
// (IA des clubs, économie, effectifs, progression des joueurs).
// Usage : dotnet run -c Release -- [saisons=3] [club=176] [--engine] [--seed N]
//   --world  : aucun club dirigé, l'IA gère tout (dérive du monde sur longue durée)
//   --engine : les matchs du club géré passent par le vrai moteur (≈1,3 s/match) ;
//              sinon ils sont tirés comme ceux des autres clubs (instantané).
//   --report : tableau par saison des indicateurs de l'IA des clubs (effectifs, âges,
//              économie, transferts, champions, survie des promus, progression par âge).
//   --worldseed N : graine du générateur de la carrière (tirages du monde) ; 825671 par défaut.
using System;using System.Collections.Generic;using System.Globalization;using System.IO;using System.Linq;using System.Reflection;using System.Text.Json;using Touchline.Core;
static class P{
 static string FindDatabase(){foreach(var start in new[]{Environment.CurrentDirectory,AppContext.BaseDirectory}){var d=new DirectoryInfo(start);while(d!=null){var f=Path.Combine(d.FullName,"unity","Touchline","Assets","Touchline","Resources","Data","database.json");if(File.Exists(f))return f;d=d.Parent;}}throw new FileNotFoundException("database.json introuvable");}
 static bool withGenerated;
 static readonly CultureInfo FR=CultureInfo.GetCultureInfo("fr-FR");
 static string M(long v)=>(v/1e6).ToString("0.0",FR)+" M€";
 static int Main(string[] a){
  int seasons=a.Length>0&&int.TryParse(a[0],out var s)?s:3;string club=a.Length>1&&!a[1].StartsWith("--")?a[1]:"176";bool engine=a.Contains("--engine"),worldOnly=a.Contains("--world");
  int si=Array.IndexOf(a,"--seed");uint seed=si>=0?uint.Parse(a[si+1]):1;
  bool saveCheck=a.Contains("--savecheck");string dbText=File.ReadAllText(FindDatabase());
  // --savecheck : base lue comme dans Unity (JsonUtility simulé), pour comparer les formats de sauvegarde à l'identique.
  var db=saveCheck?UnityEngine.JsonUtility.FromJson<Database>(dbText):JsonSerializer.Deserialize<Database>(dbText,new JsonSerializerOptions{IncludeFields=true});
  // Comme le jeu : ligues fictives générées au chargement (--no-generated pour la base seule).
  var genWatch=System.Diagnostics.Stopwatch.StartNew();withGenerated=!a.Contains("--no-generated");int generatedPlayers=withGenerated?GeneratedWorld.Expand(db):0;
  Console.WriteLine($"Base : {db.leagues.Length} ligues, {db.clubs.Length} clubs, {db.players.Length} joueurs ({generatedPlayers} générés en {genWatch.ElapsedMilliseconds} ms)");
  // Comme le jeu : empreinte prise sur la base elle-même, avant que la carrière la modifie.
  SaveBaseline baseline=saveCheck?SaveBaseline.From(db):null;
  var c=new Career{club=club,saveBaseline=baseline};c.lineup=Career.Select(db,club,c.tactic);c.EnsureLife(db);
  int wsi=Array.IndexOf(a,"--worldseed");if(wsi>=0)c.life.seed=uint.Parse(a[wsi+1]);c.EnsureWorld(db);
  // --world : personne ne dirige le club, l'IA gère tous les effectifs (observation du monde seul).
  if(worldOnly){c.world.managerStatus="unemployed";c.life.nextFixture=int.MaxValue;}
  var simFixture=typeof(Career).GetMethod("SimulateFixture",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
  Console.WriteLine($"Club : {db.clubs.First(x=>x.id==club).name} — {seasons} saison(s), matchs du club {(engine?"au moteur":"tirés au sort")}\n");
  if(a.Contains("--savesize")){{var f0=Environment.GetEnvironmentVariable("CALIB_SAVEFILE");if(f0!=null)File.WriteAllText(f0+".start.json",UnityEngine.JsonUtility.ToJson(c));}Console.WriteLine($"Sauvegarde initiale (format Unity) : {UnityEngine.JsonUtility.ToJson(c).Length/1e6:0.0} Mo, rosterChanges : {c.world.rosterChanges.Count}");}
  var snapshots=new List<Snapshot>{Snap(db,c)};int startYear=c.world.year,matches=0;var sw=System.Diagnostics.Stopwatch.StartNew();
  for(int guard=0;guard<400*seasons+50&&c.world.year<startYear+seasons;guard++){
   try{c.AdvanceDay(db);}
   catch(InvalidOperationException e) when(e.Message.Contains("rencontre est prévue")){
     var f=c.NextFixture();if(f==null)throw;c.world.activeFixture=f.id;string opp=f.home==club?f.away:f.home;
     try{c.PrepareLineup(db);}catch(InvalidOperationException ex){
       var sq=db.Squad(club).ToArray();
       Console.WriteLine($"ARRÊT jour {c.life.day} ({c.world.year}) : {ex.Message} Effectif : {sq.Length} joueurs, {sq.Count(p=>p.unavailableDays>0)} indisponibles, {sq.Count(p=>p.Goalkeeper)} gardiens. Postes : "+string.Join(", ",sq.GroupBy(p=>p.position).Select(g=>g.Key+" "+g.Count())));
       break;}
     if(engine){c.PrepareLineup(db);var sim=MatchSimulation.Create(db,c,opp,seed++,2700);c.match=sim.State;c.ApplyMatchContext(sim);sim.PlayToEnd();c.RecordMatch(db);}
     else{c.PrepareLineup(db);var sim=MatchSimulation.Create(db,c,opp,seed++,2700);c.match=sim.State;c.ApplyMatchContext(sim);
       // Résultat tiré comme pour les autres clubs, puis inscrit dans un match terminé.
       simFixture.Invoke(c,new object[]{db,f});int h=f.home==club?f.hg:f.ag,w=f.home==club?f.ag:f.hg;f.played=false;
       sim.State.score[0]=h;sim.State.score[1]=w;sim.State.finished=true;sim.State.clock=sim.State.HalfDuration*2;c.RecordMatch(db);}
     matches++;
   }
   catch(InvalidOperationException e) when(e.Message.Contains("Effectif")){
     var sq=db.Squad(club).ToArray();
     Console.WriteLine($"ARRÊT jour {c.life.day} ({c.world.year}) : {e.Message} Effectif du club : {sq.Length} joueurs, {sq.Count(p=>p.unavailableDays>0)} indisponibles, {sq.Count(p=>p.Goalkeeper)} gardiens.");
     Console.WriteLine("  Postes : "+string.Join(", ",sq.GroupBy(p=>p.position).Select(g=>g.Key+" "+g.Count())));
     break;
   }
   if(c.world.year!=snapshots.Last().year)snapshots.Add(Snap(db,c));
  }
  if(snapshots.Last().day!=c.life.day)snapshots.Add(Snap(db,c));
  Console.WriteLine($"{matches} matchs joués par le club, {sw.Elapsed.TotalSeconds:0} s\n");
  Report(db,c,snapshots);
  if(a.Contains("--report"))ClubAiReport(db,c,snapshots);
  if(saveCheck)return SaveCheck(c,dbText);
  if(a.Contains("--savesize")){var t=System.Diagnostics.Stopwatch.StartNew();var json=UnityEngine.JsonUtility.ToJson(c);{var f1=Environment.GetEnvironmentVariable("CALIB_SAVEFILE");if(f1!=null)File.WriteAllText(f1+".end.json",json);}Console.WriteLine($"\nTaille de sauvegarde (format Unity) : {json.Length/1e6:0.0} Mo, sérialisée en {t.ElapsedMilliseconds} ms ; rosterChanges : {c.world.rosterChanges.Count} joueurs, contrats : {c.world.contracts.Count}, messages : {c.life.messages.Count}");}
  int di=Array.IndexOf(a,"--dump");if(di>=0)File.WriteAllLines(a[di+1],c.world.aiTransfers.Select(t=>t.year+" "+t.player+" "+t.seller+">"+t.buyer+" "+t.fee+" "+t.wage).Concat(db.players.OrderBy(p=>p.id,StringComparer.Ordinal).Select(p=>p.id+" "+p.team+" "+p.rating.ToString("R",CultureInfo.InvariantCulture)+" "+p.wage)));
  return 0;
 }
 class Snapshot{public int year,day;public long cash;public int squad;public float top14,ageAvg;public Dictionary<string,long> aiCash=new();public Dictionary<string,long> aiDebt=new();public Dictionary<string,double> wageRatio=new();public Dictionary<string,long> revenue=new();public double worldAge,worldRating,worldTop;public int worldCount,worldOld,worldYoung;public Dictionary<string,int> aiSquad=new();public Dictionary<string,float> aiStrength=new();public float wageMedian;public int free;public int transfers;
  public Dictionary<string,string> league=new();public Dictionary<string,int> staying=new(),leaving=new();public HashSet<string> leavingIds=new();public int generated,freeAgents;public List<AiTransferRecord> yearTransfers=new();public Dictionary<string,(int age,float rating,string team)> people=new();}
 static Snapshot Snap(Database db,Career c){
  var s=new Snapshot{year=c.world.year,day=c.life.day,cash=c.life.cash};
  var sq=db.Squad(c.club).ToArray();s.squad=sq.Length;s.top14=sq.Length==0?0:(float)sq.OrderByDescending(p=>p.rating+p.development).Take(14).Average(p=>p.rating+p.development);s.ageAvg=sq.Length==0?0:(float)sq.Average(p=>p.age);
  foreach(var acc in c.world.aiAccounts){s.aiCash[acc.club]=GetLong(acc,"cash");s.aiDebt[acc.club]=GetLong(acc,"operatingDebt");}
  foreach(var cl in db.clubs.Where(x=>x.annualRevenue>0&&x.playable)){long w=db.Squad(cl.id).Sum(p=>(long)p.wage);s.wageRatio[cl.id]=w*52.0/cl.annualRevenue;s.revenue[cl.id]=cl.annualRevenue;}
  foreach(var cl in db.clubs.Where(x=>x.playable)){var q=db.Squad(cl.id).ToArray();s.aiSquad[cl.id]=q.Length;s.aiStrength[cl.id]=q.Length==0?0:(float)q.OrderByDescending(p=>p.rating+p.development).Take(14).Average(p=>p.rating+p.development);}
  var wages=db.players.Where(p=>p.team!=null&&p.wage>0).Select(p=>p.wage).OrderBy(x=>x).ToArray();s.wageMedian=wages.Length>0?wages[wages.Length/2]:0;
  {var ids=new HashSet<string>(db.clubs.Where(x=>x.playable).Select(x=>x.id));var ws=db.players.Where(p=>p.team!=null&&ids.Contains(p.team)).ToArray();s.worldCount=ws.Length;s.worldAge=ws.Average(p=>p.age);s.worldRating=ws.Average(p=>p.rating);s.worldTop=ws.Count(p=>p.rating>=80);s.worldOld=ws.Count(p=>p.age>=32);s.worldYoung=ws.Count(p=>p.age<=21);}
  // Partants annoncés : contrat non renouvelé (ou surplus) qui expire dans la fenêtre d'été.
  {var cons=c.world.contracts.GroupBy(x=>x.player).ToDictionary(g=>g.Key,g=>g.Last());foreach(var cl in db.clubs.Where(x=>x.playable)){var q=db.Squad(cl.id);var gone=q.Where(p=>cons.TryGetValue(p.id,out var e)&&e.club==cl.id&&e.aiRelease&&e.until<=c.life.day+21).Select(p=>p.id).ToArray();s.leavingIds.UnionWith(gone);int out_=gone.Length;s.leaving[cl.id]=out_;s.staying[cl.id]=q.Count-out_;}}
  s.generated=db.players.Count(p=>p.id.StartsWith("regen-")&&p.id.Contains("-"+s.year+"-"));s.freeAgents=db.players.Count(p=>p.team=="free");
  s.yearTransfers=(c.world.aiTransfers??new List<AiTransferRecord>()).Where(t=>t.year==s.year).ToList();
  foreach(var cl in db.clubs.Where(x=>x.playable))s.league[cl.id]=cl.league;foreach(var p in db.players)if(p.team!=null&&p.team!="retired")s.people[p.id]=(p.age,p.rating,p.team);
  s.free=s.freeAgents;s.transfers=c.world.aiTransfers?.Count??0;return s;
 }
 static int SaveCheck(Career c,string dbText){
  var J=(Func<object,string>)(o=>UnityEngine.JsonUtility.ToJson(o));var sw=System.Diagnostics.Stopwatch.StartNew();
  string full=J(c);long tFull=sw.ElapsedMilliseconds;sw.Restart();
  for(int warm=0;warm<2;warm++){if(c.PrepareCompactSave())c.RestoreAfterSave();}var swp=System.Diagnostics.Stopwatch.StartNew();bool packed=c.PrepareCompactSave();Console.WriteLine($"Compactage seul : {swp.ElapsedMilliseconds} ms");string compact;try{compact=J(c);var dump=Environment.GetEnvironmentVariable("CALIB_SAVEFILE");if(dump!=null)File.WriteAllText(dump,compact);}finally{if(packed)c.RestoreAfterSave();}long tCompact=sw.ElapsedMilliseconds;
  Console.WriteLine($"\nFormat complet : {full.Length/1e6:0.00} Mo ({tFull} ms) — format compact : {compact.Length/1e6:0.00} Mo ({tCompact} ms, compactage inclus)");
  if(!packed){Console.WriteLine("ÉCHEC : compactage non appliqué");return 1;}
  if(J(c)!=full){Console.WriteLine("ÉCHEC : l'état en mémoire a changé après la sauvegarde");return 1;}
  Database Load(string text,out Career state){state=UnityEngine.JsonUtility.FromJson<Career>(text);var pristine=UnityEngine.JsonUtility.FromJson<Database>(dbText);if(withGenerated)GeneratedWorld.Expand(pristine);if(!CareerSaveRestore.TryRestore(pristine,state,out var restored)){var probe=UnityEngine.JsonUtility.FromJson<Career>(text);var p2=UnityEngine.JsonUtility.FromJson<Database>(dbText);if(withGenerated)GeneratedWorld.Expand(p2);try{probe.ExpandCompactSave(p2);probe.RestoreWorld(p2);Console.WriteLine("lineup: "+string.Join(",",probe.lineup.Select(id=>id+"="+p2.Find(id)?.team))+" club "+probe.club);}catch(Exception e){Console.WriteLine(e);}throw new Exception("Restauration refusée");}return restored;}
  sw.Restart();var dbOld=Load(full,out var oldState);long lOld=sw.ElapsedMilliseconds;sw.Restart();var dbNew=Load(compact,out var newState);long lNew=sw.ElapsedMilliseconds;
  Console.WriteLine($"Chargement (désérialisation + restauration) : complet {lOld} ms, compact {lNew} ms");
  int diffs=0;string a1=J(oldState),a2=J(newState);if(a1!=a2){diffs++;int i=0;while(i<a1.Length&&i<a2.Length&&a1[i]==a2[i])i++;Console.WriteLine($"ÉCHEC carrière différente à {i} : …{a1.Substring(Math.Max(0,i-120),Math.Min(240,a1.Length-Math.Max(0,i-120)))}\n  vs …{a2.Substring(Math.Max(0,i-120),Math.Min(240,a2.Length-Math.Max(0,i-120)))}");}
  if(dbOld.players.Length!=dbNew.players.Length){diffs++;Console.WriteLine("ÉCHEC nombre de joueurs");}
  else for(int i=0;i<dbOld.players.Length&&diffs<5;i++){string p1=J(dbOld.players[i]),p2=J(dbNew.players[i]);if(p1!=p2){diffs++;Console.WriteLine("ÉCHEC joueur "+dbOld.players[i].id+"\n  "+p1.Substring(0,Math.Min(300,p1.Length))+"\n  "+p2.Substring(0,Math.Min(300,p2.Length)));}}
  if(J(dbOld.clubs)!=J(dbNew.clubs)){}
  Console.WriteLine(diffs==0?"Rechargement identique : carrière et "+dbNew.players.Length+" joueurs, ancien format = format compact.":"Différences : "+diffs);
  return diffs==0?0:1;
 }
 static long GetLong(object o,string name){var f=o.GetType().GetField(name);return f==null?0:Convert.ToInt64(f.GetValue(o));}
 static void Report(Database db,Career c,List<Snapshot> snaps){
  string N(string id)=>db.clubs.FirstOrDefault(x=>x.id==id)?.name??id;
  Console.WriteLine("| Saison | Jour | Trésorerie club | Effectif | Force top 14 | Âge moyen | Salaire médian (monde) | Transferts IA cumulés |");
  Console.WriteLine("|---|---|---|---|---|---|---|---|");
  foreach(var s in snaps)Console.WriteLine($"| {s.year} | {s.day} | {M(s.cash)} | {s.squad} | {s.top14.ToString("0.0",FR)} | {s.ageAvg.ToString("0.0",FR)} | {s.wageMedian.ToString("0",FR)} €/sem. | {s.transfers} |");
  var first=snaps.First();var last=snaps.Last();
  var clubs=first.aiSquad.Keys.ToArray();
  var squads=clubs.Select(k=>last.aiSquad.GetValueOrDefault(k)).OrderBy(x=>x).ToArray();
  Console.WriteLine($"\nClubs jouables : {clubs.Length}. Taille d'effectif en fin de simulation : min {squads.First()}, médiane {squads[squads.Length/2]}, max {squads.Last()} (début : médiane {clubs.Select(k=>first.aiSquad[k]).OrderBy(x=>x).ElementAt(clubs.Length/2)}).");
  Console.WriteLine($"Clubs sous 18 joueurs : {squads.Count(x=>x<18)} ; au-dessus de 40 : {squads.Count(x=>x>40)}.");
  var drift=clubs.Select(k=>last.aiStrength.GetValueOrDefault(k)-first.aiStrength[k]).OrderBy(x=>x).ToArray();
  Console.WriteLine($"Évolution de la force (top 14) des clubs : médiane {drift[drift.Length/2].ToString("+0.0;-0.0",FR)}, 10 % les plus bas {drift[drift.Length/10].ToString("+0.0;-0.0",FR)}, 10 % les plus hauts {drift[drift.Length*9/10].ToString("+0.0;-0.0",FR)}.");
  if(first.aiCash.Count>0){var cash=last.aiCash.Values.OrderBy(x=>x).ToArray();Console.WriteLine($"Trésorerie des clubs IA : médiane {M(cash[cash.Length/2])}, négative pour {cash.Count(x=>x<0)} / {cash.Length} (début : négative pour {first.aiCash.Values.Count(x=>x<0)}).");}

  foreach(var sn in snaps)Console.WriteLine($"Monde {sn.year} : {sn.worldCount} joueurs, âge moyen {sn.worldAge:0.0}, note moyenne {sn.worldRating:0.0}, ≥80 : {sn.worldTop}, ≤21 ans : {sn.worldYoung}, ≥32 ans : {sn.worldOld}");
  if(last.aiDebt.Count>0){var debt=last.aiDebt.Where(kv=>last.wageRatio.ContainsKey(kv.Key)).Select(kv=>kv.Value).ToArray();var withDebt=debt.Where(x=>x>0).OrderBy(x=>x).ToArray();Console.WriteLine($"Clubs IA endettés : {withDebt.Length} / {debt.Length} (clubs jouables ; début {first.aiDebt.Values.Count(x=>x>0)}), dette médiane des endettés {(withDebt.Length>0?M(withDebt[withDebt.Length/2]):"—")}.");}
  {double Med(IEnumerable<double> v){var a=v.OrderBy(x=>x).ToArray();return a.Length==0?0:a[a.Length/2];}Console.WriteLine($"Salaires joueurs / recettes : médiane {Med(first.wageRatio.Values):P0} → {Med(last.wageRatio.Values):P0}, clubs au-dessus de 70 % : {first.wageRatio.Values.Count(x=>x>.7)} → {last.wageRatio.Values.Count(x=>x>.7)}.");
   var margins=last.aiCash.Keys.Where(k=>first.aiCash.ContainsKey(k)&&last.wageRatio.ContainsKey(k)&&first.revenue.TryGetValue(k,out var r)&&r>0).Select(k=>((last.aiCash[k]-last.aiDebt[k])-(first.aiCash[k]-first.aiDebt[k]))/((double)first.revenue[k]*Math.Max(1,(last.day-first.day)/365.0))).OrderBy(x=>x).ToArray();
   if(margins.Length>0)Console.WriteLine($"Résultat annuel moyen / recettes (clubs IA) : 10 % {margins[margins.Length/10]:P0}, médiane {margins[margins.Length/2]:P0}, 90 % {margins[margins.Length*9/10]:P0} ; en perte : {margins.Count(x=>x<0)} / {margins.Length}.");}
  Console.WriteLine($"Joueurs sans club : {first.free} → {last.free}.");
  var top=clubs.OrderByDescending(k=>last.aiStrength.GetValueOrDefault(k)-first.aiStrength[k]).Take(3).Select(k=>N(k)+" "+(last.aiStrength[k]-first.aiStrength[k]).ToString("+0.0;-0.0",FR));var bottom=clubs.OrderBy(k=>last.aiStrength.GetValueOrDefault(k)-first.aiStrength[k]).Take(3).Select(k=>N(k)+" "+(last.aiStrength[k]-first.aiStrength[k]).ToString("+0.0;-0.0",FR));
  Console.WriteLine("Plus forte progression : "+string.Join(", ",top)+"\nPlus forte baisse : "+string.Join(", ",bottom));
  var champs=c.world.honours?.Select(h=>h.GetType().GetFields().Select(f=>f.Name+"="+f.GetValue(h)).Aggregate((x,y)=>x+" "+y)).Take(8);
  if(champs!=null&&champs.Any())Console.WriteLine("\nPalmarès enregistré (extrait) :\n- "+string.Join("\n- ",champs));
 }
 // --report : crédibilité de l'IA des clubs, saison par saison (clubs jouables uniquement).
 static void ClubAiReport(Database db,Career c,List<Snapshot> snaps){
  double Med(IEnumerable<double> v){var x=v.OrderBy(y=>y).ToArray();return x.Length==0?0:x[x.Length/2];}
  string F(double v,string f="0.0")=>v.ToString(f,FR);
  int Tier(string league)=>league!=null&&league.Contains('.')&&int.TryParse(league.Split('.')[1],out int t)?t:0;
  Console.WriteLine("\n## Indicateurs IA des clubs\n");
  Console.WriteLine("| Saison | Effectif min / méd. / max | Clubs 22–30 | Âge moyen | ≤21 ans | ≥32 ans | Note moy. | ≥80 | Salaires/recettes méd. | > 80 % | Endettés | Dette > recettes | Tréso/recettes méd. | Transferts | Âge recrues | Max recrues/club |");
  Console.WriteLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
  foreach(var s in snaps){
   var ids=s.aiSquad.Keys.ToArray();var sizes=ids.Select(k=>s.aiSquad[k]).OrderBy(x=>x).ToArray();
   // Effectifs de la saison : les partants annoncés (fin de contrat dans la fenêtre d'été) sont exclus.
   var ppl=s.people.Where(kv=>s.league.ContainsKey(kv.Value.team)&&!s.leavingIds.Contains(kv.Key)).Select(kv=>kv.Value).ToArray();
   var accounts=ids.Where(k=>s.aiDebt.ContainsKey(k)&&s.revenue.GetValueOrDefault(k)>0).ToArray();
   var tr=s.yearTransfers.ToArray();
   var ages=tr.Select(t=>s.people.TryGetValue(t.player,out var q)?q.age:0).Where(x=>x>0).ToArray();
   int maxPerClub=tr.Length==0?0:tr.GroupBy(t=>t.buyer).Max(g=>g.Count());
   Console.WriteLine($"| {s.year} | {sizes.First()} / {sizes[sizes.Length/2]} / {sizes.Last()} | {F(100.0*sizes.Count(x=>x>=22&&x<=30)/sizes.Length,"0")} % | {F(ppl.Average(p=>p.age))} | {F(100.0*ppl.Count(p=>p.age<=21)/ppl.Length,"0")} % | {F(100.0*ppl.Count(p=>p.age>=32)/ppl.Length,"0")} % | {F(ppl.Average(p=>p.rating))} | {ppl.Count(p=>p.rating>=80)} | {F(100*Med(s.wageRatio.Values),"0")} % | {s.wageRatio.Values.Count(x=>x>.8)} | {accounts.Count(k=>s.aiDebt[k]>0)} | {accounts.Count(k=>s.aiDebt[k]>s.revenue[k])} | {F(100*Med(accounts.Select(k=>(double)s.aiCash[k]/s.revenue[k])),"0")} % | {tr.Length} | {(ages.Length>0?F(ages.Average()):"—")} | {maxPerClub} |");
  }
  Console.WriteLine("\n| Saison | Effectif hors partants min / méd. / max | Clubs 22–30 hors partants | Partants annoncés (total / max club) | Joueurs générés | Joueurs libres | Recrues : vendeur sous sa cible |");
  Console.WriteLine("|---|---|---|---|---|---|---|");
  var targets=(c.world.developmentReferences??new List<ClubDevelopmentReference>()).ToDictionary(r=>r.club,r=>r.squadSize);
  foreach(var s in snaps){var st=s.staying.Values.OrderBy(x=>x).ToArray();if(st.Length==0)continue;
   int weakened=s.yearTransfers.Count(t=>s.staying.TryGetValue(t.seller,out var n)&&targets.TryGetValue(t.seller,out var target)&&n<target);
   Console.WriteLine($"| {s.year} | {st.First()} / {st[st.Length/2]} / {st.Last()} | {F(100.0*st.Count(x=>x>=22&&x<=30)/st.Length,"0")} % | {s.leaving.Values.Sum()} / {s.leaving.Values.DefaultIfEmpty(0).Max()} | {s.generated} | {s.freeAgents} | {weakened} / {s.yearTransfers.Count} |");}
  // Champions des premières divisions et survie des promus.
  var tops=snaps.First().league.Values.Where(l=>Tier(l)==1).Distinct().OrderBy(x=>x,StringComparer.Ordinal).ToArray();
  var honours=c.world.honours??new List<Honour>();int seasons=0,titles=0;var lines=new List<string>();
  foreach(var l in tops){var champs=honours.Where(h=>h.competition==l).OrderBy(h=>h.year).Select(h=>h.club).ToArray();if(champs.Length==0)continue;seasons+=champs.Length;titles+=champs.Distinct().Count();lines.Add(l+" "+champs.Distinct().Count()+"/"+champs.Length+" : "+string.Join(", ",champs.Select(x=>db.clubs.FirstOrDefault(y=>y.id==x)?.name??x)));}
  Console.WriteLine($"\nChampions distincts en première division : {titles} pour {seasons} titres.");foreach(var l in lines)Console.WriteLine("- "+l);
  int promoted=0,survived=0;for(int i=1;i+1<snaps.Count;i++)foreach(var kv in snaps[i].league){if(Tier(kv.Value)!=1||!snaps[i-1].league.TryGetValue(kv.Key,out var before)||Tier(before)!=2)continue;promoted++;if(snaps[i+1].league.TryGetValue(kv.Key,out var after)&&Tier(after)==1)survived++;}
  Console.WriteLine($"Promus en première division maintenus la saison suivante : {survived} / {promoted}{(promoted>0?" ("+F(100.0*survived/promoted,"0")+" %)":"")}.");
  // Rang de force (top 14) au début et à la fin, clubs de chaque première division d'origine (Spearman).
  var first=snaps.First();var last=snaps.Last();var corr=new List<double>();
  foreach(var l in tops){var clubs=first.league.Where(kv=>kv.Value==l).Select(kv=>kv.Key).ToArray();if(clubs.Length<4)continue;var r0=clubs.OrderByDescending(k=>first.aiStrength[k]).ToList();var r1=clubs.OrderByDescending(k=>last.aiStrength.GetValueOrDefault(k)).ToList();double n=clubs.Length,d2=clubs.Sum(k=>Math.Pow(r0.IndexOf(k)-r1.IndexOf(k),2));corr.Add(1-6*d2/(n*(n*n-1)));}
  if(corr.Count>0)Console.WriteLine($"Corrélation de rang de force début → fin (premières divisions) : médiane {F(Med(corr),"0.00")}, min {F(corr.Min(),"0.00")}.");
  double Spread(Snapshot s,string l){var v=s.league.Where(kv=>kv.Value==l).Select(kv=>(double)s.aiStrength.GetValueOrDefault(kv.Key)).ToArray();return v.Length<2?0:v.Max()-v.Min();}
  Console.WriteLine($"Écart de force (top 14) entre le plus fort et le plus faible d'une première division : médiane {F(Med(tops.Select(l=>Spread(first,l))))} → {F(Med(tops.Select(l=>Spread(last,l))))}.");
  // Progression annuelle moyenne de la note selon l'âge (joueurs de clubs jouables présents deux saisons de suite).
  var buckets=new SortedDictionary<string,List<double>>(StringComparer.Ordinal);
  for(int i=1;i<snaps.Count;i++)foreach(var kv in snaps[i].people){if(!snaps[i-1].people.TryGetValue(kv.Key,out var b)||!snaps[i-1].league.ContainsKey(b.team))continue;string k=b.age<=20?"a ≤20":b.age<=23?"b 21–23":b.age<=26?"c 24–26":b.age<=29?"d 27–29":b.age<=32?"e 30–32":"f ≥33";if(!buckets.TryGetValue(k,out var list))buckets[k]=list=new List<double>();list.Add(kv.Value.rating-b.rating);}
  Console.WriteLine("Progression annuelle moyenne de la note par âge : "+string.Join(", ",buckets.Select(kv=>kv.Key.Substring(2)+" "+kv.Value.Average().ToString("+0.00;-0.00",FR))));
  // Plus gros effectifs en fin : composition (prêtés entrants, ≤21 ans, contrats échus ou sans contrat au club).
  {var refs=(c.world.developmentReferences??new List<ClubDevelopmentReference>()).ToDictionary(r=>r.club);var cons=c.world.contracts.GroupBy(x=>x.player).ToDictionary(g=>g.Key,g=>g.Last());
   var big=last.aiSquad.OrderByDescending(kv=>kv.Value).Take(5).Select(kv=>{var q=db.Squad(kv.Key).ToArray();int loans=q.Count(p=>cons.TryGetValue(p.id,out var e)&&e.parent!=null&&e.parent!=kv.Key),young=q.Count(p=>p.age<=21),none=q.Count(p=>!cons.TryGetValue(p.id,out var e)||e.club!=kv.Key),regen=q.Count(p=>p.id.StartsWith("regen-"));
    return (db.clubs.FirstOrDefault(x=>x.id==kv.Key)?.name??kv.Key)+" "+kv.Value+" (réf. "+(refs.TryGetValue(kv.Key,out var r)?r.squadSize:0)+", prêtés "+loans+", ≤21 ans "+young+", sans contrat au club "+none+", générés "+regen+")";});
   Console.WriteLine("Plus gros effectifs : "+string.Join(" ; ",big));
   if(Environment.GetEnvironmentVariable("SEASONSIM_DIAG")!=null)foreach(var kv in last.aiSquad.OrderByDescending(kv=>kv.Value).Take(2)){Console.WriteLine("DIAG "+kv.Key+" ligue "+db.clubs.First(x=>x.id==kv.Key).league);foreach(var p in db.Squad(kv.Key).OrderBy(p=>p.id))Console.WriteLine($"  {p.id} {p.position} âge {p.age} note {p.rating:0.0} salaire {p.wage} contrat {(cons.TryGetValue(p.id,out var e)?e.club+" jusqu'à "+(e.until-c.life.day)+" j, rel "+e.aiRelease+" joined "+e.joined:"aucun")}");}
  }
  // Trésorerie nette (trésorerie - dette) rapportée aux recettes en fin de simulation.
  var net=last.aiCash.Keys.Where(k=>last.revenue.GetValueOrDefault(k)>0).Select(k=>(last.aiCash[k]-last.aiDebt.GetValueOrDefault(k))/(double)last.revenue[k]).OrderBy(x=>x).ToArray();
  if(net.Length>0)Console.WriteLine($"Trésorerie nette / recettes en fin : 5 % {F(100*net[net.Length/20],"0")} %, médiane {F(100*net[net.Length/2],"0")} %, 95 % {F(100*net[net.Length*19/20],"0")} % ; au-dessus de 200 % : {net.Count(x=>x>2)}, sous -100 % : {net.Count(x=>x<-1)} / {net.Length}.");
 }
}

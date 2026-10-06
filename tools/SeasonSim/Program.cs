// Simulation de plusieurs saisons de carrière hors Unity, pour calibrer la gestion
// (IA des clubs, économie, effectifs, progression des joueurs).
// Usage : dotnet run -c Release -- [saisons=3] [club=176] [--engine] [--seed N]
//   --world  : aucun club dirigé, l'IA gère tout (dérive du monde sur longue durée)
//   --engine : les matchs du club géré passent par le vrai moteur (≈1,3 s/match) ;
//              sinon ils sont tirés comme ceux des autres clubs (instantané).
using System;using System.Collections.Generic;using System.Globalization;using System.IO;using System.Linq;using System.Reflection;using System.Text.Json;using Touchline.Core;
static class P{
 static string FindDatabase(){foreach(var start in new[]{Environment.CurrentDirectory,AppContext.BaseDirectory}){var d=new DirectoryInfo(start);while(d!=null){var f=Path.Combine(d.FullName,"unity","Touchline","Assets","Touchline","Resources","Data","database.json");if(File.Exists(f))return f;d=d.Parent;}}throw new FileNotFoundException("database.json introuvable");}
 static readonly CultureInfo FR=CultureInfo.GetCultureInfo("fr-FR");
 static string M(long v)=>(v/1e6).ToString("0.0",FR)+" M€";
 static int Main(string[] a){
  int seasons=a.Length>0&&int.TryParse(a[0],out var s)?s:3;string club=a.Length>1&&!a[1].StartsWith("--")?a[1]:"176";bool engine=a.Contains("--engine"),worldOnly=a.Contains("--world");
  int si=Array.IndexOf(a,"--seed");uint seed=si>=0?uint.Parse(a[si+1]):1;
  bool saveCheck=a.Contains("--savecheck");string dbText=File.ReadAllText(FindDatabase());
  // --savecheck : base lue comme dans Unity (JsonUtility simulé), pour comparer les formats de sauvegarde à l'identique.
  var db=saveCheck?UnityEngine.JsonUtility.FromJson<Database>(dbText):JsonSerializer.Deserialize<Database>(dbText,new JsonSerializerOptions{IncludeFields=true});
  // Comme le jeu : empreinte prise sur la base elle-même, avant que la carrière la modifie.
  SaveBaseline baseline=saveCheck?SaveBaseline.From(db):null;
  var c=new Career{club=club,saveBaseline=baseline};c.lineup=Career.Select(db,club,c.tactic);c.EnsureLife(db);c.EnsureWorld(db);
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
  if(saveCheck)return SaveCheck(c,dbText);
  if(a.Contains("--savesize")){var t=System.Diagnostics.Stopwatch.StartNew();var json=UnityEngine.JsonUtility.ToJson(c);{var f1=Environment.GetEnvironmentVariable("CALIB_SAVEFILE");if(f1!=null)File.WriteAllText(f1+".end.json",json);}Console.WriteLine($"\nTaille de sauvegarde (format Unity) : {json.Length/1e6:0.0} Mo, sérialisée en {t.ElapsedMilliseconds} ms ; rosterChanges : {c.world.rosterChanges.Count} joueurs, contrats : {c.world.contracts.Count}, messages : {c.life.messages.Count}");}
  int di=Array.IndexOf(a,"--dump");if(di>=0)File.WriteAllLines(a[di+1],c.world.aiTransfers.Select(t=>t.year+" "+t.player+" "+t.seller+">"+t.buyer+" "+t.fee+" "+t.wage).Concat(db.players.OrderBy(p=>p.id,StringComparer.Ordinal).Select(p=>p.id+" "+p.team+" "+p.rating.ToString("R",CultureInfo.InvariantCulture)+" "+p.wage)));
  return 0;
 }
 class Snapshot{public int year,day;public long cash;public int squad;public float top14,ageAvg;public Dictionary<string,long> aiCash=new();public Dictionary<string,int> aiSquad=new();public Dictionary<string,float> aiStrength=new();public float wageMedian;public int free;public int transfers;}
 static Snapshot Snap(Database db,Career c){
  var s=new Snapshot{year=c.world.year,day=c.life.day,cash=c.life.cash};
  var sq=db.Squad(c.club).ToArray();s.squad=sq.Length;s.top14=sq.Length==0?0:(float)sq.OrderByDescending(p=>p.rating+p.development).Take(14).Average(p=>p.rating+p.development);s.ageAvg=sq.Length==0?0:(float)sq.Average(p=>p.age);
  foreach(var acc in c.world.aiAccounts){s.aiCash[acc.club]=GetLong(acc,"cash");}
  foreach(var cl in db.clubs.Where(x=>x.playable)){var q=db.Squad(cl.id).ToArray();s.aiSquad[cl.id]=q.Length;s.aiStrength[cl.id]=q.Length==0?0:(float)q.OrderByDescending(p=>p.rating+p.development).Take(14).Average(p=>p.rating+p.development);}
  var wages=db.players.Where(p=>p.team!=null&&p.wage>0).Select(p=>p.wage).OrderBy(x=>x).ToArray();s.wageMedian=wages.Length>0?wages[wages.Length/2]:0;
  s.free=db.players.Count(p=>string.IsNullOrEmpty(p.team));s.transfers=c.world.aiTransfers?.Count??0;return s;
 }
 static int SaveCheck(Career c,string dbText){
  var J=(Func<object,string>)(o=>UnityEngine.JsonUtility.ToJson(o));var sw=System.Diagnostics.Stopwatch.StartNew();
  string full=J(c);long tFull=sw.ElapsedMilliseconds;sw.Restart();
  for(int warm=0;warm<2;warm++){if(c.PrepareCompactSave())c.RestoreAfterSave();}var swp=System.Diagnostics.Stopwatch.StartNew();bool packed=c.PrepareCompactSave();Console.WriteLine($"Compactage seul : {swp.ElapsedMilliseconds} ms");string compact;try{compact=J(c);var dump=Environment.GetEnvironmentVariable("CALIB_SAVEFILE");if(dump!=null)File.WriteAllText(dump,compact);}finally{if(packed)c.RestoreAfterSave();}long tCompact=sw.ElapsedMilliseconds;
  Console.WriteLine($"\nFormat complet : {full.Length/1e6:0.00} Mo ({tFull} ms) — format compact : {compact.Length/1e6:0.00} Mo ({tCompact} ms, compactage inclus)");
  if(!packed){Console.WriteLine("ÉCHEC : compactage non appliqué");return 1;}
  if(J(c)!=full){Console.WriteLine("ÉCHEC : l'état en mémoire a changé après la sauvegarde");return 1;}
  Database Load(string text,out Career state){state=UnityEngine.JsonUtility.FromJson<Career>(text);var pristine=UnityEngine.JsonUtility.FromJson<Database>(dbText);if(!CareerSaveRestore.TryRestore(pristine,state,out var restored)){var probe=UnityEngine.JsonUtility.FromJson<Career>(text);var p2=UnityEngine.JsonUtility.FromJson<Database>(dbText);try{probe.ExpandCompactSave(p2);probe.RestoreWorld(p2);Console.WriteLine("lineup: "+string.Join(",",probe.lineup.Select(id=>id+"="+p2.Find(id)?.team))+" club "+probe.club);}catch(Exception e){Console.WriteLine(e);}throw new Exception("Restauration refusée");}return restored;}
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
  Console.WriteLine($"Joueurs sans club : {first.free} → {last.free}.");
  var top=clubs.OrderByDescending(k=>last.aiStrength.GetValueOrDefault(k)-first.aiStrength[k]).Take(3).Select(k=>N(k)+" "+(last.aiStrength[k]-first.aiStrength[k]).ToString("+0.0;-0.0",FR));var bottom=clubs.OrderBy(k=>last.aiStrength.GetValueOrDefault(k)-first.aiStrength[k]).Take(3).Select(k=>N(k)+" "+(last.aiStrength[k]-first.aiStrength[k]).ToString("+0.0;-0.0",FR));
  Console.WriteLine("Plus forte progression : "+string.Join(", ",top)+"\nPlus forte baisse : "+string.Join(", ",bottom));
  var champs=c.world.honours?.Select(h=>h.GetType().GetFields().Select(f=>f.Name+"="+f.GetValue(h)).Aggregate((x,y)=>x+" "+y)).Take(8);
  if(champs!=null&&champs.Any())Console.WriteLine("\nPalmarès enregistré (extrait) :\n- "+string.Join("\n- ",champs));
 }
}

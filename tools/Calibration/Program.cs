using System;using System.Collections.Generic;using System.IO;using System.Linq;using System.Text.Json;using System.Threading.Tasks;using Touchline.Core;
// Calibration du moteur de match Touchline hors d'Unity.
// Usage : dotnet run -c Release -- [matchs=200] [graine=1] [chemin database.json]
// Compile directement les sources de Assets/Touchline/Core : toute modification du moteur est mesurée.
static partial class P{
 static string FindDatabase(){foreach(var start in new[]{Environment.CurrentDirectory,AppContext.BaseDirectory}){var d=new DirectoryInfo(start);while(d!=null){var f=Path.Combine(d.FullName,"unity","Touchline","Assets","Touchline","Resources","Data","database.json");if(File.Exists(f))return f;d=d.Parent;}}throw new FileNotFoundException("database.json introuvable : passez son chemin en 3e argument.");}
 static void Main(string[] a){
  if(a.Length>0&&a[0]=="--tactics"){AuditTactics(a.Skip(1).ToArray());return;}
  int n=a.Length>0?int.Parse(a[0]):200;uint seed0=a.Length>1?uint.Parse(a[1]):1;string dbPath=a.Length>2?a[2]:FindDatabase();
  var db=JsonSerializer.Deserialize<Database>(File.ReadAllText(dbPath),new JsonSerializerOptions{IncludeFields=true});
  db.Find(db.players[0].id); // construit l'index joueur une fois : Database.Find n'est pas thread-safe
  var leagues=db.leagues.Where(l=>!l.scoutingOnly&&l.tier==1).Select(l=>l.id).ToHashSet();
  var clubs=db.clubs.Where(c=>c.playable&&!c.reserve&&leagues.Contains(c.league)).ToArray();
  float Str(string club)=>db.players.Where(p=>p.team==club).OrderByDescending(p=>p.rating).Take(14).Average(p=>p.rating);
  var str=clubs.ToDictionary(c=>c.id,c=>Str(c.id));
  var rng=new Random((int)seed0);var pairs=new List<(string,string,uint)>();
  while(pairs.Count<n){var h=clubs[rng.Next(clubs.Length)];var cands=clubs.Where(c=>c.league==h.league&&c.id!=h.id).ToArray();if(cands.Length==0)continue;var w=cands[rng.Next(cands.Length)];pairs.Add((h.id,w.id,(uint)rng.Next()));}
  var res=new MatchStats[n];
  Parallel.For(0,n,new ParallelOptions{MaxDegreeOfParallelism=Environment.ProcessorCount},i=>{var (h,w,s)=pairs[i];res[i]=Play(db,h,w,s);res[i].diff=str[h]-str[w];});
  Console.WriteLine($"Calibration : {n} matchs demandés, graine {seed0}. Niveau du favori : moyenne des 14 meilleurs ratings initiaux ; seuil de 3 points.\n");
  Report(res);
 }
 class MatchStats{public int goals,shots,onTarget,corners,fouls,yellows,reds,pens,offsides,throws,passes,completed,goalKicks,freeKicks,homeGoals,awayGoals;public float possHome,diff,xg;public bool error;public Dictionary<string,int> ph=new Dictionary<string,int>(),ev=new Dictionary<string,int>();public string err;}
 static MatchStats Play(Database db,string home,string away,uint seed,Action<Tactic> configure=null,Action<MatchState> observe=null,Action<MatchSimulation> prepare=null){
  var st=new MatchStats();
  try{
   var c=new Career{club=home};configure?.Invoke(c.tactic);c.lineup=Career.Select(db,home,c.tactic);
   var sim=MatchSimulation.Create(db,c,away,seed,2700);sim.State.professionalRules=true;prepare?.Invoke(sim);
   string prev=sim.State.phase;var m=sim.State;
   void Run(){while(!m.halfTime&&!m.finished){sim.Advance(.1);if(m.phase!=prev){st.ph[m.phase]=st.ph.GetValueOrDefault(m.phase)+1;if(m.phase=="penalty")st.pens++;if(m.phase=="goal-kick")st.goalKicks++;if(m.phase=="free-kick")st.freeKicks++;prev=m.phase;}}}
   Run();sim.ResumeHalf();Run();
   observe?.Invoke(m);
   st.homeGoals=m.score[0];st.awayGoals=m.score[1];st.goals=m.score[0]+m.score[1];st.shots=m.shots[0]+m.shots[1];st.passes=m.passes[0]+m.passes[1];st.completed=m.completedPasses[0]+m.completedPasses[1];
   foreach(var t in m.metrics){st.onTarget+=t.shotsOnTarget;st.corners+=t.corners;st.fouls+=t.fouls;st.offsides+=t.offsides;st.throws+=t.throwIns;st.xg+=t.xg;}
   foreach(var e in m.events)st.ev[e.kind]=st.ev.GetValueOrDefault(e.kind)+1;
   st.yellows=m.events.Count(e=>e.kind=="yellow");st.reds=m.events.Count(e=>e.kind=="red");
   float ph=m.metrics[0].possessionSeconds,pa=m.metrics[1].possessionSeconds;st.possHome=ph+pa>0?ph/(ph+pa):.5f;
  }catch(Exception e){st.error=true;st.err=e.GetType().Name+": "+e.Message+(Environment.GetEnvironmentVariable("CALIB_TRACE")=="1"?"\n"+e.StackTrace:"");}
  return st;
 }
 static void Report(MatchStats[] r){
  var ok=r.Where(x=>!x.error).ToArray();int errs=r.Length-ok.Length;
  string F(double v)=>v.ToString("0.00",System.Globalization.CultureInfo.GetCultureInfo("fr-FR"));
  void Row(string k,Func<MatchStats,double> f,string range){var v=ok.Select(f).ToArray();double avg=v.Average(),sd=Math.Sqrt(v.Select(x=>(x-avg)*(x-avg)).Average());Console.WriteLine($"| {k} | {F(avg)} | {F(sd)} | {range} |");}
  Console.WriteLine($"Matchs : {ok.Length} (erreurs : {errs})\n");
  Console.WriteLine("| Stat par match | Moyenne | Écart-type | Fourchette |\n|---|---|---|---|");
  Row("Buts",x=>x.goals,"2,5 – 3,0");Row("xG",x=>x.xg,"~2,6");Row("Tirs",x=>x.shots,"22 – 28");Row("Tirs cadrés",x=>x.onTarget,"7 – 10");Row("Corners",x=>x.corners,"8 – 11");Row("Fautes",x=>x.fouls,"20 – 26");Row("Cartons jaunes",x=>x.yellows,"3 – 5");Row("Cartons rouges",x=>x.reds,"~0,1");Row("Penalties",x=>x.pens,"0,20 – 0,35");Row("Hors-jeu",x=>x.offsides,"3 – 5");Row("Touches",x=>x.throws,"35 – 50");Row("Sorties de but",x=>x.goalKicks,"14 – 20");Row("Coups francs",x=>x.freeKicks,"20 – 26");Row("Passes",x=>x.passes,"800 – 1000");Row("Réussite passes %",x=>x.passes>0?100.0*x.completed/x.passes:0,"75 – 87");
  var fav=ok.Where(x=>Math.Abs(x.diff)>=3).ToArray();
  if(fav.Length>0){double win=fav.Count(x=>x.diff>0?x.homeGoals>x.awayGoals:x.awayGoals>x.homeGoals)*100.0/fav.Length,draw=fav.Count(x=>x.homeGoals==x.awayGoals)*100.0/fav.Length;double poss=fav.Average(x=>x.diff>0?x.possHome:1-x.possHome)*100;Console.WriteLine($"\nFavori net (écart ≥ 3 pts, {fav.Length} matchs) : victoires {F(win)} %, nuls {F(draw)} %, possession {F(poss)} %");
   FavouriteInterval("Tous les favoris",fav);
   FavouriteInterval("Favori à domicile",fav.Where(x=>x.diff>0).ToArray());
   FavouriteInterval("Favori à l'extérieur",fav.Where(x=>x.diff<0).ToArray());
  }
  foreach(var (lo,hi) in new[]{(3f,6f),(6f,100f)}){var b=ok.Where(x=>Math.Abs(x.diff)>=lo&&Math.Abs(x.diff)<hi).ToArray();if(b.Length==0)continue;double w=b.Count(x=>x.diff>0?x.homeGoals>x.awayGoals:x.awayGoals>x.homeGoals)*100.0/b.Length,dr=b.Count(x=>x.homeGoals==x.awayGoals)*100.0/b.Length;Console.WriteLine($"  écart {lo:0}–{(hi>50?"+":hi.ToString("0"))} ({b.Length} matchs) : victoires du favori {F(w)} %, nuls {F(dr)} %");}
  Console.WriteLine($"Domicile : victoires {F(ok.Count(x=>x.homeGoals>x.awayGoals)*100.0/ok.Length)} %, nuls {F(ok.Count(x=>x.homeGoals==x.awayGoals)*100.0/ok.Length)} %");
  if(ok.Length>=2){var xgGap=new Touchline.Analysis.PairedStatistics(ok.Select(x=>(double)x.xg).ToArray(),ok.Select(x=>(double)x.goals).ToArray());
   Console.WriteLine($"Buts − xG par match : {F(xgGap.Difference)} ± {F(xgGap.HalfInterval)} (IC95 apparié approximatif). Les xG sont une estimation géométrique avant tir ; le résultat vient de la trajectoire, des interventions et du gardien, pas d'un tirage de Bernoulli selon ces xG.");}
  Console.WriteLine("\nPhases (entrées par match) : "+string.Join(", ",ok.SelectMany(x=>x.ph.Keys).Distinct().OrderBy(k=>k).Select(k=>k+" "+F(ok.Average(x=>x.ph.GetValueOrDefault(k))))));
  if(Environment.GetEnvironmentVariable("CALIB_EVENTS")=="1")Console.WriteLine("Événements par match : "+string.Join(", ",ok.SelectMany(x=>x.ev.Keys).Distinct().OrderBy(k=>k).Select(k=>k+" "+F(ok.Average(x=>x.ev.GetValueOrDefault(k))))));
  foreach(var g in r.Where(x=>x.error).GroupBy(x=>x.err).Take(5))Console.WriteLine($"Erreur x{g.Count()} : {g.Key}");
 }
 static void FavouriteInterval(string label,MatchStats[] games){
  if(games.Length==0)return;
  int wins=games.Count(x=>x.diff>0?x.homeGoals>x.awayGoals:x.awayGoals>x.homeGoals);
  var interval=new Touchline.Analysis.BinomialInterval(wins,games.Length);
  Console.WriteLine(FormattableString.Invariant($"  {label} : {wins}/{games.Length}, IC95 Wilson [{100*interval.Lower:F2} ; {100*interval.Upper:F2}] % (incertitude d'échantillonnage seulement)."));
 }
}

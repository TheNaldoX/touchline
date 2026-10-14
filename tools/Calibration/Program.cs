using System;using System.Collections.Generic;using System.IO;using System.Linq;using System.Text.Json;using System.Threading.Tasks;using Touchline.Core;
// Calibration du moteur de match Touchline hors d'Unity.
// Usage : dotnet run -c Release -- [matchs=200] [graine=1] [chemin database.json] [--variant nom]
// --variant applique une consigne (rôle, consigne individuelle, coup de pied arrêté, entraînement,
// causerie) à l'équipe qui reçoit et ajoute un tableau A/B (comparer avec --variant none).
// Compile directement les sources de Assets/Touchline/Core : toute modification du moteur est mesurée.
static class P{
 static string Variant="none";
 static readonly string[] SlotKeys={"x possession (m)","|z| possession (m)","x sans ballon (m)","tirs","buts","passes","centres","passes en profondeur","tacles+interceptions","fautes","distance (km)","pressing (s)","sorties (s)","condition finale"};
 static readonly string[] CornerKeys={"corners","tirs","buts","encaissés","cf","tirs cf","buts cf","encaissés cf"};
 sealed class V{public int slot;public Action<Tactic> tactic;public Action<MatchSimulation> prep;public Func<string,int,float,string> talk;}
 static V Role(int slot,string role)=>new V{slot=slot,tactic=t=>TacticalRoles.SetRole(t,slot,role)};
 static V Instr(int slot,string key,int value)=>new V{slot=slot,tactic=t=>TacticalRoles.Set(t.withBall[slot],key,value)};
 static V Train(Action<TrainingPlan> f)=>new V{slot=9,prep=s=>{var plan=new TrainingPlan();f(plan);Career.ApplyTraining(s,plan);}};
 // Causerie « adaptée » : le meilleur ton selon le contexte (règle simple d'un entraîneur).
 static string Smart(string moment,int diff,float gap)=>diff<0?(gap>=TeamTalks.FavouriteGap?"demand":"fire"):diff>0?"calm":moment=="prematch"?(gap<=-TeamTalks.FavouriteGap?"encourage":"fire"):gap>=TeamTalks.FavouriteGap?"demand":"encourage";
 // 4-3-3 : 0 G, 1 DG, 2-3 DC, 4 DD, 5 MC gauche, 6 MDC, 7 MC droit, 8 AG, 9 BU, 10 AD.
 static readonly Dictionary<string,V> Variants=new Dictionary<string,V>{
  {"none",new V{slot=9}},
  {"gk-sweeper",Role(0,"sweeper")},{"lb-wingback",Role(1,"wing-back")},{"lb-inverted",Role(1,"inverted")},
  {"cb-stopper",Role(2,"stopper")},{"cb-cover",Role(2,"cover")},
  {"dm-anchor",Role(6,"anchor")},{"dm-ballwinner",Role(6,"ball-winner")},{"dm-deepplaymaker",Role(6,"deep-playmaker")},
  {"cm-boxtobox",Role(5,"box-to-box")},{"cm-playmaker",Role(5,"playmaker")},
  {"lw-winger",Role(8,"winger")},{"lw-insideforward",Role(8,"inside-forward")},
  {"st-target",Role(9,"target")},{"st-poacher",Role(9,"poacher")},{"st-false9",Role(9,"false9")},
  {"lw-wide+",Instr(8,"wide",1)},{"lw-wide-",Instr(8,"wide",-1)},{"cm-forward+",Instr(5,"forward",1)},{"cm-forward-",Instr(5,"forward",-1)},
  {"cb-marking+",Instr(2,"marking",1)},{"st-shoot+",Instr(9,"shooting",1)},{"st-shoot-",Instr(9,"shooting",-1)},{"cm-risk+",Instr(5,"risk",1)},{"cm-risk-",Instr(5,"risk",-1)},
  {"corner-near",new V{slot=2,tactic=t=>t.setPieces.corner="near"}},{"corner-far",new V{slot=2,tactic=t=>t.setPieces.corner="far"}},{"corner-short",new V{slot=2,tactic=t=>t.setPieces.corner="short"}},
  {"corner-stay2",new V{slot=2,tactic=t=>t.setPieces.stayBack=2}},{"fk-shoot",new V{slot=9,tactic=t=>t.setPieces.freeKick="shoot"}},{"fk-cross",new V{slot=9,tactic=t=>t.setPieces.freeKick="cross"}},
  {"train-familiar100",Train(p=>p.familiarity=100)},{"train-familiar30",Train(p=>p.familiarity=30)},{"train-cond100",Train(p=>p.conditioning=100)},{"train-sharp100",Train(p=>p.sharpness=100)},
  {"talk-smart",new V{slot=9,talk=Smart}},{"talk-demand",new V{slot=9,talk=(m,d,g)=>"demand"}},{"talk-fire",new V{slot=9,talk=(m,d,g)=>"fire"}},
 };
 static string FindDatabase(){foreach(var start in new[]{Environment.CurrentDirectory,AppContext.BaseDirectory}){var d=new DirectoryInfo(start);while(d!=null){var f=Path.Combine(d.FullName,"unity","Touchline","Assets","Touchline","Resources","Data","database.json");if(File.Exists(f))return f;d=d.Parent;}}throw new FileNotFoundException("database.json introuvable : passez son chemin en 3e argument.");}
 static void Main(string[] a){
  var flags=a.ToList();int vi=flags.IndexOf("--variant");if(vi>=0){Variant=flags[vi+1];flags.RemoveRange(vi,2);if(!Variants.ContainsKey(Variant))throw new ArgumentException("Variante inconnue : "+Variant+". Connues : "+string.Join(", ",Variants.Keys));}a=flags.ToArray();
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
  Report(res);
 }
 class MatchStats{public int goals,shots,onTarget,corners,fouls,yellows,reds,pens,offsides,throws,passes,completed,goalKicks,freeKicks,homeGoals,awayGoals;public float possHome,diff,xg,xgHome,xgAway;public double[] slot=new double[SlotKeys.Length],corner=new double[CornerKeys.Length];public bool error;public Dictionary<string,int> ph=new Dictionary<string,int>(),ev=new Dictionary<string,int>();public string err;}
 static MatchStats Play(Database db,string home,string away,uint seed){
  var st=new MatchStats();
  try{
   var c=new Career{club=home};var v=Variants[Variant];v.tactic?.Invoke(c.tactic);c.lineup=Career.Select(db,home,c.tactic);
   var sim=MatchSimulation.Create(db,c,away,seed,2700);sim.State.professionalRules=true;
   string prev=sim.State.phase;var m=sim.State;v.prep?.Invoke(sim);
   float gap=(float)(m.actors.Take(11).Average(x=>db.Find(x.id).rating)-m.actors.Skip(11).Average(x=>db.Find(x.id).rating));
   void Talk(string moment){if(v.talk==null)return;var tone=v.talk(moment,m.score[0]-m.score[1],gap);var o=TeamTalks.Evaluate(moment,tone,m.score[0]-m.score[1],gap);sim.PreparationFor(0).talk+=o.skill;}
   int k=v.slot;var ids=new HashSet<string>();double inX=0,inZ=0,inN=0,outX=0,outN=0,dist=0,press=0,sweep=0;
   void Sample(){var p=m.actors[k];ids.Add(p.id);if(p.sentOff||m.restart>0)return;dist+=p.velocity.Length*.1;if(p.intent=="press"||p.intent=="cover")press+=.1;if(p.intent=="sweep")sweep+=.1;float x=p.position.x*sim.Direction(0);
    if(MatchSimulation.PossessionSide(m)==0){inX+=x;inZ+=Math.Abs(p.position.z);inN++;}else{outX+=x;outN++;}}
   Talk("prematch");
   void Run(){while(!m.halfTime&&!m.finished){sim.Advance(.1);Sample();if(m.phase!=prev){st.ph[m.phase]=st.ph.GetValueOrDefault(m.phase)+1;if(m.phase=="penalty")st.pens++;if(m.phase=="goal-kick")st.goalKicks++;if(m.phase=="free-kick")st.freeKicks++;prev=m.phase;}}}
   Run();Talk("halftime");sim.ResumeHalf();Run();
   int Count(params string[] kinds)=>m.events.Count(e=>e.side==0&&e.player!=null&&ids.Contains(e.player)&&kinds.Contains(e.kind));
   st.slot=new double[]{inN>0?inX/inN:0,inN>0?inZ/inN:0,outN>0?outX/outN:0,Count("shot"),Count("goal"),Count("pass","through","cross","cutback","switch"),Count("cross","cutback"),Count("through"),Count("tackle","interception"),Count("foul"),dist/1000,press,sweep,m.actors[k].fitness};
   var restarts=new HashSet<string>{"corner","throw-in","goal-kick","free-kick","penalty","setup"};
   for(int i=0;i<m.events.Count;i++){var e=m.events[i];if(e.kind!="corner"&&e.kind!="free-kick"||e.side!=0)continue;if(e.kind=="free-kick"&&e.position.x*(e.time<m.HalfDuration?1:-1)<20)continue;int o=e.kind=="corner"?0:4;st.corner[o]++;
    for(int j=i+1;j<m.events.Count;j++){var f=m.events[j];if(restarts.Contains(f.kind)||f.time>e.time+60)break;if(f.kind=="shot"&&f.side==0)st.corner[o+1]++;if(f.kind=="goal"&&f.side==0)st.corner[o+2]++;if(f.kind=="goal"&&f.side==1)st.corner[o+3]++;}}
   st.homeGoals=m.score[0];st.awayGoals=m.score[1];st.goals=m.score[0]+m.score[1];st.shots=m.shots[0]+m.shots[1];st.passes=m.passes[0]+m.passes[1];st.completed=m.completedPasses[0]+m.completedPasses[1];
   st.xgHome=m.metrics[0].xg;st.xgAway=m.metrics[1].xg;foreach(var t in m.metrics){st.onTarget+=t.shotsOnTarget;st.corners+=t.corners;st.fouls+=t.fouls;st.offsides+=t.offsides;st.throws+=t.throwIns;st.xg+=t.xg;}
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
  if(fav.Length>0){double win=fav.Count(x=>x.diff>0?x.homeGoals>x.awayGoals:x.awayGoals>x.homeGoals)*100.0/fav.Length,draw=fav.Count(x=>x.homeGoals==x.awayGoals)*100.0/fav.Length;double poss=fav.Average(x=>x.diff>0?x.possHome:1-x.possHome)*100;Console.WriteLine($"\nFavori net (écart ≥ 3 pts, {fav.Length} matchs) : victoires {F(win)} %, nuls {F(draw)} %, possession {F(poss)} %");}
  foreach(var (lo,hi) in new[]{(3f,6f),(6f,100f)}){var b=ok.Where(x=>Math.Abs(x.diff)>=lo&&Math.Abs(x.diff)<hi).ToArray();if(b.Length==0)continue;double w=b.Count(x=>x.diff>0?x.homeGoals>x.awayGoals:x.awayGoals>x.homeGoals)*100.0/b.Length,dr=b.Count(x=>x.homeGoals==x.awayGoals)*100.0/b.Length;Console.WriteLine($"  écart {lo:0}–{(hi>50?"+":hi.ToString("0"))} ({b.Length} matchs) : victoires du favori {F(w)} %, nuls {F(dr)} %");}
  Console.WriteLine($"Domicile : victoires {F(ok.Count(x=>x.homeGoals>x.awayGoals)*100.0/ok.Length)} %, nuls {F(ok.Count(x=>x.homeGoals==x.awayGoals)*100.0/ok.Length)} %");
  Console.WriteLine("\nPhases (entrées par match) : "+string.Join(", ",ok.SelectMany(x=>x.ph.Keys).Distinct().OrderBy(k=>k).Select(k=>k+" "+F(ok.Average(x=>x.ph.GetValueOrDefault(k))))));
  if(Environment.GetEnvironmentVariable("CALIB_EVENTS")=="1")Console.WriteLine("Événements par match : "+string.Join(", ",ok.SelectMany(x=>x.ev.Keys).Distinct().OrderBy(k=>k).Select(k=>k+" "+F(ok.Average(x=>x.ev.GetValueOrDefault(k))))));
  foreach(var g in r.Where(x=>x.error).GroupBy(x=>x.err).Take(5))Console.WriteLine($"Erreur x{g.Count()} : {g.Key}");
  Console.WriteLine($"\nA/B variante « {Variant} » (équipe qui reçoit, poste {Variants[Variant].slot}) :");
  double pts=ok.Average(x=>x.homeGoals>x.awayGoals?3:x.homeGoals==x.awayGoals?1:0);
  Console.WriteLine($"AB|équipe|points {F(pts)}|buts pour {F(ok.Average(x=>x.homeGoals))}|buts contre {F(ok.Average(x=>x.awayGoals))}|xG pour {F(ok.Average(x=>x.xgHome))}|xG contre {F(ok.Average(x=>x.xgAway))}|possession {F(ok.Average(x=>x.possHome)*100)}");
  Console.WriteLine("AB|poste|"+string.Join("|",SlotKeys.Select((key,i)=>key+" "+F(ok.Average(x=>x.slot[i])))));
  double Rate(int num,int den){double d=ok.Sum(x=>x.corner[den]);return d>0?ok.Sum(x=>x.corner[num])/d:0;}
  Console.WriteLine($"AB|cpa|corners {F(ok.Average(x=>x.corner[0]))}|tirs/corner {Rate(1,0).ToString("0.000")}|buts/corner {Rate(2,0).ToString("0.000")}|buts encaissés/corner {Rate(3,0).ToString("0.000")}|CF offensifs {F(ok.Average(x=>x.corner[4]))}|tirs/CF {Rate(5,4).ToString("0.000")}|buts/CF {Rate(6,4).ToString("0.000")}");
 }
}

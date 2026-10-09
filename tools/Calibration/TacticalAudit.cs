using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Touchline.Core;
using Touchline.Analysis;

static partial class P
{
    sealed class TacticalSample
    {
        public double Width, Line, Press, PassLength, Passes, Completion, Shots, Xg, Fitness, GoalsFor, GoalsAgainst;
        public double[] Values=>new[]{Width,Line,Press,PassLength,Passes,Completion,Shots,Xg,Fitness,GoalsFor,GoalsAgainst};
        public static readonly string[] Names={"Largeur (m)","Ligne (m depuis centre)","Pressing (joueur·s)","Passe moyenne (m)","Passes tentées","Passes réussies (%)","Tirs","xG","Condition finale (%)","Buts pour","Buts contre"};
        public void Read(MatchState m)
        {
            var t=m.metrics[0];Width=t.AverageWidth;Line=t.AverageLine;Press=t.pressingSeconds;
            PassLength=t.AveragePassLength(m.passes[0]);Passes=m.passes[0];Completion=Passes>0?100*m.completedPasses[0]/Passes:0;
            Shots=m.shots[0];Xg=t.xg;Fitness=m.actors.Where(x=>x.side==0&&x.slot>0).Average(x=>x.fitness);
            GoalsFor=m.score[0];GoalsAgainst=m.score[1];
        }
    }
    static void AuditTactics(string[] args)
    {
        int count=args.Length>0?int.Parse(args[0]):200;int seed=args.Length>1?int.Parse(args[1]):1;
        string output=args.Length>2?args[2]:"tactical-audit.csv";
        if(count<2)throw new ArgumentOutOfRangeException(nameof(count));
        if(File.Exists(output))throw new IOException("Le rapport existe déjà : choisissez un autre nom.");
        var db=JsonSerializer.Deserialize<Database>(File.ReadAllText(FindDatabase()),new JsonSerializerOptions{IncludeFields=true});
        db.Find(db.players[0].id);
        var leagues=db.leagues.Where(l=>!l.scoutingOnly&&l.tier==1).Select(l=>l.id).ToHashSet();
        var clubs=db.clubs.Where(c=>c.playable&&!c.reserve&&leagues.Contains(c.league)).ToArray();
        var random=new Random(seed);var fixtures=new List<(string home,string away,uint seed)>();
        while(fixtures.Count<count){var home=clubs[random.Next(clubs.Length)];var others=clubs.Where(c=>c.league==home.league&&c.id!=home.id).ToArray();if(others.Length==0)continue;fixtures.Add((home.id,others[random.Next(others.Length)].id,(uint)random.Next()));}
        // Only the indicated instruction changes. Formation and selected XI stay identical.
        var choices=new (string name,string low,string high,Action<Tactic,bool> set)[]{
            ("Mentalité","prudente","offensive",(t,h)=>t.mentality=h?.8f:.2f),
            ("Pressing","mesuré","intense",(t,h)=>t.pressing=h?.8f:.2f),
            ("Ligne","basse","haute",(t,h)=>t.line=h?.8f:.2f),
            ("Largeur","étroite","large",(t,h)=>t.width=h?.8f:.2f),
            ("Rythme","patient","rapide",(t,h)=>t.tempo=h?.8f:.2f),
            ("Passes","courtes","directes",(t,h)=>t.directness=h?.8f:.2f),
            ("Mission des milieux","défense","attaque",(t,h)=>{foreach(var s in t.withBall.Where(s=>s.role=="CM"))s.duty=h?"attack":"defend";})
        };
        using var csv=new StreamWriter(output);
        csv.WriteLine("consigne,variante,domicile,exterieur,graine,"+string.Join(",",TacticalSample.Names));
        Console.WriteLine($"Audit apparié : {count} affiches × 2 variantes par consigne, graine {seed}. Mesures du club dirigé (domicile).\nIA adverse active, aucun résultat de victoire garanti. IC 95 % indicatifs, sans correction des comparaisons multiples.\n");
        foreach(var choice in choices)
        {
            var low=new TacticalSample[count];var high=new TacticalSample[count];
            Parallel.For(0,count,new ParallelOptions{MaxDegreeOfParallelism=Environment.ProcessorCount},i=>{
                var f=fixtures[i];low[i]=new TacticalSample();high[i]=new TacticalSample();
                var a=Play(db,f.home,f.away,f.seed,t=>choice.set(t,false),low[i].Read);
                var b=Play(db,f.home,f.away,f.seed,t=>choice.set(t,true),high[i].Read);
                if(a.error||b.error)throw new InvalidOperationException($"{f.home}/{f.away} graine {f.seed}: {a.err} {b.err}");
            });
            for(int i=0;i<count;i++)foreach(var v in new[]{(choice.low,low[i]),(choice.high,high[i])})
                csv.WriteLine($"{choice.name},{v.Item1},{fixtures[i].home},{fixtures[i].away},{fixtures[i].seed},"+string.Join(",",v.Item2.Values.Select(x=>x.ToString("R",CultureInfo.InvariantCulture))));
            csv.Flush();Console.WriteLine($"### {choice.name} : {choice.low} → {choice.high}\n| Mesure | Bas | Haut | Différence ± IC95 | Sens observé |\n|---|---:|---:|---:|---|");
            for(int k=0;k<TacticalSample.Names.Length;k++){
                var stats=new PairedStatistics(low.Select(x=>x.Values[k]).ToArray(),high.Select(x=>x.Values[k]).ToArray());
                Console.WriteLine(FormattableString.Invariant($"| {TacticalSample.Names[k]} | {stats.Low:F2} | {stats.High:F2} | {stats.Difference:F2} ± {stats.HalfInterval:F2} | {stats.Verdict} |"));
            }
        }
    }
}

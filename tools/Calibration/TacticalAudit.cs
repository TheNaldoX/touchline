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
        public double Width, Line, Press, PassLength, Passes, Completion, Shots, Xg, Fitness, GoalsFor, GoalsAgainst, Crosses, ThroughAgainst, OffsidesCaught, HighRecoveries;
        public readonly ThroughPassObserver Trace=new ThroughPassObserver();
        public double[] Values=>new[]{Width,Line,Press,PassLength,Passes,Completion,Shots,Xg,Fitness,GoalsFor,GoalsAgainst,Crosses,ThroughAgainst,OffsidesCaught,HighRecoveries}.Concat(Enumerable.Range(0,ThroughPassObserver.Labels.Length).Select(i=>(double)Trace.Counts[1,i])).Concat(Enumerable.Range(0,ThroughPassObserver.FollowLabels.Length).Select(i=>(double)Trace.FollowCounts[1,i])).Concat(new[]{(double)Trace.FarReceipts[1],Trace.CoveredReceipts[1]}).ToArray();
        public static readonly string[] Names=new[]{"Largeur (m)","Ligne (m depuis centre)","Pressing (joueur·s)","Passe moyenne (m)","Passes tentées","Passes réussies (%)","Tirs","xG","Condition finale (%)","Buts pour","Buts contre","Centres","Passes en profondeur adverses","Hors-jeu adverses","Récupérations hautes"}.Concat(ThroughPassObserver.Labels.Select(label=>"Profondeur adverse : "+label)).Concat(ThroughPassObserver.FollowLabels.Select(label=>"Après réception : "+label)).Concat(new[]{"Réceptions à plus de 25m du but","Réceptions avec au moins deux défenseurs devant"}).ToArray();
        public void Read(MatchState m)
        {
            if(Trace.Attempts[1]!=m.metrics[1].throughBalls||Enumerable.Range(0,ThroughPassObserver.Labels.Length).Sum(i=>Trace.Counts[1,i])!=Trace.Attempts[1])throw new InvalidOperationException("Unclassified through passes in audit.");
            if(Enumerable.Range(0,ThroughPassObserver.FollowLabels.Length).Sum(i=>Trace.FollowCounts[1,i])!=Trace.Counts[1,0])throw new InvalidOperationException("Unclassified reception followups in audit.");
            var t=m.metrics[0];Width=t.AverageWidth;Line=t.AverageLine;Press=t.pressingSeconds;
            PassLength=t.AveragePassLength(m.passes[0]);Passes=m.passes[0];Completion=Passes>0?100*m.completedPasses[0]/Passes:0;
            Shots=m.shots[0];Xg=t.xg;Fitness=m.actors.Where(x=>x.side==0&&x.slot>0).Average(x=>x.fitness);
            GoalsFor=m.score[0];GoalsAgainst=m.score[1];
            Crosses=t.crosses;ThroughAgainst=m.metrics[1].throughBalls;OffsidesCaught=m.metrics[1].offsides;HighRecoveries=t.highRecoveries;
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
        // Etat mental et automatismes (club dirigé seulement), valeurs de TeamMindset (−1 à +1).
        static Action<MatchSimulation> Mind(Action<TeamMindset> set)=>sim=>{var ours=TeamMindset.Neutral();set(ours);sim.State.mindset=new[]{ours,TeamMindset.Neutral()};};
        static Action<Tactic,bool> Instruct(string instruction,params string[] roles)=>(t,h)=>{for(int i=1;i<11;i++)if(roles.Contains(t.withoutBall[i].role))t.withoutBall[i].instruction=h?instruction:"";};
        var mental=new (string name,string low,string high,Action<bool,TeamMindset> set)[]{
            ("Compréhension","nouveau système","automatismes rodés",(h,m)=>m.understanding=h?.3f:-.6f),
            ("Sang-froid","groupe nerveux","groupe serein",(h,m)=>{for(int i=0;i<11;i++)m.composure[i]=h?.3f:-.4f;}),
            ("Engagement","groupe démobilisé","groupe remonté",(h,m)=>{for(int i=0;i<11;i++)m.drive[i]=h?.4f:-.3f;}),
        };
        (string name,string low,string high,Action<Tactic,bool> set,Func<bool,Action<MatchSimulation>> prepare)[] choices=new (string name,string low,string high,Action<Tactic,bool> set,Func<bool,Action<MatchSimulation>> prepare)[]{
            ("Mentalité","prudente","offensive",(t,h)=>t.mentality=h?.8f:.2f,null),
            ("Pressing","mesuré","intense",(t,h)=>t.pressing=h?.8f:.2f,null),
            ("Ligne","basse","haute",(t,h)=>t.line=h?.8f:.2f,null),
            ("Largeur","étroite","large",(t,h)=>t.width=h?.8f:.2f,null),
            ("Rythme","patient","rapide",(t,h)=>t.tempo=h?.8f:.2f,null),
            ("Passes","courtes","directes",(t,h)=>t.directness=h?.8f:.2f,null),
            ("Mission des milieux","défense","attaque",(t,h)=>{foreach(var s in t.withBall.Where(s=>s.role=="CM"))s.duty=h?"attack":"defend";},null),
            ("Latéraux projetés","non","oui",Instruct(PlayerInstructions.GetForward,"LB","RB"),null),
            ("Ailiers larges","non","oui",Instruct(PlayerInstructions.StayWide,"LW","RW"),null),
            ("Marquage serré","non","oui",Instruct(PlayerInstructions.TightMarking,"LB","CB","RB"),null)
        }.Concat(mental.Select(x=>(x.name,x.low,x.high,(Action<Tactic,bool>)((t,h)=>{}),(Func<bool,Action<MatchSimulation>>)(h=>Mind(m=>x.set(h,m)))))).ToArray();
        // Optional selection keeps identical fixture seeds for focused before/after audits.
        if(args.Length>3){
            var requested=args[3].Split(',');
            if(requested.Any(name=>!choices.Any(c=>string.Equals(c.name,name,StringComparison.OrdinalIgnoreCase))))
                throw new ArgumentException("Consigne inconnue. Utilisez : "+string.Join(", ",choices.Select(c=>c.name)));
            choices=choices.Where(c=>requested.Any(name=>string.Equals(c.name,name,StringComparison.OrdinalIgnoreCase))).ToArray();
        }
        using var csv=new StreamWriter(output);
        csv.WriteLine("consigne,variante,domicile,exterieur,graine,"+string.Join(",",TacticalSample.Names));
        Console.WriteLine($"Audit apparié : {count} affiches × 2 variantes par consigne, graine {seed}. Mesures du club dirigé (domicile).\nIA adverse active, aucun résultat de victoire garanti. IC 95 % indicatifs, sans correction des comparaisons multiples.\n");
        foreach(var choice in choices)
        {
            var low=new TacticalSample[count];var high=new TacticalSample[count];
            Parallel.For(0,count,new ParallelOptions{MaxDegreeOfParallelism=Environment.ProcessorCount},i=>{
                var f=fixtures[i];low[i]=new TacticalSample();high[i]=new TacticalSample();
                var a=Play(db,f.home,f.away,f.seed,t=>choice.set(t,false),low[i].Read,choice.prepare?.Invoke(false),low[i].Trace.Sample);
                var b=Play(db,f.home,f.away,f.seed,t=>choice.set(t,true),high[i].Read,choice.prepare?.Invoke(true),high[i].Trace.Sample);
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

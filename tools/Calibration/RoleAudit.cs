using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Touchline.Core;
using Touchline.Analysis;

// Audit apparié des rôles, consignes de frappe/risque et combinaisons de coups de pied arrêtés :
// dotnet run -c Release -- --roles 200 1 [variante,variante…]
// Les mêmes affiches et graines sont jouées une fois sans changement (référence), puis une fois
// par variante appliquée au club qui reçoit (4-3-3). Mesures du joueur au poste concerné et de l'équipe.
static partial class P
{
    sealed class RoleSample
    {
        public readonly double[] InX=new double[11],InZ=new double[11],InN=new double[11],OutX=new double[11],OutN=new double[11],Distance=new double[11],Press=new double[11],Sweep=new double[11];
        public readonly HashSet<string>[] Ids=Enumerable.Range(0,11).Select(_=>new HashSet<string>()).ToArray();
        public double[] Team=new double[0];public double[][] Slot=new double[11][];public double[] SetPieces=new double[0];
        public void Sample(MatchState m)
        {
            if(m.restart>0)return;int dir=m.period==2?-1:1;bool ours=MatchSimulation.PossessionSide(m)==0;
            for(int k=0;k<11;k++){var p=m.actors[k];Ids[k].Add(p.id);if(p.sentOff)continue;Distance[k]+=p.velocity.Length*.1;if(p.intent=="press"||p.intent=="cover")Press[k]+=.1;if(p.intent=="sweep")Sweep[k]+=.1;
                float x=p.position.x*dir;if(ours){InX[k]+=x;InZ[k]+=Math.Abs(p.position.z);InN[k]++;}else{OutX[k]+=x;OutN[k]++;}}
        }
        public void Read(MatchState m)
        {
            Team=new double[]{m.score[0]>m.score[1]?3:m.score[0]==m.score[1]?1:0,m.score[0],m.score[1],m.metrics[0].xg,m.metrics[1].xg,m.shots[0]};
            for(int k=0;k<11;k++){var ids=Ids[k];int Count(params string[] kinds)=>m.events.Count(e=>e.side==0&&e.player!=null&&ids.Contains(e.player)&&kinds.Contains(e.kind));
                Slot[k]=new double[]{InN[k]>0?InX[k]/InN[k]:0,InN[k]>0?InZ[k]/InN[k]:0,OutN[k]>0?OutX[k]/OutN[k]:0,Count("shot"),Count("goal"),Count("pass","through","cross","cutback","switch"),Count("cross","cutback"),Count("through"),Count("tackle","interception"),Count("foul"),Distance[k]/1000,Press[k],Sweep[k],m.actors[k].fitness};}
            // Séquences après un corner ou un coup franc offensif du club : jusqu'au prochain arrêt de jeu (60 s au plus).
            var restarts=new HashSet<string>{"corner","throw-in","goal-kick","free-kick","penalty","setup"};var sp=new double[8];
            for(int i=0;i<m.events.Count;i++){var e=m.events[i];if(e.kind!="corner"&&e.kind!="free-kick"||e.side!=0)continue;if(e.kind=="free-kick"&&e.position.x*(e.time<m.HalfDuration?1:-1)<20)continue;int o=e.kind=="corner"?0:4;sp[o]++;
                for(int j=i+1;j<m.events.Count;j++){var f=m.events[j];if(restarts.Contains(f.kind)||f.time>e.time+60)break;if(f.kind=="shot"&&f.side==0)sp[o+1]++;if(f.kind=="goal"&&f.side==0)sp[o+2]++;if(f.kind=="goal"&&f.side==1)sp[o+3]++;}}
            SetPieces=sp;
        }
    }
    static readonly string[] TeamNames={"Points","Buts pour","Buts contre","xG pour","xG contre","Tirs"};
    static readonly string[] SlotNames={"x en possession (m)","|z| en possession (m)","x sans ballon (m)","Tirs du joueur","Buts du joueur","Passes du joueur","Centres du joueur","Passes en profondeur","Tacles+interceptions","Fautes","Distance (km)","Pressing (s)","Sorties (s)","Condition finale"};
    static readonly string[] SetPieceNames={"Corners","Tirs après corner","Buts après corner","Encaissés après corner","Coups francs offensifs","Tirs après coup franc","Buts après coup franc","Encaissés après coup franc"};
    // 4-3-3 : 0 G, 1 DG, 2-3 DC, 4 DD, 5 MC gauche, 6 MDC, 7 MC droit, 8 AG, 9 BU, 10 AD.
    static (int slot,Action<Tactic> set,bool setPiece) Variant(string name)
    {
        (int,Action<Tactic>,bool) Role(int slot,string role)=>(slot,t=>TacticalRoles.SetRole(t,slot,role),false);
        (int,Action<Tactic>,bool) Instr(int slot,string key,int value)=>(slot,t=>TacticalRoles.Set(t.withoutBall[slot],key,value),false);
        switch(name){
            case "gk-sweeper":return Role(0,"sweeper");case "lb-wingback":return Role(1,"wing-back");case "lb-inverted":return Role(1,"inverted");
            case "cb-stopper":return Role(2,"stopper");case "cb-cover":return Role(2,"cover");
            case "dm-anchor":return Role(6,"anchor");case "dm-ballwinner":return Role(6,"ball-winner");case "dm-deepplaymaker":return Role(6,"deep-playmaker");
            case "cm-boxtobox":return Role(5,"box-to-box");case "cm-playmaker":return Role(5,"playmaker");
            case "lw-winger":return Role(8,"winger");case "lw-insideforward":return Role(8,"inside-forward");
            case "st-target":return Role(9,"target");case "st-poacher":return Role(9,"poacher");case "st-false9":return Role(9,"false9");
            case "st-shoot+":return Instr(9,"shooting",1);case "st-shoot-":return Instr(9,"shooting",-1);case "cm-risk+":return Instr(5,"risk",1);case "cm-risk-":return Instr(5,"risk",-1);
            case "corner-near":return (2,t=>t.setPieces.corner="near",true);case "corner-far":return (2,t=>t.setPieces.corner="far",true);case "corner-short":return (2,t=>t.setPieces.corner="short",true);
            case "corner-stay2":return (2,t=>t.setPieces.stayBack=2,true);case "fk-shoot":return (9,t=>t.setPieces.freeKick="shoot",true);case "fk-cross":return (9,t=>t.setPieces.freeKick="cross",true);
            default:throw new ArgumentException("Variante inconnue : "+name);
        }
    }
    static void AuditRoles(string[] args)
    {
        int count=args.Length>0?int.Parse(args[0]):200;int seed=args.Length>1?int.Parse(args[1]):1;
        var names=(args.Length>2?args[2]:"st-shoot+,lw-insideforward,st-false9,corner-near").Split(',');foreach(var n in names)Variant(n);
        var db=JsonSerializer.Deserialize<Database>(File.ReadAllText(FindDatabase()),new JsonSerializerOptions{IncludeFields=true});db.Find(db.players[0].id);
        var leagues=db.leagues.Where(l=>!l.scoutingOnly&&l.tier==1).Select(l=>l.id).ToHashSet();
        var clubs=db.clubs.Where(c=>c.playable&&!c.reserve&&leagues.Contains(c.league)).ToArray();
        var random=new Random(seed);var fixtures=new List<(string home,string away,uint seed)>();
        while(fixtures.Count<count){var home=clubs[random.Next(clubs.Length)];var others=clubs.Where(c=>c.league==home.league&&c.id!=home.id).ToArray();if(others.Length==0)continue;fixtures.Add((home.id,others[random.Next(others.Length)].id,(uint)random.Next()));}
        RoleSample[] Run(Action<Tactic> set){var r=new RoleSample[count];Parallel.For(0,count,new ParallelOptions{MaxDegreeOfParallelism=Environment.ProcessorCount},i=>{var f=fixtures[i];r[i]=new RoleSample();var s=Play(db,f.home,f.away,f.seed,set,r[i].Read,null,r[i].Sample);if(s.error)throw new InvalidOperationException($"{f.home}/{f.away} graine {f.seed}: {s.err}");});return r;}
        Console.WriteLine($"Audit apparié des rôles : {count} affiches, graine {seed}, club qui reçoit en 4-3-3. IC 95 % indicatifs.\n");
        var baseline=Run(null);
        foreach(var name in names){
            var v=Variant(name);var after=Run(v.set);
            Console.WriteLine($"### {name} (poste {v.slot})\n| Mesure | Référence | Variante | Différence ± IC95 | Sens |\n|---|---:|---:|---:|---|");
            void Row(string label,Func<RoleSample,double> f){var st=new PairedStatistics(baseline.Select(f).ToArray(),after.Select(f).ToArray());Console.WriteLine(FormattableString.Invariant($"| {label} | {st.Low:F2} | {st.High:F2} | {st.Difference:F2} ± {st.HalfInterval:F2} | {st.Verdict} |"));}
            for(int k=0;k<SlotNames.Length;k++){int kk=k;Row(SlotNames[k],x=>x.Slot[v.slot][kk]);}
            for(int k=0;k<TeamNames.Length;k++){int kk=k;Row("Équipe : "+TeamNames[k],x=>x.Team[kk]);}
            if(v.setPiece)for(int k=0;k<SetPieceNames.Length;k++){int kk=k;Row(SetPieceNames[k],x=>x.SetPieces[kk]);}
            Console.WriteLine();
        }
    }
}

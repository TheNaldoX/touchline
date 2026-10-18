using System.Text.Json;
using System.Security.Cryptography;
using Touchline.Core;

if(args.Length!=2)throw new ArgumentException("Usage: MotionMatchingTrace database.json output.json");
if(File.Exists(args[1]))throw new IOException("Trace already exists; use a distinct proof path.");
var options=new JsonSerializerOptions{IncludeFields=true};
var db=JsonSerializer.Deserialize<Database>(File.ReadAllText(args[0]),options);
var home=db.clubs.First(c=>c.id=="176");
var away=db.clubs.Where(c=>c.league==home.league&&c.id!=home.id&&c.playable&&!c.reserve).OrderBy(c=>c.id,StringComparer.Ordinal).First();
var career=new Career{club=home.id};career.lineup=Career.Select(db,home.id,career.tactic);
// Export native simulation ticks, not repeated copies at render rate.
int hz=(int)Math.Round(1.0/MatchSimulation.Step);const int seconds=26;const uint seed=1;
var sim=MatchSimulation.Create(db,career,away.id,seed,2700);sim.State.professionalRules=true;
for(int i=0;i<45*hz;i++)sim.Advance(1.0/hz);
var trace=new Trace{hz=hz,seed=seed,home=home.id,away=away.id,startClock=sim.State.clock,
    databaseSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(args[0]))),
    tracks=sim.State.actors.Select(a=>new Track{id=a.id,side=a.side,slot=a.slot}).ToArray()};
for(int frame=0;frame<=seconds*hz;frame++){
    for(int i=0;i<trace.tracks.Length;i++){
        var a=sim.State.actors[i];
        if(!float.IsFinite(a.position.x)||!float.IsFinite(a.position.z)||!float.IsFinite(a.angle))throw new Exception("Non-finite engine sample");
        trace.tracks[i].samples.Add(new Sample{x=a.position.x,z=a.position.z,angle=a.angle,action=a.action});
    }
    if(frame<seconds*hz)sim.Advance(1.0/hz);
}
File.WriteAllText(args[1],JsonSerializer.Serialize(trace,options));
Console.WriteLine($"Exported {trace.tracks.Length} real simulated actors, {seconds}s at {hz}Hz; seed {seed}; source clock {trace.startClock:F2}s.");
Console.WriteLine("SHA256 "+Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(args[1]))));
public class Trace {public int hz;public uint seed;public string home,away,databaseSha256;public float startClock;public Track[] tracks;}
public class Track {public string id;public int side,slot;public List<Sample> samples=new();}
public class Sample {public float x,z,angle;public string action;}

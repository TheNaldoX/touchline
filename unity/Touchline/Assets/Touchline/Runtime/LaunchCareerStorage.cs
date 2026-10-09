using System;
using System.IO;
using System.Linq;
using UnityEngine;
using Touchline.Core;

namespace Touchline
{
    public sealed class LaunchCareerEntry
    {
        public string Path,Label,Error;
        public bool Exists,Valid,CanAttempt,CalendarMigrated;
        public string Club,Manager,MatchAway;public int Day,MatchMinute;public int[] Score;public bool HasMatch,MatchFinished,MatchHalfTime;
        public DateTime WrittenUtc;
        public Career State;
        public Database Restored;
    }

    // Reads and trial restoration never publish a world or repair a file.
    public static class LaunchCareerStorage
    {
        [Serializable] sealed class HeaderLife { public int day; }
        [Serializable] sealed class HeaderMatch { public string home,away;public float clock;public int periodSeconds;public bool finished,halfTime;public int[] score; }
        [Serializable] sealed class Header { public int schema;public string club,manager,calendarEpoch;public string[] lineup;public Tactic tactic;public HeaderLife life;public HeaderMatch match; }
        public static LaunchCareerEntry ReadMetadata(string path,string label,Database catalogue)
        {
            var entry=new LaunchCareerEntry{Path=path,Label=label,Exists=File.Exists(path)};if(!entry.Exists)return entry;
            try{
                entry.WrittenUtc=File.GetLastWriteTimeUtc(path);string raw=File.ReadAllText(path);var h=JsonUtility.FromJson<Header>(raw);
                if(h==null||h.schema!=1||h.lineup?.Length!=11||h.lineup.Distinct().Count()!=11||!TacticValid(h.tactic)||!catalogue.clubs.Any(c=>c.id==h.club))throw new InvalidDataException();
                if(h.life!=null&&(h.life.day<0||h.life.day>365000))throw new InvalidDataException();
                entry.Club=h.club;entry.Manager=h.manager;entry.Day=(h.life?.day??0)+(string.IsNullOrEmpty(h.calendarEpoch)?67:0);
                entry.HasMatch=h.match!=null&&!string.IsNullOrEmpty(h.match.home)&&!string.IsNullOrEmpty(h.match.away);entry.MatchAway=h.match?.away;entry.MatchFinished=h.match?.finished??false;entry.MatchHalfTime=h.match?.halfTime??false;entry.Score=h.match?.score;
                entry.MatchMinute=h.match==null?0:Math.Min(90,Math.Max(0,(int)((h.match.clock+.001f)/((h.match.periodSeconds==2700?2700:360)/45f))));
                entry.CanAttempt=true; // Header only: full world validation occurs at the explicit load.
            }catch(Exception){entry.Error="Ce fichier ne peut pas être repris. Il est conservé.";}
            return entry;
        }
        static bool Finite(float x)=>!float.IsNaN(x)&&!float.IsInfinity(x);
        static bool TacticValid(Tactic tactic)=>tactic?.withBall?.Length==11&&tactic.withoutBall?.Length==11
            &&tactic.withBall.Concat(tactic.withoutBall).All(s=>s!=null&&Finite(s.x)&&Finite(s.y));
        public static LaunchCareerEntry Read(string path,string label,Database original)
        {
            var entry=new LaunchCareerEntry{Path=path,Label=label,Exists=File.Exists(path)};
            if(!entry.Exists)return entry;
            try{
                entry.WrittenUtc=File.GetLastWriteTimeUtc(path);
                string raw=File.ReadAllText(path);
                // JsonUtility tolerates missing fields: a random object is not a career.
                if(!raw.TrimStart().StartsWith("{")||!raw.Contains("\"lineup\"")||!raw.Contains("\"club\""))throw new InvalidDataException();
                var state=JsonUtility.FromJson<Career>(raw);
                if(state==null||!TacticValid(state.tactic))throw new InvalidDataException();
                if(state.world!=null&&(state.world.divisions==null||state.world.divisions.Count==0))state.world=null;
                var compatible=TouchlineCatalogue.ForSavedCareer(original,state);
                if(!CareerSaveRestore.TryRestore(compatible,state,out var restored))throw new InvalidDataException();
                if(state.life!=null&&(state.life.day<0||state.life.day>365000))throw new InvalidDataException();
                // Unity can instantiate an empty object for a serialized null class.
                // Normalize that compatible placeholder before checking a real match.
                var match=state.match;
                if(match!=null&&(match.actors==null||match.actors.Length==0)&&string.IsNullOrEmpty(match.home)&&string.IsNullOrEmpty(match.away)&&match.clock==0)state.match=null;
                else if(match!=null&&match.actors?.Length!=22)throw new InvalidDataException();
                state.BindMatchTactic();match=state.match;
                if(match!=null){
                    // Missing legacy statistical counters are reparable; malformed arrays are not.
                    if(match.shots==null||match.shots.Length==0)match.shots=new int[2];if(match.passes==null||match.passes.Length==0)match.passes=new int[2];if(match.completedPasses==null||match.completedPasses.Length==0)match.completedPasses=new int[2];if(match.substitutions==null||match.substitutions.Length==0)match.substitutions=new int[2];
                    if(match.metrics==null||match.metrics.Length==0)match.metrics=new[]{new TeamMetrics(),new TeamMetrics()};
                    if(match.metrics.Length==2)for(int i=0;i<2;i++)match.metrics[i]??=new TeamMetrics();
                    match.events??=new System.Collections.Generic.List<MatchEvent>();
                    match.used??=match.actors?.Select(a=>a.id).ToList();
                    match.homeWindows??=new System.Collections.Generic.List<float>();match.awayWindows??=new System.Collections.Generic.List<float>();
                    if(match.actors?.Length!=22||match.ball==null||match.score?.Length!=2||match.shots.Length!=2||match.passes.Length!=2||match.completedPasses.Length!=2||match.substitutions.Length!=2||match.metrics.Length!=2
                        ||!Finite(match.ball.position.x)||!Finite(match.ball.position.z)||!Finite(match.ball.previous.x)||!Finite(match.ball.previous.z)||!Finite(match.ball.height)||!Finite(match.ball.previousHeight)
                        ||!Finite(match.ball.duration)||!Finite(match.ball.elapsed)||!Finite(match.ball.loft)
                        ||!TacticValid(match.awayTactic)||match.period<1||match.period>2
                        ||!Finite(match.clock)||match.clock<0||match.clock>match.HalfDuration*2+.1f
                        ||!restored.clubs.Any(c=>c.id==match.away)||match.home!=state.club
                        ||match.actors.Any(a=>a==null||restored.Find(a.id)==null||a.side<0||a.side>1||a.slot<0||a.slot>10||!Finite(a.position.x)||!Finite(a.position.z))
                        ||match.actors.Select(a=>a.id).Distinct().Count()!=22)throw new InvalidDataException();
                }
                entry.CalendarMigrated=!raw.Contains("\"calendarEpoch\"");
                if(entry.CalendarMigrated){state.MigrateSummerEpoch();state.SynchronizePlayerAges(restored);}
                state.saveBaseline=SaveBaseline.From(compatible);
                entry.State=state;entry.Restored=restored;entry.Valid=true;entry.CanAttempt=true;
            }catch(Exception error){entry.Error="Ce fichier ne peut pas être repris. Il est conservé.";Debug.LogWarning("Lecture de carrière refusée : "+error.GetType().Name);}
            return entry;
        }
        public static void Write(string path,string json,bool primaryValidated)
        {
            if(string.IsNullOrWhiteSpace(json))throw new ArgumentException("Une carrière est requise.");
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            string temporary=path+".tmp";
            File.WriteAllText(temporary,json);
            if(!File.Exists(path)){File.Move(temporary,path);return;}
            // A damaged primary must not replace the only readable backup.
            string prior=primaryValidated?path+".backup":path+".invalid-"+DateTime.UtcNow.Ticks;
            File.Replace(temporary,path,prior);
        }
    }
}

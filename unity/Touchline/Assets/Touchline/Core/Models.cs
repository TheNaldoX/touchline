using System;
using System.Collections.Generic;
using System.Linq;

namespace Touchline.Core
{
    [Serializable] public class AttributeValue { public string key; public float value; }
    [Serializable] public class PlayerIdentityEvidence { public string birthDate; }
    [Serializable] public class PlayerData
    {
        public string id, name, team, position, nationality, preferredFoot, photo,source,assessment,salarySource,valueSource,rosterSource,rosterAsOf;
        public string surname,givenNames,birthDate;
        public PlayerIdentityEvidence evidence;
        public string[] positions;
        public int age, number, unavailableDays,heightCm,weightKg;
        public string physiqueSource;
        public float development, performanceModifier;
        public float rating, potential, fitness = 100, morale = 75;
        public long value, wage;
        public AttributeValue[] attributes;
        public PlayerData Copy()
        {
            var copy=(PlayerData)MemberwiseClone();
            copy.positions=positions?.ToArray();
            copy.attributes=attributes?.Select(a=>a==null?null:new AttributeValue{key=a.key,value=a.value}).ToArray();
            copy.evidence=evidence==null?null:new PlayerIdentityEvidence{birthDate=evidence.birthDate};
            return copy;
        }
        public float Attribute(string key)
        {
            if (attributes != null) foreach (var a in attributes) if (a.key == key) return Math.Min(99,a.value+development);
            return Math.Min(99,rating+development);
        }
        public bool Goalkeeper => FootballPositions.Matches(this, "GK");
        public float Fit(string role)
        {
            // Compare aliases at the boundary without rewriting imported or saved positions.
            role = FootballPositions.Canonical(role);
            if (!string.IsNullOrEmpty(role) && (FootballPositions.Canonical(position) == role || (positions?.Any(p => FootballPositions.Canonical(p) == role) ?? false))) return 1;
            if (Goalkeeper || role == "GK") return .25f;
            var group = role == "ST" || role == "LW" || role == "RW" ? "ATT" : role == "CB" || role == "LB" || role == "RB" ? "DEF" : "MIL";
            return position == group ? .86f : .65f;
        }
    }
    [Serializable] public class ClubData { public string id, name, league, country,countrySource,countryAsOf, color, logo, financeSource,stadium,stadiumSource,capacitySource,sourceSeason,rosterSource,rosterAsOf,referenceSeason,referenceFinanceSource; public long annualRevenue,referenceRevenue;public int stadiumCapacity; public bool playable,reserve; public ClubData Copy()=>(ClubData)MemberwiseClone(); }
    [Serializable] public class LeagueData { public string id, name, country, format, calendarSource,rulesSource,rulesNote; public int tier=1; public bool scoutingOnly; }
    [Serializable] public class Database
    {
        public int schema;
        public string importedAt, provenance;
        public PlayerData[] players;
        public ClubData[] clubs;
        public LeagueData[] leagues;
        public Fixture[] fixtures;
        public PyramidRule[] pyramidRules;
        public FreeAgentReference[] freeAgents;public int freeAgentCatalogVersion;
        [NonSerialized] PlayerData[] indexedPlayers;
        [NonSerialized] Dictionary<string,int> playerIndex;
        public PlayerData Find(string id)
        {
            if(id==null||players==null)return null;
            if(playerIndex==null||!ReferenceEquals(indexedPlayers,players))IndexPlayers();
            if(playerIndex.TryGetValue(id,out var at)&&players[at]?.id==id)return players[at];
            // Support replacement inside an existing array too. Career growth
            // normally replaces the array, which rebuilds this derived index.
            for(int i=0;i<players.Length;i++)if(players[i]?.id==id){playerIndex[id]=i;return players[i];}
            return null;
        }
        void IndexPlayers()
        {
            indexedPlayers=players;playerIndex=new Dictionary<string,int>(players.Length,StringComparer.Ordinal);
            for(int i=0;i<players.Length;i++)if(players[i]?.id!=null&&!playerIndex.ContainsKey(players[i].id))playerIndex.Add(players[i].id,i);
        }
        public List<PlayerData> Squad(string club) => players.Where(p => p.team == club).ToList();
    }
    [Serializable] public struct Point
    {
        public float x, z;
        public Point(float x, float z) { this.x = x; this.z = z; }
        public float Length => (float)Math.Sqrt(x * x + z * z);
        public Point Normalized => Length > .0001f ? this / Length : new Point();
        public static Point operator +(Point a, Point b) => new Point(a.x + b.x, a.z + b.z);
        public static Point operator -(Point a, Point b) => new Point(a.x - b.x, a.z - b.z);
        public static Point operator *(Point a, float b) => new Point(a.x * b, a.z * b);
        public static Point operator /(Point a, float b) => new Point(a.x / b, a.z / b);
        public static float Dot(Point a, Point b) => a.x * b.x + a.z * b.z;
        public static float Distance(Point a, Point b) => (a - b).Length;
        public static Point Lerp(Point a, Point b, float t) => a + (b - a) * t;
    }
    [Serializable] public class Slot { public string role, duty="support"; public float x, y;
        // Consigne individuelle (PlayerInstructions) : lue sur le poste sans ballon. Vide = aucune.
        public string instruction="";
        public Slot(string r, float x, float y) { role = r; this.x = x; this.y = y; } }
    [Serializable] public class Tactic
    {
        public string formation = "4-3-3";
        public float width = .65f, line = .45f, tempo = .5f, pressing = .5f, directness = .5f;
        public float defensiveWidth=.45f, mentality=.5f;
        public bool counterPress, counterAttack, workIntoBox;
        public string attackFocus="balanced",crossing="mixed";
        public Slot[] withBall, withoutBall;
        public Tactic() { SetFormation("4-3-3"); }
        public void SetFormation(string shape)
        {
            formation = shape;
            withoutBall = shape == "4-4-2" ? new[] {new Slot("GK",50,5),new Slot("LB",13,28),new Slot("CB",37,25),new Slot("CB",63,25),new Slot("RB",87,28),new Slot("LM",14,53),new Slot("CM",38,49),new Slot("CM",62,49),new Slot("RM",86,53),new Slot("ST",35,79),new Slot("ST",65,79)} : new[] {new Slot("GK",50,5),new Slot("LB",13,28),new Slot("CB",37,25),new Slot("CB",63,25),new Slot("RB",87,28),new Slot("CM",25,52),new Slot("DM",50,43),new Slot("CM",75,52),new Slot("LW",15,78),new Slot("ST",50,81),new Slot("RW",85,78)};
            withBall = withoutBall.Select((s,i) => new Slot(s.role,s.x,Math.Min(91,s.y+(i==0?2:i==1||i==4?17:9)))).ToArray();
            if(shape=="4-2-3-1")withoutBall=new[]{new Slot("GK",50,5),new Slot("LB",13,28),new Slot("CB",37,25),new Slot("CB",63,25),new Slot("RB",87,28),new Slot("DM",35,43),new Slot("DM",65,43),new Slot("LW",15,66),new Slot("AM",50,64),new Slot("RW",85,66),new Slot("ST",50,82)};
            if(shape=="3-4-2-1")withoutBall=new[]{new Slot("GK",50,5),new Slot("CB",24,25),new Slot("CB",50,24),new Slot("CB",76,25),new Slot("LM",8,48),new Slot("CM",36,46),new Slot("CM",64,46),new Slot("RM",92,48),new Slot("AM",32,68),new Slot("AM",68,68),new Slot("ST",50,83)};
            withBall=withoutBall.Select((s,i)=>new Slot(s.role,s.x,Math.Min(91,s.y+(i==0?2:s.role=="LB"||s.role=="RB"?17:9))){duty=s.role=="CB"||s.role=="DM"?"defend":s.role=="ST"?"attack":"support"}).ToArray();
        }
        public Point Position(int slot, bool possession, float ballX)
        {
            var p = (possession ? withBall : withoutBall)[slot];
            var baseX=-52.5f+p.y*1.05f;
            var x=possession?baseX+ballX*.38f+(mentality-.5f)*12:baseX*.57f-10+(line-.45f)*27+ballX*.24f;
            if(possession&&slot>0)x=Math.Min(x,ballX+24);
            // Board left is the player's left while facing the opposing goal.
            // Local attack is +X, whose left-hand flank is world +Z. Rotate
            // both axes once for the opposing team and the second half.
            return new Point(Mathx.Clamp(slot==0?-48:x,-49,48),(50-p.x)*.68f*(possession?.48f+width*.8f:.52f+defensiveWidth*.48f));
        }
    }
    public static class Mathx { public static float Clamp(float v,float a,float b) => Math.Max(a,Math.Min(b,v)); }
    [Serializable] public partial class Career
    {
        public const int ImportedRosterYear=2026;
        // Zero preserves legacy careers; new imported careers start intakes next season.
        public int youthGenerationFromYear;
        public int schema = 1;
        public string manager = "Victor", club = "176";
        public Tactic tactic = new Tactic();
        public string[] lineup;
        public MatchState match;
        public bool revealAttributes;
        public void BindMatchTactic(){if(match!=null&&(match.actors==null||match.actors.Length!=22))match=null;if(match!=null)match.homeTactic=tactic;}
        public static string[] Select(Database db, string club, Tactic tactic)
        {
            var available = db.Squad(club); var ids = new List<string>();
            foreach(var slot in tactic.withoutBall) { var p=available.OrderByDescending(x=>x.rating*x.Fit(slot.role)*(.65f+x.fitness/285)).FirstOrDefault();if(p==null)throw new InvalidOperationException("Effectif insuffisant.");ids.Add(p.id);available.Remove(p); }
            return ids.ToArray();
        }
    }
    [Serializable] public class Actor
    {
        public float keeperLaunchRemaining;
        public bool sentOff,injured;public int yellows;
        public string id; public int side, slot; public Point position, velocity, previous;
        public float angle, stride, actionTime, fitness=100;
        public float duelCooldown, controlTime, diveSide=1;
        public float runBehind; // secondes restantes d'un appel en profondeur
        public Point carryTarget,actionTarget;public float actionHeight=.11f,actionContactTime;public int actionSequence;
        public string actionKind;
        public string tackleOpponent;
        public float tackleWithdrawFrom;
        public string intent="shape";
        public string action="idle";
    }
    [Serializable] public class BallState
    {
        public Point position, previous, start, end, setupStart, velocity, controlOrigin;
        public float verticalVelocity, controlElapsed, controlDuration;
        public float height=.11f, previousHeight=.11f, startHeight=.11f, endHeight=.11f, elapsed, duration, loft, setupHeight=.11f, releaseDelay=.18f;
        public string owner, from, to, lastTouchId, kind="none";
        public int offsidePlayersMask;
        public int side, lastTouch; public bool offside, penalty, restartExemption, directThrow, passEligible,fixedStart,held,keeperDistribution;
        public bool goalAttempt, shotOnTarget, onTargetCounted,saveCredited;
        public int shotSide,saveSide;
    }
    [Serializable] public class MatchEvent { public string kind, player, receiver, text; public float time, xg; public int side; public Point position; public bool hasOutgoingFitness; public float observedOutgoingFitness; }
    [Serializable] public class PendingSubstitution { public int side;public string outgoing,incoming; }
    [Serializable] public class TeamMetrics
    {
        public float possessionSeconds, passDistance, forwardPassDistance, widthSum, lineSum, shapeSamples, defensiveSamples, pressingSeconds, distanceRun, xg, possessionChain, chainSeconds, transitionProgress, transitionSeconds;
        public int recoveries, highRecoveries, counters, crosses, throughBalls, longPasses, shotsOnTarget, saves, corners, throwIns, fouls, offsides, pressuredLosses, miscontrols, ownGoals, keeperClaims;
        public int leftAttackPasses,rightAttackPasses,lowCrosses,aerialCrosses;
        public float AveragePassLength(int passes)=>passes>0?passDistance/passes:0;
        public float AverageWidth=>shapeSamples>0?widthSum/shapeSamples:0;
        public float AverageLine=>defensiveSamples>0?lineSum/defensiveSamples:0;
    }
    [Serializable] public class MatchState
    {
        public string home, away;
        public uint seed=731;
        public float homeForm,awayForm; // forme du jour de chaque équipe (multiplicateur −/+, 0 = neutre)
        public int venueSide=-1; // côté qui reçoit (0 = club dirigé, 1 = adversaire, -1 = neutre ; ancienne sauvegarde : neutre)
        public double remainder;
        public float clock, restart=2, decision=.7f, carryTime;
        public int period=1, restartSide;
        // Zero is the legacy save format: preserve its 360-second periods.
        // New career fixtures explicitly choose the full 2700-second clock.
        public int periodSeconds;
        public int HalfDuration=>periodSeconds==2700?2700:360;
        public float SecondsPerMinute=>HalfDuration/45f;
        public float LegacyTimeScale=>360f/HalfDuration;
        public int MinuteAt(float time)=>Math.Min(90,Math.Max(0,(int)((time+.001f)/SecondsPerMinute)));
        public bool halfTime, finished, professionalRules;
        public float nextMedicalCheck=8;
        public string phase="kickoff";
        public string restartTaker;
        public bool indirectRestart;
        public List<string> restartWall=new List<string>();
        public int engineVersion=2, possessionSide=-1;
        public float turnoverAt=-100, shotXg;
        // Explicit match delegation only; persisted so a resumed stoppage is not reviewed twice.
        public float delegatedReviewAt, delegatedStopUntil=-1; public bool delegatedHalfReviewed;
        public float awayReviewAt=480, awayBaseLine=.45f, awayBaseTempo=.5f, awayBaseRisk=.5f;
        public float awayTacticalReviewAt=120,awayBaseDefensiveWidth=.45f,awayBasePress=.5f;
        public int awayObservedCrosses,awayObservedThrough;
        // L'adversaire exploite une ligne haute observée (OpponentCoach) ; valeurs d'origine à restaurer.
        public bool awayExploitingLine,awayBaseCounter;public float awayBaseDirectness=-1;
        public string awayPlan="balanced";
        public TeamMetrics[] metrics={new TeamMetrics(),new TeamMetrics()};
        public Actor[] actors;
        public BallState ball = new BallState();
        public Tactic homeTactic, awayTactic;
        public int[] score = new int[2], shots = new int[2], passes = new int[2], completedPasses=new int[2], substitutions=new int[2];
        public List<string> used = new List<string>();
        public List<PendingSubstitution> pendingSubstitutions=new List<PendingSubstitution>();
        public List<float> homeWindows=new List<float>(), awayWindows=new List<float>();
        public List<MatchEvent> events=new List<MatchEvent>();
        // État mental et automatismes des deux équipes (TeamMindset). Absent ou vide :
        // moteur neutre, identique aux anciennes sauvegardes et à la calibration.
        public TeamMindset[] mindset;
        public int Minute => MinuteAt(clock);
    }
}

using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Touchline.Core;

namespace Touchline.Tests
{
    // Causeries, automatismes, consignes individuelles et cris depuis la touche :
    // chaque levier change un comportement mesurable du moteur, et l'état neutre
    // laisse le moteur strictement identique (calibration inchangée).
    public sealed class MatchImmersionTests
    {
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static Database Db()=>new Database{
            leagues=new[]{new LeagueData{id="fra.1",name="Ligue de test"}},
            clubs=Enumerable.Range(0,4).Select(i=>new ClubData{id="c"+i,name="Club "+i,league="fra.1",playable=true,annualRevenue=10000000}).ToArray(),
            players=Enumerable.Range(0,96).Select(i=>new PlayerData{id="p"+i,name="Joueur "+i,team="c"+(i/24),age=i%3==0?20:29,rating=70,potential=75,fitness=100,morale=75,wage=500,value=100000,
                position=i%24<2?"GB":i%24<10?"DEF":i%24<18?"MIL":"ATT",positions=new[]{i%24<2?"GK":i%24<4?"LB":i%24<6?"RB":i%24<10?"CB":i%24<14?"CM":i%24<16?"DM":i%24<18?"LW":i%24<20?"RW":"ST"}}).ToArray()};
        static MatchSimulation Sim(Database db,uint seed=11)
        {
            var c=new Career{club="c0"};c.lineup=Career.Select(db,"c0",c.tactic);var sim=MatchSimulation.Create(db,c,"c1",seed,2700);sim.State.professionalRules=true;return sim;
        }
        static T Call<T>(MatchSimulation sim,string name,params object[] args)=>(T)typeof(MatchSimulation).GetMethod(name,Private).Invoke(sim,args);
        static void Mind(MatchSimulation sim,Action<TeamMindset> set){var ours=TeamMindset.Neutral();set(ours);sim.State.mindset=new[]{ours,TeamMindset.Neutral()};}
        static string Fingerprint(MatchState m)=>string.Join(",",m.score)+"/"+string.Join(",",m.passes)+"/"+string.Join(",",m.completedPasses)+"/"+string.Join(";",m.events.Where(e=>e.kind!="assistant").Select(e=>e.kind+":"+e.time+":"+e.player))+"/"+string.Join(";",m.actors.Select(a=>a.position.x.ToString("R")+":"+a.position.z.ToString("R")+":"+a.fitness.ToString("R")));

        // Informational assistant messages are allowed; football events and physical results must stay identical.
        [TestCase(11u)] [TestCase(29u)] [TestCase(83u)] public void NeutralMindsetLeavesTheEngineIdentical(uint seed)
        {
            var db=Db();var plain=Sim(db,seed);var neutral=Sim(db,seed);Mind(neutral,_=>{});
            foreach(var sim in new[]{plain,neutral}){sim.Advance(sim.State.HalfDuration*.8);}
            Assert.AreEqual(Fingerprint(plain.State),Fingerprint(neutral.State));
        }
        [Test] public void UnderstandingAndComposureChangePassPrecision()
        {
            var db=Db();var sim=Sim(db);var passer=sim.State.actors[6];
            Mind(sim,m=>m.understanding=-.6f);float poor=Call<float>(sim,"PassErrorFactor",passer);
            Mind(sim,m=>m.understanding=.3f);float drilled=Call<float>(sim,"PassErrorFactor",passer);
            Mind(sim,m=>{for(int i=0;i<11;i++)m.composure[i]=-.5f;});float nervous=Call<float>(sim,"PassErrorFactor",passer);
            Assert.Greater(poor,1.15f);Assert.Less(drilled,.95f);Assert.Greater(nervous,1.05f);
            Assert.AreEqual(1f,Call<float>(Sim(db),"PassErrorFactor",passer));
        }
        [Test] public void NervesMakeDecisionsNoisierAndShotsLessAccurate()
        {
            var db=Db();var sim=Sim(db);var p=sim.State.actors[9];
            Mind(sim,m=>{for(int i=0;i<11;i++)m.composure[i]=-.6f;});
            Assert.Greater(Call<float>(sim,"DecisionNoiseFactor",p),1.2f);Assert.Less(sim.Composure(p),-.5f);
            Mind(sim,m=>{for(int i=0;i<11;i++)m.composure[i]=.4f;});
            Assert.Less(Call<float>(sim,"DecisionNoiseFactor",p),.9f);
            // L'adversaire n'est pas concerné par l'état mental du club dirigé.
            Assert.AreEqual(1f,Call<float>(sim,"DecisionNoiseFactor",sim.State.actors[20]));
        }
        [Test] public void DriveCostsFitnessAndConditioningSavesIt()
        {
            var db=Db();var sim=Sim(db);var p=sim.State.actors[5];
            Mind(sim,m=>{for(int i=0;i<11;i++)m.drive[i]=.8f;});float driven=Call<float>(sim,"FatigueFactor",p);
            Mind(sim,m=>m.conditioning=.8f);float conditioned=Call<float>(sim,"FatigueFactor",p);
            Assert.Greater(driven,1.1f);Assert.Less(conditioned,.9f);
        }
        static MatchSimulation PressScene(Database db,float drive,out Actor presser)
        {
            var sim=Sim(db);var m=sim.State;m.phase="play";m.restart=0;m.clock=600;m.homeTactic.pressing=.5f;
            var carrier=m.actors[11+9];presser=m.actors[6];
            foreach(var a in m.actors){a.velocity=new Point();a.position=a.previous=new Point(a.side==0?-30-a.slot:30+a.slot,a.slot*5-25);}
            // Porteur adverse à 16 m du presseur : hors du rayon normal (5+0,5×18=14 m), dans le rayon élargi.
            carrier.position=carrier.previous=new Point(-2,0);presser.position=presser.previous=new Point(-18,0);
            m.ball=new BallState{owner=carrier.id,side=1,position=new Point(-2.4f,0),height=.11f};m.possessionSide=1;
            Mind(sim,x=>{for(int i=0;i<11;i++)x.drive[i]=drive;});
            Call<object>(sim,"Move");return sim;
        }
        [Test] public void DriveWidensTheEngagementOfThePresser()
        {
            var db=Db();PressScene(db,0,out var calm);PressScene(db,.8f,out var driven);
            Assert.AreNotEqual("press",calm.intent);Assert.AreEqual("press",driven.intent);
        }
        [Test] public void PoorUnderstandingStaggersTheBackLine()
        {
            var db=Db();
            float Spread(float understanding){var sim=Sim(db);var m=sim.State;m.phase="play";m.restart=0;m.clock=600;
                m.ball=new BallState{owner=m.actors[11+9].id,side=1,position=new Point(10,0),height=.11f};m.possessionSide=1;m.actors[11+9].position=new Point(10,0);
                Mind(sim,x=>x.understanding=understanding);Call<object>(sim,"Move");
                // Écart entre la médiane de la ligne et le défenseur le plus bas (celui qui couvre le hors-jeu).
                var xs=Enumerable.Range(1,4).Select(i=>sim.MovementTarget(i).x).OrderBy(x=>x).ToArray();return (xs[1]+xs[2])/2-xs[0];}
            Assert.Greater(Spread(-1)-Spread(0),1.5f);
        }
        [Test] public void IndividualInstructionsMoveThePlayerInTheExpectedDirection()
        {
            var db=Db();
            Point Target(int slot,string instruction,bool possession){var sim=Sim(db);var m=sim.State;m.phase="play";m.restart=0;m.clock=600;
                PlayerInstructions.Set(m.homeTactic,slot,instruction);var owner=m.actors[possession?6:11+9];
                m.ball=new BallState{owner=owner.id,side=owner.side,position=new Point(0,0),height=.11f};owner.position=new Point(0,0);m.possessionSide=owner.side;
                Call<object>(sim,"Move");return sim.MovementTarget(slot);}
            int lb=1,lw=8;
            Assert.Greater(Math.Abs(Target(lw,PlayerInstructions.StayWide,true).z),Math.Abs(Target(lw,"",true).z)+4);
            Assert.Greater(Target(lb,PlayerInstructions.GetForward,true).x,Target(lb,"",true).x+5);
            Assert.Throws<ArgumentOutOfRangeException>(()=>PlayerInstructions.Set(new Tactic(),0,PlayerInstructions.StayWide));
            Assert.Throws<ArgumentException>(()=>PlayerInstructions.Set(new Tactic(),3,"dance"));
        }
        [Test] public void TalkReactionsDependOnSituationAndPersonality()
        {
            var veteran=new PlayerData{id="v",name="V",age=31,rating=75,attributes=new[]{new AttributeValue{key="composure",value=85}}};
            var youngster=new PlayerData{id="y",name="Y",age=19,rating=65,attributes=new[]{new AttributeValue{key="composure",value=40}}};
            var trusted=new PlayerLife{id="v",trust=80,morale=75};var doubtful=new PlayerLife{id="y",trust=40,morale=60};
            var losing=new TalkSituation{moment=TeamTalks.HalfTime,margin=-1,gap=4,importance=.5f};
            var leading=new TalkSituation{moment=TeamTalks.HalfTime,margin=2,gap=4,importance=.5f};
            var fired=TeamTalks.React(veteran,trusted,TeamTalks.Fire,losing,false);Assert.Greater(fired.drive,.5f);
            Assert.AreEqual("Tétanisé",TeamTalks.React(youngster,doubtful,TeamTalks.Fire,losing,false).label);
            Assert.AreEqual("Choqué",TeamTalks.React(veteran,trusted,TeamTalks.Fire,leading,false).label);
            Assert.Less(TeamTalks.React(veteran,trusted,TeamTalks.Praise,leading,false).drive,0);
            Assert.AreEqual("Démobilisé",TeamTalks.React(veteran,trusted,TeamTalks.Free,losing,false).label);
            var bigMatch=new TalkSituation{moment=TeamTalks.PreMatch,importance=.9f};var friendly=new TalkSituation{moment=TeamTalks.PreMatch,importance=.1f};
            Assert.Greater(TeamTalks.React(youngster,doubtful,TeamTalks.Calm,bigMatch,false).composure,TeamTalks.React(youngster,doubtful,TeamTalks.Calm,friendly,false).composure+.1f);
            Assert.Less(TeamTalks.BigMatchNerves(youngster,.9f),TeamTalks.BigMatchNerves(veteran,.9f));
            Assert.Greater(TeamTalks.React(veteran,trusted,TeamTalks.Encourage,losing,true).drive,TeamTalks.React(veteran,trusted,TeamTalks.Encourage,losing,false).drive);
        }
        static Career CareerWithMatch(Database db,out MatchSimulation sim)
        {
            var c=new Career{club="c0"};c.lineup=Career.Select(db,c.club,c.tactic);c.EnsureWorld(db);c.EnsureLife(db);
            sim=MatchSimulation.Create(db,c,"c1",21,2700);c.match=sim.State;c.ApplyMatchContext(sim);return c;
        }
        [Test] public void TeamTalkChangesMindsetAndMoraleOncePerMoment()
        {
            var db=Db();var c=CareerWithMatch(db,out var sim);Assert.AreEqual(TeamTalks.PreMatch,c.TalkMoment());
            float before=sim.State.mindset[0].composure.Average();
            var reactions=c.GiveTeamTalk(db,TeamTalks.Calm);Assert.AreEqual(11,reactions.Count);
            Assert.Greater(sim.State.mindset[0].composure.Average(),before+.2f);
            Assert.Throws<InvalidOperationException>(()=>c.GiveTeamTalk(db,TeamTalks.Demand));
            var star=sim.State.actors[9].id;float morale=c.Person(star).morale;
            c.GiveTeamTalk(db,TeamTalks.Praise,star);Assert.Greater(c.Person(star).morale,morale);
            Assert.Throws<InvalidOperationException>(()=>c.GiveTeamTalk(db,TeamTalks.Praise,star));
            sim.State.clock=10;Assert.IsNull(c.TalkMoment());Assert.Throws<InvalidOperationException>(()=>c.GiveTeamTalk(db,TeamTalks.Calm));
        }
        [Test] public void FormationSwitchAndNewcomersLowerUnderstandingUntilTrained()
        {
            var db=Db();var c=CareerWithMatch(db,out var sim);float settled=sim.State.mindset[0].understanding;
            Assert.AreEqual(Career.EstablishedFamiliarity,c.FormationFamiliarityValue("4-3-3"));
            Assert.Greater(sim.State.mindset[0].cohesion,99f);c.match=null;
            c.tactic.SetFormation("4-4-2");c.lineup=Career.Select(db,c.club,c.tactic);
            var after=MatchSimulation.Create(db,c,"c1",22,2700);c.match=after.State;c.ApplyMatchContext(after);
            float switched=after.State.mindset[0].understanding;Assert.Less(switched,settled-.25f);c.match=null;
            var tactical=Train(db,"tactical");var balanced=Train(db,"balanced");
            Assert.Greater(tactical,balanced+10);Assert.Less(Career.UnderstandingFrom(c.TacticalFamiliarity(),90),Career.UnderstandingFrom(100,90));
            // Une recrue du jour fait baisser la cohésion du onze.
            var recruit=db.players.First(p=>p.team=="c2");recruit.team="c0";c.EnsureLife(db);c.TrackPreparation();
            Assert.AreEqual(c.life.day,c.Person(recruit.id).joinedDay);Assert.Less(c.Settledness(recruit.id),.1f);
            Assert.Less(c.Cohesion(c.lineup.Take(10).Append(recruit.id)),c.Cohesion(c.lineup)-5);
        }
        static float Train(Database db,string focus)
        {
            var c=new Career{club="c0"};c.lineup=Career.Select(db,c.club,c.tactic);c.EnsureWorld(db);c.EnsureLife(db);c.TrackPreparation();
            c.tactic.SetFormation("4-2-3-1");c.SetTrainingFocus(focus);for(int day=0;day<10;day++)c.PreparationDay();
            return c.FormationFamiliarityValue("4-2-3-1");
        }
        [Test] public void TrainingFocusTradesFitnessInjuryAndPreparation()
        {
            var db=Db();var c=new Career{club="c0"};c.lineup=Career.Select(db,c.club,c.tactic);c.EnsureWorld(db);c.EnsureLife(db);
            c.SetTrainingFocus("physical");for(int i=0;i<10;i++)c.PreparationDay();
            Assert.Greater(c.life.preparation.conditioning,55);Assert.Greater(c.FocusInjuryFactor(),1);Assert.Less(c.FocusRecoveryBonus(),0);
            c.SetTrainingFocus("recovery");Assert.Less(c.FocusInjuryFactor(),1);Assert.Greater(c.FocusRecoveryBonus(),0);
            c.SetTrainingFocus("setpieces");for(int i=0;i<10;i++)c.PreparationDay();Assert.Greater(c.life.preparation.setPieces,60);
            Assert.Throws<ArgumentException>(()=>c.SetTrainingFocus("yoga"));
        }
        [Test] public void TouchlineShoutsHaveTemporaryEffectsAndACooldown()
        {
            var db=Db();var plain=Sim(db);Assert.Throws<InvalidOperationException>(()=>plain.Shout("focus"));
            var sim=Sim(db);Mind(sim,_=>{});sim.State.clock=600;var p=sim.State.actors[5];float tempo=sim.State.homeTactic.tempo;
            sim.Shout("calm");Assert.Greater(sim.Composure(p),.15f);Assert.Less(sim.State.homeTactic.tempo,tempo);
            Assert.IsFalse(sim.CanShout());Assert.Throws<InvalidOperationException>(()=>sim.Shout("press"));
            sim.State.clock+=MatchSimulation.ShoutMinutes*sim.State.SecondsPerMinute+1;Assert.AreEqual(0f,sim.Composure(p));Assert.IsTrue(sim.CanShout());
            sim.Shout("press");Assert.Greater(sim.Drive(p),.3f);Assert.AreEqual("shout",sim.State.events.Last().kind);
        }
        [Test] public void OpponentAttacksTheSpaceBehindAHighLineAndStopsWhenItDrops()
        {
            var db=Db();var sim=Sim(db);var m=sim.State;m.clock=20*m.SecondsPerMinute;m.awayTacticalReviewAt=0;float before=m.awayTactic.directness;bool counter=m.awayTactic.counterAttack;
            m.metrics[0].lineSum=-19.5f*10;m.metrics[0].defensiveSamples=10;Call<object>(sim,"ReviewOpponent");
            Assert.IsTrue(m.awayExploitingLine);Assert.GreaterOrEqual(m.awayTactic.directness,MatchSimulation.ExploitDirectness);Assert.IsTrue(m.awayTactic.counterAttack);
            Assert.AreEqual("opponent-adjustment",m.events.Last().kind);
            m.metrics[0].lineSum=-30f*10;m.awayTacticalReviewAt=0;Call<object>(sim,"ReviewOpponent");
            Assert.IsFalse(m.awayExploitingLine);Assert.AreEqual(before,m.awayTactic.directness);Assert.AreEqual(counter,m.awayTactic.counterAttack);
        }
        [Test] public void DisplayedShotEstimateIsBoundedAndPreservesChanceOrdering()
        {
            Assert.AreEqual(0,MatchSimulation.CalibratedShotEstimate(0));
            Assert.AreEqual(1,MatchSimulation.CalibratedShotEstimate(1));
            float previous=0;
            for(int i=1;i<=100;i++){float value=MatchSimulation.CalibratedShotEstimate(i/100f);Assert.Greater(value,previous);Assert.LessOrEqual(value,1);previous=value;}
        }

        [TestCase(0,0f)] [TestCase(2,.1f)] [TestCase(10,.5f)] [TestCase(10,2f)]
        public void ChanceReviewSeparatesVolumeQualityAndOutcomeWithoutMutatingMatch(int shots,float xg)
        {
            var m=Sim(Db()).State;m.shots[0]=shots;m.metrics[0].xg=xg;
            string before=UnityEngine.JsonUtility.ToJson(m);var lines=MatchAssistant.ChanceReview(m);string text=string.Join(" ",lines);
            StringAssert.Contains(shots+" tirs",text);StringAssert.Contains("ne prouve pas",text);
            Assert.AreEqual(shots>=6&&xg/shots<.08f,text.Contains("Qualité moyenne faible"));
            if(shots==0)StringAssert.Contains("Aucun tir enregistré",text);
            if(shots>0&&shots<6)StringAssert.Contains("Peu de tirs",text);
            Assert.AreEqual(before,UnityEngine.JsonUtility.ToJson(m));
        }
        [Test] public void FinalReviewDoesNotTreatCumulativePassesAsProofOfFinalTacticSuccess()
        {
            var m=Sim(Db()).State;m.homeTactic.line=.8f;m.homeTactic.pressing=.8f;
            string text=string.Join(" ",MatchAssistant.FullTimeReview(m));
            StringAssert.Contains("Consigne finale",text);StringAssert.Contains("ne mesure pas leur réussite",text);
            Assert.IsFalse(text.Contains("La ligne haute a tenu"));StringAssert.Contains("au seul pressing",text);
        }

        [Test] public void OpponentRallyAtHalfTimeWhenLosingAndAssistantReadsTheGame()
        {
            var db=Db();var sim=Sim(db);Mind(sim,_=>{});sim.State.score[0]=2;
            Call<object>(sim,"MindsetHalfTime");Assert.Greater(sim.State.mindset[1].drive.Average(),.25f);
            var m=sim.State;m.homeTactic.line=.8f;m.metrics[1].throughBalls=4;
            Assert.IsTrue(MatchAssistant.Observe(m).Any(o=>o.key=="line-exposed"));
            m.homeTactic.line=.4f;Assert.IsFalse(MatchAssistant.Observe(m).Any(o=>o.key=="line-exposed"));
            m.passes[0]=50;m.completedPasses[0]=25;m.homeTactic.tempo=.8f;m.mindset=null;
            var passing=MatchAssistant.Observe(m).Single(o=>o.key=="passing").text;
            StringAssert.Contains("peut y contribuer",passing);StringAssert.Contains("Surveillez",passing);
            var report=MatchAssistant.Opponent(db,m);Assert.AreEqual(2,report.danger.Count);StringAssert.Contains(m.awayTactic.formation,report.style);Assert.IsNotEmpty(report.advice);
        }
    }
}

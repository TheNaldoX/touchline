using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Touchline.Core;

namespace Touchline.Tests
{
    // Rôles et consignes individuelles, coups de pied arrêtés, entraînement,
    // causeries et plans tactiques enregistrés.
    public sealed class TacticsOffMatchTests
    {
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static Database Db()=>new Database{players=Enumerable.Range(0,40).Select(i=>new PlayerData{id="to"+i,name="T"+i,team=i<20?"a":"b",position=i%20==0?"GB":i%20<5?"DEF":i%20<9?"MIL":"ATT",rating=70+(i%7)}).ToArray()};
        static MatchSimulation Sim(Database db,Career c,uint seed=17){c.lineup=Career.Select(db,"a",c.tactic);var sim=MatchSimulation.Create(db,c,"b",seed,2700);sim.State.professionalRules=true;return sim;}
        static T Call<T>(object target,string name,params object[] args)=>(T)target.GetType().GetMethod(name,Private).Invoke(target,args);

        [Test]
        public void StandardRoleHasNoEffect()
        {
            var e=TacticalRoles.Effect(new Slot("ST",50,80));
            Assert.AreEqual(0,e.advance);Assert.AreEqual(1,e.width);Assert.AreEqual(1,e.shot);Assert.AreEqual(1,e.runBehind);Assert.AreEqual(0,e.press);Assert.AreEqual(1,e.keeperDepth);
        }

        [Test]
        public void RolesAreOfferedOnlyForTheirPosition()
        {
            var t=new Tactic();
            Assert.IsTrue(TacticalRoles.Allowed("ST","false9"));Assert.IsFalse(TacticalRoles.Allowed("CB","false9"));
            Assert.Throws<System.ArgumentException>(()=>TacticalRoles.SetRole(t,2,"poacher"));
            TacticalRoles.SetRole(t,9,"poacher");Assert.AreEqual("poacher",t.withBall[9].playerRole);
        }

        [Test]
        public void FullBackRolesChangeHeightAndWidthInPossession()
        {
            var db=Db();var c=new Career{club="a"};var sim=Sim(db,c);var lb=sim.State.actors[1];var q=new Point(-10,20);
            var neutral=Call<Point>(sim,"RoleShape",lb,q,true);
            c.tactic.withBall[1].playerRole="wing-back";var wing=Call<Point>(sim,"RoleShape",lb,q,true);
            c.tactic.withBall[1].playerRole="inverted";var inverted=Call<Point>(sim,"RoleShape",lb,q,true);
            Assert.Greater(wing.x,neutral.x+5);Assert.Greater(System.Math.Abs(wing.z),System.Math.Abs(neutral.z));
            Assert.Less(System.Math.Abs(inverted.z),System.Math.Abs(neutral.z)*.5f);
        }

        [Test]
        public void ShootMoreInstructionProducesMoreShots()
        {
            int Shots(int shooting){int total=0;foreach(uint seed in new uint[]{3,5}){var db=Db();var c=new Career{club="a"};for(int i=8;i<=10;i++)c.tactic.withBall[i].shooting=shooting;var sim=Sim(db,c,seed);sim.PlayToEnd();total+=sim.State.shots[0];}return total;}
            Assert.Greater(Shots(1),Shots(-1));
        }

        [Test]
        public void DesignatedCornerTakerIsUsedWhenOnThePitch()
        {
            var db=Db();var c=new Career{club="a"};var sim=Sim(db,c);var chosen=sim.State.actors[6];c.tactic.setPieces.cornerTaker=chosen.id;
            sim.State.phase="corner";sim.State.restartSide=0;sim.State.restartTaker=null;sim.State.ball=new BallState{position=new Point(52.5f,33.8f)};
            var taker=Call<Actor>(sim,"RestartTaker");Assert.AreEqual(chosen.id,taker.id);
        }

        [Test]
        public void NearPostRoutineSendsBestHeaderToTheNearPost()
        {
            var db=Db();var c=new Career{club="a"};c.tactic.setPieces.corner="near";var sim=Sim(db,c);
            sim.State.phase="corner";sim.State.restartSide=0;sim.State.ball=new BallState{position=new Point(sim.Direction(0)*52.5f,33.8f)};
            var taker=Call<Actor>(sim,"RestartTaker");int near=0;
            foreach(var p in sim.State.actors.Take(11)){if(p==taker||p.slot==0)continue;var args=new object[]{p,taker,new Point()};bool placed=(bool)sim.GetType().GetMethod("CornerPosition",Private).Invoke(sim,args);var q=(Point)args[2];
                if(placed&&q.x*sim.Direction(0)>46&&q.z>0)near++;}
            Assert.GreaterOrEqual(near,1);
            Assert.Greater(Call<Point>(sim,"CornerAim",taker).z,0);
        }

        [Test]
        public void TacticalFocusBuildsFamiliarityAndFormationChangeCostsIt()
        {
            var c=new Career{club="a",life=new ClubLife()};var plan=c.Training();plan.focus="tactical";float start=plan.familiarity;
            c.GetType().GetMethod("TrainingDay",Private).Invoke(c,null);Assert.Greater(plan.familiarity,start);
            float before=plan.familiarity;c.tactic.SetFormation("4-4-2");c.GetType().GetMethod("TrainingDay",Private).Invoke(c,null);
            Assert.Less(plan.familiarity,before-TrainingFocus.FormationChangeCost+3);
        }

        [Test]
        public void FamiliarityAndConditioningReachTheMatch()
        {
            var db=Db();var c=new Career{club="a"};var sim=Sim(db,c);var p=sim.State.actors[5];
            float neutral=Call<float>(sim,"Skill",p,"shortPassing");
            Career.ApplyTraining(sim,new TrainingPlan{familiarity=100,conditioning=100});
            Assert.Greater(Call<float>(sim,"Skill",p,"shortPassing"),neutral);Assert.Less(Call<float>(sim,"PreparationFatigue",0),1);
        }

        [Test]
        public void DemandingTalkHelpsATrailingFavouriteButNotALeader()
        {
            Assert.Greater(TeamTalks.Evaluate("halftime","demand",-1,4).skill,0);
            Assert.Less(TeamTalks.Evaluate("halftime","demand",1,4).skill,0);
            Assert.Greater(TeamTalks.Evaluate("prematch","encourage",0,-4).skill,TeamTalks.Evaluate("prematch","encourage",0,4).skill);
        }

        [Test]
        public void TeamTalkIsOncePerMomentAndChangesThePitch()
        {
            var db=Db();var c=new Career{club="a"};var sim=Sim(db,c);c.match=sim.State;
            Assert.AreEqual("prematch",c.TalkMoment());c.TeamTalk(db,"fire");Assert.IsNull(c.TalkMoment());
            Assert.Greater(sim.State.preparation[0].talk,0);
        }

        [Test]
        public void SavedPlanRestoresInstructionsRolesAndShape()
        {
            var db=Db();var c=new Career{club="a"};c.lineup=Career.Select(db,"a",c.tactic);c.tactic.mentality=.9f;c.tactic.withBall[9].playerRole="false9";c.SaveTacticPlan(1,"Attaque");
            c.tactic.mentality=.2f;c.tactic.withBall[9].playerRole="";c.ChangeTacticalFormation(db,"4-4-2");
            c.ApplyTacticPlan(db,1);
            Assert.AreEqual("4-3-3",c.tactic.formation);Assert.AreEqual(.9f,c.tactic.mentality,1e-5f);Assert.AreEqual("false9",c.tactic.withBall[9].playerRole);Assert.AreEqual("Attaque",c.TacticPlans()[1].name);
            Assert.Throws<System.InvalidOperationException>(()=>c.ApplyTacticPlan(db,2));
        }
    }
}

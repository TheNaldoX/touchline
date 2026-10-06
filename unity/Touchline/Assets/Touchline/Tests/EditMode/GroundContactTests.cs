using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Touchline.Core;
using UnityEngine;

namespace Touchline.Tests
{
    public class GroundContactTests
    {
        static object Invoke(MatchSimulation sim,string method,params object[] args)=>typeof(MatchSimulation).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(sim,args);
        static MatchSimulation Simulation()
        {
            var db=new Database{players=Enumerable.Range(0,40).Select(i=>new PlayerData{id="fall"+i,name="F"+i,team=i<20?"a":"b",position=i%20==0?"GB":i%20<5?"DEF":i%20<9?"MIL":"ATT",rating=75,heightCm=180}).ToArray()};
            var career=new Career{club="a"};career.lineup=Career.Select(db,"a",career.tactic);return MatchSimulation.Create(db,career,"b");
        }
        [TestCase(.7f,4,true,true)] [TestCase(1.5f,4,true,false)] [TestCase(.7f,.4f,true,false)] [TestCase(.7f,2,false,false)]
        public void AResolvedContactOnlyCausesFallAtPlausibleDistanceAndSpeed(float distance,float speed,bool foul,bool expected)
        {
            var sim=Simulation();var p=sim.State.actors[9];var other=sim.State.actors[12];p.position=new Point();other.position=new Point(distance,0);p.velocity=new Point(0,speed);other.velocity=new Point();uint seed=sim.State.seed;
            Assert.AreEqual(expected,(bool)Invoke(sim,"BeginContactFall",p,other,foul));Assert.AreEqual(seed,sim.State.seed,"Visual consequence must not draw an extra random duel outcome");
            if(expected){Assert.IsTrue(MatchSimulation.GroundedAction(p));Assert.AreEqual(0,p.velocity.Length);Assert.GreaterOrEqual(p.controlTime,MatchSimulation.ContactFallDuration);}
        }
        [Test] public void FallRemainsUnavailableAndStationaryUntilRecoveryAndSurvivesSerialization()
        {
            var sim=Simulation();var p=sim.State.actors[9];var other=sim.State.actors[12];p.position=new Point();other.position=new Point(.7f,0);p.velocity=new Point(0,4);other.velocity=new Point();Assert.IsTrue((bool)Invoke(sim,"BeginContactFall",p,other,true));
            var copy=JsonUtility.FromJson<Actor>(JsonUtility.ToJson(p));
            for(int i=0;i<21;i++){
                Assert.IsTrue((bool)Invoke(sim,"AdvanceGroundAction",p));Assert.IsTrue((bool)Invoke(sim,"AdvanceGroundAction",copy));
                Assert.AreEqual(JsonUtility.ToJson(p),JsonUtility.ToJson(copy));Assert.AreEqual(0,p.position.Length);Assert.AreEqual(0,p.velocity.Length);Assert.AreEqual("fall",p.action);
            }
            for(int i=0;i<2;i++)Invoke(sim,"AdvanceGroundAction",p);
            Assert.AreEqual("idle",p.action);Assert.IsFalse(MatchSimulation.GroundedAction(p));
        }
        [TestCase(30,1,160)] [TestCase(60,-1,180)] [TestCase(120,1,205)]
        public void ContactRecoveryHasGroundedFeetAndNoPoseTeleport(int fps,int side,int stature)
        {
            var go=new GameObject("Contact recovery");try{
                var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="ground-recovery",heightCm=stature},0,9,Color.white);
                // This is an outfield fall. Actor.slot defaults to zero, which
                // would correctly enter the keeper's lowered ready stance on
                // return to idle rather than the outfield upright baseline.
                var actor=new Actor{slot=9,action="idle"};view.Render(actor,1,1f/fps);float baseline=go.transform.Find("Rig").position.y;
                actor.action="fall";actor.actionKind="fouled-fall";actor.actionSequence=1;actor.diveSide=side;float minBody=0,maxJump=0;var last=Vector3.zero;
                for(int i=0;i<=Mathf.CeilToInt(2.6f*fps);i++){
                    float t=i/(float)fps;actor.actionTime=Mathf.Max(0,MatchSimulation.ContactFallDuration-t);if(t>MatchSimulation.ContactFallDuration)actor.action="idle";
                    view.Render(actor,1,1f/fps);var body=go.transform.Find("Rig").position;
                    if(i>0)maxJump=Mathf.Max(maxJump,Vector3.Distance(last,body));last=body;minBody=Mathf.Min(minBody,body.y);
                    Assert.GreaterOrEqual(view.FootPosition(true).y,.025f);Assert.GreaterOrEqual(view.FootPosition(false).y,.025f);Assert.AreEqual(Vector3.zero,go.transform.position);
                }
                Assert.Less(minBody,-.35f);Assert.Less(maxJump,.10f);Assert.That(last.y,Is.InRange(-.06f,.04f));
                Assert.That(last.y,Is.EqualTo(baseline).Within(.02f),"Recovery must return to the same outfield idle height");
            }finally{Object.DestroyImmediate(go);}
        }
    }
}

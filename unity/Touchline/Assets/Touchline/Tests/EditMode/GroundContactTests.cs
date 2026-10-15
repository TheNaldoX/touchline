using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Touchline.Core;
using UnityEngine;

namespace Touchline.Tests
{
    public class GroundContactTests
    {
        [TestCase("L")] [TestCase("R")]
        public void CapturedSupportKeepsTheKneeAboveTurfWithoutMovingTheAnkle(string side)
        {
            var go=new GameObject("Grounded knee support");
            try{
                var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="knee-floor",heightCm=180},0,9,Color.white);
                go.transform.position=Vector3.down*.5f;
                var flags=BindingFlags.Instance|BindingFlags.NonPublic;
                var foot=go.GetComponentsInChildren<Transform>().First(t=>t.name=="foot."+side);
                var knee=go.GetComponentsInChildren<Transform>().First(t=>t.name=="lowerleg01."+side);
                var target=new Vector3(side=="L"?.18f:-.18f,.10f,.45f);
                typeof(PlayerView).GetMethod("SolveLeg",flags).Invoke(view,new object[]{side,target,Vector3.down});
                Assert.Less(knee.position.y,.075f,"The fixture must reproduce a knee intersecting the turf");
                typeof(PlayerView).GetMethod("SolveCapturedSupport",flags).Invoke(view,new object[]{side,target,Vector3.down,System.Type.Missing});
                Assert.GreaterOrEqual(knee.position.y,.079f,"The knee surface needs clearance above the pitch");
                Assert.Less(Vector3.Distance(foot.position,target),.005f,"The support ankle stays in place during correction");
            }finally{Object.DestroyImmediate(go);}
        }
        [TestCase("L")] [TestCase("R")]
        public void CapturedSupportPreservesTheKneePlaneWhenTheFootIsAlreadyPlanted(string side)
        {
            var go=new GameObject("Captured knee support");
            try{
                var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="knee-support",heightCm=180},0,9,Color.white);
                var flags=BindingFlags.Instance|BindingFlags.NonPublic;
                var foot=go.GetComponentsInChildren<Transform>().First(t=>t.name=="foot."+side);
                var knee=go.GetComponentsInChildren<Transform>().First(t=>t.name=="lowerleg01."+side);
                var target=foot.position;target.y=.10f;
                typeof(PlayerView).GetMethod("SolveLeg",flags).Invoke(view,new object[]{side,target,Vector3.right});
                var before=knee.position;var planted=foot.position;
                var support=typeof(PlayerView).GetMethod("MecanimFootLock",flags);
                support.Invoke(view,new object[]{1f/60,true});support.Invoke(view,new object[]{1f/60,false});
                Assert.Less(Vector3.Distance(foot.position,planted),.005f,"An unchanged support must stay planted");
                Assert.Less(Vector3.Distance(knee.position,before),.005f,"Foot locking must not rotate the captured knee around its planted ankle");
            }finally{Object.DestroyImmediate(go);}
        }
        [TestCase(30)] [TestCase(60)] [TestCase(120)]
        public void CapturedFallDoesNotSkipToImpactBeforePhysicalTimeAdvances(int hz)
        {
            bool mode=PlayerView.UseMecanim;PlayerView.UseMecanim=true;
            var go=new GameObject("Captured fall entry");var control=new GameObject("Same idle frame");
            try{
                var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="fall-entry",heightCm=180},0,9,Color.white);
                var idle=control.AddComponent<PlayerView>();idle.Build(new PlayerData{id="fall-entry",heightCm=180},0,9,Color.white);
                var actor=new Actor{slot=9,action="idle"};float dt=1f/hz;
                for(int i=0;i<hz;i++){view.Render(actor,1,dt);idle.Render(actor,1,dt);}
                var head=go.GetComponentsInChildren<Transform>().First(t=>t.name=="head");
                idle.Render(actor,1,dt);
                var before=control.GetComponentsInChildren<Transform>().First(t=>t.name=="head").position;var left=idle.FootPosition(true);var right=idle.FootPosition(false);
                actor.action="fall";actor.actionKind="fouled-fall";actor.actionSequence=1;actor.actionTime=MatchSimulation.ContactFallDuration;
                view.Render(actor,1,dt);
                Assert.Less(Vector3.Distance(before,head.position),.025f,"No fall impact before simulation time advances");
                Assert.Less(Vector3.Distance(left,view.FootPosition(true)),.025f,"Left support must not jump on entry");
                Assert.Less(Vector3.Distance(right,view.FootPosition(false)),.025f,"Right support must not jump on entry");
            }finally{Object.DestroyImmediate(go);Object.DestroyImmediate(control);PlayerView.UseMecanim=mode;}
        }
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
        [TestCase(30,-1,180)] [TestCase(30,1,205)] [TestCase(60,1,160)]
        [TestCase(60,1,205)] [TestCase(120,1,160)] [TestCase(120,-1,180)]
        public void ContactRecoveryHasGroundedFeetAndNoPoseTeleport(int fps,int side,int stature)
        {
            bool mode=PlayerView.UseMecanim;PlayerView.UseMecanim=true;
            var go=new GameObject("Contact recovery");var control=new GameObject("Matching idle pelvis");try{
                var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="ground-recovery",heightCm=stature},0,9,Color.white);
                var idle=control.AddComponent<PlayerView>();idle.Build(new PlayerData{id="ground-recovery",heightCm=stature},0,9,Color.white);
                // This is an outfield fall. Actor.slot defaults to zero, which
                // would correctly enter the keeper's lowered ready stance on
                // return to idle rather than the outfield upright baseline.
                var actor=new Actor{slot=9,action="idle"};
                for(int i=0;i<fps;i++){view.Render(actor,1,1f/fps);idle.Render(actor,1,1f/fps);}
                // Captured motion moves the anatomical pelvis, not its Rig container.
                var pelvis=go.GetComponentsInChildren<Transform>().First(t=>t.name=="root");
                var idlePelvis=control.GetComponentsInChildren<Transform>().First(t=>t.name=="root");
                var idleActor=new Actor{slot=9,action="idle"};
                float baseline=pelvis.position.y;
                actor.action="fall";actor.actionKind="fouled-fall";actor.actionSequence=1;actor.diveSide=side;float minBody=0,maxJump=0,maxJumpTime=0;var last=Vector3.zero;
                for(int i=0;i<=Mathf.CeilToInt(2.6f*fps);i++){
                    float t=i/(float)fps;actor.actionTime=Mathf.Max(0,MatchSimulation.ContactFallDuration-t);if(t>MatchSimulation.ContactFallDuration)actor.action="idle";
                    view.Render(actor,1,1f/fps);idle.Render(idleActor,1,1f/fps);var body=pelvis.position;body.y-=baseline;
                    if(i>0&&Vector3.Distance(last,body)>maxJump){maxJump=Vector3.Distance(last,body);maxJumpTime=t;}last=body;minBody=Mathf.Min(minBody,body.y);
                    Assert.GreaterOrEqual(view.FootPosition(true).y,.025f);Assert.GreaterOrEqual(view.FootPosition(false).y,.025f);Assert.AreEqual(Vector3.zero,go.transform.position);
                }
                TestContext.WriteLine($"Pelvis {fps}Hz: minimum={minBody:F4}m, maximum step={maxJump:F4}m at {maxJumpTime:F3}s, recovery={last.y:F4}m");
                Assert.Less(minBody,-.35f);Assert.Less(maxJump,.10f);Assert.That(last.y,Is.InRange(-.06f,.04f));
                Assert.That(pelvis.position.y,Is.EqualTo(idlePelvis.position.y).Within(.02f),"Recovery must return to the same outfield idle height at the same breathing phase");
            }finally{Object.DestroyImmediate(go);Object.DestroyImmediate(control);PlayerView.UseMecanim=mode;}
        }
    }
}

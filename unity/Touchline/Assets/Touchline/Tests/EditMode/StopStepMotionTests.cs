using NUnit.Framework;
using UnityEngine;
namespace Touchline.Tests
{
    // Independent footstep planner checks; rig checks cover final IK.
    public class StopStepMotionTests
    {
        static StopStepMotion Create()
        {
            var p=new StopStepMotion();
            Assert.IsTrue(p.Begin(new Vector3(.16f,.08f,.3f),new Vector3(-.16f,.08f,-.3f),new Vector3(.16f,.08f,0),new Vector3(-.16f,.08f,0),Quaternion.identity,Quaternion.identity,Quaternion.identity));return p;
        }
        [TestCase(30)][TestCase(60)][TestCase(120)]
        public void OnlyTheLiftedFootTravelsAndBothFeetSettle(int fps)
        {
            var p=Create();var before=new[]{p.Position(0),p.Position(1)};float slip=0;
            for(int frame=1;frame<=fps;frame++){
                p.Advance(1f/fps);int shifting=0;
                for(int foot=0;foot<2;foot++){
                    var now=p.Position(foot);float travel=Vector2.Distance(new Vector2(now.x,now.z),new Vector2(before[foot].x,before[foot].z));
                    if(travel>.0001f)shifting++;
                    if(now.y<=.105f&&before[foot].y<=.105f)slip+=travel;
                    Assert.That(now.y,Is.InRange(.07999f,.16001f));Assert.LessOrEqual(travel*fps,2.4f);
                    before[foot]=now;
                }
                Assert.LessOrEqual(shifting,1,"Never shuffle both support soles simultaneously");
            }
            Assert.IsTrue(p.Complete);Assert.AreEqual(0,slip,.0001f);Assert.AreEqual(0,p.Position(0).z,.0001f);Assert.AreEqual(0,p.Position(1).z,.0001f);
        }
        [Test] public void OneLargePlaybackStepAgreesWithSubdividedSimulationTime()
        {
            var once=Create();var split=Create();once.Advance(.4f);for(int i=0;i<48;i++)split.Advance(1f/120);
            for(int foot=0;foot<2;foot++)Assert.Less(Vector3.Distance(once.Position(foot),split.Position(foot)),.0001f);
            once.Advance(1);Assert.IsTrue(once.Complete);
        }
        [Test] public void PauseAndCancellationCannotKeepAdvancingAnOldStep()
        {
            var p=Create();p.Advance(.1f);var held=p.Position(0);p.Advance(0);Assert.AreEqual(held,p.Position(0));
            p.Cancel();p.Advance(.5f);Assert.IsFalse(p.Active);Assert.AreEqual(held,p.Position(0));
        }
        [Test] public void BothAirborneOrDistantFeetAreNotDeclaredGroundedSupport()
        {
            var p=new StopStepMotion();
            Assert.IsFalse(p.Begin(Vector3.up*.2f,Vector3.up*.2f,Vector3.up*.08f,Vector3.up*.08f,Quaternion.identity,Quaternion.identity,Quaternion.identity));
            Assert.IsFalse(p.Begin(new Vector3(0,.08f,.6f),new Vector3(0,.08f,0),new Vector3(0,.08f,0),new Vector3(0,.08f,0),Quaternion.identity,Quaternion.identity,Quaternion.identity));
        }
    }
}

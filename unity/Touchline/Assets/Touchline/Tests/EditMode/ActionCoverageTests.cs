using NUnit.Framework;
using UnityEngine;
using Touchline.Core;

namespace Touchline.Tests
{
    public class ActionCoverageTests
    {
        static PlayerView View(GameObject go,bool left=false){var v=go.AddComponent<PlayerView>();v.Build(new PlayerData{id="coverage",heightCm=180,preferredFoot=left?"Left":"Right"},0,9,Color.white);return v;}
        [TestCase(false,"pass")] [TestCase(true,"pass")] [TestCase(false,"through")] [TestCase(true,"cutback")]
        public void InsidePassMeetsBallWithMedialBootSurface(bool left,string kind)
        {
            var go=new GameObject("Inside-foot contact");try{var v=View(go,left);var a=new Actor{action="kick",actionKind=kind,actionSequence=1,actionTarget=new Point(0,.42f),actionHeight=.11f};var ball=new Vector3(0,.11f,.42f);
                for(int i=0;i<=18;i++){a.actionTime=.64f-i*.01f;v.Render(a,1,.01f,ball);}
                Assert.That(Vector3.Distance(v.InsideFootContactPosition(left),ball),Is.LessThan(.055f));Assert.That(v.FootPosition(!left).y,Is.GreaterThan(.03f));
            }finally{Object.DestroyImmediate(go);}
        }
        [TestCase(160)] [TestCase(180)] [TestCase(205)] public void ThrowReleaseHasTwoHandsOnBallAndGroundedFeet(int height)
        {
            var go=new GameObject("Throw-in contact");try{var v=View(go);v.ChangeIdentity(new PlayerData{id="throw",heightCm=height});var a=new Actor{action="throw",actionTime=.6f,actionContactTime=.5f,actionSequence=1,actionTarget=new Point(0,.42f),actionHeight=height*.01f};var ball=new Vector3(0,height*.01f,.42f);
                var setup=new BallState{setupStart=new Point(0,.3f),setupHeight=.11f,start=a.actionTarget,startHeight=a.actionHeight,end=new Point(0,8)};
                for(int i=0;i<=50;i++){a.actionTime=1.1f-i*.01f;var point=MatchSimulation.ThrowPreparation(setup,i/50f,out float y);v.Render(a,1,.01f,new Vector3(point.x,y,point.z));}
                var hands=go.GetComponentsInChildren<Transform>();foreach(var hand in hands)if(hand.name=="wrist.L"||hand.name=="wrist.R")Assert.That(Vector3.Distance(hand.position,ball),Is.LessThan(.17f),hand.name);
                Assert.That(v.FootPosition(true).y,Is.InRange(.04f,.14f));Assert.That(v.FootPosition(false).y,Is.InRange(.04f,.14f));
            }finally{Object.DestroyImmediate(go);}
        }
        [TestCase(30)] [TestCase(60)] [TestCase(120)] public void EveryEngineGestureHasFiniteBoundedSkeletonAndReturnsToLocomotion(int fps)
        {
            string[] actions={"idle","run","kick","header","control","miscontrol","tackle","block","claim","dive","throw","keeper-hold","place-ball","keeper-rise","hurt"};
            foreach(string action in actions){var go=new GameObject(action);try{var v=View(go);var a=new Actor{action=action,actionSequence=1,actionTarget=new Point(.2f,.42f),actionHeight=action=="header"?1.95f:action=="throw"?1.8f:.11f,actionContactTime=action=="throw"?.5f:.18f};float duration=action=="hurt"?2:action=="dive"?1.2f:action=="throw"?1.1f:action=="claim"?.8f:.64f;
                var joints=go.GetComponentsInChildren<Transform>();
                for(int frame=0;frame<fps*3;frame++){float time=frame/(float)fps;a.actionTime=Mathf.Max(0,duration-time);if(time>duration)a.action="idle";v.Render(a,1,1f/fps,new Vector3(.2f,a.actionHeight,.42f));
                    foreach(var bone in joints){var position=bone.position;Assert.IsFalse(float.IsNaN(position.x+position.y+position.z)||float.IsInfinity(position.x+position.y+position.z),action);Assert.That(position.magnitude,Is.LessThan(3.5f),action+" "+bone.name);}
                }
                Assert.That(v.FootPosition(true).y,Is.GreaterThan(.03f),action);Assert.That(v.FootPosition(false).y,Is.GreaterThan(.03f),action);
            }finally{Object.DestroyImmediate(go);}}
        }
        [TestCase(false)] [TestCase(true)] public void CloseDribbleTouchMeetsBallAndDoesNotMoveActor(bool left)
        {
            var go=new GameObject("Dribbling");try{var v=View(go,left);var a=new Actor{action="run",stride=Mathf.PI*1.5f/2.4f,velocity=new Point(0,2)};var ball=new Vector3(0,.11f,.30f);v.Render(a,1,1f/60,ball,new PlayerMotionContext{carrying=true});
                // At the first nearest touch the preferred foot is selected.
                Assert.That(Mathf.Min(Vector3.Distance(v.BootContactPosition(true),ball),Vector3.Distance(v.BootContactPosition(false),ball)),Is.LessThan(.06f));Assert.AreEqual(Vector3.zero,go.transform.position);
            }finally{Object.DestroyImmediate(go);}
        }
        [Test] public void ReactionsComeFromGoalSideAndStopWhenPlayResumes()
        {
            var m=new MatchState{phase="goal",restart=2};m.events.Add(new MatchEvent{kind="goal",side=1,player="scorer"});
            Assert.AreEqual(2,PlayerMotionContext.From(m,new Actor{id="scorer",side=1},1).reaction);
            Assert.AreEqual(1,PlayerMotionContext.From(m,new Actor{id="mate",side=1},1).reaction);
            Assert.AreEqual(-1,PlayerMotionContext.From(m,new Actor{id="opponent",side=0},1).reaction);
            m.phase="kickoff";Assert.AreEqual(0,PlayerMotionContext.From(m,new Actor{side=1},1).reaction);
        }
        [Test] public void ContextAndKickStyleInvalidatePausedCacheButIdenticalFrameDoesNot()
        {
            var c=new PausedPoseCache();var a=new Actor{id="a"};c.NeedsUpdate(0,a,Vector3.zero);a.actionKind="pass";Assert.IsTrue(c.NeedsUpdate(0,a,Vector3.zero));var context=new PlayerMotionContext{carrying=true};Assert.IsTrue(c.NeedsUpdate(0,a,Vector3.zero,context));Assert.IsFalse(c.NeedsUpdate(0,a,Vector3.zero,context));context.reaction=2;Assert.IsTrue(c.NeedsUpdate(0,a,Vector3.zero,context));
        }
        [Test] public void ContactMetadataSurvivesSerialization()
        {
            var a=new Actor{actionKind="cutback",actionSequence=17,actionTarget=new Point(3,4),actionHeight=.3f};var saved=JsonUtility.FromJson<Actor>(JsonUtility.ToJson(a));Assert.AreEqual(a.actionKind,saved.actionKind);Assert.AreEqual(17,saved.actionSequence);Assert.AreEqual(3,saved.actionTarget.x);
        }
        [TestCase(30)] [TestCase(60)] [TestCase(120)] public void ApplauseKeepsElbowsBelowHandsAndReturnsToRest(int fps)
        {
            var go=new GameObject("Applause");try{var v=View(go);var a=new Actor{action="idle"};var context=new PlayerMotionContext{reaction=1};var joints=go.GetComponentsInChildren<Transform>();
                for(int i=0;i<fps;i++){context.reactionTime=i/(float)fps;v.Render(a,1,1f/fps,Vector3.forward,context);}
                foreach(string side in new[]{"L","R"}){
                    Transform elbow=null,hand=null;foreach(var joint in joints){if(joint.name=="lowerarm01."+side)elbow=joint;if(joint.name=="wrist."+side)hand=joint;}
                    Assert.Less(elbow.position.y,hand.position.y-.05f,side+" elbow should relax below the clapping hands");
                }
                for(int i=0;i<fps;i++)v.Render(a,1,1f/fps,Vector3.forward);
                foreach(var joint in joints)if(joint.name.StartsWith("wrist."))Assert.Less(joint.position.y,1.2f,"Arms return to rest when celebration ends");
            }finally{Object.DestroyImmediate(go);}
        }
        [Test] public void ThrowTravelsBehindHeadBeforeOverheadReleaseWithoutDiscontinuity()
        {
            var b=new BallState{setupStart=new Point(0,.3f),setupHeight=.11f,start=new Point(0,.42f),startHeight=1.8f,end=new Point(0,8)};
            var previous=MatchSimulation.ThrowPreparation(b,0,out float priorHeight);Assert.AreEqual(.3f,previous.z);
            for(int i=1;i<=100;i++){var p=MatchSimulation.ThrowPreparation(b,i/100f,out float h);Assert.That(Point.Distance(p,previous),Is.LessThan(.05f));Assert.That(Mathf.Abs(h-priorHeight),Is.LessThan(.055f));if(h<1.65f)Assert.GreaterOrEqual(p.z,.29f,"The pickup path must stay in front of the torso");previous=p;priorHeight=h;}
            var behind=MatchSimulation.ThrowPreparation(b,.75f,out float peak);Assert.Less(behind.z,0);Assert.Greater(peak,1.8f);Assert.That(previous.z,Is.EqualTo(.42f).Within(.001f));Assert.That(priorHeight,Is.EqualTo(1.8f).Within(.001f));
        }
    }
}

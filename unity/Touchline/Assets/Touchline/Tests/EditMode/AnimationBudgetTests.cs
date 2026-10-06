using System;
using NUnit.Framework;
using Touchline.Core;
using UnityEngine;
using Unity.Profiling;

namespace Touchline.Tests
{
    public class AnimationBudgetTests
    {
        [TestCase("run")] [TestCase("kick")] [TestCase("dive")] [TestCase("keeper-roll")] [TestCase("tackle")]
        [TestCase("tackle",true)] [TestCase("dive",true)]
        [TestCase("tackle",true,true)]
        [TestCase("header")]
        [TestCase("control-chest")] [TestCase("control-thigh")]
        [TestCase("fall")]
        [TestCase("slide")]
        [TestCase("aerial-contest")]
        public void WarmAnimationAvoidsRecurringManagedGarbage(string action,bool prepared=false,bool withdrawn=false)
        {
            var go=new GameObject("Animation allocation budget");try{
                var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="allocation-budget",heightCm=182},0,action=="dive"||action=="keeper-roll"?0:2,Color.white);
                var actor=new Actor{slot=action=="dive"||action=="keeper-roll"?0:2,action=action,intent="mark",actionKind=action=="dive"?"save-catch":"clearance",actionContactTime=.18f,actionTarget=new Point(.5f,.42f),actionHeight=.5f};
                if(prepared)actor.actionKind=action=="dive"?"save-parry":MatchSimulation.StandingDuel;
                if(withdrawn)actor.tackleWithdrawFrom=.65f;
                if(action=="header"){actor.actionSequence=1;actor.actionTarget=new Point(.1f,.3f);actor.actionHeight=2.05f;actor.actionContactTime=.12f;}
                if(action=="control-chest"||action=="control-thigh"){actor.action="control";actor.actionKind=action;actor.actionSequence=1;actor.actionTarget=new Point(.12f,.3f);actor.actionHeight=action=="control-chest"?1.4f:.95f;}
                var context=new PlayerMotionContext{defending=action=="run"};var ball=new Vector3(.5f,.5f,.42f);
                for(int i=0;i<90;i++)RenderFrame(view,actor,context,ball,i);
                long count;
                using(var witness=ProfilerRecorder.StartNew(ProfilerCategory.Internal,"GC.Alloc",1,ProfilerRecorderOptions.SumAllSamplesInFrame|ProfilerRecorderOptions.CollectOnlyOnCurrentThread)){
                    var allocation=new byte[65536];GC.KeepAlive(allocation);witness.Stop();
                    Assert.IsTrue(witness.Valid);Assert.Greater(witness.Count,0,"Allocation counter must capture a known allocation");Assert.Greater(witness.GetSample(0).Count,0);
                }
                using(var allocation=ProfilerRecorder.StartNew(ProfilerCategory.Internal,"GC.Alloc",1,ProfilerRecorderOptions.SumAllSamplesInFrame|ProfilerRecorderOptions.CollectOnlyOnCurrentThread)){
                    for(int i=0;i<300;i++)RenderFrame(view,actor,context,ball,i);
                    allocation.Stop();count=allocation.Count>0?allocation.GetSample(0).Count:0;
                }
                TestContext.WriteLine(action+": "+count+" allocations / 300 frames");
                Assert.AreEqual(0,count,"Repeated pose updates should not allocate strings for bone names every frame");
            }finally{UnityEngine.Object.DestroyImmediate(go);}
        }
        static void RenderFrame(PlayerView view,Actor actor,PlayerMotionContext context,Vector3 ball,int i)
        {
            actor.previous=actor.position;actor.velocity=actor.action=="run"?new Point(0,-2):new Point();actor.position+=actor.velocity/60;
            float duration=actor.action=="dive"?1.2f:actor.action=="keeper-roll"?1.08f:actor.action=="tackle"?(actor.actionKind==MatchSimulation.StandingDuel?.75f:.55f):.64f;
            if(MatchSimulation.IsBodyControl(actor))duration=MatchSimulation.BodyControlDuration(actor.actionKind);
            if(actor.action=="fall")duration=MatchSimulation.ContactFallDuration;
            if(actor.action=="slide")duration=MatchSimulation.SlidingDuelDuration;
            if(actor.action==MatchSimulation.AerialContest)duration=MatchSimulation.AerialContestDuration;
            if(actor.tackleWithdrawFrom>0)duration=MatchSimulation.TackleRecovery*.35f;
            // Include the landing and complete rise, not only the first 0.75 s.
            int frames=Mathf.CeilToInt(duration*60)+1;actor.actionTime=Mathf.Max(0,duration-(i%frames)/60f);
            view.Render(actor,1,1f/60,ball,context);
        }
    }
}

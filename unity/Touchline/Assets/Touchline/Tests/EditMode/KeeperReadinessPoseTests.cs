using System;
using NUnit.Framework;
using Touchline.Core;
using UnityEngine;
namespace Touchline.Tests
{
    // Requires Unity native rig/render resources. Compilation is not execution.
    public sealed class KeeperReadinessPoseTests
    {
        static MatchState State(bool opponent,bool shot,out Actor keeper)
        {
            keeper=new Actor{id="readiness-native",slot=0,side=0,position=new Point(-48,0),previous=new Point(-48,0),angle=(float)Math.PI/2,fitness=100};
            var owner=new Actor{id="readiness-owner",slot=9,side=opponent?1:0,position=new Point(opponent?-40:35,0)};
            return new MatchState{phase="play",restart=0,clock=100,actors=new[]{keeper,owner},ball=new BallState{side=owner.side,owner=shot?null:owner.id,from=owner.id,
                position=owner.position,previous=owner.position,start=owner.position,end=new Point(-53,0),kind=shot?"shot":"none",duration=shot?.4f:0,elapsed=shot?-.18f:0}};
        }
        static PlayerView Build(GameObject root)
        {var view=root.AddComponent<PlayerView>();view.Build(new PlayerData{id="readiness-native",heightCm=182},0,0,Color.yellow);return view;}
        static void Render(PlayerView view,MatchState state,Actor actor,float dt,bool clear=false)
        {var context=PlayerMotionContext.From(state,actor,1);if(clear)context.keeperReadiness=default;view.Render(actor,1,dt,new Vector3(state.ball.position.x,state.ball.height,state.ball.position.z),context);}
        static float Drop(bool opponent,bool shot)
        {
            var root=new GameObject("readiness-native-pose");
            try{var view=Build(root);Actor actor;var state=State(opponent,shot,out actor);string before=JsonUtility.ToJson(state);Render(view,state,actor,0);
                for(int i=1;i<=60;i++){state.clock=100+i*.1f;Render(view,state,actor,1f/60);}
                state.clock=100;Assert.That(JsonUtility.ToJson(state),Is.EqualTo(before));
                Assert.That(Vector3.Distance(root.transform.position,new Vector3(-48,0,0)),Is.LessThan(.00001));
                foreach(bool left in new[]{true,false}){var foot=view.FootPosition(left);Assert.That(float.IsNaN(foot.y)||float.IsInfinity(foot.y),Is.False);Assert.That(foot.y,Is.GreaterThanOrEqualTo(.055f));}
                return root.transform.Find("Rig").localPosition.y;
            }finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        [Test] public void CompleteRenderHasDistinctReadinessAndNoAuthoritativeMutation()
        {float far=Drop(false,false),near=Drop(true,false),shot=Drop(true,true);Assert.That(far-near,Is.GreaterThan(.025));Assert.That(near-shot,Is.GreaterThan(.012));}
        [TestCase("dive")][TestCase("claim")][TestCase("keeper-hold")][TestCase("keeper-rise")][TestCase("keeper-roll")][TestCase("keeper-throw")][TestCase("run")]
        public void SpecialisedOrMovingPoseIsUnchangedByNewContext(string action)
        {
            var first=new GameObject("readiness-preservation-a");var second=new GameObject("readiness-preservation-b");
            try{var a=Build(first);var b=Build(second);Actor actor;var state=State(true,true,out actor);actor.action=action;actor.actionTime=.8f;actor.actionSequence=1;
                actor.actionTarget=new Point(-47,.3f);actor.actionHeight=.8f;actor.actionContactTime=.2f;if(action=="run")actor.velocity=new Point(3,0);
                Render(a,state,actor,0);Render(b,state,actor,0,true);
                for(int i=1;i<=15;i++){state.clock=100+i*.1f;Render(a,state,actor,1f/60);Render(b,state,actor,1f/60,true);}
                var aa=first.GetComponentsInChildren<Transform>();var bb=second.GetComponentsInChildren<Transform>();Assert.That(aa.Length,Is.EqualTo(bb.Length));
                for(int i=0;i<aa.Length;i++){Assert.That(Vector3.Distance(aa[i].localPosition,bb[i].localPosition),Is.LessThan(.00001),aa[i].name);Assert.That(Quaternion.Angle(aa[i].localRotation,bb[i].localRotation),Is.LessThan(.01),aa[i].name);}
            }finally{UnityEngine.Object.DestroyImmediate(first);UnityEngine.Object.DestroyImmediate(second);}
        }
        [Test] public void ContextCacheSeesReadinessChange()
        {var a=new PlayerMotionContext{keeperReadiness=new KeeperReadinessSample{active=true,weight=.25f}};var b=a;b.keeperReadiness.weight=1;Assert.That(a.Same(b),Is.False);}
        [Test] public void CompleteRenderReentryAfterControlIsProgressiveButIdentityResetMaySnap()
        {
            var root=new GameObject("readiness-control-return");
            try{
                var view=Build(root);Actor actor;var state=State(false,false,out actor);Render(view,state,actor,0);
                var rig=root.transform.Find("Rig");float far=rig.localPosition.y;
                actor.controlTime=.1f;state.clock=100.1f;Render(view,state,actor,1f/60);float exclusive=rig.localPosition.y;
                Assert.That(far-exclusive,Is.GreaterThan(.04));
                actor.controlTime=0;string before=JsonUtility.ToJson(state);Render(view,state,actor,0);
                Assert.That(rig.localPosition.y,Is.EqualTo(exclusive).Within(.00001),"Paused re-entry must preserve its displayed origin.");
                state.clock=100.2f;before=JsonUtility.ToJson(state);Render(view,state,actor,1f/60);float first=rig.localPosition.y;
                Assert.That(first-exclusive,Is.InRange(0f,.015f),"Ordinary re-entry is a small blend, not the old .064m snap.");
                Assert.That(JsonUtility.ToJson(state),Is.EqualTo(before));
                view.ResetPresentation();Render(view,state,actor,0);Assert.That(rig.localPosition.y-first,Is.GreaterThan(.03f));
            }finally{UnityEngine.Object.DestroyImmediate(root);}
        }
    }
}

using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Touchline.Core;
namespace Touchline.Tests
{
    public class SlidingReachPresentationTests
    {
        static PlayerView Build(out GameObject go,int height=181){go=new GameObject("Synthetic sliding reach");var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="slide-reach-proof",heightCm=height,preferredFoot="Right"},0,3,Color.white);return view;}
        static Actor Actor(int side)=>new Actor{slot=3,action="idle",position=new Point(),previous=new Point(),angle=0,diveSide=side};
        static Vector3 Impact(int side)=>new Vector3(-side*.641f,.11f,.785f);
        static void Start(Actor actor){actor.action="slide";actor.actionKind=MatchSimulation.SlidingDuel;actor.actionSequence=1;actor.actionTarget=new Point(0,1.2f);actor.actionHeight=.11f;actor.actionTime=MatchSimulation.SlidingDuelDuration;actor.actionContactTime=MatchSimulation.SlidingDuelContact;}
        static Transform Bone(PlayerView view,string name)=>view.GetComponentsInChildren<Transform>().First(t=>t.name==name);
        static Vector3 Ball(int side,float t)=>Vector3.Lerp(new Vector3(side*.4f,.11f,.85f),Impact(side),Mathf.Clamp01(t/MatchSimulation.SlidingDuelContact));
        static void Advance(Actor a,float t){a.actionTime=Mathf.Max(0,MatchSimulation.SlidingDuelDuration-t);if(t>=MatchSimulation.SlidingDuelContact-.00001f){var impact=Impact(a.diveSide>=0?1:-1);a.actionTarget=new Point(impact.x,impact.z);}}
        [TestCase(30,1,160)] [TestCase(30,-1,205)] [TestCase(60,1,181)] [TestCase(60,-1,181)] [TestCase(120,1,205)] [TestCase(120,-1,160)]
        public void CommittedMirroredSlideReachesRecordedImpactWithoutMovingActorOrBuryingKnees(int fps,int side,int stature){
            var view=Build(out var go,stature);try{
                var actor=Actor(side);view.Render(actor,1,1f/fps);Start(actor);float distance=float.MaxValue;
                for(int frame=0;frame<=Mathf.RoundToInt(.65f*fps);frame++){
                    float t=frame/(float)fps;Advance(actor,t);view.Render(actor,1,1f/fps,Ball(side,t));
                    Assert.AreEqual(Vector3.zero,view.transform.position,"The visual reach must never teleport the simulated actor");Assert.AreEqual(side,actor.diveSide,"Do not switch the committed leg during impact");
                    Assert.GreaterOrEqual(Bone(view,"lowerleg01.L").position.y,0,"Left knee below pitch");Assert.GreaterOrEqual(Bone(view,"lowerleg01.R").position.y,0,"Right knee below pitch");
                    if(Mathf.Abs(t-MatchSimulation.SlidingDuelContact)<.0001f)distance=Mathf.Min(Vector3.Distance(view.BootContactPosition(true),Impact(side)),Vector3.Distance(view.BootContactPosition(false),Impact(side)));
                }
                Assert.Less(distance,.18f,"The existing natural-contact tolerance remains unchanged");
            }finally{UnityEngine.Object.DestroyImmediate(go);}
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void AbsentHighOrDistantBallDoesNotRedirectCommittedPreparation(int invalidKind){
            var a=Build(out var ga);var b=Build(out var gb);try{
                var actorA=Actor(1);var actorB=Actor(1);a.Render(actorA,1,1f/60);b.Render(actorB,1,1f/60);Start(actorA);Start(actorB);
                Vector3 invalid=invalidKind==0?Vector3.zero:invalidKind==1?new Vector3(-.6f,1.2f,.8f):new Vector3(-4,.11f,4);
                for(int frame=0;frame<18;frame++){float t=frame/60f;Advance(actorA,t);Advance(actorB,t);a.Render(actorA,1,1f/60,invalid);b.Render(actorB,1,1f/60,new Vector3(10,.11f,10));Assert.Less(Vector3.Distance(a.FootPosition(true),b.FootPosition(true)),.0001f);Assert.Less(Vector3.Distance(a.FootPosition(false),b.FootPosition(false)),.0001f);}
            }finally{UnityEngine.Object.DestroyImmediate(ga);UnityEngine.Object.DestroyImmediate(gb);}
        }
        [Test] public void RecoveryRetainsRecordedPointInsteadOfFollowingReleasedBall(){
            var a=Build(out var ga);var b=Build(out var gb);try{
                var actorA=Actor(1);var actorB=Actor(1);a.Render(actorA,1,1f/60);b.Render(actorB,1,1f/60);Start(actorA);Start(actorB);
                for(int frame=0;frame<=90;frame++){
                    float t=frame/60f;Advance(actorA,t);Advance(actorB,t);var ball=Ball(1,t);a.Render(actorA,1,1f/60,ball);b.Render(actorB,1,1f/60,t<.3f?ball:new Vector3(.5f,.11f,-.5f));
                    if(t>=.3f){Assert.Less(Vector3.Distance(a.FootPosition(true),b.FootPosition(true)),.0001f);Assert.Less(Vector3.Distance(a.FootPosition(false),b.FootPosition(false)),.0001f);}
                }
            }finally{UnityEngine.Object.DestroyImmediate(ga);UnityEngine.Object.DestroyImmediate(gb);}
        }
        static float LargestApproachStep(int fps){var v=Build(out var go);try{var actor=Actor(1);v.Render(actor,1,1f/fps);Start(actor);Vector3 last=Vector3.zero;float maximum=0;for(int frame=0;frame<=Mathf.RoundToInt(.4f*fps);frame++){float t=frame/(float)fps;Advance(actor,t);v.Render(actor,1,1f/fps,Ball(1,t));var rig=Bone(v,"Rig").position;if(t>=.22f)maximum=Mathf.Max(maximum,Vector3.Distance(last,rig));last=rig;}return maximum;}finally{UnityEngine.Object.DestroyImmediate(go);}}
        [Test] public void ApproachDisplacementConvergesWhenFrameRateQuadruples(){float coarse=LargestApproachStep(30),fine=LargestApproachStep(120);TestContext.WriteLine("Approach rig maxstep30="+coarse+" 120="+fine);Assert.Greater(coarse,.001f);Assert.Less(fine,coarse*.45f,"A snapshot-boundary pose jump would persist at high framerate");}
    }
}

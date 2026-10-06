using NUnit.Framework;
using UnityEngine;
using Touchline.Core;

namespace Touchline.Tests
{
    public class PreparedDuelMotionTests
    {
        [TestCase(30,1)] [TestCase(60,1)] [TestCase(120,1)] [TestCase(60,-1)]
        public void FootApproachesImpactBeforeContactAndRecoversWithoutTeleport(int fps,int side)
        {
            var go=new GameObject("Prepared standing duel");try{
                var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="prepared",heightCm=180},0,2,Color.white);
                var actor=new Actor{slot=2,action="idle"};for(int i=0;i<30;i++)view.Render(actor,1,1f/fps);
                var ball=new Vector3(side*.85f,.11f,.42f);actor.action="tackle";actor.actionKind=MatchSimulation.StandingDuel;actor.actionSequence=1;actor.actionContactTime=.2f;actor.actionTarget=new Point(ball.x,ball.z);actor.actionHeight=.11f;
                float startDistance=0,contactDistance=0,maxStep=0;var previous=view.BootContactPosition(side>0);
                for(int i=0;i<=fps;i++){
                    float time=i/(float)fps;actor.actionTime=Mathf.Max(0,.75f-time);if(time>.75f)actor.action="idle";
                    view.Render(actor,1,1f/fps,ball);var boot=view.BootContactPosition(side>0);
                    if(i==0)startDistance=Vector3.Distance(boot,ball);
                    if(i==fps/5)contactDistance=Vector3.Distance(boot,ball);
                    maxStep=Mathf.Max(maxStep,Vector3.Distance(previous,boot));previous=boot;
                    Assert.That(view.FootPosition(side<0).y,Is.GreaterThan(.025f));
                }
                Assert.Greater(startDistance,.4f,"Do not extend the foot instantly when the duel begins");
                Assert.Less(contactDistance,.13f,"Reach at 0.2 seconds, not at the first preparation frame");
                Assert.Less(maxStep,.3f,"No one-frame foot teleport during the prepared gesture");
                Assert.AreEqual(Vector3.zero,go.transform.position);
            }finally{Object.DestroyImmediate(go);}
        }
        [Test] public void InterpolatedRenderReachesTheSameContactBetweenSimulationTicks()
        {
            var go=new GameObject("Substep duel");try{
                var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="substep-duel",heightCm=185},0,2,Color.white);
                var actor=new Actor{slot=2,action="tackle",actionKind=MatchSimulation.StandingDuel,actionSequence=1,actionContactTime=.2f,actionTarget=new Point(.85f,.42f),actionHeight=.11f};
                var ball=new Vector3(.85f,.11f,.42f);float previousDistance=10;
                // The second fixed tick holds the contact state; alpha walks
                // through the preceding 0.1 seconds of preparation.
                actor.actionTime=.55f;
                for(int i=0;i<=6;i++){view.Render(actor,i/6f,1f/60,ball);float distance=Vector3.Distance(view.BootContactPosition(true),ball);Assert.LessOrEqual(distance,previousDistance+.03f);previousDistance=distance;}
                Assert.Less(previousDistance,.13f);
            }finally{Object.DestroyImmediate(go);}
        }
    }
}

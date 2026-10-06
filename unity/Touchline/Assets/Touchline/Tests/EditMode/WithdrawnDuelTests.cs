using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Touchline.Core;

namespace Touchline.Tests
{
    public class WithdrawnDuelTests
    {
        static float Reach(Actor actor,float remaining)=>(float)typeof(PlayerView).GetMethod("DuelReach",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{actor,remaining});
        [TestCase(.65f)] [TestCase(.55f)] [TestCase(.4f)]
        public void CancellationContinuesFromExactExtensionAndInterpolatesPreviousTick(float original)
        {
            var a=new Actor{action="tackle",actionKind=MatchSimulation.StandingDuel};
            float contact=Reach(a,original),prior=Reach(a,original+.05f);
            a.tackleWithdrawFrom=original;float duration=Mathf.Min(original,MatchSimulation.TackleRecovery*.35f);
            Assert.That(Reach(a,duration),Is.EqualTo(contact).Within(.00001f));
            Assert.That(Reach(a,duration+.05f),Is.EqualTo(prior).Within(.00001f));
            float last=contact;
            for(int i=1;i<=20;i++){float value=Reach(a,duration*(1-i/20f));Assert.LessOrEqual(value,last);last=value;}
            Assert.AreEqual(0,last);
            var restored=JsonUtility.FromJson<Actor>(JsonUtility.ToJson(a));Assert.AreEqual(Reach(a,duration*.7f),Reach(restored,duration*.7f));
        }
        [TestCase(30,1)] [TestCase(60,1)] [TestCase(120,-1)] [TestCase(60,-1)]
        public void CancelledBootRetractsWithoutTeleportOrRootMovement(int fps,int side)
        {
            var go=new GameObject("Withdrawn duel");try{
                var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="withdrawn",heightCm=180},0,2,Color.white);
                var a=new Actor{slot=2};for(int i=0;i<30;i++)view.Render(a,1,1f/fps);
                var ball=new Vector3(side*.85f,.11f,.42f);a.action="tackle";a.actionKind=MatchSimulation.StandingDuel;a.actionSequence=1;a.actionTarget=new Point(ball.x,ball.z);a.actionContactTime=.2f;
                var previous=view.BootContactPosition(side>0);float maxStep=0,maxWithdrawal=0;
                for(int i=0;i<=fps;i++){
                    float time=i/(float)fps;
                    if(time<.1f)a.actionTime=.75f-time;
                    else {a.tackleWithdrawFrom=.65f;a.actionTime=Mathf.Max(0,MatchSimulation.TackleRecovery*.35f-(time-.1f));if(a.actionTime<=0)a.action="idle";}
                    view.Render(a,1,1f/fps,ball);var boot=view.BootContactPosition(side>0);float step=Vector3.Distance(boot,previous);maxStep=Mathf.Max(maxStep,step);if(time>.1001f)maxWithdrawal=Mathf.Max(maxWithdrawal,step);previous=boot;
                    Assert.Greater(view.FootPosition(side<0).y,.025f);
                }
                TestContext.WriteLine("Maximum frame displacement: "+maxStep+" m; withdrawal: "+maxWithdrawal+" m");
                Assert.Less(maxStep,.3f,"Same preparation limit as a committed tackle");Assert.Less(maxWithdrawal,.18f,"Withdrawal has no sudden jump to the resting foot");Assert.AreEqual(Vector3.zero,go.transform.position);
            }finally{Object.DestroyImmediate(go);}
        }
        [Test] public void WithdrawalInvalidatesPausedPoseOnlyOnce()
        {
            var cache=new PausedPoseCache();var a=new Actor{id="withdraw"};cache.NeedsUpdate(0,a,Vector3.zero);a.tackleWithdrawFrom=.65f;
            Assert.IsTrue(cache.NeedsUpdate(0,a,Vector3.zero));Assert.IsFalse(cache.NeedsUpdate(0,a,Vector3.zero));
        }
    }
}

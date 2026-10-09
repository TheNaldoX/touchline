using NUnit.Framework;
using Touchline.Core;
using UnityEngine;

namespace Touchline.Tests
{
    public class LocomotionFacingTests
    {
        [TestCase(false,"pass")] [TestCase(true,"pass")]
        [TestCase(false,"through")] [TestCase(true,"through")]
        [TestCase(false,"cutback")] [TestCase(true,"cutback")]
        public void CapturedShortPassUsesPreferredFoot(bool left,string kind)
        {
            bool before=PlayerView.UseMecanim;var root=new GameObject("Preferred passing foot");
            try{
                PlayerView.UseMecanim=true;
                var view=root.AddComponent<PlayerView>();view.Build(new PlayerData{id="passing-foot",heightCm=182,preferredFoot=left?"Left":"Right"},0,9,Color.blue);
                Assert.IsTrue(PlayerView.MecanimReady,"Imported football clips are required for this regression");
                var actor=new Actor{id="passing-foot",slot=9,action="kick",actionKind=kind,actionSequence=1,actionTime=.64f};
                for(int i=0;i<12;i++){actor.actionTime=.64f-i/60f;view.Render(actor,1,1f/60,new Vector3(0,.11f,.42f));}
                Assert.AreEqual(left?"Soccer Pass (miroir)":"Soccer Pass",view.MecanimGestureClip);
                Assert.Greater(view.FootPosition(left).z,view.FootPosition(!left).z,"The preferred foot must follow through in front of the support foot");
                Assert.AreEqual(Vector3.zero,root.transform.position,"The presentation must not displace the simulated player");
            }finally{Object.DestroyImmediate(root);PlayerView.UseMecanim=before;}
        }
        [TestCase(160)] [TestCase(182)] [TestCase(200)]
        public void ReplacementStepStartsAtVisibleFootInsteadOfUnreachableAnchor(int height)
        {
            var root=new GameObject("Foot step origin");
            try{
                var view=root.AddComponent<PlayerView>();view.Build(new PlayerData{id="support-origin",heightCm=height},0,9,Color.blue);
                var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
                T Field<T>(string name)=>(T)typeof(PlayerView).GetField(name,flags).GetValue(view);
                Vector3 before=view.FootPosition(true);
                Assert.Less(before.y,.12f*root.transform.localScale.y,"Fixture must exercise a grounded foot");
                var previous=Field<Vector3[]>("previousFeet");previous[0]=before;previous[1]=view.FootPosition(false);
                Field<bool[]>("footLocked")[0]=true;Field<float[]>("footLockWeight")[0]=1;
                Field<Vector3[]>("footLockPoint")[0]=before+Vector3.back*.8f;
                typeof(PlayerView).GetMethod("MecanimFootLock",flags).Invoke(view,new object[]{1f/60,false});
                Assert.Greater(Field<float[]>("footStep")[0],0,"Stale support must initiate a recovery step");
                Assert.Less(Vector3.Distance(before,view.FootPosition(true)),.015f,"The first step frame must not jump towards the stale anchor");
            }finally{Object.DestroyImmediate(root);}
        }
        [TestCase(30)] [TestCase(60)] [TestCase(120)]
        public void StrafeReversalBlendsContinuouslyAtDifferentFrameRates(int fps)
        {
            float right=1,first=PlayerView.StrafeBlend(1,0,1f/fps);
            Assert.Greater(first,.7f,"No instant left/right pose swap");Assert.Less(first,1);
            for(int i=0;i<fps;i++){float next=PlayerView.StrafeBlend(right,0,1f/fps);Assert.That(next,Is.InRange(0f,right));right=next;}
            Assert.AreEqual(Mathf.Exp(-8),right,.00001f);
            Assert.AreEqual(right,PlayerView.StrafeBlend(right,1,0),"Paused pose stays fixed");
        }
        [TestCase(30)][TestCase(60)][TestCase(120)]
        public void ReversalBuildsMomentumAndSettlesWithoutOvershoot(int fps)
        {
            var facing=new LocomotionFacing();facing.Sample(0,7,0,true);
            float first=facing.Sample(179,7,1f/fps),previous=first;
            Assert.Less(first,2,"A reversal must not turn tens of degrees in its first frame");
            for(int i=1;i<fps*2;i++){
                float angle=facing.Sample(179,7,1f/fps);
                Assert.That(angle,Is.InRange(previous-.001f,179.001f));
                Assert.LessOrEqual(angle-previous,LocomotionFacing.MaximumRate(7)/fps+.002f);
                previous=angle;
            }
            Assert.AreEqual(179,previous,.02f);Assert.AreEqual(0,facing.Velocity,.02f);
        }
        [Test] public void IdenticalTurnAndCounterTurnAgreeAtThirtySixtyAndOneTwentyFrames()
        {
            float[] angles=new float[3],velocities=new float[3];int index=0;
            foreach(int fps in new[]{30,60,120}){
                var facing=new LocomotionFacing();facing.Sample(350,3,0,true);
                for(int frame=0;frame<fps;frame++)facing.Sample(frame<fps/2?120:290,3,1f/fps);
                angles[index]=facing.Angle;velocities[index++]=facing.Velocity;
            }
            for(int i=1;i<3;i++){Assert.Less(Mathf.Abs(Mathf.DeltaAngle(angles[0],angles[i])),.02f);Assert.AreEqual(velocities[0],velocities[i],.02f);}
        }
        [Test] public void PauseAndInvalidInputCannotAdvanceFacingAndResetIsImmediate()
        {
            var facing=new LocomotionFacing();facing.Sample(0,4,0,true);facing.Sample(90,4,.1f);
            float angle=facing.Angle,velocity=facing.Velocity;
            Assert.AreEqual(angle,facing.Sample(180,4,0));Assert.AreEqual(velocity,facing.Velocity);
            Assert.AreEqual(angle,facing.Sample(float.NaN,4,.1f));Assert.AreEqual(angle,facing.Sample(180,4,-.1f));
            Assert.AreEqual(270,facing.Sample(270,4,0,true));Assert.AreEqual(0,facing.Velocity);
        }
        [Test] public void WrapBoundaryTakesTheShortWay()
        {
            var facing=new LocomotionFacing();facing.Sample(350,0,0,true);
            for(int i=0;i<60;i++)facing.Sample(10,0,1f/60);
            Assert.AreEqual(10,facing.Angle,.02f);
        }
        [TestCase(30,1)][TestCase(30,2)][TestCase(30,5)][TestCase(30,10)]
        [TestCase(60,1)][TestCase(60,2)][TestCase(60,5)][TestCase(60,10)]
        [TestCase(120,1)][TestCase(120,2)][TestCase(120,5)][TestCase(120,10)]
        public void PlaybackRatePreservesTheSameSimulatedTurnTime(int fps,int playback)
        {
            var facing=new LocomotionFacing();facing.Sample(0,7,0,true);
            for(int frame=0;frame<fps/(3*playback);frame++)facing.Sample(179,7,(float)playback/fps);
            Assert.AreEqual(69.75f,facing.Angle,.03f,"A third of a simulated second must not discard elapsed time at x10");
        }
        [Test] public void SlowTenFpsPlaybackAndPausedPoseRefreshConsumeTheWholeSecond()
        {
            var facing=new LocomotionFacing();facing.Sample(0,7,0,true);
            facing.Sample(179,7,1);Assert.AreEqual(179,facing.Angle,.02f);Assert.AreEqual(0,facing.Velocity,.02f);
        }
        [Test] public void SubstitutionAndTeleportSnapFacingWithoutMovingAuthoritativePosition()
        {
            var go=new GameObject("Locomotion facing reset");try{
                var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="first",heightCm=182},0,9,Color.blue);
                var actor=new Actor{id="first",slot=9,action="run",angle=0,position=new Point(1,2),previous=new Point(1,2)};
                view.Render(actor,1,.016f);actor.angle=2;view.Render(actor,1,.016f);
                Assert.Less(view.transform.eulerAngles.y,2);
                view.ChangeIdentity(new PlayerData{id="incoming",heightCm=178});actor.id="incoming";
                view.Render(actor,1,.016f);Assert.Less(Quaternion.Angle(view.transform.rotation,Quaternion.Euler(0,2*Mathf.Rad2Deg,0)),.02f);
                actor.angle=-1;actor.position=actor.previous=new Point(20,30);view.Render(actor,1,.016f);
                Assert.Less(Quaternion.Angle(view.transform.rotation,Quaternion.Euler(0,-Mathf.Rad2Deg,0)),.02f);
                Assert.AreEqual(new Vector3(20,0,30),view.transform.position);
                actor.position=new Point(21,31);actor.previous=new Point(20,30);view.Render(actor,.4f,.016f);
                Assert.Less(Vector3.Distance(new Vector3(20.4f,0,30.4f),view.transform.position),.0001f);
            }finally{Object.DestroyImmediate(go);}
        }
        [TestCase(.1f)] [TestCase(1f)] public void KickKeepsContactOrientationWithinExistingTurnLimit(float angle)
        {
            var go=new GameObject("Contact facing preserved");try{
                var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="contact",heightCm=182},0,9,Color.blue);
                var actor=new Actor{id="contact",slot=9,action="run",angle=0};view.Render(actor,1,.016f);
                actor.action="kick";actor.angle=angle;actor.actionTime=.5f;
                view.Render(actor,1,.016f);
                const float contactTurnDegreesPerSecond=600f; // Existing PlayerView gesture rotation cap.
                float expected=Mathf.Min(angle*Mathf.Rad2Deg*(1-Mathf.Exp(-.016f*16)),contactTurnDegreesPerSecond*.016f);
                Assert.AreEqual(expected,view.transform.eulerAngles.y,.02f);
            }finally{Object.DestroyImmediate(go);}
        }
    }
}

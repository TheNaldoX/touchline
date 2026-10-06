using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Touchline.Core;
using UnityEngine;

namespace Touchline.Tests
{
    public class LocomotionAccelerationTests
    {
        [TestCase(30)][TestCase(60)][TestCase(120)]
        public void FixedTickRampHasContinuousLeanAtEveryRefreshRate(int fps)
        {
            var lean=new LocomotionAcceleration();lean.Sample(0,0,true,0,true);float minimum=6,maximum=-6;
            for(int frame=1;frame<=fps*2;frame++){
                // Six metres per second reached in two seconds, sampled at
                // the engine's ten ticks per second, not the rendering rate.
                float clock=(float)Math.Floor((double)frame/fps*10+1e-7)/10;
                float result=lean.Sample(clock*3,1f/fps,true,clock);
                if(frame>=fps){minimum=Mathf.Min(minimum,result);maximum=Mathf.Max(maximum,result);}
            }
            Assert.That(minimum,Is.GreaterThan(2.99f));Assert.That(maximum,Is.LessThan(3.001f));
            Assert.That(maximum-minimum,Is.LessThan(.01f),"No ten-Hz body pulses between unchanged velocity samples");
        }
        [TestCase(30)][TestCase(60)][TestCase(120)]
        public void BrakingStopAndRestartAreBoundedAndRecover(int fps)
        {
            var lean=new LocomotionAcceleration();lean.Sample(3,0,true,0,true);float brake=0,rest=0,restart=0;
            for(int frame=1;frame<=fps*4;frame++){
                float clock=(float)Math.Floor((double)frame/fps*10+1e-7)/10;
                float speed=clock<1?3*(1-clock):clock<2?0:clock<3?3*(clock-2):3;
                float value=lean.Sample(speed,1f/fps,true,clock);Assert.That(value,Is.InRange(-6f,6f));
                if(frame==fps/2)brake=value;if(frame==fps*2)rest=value;if(frame==fps*5/2)restart=value;
            }
            Assert.Less(brake,-2.8f);Assert.Less(Mathf.Abs(rest),.003f);Assert.Greater(restart,2.8f);Assert.Less(Mathf.Abs(lean.Value),.003f);
        }
        [Test] public void ResetDiscontinuityAndRepeatedClockNeverInventAcceleration()
        {
            var lean=new LocomotionAcceleration();lean.Sample(0,0,true,10,true);lean.Sample(.6f,.1f,true,10.1f);
            Assert.Greater(lean.Value,0);Assert.AreEqual(0,lean.Sample(8,.016f,true,10.1f,true));
            Assert.AreEqual(0,lean.Sample(8,.016f,true,10.1f));
            Assert.AreEqual(0,lean.Sample(2,.016f,true,5));Assert.AreEqual(0,lean.Sample(7,.016f,true,20));
            Assert.AreEqual(0,lean.Sample(7,0,false,0));Assert.AreEqual(0,lean.Sample(7,.016f,false,0));
        }
        [Test] public void ClockContextUsesSimulationTimeRatherThanDisplayInterpolation()
        {
            var actor=new Actor{id="context",side=0};var match=new MatchState{clock=12.3f,actors=new[]{actor},ball=new BallState{owner="context"}};
            var a=PlayerMotionContext.From(match,actor,.1f);var b=PlayerMotionContext.From(match,actor,.9f);
            Assert.IsTrue(a.hasSimulationClock);Assert.AreEqual(12.3f,a.simulationClock);Assert.AreEqual(a.simulationClock,b.simulationClock);
        }
        [Test] public void PlayerPresentationResetAndSubstitutionClearThePreviousLean()
        {
            var go=new GameObject("Locomotion reset regression");try{
                var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="first",heightCm=182},0,9,Color.blue);
                var actor=new Actor{id="first",slot=9,action="run",velocity=new Point()};
                view.Render(actor,1,.1f,default,new PlayerMotionContext{hasSimulationClock=true,simulationClock=10});
                actor.velocity=new Point(0,.6f);view.Render(actor,1,.1f,default,new PlayerMotionContext{hasSimulationClock=true,simulationClock=10.1f});
                var field=typeof(PlayerView).GetField("accelerationLean",BindingFlags.Instance|BindingFlags.NonPublic);
                Assert.Greater((float)field.GetValue(view),0);view.ResetPresentation();view.Render(actor,1,.1f,default,new PlayerMotionContext{hasSimulationClock=true,simulationClock=10.1f});Assert.AreEqual(0,(float)field.GetValue(view));
                actor.velocity=new Point(0,1.2f);view.Render(actor,1,.1f,default,new PlayerMotionContext{hasSimulationClock=true,simulationClock=10.2f});Assert.Greater((float)field.GetValue(view),0);
                view.ChangeIdentity(new PlayerData{id="incoming",heightCm=178});actor.id="incoming";actor.velocity=new Point(0,4);view.Render(actor,1,.1f,default,new PlayerMotionContext{hasSimulationClock=true,simulationClock=10.2f});Assert.AreEqual(0,(float)field.GetValue(view));
                Assert.AreEqual(new Vector3(actor.position.x,0,actor.position.z),view.transform.position);
            }finally{UnityEngine.Object.DestroyImmediate(go);}
        }
    }
}

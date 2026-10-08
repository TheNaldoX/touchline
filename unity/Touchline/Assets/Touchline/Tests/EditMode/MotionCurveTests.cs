using System;
using NUnit.Framework;
using Touchline.Core;

namespace Touchline.Tests
{
    // Trajectoire de rendu : mêmes positions que la simulation à chaque pas,
    // mais vitesse continue entre deux pas (plus de cassure à 10 Hz).
    public sealed class MotionCurveTests
    {
        const float Step=MatchSimulation.Step;

        [Test]
        public void PassesThroughSimulationPositionsAtEachStep()
        {
            var curve=new MotionCurve();var a=new Point(0,0);var b=new Point(.5f,0);var c=new Point(.9f,.3f);
            curve.Observe(a,b,Step);curve.Observe(b,c,Step);
            var start=curve.Position(b,c,Step,0);var end=curve.Position(b,c,Step,1);
            Assert.AreEqual(b.x,start.x,1e-5f);Assert.AreEqual(b.z,start.z,1e-5f);
            Assert.AreEqual(c.x,end.x,1e-5f);Assert.AreEqual(c.z,end.z,1e-5f);
        }

        [Test]
        public void VelocityIsContinuousAcrossSteps()
        {
            // Un joueur qui tourne : la direction change à chaque pas.
            var curve=new MotionCurve();Point prev=new Point(0,0);Point lastEndVelocity=default;bool first=true;
            for(int k=1;k<=20;k++){
                float angle=k*.25f;var next=prev+new Point((float)Math.Cos(angle),(float)Math.Sin(angle))*(6*Step);
                curve.Observe(prev,next,Step);
                var startVelocity=curve.Velocity(prev,next,Step,0);
                if(k>2&&!first)Assert.AreEqual(0,Point.Distance(startVelocity,lastEndVelocity),1e-3f,"Cassure de vitesse au pas "+k);
                lastEndVelocity=curve.Velocity(prev,next,Step,1);first=false;prev=next;
            }
        }

        [Test]
        public void LinearSegmentsHaveVelocityJumpsTheCurveRemoves()
        {
            // Avec une interpolation linéaire, la vitesse saute de 6 m/s × 2 sin(0,125) ≈ 1,5 m/s à chaque pas.
            var curve=new MotionCurve();var a=new Point(0,0);var b=new Point(.6f,0);var c=b+new Point((float)Math.Cos(.25f),(float)Math.Sin(.25f))*.6f;
            curve.Observe(a,b,Step);curve.Observe(b,c,Step);
            var linearJump=Point.Distance((c-b)/Step,(b-a)/Step);
            var curveJump=Point.Distance(curve.Velocity(b,c,Step,0),(b-a)/Step);
            Assert.Greater(linearJump,1f);Assert.Less(curveJump,1e-3f);
        }

        [Test]
        public void TeleportFallsBackToLinear()
        {
            var curve=new MotionCurve();var a=new Point(0,0);var b=new Point(.5f,0);var far=new Point(30,10);
            curve.Observe(a,b,Step);curve.Observe(b,far,Step);
            var mid=curve.Position(b,far,Step,.5f);var lerp=Point.Lerp(b,far,.5f);
            Assert.AreEqual(lerp.x,mid.x,1e-4f);Assert.AreEqual(lerp.z,mid.z,1e-4f);
        }
    }
}

using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Touchline.Core;

namespace Touchline.Tests
{
    public class BodyContactMotionTests
    {
        [TestCase(30,-1)] [TestCase(60,-1)] [TestCase(120,1)] [TestCase(60,1)]
        public void BodyBracePreservesTheRootAndGroundedFeet(int fps,int side){var go=new GameObject("Contact balance pose");try{var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="brace",heightCm=180},0,6,Color.white);var a=new Actor{slot=6,action="idle"};for(int i=0;i<fps;i++)view.Render(a,1,1f/fps);var spine=go.GetComponentsInChildren<Transform>().First(t=>t.name=="spine02");var rest=spine.localRotation;var context=new PlayerMotionContext{contactWeight=1,contactDirection=new Point(side,0)};for(int i=0;i<fps;i++){view.Render(a,1,1f/fps,Vector3.zero,context);Assert.Greater(view.FootPosition(true).y,.02f);Assert.Greater(view.FootPosition(false).y,.02f);}Assert.Greater(Quaternion.Angle(rest,spine.localRotation),4);Assert.AreEqual(Vector3.zero,go.transform.position);}finally{Object.DestroyImmediate(go);}}
        [Test] public void ContactContextSurvivesSaveAndIgnoresExcludedOpponents(){var a=new Actor{side=0,slot=6,action="run",position=new Point(),velocity=new Point(2,0)};var b=new Actor{side=1,slot=6,action="run",position=new Point(.65f,0),velocity=new Point(-2,0)};var m=new MatchState{restart=0,phase="play",actors=new[]{a,b}};var original=PlayerMotionContext.From(m,a,1);Assert.Greater(original.contactWeight,.5f);var restored=JsonUtility.FromJson<MatchState>(JsonUtility.ToJson(m));Assert.IsTrue(original.Same(PlayerMotionContext.From(restored,restored.actors[0],1)));b.sentOff=true;Assert.AreEqual(0,PlayerMotionContext.From(m,a,1).contactWeight);}
    }
}

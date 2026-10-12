using System.Linq;
using NUnit.Framework;
using Touchline.Core;
using UnityEngine;

namespace Touchline.Tests
{
    public class SoccerMocapTests
    {
        const string Folder="Animations/RohrSoccer/";
        [TestCase(9f,6f,1.4f,42)] [TestCase(9f,8f,3f,30)] [TestCase(9f,0f,3f,90)]
        public void ReviewWindowReachesLateContactWithoutHoldingPastClipEnd(float length,float start,float duration,int frames)
        {
            Assert.AreEqual(frames,Touchline.Editor.MecanimPrototypeFilm.CaptureFrameCount(length,false,start,duration));
        }
        [TestCase(-1f,1f)] [TestCase(9f,1f)] [TestCase(float.NaN,1f)] [TestCase(0f,0f)] [TestCase(0f,float.PositiveInfinity)]
        public void ReviewWindowRejectsInvalidTimes(float start,float duration)
        {
            Assert.Throws<System.ArgumentException>(()=>Touchline.Editor.MecanimPrototypeFilm.CaptureFrameCount(9f,false,start,duration));
        }
        static void RequirePack(){if(Resources.Load<AnimationClip>(Folder+"Rohr_08_Side_Foot_Kick")==null)Assert.Ignore("Personal licensed pack not installed; run tools/import-soccer-mocap.ps1 then the Unity import menu.");}
        [Test] public void PersonalPackContainsTwentyOneHumanoidClipsWithoutSourceMeshes()
        {
            RequirePack();var assets=Resources.LoadAll<Object>(Folder.TrimEnd('/'));
            var clips=assets.OfType<AnimationClip>().ToArray();Assert.AreEqual(21,clips.Length);
            Assert.AreEqual(21,clips.Select(c=>c.name).Distinct().Count());
            Assert.IsFalse(assets.Any(a=>a is GameObject||a is Mesh||a is Material));
            foreach(var clip in clips){Assert.IsTrue(clip.humanMotion,clip.name);Assert.Greater(clip.length,.1f);Assert.AreEqual(60,clip.frameRate,.01f);}
        }
        [TestCase(160,30)] [TestCase(182,60)] [TestCase(200,120)]
        public void ImportedInsidePassKeepsThePhysicalContact(int height,int fps)
        {
            RequirePack();var root=new GameObject("Rohr pass contact");bool before=PlayerView.UseMecanim;
            try{
                PlayerView.UseMecanim=true;var view=root.AddComponent<PlayerView>();view.Build(new PlayerData{id="rohr-pass",heightCm=height,preferredFoot="Right"},0,9,Color.blue);
                var ball=new Vector3(0,.11f,.42f);
                var actor=new Actor{slot=9,action="kick",actionKind="pass",actionSequence=2,actionTarget=new Point(ball.x,ball.z),actionHeight=ball.y};
                float last=0;
                for(int frame=0;frame/(float)fps<.18f;frame++){float elapsed=frame/(float)fps;actor.actionTime=.64f-elapsed;view.Render(actor,1,1f/fps,ball);last=elapsed;}
                actor.actionTime=.46f;view.Render(actor,1,.18f-last,ball);
                Assert.AreEqual("Rohr_08_Side_Foot_Kick",view.MecanimGestureClip);
                Assert.Less(Vector3.Distance(view.InsideFootContactPosition(false),ball),.055f);
                Assert.Greater(view.FootPosition(true).y,.03f);Assert.AreEqual(Vector3.zero,root.transform.position);
            }finally{Object.DestroyImmediate(root);PlayerView.UseMecanim=before;}
        }
    }
}

using System.Linq;
using NUnit.Framework;
using Touchline.Core;
using UnityEngine;

namespace Touchline.Tests
{
    public class SoccerMocapTests
    {
        const string Folder="Animations/RohrSoccer/";
        [TestCase(160)] [TestCase(182)] [TestCase(200)]
        public void ReviewMeshMatchesAnatomicalSkinningAtEveryStature(int height)
        {
            var root=new GameObject("Capture scale regression");var baked=new Mesh();
            try{
                var view=root.AddComponent<PlayerView>();view.Build(new PlayerData{id="capture-scale",heightCm=height},0,9,Color.blue);view.Render(new Actor{slot=9,action="idle"},1,.02f);
                var skin=root.GetComponentInChildren<SkinnedMeshRenderer>();skin.BakeMesh(baked,true);
                var source=skin.sharedMesh.vertices;var captured=baked.vertices;var weights=skin.sharedMesh.boneWeights;var bind=skin.sharedMesh.bindposes;var bones=skin.bones;
                for(int i=0;i<source.Length;i+=Mathf.Max(1,source.Length/100)){
                    var w=weights[i];Vector3 Joint(int index)=>bones[index].TransformPoint(bind[index].MultiplyPoint3x4(source[i]));
                    var expected=Joint(w.boneIndex0)*w.weight0+Joint(w.boneIndex1)*w.weight1+Joint(w.boneIndex2)*w.weight2+Joint(w.boneIndex3)*w.weight3;
                    Assert.Less(Vector3.Distance(expected,skin.transform.TransformPoint(captured[i])),.002f,"Capture must match bone skinning, without a second stature scale");
                }
            }finally{Object.DestroyImmediate(baked);Object.DestroyImmediate(root);}
        }
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
        [TestCase(.22f,30)] [TestCase(.4f,60)] [TestCase(.6f,120)]
        public void CapturedLowGatherMeetsBallWithoutLateralDive(float height,int fps)
        {
            RequirePack();var root=new GameObject("Low gather");
            try{
                var view=root.AddComponent<PlayerView>();view.Build(new PlayerData{id="low-gather",heightCm=182},0,0,Color.yellow);
                var actor=new Actor{slot=0,action="dive",actionKind="save-catch",actionSequence=1,actionContactTime=.2f,actionHeight=height,actionTarget=new Point(.2f,.65f)};
                var ball=new Vector3(.2f,height,.65f);var animator=root.GetComponentInChildren<Animator>();
                for(int frame=0;frame<=fps;frame++){
                    float t=frame/(float)fps;actor.actionTime=1.2f-t;view.Render(actor,1,1f/fps,ball);animator=root.GetComponentInChildren<Animator>();
                    Assert.AreEqual("Rohr_17_Goalkeeper_Save_03",view.MecanimGestureClip);
                    var spine=animator.GetBoneTransform(HumanBodyBones.Chest).position-animator.GetBoneTransform(HumanBodyBones.Hips).position;
                    Assert.Less(Mathf.Abs(Vector3.Dot(spine.normalized,root.transform.right)),.6f,"Low scoop must not roll sideways");
                    if(frame==fps/5)Assert.Less(Vector3.Distance(view.HeldBallPosition,ball),.15f);
                }
            }finally{Object.DestroyImmediate(root);}
        }
        [TestCase(160,30)] [TestCase(180,30)] [TestCase(182,60)] [TestCase(200,120)]
        public void CapturedThrowHasTwoHandsOnRelease(int height,int fps)
        {
            RequirePack();var root=new GameObject("Overhead release");
            try{
                var view=root.AddComponent<PlayerView>();view.Build(new PlayerData{id="thrower",heightCm=height},0,9,Color.blue);
                // MatchDecisions.Flight sets throw release height to the player's actual stature.
                var ball=new Vector3(0,height*.01f,.42f);var actor=new Actor{slot=9,action="throw",actionKind="throw",actionSequence=1,actionContactTime=.5f,actionHeight=ball.y,actionTarget=new Point(0,.42f)};
                for(int frame=0;frame<=fps/2;frame++){actor.actionTime=1.1f-frame/(float)fps;view.Render(actor,1,1f/fps,ball);}
                Assert.AreEqual("Rohr_07_Throw_In",view.MecanimGestureClip);
                var animator=root.GetComponentInChildren<Animator>();
                var skin=root.GetComponentInChildren<SkinnedMeshRenderer>();var baked=new Mesh();
                try{
                    skin.BakeMesh(baked,true);var vertices=baked.vertices;var weights=skin.sharedMesh.boneWeights;var bones=skin.bones;
                    foreach(var side in new[]{".L",".R"}){
                        float distance=float.MaxValue;
                        for(int i=0;i<vertices.Length;i++){string name=bones[weights[i].boneIndex0].name;if((name.StartsWith("finger")||name.StartsWith("wrist"))&&name.EndsWith(side))distance=Mathf.Min(distance,Vector3.Distance(skin.transform.TransformPoint(vertices[i]),ball));}
                        Assert.That(distance,Is.InRange(.07f,.135f),"The actual hand skin must meet the 11cm-radius football surface without sinking into its centre");
                    }
                }finally{Object.DestroyImmediate(baked);}
                Assert.Less(Vector3.Distance(view.HeldBallPosition,ball),.12f,"The held-ball anchor must agree with the actual release");
                foreach(var bone in new[]{HumanBodyBones.LeftFoot,HumanBodyBones.RightFoot}){
                    var foot=animator.GetBoneTransform(bone);var rotation=foot.rotation;var center=rotation*new Vector3(0,.02f,.075f);
                    float extent=Mathf.Abs((rotation*Vector3.right).y)*.057f+Mathf.Abs((rotation*Vector3.up).y)*.085f+Mathf.Abs((rotation*Vector3.forward).y)*.145f;
                    Assert.That(foot.position.y+(center.y-extent)*root.transform.localScale.y,Is.InRange(-.005f,.04f),"Throwing must keep both feet on turf");
                }
                Assert.AreEqual(Vector3.zero,root.transform.position);
            }finally{Object.DestroyImmediate(root);}
        }
        [TestCase(30)] [TestCase(60)] [TestCase(120)]
        public void CapturedCelebrationFollowsGoalClockAndEnds(int fps)
        {
            RequirePack();var root=new GameObject("Goal capture");bool before=PlayerView.UseMecanim;
            try{
                PlayerView.UseMecanim=true;var view=root.AddComponent<PlayerView>();view.Build(new PlayerData{id="scorer",heightCm=182},0,9,Color.blue);
                var actor=new Actor{slot=9,action="idle"};
                for(int f=0;f<=fps;f++)view.Render(actor,1,1f/fps,default,new PlayerMotionContext{reaction=2,reactionTime=f/(float)fps});
                Assert.AreEqual("Rohr_13_Goal_Celebration_02",view.MecanimGestureClip);
                var animator=root.GetComponentInChildren<Animator>();var hand=animator.GetBoneTransform(HumanBodyBones.RightHand);
                Assert.Greater(hand.position.y,1.55f,"Captured celebration must visibly raise the arm");
                var paused=hand.position;for(int f=0;f<10;f++)view.Render(actor,1,0,default,new PlayerMotionContext{reaction=2,reactionTime=1});
                Assert.Less(Vector3.Distance(paused,hand.position),.002f,"Pause must not advance the captured reaction");
                for(int f=fps+1;f<=fps*4;f++){float t=f/(float)fps;view.Render(actor,1,1f/fps,default,new PlayerMotionContext{reaction=t<3?2:0,reactionTime=t});}
                Assert.IsNull(view.MecanimGestureClip);Assert.Less(hand.position.y,1.4f,"Return to ordinary stance after the goal reaction");
                Assert.AreEqual(Vector3.zero,root.transform.position);
            }finally{Object.DestroyImmediate(root);PlayerView.UseMecanim=before;}
        }
        [TestCase(30)] [TestCase(60)] [TestCase(120)]
        public void TurningCelebrationUsesFullClipAndReturnsWithoutMovingActor(int fps)
        {
            RequirePack();var root=new GameObject("Turning celebration");try{
                var view=root.AddComponent<PlayerView>();view.Build(new PlayerData{id="turning-scorer",heightCm=182},0,9,Color.blue);
                var actor=new Actor{slot=9,action="idle",actionSequence=1};view.Render(actor,1,0);var animator=root.GetComponentInChildren<Animator>();
                var hips=animator.GetBoneTransform(HumanBodyBones.Hips);var previous=hips.rotation;float peakHand=0;
                for(int f=0;f<=fps*4;f++){
                    float t=f/(float)fps;var context=new PlayerMotionContext{reaction=t<3?2:0,reactionTime=t};string before=JsonUtility.ToJson(actor);
                    view.Render(actor,1,1f/fps,default,context);
                    if(t>.25f&&t<2.6f)Assert.AreEqual("Rohr_14_Goal_Celebration_03",view.MecanimGestureClip);
                    if(f>0)Assert.Less(Quaternion.Angle(previous,hips.rotation),600f/fps+1,"No instant body turn at entry or exit");previous=hips.rotation;
                    peakHand=Mathf.Max(peakHand,animator.GetBoneTransform(HumanBodyBones.RightHand).position.y);
                    if(f==fps){var pose=hips.position;for(int pause=0;pause<10;pause++)view.Render(actor,1,0,default,context);Assert.Less(Vector3.Distance(pose,hips.position),.003f);}
                    Assert.AreEqual(before,JsonUtility.ToJson(actor));Assert.AreEqual(Vector3.zero,root.transform.position);
                }
                Assert.Greater(peakHand,1.5f);Assert.IsNull(view.MecanimGestureClip);
            }finally{Object.DestroyImmediate(root);}
        }
        [TestCase(0,30)] [TestCase(0,60)] [TestCase(0,120)]
        [TestCase(1,30)] [TestCase(1,60)] [TestCase(1,120)]
        public void CelebrationJoinedAfterShotFadesFromCurrentPose(int sequence,int fps)
        {
            RequirePack();var root=new GameObject("Delayed celebration");var neutralRoot=new GameObject("Ordinary idle reference");try{
                var view=root.AddComponent<PlayerView>();view.Build(new PlayerData{id="late-scorer",heightCm=182},0,9,Color.blue);
                var actor=new Actor{slot=9,action="idle",actionSequence=sequence};view.Render(actor,1,0);
                for(int warm=0;warm<fps;warm++)view.Render(actor,1,1f/fps);
                var neutral=neutralRoot.AddComponent<PlayerView>();neutral.Build(new PlayerData{id="late-scorer",heightCm=182},0,9,Color.blue);neutral.Render(actor,1,0);
                for(int warm=0;warm<fps;warm++)neutral.Render(actor,1,1f/fps);
                neutral.Render(actor,1,1f/fps);var ordinaryHand=neutralRoot.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.RightHand).position;
                var animator=root.GetComponentInChildren<Animator>();var hand=animator.GetBoneTransform(HumanBodyBones.RightHand);var hips=animator.GetBoneTransform(HumanBodyBones.Hips);
                var start=hand.position;var previous=hips.rotation;
                for(int f=0;f<=fps;f++){
                    var context=new PlayerMotionContext{reaction=2,reactionTime=1+f/(float)fps};view.Render(actor,1,1f/fps,default,context);
                    if(f==0)Assert.Less(Vector3.Distance(ordinaryHand,hand.position),.003f,"Entry must retain the ordinary idle pose at the same clip phase");
                    Assert.Less(Quaternion.Angle(previous,hips.rotation),600f/fps+1);previous=hips.rotation;
                    var paused=hand.position;view.Render(actor,1,0,default,context);Assert.Less(Vector3.Distance(paused,hand.position),.003f);
                }
            }finally{Object.DestroyImmediate(root);Object.DestroyImmediate(neutralRoot);}
        }
        [TestCase("Left",2,160,30)] [TestCase("Left",2,182,60)] [TestCase("Left",2,200,120)]
        [TestCase("Right",2,160,30)] [TestCase("Right",2,182,60)] [TestCase("Right",2,200,120)]
        [TestCase("Right",3,160,30)] [TestCase("Right",3,182,60)] [TestCase("Right",3,200,120)]
        public void ImportedStrikeContactsBallWithPreferredBoot(string foot,int sequence,int height,int fps)
        {
            RequirePack();var root=new GameObject("Captured strike");bool before=PlayerView.UseMecanim;
            try{
                PlayerView.UseMecanim=true;var view=root.AddComponent<PlayerView>();view.Build(new PlayerData{id="captured-strike",heightCm=height,preferredFoot=foot},0,9,Color.blue);
                var ball=new Vector3(0,.11f,.42f);var actor=new Actor{slot=9,action="kick",actionKind="shot",actionSequence=sequence,actionTarget=new Point(0,.42f),actionHeight=.11f};
                float last=0;
                for(int frame=0;frame/(float)fps<.18f;frame++){float elapsed=frame/(float)fps;actor.actionTime=.64f-elapsed;view.Render(actor,1,1f/fps,ball);last=elapsed;}
                actor.actionTime=.46f;view.Render(actor,1,.18f-last,ball);
                Assert.AreEqual(foot=="Left"?"Rohr_09_Power_Kick":sequence==2?"Rohr_10_Penalty_Kick_01":"Rohr_11_Penalty_Kick_02",view.MecanimGestureClip);
                var animator=root.GetComponentInChildren<Animator>();
                var striker=animator.GetBoneTransform(foot=="Left"?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot);
                var support=animator.GetBoneTransform(foot=="Left"?HumanBodyBones.RightFoot:HumanBodyBones.LeftFoot);
                Assert.Less(Vector3.Distance(striker.TransformPoint(new Vector3(0,0,.22f))+striker.forward*.11f,ball),.08f,"Anatomical preferred foot must contact the ball, regardless of source bone labels");
                var rotation=support.rotation;var center=rotation*new Vector3(0,.02f,.075f);
                float extent=Mathf.Abs((rotation*Vector3.right).y)*.057f+Mathf.Abs((rotation*Vector3.up).y)*.085f+Mathf.Abs((rotation*Vector3.forward).y)*.145f;
                float sole=support.position.y+(center.y-extent)*root.transform.localScale.y;
                Assert.That(sole,Is.InRange(-.005f,.04f),"Support sole must rest on turf at impact");Assert.AreEqual(Vector3.zero,root.transform.position);
            }finally{Object.DestroyImmediate(root);PlayerView.UseMecanim=before;}
        }
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
                var anatomicalRight=root.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.RightFoot);
                var contact=anatomicalRight.TransformPoint(new Vector3(-.08f,0,.12f))-anatomicalRight.right*.11f;
                Assert.Less(Vector3.Distance(contact,ball),.055f);
                Assert.AreEqual(Vector3.zero,root.transform.position);
            }finally{Object.DestroyImmediate(root);PlayerView.UseMecanim=before;}
        }
    }
}

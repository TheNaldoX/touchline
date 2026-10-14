using NUnit.Framework;
using UnityEngine;
using Touchline.Core;

namespace Touchline.Tests
{
    public class HeaderLandingTests
    {
        [TestCase(30,"Right")] [TestCase(60,"Left")] [TestCase(120,"Right")]
        [TestCase(30,"Left")] [TestCase(60,"Right")] [TestCase(120,"Left")]
        public void JumpReturnsToStaggeredGroundedFeetWithAbsorption(int fps,string preferredFoot)
        {
            var go=new GameObject("Header landing");try{
                var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="landing",heightCm=180,preferredFoot=preferredFoot},0,9,Color.white);
                var a=new Actor{slot=9,action="idle"};view.Render(a,1,0);var animator=go.GetComponentInChildren<Animator>();var hips=animator.GetBoneTransform(HumanBodyBones.Hips);float neutralHip=hips.position.y,minHip=10;
                a.action="header";a.actionSequence=1;a.actionContactTime=.12f;a.actionTarget=new Point(.1f,.3f);a.actionHeight=2.05f;
                float first=-1,second=-1,maxStep=0;var lastLeft=Vector3.zero;var lastRight=Vector3.zero;bool initialized=false;
                float Sole(HumanBodyBones bone){var foot=animator.GetBoneTransform(bone);float low=float.PositiveInfinity;foreach(var vertex in FootballBootMesh.Shared.vertices)low=Mathf.Min(low,foot.TransformPoint(vertex).y);return low;}
                for(int i=0;i<=Mathf.CeilToInt(.64f*fps);i++){
                    float t=i/(float)fps;a.actionTime=Mathf.Max(0,.64f-t);view.Render(a,1,1f/fps,new Vector3(.1f,2.05f,.3f));
                    var left=view.FootPosition(true);var right=view.FootPosition(false);if(initialized)maxStep=Mathf.Max(maxStep,Vector3.Distance(left,lastLeft),Vector3.Distance(right,lastRight));lastLeft=left;lastRight=right;initialized=true;
                    float leftSole=Sole(HumanBodyBones.LeftFoot),rightSole=Sole(HumanBodyBones.RightFoot);
                    if(t>.2f){if(first<0&&leftSole<.04f)first=t;if(second<0&&rightSole<.04f)second=t;}
                    Assert.GreaterOrEqual(Mathf.Min(leftSole,rightSole),-.025f,"Visible boots remain above the pitch");
                    Assert.Greater(left.y,.025f);Assert.Greater(right.y,.025f);
                    if(t>.2f)minHip=Mathf.Min(minHip,hips.position.y);
                }
                TestContext.WriteLine("Landing first="+first+" second="+second+" absorption="+(neutralHip-minHip)+" maxstep="+maxStep+" stagger="+Mathf.Abs(lastLeft.z-lastRight.z));
                Assert.Greater(first,.2f);Assert.Greater(second,.2f);
                // This capture lands both soles together with the feet offset
                // fore/aft; ankle heights differ because one heel is raised.
                Assert.LessOrEqual(first,MatchSimulation.AerialContestDuration);Assert.LessOrEqual(second,MatchSimulation.AerialContestDuration);
                Assert.Less(Mathf.Min(Sole(HumanBodyBones.LeftFoot),Sole(HumanBodyBones.RightFoot)),.04f,"A support remains while the trailing foot starts recovery");
                Assert.Less(minHip,neutralHip,"The anatomical hips absorb the landing rather than remaining in the upright pose");
                Assert.Less(maxStep,.3f);Assert.Greater(Mathf.Abs(lastLeft.z-lastRight.z),.14f);Assert.AreEqual(Vector3.zero,go.transform.position);
                a.action="idle";
                for(int i=0;i<fps;i++){
                    view.Render(a,1,1f/fps);var left=view.FootPosition(true);var right=view.FootPosition(false);
                    Assert.Less(Vector3.Distance(left,lastLeft),.3f);Assert.Less(Vector3.Distance(right,lastRight),.3f);lastLeft=left;lastRight=right;
                }
                Assert.Less(Sole(HumanBodyBones.LeftFoot),.04f);Assert.Less(Sole(HumanBodyBones.RightFoot),.04f,"Both supports settle after the captured recovery finishes");
            }finally{Object.DestroyImmediate(go);}
        }
    }
}

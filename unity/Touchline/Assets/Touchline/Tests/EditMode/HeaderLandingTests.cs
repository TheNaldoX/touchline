using NUnit.Framework;
using UnityEngine;
using Touchline.Core;

namespace Touchline.Tests
{
    public class HeaderLandingTests
    {
        [TestCase(30,"Right")] [TestCase(60,"Left")] [TestCase(120,"Right")]
        public void JumpReturnsToStaggeredGroundedFeetWithAbsorption(int fps,string preferredFoot)
        {
            var go=new GameObject("Header landing");try{
                var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="landing",heightCm=180,preferredFoot=preferredFoot},0,9,Color.white);
                var a=new Actor{action="header",actionSequence=1,actionContactTime=.12f,actionTarget=new Point(.1f,.3f),actionHeight=2.05f};
                float first=-1,second=-1,minBody=10,maxStep=0;var lastLeft=Vector3.zero;var lastRight=Vector3.zero;bool initialized=false;
                for(int i=0;i<=Mathf.CeilToInt(.64f*fps);i++){
                    float t=i/(float)fps;a.actionTime=Mathf.Max(0,.64f-t);view.Render(a,1,1f/fps,new Vector3(.1f,2.05f,.3f));
                    var left=view.FootPosition(true);var right=view.FootPosition(false);if(initialized)maxStep=Mathf.Max(maxStep,Vector3.Distance(left,lastLeft),Vector3.Distance(right,lastRight));lastLeft=left;lastRight=right;initialized=true;
                    if(t>.2f){float lead=(preferredFoot=="Left"?left:right).y,trail=(preferredFoot=="Left"?right:left).y;if(first<0&&lead<.095f)first=t;if(second<0&&trail<.095f)second=t;minBody=Mathf.Min(minBody,go.transform.Find("Rig").localPosition.y);}
                    Assert.Greater(left.y,.025f);Assert.Greater(right.y,.025f);
                }
                Assert.Greater(second,first);Assert.Less(minBody,-.07f);Assert.Less(maxStep,.3f);Assert.Greater(Mathf.Abs(lastLeft.z-lastRight.z),.14f);Assert.AreEqual(Vector3.zero,go.transform.position);
            }finally{Object.DestroyImmediate(go);}
        }
    }
}

using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Touchline.Core;
using UnityEngine;
namespace Touchline.Tests
{
    public class SlidingEntryFloorPlaneTests
    {
        [TestCase(160,1,.00017f)] [TestCase(182,-1,.00017f)] [TestCase(205,1,.00017f)]
        [TestCase(160,-1,.007f)] [TestCase(182,1,.007f)] [TestCase(205,-1,.007f)]
        public void AMinimalSoleCorrectionPreservesThePreparedKneePlane(int height,int side,float clearanceDeficit)
        {
            var go=new GameObject("Slide entry sole plane");
            try{
                var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="slide-plane",heightCm=height},0,2,Color.white);
                view.Render(new Actor{action="idle"},1,1f/120);
                const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
                var solve=typeof(PlayerView).GetMethod("SolveLeg",flags);
                var guard=typeof(PlayerView).GetMethod("KeepBootsAbovePitch",flags);
                string suffix=side>0?"L":"R";var bones=go.GetComponentsInChildren<Transform>();
                var hip=bones.Single(b=>b.name=="upperleg01."+suffix);var knee=bones.Single(b=>b.name=="lowerleg01."+suffix);var foot=bones.Single(b=>b.name=="foot."+suffix);
                float scale=go.transform.localScale.y;
                // A fully reachable knee turned to the side, as in the blend.
                // Its sole is only0.17mm below the guard's1cm clearance target
                // in the native regression; physical grass remains unpenetrated.
                var target=new Vector3(side*.19f*scale,.065f*scale+.01f-clearanceDeficit,-.10f*scale);
                solve.Invoke(view,new object[]{suffix,target,Vector3.right*side});foot.rotation=go.transform.rotation;
                var oldKnee=knee.position;var oldFoot=foot.position;var oldRoot=go.transform.position;
                float upper=Vector3.Distance(hip.position,knee.position),lower=Vector3.Distance(knee.position,foot.position);
                Assert.Less(Vector3.Distance(target,foot.position),.001f,"Fixture target must be anatomically reachable");
                // Supports execution against the old one-argument guard to prove
                // baseline red without editing the production method signature.
                guard.Invoke(view,guard.GetParameters().Length==2?new object[]{go.transform.forward,true}:new object[]{go.transform.forward});
                TestContext.WriteLine($"height={height}; side={side}; deficit={clearanceDeficit:F7}; kneeStep={Vector3.Distance(oldKnee,knee.position):F7}; footStep={Vector3.Distance(oldFoot,foot.position):F7}; sole={foot.position.y-.065f*scale:F7}");
                Assert.Less(Vector3.Distance(oldKnee,knee.position),.02f,"A millimetric foot lift must not rotate the knee into another plane");
                Assert.Less(Vector2.Distance(new Vector2(oldFoot.x,oldFoot.z),new Vector2(foot.position.x,foot.position.z)),.001f,"The floor guard must not change horizontal contact");
                Assert.GreaterOrEqual(foot.position.y-.065f*scale,0,"The entire horizontal sole must remain above physical grass");
                Assert.That(Vector3.Distance(hip.position,knee.position),Is.EqualTo(upper).Within(.0001f));
                Assert.That(Vector3.Distance(knee.position,foot.position),Is.EqualTo(lower).Within(.0001f));
                Assert.AreEqual(oldRoot,go.transform.position);
            }finally{Object.DestroyImmediate(go);}
        }
    }
}

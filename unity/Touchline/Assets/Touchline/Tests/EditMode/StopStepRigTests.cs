using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Touchline.Core;
using UnityEngine;

namespace Touchline.Tests
{
    public class StopStepRigTests
    {
        static StopStepMotion Planner(PlayerView view)=>(StopStepMotion)typeof(PlayerView).GetField("stoppingSteps",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(view);
        static float FlatDistance(Vector3 a,Vector3 b)=>new Vector2(a.x-b.x,a.z-b.z).magnitude;
        static float Sole(Transform foot,float scale)
        {
            var q=foot.rotation;var center=q*new Vector3(0,.02f,.075f);
            float extent=Mathf.Abs((q*Vector3.right).y)*.057f+Mathf.Abs((q*Vector3.up).y)*.085f+Mathf.Abs((q*Vector3.forward).y)*.145f;
            return foot.position.y+(center.y-extent)*scale;
        }
        static Actor Pose(float time)
        {
            float u=time-.5f,speed=time<.5f?3:time<1?3-6*u:0,z=time<.5f?3*time:time<1?1.5f+3*u-3*u*u:2.25f;
            return new Actor{id="stop-rig",slot=9,action="run",angle=0,position=new Point(0,z),previous=new Point(0,z),velocity=new Point(0,speed)};
        }
        [TestCase(30)][TestCase(60)][TestCase(120)]
        public void BrakingFinishesInAlternatingGroundedSupportsWithoutMovingTheActor(int fps)
        {
            var go=new GameObject("Stop step rig");try{
                var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="stop-rig",heightCm=182},0,9,Color.blue);
                var feet=new[]{go.GetComponentsInChildren<Transform>().First(t=>t.name=="foot.L"),go.GetComponentsInChildren<Transform>().First(t=>t.name=="foot.R")};
                Vector3[] before=null;float[] previousSole=null;bool previousActive=false;float slip=0,minimum=1;int activeFrames=0;
                for(int frame=0;frame<=fps*2;frame++){
                    var actor=Pose((float)frame/fps);string state=JsonUtility.ToJson(actor);
                    view.Render(actor,1,frame==0?0:1f/fps,new Vector3(0,.11f,4));
                    Assert.AreEqual(state,JsonUtility.ToJson(actor));Assert.AreEqual(new Vector3(actor.position.x,0,actor.position.z),view.transform.position);
                    bool active=Planner(view).Active;var current=feet.Select(f=>f.position).ToArray();var soles=feet.Select(f=>Sole(f,view.transform.localScale.y)).ToArray();
                    if(active){activeFrames++;for(int i=0;i<2;i++){
                        minimum=Mathf.Min(minimum,soles[i]);
                        if(previousActive&&soles[i]<=.025f&&previousSole[i]<=.025f)slip+=FlatDistance(current[i],before[i]);
                    }}
                    previousActive=active;before=current;previousSole=soles;
                }
                TestContext.WriteLine("fps="+fps+" activeFrames="+activeFrames+" groundedSlip="+slip+" minimumSole="+minimum);
                Assert.Greater(activeFrames,fps/2);Assert.IsTrue(Planner(view).Complete);
                Assert.Less(slip,.04f);Assert.GreaterOrEqual(minimum,-.001f);
            }finally{Object.DestroyImmediate(go);}
        }
        [TestCase("kick")][TestCase("control")][TestCase("tackle")]
        public void ContactActionsImmediatelyReleaseSettlingOwnership(string action)
        {
            var go=new GameObject("Stop step contact override");try{
                var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="stop-rig",heightCm=182},0,9,Color.blue);
                for(int frame=0;frame<=70;frame++)view.Render(Pose(frame/60f),1,1f/60,new Vector3(0,.11f,4));
                Assert.IsTrue(Planner(view).Active);
                var actor=Pose(1.2f);actor.action=action;actor.actionTime=.3f;actor.actionTarget=actor.position+new Point(0,.3f);actor.actionHeight=.11f;
                view.Render(actor,1,1f/60,new Vector3(0,.11f,2.55f));Assert.IsFalse(Planner(view).Active);
            }finally{Object.DestroyImmediate(go);}
        }
        [Test] public void ResetAndIdentityReplacementDiscardOldSupportAnchors()
        {
            var go=new GameObject("Stop step identity reset");try{
                var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="stop-rig",heightCm=182},0,9,Color.blue);
                for(int frame=0;frame<=70;frame++)view.Render(Pose(frame/60f),1,1f/60,new Vector3(0,.11f,4));
                Assert.IsTrue(Planner(view).Active);view.ResetPresentation();view.Render(Pose(1.2f),1,1f/60);Assert.IsFalse(Planner(view).Active);
                view.ChangeIdentity(new PlayerData{id="incoming-stop",heightCm=177});var actor=Pose(1.2f);actor.position=actor.previous=new Point(10,10);actor.id="incoming-stop";
                view.Render(actor,1,1f/60);Assert.IsFalse(Planner(view).Active);Assert.AreEqual(new Vector3(10,0,10),view.transform.position);
            }finally{Object.DestroyImmediate(go);}
        }
    }
}

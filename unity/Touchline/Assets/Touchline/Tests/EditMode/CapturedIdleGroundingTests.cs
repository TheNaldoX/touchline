using System.Linq;
using NUnit.Framework;
using Touchline.Core;
using UnityEngine;

namespace Touchline.Tests
{
    public class CapturedIdleGroundingTests
    {
        [TestCase(160,30)][TestCase(182,60)][TestCase(205,120)]
        public void StationaryCapturedPoseHasAVisibleGroundedSupportAndPreservesPause(int stature,int fps)
        {
            var go=new GameObject("Captured standing support");try{
                var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="idle-ground",heightCm=stature},0,9,Color.blue);
                var actor=new Actor{id="idle-ground",slot=9,action="idle"};
                var boots=go.GetComponentsInChildren<MeshFilter>().Where(m=>m.name=="Football boot").ToArray();
                Assert.AreEqual(2,boots.Length);var vertices=boots[0].sharedMesh.vertices;
                float highestSupport=0;
                for(int frame=0;frame<=fps*2;frame++){
                    string state=JsonUtility.ToJson(actor);view.Render(actor,1,frame==0?0:1f/fps);
                    Assert.AreEqual(state,JsonUtility.ToJson(actor));Assert.AreEqual(Vector3.zero,go.transform.position);
                    if(frame>fps/2){
                        var steps=(float[])typeof(PlayerView).GetField("footStep",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(view);
                        float[] soles=boots.Select(b=>vertices.Min(v=>b.transform.TransformPoint(v).y)).ToArray();
                        Assert.That(soles.Min(),Is.InRange(-.005f,.035f),"A replacement step retains its opposite support.");
                        for(int i=0;i<boots.Length;i++){
                            int side=boots[i].transform.parent.name=="foot.L"?0:1;
                            if(steps[side]>0)continue; // The explicit replacement step is allowed to lift its moving foot.
                            highestSupport=Mathf.Max(highestSupport,soles[i]);
                            Assert.That(soles[i],Is.InRange(-.005f,.035f),"A planted foot must stay on the visible ground.");
                        }
                    }
                }
                var left=view.FootPosition(true);var right=view.FootPosition(false);
                for(int i=0;i<20;i++){view.Render(actor,1,0);Assert.Less(Vector3.Distance(left,view.FootPosition(true)),.002f);Assert.Less(Vector3.Distance(right,view.FootPosition(false)),.002f);}
                TestContext.WriteLine("stature="+stature+" fps="+fps+" highestSupport="+highestSupport);
            }finally{Object.DestroyImmediate(go);}
        }
    }
}

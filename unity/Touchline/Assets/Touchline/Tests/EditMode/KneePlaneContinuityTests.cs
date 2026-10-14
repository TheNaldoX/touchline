using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Touchline.Core;
namespace Touchline.Tests
{
    public class KneePlaneContinuityTests
    {
        [TestCase(30,"L")][TestCase(60,"L")][TestCase(120,"L")]
        [TestCase(30,"R")][TestCase(60,"R")][TestCase(120,"R")]
        public void PoleFlipIsLimitedInTimeWithoutMovingTheAnkle(int fps,string side)
        {
            var go=new GameObject("Knee plane continuity");
            try{
                var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="knee",heightCm=182},0,9,Color.white);
                var actor=new Actor{id="knee",slot=9,action="idle"};view.Render(actor,1,1f/fps);
                var all=go.GetComponentsInChildren<Transform>();
                var hip=all.Single(t=>t.name=="upperleg01."+side);var knee=all.Single(t=>t.name=="lowerleg01."+side);var foot=all.Single(t=>t.name=="foot."+side);
                var feet=new[]{view.FootPosition(true),view.FootPosition(false)};
                var previous=(Vector3[])typeof(PlayerView).GetField("previousKnees",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(view);
                previous[0]=all.Single(t=>t.name=="lowerleg01.L").position;previous[1]=all.Single(t=>t.name=="lowerleg01.R").position;
                var axis=(foot.position-hip.position).normalized;var before=Vector3.ProjectOnPlane(knee.position-hip.position,axis);
                hip.rotation=Quaternion.AngleAxis(160,axis)*hip.rotation;
                var method=typeof(PlayerView).GetMethod("MecanimKneeContinuity",BindingFlags.Instance|BindingFlags.NonPublic);
                method.Invoke(view,new object[]{actor,1f/fps,false});
                var after=Vector3.ProjectOnPlane(knee.position-hip.position,axis);
                Assert.LessOrEqual(Vector3.Angle(before,after),540f/fps+.1f);
                Assert.Less(Vector3.Distance(feet[0],view.FootPosition(true)),.003f);
                Assert.Less(Vector3.Distance(feet[1],view.FootPosition(false)),.003f);
                Assert.AreEqual(Vector3.zero,go.transform.position);
            }finally{Object.DestroyImmediate(go);}
        }
    }
}

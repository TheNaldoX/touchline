using NUnit.Framework;
using Touchline.Core;
using UnityEngine;

namespace Touchline.Tests
{
    public class CarrierMarkingFacingTests
    {
        [TestCase(30)][TestCase(60)][TestCase(120)]
        public void CarrierMarkingMatchesExistingMarkerPostureThroughBrakingAndRestart(int fps)
        {
            var a=new GameObject("Existing marker");var b=new GameObject("Carrier marker");
            try {
                var one=a.AddComponent<PlayerView>();var two=b.AddComponent<PlayerView>();
                var data=new PlayerData{id="same-marker",heightCm=182};one.Build(data,0,2,Color.white);two.Build(data,0,2,Color.white);
                var actor=new Actor{slot=2,action="run",angle=0};var other=new Actor{slot=2,action="run",angle=0};
                var context=new PlayerMotionContext{defending=true};
                var bonesA=a.GetComponentInChildren<SkinnedMeshRenderer>().bones;var bonesB=b.GetComponentInChildren<SkinnedMeshRenderer>().bones;
                for(int frame=0;frame<fps*2;frame++){
                    float t=(float)frame/fps,speed=t<.5f?2:t<1?Mathf.Max(0,2-4*(t-.5f)):t<1.5f?0:2;
                    actor.velocity=other.velocity=new Point(0,-speed);actor.previous=other.previous=actor.position;actor.position+=actor.velocity/fps;other.position=actor.position;
                    actor.intent="mark";other.intent="mark-carrier";actor.action=other.action=speed==0?"idle":"run";
                    one.Render(actor,1,1f/fps,new Vector3(0,.11f,7),context);two.Render(other,1,1f/fps,new Vector3(0,.11f,7),context);
                    Assert.Less(Quaternion.Angle(a.transform.rotation,b.transform.rotation),.001f);
                    for(int i=0;i<bonesA.Length;i++){
                        Assert.Less(Vector3.Distance(bonesA[i].position,bonesB[i].position),.0001f,"Readiness and stopping support must agree: "+bonesA[i].name);
                        Assert.Less(Quaternion.Angle(bonesA[i].rotation,bonesB[i].rotation),.06f);
                    }
                }
                var held=b.transform.rotation;other.angle=1.5f;
                two.Render(other,1,0,new Vector3(4,.11f,7),context);
                Assert.Less(Quaternion.Angle(held,b.transform.rotation),.001f,"A paused heading cannot advance");
            }finally{Object.DestroyImmediate(a);Object.DestroyImmediate(b);}
        }
        [TestCase(30,0,-2)][TestCase(60,0,-2)][TestCase(120,0,-2)]
        [TestCase(30,2,0)][TestCase(60,2,0)][TestCase(120,2,0)]
        [TestCase(30,0,0)][TestCase(60,0,0)][TestCase(120,0,0)]
        public void CloseCarrierMarkingWatchesPlayDuringRetreatShuffleAndHold(int fps,float vx,float vz)
        {
            var go=new GameObject("Carrier marking orientation");
            try {
                var view=go.AddComponent<PlayerView>();
                view.Build(new PlayerData{id="carrier-watch",heightCm=182},0,2,Color.white);
                var actor=new Actor{slot=2,action=vx==0&&vz==0?"idle":"run",intent="mark-carrier",velocity=new Point(vx,vz),angle=Mathf.PI};
                var ball=new Vector3(0,.11f,7);
                for(int frame=0;frame<fps;frame++){
                    actor.previous=actor.position;actor.position+=actor.velocity/fps;
                    string simulationBefore=JsonUtility.ToJson(actor);
                    view.Render(actor,1,1f/fps,ball,new PlayerMotionContext{defending=true});
                    Assert.AreEqual(simulationBefore,JsonUtility.ToJson(actor),"Presentation cannot change tactical movement");
                    Assert.Greater(view.FootPosition(true).y,-.025f);
                    Assert.Greater(view.FootPosition(false).y,-.025f);
                }
                var ballDirection=Vector3.ProjectOnPlane(ball-go.transform.position,Vector3.up).normalized;
                Assert.Greater(Vector3.Dot(go.transform.forward,ballDirection),.98f,"The carrier marker must watch nearby play, like the other defending intents");
                if(vz<0)Assert.Less(go.transform.InverseTransformDirection(new Vector3(vx,0,vz)).z,-1.9f,"Retreat remains backward locomotion");
                if(vx>0)Assert.Greater(Mathf.Abs(go.transform.InverseTransformDirection(new Vector3(vx,0,vz)).x),1.8f,"Lateral coverage remains a shuffle");
            } finally {Object.DestroyImmediate(go);}
        }
        [TestCase("sprint")][TestCase("carrying")][TestCase("attacking")]
        [TestCase("keeper")][TestCase("distant")][TestCase("kick")]
        public void CarrierIntentCannotOverrideTravelOrContactOwnership(string scenario)
        {
            var go=new GameObject("Carrier facing ownership");
            try {
                int slot=scenario=="keeper"?0:2;
                var view=go.AddComponent<PlayerView>();
                view.Build(new PlayerData{id="carrier-ownership",heightCm=182},0,slot,Color.white);
                var actor=new Actor{slot=slot,action=scenario=="kick"?"kick":"run",intent="mark-carrier",velocity=new Point(0,scenario=="sprint"?-7:-2),angle=Mathf.PI,actionTime=.5f,actionKind="pass"};
                var context=new PlayerMotionContext{defending=scenario!="attacking",carrying=scenario=="carrying"};
                var ball=new Vector3(0,.11f,scenario=="distant"?25:7);
                view.Render(actor,1,1f/60,ball,context);
                Assert.Greater(Vector3.Dot(go.transform.forward,Vector3.back),.99f);
            } finally {Object.DestroyImmediate(go);}
        }
    }
}

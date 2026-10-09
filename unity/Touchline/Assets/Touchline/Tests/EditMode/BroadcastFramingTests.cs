using NUnit.Framework;
using UnityEngine;
using System.Linq;
using Touchline.Core;

namespace Touchline.Tests
{
    public class BroadcastFramingTests
    {
        [TestCase(.43f,-1,.1f)] [TestCase(.43f,1,.1f)] [TestCase(1.32f,-1,.1f)] [TestCase(1.32f,1,.1f)]
        [TestCase(.43f,-1,.5f)] [TestCase(.43f,1,.5f)] [TestCase(1.32f,-1,.5f)] [TestCase(1.32f,1,.5f)]
        public void EnteringTheLastThirdDoesNotCutToAWiderCamera(float aspect,int end,float step)
        {
            var go=new GameObject("Continuous goal framing");try{
                var c=go.AddComponent<Camera>();c.aspect=aspect;c.fieldOfView=46;Vector3 previous=Vector3.zero;
                int leg=Mathf.RoundToInt(10/step);
                for(int i=0;i<=2*leg;i++){
                    float x=23+(i<=leg?i:2*leg-i)*step;var ball=new Vector3(end*x,.11f,0);var focus=new Vector3(end*(x-4),.6f,0);
                    BroadcastFraming.Apply(c,focus,ball,false,true,end,.85f);Check(c,ball);
                    if(i>0)Assert.Less(Vector3.Distance(previous,c.transform.position),8*step,"Pas de coupe au seuil du dernier tiers : x="+x);
                    previous=c.transform.position;
                    if(x>=28&&Mathf.Abs(end*52.5f-focus.x)<=29){Check(c,new Vector3(end*52.5f,0,-3.66f));Check(c,new Vector3(end*52.5f,2.44f,3.66f));}
                }
            }finally{Object.DestroyImmediate(go);}
        }
        [TestCase(18f,10f,true)] [TestCase(17f,12f,false)]
        public void PortraitDeliveryDoesNotJumpAtTheCentralFramingThreshold(float ballZ,float focusZ,bool moveBall)
        {
            var go=new GameObject("Continuous portrait delivery");try{
                var c=go.AddComponent<Camera>();c.aspect=.43f;c.fieldOfView=46;Vector3 previous=Vector3.zero;
                for(int i=0;i<=80;i++){
                    float offset=2-(i<=40?i:80-i)*.1f;var ball=new Vector3(35,.11f,ballZ+(moveBall?offset:0));var focus=new Vector3(28,.6f,focusZ+(moveBall?0:offset));
                    BroadcastFraming.Apply(c,focus,ball,false,true,1,.85f);Check(c,ball);
                    if(i>0)Assert.Less(Vector3.Distance(previous,c.transform.position),1f,"La livraison ne doit pas déclencher un recul brutal");
                    previous=c.transform.position;
                }
            }finally{Object.DestroyImmediate(go);}
        }
        [TestCase(.43f,-1,-1)] [TestCase(.43f,1,1)] [TestCase(1.32f,1,-1)] [TestCase(3.62f,-1,1)] [TestCase(3.62f,1,1)]
        public void CornersKeepBallAndGoalInsideTheViewport(float aspect,int end,int side)
        {
            var go=new GameObject("Camera framing test");try{var camera=go.AddComponent<Camera>();camera.fieldOfView=46;camera.aspect=aspect;var ball=new Vector3(end*52.5f,.11f,side*34);BroadcastFraming.Apply(camera,new Vector3(end*43,.6f,side*14),ball,false,true,end,.7f);Check(camera,ball);Check(camera,ball+Vector3.up*2);if(aspect>=.8f){Check(camera,new Vector3(end*52.5f,2.44f,-3.66f));Check(camera,new Vector3(end*52.5f,2.44f,3.66f));}}finally{Object.DestroyImmediate(go);}
        }
        [TestCase(.43f)] [TestCase(1.32f)] [TestCase(3.62f)] public void TacticalViewContainsTheWholePitch(float aspect){var go=new GameObject("Full pitch camera");try{var c=go.AddComponent<Camera>();c.fieldOfView=46;c.aspect=aspect;BroadcastFraming.Apply(c,new Vector3(0,.6f,0),Vector3.zero,true,false,1,.7f);foreach(int x in new[]{-1,1})foreach(int z in new[]{-1,1})Check(c,new Vector3(x*52.5f,0,z*34));}finally{Object.DestroyImmediate(go);}}
        [Test] public void TacticalPortraitUsesTheHeightOfThePhone(){var go=new GameObject("Vertical tactical camera");try{var c=go.AddComponent<Camera>();c.aspect=.43f;c.fieldOfView=46;c.farClipPlane=270;BroadcastFraming.Apply(c,Vector3.zero,Vector3.zero,true,false,1,1);var top=c.WorldToViewportPoint(new Vector3(52.5f,0,0));var bottom=c.WorldToViewportPoint(new Vector3(-52.5f,0,0));Assert.Greater(top.y-bottom.y,.5f,"The pitch must fill the tall viewport rather than form a narrow horizontal strip");Check(c,new Vector3(52.5f,0,34));Check(c,new Vector3(-52.5f,0,-34));}finally{Object.DestroyImmediate(go);}}
        [TestCase(.43f)][TestCase(1.3f)][TestCase(2.28f)]
        public void TelevisionViewRetainsNearbyPassingLanes(float aspect)
        {
            var go=new GameObject("TV context test");try{var c=go.AddComponent<Camera>();c.aspect=aspect;c.fieldOfView=46;
                BroadcastFraming.Apply(c,new Vector3(8,.6f,5),new Vector3(8,.11f,5),false,false,1,1);
                for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)Check(c,new Vector3(8+x*18,.6f,5+z*12));
                if(aspect<.8f)Assert.Greater(Mathf.Abs((c.WorldToViewportPoint(new Vector3(26,0,5))-c.WorldToViewportPoint(new Vector3(-10,0,5))).y),.3f);
            }finally{Object.DestroyImmediate(go);}
        }
        [TestCase(.43f,1)][TestCase(.43f,10)][TestCase(1.32f,10)][TestCase(3.62f,10)]
        public void ArenaTracksContinuousFastTransitionsWithoutLeavingTheStadium(float aspect,int speed)
        {
            int previousMode=PlayerPrefs.GetInt("match-camera-mode",0),previousSpeed=PlayerPrefs.GetInt("match-live-speed",1);float previousZoom=PlayerPrefs.GetFloat("match-camera-zoom",1);
            var ambient=RenderSettings.ambientLight;bool fog=RenderSettings.fog;var fogColor=RenderSettings.fogColor;var fogMode=RenderSettings.fogMode;float fogStart=RenderSettings.fogStartDistance,fogEnd=RenderSettings.fogEndDistance;GameObject go=null;
            try{
                PlayerPrefs.SetInt("match-camera-mode",0);PlayerPrefs.SetInt("match-live-speed",1);PlayerPrefs.SetFloat("match-camera-zoom",1);
                var db=JsonUtility.FromJson<Database>(Resources.Load<TextAsset>("Data/database").text);var clubs=db.clubs.Where(c=>c.playable).Take(2).ToArray();var career=new Career{club=clubs[0].id};career.lineup=Career.Select(db,career.club,career.tactic);
                var sim=MatchSimulation.Create(db,career,clubs[1].id);go=new GameObject("Continuous broadcast tracking");var arena=go.AddComponent<MatchArena>();arena.Initialize(db,sim);arena.Paused=true;arena.Speed=speed;arena.Broadcast.Enabled=false;arena.MatchCamera.aspect=aspect;
                Vector3 previous=Vector3.zero;float maximumDistance=0,maximumStep=0;
                for(int frame=0;frame<160;frame++){
                    float time=frame/60f*speed;var point=new Point(Mathf.Sin(time*.55f)*49,Mathf.Sin(time*.85f)*32);
                    sim.State.ball.owner=null;sim.State.ball.position=sim.State.ball.previous=point;sim.State.ball.height=sim.State.ball.previousHeight=.11f;
                    arena.RenderFrame(1f/60);Check(arena.MatchCamera,new Vector3(point.x,.11f,point.z));
                    float distance=Vector3.Distance(arena.MatchCamera.transform.position,new Vector3(point.x,.11f,point.z));maximumDistance=Mathf.Max(maximumDistance,distance);
                    if(frame>0)maximumStep=Mathf.Max(maximumStep,Vector3.Distance(previous,arena.MatchCamera.transform.position));previous=arena.MatchCamera.transform.position;
                }
                Assert.Less(maximumDistance,aspect<.8f?145:110,"A fast pass must not zoom the camera away from the stadium");Assert.Less(maximumStep,35,"No sudden camera leap across the stadium during a continuous trajectory");TestContext.WriteLine($"Aspect {aspect}; x{speed}; max distance {maximumDistance:0.0} m; largest camera step {maximumStep:0.0} m");
            }finally{if(go!=null)Object.DestroyImmediate(go);PlayerPrefs.SetInt("match-camera-mode",previousMode);PlayerPrefs.SetInt("match-live-speed",previousSpeed);PlayerPrefs.SetFloat("match-camera-zoom",previousZoom);RenderSettings.ambientLight=ambient;RenderSettings.fog=fog;RenderSettings.fogColor=fogColor;RenderSettings.fogMode=fogMode;RenderSettings.fogStartDistance=fogStart;RenderSettings.fogEndDistance=fogEnd;}
        }
        [Test] public void PortraitTelevisionZoomChangesScaleWhileRetainingTheBall()
        {
            var go=new GameObject("Portrait zoom test");try{var c=go.AddComponent<Camera>();c.aspect=.43f;c.fieldOfView=46;BroadcastFraming.Apply(c,Vector3.zero,Vector3.zero,false,false,1,.8f);float close=c.transform.position.magnitude;Check(c,Vector3.zero);BroadcastFraming.Apply(c,Vector3.zero,Vector3.zero,false,false,1,1.3f);Assert.Greater(c.transform.position.magnitude,close*1.2f);Check(c,Vector3.zero);}finally{Object.DestroyImmediate(go);}
        }
        [TestCase("Entraînement","entrainement")][TestCase("Prêts et agents","PRETS")]
        public void MenuSearchAcceptsUnaccentedQueries(string title,string query)=>Assert.IsTrue(TouchlineApp.DirectoryMatches(title,query));
        static void Check(Camera c,Vector3 world){var p=c.WorldToViewportPoint(world);Assert.Greater(p.z,0);Assert.Less(p.z,c.farClipPlane-1,"A fitted point must not be culled by the far plane");Assert.That(p.x,Is.InRange(.10f,.9f));Assert.That(p.y,Is.InRange(.10f,.9f));}
    }
}

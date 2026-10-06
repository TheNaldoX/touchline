using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Touchline.Core;

namespace Touchline.Tests
{
    public class BroadcastResumeFramingTests
    {
        [TestCase(.43f,1,18f)] [TestCase(.43f,-1,-18f)]
        [TestCase(1.32f,1,18f)] [TestCase(1.32f,-1,-18f)]
        [TestCase(3.62f,1,18f)] [TestCase(3.62f,-1,-18f)]
        public void AStaticResumedMatchIsAlreadyFramedOnItsFirstRenderedFrame(float aspect,int end,float flank)
        {
            string[] keys={"match-camera-mode","match-live-speed","match-camera-zoom"};
            var present=keys.Select(PlayerPrefs.HasKey).ToArray();int mode=PlayerPrefs.GetInt(keys[0],0),speed=PlayerPrefs.GetInt(keys[1],1);float zoom=PlayerPrefs.GetFloat(keys[2],1);
            var ambient=RenderSettings.ambientLight;bool fog=RenderSettings.fog;var fogColor=RenderSettings.fogColor;var fogMode=RenderSettings.fogMode;float fogStart=RenderSettings.fogStartDistance,fogEnd=RenderSettings.fogEndDistance;
            GameObject go=null;
            try{
                PlayerPrefs.SetInt(keys[0],0);PlayerPrefs.SetInt(keys[1],1);PlayerPrefs.SetFloat(keys[2],1);
                var db=JsonUtility.FromJson<Database>(Resources.Load<TextAsset>("Data/database").text);var clubs=db.clubs.Where(c=>c.playable).Take(2).ToArray();
                var career=new Career{club=clubs[0].id};career.lineup=Career.Select(db,career.club,career.tactic);var sim=MatchSimulation.Create(db,career,clubs[1].id);
                var point=new Point(end*47,flank);sim.State.clock=1800;sim.State.restart=0;sim.State.phase="play";sim.State.remainder=0;
                sim.State.ball=new BallState{position=point,previous=point,height=.11f,previousHeight=.11f};
                go=new GameObject("Resume camera framing diagnostic");var arena=go.AddComponent<MatchArena>();arena.Initialize(db,sim);arena.enabled=false;arena.Paused=true;arena.Speed=1;arena.Broadcast.Enabled=false;arena.MatchCamera.aspect=aspect;
                arena.RenderFrame(1f/60);var camera=arena.MatchCamera;var first=camera.transform.position;var ball=new Vector3(point.x,.11f,point.z);
                var firstViewport=camera.WorldToViewportPoint(ball);float firstSize=((Vector2)(camera.WorldToViewportPoint(ball+Vector3.up*1.8f)-firstViewport)).magnitude;
                for(int i=0;i<240;i++)arena.RenderFrame(1f/60);
                var final=camera.transform.position;var finalViewport=camera.WorldToViewportPoint(ball);float finalSize=((Vector2)(camera.WorldToViewportPoint(ball+Vector3.up*1.8f)-finalViewport)).magnitude;
                float drift=Vector3.Distance(first,final);float viewportDrift=Vector2.Distance(new Vector2(firstViewport.x,firstViewport.y),new Vector2(finalViewport.x,finalViewport.y));
                TestContext.WriteLine($"aspect={aspect:F3}; end={end}; firstCamera={first:F4}; settledCamera={final:F4}; cameraDrift={drift:F4}m; viewportDrift={viewportDrift:F5}; firstScale={firstSize:F5}; settledScale={finalSize:F5}");
                Assert.AreEqual(1800,sim.State.clock,"A paused framing diagnostic must not advance the saved match");Assert.AreEqual(point.x,sim.State.ball.position.x);Assert.AreEqual(point.z,sim.State.ball.position.z);
                Assert.That(firstViewport.x,Is.InRange(.1f,.9f));Assert.That(firstViewport.y,Is.InRange(.1f,.9f));Assert.Greater(firstViewport.z,0);
                Assert.Less(drift,.25f,"Loading a motionless paused scene must not pan from midfield for several seconds");
                Assert.Less(viewportDrift,.01f,"The first frame must already show the loaded action at its stable screen position");
            }finally{
                if(go!=null)Object.DestroyImmediate(go);
                if(present[0])PlayerPrefs.SetInt(keys[0],mode);else PlayerPrefs.DeleteKey(keys[0]);
                if(present[1])PlayerPrefs.SetInt(keys[1],speed);else PlayerPrefs.DeleteKey(keys[1]);
                if(present[2])PlayerPrefs.SetFloat(keys[2],zoom);else PlayerPrefs.DeleteKey(keys[2]);
                RenderSettings.ambientLight=ambient;RenderSettings.fog=fog;RenderSettings.fogColor=fogColor;RenderSettings.fogMode=fogMode;RenderSettings.fogStartDistance=fogStart;RenderSettings.fogEndDistance=fogEnd;
            }
        }
    }
}


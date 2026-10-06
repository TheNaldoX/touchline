using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Touchline.Core;

namespace Touchline.Tests
{
    public class SimulatedHeaderContactTests
    {
        [TestCase(360)] [TestCase(2700)] public void NaturalHeadersKeepTheirRecordedBallWithinHeadReachAfterThePlant(int halfSeconds)
        {
            var db=JsonUtility.FromJson<Database>(Resources.Load<TextAsset>("Data/database").text);var clubs=db.clubs.Where(c=>c.playable&&c.league=="eng.1").Take(2).ToArray();int count=0;float worst=0;
            foreach(uint seed in new uint[]{7919,15838,23757,31676}){
                var career=new Career{club=clubs[0].id};career.lineup=Career.Select(db,career.club,career.tactic);var sim=MatchSimulation.Create(db,career,clubs[1].id,seed,halfSeconds);var checkedSequence=new int[22];
                for(int tick=0;tick<halfSeconds*20;tick++){
                    if(sim.State.halfTime)sim.ResumeHalf();sim.Advance(.1);
                    for(int index=0;index<22;index++){
                        var actor=sim.State.actors[index];if(actor.action!="header"||actor.actionSequence==checkedSequence[index])continue;
                        float remaining=.64f-actor.actionContactTime;if(remaining<actor.actionTime-.0001f||remaining>actor.actionTime+MatchSimulation.Step+.0001f)continue;
                        checkedSequence[index]=actor.actionSequence;float alpha=Mathf.Clamp01(1-(remaining-actor.actionTime)/MatchSimulation.Step);
                        var go=new GameObject("Natural header contact");try{
                            var view=go.AddComponent<PlayerView>();view.Build(db.Find(actor.id),actor.side,actor.slot,Color.white);go.transform.rotation=Quaternion.Euler(0,actor.angle*Mathf.Rad2Deg,0);
                            var contact=new Vector3(actor.actionTarget.x,actor.actionHeight,actor.actionTarget.z);view.Render(actor,alpha,1f/60,contact);
                            float gap=Vector3.Distance(view.HeaderContactPosition,contact);worst=Mathf.Max(worst,gap);count++;
                            if(gap>.08f)TestContext.WriteLine("Header at "+sim.State.clock+" player "+actor.id+" seed "+seed+" gap="+gap+" head="+view.HeaderContactPosition+" target="+contact+" root="+view.transform.position+" stature="+db.Find(actor.id).heightCm+" angle="+actor.angle+" intent="+actor.intent+" side="+actor.side+" velocity="+actor.velocity.x+","+actor.velocity.z+" rig="+go.transform.Find("Rig").position);
                        }finally{Object.DestroyImmediate(go);}
                    }
                }
                Assert.IsTrue(sim.State.finished,"Inspect a complete match, including both halves");
            }
            TestContext.WriteLine(count+" natural headers; maximum head-ball gap "+worst+" m");Assert.Greater(count,0,"Exercise natural headers, not only a synthetic pose");Assert.Less(worst,.12f);
        }
    }
}

using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Touchline.Core;

namespace Touchline.Tests
{
    public class SimulatedDuelContactTests
    {
        [Test] public void ActualMatchTacklesMeetTheirBallAtTheRecordedImpact()
        {
            var db=JsonUtility.FromJson<Database>(Resources.Load<TextAsset>("Data/database").text);
            var clubs=db.clubs.Where(c=>c.playable&&c.league=="eng.1").Take(2).ToArray();
            int checkedContacts=0;float worst=0;
            foreach(uint seed in new uint[]{7919,15838,23757,31676}){
                var career=new Career{club=clubs[0].id};career.lineup=Career.Select(db,career.club,career.tactic);
                var sim=MatchSimulation.Create(db,career,clubs[1].id,seed);var views=new PlayerView[22];int eventIndex=0;
                try{
                    for(int tick=0;tick<7200;tick++){
                        if(sim.State.halfTime)sim.ResumeHalf();sim.Advance(.1);var state=sim.State;
                        for(int index=0;index<22;index++){
                            var actor=state.actors[index];if(!((actor.action=="tackle"&&actor.actionKind==MatchSimulation.StandingDuel)||(actor.action=="slide"&&actor.actionKind==MatchSimulation.SlidingDuel)))continue;
                            var view=views[index];if(view==null){var go=new GameObject("Simulated defender "+index);view=views[index]=go.AddComponent<PlayerView>();view.Build(db.Find(actor.id),actor.side,actor.slot,Color.white);go.transform.rotation=Quaternion.Euler(0,actor.angle*Mathf.Rad2Deg,0);}
                            if(view.PlayerId!=actor.id)view.ChangeIdentity(db.Find(actor.id));
                            for(int sample=0;sample<=6;sample++){
                                float alpha=sample/6f;var p=Point.Lerp(state.ball.previous,state.ball.position,alpha);
                                var ball=new Vector3(p.x,Mathf.Lerp(state.ball.previousHeight,state.ball.height,alpha),p.z);
                                view.Render(actor,alpha,1f/60,ball,PlayerMotionContext.From(state,actor,alpha));
                            }
                        }
                        for(;eventIndex<state.events.Count;eventIndex++){
                            var e=state.events[eventIndex];if(e.kind!="tackle")continue;
                            int index=System.Array.FindIndex(state.actors,a=>a.id==e.player);var actor=state.actors[index];var view=views[index];
                            Assert.IsNotNull(view,"A successful tackle must have a preparation pose");
                            Assert.AreEqual(actor.actionKind==MatchSimulation.SlidingDuel?"slide":"tackle",actor.action,"Do not replace the contact pose in the same tick");
                            var impact=new Vector3(actor.actionTarget.x,actor.actionHeight,actor.actionTarget.z);
                            float distance=Mathf.Min(Vector3.Distance(view.BootContactPosition(true),impact),Vector3.Distance(view.BootContactPosition(false),impact));
                            worst=Mathf.Max(worst,distance);checkedContacts++;
                            Assert.Less(distance,.18f,"Natural contact at "+state.clock+" seed "+seed+" player "+actor.id);
                        }
                    }
                }finally{foreach(var view in views)if(view!=null)Object.DestroyImmediate(view.gameObject);}
            }
            TestContext.WriteLine(checkedContacts+" actual match contacts; maximum distance "+worst+" m");
            Assert.GreaterOrEqual(checkedContacts,3,"The test must exercise actual successful interventions");
        }
    }
}

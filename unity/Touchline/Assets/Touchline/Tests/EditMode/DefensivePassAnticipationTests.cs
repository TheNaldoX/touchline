using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Touchline.Core;

namespace Touchline.Tests
{
    public sealed class DefensivePassAnticipationTests
    {
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static MatchSimulation Setup(string kind,int attackingSide,int period,float elapsed,float endpointZ)
        {
            var db=new Database{players=Enumerable.Range(0,40).Select(i=>new PlayerData{id="anticipation-"+i,name="P"+i,team=i<20?"a":"b",position=i%20==0?"GB":i%20<5?"DEF":i%20<9?"MIL":"ATT",rating=70}).ToArray()};
            var career=new Career{club="a"};career.lineup=Career.Select(db,"a",career.tactic);
            var sim=MatchSimulation.Create(db,career,"b",17,2700);var m=sim.State;m.period=period;
            int direction=sim.Direction(attackingSide);var source=m.actors[attackingSide*11+7];var receiver=m.actors[attackingSide*11+9];
            foreach(var actor in m.actors){actor.sentOff=true;actor.velocity=new Point();}
            source.sentOff=receiver.sentOff=false;source.position=source.previous=new Point();receiver.position=receiver.previous=new Point(direction*20,0);
            // Keep the visible attacker positions identical while probing one defensive movement step.
            // The destination is the only hidden field that differs between the two scenarios.
            source.action=receiver.action="kick";source.actionTime=receiver.actionTime=2;
            foreach(int slot in new[]{5,6}){var defender=m.actors[(1-attackingSide)*11+slot];defender.sentOff=false;defender.position=defender.previous=new Point(direction*20,slot==5?2:-2);defender.action="idle";defender.actionTime=0;}
            m.phase="play";m.restart=0;m.clock=600;m.turnoverAt=-100;m.possessionSide=attackingSide;
            m.ball=new BallState{kind=kind,from=source.id,to=receiver.id,side=attackingSide,owner=null,position=new Point(),previous=new Point(),start=new Point(),end=new Point(direction*20,endpointZ),height=.11f,startHeight=.11f,endHeight=.11f,elapsed=elapsed,duration=1.2f,loft=kind=="cross"?3.5f:.08f};
            var tactic=sim.Tactic(1-attackingSide);tactic.pressing=1;tactic.line=1;tactic.counterPress=false;
            return sim;
        }
        static Point Focus(MatchSimulation sim,int side)=>(Point)typeof(MatchSimulation).GetMethod("DefensiveFocus",Private).Invoke(sim,new object[]{side});
        static void Move(MatchSimulation sim)=>typeof(MatchSimulation).GetMethod("Move",Private).Invoke(sim,null);

        [TestCase("pass",0,1)] [TestCase("pass",1,1)] [TestCase("pass",0,2)] [TestCase("pass",1,2)]
        [TestCase("through",0,1)] [TestCase("through",1,1)] [TestCase("through",0,2)] [TestCase("through",1,2)]
        [TestCase("cross",0,1)] [TestCase("cross",1,1)] [TestCase("cross",0,2)] [TestCase("cross",1,2)]
        public void BeforeContactDefensiveFocusUsesOnlyTheVisibleBall(string kind,int side,int period)
        {
            foreach(float elapsed in new[]{-.18f,-.001f}){
                var left=Setup(kind,side,period,elapsed,-10);var right=Setup(kind,side,period,elapsed,10);
                Assert.Less(Point.Distance(Focus(left,1-side),Focus(right,1-side)),.0001f,"La destination aléatoire ne doit pas être connue avant le contact");
                Assert.Less(Point.Distance(Focus(left,1-side),left.State.ball.position),.0001f);
            }
        }
        [TestCase("pass",0,1)] [TestCase("pass",1,1)] [TestCase("pass",0,2)] [TestCase("pass",1,2)]
        [TestCase("through",0,1)] [TestCase("through",1,1)] [TestCase("through",0,2)] [TestCase("through",1,2)]
        [TestCase("cross",0,1)] [TestCase("cross",1,1)] [TestCase("cross",0,2)] [TestCase("cross",1,2)]
        public void BeforeContactTheDefensiveMovementCannotFollowTheFutureEndpoint(string kind,int side,int period)
        {
            var left=Setup(kind,side,period,-.18f,-10);var right=Setup(kind,side,period,-.18f,10);Move(left);Move(right);
            foreach(int slot in new[]{5,6}){
                int index=(1-side)*11+slot;
                Assert.Less(Point.Distance(left.MovementTarget(index),right.MovementTarget(index)),.0001f,"Cible du défenseur avant contact, poste "+slot);
                Assert.AreEqual(left.State.actors[index].intent,right.State.actors[index].intent);
                Assert.AreNotEqual("attack-cross",left.State.actors[index].intent,"Un centre préparé ne constitue pas encore une trajectoire à attaquer");
            }
        }
        [TestCase("pass",0,1)] [TestCase("pass",1,1)] [TestCase("pass",0,2)] [TestCase("pass",1,2)]
        [TestCase("through",0,1)] [TestCase("through",1,1)] [TestCase("through",0,2)] [TestCase("through",1,2)]
        [TestCase("cross",0,1)] [TestCase("cross",1,1)] [TestCase("cross",0,2)] [TestCase("cross",1,2)]
        public void ReleasedPassStillAllowsAnticipation(string kind,int side,int period)
        {
            foreach(float elapsed in new[]{0f,.12f}){
                var left=Setup(kind,side,period,elapsed,-10);var right=Setup(kind,side,period,elapsed,10);
                Assert.Greater(Point.Distance(Focus(left,1-side),Focus(right,1-side)),10,"L'anticipation du ballon parti reste active");
                if(kind=="cross"){
                    Move(left);Assert.IsTrue(new[]{5,6}.Any(slot=>left.State.actors[(1-side)*11+slot].intent=="attack-cross"),"Les défenseurs continuent de disputer les centres après contact");
                }
            }
        }

        [TestCase("pass",0,1,false)] [TestCase("pass",1,1,false)] [TestCase("pass",0,2,false)] [TestCase("pass",1,2,false)]
        [TestCase("through",0,1,false)] [TestCase("through",1,1,false)] [TestCase("through",0,2,false)] [TestCase("through",1,2,false)]
        [TestCase("cross",0,1,false)] [TestCase("cross",1,1,false)] [TestCase("cross",0,2,false)] [TestCase("cross",1,2,false)]
        [TestCase("pass",0,1,true)] [TestCase("pass",1,1,true)] [TestCase("pass",0,2,true)] [TestCase("pass",1,2,true)]
        [TestCase("through",0,1,true)] [TestCase("through",1,1,true)] [TestCase("through",0,2,true)] [TestCase("through",1,2,true)]
        [TestCase("cross",0,1,true)] [TestCase("cross",1,1,true)] [TestCase("cross",0,2,true)] [TestCase("cross",1,2,true)]
        public void ReceiverDoesNotKnowTheRandomEndpointUntilContact(string kind,int side,int period,bool keeper)
        {
            foreach(float elapsed in new[]{-.18f,-.001f,0f,.12f}){
                var left=Setup(kind,side,period,elapsed,-10);var right=Setup(kind,side,period,elapsed,10);
                int index=side*11+(keeper?0:9);
                foreach(var sim in new[]{left,right}){
                    var receiver=sim.State.actors[index];receiver.sentOff=false;receiver.action="run";receiver.actionTime=0;
                    receiver.position=receiver.previous=new Point(sim.Direction(side)*(keeper?-20:20),0);receiver.velocity=new Point(sim.Direction(side)*2,.5f);
                    if(keeper){sim.State.actors[side*11+9].sentOff=true;sim.State.ball.end.x=receiver.position.x;}
                    sim.State.ball.to=receiver.id;Move(sim);
                }
                float gap=Point.Distance(left.MovementTarget(index),right.MovementTarget(index));
                if(elapsed<0){
                    Assert.Less(gap,.0001f,"Avant contact : "+kind+", gardien="+keeper+", temps="+elapsed);
                    Assert.AreNotEqual("receive",left.State.actors[index].intent);
                }else{
                    Assert.Greater(gap,1,"La trajectoire visible après contact reste prise en compte, gardien="+keeper);
                    if(!keeper)Assert.AreEqual("receive",left.State.actors[index].intent);
                }
            }
        }
    }
}

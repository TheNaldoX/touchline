using System.Linq;
using NUnit.Framework;
using Touchline.Core;

namespace Touchline.Tests
{
    // Cohérence des décisions : un joueur qui a déjà orienté son corps pour
    // donner le ballon (passe ou frappe) va au bout de son geste au lieu de
    // repartir en conduite après s'être arrêté et retourné.
    public sealed class PreparedDeliveryCommitmentTests
    {
        const string Ready="foot-delivery-ready"; // MatchSimulation.PreparedFootDelivery
        MatchSimulation sim;Actor carrier,mate,blocker,keeper;
        void Prepare(int side,Point carrierAt,Point blockerAt,Point? mateAt)
        {
            var db=new Database{players=Enumerable.Range(0,40).Select(i=>new PlayerData{id="pd"+i,name="S"+i,team=i<20?"a":"b",position=i%20==0?"GB":i%20<5?"DEF":i%20<9?"MIL":"ATT",rating=80}).ToArray()};
            var c=new Career{club="a"};c.lineup=Career.Select(db,"a",c.tactic);sim=MatchSimulation.Create(db,c,"b",29,2700);
            sim.State.restart=0;sim.State.phase="play";int dir=sim.Direction(side);
            foreach(var p in sim.State.actors){p.sentOff=true;p.position=p.previous=new Point(0,30);p.velocity=new Point();}
            carrier=sim.State.actors[side*11+(mateAt.HasValue?6:9)];mate=sim.State.actors[side*11+9];
            blocker=sim.State.actors[(1-side)*11+3];keeper=sim.State.actors[(1-side)*11];var deep=sim.State.actors[(1-side)*11+2];
            foreach(var p in new[]{carrier,blocker,keeper,deep})p.sentOff=false;
            carrier.position=carrier.previous=carrierAt*dir;carrier.angle=dir*(float)System.Math.PI/2;
            blocker.position=blocker.previous=blockerAt*dir;keeper.position=keeper.previous=new Point(dir*51,0);
            deep.position=deep.previous=new Point(dir*45,25); // tient la ligne du hors-jeu loin derrière
            if(mateAt.HasValue){mate.sentOff=false;mate.position=mate.previous=mateAt.Value*dir;}
            sim.State.ball=new BallState{owner=carrier.id,side=side,lastTouch=side,position=carrier.position,previous=carrier.position,height=.11f};
            sim.State.possessionSide=side;sim.State.carryTime=0;
        }
        [TestCase(0)] [TestCase(1)]
        public void PreparedShotIsTakenWhenADefenderStepsIn(int side)
        {
            // Défenseur à 1 m devant, axe du but : sans préparation, l'attaquant ne frappe pas.
            Prepare(side,new Point(41,0),new Point(42,0),null);
            Assert.AreNotEqual("shot",sim.Decide(carrier));Assert.AreEqual(0,sim.State.shots[side]);
            // Corps déjà orienté vers le centre du but : il va au bout de sa frappe.
            Prepare(side,new Point(41,0),new Point(42,0),null);int dir=sim.Direction(side);
            carrier.actionKind=Ready;carrier.actionTarget=new Point(dir*52.5f,0);
            Assert.AreEqual("shot",sim.Decide(carrier));Assert.AreEqual(1,sim.State.shots[side]);Assert.AreEqual("shot",sim.State.ball.kind);
        }
        [TestCase(0,9)] [TestCase(0,-9)] [TestCase(1,9)]
        public void PreparedPassIsPlayedInsteadOfCarryingAway(int side,float lateral)
        {
            // Pressé à 2 m, le milieu préfère d'ordinaire conduire pour éliminer.
            Prepare(side,new Point(14,0),new Point(16,0),new Point(22,lateral));
            Assert.AreEqual("carry",sim.Decide(carrier));
            // Une fois tourné vers son partenaire, il lui donne le ballon.
            Prepare(side,new Point(14,0),new Point(16,0),new Point(22,lateral));
            carrier.actionKind=Ready;
            Assert.AreEqual("pass",sim.Decide(carrier));Assert.AreEqual(mate.id,sim.State.ball.to);
        }
    }
}

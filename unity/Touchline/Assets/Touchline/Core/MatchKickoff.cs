using System;
using System.Linq;

namespace Touchline.Core
{
    public sealed partial class MatchSimulation
    {
        Actor KickoffTaker(int side)=>State.actors[side*11+9].sentOff?State.actors.First(p=>p.side==side&&!p.sentOff&&p.slot>0):State.actors[side*11+9];
        Point KickoffPosition(Actor player,Actor taker,int side)
        {
            if(player==taker)return new Point(Direction(side)*.42f,0);
            var shape=Tactic(player.side).Position(player.slot,false,0);var q=new Point(Math.Min(-5,shape.x),shape.z)*Direction(player.side);
            if(player.side!=side&&q.Length<9.2f)q=q.Normalized*9.2f;
            return q;
        }
        // Initial presentation and the interval are explicit breaks. Following
        // a goal, the same destinations are reached through normal movement.
        void PlaceKickoffPlayers(int side)
        {
            var taker=KickoffTaker(side);
            foreach(var p in State.actors){if(p.sentOff)continue;p.position=KickoffPosition(p,taker,side);p.previous=p.position;p.velocity=new Point();p.action="idle";p.actionTime=0;p.angle=(p==taker?-1:1)*Direction(p.side)*(float)Math.PI/2;}
        }
        void MoveKickoffRestart()
        {
            var m=State;var taker=KickoffTaker(m.restartSide);bool ready=true;
            foreach(var p in m.actors){if(p.sentOff)continue;var target=KickoffPosition(p,taker,m.restartSide);p.intent="restart-shape";MoveActor(p,target,4.8f);
                if(p==taker){if(Point.Distance(p.position,target)>.3f||p.velocity.Length>.6f)ready=false;}
                else if(p.position.x*Direction(p.side)>0)ready=false;
                if(p.side!=m.restartSide&&p.position.Length<9.15f)ready=false;
            }
            if(m.phase=="kickoff"&&m.restart<.11f&&!ready)m.restart=.2f;
        }
        void Kickoff(int side)
        {
            var m=State;var starter=KickoffTaker(side);m.ball=new BallState{position=new Point(),previous=new Point(),side=side,lastTouch=side,lastTouchId=starter.id};
            m.phase="play";m.restart=0;m.decision=.6f;m.carryTime=0;m.possessionSide=side;m.turnoverAt=-100;
            Actor support=null;float best=float.MaxValue;foreach(var p in m.actors){if(p.sentOff||p.side!=side||p==starter||p.slot==0)continue;float d=Point.Distance(p.position,starter.position);if(d>2&&d<best){support=p;best=d;}}
            if(support!=null)Pass(starter,support,"pass");else Flight(starter,null,"clearance",new Point(Direction(side)*12,0),.11f,.7f,.1f);
            m.ball.start=new Point();m.ball.fixedStart=true;starter.actionTarget=m.ball.start;
            Emit("kickoff",side,starter.id,Data(starter).name+" donne le coup d’envoi.",support?.id);
        }
    }
}

using System;

namespace Touchline.Core
{
    public sealed partial class MatchSimulation
    {
        void DeliverySupport(Actor player,Actor owner,ref Point target)
        {
            var ball=State.ball;var tactic=Tactic(player.side);var slot=tactic.withBall[player.slot];
            if(player==owner||player.slot==0||slot.duty=="defend")return;
            bool flying=owner==null&&(ball.kind=="cross"||ball.kind=="cutback")&&ball.side==player.side;
            if(!flying&&(owner==null||owner.side!=player.side))return;
            var origin=flying?ball.start:owner.position;int dir=Direction(player.side);float depth=origin.x*dir,lateral=origin.z*dir;
            if(depth<27||Math.Abs(lateral)<18)return;float flank=Math.Sign(lateral);
            if(slot.role=="ST"){
                bool far=(50-slot.x)*flank< -8;
                target=new Point(Mathx.Clamp(depth+(far?3:7),42,far?46.5f:48.5f),flank*(far?-5.5f:3.2f));player.intent=far?"far-post":"near-post";return;
            }
            bool winger=slot.role=="LW"||slot.role=="RW"||slot.role=="LM"||slot.role=="RM";
            if(winger&&(50-slot.x)*flank< -12){target=new Point(Mathx.Clamp(depth+3,40,46.5f),-flank*(4.5f+tactic.width*2));player.intent="far-post";return;}
            if(depth<33||(slot.role!="CM"&&slot.role!="AM"))return;
            // One midfielder arrives late. The holding players retain their
            // assigned cover and are not all pulled into the penalty area.
            int runner=-1;float best=float.MinValue;
            for(int i=1;i<11;i++){
                var candidate=State.actors[player.side*11+i];var role=tactic.withBall[i];
                if(candidate.sentOff||candidate==owner||role.duty=="defend"||(role.role!="CM"&&role.role!="AM"))continue;
                float score=role.y-Math.Abs(role.x-50)*.05f+((50-role.x)*flank>0?1:0);
                if(score>best){best=score;runner=i;}
            }
            if(player.slot==runner){target=new Point(Mathx.Clamp(depth-3,32,40),-flank*1.5f);player.intent="box-arrival";}
        }
    }
}

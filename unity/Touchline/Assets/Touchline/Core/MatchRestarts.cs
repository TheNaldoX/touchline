using System;
using System.Linq;

namespace Touchline.Core
{
    public sealed partial class MatchSimulation
    {
        // Read-only broadcast cue: show the approach only once the taker and wall
        // are within two metres of their assigned marks. This does not authorize a kick.
        public bool FreeKickDeliveryApproaching
        {
            get{
                var m=State;if(m.phase!="free-kick"||m.restart<=0)return false;
                var taker=Find(m.restartTaker);if(taker==null||taker.sentOff||Point.Distance(taker.position,RestartPosition(taker,taker))>2)return false;
                for(int i=0;i<m.restartWall.Count;i++){var p=Find(m.restartWall[i]);if(p==null||p.sentOff||Point.Distance(p.position,WallPosition(i))>2)return false;}
                return true;
            }
        }
        float FreeKickSkill(Actor p)=>Skill(p,Data(p).attributes?.Any(a=>a.key=="freeKickAccuracy")==true?"freeKickAccuracy":"fkAccuracy");
        Actor RestartTaker()
        {
            var existing=Find(State.restartTaker);
            if(existing!=null&&!existing.sentOff&&!GroundedAction(existing)&&existing.side==State.restartSide)return existing;
            Actor best=null;float highest=float.MinValue;
            bool attackingKick=State.phase=="free-kick"&&State.ball.position.x*Direction(State.restartSide)>20&&!State.indirectRestart;
            foreach(var p in State.actors){if(p.sentOff||GroundedAction(p)||p.side!=State.restartSide)continue;
                float distance=Point.Distance(p.position,State.ball.position);
                float score=State.phase=="penalty"?Skill(p,"penalties"):State.phase=="goal-kick"&&p.slot==0?1000:-distance;
                if(State.phase!="goal-kick"&&p.slot==0)score-=1000;
                if(attackingKick)score=FreeKickSkill(p)-distance*.25f-(p.slot==0?1000:0);
                if(State.phase=="corner")score=Skill(p,"crossing")-distance*.25f-(p.slot==0?1000:0);
                if(score>highest){highest=score;best=p;}
            }
            best=DesignatedTaker(best);State.restartTaker=best?.id;
            State.restartWall??=new System.Collections.Generic.List<string>();State.restartWall.Clear();
            var goal=new Point(Direction(State.restartSide)*52.5f,0);
            float range=Point.Distance(State.ball.position,goal);
            if(attackingKick&&range<32&&range>12&&Math.Abs(State.ball.position.z)<24){
                var centre=State.ball.position+(goal-State.ball.position).Normalized*9.6f;
                foreach(var defender in State.actors.Where(p=>p.side!=State.restartSide&&!p.sentOff&&!GroundedAction(p)&&p.slot>0).OrderBy(p=>Point.Distance(p.position,centre)).Take(range<25?4:3))State.restartWall.Add(defender.id);
            }
            return best;
        }
        Point WallPosition(int index)
        {
            var direction=(new Point(Direction(State.restartSide)*52.5f,0)-State.ball.position).Normalized;
            return State.ball.position+direction*9.6f+new Point(-direction.z,direction.x)*((index-(State.restartWall.Count-1)*.5f)*.8f);
        }
        Point RestartPosition(Actor p,Actor taker)
        {
            var m=State;int attack=Direction(m.restartSide);var ball=m.ball.position;
            var q=p==taker?ball:Tactic(p.side).Position(p.slot,p.side==m.restartSide,ball.x*Direction(p.side))*Direction(p.side);
            if(p==taker&&m.phase!="throw-in")q-=new Point(attack*.42f,0);
            if(CornerPosition(p,taker,ref q)){}
            else if(m.phase=="corner"&&p!=taker&&p.slot>4)q=new Point(attack*(p.side==m.restartSide?41:44),((p.slot%5)-2)*4);
            if(m.phase=="penalty"&&p!=taker){
                return p.side!=m.restartSide&&p.slot==0?new Point(attack*52.5f,0):new Point(attack*Math.Min(q.x*attack,32),q.z);
            }
            int wallIndex=m.restartWall.IndexOf(p.id);
            if(wallIndex>=0)return WallPosition(wallIndex);
            if(p.side!=m.restartSide){
                if(m.phase=="goal-kick"){if(q.x*attack< -35.5f&&Math.Abs(q.z)<20.5f)q.x=-attack*35.5f;}
                else{float distance=m.phase=="throw-in"?2.2f:9.5f;var delta=q-ball;if(delta.Length<distance){if(delta.Length<.01f)delta=new Point(-attack,0);q=ball+delta.Normalized*distance;}}
            }
            if(p.side==m.restartSide&&p!=taker&&m.restartWall.Count>=3){
                // Leave the wall clear; its members are approached through their actual movement.
                for(int i=0;i<m.restartWall.Count;i++){var centre=WallPosition(i);if(Point.Distance(q,centre)<1.5f)q=centre+new Point(-attack*1.6f,0);}
            }
            return q;
        }
        void MoveRestart()
        {
            var m=State;if(m.phase=="kickoff"||m.phase=="goal"){MoveKickoffRestart();return;}
            var taker=RestartTaker();bool ready=taker!=null;int attack=Direction(m.restartSide);
            foreach(var p in m.actors){if(p.sentOff)continue;var q=RestartPosition(p,taker);p.intent=m.restartWall.Contains(p.id)?"wall":"restart-shape";MoveActor(p,q,4.8f);
                if(p==taker){if(Point.Distance(p.position,q)>.3f||p.velocity.Length>.7f)ready=false;continue;}
                if(m.phase=="penalty"){
                    if(p.side!=m.restartSide&&p.slot==0){if(Math.Abs(p.position.x-attack*52.5f)>.01f||Math.Abs(p.position.z)>3.2f)ready=false;}
                    else if(p.position.x*attack>35.9f||Point.Distance(p.position,m.ball.position)<9.15f)ready=false;
                }else if(m.phase=="goal-kick"&&p.side!=m.restartSide){if(p.position.x*attack< -36&&Math.Abs(p.position.z)<20.16f)ready=false;
                }else if(p.side!=m.restartSide){
                    float minimum=m.phase=="throw-in"?2:9.15f;
                    bool onGoalLine=p.slot==0&&Math.Abs(p.position.x)>52.45f&&Math.Abs(p.position.z)<3.66f;
                    if(!onGoalLine&&Point.Distance(p.position,m.ball.position)<minimum)ready=false;
                }
                if(m.restartWall.Contains(p.id)&&Point.Distance(p.position,q)>.3f)ready=false;
            }
            if(m.restart<.11f&&!ready)m.restart=.2f;
        }
        void RestartKick()
        {
            var m=State;var phase=m.phase;var p=RestartTaker();var spot=m.ball.position;Control(p);m.phase="play";m.decision=.6f;
            m.ball.restartExemption=phase=="goal-kick"||phase=="corner"||phase=="throw-in";
            if(phase=="penalty")Shoot(p,false,true);
            else if(phase=="free-kick"&&!m.indirectRestart&&DirectFreeKick(p)){
                var receiver=Routine(p.side).freeKick=="shoot"?null:FreeKickCombination(p);
                if(receiver==null)Shoot(p,false,false,true);else Pass(p,receiver,"pass");
            }
            else if(PlayShort(p,phase)||phase=="free-kick"&&CrossFreeKick(p)){}
            else{
                Actor target=null;float best=float.MinValue;string delivery=phase=="corner"?"cross":phase=="throw-in"?"throw":"pass";
                int direction=Direction(p.side);bool wideFreeKick=phase=="free-kick"&&p.position.x*direction>16&&Math.Abs(p.position.z)>=18;
                foreach(var a in m.actors){if(a.sentOff||GroundedAction(a)||a.side!=p.side||a==p)continue;float distance=Point.Distance(a.position,p.position);if(distance<2)continue;
                    float score=phase=="corner"?Skill(a,"headingAccuracy")-Point.Distance(a.position,CornerAim(p))-(a.slot==0?1000:0):Space(a.position,1-p.side)*.6f-distance;
                    if(phase=="free-kick"&&a.position.x*Direction(p.side)>OffsideLine(p.side)+.1f)continue;
                    string candidate=phase=="corner"?"cross":phase=="throw-in"?"throw":"pass";
                    // From the flank, compare a credible box delivery with the short pass.
                    // Reuse the live crossing assessment (receiver, marker, keeper and tactic).
                    if(wideFreeKick&&a.slot>0&&a.position.x*direction>32&&Math.Abs(a.position.z)<20&&(a.position.x-p.position.x)*direction>3){
                        float outlook=CrossOutlook(p,a,LowCross(p,a));
                        const float MinimumRestartCrossOutlook=.5f; // Estimated contest share, 0–1.
                        float crossScore=outlook*24-distance*.25f; // Utility points, distance in metres.
                        if(outlook>=MinimumRestartCrossOutlook&&crossScore>score){score=crossScore;candidate="cross";}
                    }
                    if(score>best){best=score;target=a;delivery=candidate;}
                }
                if(target!=null)Pass(p,target,delivery);
                else Flight(p,null,"clearance",new Point(Direction(p.side)*8,Math.Sign(p.position.z+.01f)*27),.11f,2.6f,8);
            }
            if(phase!="throw-in"){m.ball.start=spot;m.ball.fixedStart=true;p.actionTarget=spot;}
        }
        Actor FreeKickCombination(Actor taker)
        {
            // A set-piece specialist retains the shot unless a legal, open pass
            // offers at least twice its geometric chance. No random variety for its own sake.
            const float ChanceAdvantage=2f,MinimumLaneSafety=.65f,MaximumCombinationDistance=24f;
            float best=ShotQuality(taker)*(.75f+FreeKickSkill(taker)/100)*ChanceAdvantage;
            Actor receiver=null;int dir=Direction(taker.side);float line=OffsideLine(taker.side);
            foreach(var mate in State.actors){
                if(mate.side!=taker.side||mate==taker||mate.slot==0||mate.sentOff||GroundedAction(mate)||mate.position.x*dir>line+.1f)continue;
                float distance=Point.Distance(taker.position,mate.position);
                if(distance<2||distance>MaximumCombinationDistance||(mate.position.x-taker.position.x)*dir<3)continue;
                float safety=Safety(taker,mate.position);if(safety<MinimumLaneSafety)continue;
                float chance=ShotQuality(mate)*safety;
                if(chance>best){best=chance;receiver=mate;}
            }
            return receiver;
        }
    }
}

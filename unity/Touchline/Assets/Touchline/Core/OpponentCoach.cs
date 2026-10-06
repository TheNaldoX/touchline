using System;
using System.Linq;

namespace Touchline.Core
{
    public sealed partial class MatchSimulation
    {
        // Tactical reviews are persisted; substitutions wait for a stoppage and
        // share one window when several players enter together.
        void ReviewOpponent()
        {
            var m=State;bool tacticalDue=m.clock>=m.awayTacticalReviewAt;
            bool replacementDue=m.clock>=m.awayReviewAt||m.actors.Any(a=>a.side==1&&a.injured&&!a.sentOff);
            if(!tacticalDue&&!replacementDue)return;
            int margin=m.score[1]-m.score[0];string plan=margin<0?"chase":margin>0?"protect":"balanced";
            var tactic=m.awayTactic;float urgency=Mathx.Clamp((m.clock/m.SecondsPerMinute-52.5f)/37.5f,0,1);
            if(tacticalDue){
            int crosses=m.metrics[0].crosses-m.awayObservedCrosses,through=m.metrics[0].throughBalls-m.awayObservedThrough;
            // React to observed patterns over a review window, not to the
            // opponent's next random decision or a hidden future ball path.
            tactic.defensiveWidth=Mathx.Clamp(m.awayBaseDefensiveWidth+(crosses>=4?.10f:0),.2f,.8f);
            tactic.line=Mathx.Clamp(m.awayBaseLine+(margin<0?.12f:margin>0?-.08f:0)*urgency-(through>=3?.06f:0),.15f,.85f);
            tactic.tempo=Mathx.Clamp(m.awayBaseTempo+(margin<0?.22f:margin>0?-.10f:0)*urgency,.15f,.9f);
            tactic.mentality=Mathx.Clamp(m.awayBaseRisk+(margin<0?.25f:margin>0?-.17f:0)*urgency,.15f,.9f);
            float fitness=0;int countFit=0;foreach(var a in m.actors)if(a.side==1&&!a.sentOff&&a.slot>0){fitness+=a.fitness;countFit++;}
            tactic.pressing=Mathx.Clamp(m.awayBasePress-(countFit>0&&fitness/countFit<65?.12f:0)+(margin<0?.1f*urgency:0),.2f,.85f);
            if(crosses>=4)Emit("opponent-adjustment",1,null,"L’adversaire élargit sa couverture pour limiter les centres.");
            else if(through>=3)Emit("opponent-adjustment",1,null,"L’adversaire protège davantage la profondeur.");
            m.awayObservedCrosses=m.metrics[0].crosses;m.awayObservedThrough=m.metrics[0].throughBalls;
            m.awayTacticalReviewAt=m.clock+10*m.SecondsPerMinute;
            if(plan!=m.awayPlan){m.awayPlan=plan;Emit("opponent-plan",1,null,plan=="chase"?"L’adversaire remonte son bloc et accélère pour revenir au score.":plan=="protect"?"L’adversaire abaisse son bloc et temporise pour protéger son avance.":"L’adversaire retrouve une approche équilibrée.");}
            }
            if(!replacementDue)return;
            if(m.restart<=0)return;
            m.awayReviewAt=m.clock+10*m.SecondsPerMinute;
            if(m.substitutions[1]>=5||m.awayWindows.Count>=3)return;
            int count=0;
            foreach(var actor in m.actors.Where(a=>!a.sentOff&&a.side==1&&a.slot>0).OrderBy(a=>a.fitness).ToArray()){
                if(count>=2||m.substitutions[1]>=5)break;
                if(!actor.injured&&(actor.fitness>90||Data(actor).fitness-actor.fitness<6))continue;
                var role=tactic.withoutBall[actor.slot].role;
                var replacement=roster.Values.Where(p=>p.unavailableDays<=0&&p.team==m.away&&!m.used.Contains(p.id)&&!p.Goalkeeper&&p.Fit(role)>=.65f&&p.fitness>actor.fitness+5)
                    .OrderByDescending(p=>p.rating*p.Fit(role)*(.55f+p.fitness*.0045f)).ThenBy(p=>p.id,StringComparer.Ordinal).FirstOrDefault();
                if(replacement==null)continue;
                float current=Data(actor).rating*Data(actor).Fit(role)*(.55f+actor.fitness*.0045f);
                if(!actor.injured&&replacement.rating*replacement.Fit(role)*(.55f+replacement.fitness*.0045f)<current*.94f)continue;
                Substitute(1,actor.slot,replacement.id);count++;
            }
        }
    }
}

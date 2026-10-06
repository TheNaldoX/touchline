using System;
using System.Linq;

namespace Touchline.Core
{
    public sealed partial class MatchSimulation
    {
        // Called only by explicit "delegate the rest". Manual simulation never
        // changes the manager's eleven, formation or instructions automatically.
        public void AdvanceDelegatedStep()
        {
            ReviewDelegatedSubstitutions();
            if(State.halfTime)ResumeHalf();
            Advance(.1);
        }
        public void ReviewDelegatedSubstitutions()
        {
            var m=State;if(m.finished)return;
            if(!m.halfTime&&m.restart<=0)return;
            // The scheduled restart end identifies this stoppage, even if the user
            // cancelled delegation and later returns during another stoppage.
            float stopUntil=m.clock+m.restart;
            if(m.halfTime?m.delegatedHalfReviewed:Math.Abs(m.delegatedStopUntil-stopUntil)<.05f)return;
            bool injured=m.actors.Any(a=>a.side==0&&!a.sentOff&&a.injured);
            if(!injured&&!m.halfTime&&(m.Minute<55||m.clock<m.delegatedReviewAt))return;
            // Honour the manager's previously prepared changes first. They are
            // applied by the normal simulation and consume their normal quota.
            if(m.pendingSubstitutions.Any(p=>p.side==0)){if(m.halfTime)m.delegatedHalfReviewed=true;else m.delegatedStopUntil=stopUntil;return;}
            if(m.halfTime)m.delegatedHalfReviewed=true;else m.delegatedStopUntil=stopUntil;
            m.delegatedReviewAt=m.clock+10*m.SecondsPerMinute;
            if(m.substitutions[0]>=5||(!m.halfTime&&m.homeWindows.Count>=3&&!m.homeWindows.Contains(m.clock)))return;
            int routine=0;
            foreach(var actor in m.actors.Where(a=>a.side==0&&!a.sentOff).OrderByDescending(a=>a.injured).ThenBy(a=>a.fitness).ThenBy(a=>a.slot).ToArray()){
                if(m.substitutions[0]>=5)break;
                if(!actor.injured&&(actor.slot==0||routine>=2||actor.fitness>85||Data(actor).fitness-actor.fitness<8))continue;
                string role=m.homeTactic.withoutBall[actor.slot].role;bool keeper=actor.slot==0;
                var replacement=roster.Values.Where(p=>p.team==m.home&&p.unavailableDays<=0&&!m.used.Contains(p.id)&&p.Goalkeeper==keeper&&p.Fit(role)>=.65f&&(actor.injured||p.fitness>actor.fitness+5))
                    .OrderByDescending(p=>p.rating*p.Fit(role)*(.55f+p.fitness*.0045f)).ThenBy(p=>p.id,StringComparer.Ordinal).FirstOrDefault();
                if(replacement==null)continue;
                float current=Data(actor).rating*Data(actor).Fit(role)*(.55f+actor.fitness*.0045f);
                if(!actor.injured&&replacement.rating*replacement.Fit(role)*(.55f+replacement.fitness*.0045f)<current*.94f)continue;
                if(!actor.injured)routine++;
                Substitute(0,actor.slot,replacement.id);
            }
        }
    }
}

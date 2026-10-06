using UnityEngine;

namespace Touchline
{
    public sealed partial class MatchArena
    {
        public bool AutomaticGoalReplays {get;private set;}=true;
        public float GoalReplaySpeed {get;private set;}=1;
        void InitializeReplaySettings()
        {
            AutomaticGoalReplays=PlayerPrefs.GetInt("match-goal-replays",1)!=0;
            GoalReplaySpeed=1;
        }
        public void SetAutomaticGoalReplays(bool enabled)
        {
            if(!enabled)SkipGoalReplay();
            if(AutomaticGoalReplays!=enabled)ClearUnseenGoalReplay();
            AutomaticGoalReplays=enabled;
            PlayerPrefs.SetInt("match-goal-replays",enabled?1:0);
        }
        public void SetGoalReplaySpeed(float speed)=>GoalReplaySpeed=speed==.5f?.5f:1;
    }
}

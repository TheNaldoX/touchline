using System.Collections.Generic;
using UnityEngine;
using Touchline.Core;

namespace Touchline
{
    public sealed partial class MatchArena
    {
        GoalReplayPoses goalReplay;int replayKnownScore;float pendingGoalClock=-1;
        Vector3 replayCameraPosition;Quaternion replayCameraRotation;
        readonly string[] replayPlayerIds=new string[22];
        public bool GoalReplayActive=>goalReplay!=null&&goalReplay.Active;
        public float GoalReplayProgress=>goalReplay?.Progress??0;
        public long GoalReplayPayloadBytes=>goalReplay?.EstimatedPayloadBytes??0;
        void ResetGoalReplay(MatchState state){InitializeReplaySettings();goalReplay=null;pendingGoalClock=-1;replayKnownScore=state.score[0]+state.score[1];}
        void BuildGoalReplay()
        {
            var transforms=new List<Transform>();
            for(int i=0;i<players.Length;i++){transforms.AddRange(players[i].GetComponentsInChildren<Transform>(true));replayPlayerIds[i]=players[i].PlayerId;}
            transforms.Add(ball);transforms.Add(MatchCamera.transform);goalReplay=new GoalReplayPoses(transforms.ToArray(),MatchCamera);
        }
        void ClearUnseenGoalReplay(){goalReplay?.Reset();pendingGoalClock=-1;replayKnownScore=Simulation.State.score[0]+Simulation.State.score[1];}
        void CaptureGoalReplay(MatchState state,float alpha)
        {
            if(goalReplay==null)return;
            if(!AutomaticGoalReplays){ClearUnseenGoalReplay();return;}
            for(int i=0;i<players.Length;i++)if(replayPlayerIds[i]!=players[i].PlayerId){ClearUnseenGoalReplay();for(int j=0;j<players.Length;j++)replayPlayerIds[j]=players[j].PlayerId;break;}
            int score=state.score[0]+state.score[1];
            if(score>replayKnownScore)pendingGoalClock=state.clock;
            replayKnownScore=score;
            float time=state.clock-.1f+alpha*.1f;goalReplay.Capture(time);
            if(pendingGoalClock<0||state.clock-pendingGoalClock<.7f&&!state.finished&&!state.halfTime)return;
            // Freeze only presentation; the authoritative live match is untouched.
            // Force a final sample so Skip restores the exact latest displayed pose.
            pendingGoalClock=-1;goalReplay.Capture(time,true);
            if(goalReplay.Begin()){RememberReplayCamera();foreach(var line in tacticalLines)line.enabled=false;}
        }
        void AdvanceGoalReplay(float elapsed)
        {
            goalReplay.Advance(elapsed*GoalReplaySpeed);
            if(!goalReplay.Active){RestoreAfterGoalReplay();return;}
            RememberReplayCamera();ReframeGoalReplayCamera();
        }
        void RememberReplayCamera(){replayCameraPosition=MatchCamera.transform.position;replayCameraRotation=MatchCamera.transform.rotation;}
        void ReframeGoalReplayCamera()
        {
            // Restore the sampled pose even if the viewport folds back before
            // playback advances. The live camera focus remains frozen.
            MatchCamera.transform.SetPositionAndRotation(replayCameraPosition,replayCameraRotation);
            MatchCamera.farClipPlane=goalReplay.RecordedCameraFarClip;
            if(Mathf.Abs(MatchCamera.aspect-goalReplay.RecordedCameraAspect)<.002f)return;
            var point=ball.position;bool portrait=MatchCamera.aspect<.8f;
            var center=tactical?new Vector3(0,.6f,0):new Vector3(Mathf.Clamp(point.x*.9f,-46,46),.6f,Mathf.Clamp(point.z*(portrait?.72f:.58f),portrait?-25:-19,portrait?25:19));
            BroadcastFraming.Apply(MatchCamera,center,point,tactical,Mathf.Abs(point.x)>28,point.x>=0?1:-1,zoom);
        }
        void RestoreAfterGoalReplay(){MatchCamera.farClipPlane=goalReplay.RecordedCameraFarClip;cameraReset=true;poseCache.Reset();foreach(var line in tacticalLines)line.enabled=showTactics;}
        public void SkipGoalReplay(){if(!GoalReplayActive)return;goalReplay.Skip();RestoreAfterGoalReplay();}
    }
}

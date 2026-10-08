using System.Collections.Generic;
using UnityEngine;
using Touchline.Core;

namespace Touchline
{
    public sealed partial class MatchArena
    {
        GoalReplayPoses goalReplay;int replayKnownScore;float pendingGoalClock=-1;
        readonly GoalReplayCamera replayCamera=new GoalReplayCamera();float liveFieldOfView;
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
            if(goalReplay.Begin()){liveFieldOfView=MatchCamera.fieldOfView;replayCamera.Begin(ball.position.x,ball.position);foreach(var line in tacticalLines)line.enabled=false;}
        }
        void AdvanceGoalReplay(float elapsed)
        {
            goalReplay.Advance(elapsed*GoalReplaySpeed);
            if(!goalReplay.Active){RestoreAfterGoalReplay();return;}
            // Plan de ralenti (GoalReplayCamera) : suit le ballon rejoué en temps réel,
            // à la place de la pose enregistrée de la caméra télé. Le direct reste figé.
            replayCamera.Advance(ball.position,elapsed);ReframeGoalReplayCamera();
            foreach(var ripple in netRipples)ripple?.Advance(ball.position,true,elapsed*GoalReplaySpeed); // le filet se creuse aussi au ralenti
        }
        // Rappelable sans faire avancer le plan (changement de format en cours de ralenti).
        void ReframeGoalReplayCamera(){replayCamera.Apply(MatchCamera);MatchCamera.farClipPlane=goalReplay.RecordedCameraFarClip;}
        void RestoreAfterGoalReplay(){MatchCamera.fieldOfView=liveFieldOfView;MatchCamera.farClipPlane=goalReplay.RecordedCameraFarClip;cameraReset=true;poseCache.Reset();foreach(var line in tacticalLines)line.enabled=showTactics;}
        public void SkipGoalReplay(){if(!GoalReplayActive)return;goalReplay.Skip();RestoreAfterGoalReplay();}
    }
}

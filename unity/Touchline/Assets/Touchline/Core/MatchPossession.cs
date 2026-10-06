namespace Touchline.Core
{
    public sealed partial class MatchSimulation
    {
        public static int PossessionSide(MatchState match)
        {
            var ball=match.ball;
            if(string.IsNullOrEmpty(ball.owner)&&(ball.kind=="loose"||ball.kind=="none")){
                // A touch determines the restart, not secure possession.
                if(match.possessionSide>=0)return match.possessionSide;
                if(ball.goalAttempt)return ball.shotSide;
            }
            return ball.side;
        }
    }
}
